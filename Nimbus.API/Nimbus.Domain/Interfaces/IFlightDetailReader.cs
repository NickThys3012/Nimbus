using Nimbus.Domain.Entities;
using Nimbus.Domain.Services;
namespace Nimbus.Domain.Interfaces;

/// <summary>How the caller relates to a flight. Owner first, then admin (which bypasses the share check), then an active share.</summary>
public enum FlightViewerRole
{
    Owner,
    Admin,
    Shared
}

/// <summary><see cref="Role" /> is null when the flight exists but the caller may not see it. <see cref="OwnerId" /> is set whenever the flight exists.</summary>
public sealed record FlightAccess(bool Exists, FlightViewerRole? Role, Guid? OwnerId = null);

/// <summary>
///     A flight with everything the read-only view needs: the dossier, its owner-scoped names and the single
///     assessment input (the flight's own balloon-weight snapshot, falling back to the balloon for older rows).
/// </summary>
public sealed record FlightDetailData(
    FlightPreparation Flight,
    FlightViewerRole Role,
    string? BalloonRegistration,
    string? BalloonType,
    string? PilotName,
    string? LocationName,
    string? LocationIcaoCode,
    FlightAssessmentInput Assessment);

/// <summary><see cref="Data" /> is present only when the caller may see the flight.</summary>
public sealed record FlightDetailResult(FlightAccess Access, FlightDetailData? Data);

public interface IFlightDetailReader
{
    Task<FlightAccess> GetAccessAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default);

    Task<FlightDetailResult> GetAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default);
}
