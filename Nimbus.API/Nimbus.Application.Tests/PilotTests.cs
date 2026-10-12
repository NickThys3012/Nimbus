using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Application.Features.Pilots;
using Nimbus.Contracts.DTOs.Features.Pilots;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class PilotTests
{
    [Test]
    public Task PilotService_SatisfiesOwnerScopingContract()
    {
        return OwnedServiceContract.AssertHoldsAsync(
            repo => new PilotService(new Adapter(repo), NullLogger<PilotService>.Instance),
            () => new Pilot { Id = Guid.NewGuid(), Name = "N. Thys", WeightKg = 82m },
            p => p.WeightKg = 90m);
    }

    [Test]
    public void Mapper_TrimsName_AndRoundTrips()
    {
        var pilot = new PilotRequestDto("  N. Thys ", 82.5m).MapToEntity();
        Assert.That(pilot.Name, Is.EqualTo("N. Thys"));
        Assert.That(pilot.MapToDto().WeightKg, Is.EqualTo(82.5m));
    }

    [TestCase("N. Thys", "82", true)]
    [TestCase("N. Thys", "30", true)]
    [TestCase("N. Thys", "250", true)]
    [TestCase("N. Thys", "82.25", true)]
    [TestCase("N. Thys", "29.99", false)]
    [TestCase("N. Thys", "250.01", false)]
    [TestCase("N. Thys", "0", false)]
    [TestCase("N. Thys", "-5", false)]
    [TestCase("N. Thys", "82.123", false)]
    [TestCase("", "82", false)]
    [TestCase("   ", "82", false)]
    public void Validator_Rules(string name, string weight, bool valid)
    {
        var w = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(new PilotRequestValidator().Validate(new PilotRequestDto(name, w)).IsValid, Is.EqualTo(valid));
    }

    [Test]
    public void Validator_NameTooLong_Fails()
    {
        Assert.That(new PilotRequestValidator().Validate(new PilotRequestDto(new string('A', 101), 80m)).IsValid, Is.False);
    }

    private sealed class Adapter(IOwnedRepository<Pilot> inner) : IPilotRepository
    {
        public Task<Pilot?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<IReadOnlyList<Pilot>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default) => inner.ListByOwnerAsync(ownerId, ct);
        public Task<IReadOnlyList<Pilot>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        public Task AddAsync(Pilot e, CancellationToken ct = default) => inner.AddAsync(e, ct);
        public Task UpdateAsync(Pilot e, CancellationToken ct = default) => inner.UpdateAsync(e, ct);
        public Task RemoveAsync(Pilot e, CancellationToken ct = default) => inner.RemoveAsync(e, ct);
    }
}
