using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightPreparations.Images;
using Nimbus.Application.Features.FlightPreparations.Images.Delete;
using Nimbus.Application.Features.FlightPreparations.Images.Get;
using Nimbus.Application.Features.FlightPreparations.Images.List;
using Nimbus.Application.Features.FlightPreparations.Images.Reorder;
using Nimbus.Application.Features.FlightPreparations.Images.Upload;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Tests;

/// <summary>Issue #51: validated upload, files in the bucket and metadata in the database, access-checked serving, delete that reports failure.</summary>
public class FlightImageHandlerTests
{
    private static readonly byte[] Png = [1, 2, 3, 4, 5];
    private readonly Guid _flight = Guid.NewGuid();
    private readonly Guid _owner = Guid.NewGuid();

    private Rig NewRig(FlightViewerRole? role = FlightViewerRole.Owner, bool exists = true) => new(_owner, role, exists);

    private static UploadFlightImageCommand Upload(Guid flight, byte[]? bytes = null, string type = "image/png", FlightPreparationSection section = FlightPreparationSection.Meteo) =>
        new(flight, section, new MemoryStream(bytes ?? Png), type);

    [Test]
    public async Task Upload_StoresOriginalAndThumbnailInTheBucket_AndOnlyMetadataInTheRow()
    {
        var r = NewRig();
        var dto = await r.Upload().Handle(Upload(_flight), default);

        Assert.That(r.Storage.Uploaded.Select(u => u.Bucket), Is.All.EqualTo(StorageBucket.FlightImages));
        Assert.That(r.Storage.Uploaded.Select(u => u.Key), Is.EqualTo(new[] { $"{_owner}/{_flight}/{dto.Id}.png", $"{_owner}/{_flight}/{dto.Id}.thumb.png" }));
        Assert.That(r.Storage.Uploaded[0].Bytes, Is.EqualTo(Png));
        var row = r.Images.Added.Single();
        Assert.That((row.Section, row.ContentType, row.SizeBytes), Is.EqualTo((FlightPreparationSection.Meteo, "image/png", (long)Png.Length)));
        Assert.That(row.ObjectKey, Does.EndWith($"{dto.Id}.png"));
        Assert.That(row.ThumbnailObjectKey, Does.EndWith($"{dto.Id}.thumb.png"));
        Assert.That(dto.SizeBytes, Is.EqualTo(Png.Length));
        Assert.That(dto.ThumbnailUrl, Is.Not.EqualTo(dto.Url));
    }

    [Test]
    public async Task Upload_TrustsTheBytesNotTheClaim_StoredTypeIsTheDetectedOne()
    {
        var r = NewRig();
        r.Inspector.Result = new InspectedImage("image/jpeg", "jpg", new ImageVariant([9], "image/jpeg"));

        await r.Upload().Handle(Upload(_flight, type: "image/png"), default);

        Assert.That(r.Images.Added.Single().ContentType, Is.EqualTo("image/jpeg"));
        Assert.That(r.Storage.Uploaded[0].Key, Does.EndWith(".jpg"));
    }

    [Test]
    public void Upload_AFileThatIsNotReallyAnImage_IsRejected_NothingStored()
    {
        var r = NewRig();
        r.Inspector.Result = null;

        var ex = Assert.ThrowsAsync<ValidationException>(() => r.Upload().Handle(Upload(_flight, [0x4d, 0x5a]), default));

        Assert.That(ex!.Errors["file"], Has.One.Contain("not a valid image"));
        Assert.That(r.Storage.Uploaded, Is.Empty);
        Assert.That(r.Images.Added, Is.Empty);
    }

    [Test]
    public void Upload_ADisallowedContentType_IsRejectedBeforeTheBodyIsRead()
    {
        var r = NewRig();
        Assert.ThrowsAsync<ValidationException>(() => r.Upload().Handle(Upload(_flight, type: "application/pdf"), default));
        Assert.That(r.Inspector.Calls, Is.Zero);
    }

    [Test]
    public void Upload_ATooLargeFile_IsRejected_EvenWhenNobodyDeclaredItsLength()
    {
        var r = NewRig();
        r.Options = new ImageUploadOptions { MaxBytes = 4 };

        var ex = Assert.ThrowsAsync<ValidationException>(() => r.Upload().Handle(Upload(_flight, Png), default));

        Assert.That(ex!.Errors["file"], Has.One.Contain("limit"));
        Assert.That(r.Storage.Uploaded, Is.Empty);
    }

    [Test]
    public void Upload_AnEmptyFile_IsRejected()
    {
        var r = NewRig();
        Assert.ThrowsAsync<ValidationException>(() => r.Upload().Handle(Upload(_flight, []), default));
    }

