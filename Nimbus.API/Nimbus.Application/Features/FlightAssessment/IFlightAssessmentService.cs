using Nimbus.Contracts.DTOs.Features.FlightAssessment;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Features.FlightAssessment;

/// <summary>
///     The one entry point for a flight's assessment (total weight, lift sufficiency, Go/No-Go verdict).
///     The edit form, detail page, flight list and PDF exporter must all go through it and never recompute.
///     The Go/No-Go limits are always those of the signed-in user.
/// </summary>
public interface IFlightAssessmentService
{
    Task<FlightAssessmentDto> AssessAsync(FlightAssessmentInput input, CancellationToken ct = default);

    /// <summary>Assesses many flights (the list) resolving the user's limits once; result order matches the input.</summary>
    Task<IReadOnlyList<FlightAssessmentDto>> AssessManyAsync(IReadOnlyList<FlightAssessmentInput> inputs, CancellationToken ct = default);
}
