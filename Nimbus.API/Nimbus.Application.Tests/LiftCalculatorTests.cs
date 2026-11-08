using Nimbus.Domain.Services;
namespace Nimbus.Application.Tests;

/// <summary>
///     NOT verified against the Cameron manual (Amendment 18, Appendix A2-1). Expected values were derived
///     independently from ISA physics: rho = p / (287.05 * T), p = 101325 * (1 - 0.0065 h / 288.15)^5.255877,
///     T(alt) = T0 - 0.0065 * (alt - elevation), lift = V * (rho_amb - rho_int).
/// </summary>
public class LiftCalculatorTests
{
    // Sea level, 15 C ambient, 100 C internal, 1000 m3:
    // rho_amb = 101325 / (287.05 * 288.15) = 1.22498; rho_int = 101325 / (287.05 * 373.15) = 0.94594
    // difference 0.27904 kg/m3 -> 279.05 kg.
    [Test]
    public void SeaLevelIsaDay_100C() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 100, 1000), Is.EqualTo(279.05).Within(0.1));

    // 1000 m AMSL from sea level: T_amb = 15 - 6.5 = 8.5 C (281.65 K); p = 101325 * (1 - 0.0065*1000/288.15)^5.255877 = 89874 Pa.
    // 3000 * 89874/287.05 * (1/281.65 - 1/373.15) = 817.76 kg.
    [Test]
    public void ClimbTo1000m() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(1000, 0, 15, 100, 3000), Is.EqualTo(817.76).Within(0.5));

    // Site at 300 m, max 1500 m (1200 m climb): T_amb = 20 - 7.8 = 12.2 C; pressure from 1500 m AMSL; internal 90 C; 4000 m3.
    [Test]
    public void ElevatedSite_90C() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(1500, 300, 20, 90, 4000), Is.EqualTo(884.63).Within(0.5));

    [Test]
    public void InternalTemperatureAbove100_IsClampedTo100() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 250, 1000),
            Is.EqualTo(LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 100, 1000)));

    // Clamped to 0 C: 1000 * 101325/287.05 * (1/288.15 - 1/273.15) = -67.27 kg (internal colder than ambient).
    [Test]
    public void InternalTemperatureBelow0_IsClampedTo0()
    {
        var lift = LiftCalculator.CalculateTotalLiftKg(0, 0, 15, -20, 1000);
        Assert.That(lift, Is.EqualTo(LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 0, 1000)));
        Assert.That(lift, Is.EqualTo(-67.27).Within(0.1));
    }

    [Test]
    public void ZeroVolume_GivesZeroLift() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(1000, 0, 15, 100, 0), Is.Zero);

    [Test]
    public void LiftScalesLinearlyWithVolume()
    {
        var one = LiftCalculator.CalculateTotalLiftKg(500, 100, 15, 95, 1000);
        Assert.That(LiftCalculator.CalculateTotalLiftKg(500, 100, 15, 95, 2500), Is.EqualTo(one * 2.5).Within(1e-9));
    }

    [Test]
    public void NegativeVolume_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 100, -1));

    [Test]
    public void NonFiniteInput_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LiftCalculator.CalculateTotalLiftKg(double.NaN, 0, 15, 100, 1000));
}
