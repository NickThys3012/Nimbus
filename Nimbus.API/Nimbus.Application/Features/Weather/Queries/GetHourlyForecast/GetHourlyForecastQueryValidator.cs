using FluentValidation;
namespace Nimbus.Application.Features.Weather.Queries.GetHourlyForecast;

public sealed class GetHourlyForecastQueryValidator : AbstractValidator<GetHourlyForecastQuery>
{
    public GetHourlyForecastQueryValidator()
    {
        RuleFor(q => q.Latitude).InclusiveBetween(-90, 90);
        RuleFor(q => q.Longitude).InclusiveBetween(-180, 180);
        RuleFor(q => q.Model)
            .Must(m => string.IsNullOrWhiteSpace(m) || HourlyForecastModels.Supported.Contains(m.Trim()))
            .WithMessage($"'Model' must be one of: {string.Join(", ", HourlyForecastModels.Supported)}.");
    }
}
