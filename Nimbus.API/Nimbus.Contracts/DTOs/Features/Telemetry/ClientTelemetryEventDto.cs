namespace Nimbus.Contracts.DTOs.Features.Telemetry;

/// <summary>
/// A page view or unhandled error reported by the Angular client (issue #12).
/// Kept deliberately small: this is a diagnosis signal, not an analytics event —
/// no personal data belongs in <see cref="Message"/> or <see cref="Url"/>.
/// </summary>
public sealed record ClientTelemetryEventDto(
    ClientTelemetryEventType Type,
    string Url,
    string? Message = null,
    string? Stack = null);

public enum ClientTelemetryEventType
{
    PageView,
    UnhandledError
}