    [TestCase(FlightViewerRole.Shared)]
    [TestCase(null)]
    public void Upload_ByAnyoneWhoMayNotManageTheFlight_IsForbidden(FlightViewerRole? role)
    {
        var r = NewRig(role);
        Assert.ThrowsAsync<ForbiddenException>(() => r.Upload().Handle(Upload(_flight), default));
        Assert.That(r.Storage.Uploaded, Is.Empty);
    }

    [Test]
    public void Upload_ToAMissingFlight_IsNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => NewRig(exists: false).Upload().Handle(Upload(_flight), default));
    }

    [Test]
    public async Task Upload_ByAnAdmin_StoresUnderTheOwnersPrefix()
    {
        var r = NewRig(FlightViewerRole.Admin);
        await r.Upload().Handle(Upload(_flight), default);
        Assert.That(r.Storage.Uploaded[0].Key, Does.StartWith($"{_owner}/{_flight}/"));
    }

    [Test]
    public void Upload_WhenTheRowCannotBeSaved_TheFilesAreRemovedAgain_AndTheErrorIsReported()
    {
        var r = NewRig();
        r.Images.FailAdd = true;

        Assert.ThrowsAsync<InvalidOperationException>(() => r.Upload().Handle(Upload(_flight), default));

        Assert.That(r.Storage.Deleted.Select(d => d.Key), Is.EquivalentTo(r.Storage.Uploaded.Select(u => u.Key)));
    }

    [Test]
    public void Upload_WhenTheThumbnailCannotBeStored_TheOriginalIsRemovedAndNoRowIsWritten()
    {
        var r = NewRig();
        r.Storage.FailUploadSuffix = ".thumb.png";

        Assert.ThrowsAsync<ObjectStorageException>(() => r.Upload().Handle(Upload(_flight), default));

        Assert.That(r.Storage.Deleted, Has.Count.EqualTo(1));
        Assert.That(r.Images.Added, Is.Empty);
    }

    [Test]
    public async Task Delete_RemovesBothFilesAndTheRow()
    {
        var r = NewRig();
        var image = r.Images.Seed(_flight);

        await r.Delete().Handle(new DeleteFlightImageCommand(_flight, image.Id), default);

        Assert.That(r.Storage.Deleted.Select(d => d.Key), Is.EqualTo(new[] { image.ObjectKey, image.ThumbnailObjectKey }));
        Assert.That(r.Images.DeletedIds, Is.EqualTo(new[] { image.Id }));
    }

    [Test]
    public void Delete_WhenAFileCannotBeRemoved_FailsLoudly_AndKeepsTheRowSoItCanBeRetried()
    {
        var r = NewRig();
        var image = r.Images.Seed(_flight);
        r.Storage.FailDeleteKey = image.ObjectKey;

        Assert.ThrowsAsync<ObjectStorageException>(() => r.Delete().Handle(new DeleteFlightImageCommand(_flight, image.Id), default));

        Assert.That(r.Images.DeletedIds, Is.Empty);
    }

    [Test]
    public void Delete_WhenTheRowCannotBeRemoved_FailsLoudly()
    {
        var r = NewRig();
        var image = r.Images.Seed(_flight);
        r.Images.FailDelete = true;

        Assert.ThrowsAsync<InvalidOperationException>(() => r.Delete().Handle(new DeleteFlightImageCommand(_flight, image.Id), default));
    }

    [Test]
    public void Delete_ByASharedViewer_IsForbidden_AndTouchesNothing()
    {
        var r = NewRig(FlightViewerRole.Shared);
        var image = r.Images.Seed(_flight);

        Assert.ThrowsAsync<ForbiddenException>(() => r.Delete().Handle(new DeleteFlightImageCommand(_flight, image.Id), default));

        Assert.That(r.Storage.Deleted, Is.Empty);
        Assert.That(r.Images.DeletedIds, Is.Empty);
    }

    [Test]
    public void Delete_OfAnImageThatIsNotThere_IsNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => NewRig().Delete().Handle(new DeleteFlightImageCommand(_flight, Guid.NewGuid()), default));
    }

    [TestCase(false, ".png")]
    [TestCase(true, ".thumb.png")]
    public async Task Get_ServesTheOriginalOrTheThumbnail_ToASharedViewerToo(bool thumbnail, string suffix)
    {
        var r = NewRig(FlightViewerRole.Shared);
        var image = r.Images.Seed(_flight);

        await using var download = await r.Get().Handle(new GetFlightImageQuery(_flight, image.Id, thumbnail), default);

        Assert.That(r.Storage.Downloaded.Single(), Does.EndWith(suffix));
    }

    [Test]
    public void Get_WithoutAccessToTheFlight_IsForbidden()
    {
        var r = NewRig(role: null);
        var image = r.Images.Seed(_flight);
        Assert.ThrowsAsync<ForbiddenException>(() => r.Get().Handle(new GetFlightImageQuery(_flight, image.Id, false), default));
    }

    [Test]
    public void Get_OfAnImageOfAnotherFlight_IsNotFound()
    {
        var r = NewRig();
        var image = r.Images.Seed(Guid.NewGuid());
        Assert.ThrowsAsync<NotFoundException>(() => r.Get().Handle(new GetFlightImageQuery(_flight, image.Id, false), default));
    }

    [Test]
    public async Task Get_AnImageThatPredatesThumbnails_FallsBackToTheOriginal()
    {
        var r = NewRig();
        var image = r.Images.Seed(_flight);
        image.ThumbnailObjectKey = null;

        await using var _ = await r.Get().Handle(new GetFlightImageQuery(_flight, image.Id, true), default);

        Assert.That(r.Storage.Downloaded.Single(), Is.EqualTo(image.ObjectKey));
    }

    [Test]
    public async Task List_ReturnsTheFlightsImagesWithLinks_ToASharedViewerToo()
    {
        var r = NewRig(FlightViewerRole.Shared);
        var a = r.Images.Seed(_flight);
        r.Images.Seed(Guid.NewGuid());

        var list = await r.List().Handle(new ListFlightImagesQuery(_flight), default);

        Assert.That(list.Select(i => i.Id), Is.EqualTo(new[] { a.Id }));
        Assert.That(list[0].ThumbnailUrl, Does.Contain("thumb"));
    }

    [Test]
    public void List_WithoutAccess_IsForbidden()
    {
        Assert.ThrowsAsync<ForbiddenException>(() => NewRig(role: null).List().Handle(new ListFlightImagesQuery(_flight), default));
    }

    [Test]
    public async Task Reorder_SetsTheNewOrder_WhenTheListNamesEveryImageOfTheSectionOnce()
    {
        var r = NewRig();
        var (a, b, c) = (r.Images.Seed(_flight), r.Images.Seed(_flight), r.Images.Seed(_flight));
        var other = r.Images.Seed(_flight);
        other.Section = FlightPreparationSection.Route;

        await r.Reorder().Handle(new ReorderFlightImagesCommand(_flight, FlightPreparationSection.Meteo, [c.Id, a.Id, b.Id]), default);

        Assert.That(r.Images.NewOrder, Is.EqualTo(new[] { c.Id, a.Id, b.Id }));
    }

    [Test]
    public void Reorder_ARequestThatMissesDuplicatesOrAddsAnImage_IsRejected_NothingChanges()
    {
        var r = NewRig();
        var (a, b) = (r.Images.Seed(_flight), r.Images.Seed(_flight));

        Assert.ThrowsAsync<ValidationException>(() => r.Reorder().Handle(new ReorderFlightImagesCommand(_flight, FlightPreparationSection.Meteo, [a.Id]), default));
        Assert.ThrowsAsync<ValidationException>(() => r.Reorder().Handle(new ReorderFlightImagesCommand(_flight, FlightPreparationSection.Meteo, [a.Id, a.Id]), default));
        Assert.ThrowsAsync<ValidationException>(() => r.Reorder().Handle(new ReorderFlightImagesCommand(_flight, FlightPreparationSection.Meteo, [a.Id, b.Id, Guid.NewGuid()]), default));
        Assert.That(r.Images.NewOrder, Is.Empty);
    }

    [Test]
    public void Reorder_ByASharedViewer_IsForbidden()
    {
        var r = NewRig(FlightViewerRole.Shared);
        Assert.ThrowsAsync<ForbiddenException>(() => r.Reorder().Handle(new ReorderFlightImagesCommand(_flight, FlightPreparationSection.Meteo, []), default));
    }

    private sealed class Rig(Guid owner, FlightViewerRole? role, bool exists)
    {
        public ImageUploadOptions Options { get; set; } = new();
        public Access AccessFake { get; } = new(exists ? new FlightAccess(true, role, owner) : new FlightAccess(false, null));
        public ImagesFake Images { get; } = new();
        public StorageFake Storage { get; } = new();
        public InspectorFake Inspector { get; } = new();
        private Me User => new(Guid.NewGuid(), role == FlightViewerRole.Admin);

        public UploadFlightImageCommandHandler Upload() =>
            new(AccessFake, Images, Storage, Inspector, User, Microsoft.Extensions.Options.Options.Create(Options), NullLogger<UploadFlightImageCommandHandler>.Instance);

        public DeleteFlightImageCommandHandler Delete() => new(AccessFake, Images, Storage, User, NullLogger<DeleteFlightImageCommandHandler>.Instance);

        public ListFlightImagesQueryHandler List() => new(AccessFake, Images, Storage, User, NullLogger<ListFlightImagesQueryHandler>.Instance);

        public ReorderFlightImagesCommandHandler Reorder() => new(AccessFake, Images, User, NullLogger<ReorderFlightImagesCommandHandler>.Instance);

        public GetFlightImageQueryHandler Get() => new(AccessFake, Images, Storage, User);
    }

    private sealed class Me(Guid id, bool admin) : ICurrentUserService
    {
        public Guid UserId => id;
        public bool IsAdmin => admin;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class Access(FlightAccess access) : IFlightDetailReader
    {
        public Task<FlightAccess> GetAccessAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default) => Task.FromResult(access);
        public Task<FlightDetailResult> GetAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class InspectorFake : IImageInspector
    {
        public int Calls { get; private set; }
        public InspectedImage? Result { get; set; } = new("image/png", "png", new ImageVariant([7, 7], "image/png"));

        public Task<InspectedImage?> InspectAsync(byte[] content, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class ImagesFake : IFlightImageRepository
    {
        private readonly List<FlightPreparationImage> _rows = [];
        public List<FlightPreparationImage> Added { get; } = [];
        public List<Guid> DeletedIds { get; } = [];
        public bool FailAdd { get; set; }
        public bool FailDelete { get; set; }

        public FlightPreparationImage Seed(Guid flight)
        {
            var id = Guid.NewGuid();
            var image = new FlightPreparationImage
            {
                Id = id, FlightPreparationId = flight, Section = FlightPreparationSection.Meteo, ObjectKey = $"o/f/{id}.png", ThumbnailObjectKey = $"o/f/{id}.thumb.png", ContentType = "image/png"
            };
            _rows.Add(image);
            return image;
        }

        public Task AddAsync(FlightPreparationImage image, CancellationToken ct = default)
        {
            if (FailAdd) throw new InvalidOperationException("db down");
            Added.Add(image);
            return Task.CompletedTask;
        }

        public Task<FlightPreparationImage?> GetAsync(Guid flightId, Guid imageId, CancellationToken ct = default) =>
            Task.FromResult(_rows.FirstOrDefault(i => i.Id == imageId && i.FlightPreparationId == flightId));

        public List<Guid> NewOrder { get; private set; } = [];

        public Task<IReadOnlyList<FlightPreparationImage>> ListAsync(Guid flightId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FlightPreparationImage>>(_rows.Where(i => i.FlightPreparationId == flightId).ToList());

        public Task SetOrderAsync(Guid flightId, IReadOnlyList<Guid> orderedImageIds, CancellationToken ct = default)
        {
            NewOrder = [.. orderedImageIds];
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid flightId, Guid imageId, CancellationToken ct = default)
        {
            if (FailDelete) throw new InvalidOperationException("db down");
            DeletedIds.Add(imageId);
            return Task.FromResult(true);
        }
    }

    private sealed class StorageFake : IObjectStorageService
    {
        public List<(StorageBucket Bucket, string Key, byte[] Bytes)> Uploaded { get; } = [];
        public List<(StorageBucket Bucket, string Key)> Deleted { get; } = [];
        public List<string> Downloaded { get; } = [];
        public string? FailUploadSuffix { get; set; }
        public string? FailDeleteKey { get; set; }

        public Task UploadAsync(StorageBucket bucket, ObjectKey key, ObjectUpload upload, CancellationToken cancellationToken = default)
        {
            if (FailUploadSuffix is { } s && key.Value.EndsWith(s)) throw new ObjectStorageException("down");
            var ms = new MemoryStream();
            upload.Content.CopyTo(ms);
            Uploaded.Add((bucket, key.Value, ms.ToArray()));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default)
        {
            if (key.Value == FailDeleteKey) throw new ObjectStorageException("down");
            Deleted.Add((bucket, key.Value));
            return Task.CompletedTask;
        }

        public Task<ObjectDownload?> DownloadAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default)
        {
            Downloaded.Add(key.Value);
            return Task.FromResult<ObjectDownload?>(new ObjectDownload(new MemoryStream(), "image/png", 0));
        }

        public Task<bool> ExistsAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Uri> GetPresignedDownloadUrlAsync(StorageBucket bucket, ObjectKey key) => Task.FromResult(new Uri($"https://store.test/{key.Value}?sig=x"));
    }
}
