using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.GoNoGoThresholds.Queries.GetMyGoNoGoThresholds;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.GoNoGoThresholds.Commands.ResetMyGoNoGoThresholds;

public sealed class ResetMyGoNoGoThresholdsCommandHandler : IRequestHandler<ResetMyGoNoGoThresholdsCommand, GoNoGoThresholdsDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGoNoGoThresholdsRepository _repository;

    public ResetMyGoNoGoThresholdsCommandHandler(IGoNoGoThresholdsRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GoNoGoThresholdsDto> Handle(ResetMyGoNoGoThresholdsCommand request, CancellationToken ct)
    {
        // Removes only the caller's own row; the global default (null owner) is never matched.
        var existing = await _repository.GetByOwnerAsync(_currentUser.UserId, ct);
        if (existing is not null)
        {
            await _repository.RemoveAsync(existing, ct);
        }

        return await GoNoGoThresholdsResolver.ResolveAsync(_repository, _currentUser.UserId, ct);
    }
}
