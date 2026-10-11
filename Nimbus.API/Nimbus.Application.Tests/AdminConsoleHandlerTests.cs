using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Admin.Commands.ApproveUser;
using Nimbus.Application.Features.Admin.Commands.DeleteUser;
using Nimbus.Application.Features.Admin.Commands.RecordLoginAttempt;
using Nimbus.Application.Features.Admin.Commands.RejectUser;
using Nimbus.Application.Features.Admin.Commands.SetUserBlocked;
using Nimbus.Application.Features.Admin.Commands.SetUserRole;
using Nimbus.Application.Features.Admin.Queries.ListLoginEvents;
using Nimbus.Application.Features.Admin.Queries.ListUsers;
using Nimbus.Application.Helpers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class AdminConsoleHandlerTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    private static User Pilot(bool approved = false, bool rejected = false)
    {
        return new User(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", "Doe", "Jane", UserRole.Pilot, true,
            isApproved: approved, roles: ["Pilot"], isRejected: rejected);
    }

    private static User Admin(Guid? id = null)
    {
        return new User(id ?? Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", "Boss", "Big", UserRole.Admin, true,
            isApproved: true, roles: ["Admin"]);
    }

    // ── reject ──
    [Test]
    public async Task Reject_MarksPendingUser_AndRevokesSessions()
    {
        var target = Pilot();
        var (repo, svc) = (new Repo(target), new Administration());
        var handler = new RejectUserCommandHandler(repo, svc, new Me(AdminId), new NoopQueue(), NullLogger<RejectUserCommandHandler>.Instance);

        await handler.Handle(new RejectUserCommand(target.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(target.IsRejected, Is.True);
            Assert.That(svc.Revoked, Does.Contain(target.Id));
            Assert.That(svc.Invalidated, Does.Contain(target.Id));
        });
    }

    [Test]
    public void Reject_Self_Unknown_AndApproved_AreRefused()
    {
        var approved = Pilot(true);
        var handler = new RejectUserCommandHandler(new Repo(approved), new Administration(), new Me(approved.Id), new NoopQueue(), NullLogger<RejectUserCommandHandler>.Instance);

        Assert.ThrowsAsync<ProcessingException>(() => handler.Handle(new RejectUserCommand(approved.Id), CancellationToken.None));
        var other = new RejectUserCommandHandler(new Repo(approved), new Administration(), new Me(AdminId), new NoopQueue(), NullLogger<RejectUserCommandHandler>.Instance);
        Assert.ThrowsAsync<DomainException>(() => other.Handle(new RejectUserCommand(approved.Id), CancellationToken.None));
        Assert.ThrowsAsync<NotFoundException>(() => other.Handle(new RejectUserCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Test]
    public void Approve_RejectedUser_IsRefused()
    {
        var rejected = Pilot(rejected: true);
        var handler = new ApproveUserCommandHandler(new Repo(rejected), new Me(AdminId), new NoopQueue(), NullLogger<ApproveUserCommandHandler>.Instance);

        Assert.ThrowsAsync<DomainException>(() => handler.Handle(new ApproveUserCommand(rejected.Id), CancellationToken.None));
        Assert.That(rejected.IsApproved, Is.False);
    }

    // ── role ──
    [Test]
    public async Task SetRole_PromotesPilot()
    {
        var target = Pilot(true);
        var svc = new Administration();
        var handler = new SetUserRoleCommandHandler(new Repo(target), svc, new Me(AdminId), NullLogger<SetUserRoleCommandHandler>.Instance);

        await handler.Handle(new SetUserRoleCommand(target.Id, "Admin"), CancellationToken.None);

        Assert.That(svc.Roles[target.Id], Is.EqualTo("Admin"));
    }

    [Test]
    public void SetRole_SelfDemotion_IsRefused()
    {
        var me = Admin(AdminId);
        var handler = new SetUserRoleCommandHandler(new Repo(me), new Administration(), new Me(AdminId), NullLogger<SetUserRoleCommandHandler>.Instance);

        Assert.ThrowsAsync<ProcessingException>(() => handler.Handle(new SetUserRoleCommand(me.Id, "Pilot"), CancellationToken.None));
    }

    [Test]
    public void SetRole_DemotingAnotherAdmin_WhenSeveralExist_Succeeds_AndUnknownIs404()
    {
        var other = Admin();
        var handler = new SetUserRoleCommandHandler(new Repo(other), new Administration(), new Me(AdminId), NullLogger<SetUserRoleCommandHandler>.Instance);

        Assert.DoesNotThrowAsync(() => handler.Handle(new SetUserRoleCommand(other.Id, "Pilot"), CancellationToken.None));
        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new SetUserRoleCommand(Guid.NewGuid(), "Pilot"), CancellationToken.None));
    }

    [Test]
    public void SetRole_Validator_OnlyAcceptsExistingRoles()
    {
        var validator = new SetUserRoleCommandValidator();
        Assert.Multiple(() =>
        {
            Assert.That(validator.Validate(new SetUserRoleCommand(Guid.NewGuid(), "Admin")).IsValid, Is.True);
            Assert.That(validator.Validate(new SetUserRoleCommand(Guid.NewGuid(), "Pilot")).IsValid, Is.True);
            Assert.That(validator.Validate(new SetUserRoleCommand(Guid.NewGuid(), "crew")).IsValid, Is.False);
            Assert.That(validator.Validate(new SetUserRoleCommand(Guid.NewGuid(), "")).IsValid, Is.False);
        });
    }

    // ── block ──
    [Test]
    public async Task Block_AndUnblock_Work_ButNotOnSelf()
    {
        var target = Pilot(true);
        var svc = new Administration();
        var handler = new SetUserBlockedCommandHandler(new Repo(target), svc, new Me(AdminId), NullLogger<SetUserBlockedCommandHandler>.Instance);

        await handler.Handle(new SetUserBlockedCommand(target.Id, true), CancellationToken.None);
        Assert.That(svc.Blocked[target.Id], Is.True);
        await handler.Handle(new SetUserBlockedCommand(target.Id, false), CancellationToken.None);
        Assert.That(svc.Blocked[target.Id], Is.False);

        var selfHandler = new SetUserBlockedCommandHandler(new Repo(target), svc, new Me(target.Id), NullLogger<SetUserBlockedCommandHandler>.Instance);
        Assert.ThrowsAsync<ProcessingException>(() => selfHandler.Handle(new SetUserBlockedCommand(target.Id, true), CancellationToken.None));
        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new SetUserBlockedCommand(Guid.NewGuid(), true), CancellationToken.None));
    }

    // ── delete ──
    [Test]
    public async Task Delete_RemovesTarget_ButNotSelf()
    {
        var target = Pilot(rejected: true);
        var svc = new Administration();
        var handler = new DeleteUserCommandHandler(new Repo(target), svc, new Me(AdminId), NullLogger<DeleteUserCommandHandler>.Instance);

        await handler.Handle(new DeleteUserCommand(target.Id), CancellationToken.None);
        Assert.That(svc.Deleted, Does.Contain(target.Id));

        Assert.ThrowsAsync<ProcessingException>(() => handler.Handle(new DeleteUserCommand(AdminId), CancellationToken.None));
        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteUserCommand(Guid.NewGuid()), CancellationToken.None));
    }

    // ── validators ──
    [Test]
    public void RecordLoginAttemptValidator_RequiresMatchedUserForSuccess()
    {
        var validator = new RecordLoginAttemptCommandValidator();
        Assert.Multiple(() =>
        {
            Assert.That(validator.Validate(new RecordLoginAttemptCommand(null, null, true, "1.2.3.4")).IsValid, Is.False);
            Assert.That(validator.Validate(new RecordLoginAttemptCommand(Guid.NewGuid(), "a@example.com", true, "1.2.3.4")).IsValid, Is.True);
            Assert.That(validator.Validate(new RecordLoginAttemptCommand(null, null, false, null, LoginFailureReason.InvalidPassword)).IsValid, Is.True);
            Assert.That(validator.Validate(new RecordLoginAttemptCommand(null, null, false, null)).IsValid, Is.False, "a failure needs a reason");
            Assert.That(validator.Validate(new RecordLoginAttemptCommand(Guid.NewGuid(), "a@example.com", true, null, LoginFailureReason.LockedOut)).IsValid, Is.False, "a success has no reason");
            Assert.That(validator.Validate(new RecordLoginAttemptCommand(Guid.NewGuid(), "", false, null, LoginFailureReason.LockedOut)).IsValid, Is.False);
            Assert.That(validator.Validate(new RecordLoginAttemptCommand(Guid.Empty, "a@example.com", false, null, LoginFailureReason.LockedOut)).IsValid, Is.False);
            Assert.That(validator.Validate(new RecordLoginAttemptCommand(null, null, false, new string('1', 65), LoginFailureReason.InvalidPassword)).IsValid, Is.False);
        });
    }

    [Test]
    public void ListUsersValidator_AcceptsTheParameterlessQuery()
    {
        Assert.That(new ListUsersQueryValidator().Validate(new ListUsersQuery()).IsValid, Is.True);
    }

    // ── login events ──
    [Test]
    public void IpMasker_MasksIpv4AndIpv6_AndRejectsGarbage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(IpMasker.Mask("84.198.12.9"), Is.EqualTo("84.198.x.x"));
            Assert.That(IpMasker.Mask("::ffff:84.198.12.9"), Is.EqualTo("84.198.x.x"));
            Assert.That(IpMasker.Mask("2001:db8:abcd::1"), Is.EqualTo("2001:db8:x:x"));
            Assert.That(IpMasker.Mask("2001::dead:beef"), Is.EqualTo("2001:0:x:x"));
            Assert.That(IpMasker.Mask("::1"), Is.EqualTo("0:0:x:x"));
            Assert.That(IpMasker.Mask("::"), Is.EqualTo("0:0:x:x"));
            Assert.That(IpMasker.Mask("fe80::1%eth0"), Is.EqualTo("fe80:0:x:x"));
            Assert.That(IpMasker.Mask("2001:0DB8:0000:0000:0000:0000:0000:0001"), Is.EqualTo("2001:db8:x:x"));
            Assert.That(IpMasker.Mask("::ffff:1.2.3.4"), Is.EqualTo("1.2.x.x"));
            Assert.That(IpMasker.Mask("nope"), Is.Null);
            Assert.That(IpMasker.Mask(null), Is.Null);
        });
    }

    [Test]
    public async Task RecordLoginAttempt_StoresMaskedIp_AndDropsEmailForUnknownAccounts()
    {
        var events = new Queue();
        var handler = new RecordLoginAttemptCommandHandler(events, new FakeTimeProvider());

        await handler.Handle(new RecordLoginAttemptCommand(null, "typo@example.com", false, "45.227.1.2", LoginFailureReason.InvalidPassword), CancellationToken.None);
        await handler.Handle(new RecordLoginAttemptCommand(Guid.NewGuid(), "a@example.com", true, "84.198.1.2"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(events.Items[0].Email, Is.Null);
            Assert.That(events.Items[0].MaskedIp, Is.EqualTo("45.227.x.x"));
            Assert.That(events.Items[0].FailureReason, Is.EqualTo(LoginFailureReason.InvalidPassword));
            Assert.That(events.Items[1].Email, Is.EqualTo("a@example.com"));
            Assert.That(events.Items[1].FailureReason, Is.Null);
        });
    }

    [Test]
    public async Task ListLoginEvents_MapsEvents_AndValidatorBoundsLimit()
    {
        var events = new Events();
        events.Items.Add(new LoginEvent(DateTime.UtcNow, null, null, false, "1.2.x.x"));
        var result = await new ListLoginEventsQueryHandler(events).Handle(new ListLoginEventsQuery(10), CancellationToken.None);
        Assert.That(result.Single().Succeeded, Is.False);

        var validator = new ListLoginEventsQueryValidator();
        Assert.Multiple(() =>
        {
            Assert.That(validator.Validate(new ListLoginEventsQuery(0)).IsValid, Is.False);
            Assert.That(validator.Validate(new ListLoginEventsQuery(201)).IsValid, Is.False);
            Assert.That(validator.Validate(new ListLoginEventsQuery(200)).IsValid, Is.True);
        });
    }

    private sealed class Me(Guid id) : ICurrentUserService
    {
        public Guid UserId { get; } = id;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class Repo(params User[] users) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult(users.FirstOrDefault(u => u.Email == email));
        public Task<IReadOnlyList<User>> ListAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(users);
        public Task UpdateAsync(User user) => Task.CompletedTask;
    }

    private sealed class Administration : IUserAdministrationService
    {
        public List<Guid> Revoked { get; } = [];
        public List<Guid> Deleted { get; } = [];
        public List<Guid> Invalidated { get; } = [];
        public Dictionary<Guid, string> Roles { get; } = new();
        public Dictionary<Guid, bool> Blocked { get; } = new();

        public Task SetRoleAsync(Guid userId, string role, CancellationToken ct = default) { Roles[userId] = role; return Task.CompletedTask; }
        public Task SetBlockedAsync(Guid userId, bool blocked, CancellationToken ct = default) { Blocked[userId] = blocked; return Task.CompletedTask; }
        public Task RevokeSessionsAsync(Guid userId, CancellationToken ct = default) { Revoked.Add(userId); return Task.CompletedTask; }
        public Task InvalidateAccessTokensAsync(Guid userId, CancellationToken ct = default) { Invalidated.Add(userId); return Task.CompletedTask; }
        public Task DeleteAsync(Guid userId, CancellationToken ct = default) { Deleted.Add(userId); return Task.CompletedTask; }
    }

    private sealed class Events : ILoginEventRepository
    {
        public List<LoginEvent> Items { get; } = [];
        public Task AddRangeAsync(IReadOnlyCollection<LoginEvent> loginEvents, CancellationToken ct = default) { Items.AddRange(loginEvents); return Task.CompletedTask; }
        public Task<IReadOnlyList<LoginEvent>> ListRecentAsync(int limit, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LoginEvent>>(Items);
    }

    private sealed class Queue : ILoginEventQueue
    {
        public List<LoginEvent> Items { get; } = [];
        public bool TryEnqueue(LoginEvent loginEvent) { Items.Add(loginEvent); return true; }
    }
}
