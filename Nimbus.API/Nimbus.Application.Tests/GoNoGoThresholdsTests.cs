using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.GoNoGoThresholds;
using Nimbus.Application.Features.GoNoGoThresholds.Commands.ResetMyGoNoGoThresholds;
using Nimbus.Application.Features.GoNoGoThresholds.Commands.SaveMyGoNoGoThresholds;
using Nimbus.Application.Features.GoNoGoThresholds.Queries.GetMyGoNoGoThresholds;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class GoNoGoThresholdsTests
{
    private static readonly GoNoGoThresholdsRequestDto Valid = new(8m, 12m, 6m, 4m, 250m, 400m);

    [Test]
    public async Task Get_WithoutRow_ReturnsBuiltInDefaults()
    {
        var dto = await new GetMyGoNoGoThresholdsQueryHandler(new FakeRepo(), new FakeUser(Guid.NewGuid())).Handle(new(), default);
        Assert.That(dto, Is.EqualTo(new GoNoGoThresholdsDto(10m, 15m, 5m, 3m, 300m, 500m, false)));
    }

    [Test]
    public async Task Get_WithoutOwnRow_FallsBackToGlobalDefault_NotToAnotherUsersRow()
    {
        var repo = new FakeRepo();
        repo.Rows.Add(new GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), WindYellowKt = 1m, WindRedKt = 2m, VisibilityYellowKm = 9m, VisibilityRedKm = 8m, CapeYellow = 1m, CapeRed = 2m });
        repo.Rows.Add(new GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = null, WindYellowKt = 7m, WindRedKt = 9m, VisibilityYellowKm = 6m, VisibilityRedKm = 2m, CapeYellow = 100m, CapeRed = 200m });
        var dto = await new GetMyGoNoGoThresholdsQueryHandler(repo, new FakeUser(Guid.NewGuid())).Handle(new(), default);
        Assert.That(dto, Is.EqualTo(new GoNoGoThresholdsDto(7m, 9m, 6m, 2m, 100m, 200m, false)));
    }

    [Test]
    public async Task Save_CreatesOwnRow_ThenUpdatesItInPlace_KeepingIdAndOwner()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        var handler = new SaveMyGoNoGoThresholdsCommandHandler(repo, new FakeUser(me));
        Assert.That((await handler.Handle(new(Valid), default)).IsCustom, Is.True);
        var id = repo.Rows.Single().Id;

        await handler.Handle(new(Valid with { WindRedKt = 14m }), default);
        Assert.That(repo.Rows, Has.Count.EqualTo(1));
        Assert.That(repo.Rows[0].Id, Is.EqualTo(id));
        Assert.That(repo.Rows[0].OwnerId, Is.EqualTo(me));
        Assert.That(repo.Rows[0].WindRedKt, Is.EqualTo(14m));
        Assert.That(repo.Adds, Is.EqualTo(1));
    }

    [Test]
    public async Task Save_NeverTouchesGlobalDefaultOrOtherUsersRows()
    {
        var repo = new FakeRepo();
        var other = new GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), WindYellowKt = 1m, WindRedKt = 2m, VisibilityYellowKm = 9m, VisibilityRedKm = 8m, CapeYellow = 1m, CapeRed = 2m };
        var global = new GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = null, WindYellowKt = 1m, WindRedKt = 2m, VisibilityYellowKm = 9m, VisibilityRedKm = 8m, CapeYellow = 1m, CapeRed = 2m };
        repo.Rows.AddRange([other, global]);
        var me = Guid.NewGuid();
        await new SaveMyGoNoGoThresholdsCommandHandler(repo, new FakeUser(me)).Handle(new(Valid), default);
        Assert.That(other.WindYellowKt, Is.EqualTo(1m));
        Assert.That(global.WindYellowKt, Is.EqualTo(1m));
        Assert.That(repo.Rows, Has.Count.EqualTo(3));
        Assert.That(repo.Rows.Count(r => r.OwnerId == me), Is.EqualTo(1));
    }

    [Test]
    public async Task Reset_RemovesOnlyOwnRow_AndReturnsDefaults()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        var other = new GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        var global = new GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = null, WindYellowKt = 11m, WindRedKt = 16m, VisibilityYellowKm = 5m, VisibilityRedKm = 3m, CapeYellow = 300m, CapeRed = 500m };
        repo.Rows.AddRange([other, global]);
        await new SaveMyGoNoGoThresholdsCommandHandler(repo, new FakeUser(me)).Handle(new(Valid), default);

        var dto = await new ResetMyGoNoGoThresholdsCommandHandler(repo, new FakeUser(me)).Handle(new(), default);
        Assert.That(repo.Rows, Is.EquivalentTo(new[] { other, global }));
        Assert.That(dto.IsCustom, Is.False);
        Assert.That(dto.WindYellowKt, Is.EqualTo(11m), "falls back to the global default row");
    }

    [TestCase(8, 12, 6, 4, 250, 400, true)]
    [TestCase(10, 15, 5, 3, 300, 500, true)]
    [TestCase(10, 10, 5, 3, 300, 500, false)] // wind red not above yellow
    [TestCase(15, 10, 5, 3, 300, 500, false)]
    [TestCase(10, 15, 3, 5, 300, 500, false)] // visibility red not below yellow
    [TestCase(10, 15, 5, 5, 300, 500, false)]
    [TestCase(10, 15, 5, 3, 500, 300, false)] // CAPE
    [TestCase(0, 15, 5, 3, 300, 500, false)] // must be positive
    [TestCase(10, 101, 5, 3, 300, 500, false)] // above range
    [TestCase(10, 15, 5, 0, 300, 500, false)]
    [TestCase(10, 15, 5, 3, 300, 10001, false)]
    public void Validator_Rules(int wy, int wr, int vy, int vr, int cy, int cr, bool valid)
    {
        var result = new GoNoGoThresholdsRequestValidator().Validate(new GoNoGoThresholdsRequestDto(wy, wr, vy, vr, cy, cr));
        Assert.That(result.IsValid, Is.EqualTo(valid));
    }

    [Test]
    public void Validator_RejectsMoreThanTwoDecimals_AndNamesTheOffendingField()
    {
        Assert.That(new GoNoGoThresholdsRequestValidator().Validate(Valid with { WindYellowKt = 8.123m }).IsValid, Is.False);
        var bad = new GoNoGoThresholdsRequestValidator().Validate(new GoNoGoThresholdsRequestDto(15, 10, 5, 3, 300, 500));
        Assert.That(bad.Errors.Single().PropertyName, Is.EqualTo(nameof(GoNoGoThresholdsRequestDto.WindRedKt)));
        Assert.That(bad.Errors.Single().ErrorMessage, Is.EqualTo("Must be higher than 15 kt."));
    }

    private sealed class FakeUser(Guid id) : ICurrentUserService
    {
        public Guid UserId => id;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class FakeRepo : IGoNoGoThresholdsRepository
    {
        public List<GoNoGoThreshold> Rows { get; } = [];
        public int Adds { get; private set; }

        public Task<GoNoGoThreshold?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.OwnerId == ownerId));

        public Task<GoNoGoThreshold?> GetGlobalDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.OwnerId is null));

        public Task AddAsync(GoNoGoThreshold e, CancellationToken ct = default) { Adds++; Rows.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(GoNoGoThreshold e, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(GoNoGoThreshold e, CancellationToken ct = default) { Rows.Remove(e); return Task.CompletedTask; }
    }
}
