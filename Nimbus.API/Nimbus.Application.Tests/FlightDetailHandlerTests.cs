using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightAssessment;
using Nimbus.Application.Features.FlightPreparations;
using Nimbus.Application.Features.FlightPreparations.Queries.GetFlightDetail;
using Nimbus.Contracts.DTOs.Features.FlightAssessment;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.Services;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Tests;

/// <summary>Issue #41: the read-only view. The server decides who may read; permissions, names, the single assessment and image links come back ready to render.</summary>
public class FlightDetailHandlerTests
{
    private static readonly FlightAssessmentInput Input = new(100, 50, 20, 30, 80, [70m], 500, 5, 10, 0);
    private static readonly FlightAssessmentDto Assessed = new(350m, 70m, 150m, true, "green");

    private static FlightPreparation Flight(Guid owner, params FlightPreparationImage[] images) => new()
    {
        Id = Guid.NewGuid(), OwnerId = owner, Title = "Weelde · avond", FlightDate = new DateOnly(2026, 8, 8), FlightTime = new TimeOnly(18, 45),
        Status = FlightPreparationStatus.Flown, FlownAt = new DateTime(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc), Images = images.ToList()
    };

    private static GetFlightDetailQueryHandler Handler(
        Reader reader, Guid viewer, bool admin = false, Directory? directory = null, Storage? storage = null) =>
        new(reader, new Assess(), directory ?? new Directory(), storage ?? new Storage(), new Me(viewer, admin), NullLogger<GetFlightDetailQueryHandler>.Instance);

    [Test]
    public async Task Owner_GetsEveryPermission_AndNoOwnerName()
    {
        var me = Guid.NewGuid();
        var flight = Flight(me);
        var directory = new Directory();
        var dto = await Handler(new Reader(flight, FlightViewerRole.Owner), me, directory: directory).Handle(new GetFlightDetailQuery(flight.Id), default);

        Assert.That(dto.ViewerRole, Is.EqualTo("Owner"));
        Assert.That(dto.Permissions, Is.EqualTo(new Nimbus.Contracts.DTOs.Features.FlightPreparations.FlightPermissionsDto(true, true, true, true, true)));
        Assert.That(dto.OwnerName, Is.Null);
        Assert.That(directory.Calls, Is.Zero, "the owner's own name is never looked up");
        Assert.That(dto.Status, Is.EqualTo("Flown"));
        Assert.That(dto.FlownAt, Is.EqualTo(flight.FlownAt));
    }

    [Test]
    public async Task SharedViewer_CanReadAndExport_ButNotChange_AndSeesTheOwnersName()
    {
        var owner = Guid.NewGuid();
        var flight = Flight(owner);
        var dto = await Handler(new Reader(flight, FlightViewerRole.Shared), Guid.NewGuid(), directory: new Directory { [owner] = "Bob Builder" })
            .Handle(new GetFlightDetailQuery(flight.Id), default);

        Assert.That(dto.ViewerRole, Is.EqualTo("Shared"));
        Assert.That(dto.Permissions.CanExport, Is.True);
        Assert.That((dto.Permissions.CanEdit, dto.Permissions.CanDelete, dto.Permissions.CanShare, dto.Permissions.CanMarkFlown), Is.EqualTo((false, false, false, false)));
        Assert.That(dto.OwnerName, Is.EqualTo("Bob Builder"));
    }

    [Test]
    public async Task Admin_CanRead_ButDoesNotEditSomeoneElsesFlight()
    {
        var flight = Flight(Guid.NewGuid());
        var dto = await Handler(new Reader(flight, FlightViewerRole.Admin), Guid.NewGuid(), admin: true).Handle(new GetFlightDetailQuery(flight.Id), default);

        Assert.That(dto.ViewerRole, Is.EqualTo("Admin"));
        Assert.That(dto.Permissions.CanEdit, Is.False);
        Assert.That(dto.Permissions.CanExport, Is.True);
    }

    [Test]
    public void NeitherOwnerAdminNorShared_IsForbidden_AndAMissingFlight_IsNotFound()
    {
        var flight = Flight(Guid.NewGuid());
        var me = Guid.NewGuid();

        Assert.ThrowsAsync<ForbiddenException>(() => Handler(new Reader(flight, null), me).Handle(new GetFlightDetailQuery(flight.Id), default));
        Assert.ThrowsAsync<NotFoundException>(() => Handler(new Reader(null, null), me).Handle(new GetFlightDetailQuery(Guid.NewGuid()), default));
    }

