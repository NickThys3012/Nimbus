using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.OfpSettings.Commands.SaveMyOfpSettings;

public sealed class SaveMyOfpSettingsCommandHandler : IRequestHandler<SaveMyOfpSettingsCommand, OfpSettingsDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IOfpSettingsRepository _repository;

    public SaveMyOfpSettingsCommandHandler(IOfpSettingsRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<OfpSettingsDto> Handle(SaveMyOfpSettingsCommand request, CancellationToken ct)
    {
        // Only the caller's own row is ever looked up (never by row id), so another user's row cannot be touched.
        var userId = _currentUser.UserId;
        var existing = await _repository.GetByOwnerAsync(userId, ct);
        if (existing is null)
        {
            var created = new Nimbus.Domain.Entities.OfpSettings { Id = Guid.NewGuid(), OwnerId = userId };
            request.Settings.ApplyTo(created);
            await _repository.AddAsync(created, ct);
            return created.MapToDto(true);
        }

        // Update in place: values only; Id and OwnerId are never reassigned.
        request.Settings.ApplyTo(existing);
        await _repository.UpdateAsync(existing, ct);
        return existing.MapToDto(true);
    }
}
