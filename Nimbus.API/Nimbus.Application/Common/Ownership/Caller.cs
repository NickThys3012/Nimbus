namespace Nimbus.Application.Common.Ownership;

/// <summary>The authenticated principal an owned-entity operation runs on behalf of.</summary>
public readonly record struct Caller(Guid UserId, bool IsAdmin);
