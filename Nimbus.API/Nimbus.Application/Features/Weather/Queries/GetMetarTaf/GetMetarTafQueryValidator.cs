using FluentValidation;
namespace Nimbus.Application.Features.Weather.Queries.GetMetarTaf;

public sealed class GetMetarTafQueryValidator : AbstractValidator<GetMetarTafQuery>
{
    public GetMetarTafQueryValidator()
    {
        // An ICAO station identifier: four letters or digits. Anything else never reaches the provider.
        RuleFor(q => q.IcaoCode)
            .NotEmpty().WithMessage("An ICAO code is needed to fetch the weather.")
            .Must(c => c is null || (c.Trim().Length == 4 && c.Trim().All(char.IsAsciiLetterOrDigit)))
            .WithMessage("'Icao Code' must be exactly four letters or digits.");
    }
}
