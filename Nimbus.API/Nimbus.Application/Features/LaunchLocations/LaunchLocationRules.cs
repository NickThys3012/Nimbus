using FluentValidation;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
namespace Nimbus.Application.Features.LaunchLocations;

/// <summary>Field rules shared by create and update.</summary>
public sealed class LaunchLocationRequestValidator : AbstractValidator<LaunchLocationRequestDto>
{
    public const decimal MinElevationM = -500m;
    public const decimal MaxElevationM = 9000m;
    public const int MaxNotesLength = 2000;

    public LaunchLocationRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().Must(n => n is null || n.Trim().Length > 0).WithMessage("'Name' must not be empty.");
        RuleFor(r => r.Name).MaximumLength(100);

        // Four letters or digits (ICAO ident), optional; case does not matter, it is stored upper case.
        RuleFor(r => r.IcaoCode)
            .Must(c => c!.Trim().Length == 4 && c.Trim().All(char.IsAsciiLetterOrDigit))
            .When(r => !string.IsNullOrWhiteSpace(r.IcaoCode))
            .WithMessage("'Icao Code' must be exactly four letters or digits.");

        // Columns are decimal(9,6) / decimal(7,2): anything finer would be rounded by the database but echoed unrounded.
        RuleFor(r => r.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(r => r.Latitude).Must(v => decimal.Round(v, 6) == v).WithMessage("'Latitude' must have at most 6 decimal places.");
        RuleFor(r => r.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(r => r.Longitude).Must(v => decimal.Round(v, 6) == v).WithMessage("'Longitude' must have at most 6 decimal places.");
        RuleFor(r => r.ElevationM).InclusiveBetween(MinElevationM, MaxElevationM);
        RuleFor(r => r.ElevationM).Must(v => decimal.Round(v, 2) == v).WithMessage("'Elevation M' must have at most 2 decimal places.");

        RuleFor(r => r.AirspaceNotes).MaximumLength(MaxNotesLength);
    }
}
