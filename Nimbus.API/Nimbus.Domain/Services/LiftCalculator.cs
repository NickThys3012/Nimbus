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

    private const double SeaLevelPressureHpa = 1013.25;
    private const double SeaLevelTemperatureK = 288.15;
    private const double LapseRateKPerM = 0.0065;
    private const double BarometricExponent = 5.256;
    private const double LiftConstant = 0.3484;
    private const double CelsiusToKelvin = 273.15;

    /// <summary>
    ///     Calculates the total lift (in kg) of a hot-air envelope at the planned maximum altitude.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Source: the formula is taken as published in the Cameron manual (see issue #10) and implemented exactly:
    ///     </para>
    ///     <code>
    ///         Ta = Tg - 0.0065 * (A - Eg)
    ///         P  = 1013.25 * (1 - (0.0065 * A) / 288.15)^5.256          (hPa)
    ///         L  = 0.3484 * V * P * (1 / (Ta + 273.15) - 1 / (Ti + 273.15))
    ///     </code>
    ///     <para>
    ///         A = maximum planned altitude (m), Eg = take-off site elevation (m), L = lift (kg), P in hPa,
    ///         Ta = ambient temperature at altitude (C), Tg = ambient temperature at the take-off site (C),
    ///         Ti = average internal envelope temperature (C, at most 100), V = envelope volume (m3).
    ///         The ISA lapse rate is assumed. The internal temperature is clamped to 0-100 C.
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

        var ti = Math.Clamp(internalTemperatureCelsius, MinInternalTemperatureCelsius, MaxInternalTemperatureCelsius);
        var ta = ambientTemperatureCelsius - LapseRateKPerM * (maxAltitudeAmslMeters - siteElevationMeters);
        var pressureHpa = SeaLevelPressureHpa * Math.Pow(1 - LapseRateKPerM * maxAltitudeAmslMeters / SeaLevelTemperatureK, BarometricExponent);

        return LiftConstant * envelopeVolumeCubicMeters * pressureHpa
               * (1 / (ta + CelsiusToKelvin) - 1 / (ti + CelsiusToKelvin));
    }

    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, value, "Value must be a finite number.");
    }
}
