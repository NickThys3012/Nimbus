using FluentValidation;
namespace Nimbus.Application.Features.FlightPreparations.Commands.UpdateFlightPreparation;

public sealed class UpdateFlightPreparationCommandValidator : AbstractValidator<UpdateFlightPreparationCommand>
{
    public UpdateFlightPreparationCommandValidator()
    {
        RuleFor(c => c.Flight).NotNull().SetValidator(new FlightPreparationRequestValidator());
    }
}
