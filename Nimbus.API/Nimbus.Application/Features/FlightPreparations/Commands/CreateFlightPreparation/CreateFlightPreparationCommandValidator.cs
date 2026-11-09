using FluentValidation;
namespace Nimbus.Application.Features.FlightPreparations.Commands.CreateFlightPreparation;

public sealed class CreateFlightPreparationCommandValidator : AbstractValidator<CreateFlightPreparationCommand>
{
    public CreateFlightPreparationCommandValidator()
    {
        RuleFor(c => c.Flight).NotNull().SetValidator(new FlightPreparationRequestValidator());
    }
}
