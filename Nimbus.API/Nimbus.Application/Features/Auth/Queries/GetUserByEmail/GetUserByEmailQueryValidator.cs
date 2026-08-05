using FluentValidation;
namespace Nimbus.Application.Features.Auth.Queries.GetUserByEmail;

public class GetUserByEmailQueryValidator : AbstractValidator<GetUserByEmailQuery>
{
    public GetUserByEmailQueryValidator()
    {
        RuleFor(v => v.Email).NotEmpty().EmailAddress();
    }
}
