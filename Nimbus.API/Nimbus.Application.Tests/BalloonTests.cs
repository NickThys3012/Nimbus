using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Ownership;
using Nimbus.Application.Features.Balloons;
using Nimbus.Contracts.DTOs.Features.Balloons;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class BalloonTests
{
    private static Balloon NewBalloon(string registration = "OO-ABC") => new()
    {
        Id = Guid.NewGuid(), Registration = registration, Type = "Cameron Z-90",
        EnvelopeVolumeM3 = 2500, MaxEnvelopeTemperatureC = 100,
        EnvelopeWeightKg = 120m, BasketWeightKg = 80m, BurnerWeightKg = 20m, CylindersWeightKg = 30m
    };

    [Test]
    public Task BalloonService_SatisfiesOwnerScopingContract()
    {
        return OwnedServiceContract.AssertHoldsAsync(
            repo => new BalloonService(new Adapter(repo), new FakeUsage(), NullLogger<BalloonService>.Instance),
            () => NewBalloon(),
            b => b.Type = "changed");
    }

    [Test]
    public void EmptyWeight_IsSumOfReferenceWeights()
    {
        Assert.That(NewBalloon().EmptyWeightKg, Is.EqualTo(250m));
        Assert.That(NewBalloon().MapToDto().EmptyWeightKg, Is.EqualTo(250m));
    }

    [Test]
    public async Task Create_DuplicateRegistrationForSameOwner_Throws_CaseInsensitive()
    {
        var service = new BalloonService(new FakeRepo(), new FakeUsage(), NullLogger<BalloonService>.Instance);
        var owner = new Caller(Guid.NewGuid(), false);
        await service.CreateAsync(owner, new BalloonRequestDto("oo-abc ", "T", 1, 1, 0, 0, 0, 0).MapToEntity());

        Assert.ThrowsAsync<DuplicateException>(() => service.CreateAsync(owner, NewBalloon("OO-ABC")));
    }

    [Test]
    public async Task Create_SameRegistrationForDifferentOwners_IsAllowed()
    {
        var service = new BalloonService(new FakeRepo(), new FakeUsage(), NullLogger<BalloonService>.Instance);
        await service.CreateAsync(new Caller(Guid.NewGuid(), false), NewBalloon());
        Assert.DoesNotThrowAsync(() => service.CreateAsync(new Caller(Guid.NewGuid(), false), NewBalloon()));
    }

    [Test]
    public async Task Update_ToAnotherOfMyRegistrations_Throws_ButKeepingOwnIsFine()
    {
        var service = new BalloonService(new FakeRepo(), new FakeUsage(), NullLogger<BalloonService>.Instance);
        var owner = new Caller(Guid.NewGuid(), false);
        var a = await service.CreateAsync(owner, NewBalloon("OO-A"));
        await service.CreateAsync(owner, NewBalloon("OO-B"));

        Assert.ThrowsAsync<DuplicateException>(() => service.UpdateAsync(owner, a.Id, NewBalloon("OO-B")));
        Assert.That(await service.UpdateAsync(owner, a.Id, NewBalloon("OO-A")), Is.EqualTo(OwnedResult.Success));
    }

    [TestCase("", "T", 1, 1, 0, false)]
    [TestCase("OO-A", "", 1, 1, 0, true)] // type is optional
    [TestCase("OO-A", "T", 0, 1, 0, false)]
    [TestCase("OO-A", "T", 1, 0, 0, false)]
    [TestCase("OO-A", "T", 1, 1, -1, false)]
    [TestCase("OO-A", "T", 2500, 100, 10, true)]
    public void Validator_Rules(string reg, string type, int vol, int temp, int weight, bool valid)
    {
        var result = new BalloonRequestValidator().Validate(new BalloonRequestDto(reg, type, vol, temp, weight, weight, weight, weight));
        Assert.That(result.IsValid, Is.EqualTo(valid));
    }

    [Test]
    public void Validator_RegistrationTooLong_Fails()
    {
        var result = new BalloonRequestValidator().Validate(new BalloonRequestDto(new string('A', 21), "T", 1, 1, 0, 0, 0, 0));
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task Delete_WhenFlightsUseTheBalloon_IsBlockedAndBalloonRetained()
    {
        var repo = new FakeRepo();
        var usage = new FakeUsage { Result = new BalloonUsage(9, [new BalloonFlightRef("VR 08 · Weelde", new DateOnly(2026, 10, 8))]) };
        var service = new BalloonService(repo, usage, NullLogger<BalloonService>.Instance);
        var owner = new Caller(Guid.NewGuid(), false);
        var created = await service.CreateAsync(owner, NewBalloon());

        var ex = Assert.ThrowsAsync<InUseException>(() => service.DeleteAsync(owner, created.Id));
        Assert.That(ex!.Message, Does.Contain("9 flights"));
        Assert.That(await service.GetAsync(owner, created.Id), Is.Not.Null, "balloon must be retained");
        Assert.That(usage.LastOwnerId, Is.EqualTo(owner.UserId), "usage is looked up for the balloon's owner");
    }

    [Test]
    public async Task Delete_WhenNoFlightsUseTheBalloon_Deletes()
    {
        var service = new BalloonService(new FakeRepo(), new FakeUsage(), NullLogger<BalloonService>.Instance);
        var owner = new Caller(Guid.NewGuid(), false);
        var created = await service.CreateAsync(owner, NewBalloon());

        Assert.That(await service.DeleteAsync(owner, created.Id), Is.EqualTo(OwnedResult.Success));
        Assert.That(await service.GetAsync(owner, created.Id), Is.Null);
    }

    [Test]
    public async Task Usage_OfAnotherUsersBalloon_IsNull_AndNeverQueried()
    {
        var usage = new FakeUsage();
        var service = new BalloonService(new FakeRepo(), usage, NullLogger<BalloonService>.Instance);
        var created = await service.CreateAsync(new Caller(Guid.NewGuid(), false), NewBalloon());
        var stranger = new Caller(Guid.NewGuid(), false);

        Assert.That(await service.GetUsageAsync(stranger, created.Id), Is.Null);
        Assert.That(await service.DeleteAsync(stranger, created.Id), Is.EqualTo(OwnedResult.NotFound));
        Assert.That(usage.Calls, Is.Zero);
    }

    private sealed class FakeUsage : IBalloonUsageReader
    {
        public BalloonUsage Result { get; set; } = BalloonUsage.None;
        public int Calls { get; private set; }
        public Guid? LastOwnerId { get; private set; }

        public Task<BalloonUsage> GetUsageAsync(Guid ownerId, Guid balloonId, CancellationToken ct = default)
        {
            Calls++;
            LastOwnerId = ownerId;
            return Task.FromResult(Result);
        }
    }

    private sealed class Adapter(IOwnedRepository<Balloon> inner) : IBalloonRepository
    {
        public Task<Balloon?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<IReadOnlyList<Balloon>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        public Task AddAsync(Balloon e, CancellationToken ct = default) => inner.AddAsync(e, ct);
        public Task UpdateAsync(Balloon e, CancellationToken ct = default) => inner.UpdateAsync(e, ct);
        public Task RemoveAsync(Balloon e, CancellationToken ct = default) => inner.RemoveAsync(e, ct);

        public async Task<bool> RegistrationExistsAsync(Guid ownerId, string registration, Guid? excludingId, CancellationToken ct = default)
            => (await inner.ListAsync(ct)).Any(b => b.OwnerId == ownerId && b.Id != excludingId
                && b.Registration == Balloon.NormalizeRegistration(registration));
    }

    private sealed class FakeRepo : IBalloonRepository
    {
        private readonly List<Balloon> _items = [];
        public Task<Balloon?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_items.FirstOrDefault(b => b.Id == id));
        public Task<IReadOnlyList<Balloon>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Balloon>>(_items.ToList());
        public Task AddAsync(Balloon e, CancellationToken ct = default) { _items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Balloon e, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(Balloon e, CancellationToken ct = default) { _items.Remove(e); return Task.CompletedTask; }

        public Task<bool> RegistrationExistsAsync(Guid ownerId, string registration, Guid? excludingId, CancellationToken ct = default)
            => Task.FromResult(_items.Any(b => b.OwnerId == ownerId && b.Id != excludingId
                && b.Registration == Balloon.NormalizeRegistration(registration)));
    }
}