    [Test]
    public async Task ReturnsTheSingleAssessment_NamesAndTheDossierFields()
    {
        var me = Guid.NewGuid();
        var flight = Flight(me);
        flight.WindSpeedKt = 6.5m;
        var dto = await Handler(new Reader(flight, FlightViewerRole.Owner), me).Handle(new GetFlightDetailQuery(flight.Id), default);

        Assert.That(dto.Assessment, Is.EqualTo(Assessed));
        Assert.That((dto.BalloonRegistration, dto.PilotName, dto.LaunchLocationName, dto.LaunchLocationIcaoCode), Is.EqualTo(("OO-ABC", "Alice", "Weelde", "EBWE")));
        Assert.That(dto.Details.WindSpeedKt, Is.EqualTo(6.5m));
    }

    [Test]
    public async Task Images_ComeBackInOrder_WithALinkEach_AndABrokenOneIsOmittedNotFatal()
    {
        var me = Guid.NewGuid();
        var flight = Flight(me,
            new FlightPreparationImage { Id = Guid.NewGuid(), Section = FlightPreparationSection.Route, ObjectKey = "o/f/b.png", ContentType = "image/png", Order = 1 },
            new FlightPreparationImage { Id = Guid.NewGuid(), Section = FlightPreparationSection.Meteo, ObjectKey = "o/f/a.png", ContentType = "image/png", Order = 0 },
            new FlightPreparationImage { Id = Guid.NewGuid(), Section = FlightPreparationSection.Meteo, ObjectKey = "o/f/broken.png", ContentType = "image/png", Order = 2 });
        var storage = new Storage { FailFor = "o/f/broken.png" };

        var dto = await Handler(new Reader(flight, FlightViewerRole.Owner), me, storage: storage).Handle(new GetFlightDetailQuery(flight.Id), default);

        Assert.That(dto.Images.Select(i => (i.Section, i.Url)), Is.EqualTo(new[] { ("Meteo", "https://files.test/o/f/a.png"), ("Route", "https://files.test/o/f/b.png") }));
    }

    [TestCase(FlightViewerRole.Owner, true)]
    [TestCase(FlightViewerRole.Admin, false)]
    [TestCase(FlightViewerRole.Shared, false)]
    public void OnlyTheOwnerCanEdit(FlightViewerRole role, bool canEdit)
    {
        Assert.That(FlightPermissions.For(role).CanEdit, Is.EqualTo(canEdit));
    }

    private sealed class Me(Guid id, bool admin) : ICurrentUserService
    {
        public Guid UserId => id;
        public bool IsAdmin => admin;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class Reader(FlightPreparation? flight, FlightViewerRole? role) : IFlightDetailReader
    {
        public Task<FlightAccess> GetAccessAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default) =>
            Task.FromResult(new FlightAccess(flight is not null, role));

        public Task<FlightDetailResult> GetAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default)
        {
            var access = new FlightAccess(flight is not null, role);
            return Task.FromResult(new FlightDetailResult(access, flight is null || role is null
                ? null
                : new FlightDetailData(flight, role.Value, "OO-ABC", "Alice", "Weelde", "EBWE", Input)));
        }
    }

    private sealed class Assess : IFlightAssessmentService
    {
        public Task<FlightAssessmentDto> AssessAsync(FlightAssessmentInput input, CancellationToken ct = default) => Task.FromResult(Assessed);

        public Task<IReadOnlyList<FlightAssessmentDto>> AssessManyAsync(IReadOnlyList<FlightAssessmentInput> inputs, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Directory : Dictionary<Guid, string>, IUserDirectory
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(this);
        }
    }

    private sealed class Storage : IObjectStorageService
    {
        public string? FailFor { get; init; }

        public Task<Uri> GetPresignedDownloadUrlAsync(StorageBucket bucket, ObjectKey key) =>
            key.Value == FailFor ? throw new ObjectStorageException("down") : Task.FromResult(new Uri($"https://files.test/{key.Value}"));

        public Task UploadAsync(StorageBucket bucket, ObjectKey key, ObjectUpload upload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ObjectDownload?> DownloadAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
