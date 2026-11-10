namespace Nimbus.Application.Common;

/// <summary>
///     The outcome of a call to an outside provider: a value, or a short reason it failed (safe to log, null on success).
///     Provider clients return this instead of throwing, so a provider outage is an ordinary case the caller decides about,
///     never an unhandled exception. Build one with <see cref="ProviderResult.Ok{T}" /> or <see cref="ProviderResult.Fail{T}" />.
/// </summary>
public sealed record ProviderResult<T>(bool IsSuccess, T? Value, string? Error);

public static class ProviderResult
{
    public static ProviderResult<T> Ok<T>(T value) => new(true, value, null);

    public static ProviderResult<T> Fail<T>(string error) => new(false, default, error);
}
