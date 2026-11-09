namespace Nimbus.Contracts.DTOs.Features.FlightAssessment;

/// <summary>Raw flight values to assess. Nothing here is an owner or user id; the user comes from the auth context.</summary>
public record FlightAssessmentRequestDto(
    decimal EnvelopeWeightKg,
    decimal BasketWeightKg,
    decimal BurnerWeightKg,
    decimal CylindersWeightKg,
    decimal PilotWeightKg,
    List<decimal> PassengerWeightsKg,
    decimal TotalLiftKg,
    decimal? WindSpeedKt,
    decimal? VisibilityKm,
    decimal? CapeJPerKg);

/// <summary>
///     The single assessment record every surface shows. <c>LiftSufficient</c> is null while no total lift is entered;
///     <c>GoNoGoVerdict</c> is "green", "yellow", "red" or "unknown".
/// </summary>
public record FlightAssessmentDto(
    decimal TotalWeightKg, decimal PassengersWeightKg, decimal MarginKg, bool? LiftSufficient, string GoNoGoVerdict);
