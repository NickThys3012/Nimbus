using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Services;

/// <summary>
///     Everything the assessment needs, as plain values: lets the unsaved edit form and persisted flights
///     (list, detail, PDF) go through exactly the same calculation.
/// </summary>
public sealed record FlightAssessmentInput(
    decimal EnvelopeWeightKg,
    decimal BasketWeightKg,
    decimal BurnerWeightKg,
    decimal CylindersWeightKg,
    decimal PilotWeightKg,
    IReadOnlyList<decimal> PassengerWeightsKg,
    decimal TotalLiftKg,
    decimal? WindSpeedKt,
    decimal? VisibilityKm,
    decimal? CapeJPerKg)
{
    /// <summary>
    ///     Maps a persisted flight. Basket, burner and cylinder weights are not stored on the flight (they are
    ///     snapshotted from the balloon in the form), so the caller supplies them.
    /// </summary>
    public static FlightAssessmentInput From(
        FlightPreparation flight, decimal pilotWeightKg, decimal basketWeightKg = 0, decimal burnerWeightKg = 0, decimal cylindersWeightKg = 0)
    {
        ArgumentNullException.ThrowIfNull(flight);
        return new FlightAssessmentInput(
            flight.EnvelopeWeightKg ?? 0, basketWeightKg, burnerWeightKg, cylindersWeightKg, pilotWeightKg,
            flight.Passengers.OrderBy(p => p.Order).Select(p => p.WeightKg).ToList(),
            flight.TotalLiftKg ?? 0, flight.WindSpeedKt, flight.VisibilityKm, flight.CapeJPerKg);
    }
}

/// <summary>
///     The single assessment record of a flight. <see cref="LiftSufficient" /> is null while no total lift is
///     entered, so surfaces prompt instead of claiming "overloaded". Derived on demand, never stored.
/// </summary>
public sealed record FlightAssessmentResult(
    decimal TotalWeightKg, decimal PassengersWeightKg, decimal MarginKg, bool? LiftSufficient, GoNoGoVerdict Verdict);

/// <summary>
///     The only place total weight, lift sufficiency and the Go/No-Go verdict of a flight are computed (issue #39).
///     Pure and stateless; the user's limits are supplied by the caller.
/// </summary>
public static class FlightAssessor
{
    public static FlightAssessmentResult Assess(FlightAssessmentInput input, GoNoGoLimits limits)
    {
        ArgumentNullException.ThrowIfNull(input);
        var passengers = input.PassengerWeightsKg.Sum();
        var total = input.EnvelopeWeightKg + input.BasketWeightKg + input.BurnerWeightKg + input.CylindersWeightKg + input.PilotWeightKg + passengers;
        var margin = input.TotalLiftKg - total;
        bool? sufficient = input.TotalLiftKg > 0 ? margin > 0 : null;
        var verdict = GoNoGoEvaluator.Evaluate(input.WindSpeedKt, input.VisibilityKm, input.CapeJPerKg, limits);
        return new FlightAssessmentResult(total, passengers, margin, sufficient, verdict);
    }
}
