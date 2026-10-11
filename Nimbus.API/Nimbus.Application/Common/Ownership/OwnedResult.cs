namespace Nimbus.Application.Common.Ownership;

/// <summary>
///     Outcome of an owned-entity operation. <see cref="NotFound" /> covers both a missing and a
///     foreign entity so callers can answer 404 for both and never leak that an id exists.
/// </summary>
public enum OwnedResult
{
    Success,
    NotFound
}
