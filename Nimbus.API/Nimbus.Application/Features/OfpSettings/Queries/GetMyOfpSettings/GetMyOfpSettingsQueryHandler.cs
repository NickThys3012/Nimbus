using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.OfpSettings.Queries.GetMyOfpSettings;

public sealed class GetMyOfpSettingsQueryHandler : IRequestHandler<GetMyOfpSettingsQuery, OfpSettingsDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IOfpSettingsRepository _repository;

    public GetMyOfpSettingsQueryHandler(IOfpSettingsRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<OfpSettingsDto> Handle(GetMyOfpSettingsQuery request, CancellationToken ct)
    {
        var mine = await _repository.GetByOwnerAsync(_currentUser.UserId, ct);
        return mine is null ? OfpSettingsDefaults.Effective() : mine.MapToDto(true);
    }
}
