using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.ForgotPassword;

/// <summary>
///     Starts a password reset. Behaves identically whether or not the address belongs to an
///     account: no exception, no different result, and a floor on the elapsed time so the
///     extra work done for a real account (token generation, enqueue) is not measurable.
/// </summary>
public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    internal static readonly TimeSpan MinimumDuration = TimeSpan.FromMilliseconds(500);
    private readonly IIdentityService _identityService;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(IIdentityService identityService, ILogger<ForgotPasswordCommandHandler> logger)
    {
        _identityService = identityService;
        _logger = logger;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        try
        {
            await _identityService.RequestPasswordResetAsync(request.Email, request.WebBaseUrl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallowed on purpose: a failure must not differ from success for the caller.
            _logger.LogError(ex, "Password reset request failed.");
        }

        var remaining = MinimumDuration - started.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, ct);
        }
    }
}
