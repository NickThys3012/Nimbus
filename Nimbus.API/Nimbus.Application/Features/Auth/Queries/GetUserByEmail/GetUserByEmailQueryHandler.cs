using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Queries.GetUserByEmail;

// This is a sample query handler. Replace it with your own logic.
// This should only be used for testing the app, and once you have a real query handler, remove it.
public class GetUserByEmailQueryHandler : IRequestHandler<GetUserByEmailQuery, UserDto>
{
    private readonly IBusinessMetrics _businessMetrics;
    private readonly IUserRepository _userRepository;

    public GetUserByEmailQueryHandler(IUserRepository userRepository, IBusinessMetrics businessMetrics)
    {
        _userRepository = userRepository;
        _businessMetrics = businessMetrics;
    }

    public async Task<UserDto> Handle(GetUserByEmailQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        _businessMetrics.UserFetchedByEmail();
        return user == null ? throw new NotFoundException(nameof(User), request.Email) : user.MapToUserDto();
    }
}
