using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightAssessment;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Features.FlightAssessment.Queries.AssessFlight;

public sealed class AssessFlightQueryHandler : IRequestHandler<AssessFlightQuery, FlightAssessmentDto>
{
    private readonly IFlightAssessmentService _service;

    public AssessFlightQueryHandler(IFlightAssessmentService service)
    {
        _service = service;
    }

    public Task<FlightAssessmentDto> Handle(AssessFlightQuery request, CancellationToken ct)
    {
        var f = request.Flight;
        var input = new FlightAssessmentInput(
            f.EnvelopeWeightKg, f.BasketWeightKg, f.BurnerWeightKg, f.CylindersWeightKg, f.PilotWeightKg,
            f.PassengerWeightsKg, f.TotalLiftKg, f.WindSpeedKt, f.VisibilityKm, f.CapeJPerKg);
        return _service.AssessAsync(input, ct);
    }
}
