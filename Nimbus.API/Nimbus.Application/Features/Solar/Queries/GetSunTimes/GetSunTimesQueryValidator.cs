using FluentValidation;
namespace Nimbus.Application.Features.Solar.Queries.GetSunTimes;

public sealed class GetSunTimesQueryValidator : AbstractValidator<GetSunTimesQuery>
{
    public GetSunTimesQueryValidator()
    {
        RuleFor(q => q.Latitude).InclusiveBetween(-90, 90);
        RuleFor(q => q.Longitude).InclusiveBetween(-180, 180);
        RuleFor(q => q.Date).Must(d => d.Year is >= 1900 and <= 2200).WithMessage("'Date' must be between the years 1900 and 2200.");
        RuleFor(q => q.TimeZone)
            .Must(z => string.IsNullOrWhiteSpace(z) || TimeZoneInfo.TryFindSystemTimeZoneById(z.Trim(), out _))
            .WithMessage("'TimeZone' must be a known IANA time zone id such as Europe/Brussels.");
    }
}
