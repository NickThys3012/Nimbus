namespace Nimbus.Contracts.DTOs.Features.Auth;

public record LoginResponseDto(string Email, string Role, bool IsApproved, bool IsRejected, DateTime? RequestedAt);
