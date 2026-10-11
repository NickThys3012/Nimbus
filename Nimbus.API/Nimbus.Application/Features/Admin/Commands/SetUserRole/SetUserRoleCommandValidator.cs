using FluentValidation;
using Nimbus.Domain.Enums;
namespace Nimbus.Application.Features.Admin.Commands.SetUserRole;

public sealed class SetUserRoleCommandValidator : AbstractValidator<SetUserRoleCommand>
{
    public SetUserRoleCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        // Only the roles that exist (seeded from UserRole in IdentitySeed).
        RuleFor(c => c.Role)
            .NotEmpty()
            .Must(role => Enum.GetNames<UserRole>().Contains(role, StringComparer.Ordinal))
            .WithMessage($"Role must be one of: {string.Join(", ", Enum.GetNames<UserRole>())}.");
    }
}
