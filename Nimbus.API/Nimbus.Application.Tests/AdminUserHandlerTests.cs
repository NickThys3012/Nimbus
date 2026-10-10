using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Admin.Commands.ApproveUser;
using Nimbus.Application.Features.Admin.Queries.ListUsers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class AdminUserHandlerTests
{
    [Test]
    public async Task ApproveUser_SetsIsApprovedOnlyOnTargetUser()
    {
        var target = NewUser("target@example.com");
        var other = NewUser("other@example.com");
        var repo = new Repo(target, other);
        var handler = new ApproveUserCommandHandler(repo, new Admin(Guid.NewGuid()), NullLogger<ApproveUserCommandHandler>.Instance);

        await handler.Handle(new ApproveUserCommand(target.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(target.IsApproved, Is.True);
            Assert.That(other.IsApproved, Is.False);
        });
    }

    [Test]
    public void ApproveUser_UnknownUser_ThrowsNotFound()
    {
        var handler = new ApproveUserCommandHandler(new Repo(), new Admin(Guid.NewGuid()), NullLogger<ApproveUserCommandHandler>.Instance);

        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ApproveUserCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Test]
    public async Task ListUsers_ReturnsEveryUserWithApprovalAndRoles()
    {
        var approved = new User(Guid.NewGuid(), "a@example.com", "A", "A", UserRole.Admin, true, isApproved: true, roles: ["Admin"]);
        var pending = NewUser("b@example.com");
        var handler = new ListUsersQueryHandler(new Repo(approved, pending));

        var result = await handler.Handle(new ListUsersQuery(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Single(u => u.Email == "a@example.com").Roles, Is.EqualTo(new[] { "Admin" }));
            Assert.That(result.Single(u => u.Email == "b@example.com").IsApproved, Is.False);
        });
    }

    private static User NewUser(string email)
    {
        return new User(Guid.NewGuid(), email, "Doe", "Jane", UserRole.Pilot, true, roles: ["Pilot"]);
    }

    private sealed class Admin(Guid id) : ICurrentUserService
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
}
