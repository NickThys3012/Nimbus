using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.Profile;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Profile.Queries.GetMyProfile;

public sealed class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, ProfileDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUserProfileRepository _repository;

    public GetMyProfileQueryHandler(IUserProfileRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ProfileDto> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var mine = await _repository.GetByOwnerAsync(_currentUser.UserId, ct);
        return mine?.MapToDto() ?? new ProfileDto(null, null);
    }
}
