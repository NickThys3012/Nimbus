using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightAssessment;
namespace Nimbus.Application.Features.FlightAssessment.Queries.AssessFlight;

/// <summary>Assesses the supplied flight values against the signed-in user's Go/No-Go limits. Stores nothing.</summary>
public sealed record AssessFlightQuery(FlightAssessmentRequestDto Flight) : IRequest<FlightAssessmentDto>;
