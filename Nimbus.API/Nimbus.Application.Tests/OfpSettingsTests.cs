using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.OfpSettings;
using Nimbus.Application.Features.OfpSettings.Commands.ResetMyOfpSettings;
using Nimbus.Application.Features.OfpSettings.Commands.SaveMyOfpSettings;
using Nimbus.Application.Features.OfpSettings.Queries.GetMyOfpSettings;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class OfpSettingsTests
{
    [Test]
    public async Task Get_WithoutRow_ReturnsDefaultOf7Kg()
    {
        var dto = await new GetMyOfpSettingsQueryHandler(new FakeRepo(), new FakeUser(Guid.NewGuid())).Handle(new(), default);
        Assert.That(dto, Is.EqualTo(new OfpSettingsDto(7m, false)));
    }

    [Test]
    public async Task Get_NeverReturnsAnotherUsersRow()
    {
        var repo = new FakeRepo();
        repo.Rows.Add(new OfpSettings { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), EquipmentWeightPerPassengerKg = 12m });
        var dto = await new GetMyOfpSettingsQueryHandler(repo, new FakeUser(Guid.NewGuid())).Handle(new(), default);
        Assert.That(dto, Is.EqualTo(new OfpSettingsDto(7m, false)));
    }

    [Test]
    public async Task Save_CreatesOwnRow_ThenUpdatesInPlace_KeepingIdAndOwner()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        var handler = new SaveMyOfpSettingsCommandHandler(repo, new FakeUser(me));
        Assert.That(await handler.Handle(new(new(9m)), default), Is.EqualTo(new OfpSettingsDto(9m, true)));
        var id = repo.Rows.Single().Id;

        await handler.Handle(new(new(5.5m)), default);
        Assert.That(repo.Rows, Has.Count.EqualTo(1));
        Assert.That(repo.Rows[0].Id, Is.EqualTo(id));
        Assert.That(repo.Rows[0].OwnerId, Is.EqualTo(me));
        Assert.That(repo.Rows[0].EquipmentWeightPerPassengerKg, Is.EqualTo(5.5m));
    }

    [Test]
    public async Task SaveAndReset_NeverTouchOtherUsersRows()
    {
        var repo = new FakeRepo();
        var other = new OfpSettings { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), EquipmentWeightPerPassengerKg = 12m };
        repo.Rows.Add(other);
        var me = Guid.NewGuid();
        await new SaveMyOfpSettingsCommandHandler(repo, new FakeUser(me)).Handle(new(new(9m)), default);
        var dto = await new ResetMyOfpSettingsCommandHandler(repo, new FakeUser(me)).Handle(new(), default);
        Assert.That(repo.Rows, Is.EquivalentTo(new[] { other }));
        Assert.That(other.EquipmentWeightPerPassengerKg, Is.EqualTo(12m));
        Assert.That(dto, Is.EqualTo(new OfpSettingsDto(7m, false)));
    }

    [TestCase(7, true)]
    [TestCase(0, true)]
    [TestCase(100, true)]
    [TestCase(-3, false)]
    [TestCase(101, false)]
    public void Validator_Range(int kg, bool valid)
    {
        Assert.That(new OfpSettingsRequestValidator().Validate(new OfpSettingsRequestDto(kg)).IsValid, Is.EqualTo(valid));
    }

    [Test]
    public void Validator_RejectsMoreThanTwoDecimals() =>
        Assert.That(new OfpSettingsRequestValidator().Validate(new OfpSettingsRequestDto(7.123m)).IsValid, Is.False);

    [Test]
    public void Calculator_UsesEquipmentWeightOnlyInTotal_AndRecordedFiguresAreUnaffectedByLaterSettings()
    {
        // 8 passengers x 7 kg = 56 kg added on top of the (separately computed) basic weight.
        Assert.That(OfpWeightCalculator.EquipmentWeightKg(8, 7m), Is.EqualTo(56m));
        var recordedTotal = OfpWeightCalculator.TotalWeightKg(600m, 8, 7m);
        Assert.That(recordedTotal, Is.EqualTo(656m));
        // The setting changes to 9 kg: a new flight uses it, the already recorded figure is a plain value.
        Assert.That(OfpWeightCalculator.TotalWeightKg(600m, 8, 9m), Is.EqualTo(672m));
        Assert.That(recordedTotal, Is.EqualTo(656m));
        // A zero equipment weight leaves the basic weight untouched.
        Assert.That(OfpWeightCalculator.TotalWeightKg(600m, 8, 0m), Is.EqualTo(600m));
    }

    private sealed class FakeUser(Guid id) : ICurrentUserService
    {
        public Guid UserId => id;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class FakeRepo : IOfpSettingsRepository
    {
        public List<OfpSettings> Rows { get; } = [];

        public Task<OfpSettings?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.OwnerId == ownerId));

        public Task AddAsync(OfpSettings e, CancellationToken ct = default) { Rows.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(OfpSettings e, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(OfpSettings e, CancellationToken ct = default) { Rows.Remove(e); return Task.CompletedTask; }
    }
}
