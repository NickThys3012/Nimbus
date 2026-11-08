namespace Nimbus.Domain.Services;

/// <summary>
///     Pure, stateless hot-air balloon lift calculations. Holds no user data, so there is nothing to scope per user.
/// </summary>
public static class LiftCalculator
{
    /// <summary>Lowest accepted average internal envelope temperature, in degrees Celsius.</summary>
    public const double MinInternalTemperatureCelsius = 0;

    /// <summary>Highest accepted average internal envelope temperature, in degrees Celsius.</summary>
    public const double MaxInternalTemperatureCelsius = 100;

    private const double SeaLevelPressurePa = 101_325.0;
    private const double SeaLevelTemperatureK = 288.15;
    private const double LapseRateKPerM = 0.0065;
    private const double SpecificGasConstantDryAir = 287.05; // J/(kg*K)
    private const double BarometricExponent = 5.255877; // g / (R * L)
    private const double CelsiusToKelvin = 273.15;

    /// <summary>
    ///     Calculates the total lift (in kg) of a hot-air envelope at the planned maximum altitude.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Source: Cameron Hot Air Balloon Flight Manual, Amendment 18, Appendix A2-1 (ISA-based total lift).
    ///         The manual's tables were not available when this was written; the implementation follows the ISA
    ///         physics the appendix is based on and is NOT yet verified against the manual's published figures.
    ///     </para>
    ///     <para>
    ///         Model: lift = V * (rho_ambient - rho_internal), with rho = p / (R * T) for dry air. The envelope mouth is
    ///         open, so internal and ambient pressure are equal. Pressure at the max altitude comes from the ISA
    ///         barometric formula p = 101325 * (1 - 0.0065 * h / 288.15)^5.255877 (h = altitude AMSL).
    ///         Ambient temperature at altitude is the take-off temperature reduced by the ISA lapse rate
    ///         (6.5 K per 1000 m) over the climb from site elevation to max altitude. The internal temperature is
    ///         the average absolute envelope air temperature and is clamped to 0-100 C.
    ///     </para>
    ///     <para>
    ///         The result is not clamped: it is negative when the clamped internal temperature is below the ambient
    ///         temperature at altitude (the balloon cannot lift anything).
    ///     </para>
    /// </remarks>
    /// <param name="maxAltitudeAmslMeters">Maximum planned altitude above mean sea level, in metres.</param>
    /// <param name="siteElevationMeters">Take-off site elevation above mean sea level, in metres.</param>
    /// <param name="ambientTemperatureCelsius">Ambient temperature at take-off, in degrees Celsius.</param>
    /// <param name="internalTemperatureCelsius">Average internal envelope temperature in degrees Celsius (clamped to 0-100).</param>
    /// <param name="envelopeVolumeCubicMeters">Envelope volume in cubic metres; 0 yields 0 lift.</param>
    /// <returns>Total lift in kilograms.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The volume is negative or any input is not a finite number.</exception>
    public static double CalculateTotalLiftKg(
        double maxAltitudeAmslMeters,
        double siteElevationMeters,
        double ambientTemperatureCelsius,
        double internalTemperatureCelsius,
        double envelopeVolumeCubicMeters)
    {
        RequireFinite(maxAltitudeAmslMeters, nameof(maxAltitudeAmslMeters));
        RequireFinite(siteElevationMeters, nameof(siteElevationMeters));
        RequireFinite(ambientTemperatureCelsius, nameof(ambientTemperatureCelsius));
        RequireFinite(internalTemperatureCelsius, nameof(internalTemperatureCelsius));
        RequireFinite(envelopeVolumeCubicMeters, nameof(envelopeVolumeCubicMeters));
        ArgumentOutOfRangeException.ThrowIfNegative(envelopeVolumeCubicMeters);

        if (envelopeVolumeCubicMeters == 0) return 0;

        var internalK = Math.Clamp(internalTemperatureCelsius, MinInternalTemperatureCelsius, MaxInternalTemperatureCelsius) + CelsiusToKelvin;
        var ambientK = ambientTemperatureCelsius - LapseRateKPerM * (maxAltitudeAmslMeters - siteElevationMeters) + CelsiusToKelvin;
        var pressurePa = SeaLevelPressurePa * Math.Pow(1 - LapseRateKPerM * maxAltitudeAmslMeters / SeaLevelTemperatureK, BarometricExponent);

        var densityDifference = pressurePa / SpecificGasConstantDryAir * (1 / ambientK - 1 / internalK);
        return envelopeVolumeCubicMeters * densityDifference;
    }

    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, value, "Value must be a finite number.");
    }
}
