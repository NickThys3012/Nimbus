using MediatR;
using Nimbus.Contracts.DTOs.Features.LiftCalculation;
namespace Nimbus.Application.Features.LiftCalculation.Queries.CalculateTotalLift;

/// <summary>
///     Stateless calculation (Cameron manual formula, see <c>LiftCalculator</c>): the inputs are the whole request,
///     no stored user data is read or written, so there is nothing to scope to an owner beyond requiring sign-in.
/// </summary>
public sealed record CalculateTotalLiftQuery(
    decimal MaxAltitudeMeters,
    decimal SiteElevationMeters,
    decimal AmbientTemperatureC,
    decimal InternalTemperatureC,
    decimal EnvelopeVolumeM3) : IRequest<TotalLiftDto>;
