using FluentValidation;
namespace Nimbus.Application.Features.Admin.Queries.ListUsers;

/// <summary>The query has no parameters; the validator exists to keep the slice layout (and pipeline) uniform.</summary>
public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery> {}
