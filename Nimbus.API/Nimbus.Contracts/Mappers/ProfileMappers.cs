using Nimbus.Contracts.DTOs.Features.Profile;
using Nimbus.Domain.Entities;
namespace Nimbus.Contracts.Mappers;

public static class ProfileMappers
{
    public static ProfileDto MapToDto(this UserProfile p) => new(p.OperatorName, p.PicWeightKg);

    public static void ApplyTo(this ProfileRequestDto r, UserProfile target)
    {
        target.OperatorName = string.IsNullOrWhiteSpace(r.OperatorName) ? null : r.OperatorName.Trim();
        target.PicWeightKg = r.PicWeightKg;
    }
}
