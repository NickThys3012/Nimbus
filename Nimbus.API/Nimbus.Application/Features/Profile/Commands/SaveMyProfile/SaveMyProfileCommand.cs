using MediatR;
using Nimbus.Contracts.DTOs.Features.Profile;
namespace Nimbus.Application.Features.Profile.Commands.SaveMyProfile;

public sealed record SaveMyProfileCommand(ProfileRequestDto Profile) : IRequest<ProfileDto>;
