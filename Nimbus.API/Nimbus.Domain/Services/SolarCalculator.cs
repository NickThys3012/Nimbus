namespace Nimbus.Domain.Services;

/// <summary>What the sun does on a day at a place: it rises and sets, it stays up all day (polar day) or it never rises (polar night).</summary>
public enum SolarCondition
{
    Normal,
    PolarDay,
    PolarNight
}

/// <summary>
///     Sunrise, solar noon and sunset as UTC instants, rounded to the minute. In polar day and polar night there is no real
///     sunrise or sunset: the hour angle is clamped, so they are an edge value (polar day: 12 h either side of solar noon; polar
///     night: both at solar noon), never NaN. <see cref="Condition" /> says which case it is.
/// </summary>
public readonly record struct SolarTimes(DateTime SunriseUtc, DateTime SolarNoonUtc, DateTime SunsetUtc, SolarCondition Condition);

/// <summary>
///     Pure, stateless sunrise and sunset by the NOAA Solar Calculator method (issue #49): Julian date and century, geometric mean
///     longitude and anomaly, orbit eccentricity, equation of centre, true and apparent longitude, mean and corrected obliquity,
///     declination, equation of time and the sunrise hour angle, for the official horizon (90.833 degrees: the sun's radius plus
///     atmospheric refraction). Like NOAA's calculator it refines once: the first estimate is recomputed at the time it found.
///     Good to within about a minute between roughly 72 degrees north and south.
/// </summary>
public static class SolarCalculator
{
    /// <summary>Zenith angle of the official sunrise and sunset: 90 degrees, plus 50 arc-minutes for refraction and the sun's disc.</summary>
    public const double OfficialZenithDegrees = 90.833;

    /// <summary>Julian date of 2000-01-01 12:00 UT.</summary>
    private const double J2000 = 2451545.0;

    public static SolarTimes Calculate(double latitudeDegrees, double longitudeDegrees, DateOnly date)
    {
        if (latitudeDegrees is < -90 or > 90 || double.IsNaN(latitudeDegrees))
            throw new ArgumentOutOfRangeException(nameof(latitudeDegrees), "Latitude must be between -90 and 90 degrees.");
        if (longitudeDegrees is < -180 or > 180 || double.IsNaN(longitudeDegrees))
            throw new ArgumentOutOfRangeException(nameof(longitudeDegrees), "Longitude must be between -180 and 180 degrees.");

        var midnightUtc = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
        var jd = JulianDate(date);

        // Minutes after 00:00 UT of the date. The first pass uses the day's start, the second the time the first pass found.
        var rise0 = EventMinutes(jd + (720 - 4 * longitudeDegrees) / 1440, latitudeDegrees, longitudeDegrees, rising: true, out _);
        var set0 = EventMinutes(jd + (720 - 4 * longitudeDegrees) / 1440, latitudeDegrees, longitudeDegrees, rising: false, out _);

        var rise = EventMinutes(jd + rise0 / 1440, latitudeDegrees, longitudeDegrees, rising: true, out var condition);
        var sunset = EventMinutes(jd + set0 / 1440, latitudeDegrees, longitudeDegrees, rising: false, out _);
        var noon = SolarNoonMinutes(jd + (rise0 + set0) / 2 / 1440, longitudeDegrees);

        return new SolarTimes(AtMinute(midnightUtc, rise), AtMinute(midnightUtc, noon), AtMinute(midnightUtc, sunset), condition);
    }

    /// <summary>Minutes after 00:00 UT at which the sun crosses the horizon, from the sun's position at <paramref name="jd" />.</summary>
    private static double EventMinutes(double jd, double lat, double lon, bool rising, out SolarCondition condition)
    {
        var t = JulianCentury(jd);
        var eqTime = EquationOfTimeMinutes(t);
        var decl = DeclinationRadians(t);
        var hourAngleDeg = HourAngleDegrees(lat, decl, out condition);
        var noon = 720 - 4 * lon - eqTime;
        return rising ? noon - 4 * hourAngleDeg : noon + 4 * hourAngleDeg;
    }

    private static double SolarNoonMinutes(double jd, double lon) => 720 - 4 * lon - EquationOfTimeMinutes(JulianCentury(jd));

