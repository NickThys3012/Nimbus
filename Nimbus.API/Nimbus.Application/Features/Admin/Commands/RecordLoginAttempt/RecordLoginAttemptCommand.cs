using MediatR;
namespace Nimbus.Application.Features.Admin.Commands.RecordLoginAttempt;

/// <summary>Records one sign-in attempt for the admin audit list. Raised by the login endpoint.</summary>
/// <param name="UserId">Matched account, or null when no account matched.</param>
/// <param name="RemoteIp">Raw client address; masked before it is stored.</param>
public sealed record RecordLoginAttemptCommand(Guid? UserId, string? Email, bool Succeeded, string? RemoteIp) : IRequest;
