using MediatR;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
namespace Nimbus.Application.Features.GoNoGoThresholds.Queries.EvaluateGoNoGo;

/// <summary>
///     Evaluates raw weather values against the signed-in user's effective thresholds (own row, else default).
///     The user is taken from the auth context, never from the request.
/// </summary>
public sealed record EvaluateGoNoGoQuery(decimal? WindSpeedKt, decimal? VisibilityKm, decimal? CapeJPerKg) : IRequest<GoNoGoVerdictDto>;
