using MediatR;
using Nimbus.Application.Helpers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Admin.Commands.RecordLoginAttempt;

public sealed class RecordLoginAttemptCommandHandler : IRequestHandler<RecordLoginAttemptCommand>
{
    private readonly ILoginEventQueue _queue;
    private readonly TimeProvider _time;

    public RecordLoginAttemptCommandHandler(ILoginEventQueue queue, TimeProvider time)
    {
        _queue = queue;
        _time = time;
    }

    public Task Handle(RecordLoginAttemptCommand request, CancellationToken ct)
    {
        // The email is only kept for attempts that matched an account.
        var email = request.UserId is null ? null : request.Email;
        var evt = new LoginEvent(_time.GetUtcNow().UtcDateTime, request.UserId, email, request.Succeeded, IpMasker.Mask(request.RemoteIp), request.FailureReason);
        // Persisted by the background writer; a full queue drops the event rather than slowing the login.
        _queue.TryEnqueue(evt);
        return Task.CompletedTask;
    }
}
