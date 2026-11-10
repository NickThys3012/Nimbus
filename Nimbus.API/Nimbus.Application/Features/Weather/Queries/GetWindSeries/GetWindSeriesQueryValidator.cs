using FluentValidation;
namespace Nimbus.Application.Features.Weather.Queries.GetWindSeries;

public sealed class GetWindSeriesQueryValidator : AbstractValidator<GetWindSeriesQuery>
{
    /// <summary>The longest window that is looked at: no model offers more than 96 h, and a longer one only costs a bigger reply.</summary>
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(7);

    public GetWindSeriesQueryValidator()
    {
        RuleFor(q => q.Latitude).InclusiveBetween(-90, 90);
        RuleFor(q => q.Longitude).InclusiveBetween(-180, 180);
        RuleFor(q => q.ToUtc).GreaterThanOrEqualTo(q => q.FromUtc).WithMessage("'ToUtc' must not be before 'FromUtc'.");
        RuleFor(q => q).Must(q => q.ToUtc - q.FromUtc <= MaxWindow).WithName("ToUtc").WithMessage("The window must not be longer than 7 days.");
        RuleFor(q => q.Model)
            .Must(m => string.IsNullOrWhiteSpace(m) || WindProfileMapper.SupportedModels.Contains(m.Trim()))
            .WithMessage($"'Model' must be one of: {string.Join(", ", WindProfileMapper.SupportedModels)}. Other models lack pressure levels or low-altitude winds.");
    }
}
