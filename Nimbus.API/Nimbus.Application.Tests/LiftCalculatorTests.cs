using Nimbus.Domain.Services;
namespace Nimbus.Application.Tests;

/// <summary>
///     Expected values computed independently (Python) from the Cameron manual formula (issue #10):
///     Ta = Tg - 0.0065 (A - Eg); P = 1013.25 (1 - 0.0065 A / 288.15)^5.256 hPa;
///     L = 0.3484 V P (1/(Ta + 273.15) - 1/(Ti + 273.15)).
/// </summary>
public class LiftCalculatorTests
{
    // Sea level, 15 C ambient, 100 C internal, 1000 m3:
    // P = 1013.25 hPa; 0.3484 * 1000 * 1013.25 * (1/288.15 - 1/373.15) = 279.07 kg.
    [Test]
    public void SeaLevelIsaDay_100C() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 100, 1000), Is.EqualTo(279.07).Within(0.05));

    // 1000 m AMSL from sea level: Ta = 15 - 6.5 = 8.5 C (281.65 K); P = 1013.25 * (1 - 0.0065*1000/288.15)^5.256 = 898.7 hPa.
    // 0.3484 * 3000 * P * (1/281.65 - 1/373.15) = 817.83 kg.
    [Test]
    public void ClimbTo1000m() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(1000, 0, 15, 100, 3000), Is.EqualTo(817.83).Within(0.05));

    // Site at 300 m, max 1500 m (1200 m climb): Ta = 20 - 7.8 = 12.2 C; pressure from 1500 m AMSL; internal 90 C; 4000 m3.
    [Test]
    public void ElevatedSite_90C() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(1500, 300, 20, 90, 4000), Is.EqualTo(884.70).Within(0.05));

    [Test]
    public void InternalTemperatureAbove100_IsClampedTo100() =>
        Assert.That(LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 250, 1000),
            Is.EqualTo(LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 100, 1000)));

    // Clamped to 0 C: 0.3484 * 1000 * 1013.25 * (1/288.15 - 1/273.15) = -67.28 kg (internal colder than ambient).
    [Test]
    public void InternalTemperatureBelow0_IsClampedTo0()
    {
        var lift = LiftCalculator.CalculateTotalLiftKg(0, 0, 15, -20, 1000);
        Assert.That(lift, Is.EqualTo(LiftCalculator.CalculateTotalLiftKg(0, 0, 15, 0, 1000)));
        Assert.That(lift, Is.EqualTo(-67.28).Within(0.05));
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
