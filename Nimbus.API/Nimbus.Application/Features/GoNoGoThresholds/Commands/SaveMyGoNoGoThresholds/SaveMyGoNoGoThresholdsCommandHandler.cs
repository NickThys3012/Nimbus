using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.GoNoGoThresholds.Commands.SaveMyGoNoGoThresholds;

public sealed class SaveMyGoNoGoThresholdsCommandHandler : IRequestHandler<SaveMyGoNoGoThresholdsCommand, GoNoGoThresholdsDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGoNoGoThresholdsRepository _repository;

    public SaveMyGoNoGoThresholdsCommandHandler(IGoNoGoThresholdsRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GoNoGoThresholdsDto> Handle(SaveMyGoNoGoThresholdsCommand request, CancellationToken ct)
    {
        // Only the caller's own row is ever looked up (never the global default, never by row id),
        // so this can neither touch another user's row nor the null-owner default.
        var userId = _currentUser.UserId;
        var existing = await _repository.GetByOwnerAsync(userId, ct);
        if (existing is null)
        {
            var created = new Nimbus.Domain.Entities.GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = userId };
            request.Thresholds.ApplyTo(created);
            await _repository.AddAsync(created, ct);
            return created.MapToDto(true);
        }

        // Update in place: values only; Id and OwnerId are never reassigned.
        request.Thresholds.ApplyTo(existing);
        await _repository.UpdateAsync(existing, ct);
        return existing.MapToDto(true);
    }
}
