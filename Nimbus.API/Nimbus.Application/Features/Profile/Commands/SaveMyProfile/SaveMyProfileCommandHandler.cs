using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.Profile;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Profile.Commands.SaveMyProfile;

public sealed class SaveMyProfileCommandHandler : IRequestHandler<SaveMyProfileCommand, ProfileDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUserProfileRepository _repository;

    public SaveMyProfileCommandHandler(IUserProfileRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ProfileDto> Handle(SaveMyProfileCommand request, CancellationToken ct)
    {
        // Only the caller's own row is ever looked up (never by row id); Id and OwnerId are never reassigned.
        var userId = _currentUser.UserId;
        var existing = await _repository.GetByOwnerAsync(userId, ct);
        if (existing is null)
        {
            var created = new UserProfile { Id = Guid.NewGuid(), OwnerId = userId };
            request.Profile.ApplyTo(created);
            await _repository.AddAsync(created, ct);
            return created.MapToDto();
        }

        request.Profile.ApplyTo(existing);
        await _repository.UpdateAsync(existing, ct);
        return existing.MapToDto();
    }
}
