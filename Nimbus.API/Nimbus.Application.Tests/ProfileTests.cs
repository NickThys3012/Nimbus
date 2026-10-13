using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Profile;
using Nimbus.Application.Features.Profile.Commands.SaveMyProfile;
using Nimbus.Application.Features.Profile.Queries.GetMyProfile;
using Nimbus.Contracts.DTOs.Features.Profile;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class ProfileTests
{
    [Test]
    public async Task Get_WithoutRow_ReturnsEmptyProfile()
    {
        var dto = await new GetMyProfileQueryHandler(new FakeRepo(), new FakeUser(Guid.NewGuid())).Handle(new(), default);
        Assert.That(dto, Is.EqualTo(new ProfileDto(null, null)));
    }

    [Test]
    public async Task Get_NeverReturnsAnotherUsersProfile()
    {
        var repo = new FakeRepo();
        repo.Rows.Add(new UserProfile { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), OperatorName = "Other", PicWeightKg = 70m });
        var dto = await new GetMyProfileQueryHandler(repo, new FakeUser(Guid.NewGuid())).Handle(new(), default);
        Assert.That(dto, Is.EqualTo(new ProfileDto(null, null)));
    }

    [Test]
    public async Task Save_CreatesOwnRow_ThenUpdatesInPlace_KeepingIdAndOwner()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        var handler = new SaveMyProfileCommandHandler(repo, new FakeUser(me));
        await handler.Handle(new(new ProfileRequestDto("  Skyline  ", 82m)), default);
        var id = repo.Rows.Single().Id;

        var dto = await handler.Handle(new(new ProfileRequestDto("Skyline", 79m)), default);
        Assert.That(repo.Rows, Has.Count.EqualTo(1));
        Assert.That(repo.Rows[0].Id, Is.EqualTo(id));
        Assert.That(repo.Rows[0].OwnerId, Is.EqualTo(me));
        Assert.That(dto, Is.EqualTo(new ProfileDto("Skyline", 79m)));
        Assert.That(repo.Adds, Is.EqualTo(1));
    }

    [Test]
    public async Task Save_BlankName_IsStoredAsNull_AndValuesCanBeCleared()
    {
        var repo = new FakeRepo();
        var handler = new SaveMyProfileCommandHandler(repo, new FakeUser(Guid.NewGuid()));
        await handler.Handle(new(new ProfileRequestDto("X", 80m)), default);
        var dto = await handler.Handle(new(new ProfileRequestDto("   ", null)), default);
        Assert.That(dto, Is.EqualTo(new ProfileDto(null, null)));
    }

    [Test]
    public async Task Save_NeverTouchesOtherUsersRows()
    {
        var repo = new FakeRepo();
        var other = new UserProfile { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), OperatorName = "Other", PicWeightKg = 70m };
        repo.Rows.Add(other);
        var me = Guid.NewGuid();
        await new SaveMyProfileCommandHandler(repo, new FakeUser(me)).Handle(new(new ProfileRequestDto("Mine", 90m)), default);
        Assert.That(other.OperatorName, Is.EqualTo("Other"));
        Assert.That(other.PicWeightKg, Is.EqualTo(70m));
        Assert.That(repo.Rows.Count(r => r.OwnerId == me), Is.EqualTo(1));
    }

    [TestCase("Skyline", 82, true)]
    [TestCase(null, null, true)]
    [TestCase("", 0.5, true)]
    [TestCase("x", 0, false)]
    [TestCase("x", -5, false)]
    [TestCase("x", 501, false)]
    [TestCase("x", 500, true)]
    public void Validator_Rules(string? name, double? weight, bool valid)
    {
        var result = new ProfileRequestValidator().Validate(new ProfileRequestDto(name, (decimal?)weight));
        Assert.That(result.IsValid, Is.EqualTo(valid));
    }

    [Test]
    public void Validator_RejectsLongNameAndTooManyDecimals_AndKeysByProperty()
    {
        var v = new ProfileRequestValidator();
        Assert.That(v.Validate(new ProfileRequestDto(new string('a', 201), null)).IsValid, Is.False);
        var bad = v.Validate(new ProfileRequestDto("x", 80.123m));
        Assert.That(bad.Errors.Single().PropertyName, Is.EqualTo(nameof(ProfileRequestDto.PicWeightKg)));
        var zero = v.Validate(new ProfileRequestDto("x", 0m));
        Assert.That(zero.Errors.Single().ErrorMessage, Is.EqualTo("Enter a weight above 0 kg."));
    }

    private sealed class FakeUser(Guid id) : ICurrentUserService
    {
        public Guid UserId => id;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class FakeRepo : IUserProfileRepository
    {
        public List<UserProfile> Rows { get; } = [];
        public int Adds { get; private set; }

        public Task<UserProfile?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.OwnerId == ownerId));

        public Task AddAsync(UserProfile e, CancellationToken ct = default) { Adds++; Rows.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(UserProfile e, CancellationToken ct = default) => Task.CompletedTask;
    }
}
