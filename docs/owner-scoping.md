# Owner scoping

Every entity that belongs to a single user (a pilot's flights, aircraft, documents, …) follows one
shared convention, enforced **in the service layer**. No user can read or change another user's
data by guessing an id, regardless of what the UI or controller filtered. Introduced in #23.

## Building blocks

| Piece | Project | Purpose |
| --- | --- | --- |
| `IOwnedEntity` / `OwnedEntity` | `Nimbus.Domain/Entities/Base` | Marks an entity as owned; exposes `OwnerId`. |
| `IOwnedRepository<T>` | `Nimbus.Domain/Interfaces` | Owner-agnostic persistence (get, list, add, update, remove). |
| `Caller` | `Nimbus.Application/Common/Ownership` | `(UserId, IsAdmin)` of the authenticated principal. |
| `OwnedEntityService<T>` | `Nimbus.Application/Common/Ownership` | Base service that enforces the rules below. |
| `OwnedResult` | `Nimbus.Application/Common/Ownership` | `Success` or `NotFound`. |
| `OwnedServiceContract` | `Nimbus.Application.Tests` | Reusable test helper asserting the rules. |

## The rules

1. **Convention.** Owned entities derive from `OwnedEntity` (or implement `IOwnedEntity`) and use
   `OwnerId` as the owner column.
2. **Every path takes a `Caller`.** `GetAsync`, `ListAsync`, `CreateAsync`, `UpdateAsync` and
   `DeleteAsync` all receive the caller's user id and admin flag and re-check ownership.
   Admins may access every entity; everyone else only their own.
3. **Create stamps the owner.** `OwnerId` is set from `Caller.UserId`; any owner on the incoming
   entity is overwritten.
4. **Update preserves the owner.** The service captures the existing `OwnerId`, runs `CopyValues`,
   then restores it, so a bulk copy from an incoming model cannot reassign the entity. An admin
   updating someone else's entity does not take ownership.
5. **Unauthorised = warning + no-op.** Reading a foreign entity returns `null`; update/delete
   return `OwnedResult.NotFound`. A warning is logged (user, operation, entity type and id).
   Nothing throws. A missing and a foreign entity are indistinguishable to the caller, so
   controllers should answer **404 for both** and never reveal that an id exists.
6. **List is scoped.** Non-admins only receive their own entities.

## Adding a new owned entity

```csharp
// Domain
public class Flight : OwnedEntity
{
    public string Callsign { get; set; } = "";
}

// Application
public sealed class FlightService(IOwnedRepository<Flight> repository, ILogger<FlightService> logger)
    : OwnedEntityService<Flight>(repository, logger)
{
    protected override void CopyValues(Flight target, Flight source)
    {
        target.Callsign = source.Callsign;
        // Do not worry about OwnerId: the base class restores it after this call.
    }
}
```

Then implement `IOwnedRepository<Flight>` in Infrastructure and register both in DI.
In a handler or controller, build the caller from the authenticated principal:
`new Caller(currentUser.UserId, isAdmin)`.

### Covering it with tests

```csharp
[Test]
public Task FlightService_SatisfiesOwnerScopingContract() =>
    OwnedServiceContract.AssertHoldsAsync(
        repo => new FlightService(repo, NullLogger<FlightService>.Instance),
        () => new Flight { Id = Guid.NewGuid(), Callsign = "OO-ABC" },
        flight => flight.Callsign = "OO-XYZ");
```

The helper checks: create stamps the caller and ignores a supplied owner; strangers cannot read,
list, update or delete; the owner and an admin can; update preserves the owner (including when the
incoming model carries another one); missing ids no-op.

## Limitations

- `ListAsync` filters in memory after `IOwnedRepository.ListAsync`. For large tables, add an
  owner-filtered query to the repository when the first real entity needs it, but keep the
  service-layer check as the authority.
- The helper does not assert that the warning is logged.
- No entity uses this yet; it is the foundation for the upcoming owned entities.
