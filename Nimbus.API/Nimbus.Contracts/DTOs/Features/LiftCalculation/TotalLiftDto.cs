namespace Nimbus.Contracts.DTOs.Features.LiftCalculation;

/// <summary>Total lift in kg for the supplied conditions. Derived on demand, never stored.</summary>
public record TotalLiftDto(decimal TotalLiftKg);
