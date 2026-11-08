namespace Nimbus.Domain.Enums;

/// <summary>Traffic-light weather verdict (issue #38). <see cref="Unknown" /> means no weather data was available.</summary>
public enum GoNoGoVerdict
{
    Unknown = 0,
    Green = 1,
    Yellow = 2,
    Red = 3
}
