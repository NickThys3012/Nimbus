using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightPreparations.Commands.DeleteFlightPreparation;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Tests;

/// <summary>Issue #43: only the owner or an admin deletes; everyone else is rejected and logged; the files follow the row without ever blocking it.</summary>
public class DeleteFlightHandlerTests
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _flight = Guid.NewGuid();

    private DeleteFlightPreparationCommandHandler Handler(Fakes f, Guid viewer, bool admin = false) =>
        new(f.Access, f.Repo, f.Storage, new Me(viewer, admin), f.Log);

    private Fakes Flight(FlightViewerRole? role, FlightDeletion? deletion = null) => new(_flight, _owner, role, deletion ?? new FlightDeletion([], null));

    [Test]
    public async Task Owner_DeletesTheirFlight_AskedAsTheOwner()
    {
        var f = Flight(FlightViewerRole.Owner);
        await Handler(f, _owner).Handle(new DeleteFlightPreparationCommand(_flight), default);

        Assert.That(f.Repo.DeletedAsOwner, Is.EqualTo(_owner));
        Assert.That(f.Repo.DeletedFlight, Is.EqualTo(_flight));
    }

    [Test]
    public async Task Admin_DeletesSomeoneElsesFlight_OnTheOwnersBehalf_AndItIsLogged()
    {
        var admin = Guid.NewGuid();
        var f = Flight(FlightViewerRole.Admin);
        await Handler(f, admin, admin: true).Handle(new DeleteFlightPreparationCommand(_flight), default);

        Assert.That(f.Repo.DeletedAsOwner, Is.EqualTo(_owner), "the owner-scoped repository is asked as the flight's owner");
        var entry = f.Log.Entries.Single(e => e.Level == LogLevel.Information);
        Assert.That(entry.Message, Does.Contain(_flight.ToString()).And.Contain(admin.ToString()).And.Contain("Admin"));
    }

    [TestCase(FlightViewerRole.Shared)]
    [TestCase(null)]
    public void SomeoneWhoMayNotManageIt_IsRejected_NothingIsDeleted_AndTheAttemptIsLogged(FlightViewerRole? role)
    {
        var intruder = Guid.NewGuid();
        var f = Flight(role);

        Assert.ThrowsAsync<ForbiddenException>(() => Handler(f, intruder).Handle(new DeleteFlightPreparationCommand(_flight), default));

        Assert.That(f.Repo.DeletedFlight, Is.Null);
        Assert.That(f.Storage.Deleted, Is.Empty);
        var entry = f.Log.Entries.Single(e => e.Level == LogLevel.Warning);
        Assert.That(entry.Message, Does.Contain(intruder.ToString()).And.Contain(_flight.ToString()));
    }

    [Test]
    public void AMissingFlight_IsNotFound_AndNothingIsLoggedAsAnAttack()
    {
        var f = new Fakes(_flight, _owner, role: null, deletion: null, exists: false);

        Assert.ThrowsAsync<NotFoundException>(() => Handler(f, _owner).Handle(new DeleteFlightPreparationCommand(_flight), default));

        Assert.That(f.Log.Entries.Where(e => e.Level >= LogLevel.Warning), Is.Empty);
    }

    [Test]
    public void AFlightThatVanishedBetweenTheCheckAndTheDelete_IsNotFound()
    {
        var f = new Fakes(_flight, _owner, FlightViewerRole.Owner, deletion: null);

        Assert.ThrowsAsync<NotFoundException>(() => Handler(f, _owner).Handle(new DeleteFlightPreparationCommand(_flight), default));
    }

    [Test]
    public async Task ImagesAndTheKmlTrack_AreRemovedFromTheirBuckets()
    {
        var f = Flight(FlightViewerRole.Owner, new FlightDeletion(["o/f/a.png", "o/f/b.png"], "o/f/track.kml"));
        await Handler(f, _owner).Handle(new DeleteFlightPreparationCommand(_flight), default);

        Assert.That(f.Storage.Deleted, Is.EqualTo(new[]
        {
            (StorageBucket.FlightImages, "o/f/a.png"), (StorageBucket.FlightImages, "o/f/b.png"), (StorageBucket.FlightTracks, "o/f/track.kml")
        }));
    }

    [Test]
    public async Task AFileThatCannotBeRemoved_DoesNotFailTheDelete_IsLoggedAsOrphaned_AndTheRestAreStillTried()
    {
        var f = Flight(FlightViewerRole.Owner, new FlightDeletion(["o/f/a.png", "o/f/broken.png", "o/f/c.png"], null));
        f.Storage.FailFor = "o/f/broken.png";

        await Handler(f, _owner).Handle(new DeleteFlightPreparationCommand(_flight), default);

        Assert.That(f.Repo.DeletedFlight, Is.EqualTo(_flight), "the row is gone regardless");
        Assert.That(f.Storage.Deleted.Select(d => d.Key), Is.EqualTo(new[] { "o/f/a.png", "o/f/c.png" }));
        Assert.That(f.Log.Entries.Single(e => e.Level == LogLevel.Warning).Message, Does.Contain("orphaned").And.Contain("o/f/broken.png"));
    }

    [Test]
    public async Task AMalformedStoredKey_IsSkippedWithAWarning_NotFatal()
    {
        var f = Flight(FlightViewerRole.Owner, new FlightDeletion(["../escape.png"], null));

        await Handler(f, _owner).Handle(new DeleteFlightPreparationCommand(_flight), default);

        Assert.That(f.Storage.Deleted, Is.Empty);
        Assert.That(f.Log.Entries.Count(e => e.Level == LogLevel.Warning), Is.EqualTo(1));
    }

    private sealed class Fakes
    {
        public Fakes(Guid flight, Guid owner, FlightViewerRole? role, FlightDeletion? deletion, bool exists = true)
        {
            Access = new AccessFake(exists ? new FlightAccess(true, role, owner) : new FlightAccess(false, null));
            Repo = new RepoFake(deletion);
        }

        public AccessFake Access { get; }
        public RepoFake Repo { get; }
        public StorageFake Storage { get; } = new();
        public CapturingLogger<DeleteFlightPreparationCommandHandler> Log { get; } = new();
    }

    private sealed class Me(Guid id, bool admin) : ICurrentUserService
    {
        public Guid UserId => id;
        public bool IsAdmin => admin;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class AccessFake(FlightAccess access) : IFlightDetailReader
    {
        public Task<FlightAccess> GetAccessAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default) => Task.FromResult(access);

        public Task<FlightDetailResult> GetAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RepoFake(FlightDeletion? deletion) : IFlightPreparationRepository
    {
        public Guid? DeletedAsOwner { get; private set; }
        public Guid? DeletedFlight { get; private set; }

        public Task<FlightDeletion?> DeleteAsync(Guid ownerId, Guid id, CancellationToken ct = default)
        {
            if (deletion is null) return Task.FromResult<FlightDeletion?>(null);
            (DeletedAsOwner, DeletedFlight) = (ownerId, id);
            return Task.FromResult<FlightDeletion?>(deletion);
        }

        public Task<FlightPreparation?> GetByIdAsync(Guid ownerId, Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(FlightPreparation flight, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(Guid ownerId, FlightPreparation flight, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SetTrajectoryJsonAsync(Guid ownerId, Guid id, string? trajectoryJson, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SetKmlTrackAsync(Guid ownerId, Guid id, string? objectKey, string? contentType, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StorageFake : IObjectStorageService
    {
        public List<(StorageBucket Bucket, string Key)> Deleted { get; } = [];
        public string? FailFor { get; set; }

        public Task DeleteAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default)
        {
            if (key.Value == FailFor) throw new ObjectStorageException("down");
            Deleted.Add((bucket, key.Value));
            return Task.CompletedTask;
        }

        public Task UploadAsync(StorageBucket bucket, ObjectKey key, ObjectUpload upload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ObjectDownload?> DownloadAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Uri> GetPresignedDownloadUrlAsync(StorageBucket bucket, ObjectKey key) => throw new NotSupportedException();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
