using Microsoft.Extensions.Time.Testing;
using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Auth.Command.CreateUser;
using Nimbus.Application.Features.Auth.Command.ResendVerificationEmail;
using Nimbus.Application.Features.Auth.Command.UnblockVerificationEmail;
using Nimbus.Application.Helpers;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Auth.Register;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class VerificationEmailHandlerTests
{
    [Test]
    public async Task CreateUserCommandHandler_RecordsTheInitialVerificationEmailSend()
    {
        var users = new FakeUserRepository();
        var identity = new FakeIdentityService(users);
        var queue = new CapturingEmailQueue();
        var handler = new CreateUserCommandHandler(users, identity, queue, new NoopBusinessMetrics(), new EmailHelpers(identity));

        var result = await handler.Handle(
            new CreateUserCommand(new RegisterRequestDto("pilot@example.com", "P@ssword1!", "Piper", "Pilot"), "https://app.example"),
            CancellationToken.None);

        var user = await users.GetByIdAsync(Guid.Parse(result));

        Assert.Multiple(() =>
        {
            Assert.That(user, Is.Not.Null);
            Assert.That(user!.VerificationEmailSendCount, Is.EqualTo(1));
            Assert.That(user.VerificationEmailCooldownEndUtc, Is.GreaterThan(DateTimeOffset.UtcNow));
            Assert.That(user.VerificationEmailLocked, Is.False);
            Assert.That(queue.Messages, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ResendVerificationEmailCommandHandler_RejectsDuringCooldown()
    {
        var fakeTimeProvider = new FakeTimeProvider();

        var user = new User(
            Guid.NewGuid(),
            "pilot@example.com",
            "Pilot",
            "Piper",
            UserRole.Pilot,
            false,
            fakeTimeProvider.GetUtcNow().AddMinutes(5),
            1,
            false);

        var users = new FakeUserRepository(user);
        var identity = new FakeIdentityService(users);
        var queue = new CapturingEmailQueue();
        var handler = new ResendVerificationEmailCommandHandler(users, queue, new EmailHelpers(identity), fakeTimeProvider);

        Assert.ThrowsAsync<ProcessingException>(async () => await handler.Handle(
            new ResendVerificationEmailCommand(new ResendVerificationEmailCommandDto("pilot@example.com"), "https://app.example"),
            CancellationToken.None));

        Assert.That(queue.Messages, Is.Empty);
    }

    [Test]
    public async Task ResendVerificationEmailCommandHandler_LocksOnTheFifthSend()
    {
        var fakeTimeProvider = new FakeTimeProvider();

        var user = new User(
            Guid.NewGuid(),
            "pilot@example.com",
            "Pilot",
            "Piper",
            UserRole.Pilot,
            false,
            fakeTimeProvider.GetUtcNow().AddMinutes(-1),
            4,
            false);

        var users = new FakeUserRepository(user);
        var identity = new FakeIdentityService(users);
        var queue = new CapturingEmailQueue();
        var handler = new ResendVerificationEmailCommandHandler(users, queue, new EmailHelpers(identity), fakeTimeProvider);

        await handler.Handle(
            new ResendVerificationEmailCommand(new ResendVerificationEmailCommandDto("pilot@example.com"), "https://app.example"),
            CancellationToken.None);

        var updated = await users.GetByEmailAsync("pilot@example.com");
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.VerificationEmailSendCount, Is.EqualTo(5));
            Assert.That(updated.VerificationEmailLocked, Is.True);
            Assert.That(queue.Messages, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task UnblockVerificationEmailCommandHandler_ResetsTheVerificationState()
    {
        var user = new User(
            Guid.NewGuid(),
            "pilot@example.com",
            "Pilot",
            "Piper",
            UserRole.Pilot,
            false,
            DateTimeOffset.UtcNow.AddMinutes(5),
            5,
            true);

        var users = new FakeUserRepository(user);
        var handler = new UnblockVerificationEmailCommandHandler(users);

        await handler.Handle(new UnblockVerificationEmailCommand("pilot@example.com"), CancellationToken.None);

        var updated = await users.GetByEmailAsync("pilot@example.com");
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.VerificationEmailSendCount, Is.Zero);
            Assert.That(updated.VerificationEmailCooldownEndUtc, Is.Null);
            Assert.That(updated.VerificationEmailLocked, Is.False);
        });
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, Guid> _emails = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, User> _users = new();

        public FakeUserRepository(params User[] users)
        {
            foreach (var user in users)
            {
                Seed(user);
            }
        }

        public Task<User?> GetByIdAsync(Guid id)
        {
            return Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            return Task.FromResult(_emails.TryGetValue(email, out var id) && _users.TryGetValue(id, out var user) ? user : null);
        }

        public Task UpdateAsync(User user)
        {
            _users[user.Id] = user;
            _emails[user.Email] = user.Id;
            return Task.CompletedTask;
        }

        public void Seed(User user)
        {
            _users[user.Id] = user;
            _emails[user.Email] = user.Id;
        }
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Guid _id = Guid.NewGuid();
        private readonly FakeUserRepository _users;

        public FakeIdentityService(FakeUserRepository users)
        {
            _users = users;
        }

        public Task<string> RegisterAsync(string email, string password, string firstName, string lastName)
        {
            var user = new User(_id, email, lastName, firstName, UserRole.Pilot, false);
            _users.Seed(user);
            return Task.FromResult(_id.ToString());
        }

        public Task<string> GenerateEmailConfrimTokenAsync(string id)
        {
            return Task.FromResult("verification-token");
        }
    }

    private sealed class CapturingEmailQueue : IEmailQueue
    {
        public List<EmailMessage> Messages { get; } = [];

        public ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopBusinessMetrics : IBusinessMetrics
    {
        public void UserFetchedByEmail() {}

        public void UsersRegistered() {}
    }
}
