using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.OfpSettings.Commands.ResetMyOfpSettings;

public sealed class ResetMyOfpSettingsCommandHandler : IRequestHandler<ResetMyOfpSettingsCommand, OfpSettingsDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IOfpSettingsRepository _repository;

    public ResetMyOfpSettingsCommandHandler(IOfpSettingsRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<OfpSettingsDto> Handle(ResetMyOfpSettingsCommand request, CancellationToken ct)
    {
        var existing = await _repository.GetByOwnerAsync(_currentUser.UserId, ct);
        if (existing is not null)
        {
            await _repository.RemoveAsync(existing, ct);
        }

        return OfpSettingsDefaults.Effective();
    }
}
