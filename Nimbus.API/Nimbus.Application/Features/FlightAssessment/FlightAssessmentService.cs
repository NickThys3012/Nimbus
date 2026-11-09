using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.GoNoGoThresholds.Queries.GetMyGoNoGoThresholds;
using Nimbus.Contracts.DTOs.Features.FlightAssessment;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Features.FlightAssessment;

public sealed class FlightAssessmentService : IFlightAssessmentService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGoNoGoThresholdsRepository _thresholds;

    public FlightAssessmentService(IGoNoGoThresholdsRepository thresholds, ICurrentUserService currentUser)
    {
        _thresholds = thresholds;
        _currentUser = currentUser;
    }

    public async Task<FlightAssessmentDto> AssessAsync(FlightAssessmentInput input, CancellationToken ct = default)
    {
        var limits = await ResolveLimitsAsync(ct);
        return Map(FlightAssessor.Assess(input, limits));
    }

    public async Task<IReadOnlyList<FlightAssessmentDto>> AssessManyAsync(IReadOnlyList<FlightAssessmentInput> inputs, CancellationToken ct = default)
    {
        var limits = await ResolveLimitsAsync(ct);
        return inputs.Select(i => Map(FlightAssessor.Assess(i, limits))).ToList();
    }

    private async Task<GoNoGoLimits> ResolveLimitsAsync(CancellationToken ct)
    {
        var t = await GoNoGoThresholdsResolver.ResolveAsync(_thresholds, _currentUser.UserId, ct);
        return new GoNoGoLimits(t.WindYellowKt, t.WindRedKt, t.VisibilityYellowKm, t.VisibilityRedKm, t.CapeYellow, t.CapeRed);
    }

    private static FlightAssessmentDto Map(FlightAssessmentResult a) =>
        new(a.TotalWeightKg, a.PassengersWeightKg, a.MarginKg, a.LiftSufficient, a.Verdict.ToString().ToLowerInvariant());
}
