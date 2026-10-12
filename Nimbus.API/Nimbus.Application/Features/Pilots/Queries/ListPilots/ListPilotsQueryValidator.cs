using FluentValidation;
namespace Nimbus.Application.Features.Pilots.Queries.ListPilots;

/// <summary>The query has no parameters; the validator exists to keep the slice layout (and pipeline) uniform.</summary>
public sealed class ListPilotsQueryValidator : AbstractValidator<ListPilotsQuery> {}