    /// <summary>
    ///     The sunrise hour angle in degrees. The cosine is clamped to [-1, 1]: beyond it the sun never reaches (polar night, 0 degrees:
    ///     it rises and sets at solar noon) or never leaves (polar day, 180 degrees) the horizon, and acos would be NaN.
    /// </summary>
    private static double HourAngleDegrees(double latDeg, double declRad, out SolarCondition condition)
    {
        var lat = ToRadians(latDeg);
        var cosH = Math.Cos(ToRadians(OfficialZenithDegrees)) / (Math.Cos(lat) * Math.Cos(declRad)) - Math.Tan(lat) * Math.Tan(declRad);
        if (double.IsNaN(cosH))
        {
            // Exactly at a pole the quotient is 0/0: the sun is up in the summer half-year, down in the winter one.
            cosH = declRad * Math.Sign(latDeg) > 0 ? -1 : 1;
        }

        condition = cosH > 1 ? SolarCondition.PolarNight : cosH < -1 ? SolarCondition.PolarDay : SolarCondition.Normal;
        return ToDegrees(Math.Acos(Math.Clamp(cosH, -1, 1)));
    }

    /// <summary>Julian date at 00:00 UT of a Gregorian calendar date.</summary>
    public static double JulianDate(DateOnly date)
    {
        int y = date.Year, m = date.Month;
        if (m <= 2)
        {
            y -= 1;
            m += 12;
        }

        var a = Math.Floor(y / 100.0);
        var b = 2 - a + Math.Floor(a / 4);
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + date.Day + b - 1524.5;
    }

    public static double JulianCentury(double jd) => (jd - J2000) / 36525.0;

    public static double GeometricMeanLongitudeDegrees(double t)
    {
        var l0 = 280.46646 + t * (36000.76983 + t * 0.0003032);
        return Mod(l0, 360);
    }

    public static double GeometricMeanAnomalyDegrees(double t) => 357.52911 + t * (35999.05029 - 0.0001537 * t);

    public static double EccentricityEarthOrbit(double t) => 0.016708634 - t * (0.000042037 + 0.0000001267 * t);

    public static double EquationOfCentreDegrees(double t)
    {
        var m = ToRadians(GeometricMeanAnomalyDegrees(t));
        return Math.Sin(m) * (1.914602 - t * (0.004817 + 0.000014 * t))
               + Math.Sin(2 * m) * (0.019993 - 0.000101 * t)
               + Math.Sin(3 * m) * 0.000289;
    }

    public static double ApparentLongitudeDegrees(double t)
    {
        var trueLong = GeometricMeanLongitudeDegrees(t) + EquationOfCentreDegrees(t);
        var omega = 125.04 - 1934.136 * t;
        return trueLong - 0.00569 - 0.00478 * Math.Sin(ToRadians(omega));
    }

    public static double MeanObliquityDegrees(double t)
    {
        var seconds = 21.448 - t * (46.815 + t * (0.00059 - t * 0.001813));
        return 23 + (26 + seconds / 60) / 60;
    }

    public static double CorrectedObliquityDegrees(double t)
    {
        var omega = 125.04 - 1934.136 * t;
        return MeanObliquityDegrees(t) + 0.00256 * Math.Cos(ToRadians(omega));
    }

    public static double DeclinationRadians(double t)
    {
        var e = ToRadians(CorrectedObliquityDegrees(t));
        var lambda = ToRadians(ApparentLongitudeDegrees(t));
        return Math.Asin(Math.Sin(e) * Math.Sin(lambda));
    }

    /// <summary>Equation of time: apparent minus mean solar time, in minutes.</summary>
    public static double EquationOfTimeMinutes(double t)
    {
        var epsilon = ToRadians(CorrectedObliquityDegrees(t));
        var l0 = ToRadians(GeometricMeanLongitudeDegrees(t));
        var e = EccentricityEarthOrbit(t);
        var m = ToRadians(GeometricMeanAnomalyDegrees(t));
        var y = Math.Tan(epsilon / 2);
        y *= y;
        var eq = y * Math.Sin(2 * l0) - 2 * e * Math.Sin(m) + 4 * e * y * Math.Sin(m) * Math.Cos(2 * l0)
                 - 0.5 * y * y * Math.Sin(4 * l0) - 1.25 * e * e * Math.Sin(2 * m);
        return ToDegrees(eq) * 4;
    }

    private static DateTime AtMinute(DateTime midnightUtc, double minutes) =>
        midnightUtc.AddMinutes(Math.Round(minutes, MidpointRounding.AwayFromZero));

    private static double Mod(double value, double m) => (value % m + m) % m;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
