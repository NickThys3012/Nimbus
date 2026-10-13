using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Application.Features.LaunchLocations;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class LaunchLocationTests
{
    private static LaunchLocationRequestDto Req(string name = "Dendermonde", string? icao = "EBDM", decimal lat = 51.02m, decimal lon = 4.1m, decimal elev = 8m, string? notes = null)
        => new(name, icao, lat, lon, elev, notes);

    [Test]
    public Task LaunchLocationService_SatisfiesOwnerScopingContract()
    {
        return OwnedServiceContract.AssertHoldsAsync(
            repo => new LaunchLocationService(new Adapter(repo), NullLogger<LaunchLocationService>.Instance),
            () => new LaunchLocation { Id = Guid.NewGuid(), Name = "Field", Latitude = 51m, Longitude = 4m },
            l => l.Latitude = 52m);
    }

    [Test]
    public void Mapper_TrimsText_UppercasesIcao_AndBlankOptionalsBecomeNull()
    {
        var e = Req("  Field ", " ebdm ", notes: "   ").MapToEntity();
        Assert.That(e.Name, Is.EqualTo("Field"));
        Assert.That(e.IcaoCode, Is.EqualTo("EBDM"));
        Assert.That(e.AirspaceNotes, Is.Null);
        Assert.That(Req(icao: "").MapToEntity().IcaoCode, Is.Null);
        Assert.That(Req(icao: null).MapToEntity().MapToDto().IcaoCode, Is.Null);
    }

    [TestCase("EBDM", true)]
    [TestCase("ebdm", true)]
    [TestCase("EB1M", true)]
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("EBD", false)]
    [TestCase("EBDMX", false)]
    [TestCase("EB M", false)]
    [TestCase("EB-M", false)]
    public void Validator_IcaoCode(string? icao, bool valid)
        => Assert.That(new LaunchLocationRequestValidator().Validate(Req(icao: icao)).IsValid, Is.EqualTo(valid));

    [TestCase("51.02", "4.1", true)]
    [TestCase("90", "180", true)]
    [TestCase("-90", "-180", true)]
    [TestCase("90.000001", "4", false)]
    [TestCase("-90.1", "4", false)]
    [TestCase("51", "180.1", false)]
    [TestCase("51", "-181", false)]
    [TestCase("51.1234567", "4", false)]
    [TestCase("51", "4.1234567", false)]
    public void Validator_Coordinates(string lat, string lon, bool valid)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Assert.That(new LaunchLocationRequestValidator().Validate(Req(lat: decimal.Parse(lat, inv), lon: decimal.Parse(lon, inv))).IsValid, Is.EqualTo(valid));
    }

    [TestCase("8", true)]
    [TestCase("-500", true)]
    [TestCase("9000", true)]
    [TestCase("-500.01", false)]
    [TestCase("9000.01", false)]
    [TestCase("8.123", false)]
    public void Validator_Elevation(string elev, bool valid)
        => Assert.That(new LaunchLocationRequestValidator().Validate(Req(elev: decimal.Parse(elev, System.Globalization.CultureInfo.InvariantCulture))).IsValid, Is.EqualTo(valid));

    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("Field", true)]
    public void Validator_Name(string name, bool valid)
        => Assert.That(new LaunchLocationRequestValidator().Validate(Req(name)).IsValid, Is.EqualTo(valid));

    [Test]
    public void Validator_LengthLimits()
    {
        Assert.That(new LaunchLocationRequestValidator().Validate(Req(new string('A', 101))).IsValid, Is.False);
        Assert.That(new LaunchLocationRequestValidator().Validate(Req(notes: new string('n', 2001))).IsValid, Is.False);
        Assert.That(new LaunchLocationRequestValidator().Validate(Req(notes: new string('n', 2000))).IsValid, Is.True);
    }

    private sealed class Adapter(IOwnedRepository<LaunchLocation> inner) : ILaunchLocationRepository
    {
        public Task<LaunchLocation?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<IReadOnlyList<LaunchLocation>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default) => inner.ListByOwnerAsync(ownerId, ct);
        public Task<IReadOnlyList<LaunchLocation>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        public Task AddAsync(LaunchLocation e, CancellationToken ct = default) => inner.AddAsync(e, ct);
        public Task UpdateAsync(LaunchLocation e, CancellationToken ct = default) => inner.UpdateAsync(e, ct);
        public Task RemoveAsync(LaunchLocation e, CancellationToken ct = default) => inner.RemoveAsync(e, ct);
    }
}
