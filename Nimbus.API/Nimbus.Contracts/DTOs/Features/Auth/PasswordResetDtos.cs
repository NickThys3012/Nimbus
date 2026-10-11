namespace Nimbus.Contracts.DTOs.Features.Auth;

public record ForgotPasswordRequestDto(string Email);

public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);
