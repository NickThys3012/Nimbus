using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.GoNoGoThresholds.Queries.GetMyGoNoGoThresholds;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Features.GoNoGoThresholds.Queries.EvaluateGoNoGo;

public sealed class EvaluateGoNoGoQueryHandler : IRequestHandler<EvaluateGoNoGoQuery, GoNoGoVerdictDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGoNoGoThresholdsRepository _repository;

    public EvaluateGoNoGoQueryHandler(IGoNoGoThresholdsRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GoNoGoVerdictDto> Handle(EvaluateGoNoGoQuery request, CancellationToken ct)
    {
        var t = await GoNoGoThresholdsResolver.ResolveAsync(_repository, _currentUser.UserId, ct);
        var limits = new GoNoGoLimits(t.WindYellowKt, t.WindRedKt, t.VisibilityYellowKm, t.VisibilityRedKm, t.CapeYellow, t.CapeRed);
        var verdict = GoNoGoEvaluator.Evaluate(request.WindSpeedKt, request.VisibilityKm, request.CapeJPerKg, limits);
        return new GoNoGoVerdictDto(verdict.ToString().ToLowerInvariant());
    }
}
