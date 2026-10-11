using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

/// <summary>
///     Hand-off point between the login request and the background writer (issue #18). Enqueueing never
///     blocks and never throws, so recording an attempt cannot slow down or fail the login.
/// </summary>
public interface ILoginEventQueue
{
    /// <returns><c>false</c> when the event was dropped because the queue is full or already shut down.</returns>
    bool TryEnqueue(LoginEvent loginEvent);
}
