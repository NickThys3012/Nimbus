using MediatR;
using Nimbus.Contracts.DTOs.Features.Profile;
namespace Nimbus.Application.Features.Profile.Queries.GetMyProfile;

public sealed record GetMyProfileQuery : IRequest<ProfileDto>;
