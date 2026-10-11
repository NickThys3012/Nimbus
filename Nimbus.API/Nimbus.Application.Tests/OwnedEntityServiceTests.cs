using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Application.Common.Ownership;
using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

public class OwnedEntityServiceTests
{
    [Test]
    public Task SampleService_SatisfiesOwnerScopingContract()
    {
        return OwnedServiceContract.AssertHoldsAsync(
            repo => new NoteService(repo, NullLogger.Instance),
            () => new Note { Id = Guid.NewGuid(), Title = "a" },
            note => note.Title = "changed");
    }

    private sealed class Note : OwnedEntity
    {
        public string Title { get; set; } = "";
    }

    private sealed class NoteService(IOwnedRepository<Note> repository, ILogger logger)
        : OwnedEntityService<Note>(repository, logger)
    {
        protected override void CopyValues(Note target, Note source)
        {
            target.Title = source.Title;
            target.OwnerId = source.OwnerId; // deliberately naive bulk copy; the base must undo it
        }
    }
}
