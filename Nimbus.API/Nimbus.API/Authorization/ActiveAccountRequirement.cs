using Microsoft.AspNetCore.Authorization;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.API.Authorization;

/// <summary>
///     The caller's account must be approved and not rejected. The status is read from the
///     database on every request (<see cref="AccessTokenValidator" />), so an approval or
///     rejection takes effect immediately. Pending/rejected users can still sign in (the SPA
///     needs the tokens to show its notice card) but only reach endpoints guarded by the
///     <see cref="AuthorizationPolicies.Authenticated" /> policy (/me).
/// </summary>
public sealed class ActiveAccountRequirement : IAuthorizationRequirement {}

public sealed class ActiveAccountHandler : AuthorizationHandler<ActiveAccountRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveAccountRequirement requirement)
    {
        if (context.User.HasClaim(AccessTokenValidator.StatusClaim, AccessTokenValidator.StatusActive))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
