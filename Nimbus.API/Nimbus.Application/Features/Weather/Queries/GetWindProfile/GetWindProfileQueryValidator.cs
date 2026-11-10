using FluentValidation;
namespace Nimbus.Application.Features.Weather.Queries.GetWindProfile;

public sealed class GetWindProfileQueryValidator : AbstractValidator<GetWindProfileQuery>
{
    public GetWindProfileQueryValidator()
    {
        RuleFor(q => q.Latitude).InclusiveBetween(-90, 90);
        RuleFor(q => q.Longitude).InclusiveBetween(-180, 180);
        RuleFor(q => q.Model)
            .Must(m => string.IsNullOrWhiteSpace(m) || WindProfileMapper.SupportedModels.Contains(m.Trim()))
            .WithMessage($"'Model' must be one of: {string.Join(", ", WindProfileMapper.SupportedModels)}. Other models lack pressure levels or low-altitude winds.");
    }
}
