using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    private readonly IUserRepository _users;

    public CurrentUserService(IHttpContextAccessor http, IUserRepository users)
    {
        _http = http;
        _users = users;
    }

    public Guid UserId
    {
        get
        {
            var raw = _http.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : throw new InvalidOperationException("User ID is not available.");
        }
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        return await _users.GetByIdAsync(UserId);
    }
}
