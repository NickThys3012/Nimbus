using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.API.Identity;

public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "PasswordReset";

    public PasswordResetTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = TimeSpan.FromHours(1);
    }
}

/// <summary>
///     Dedicated provider so reset links expire after an hour without shortening the (day-long)
///     email-confirmation links. Tokens embed the security stamp, which a successful reset rotates,
///     so each token works exactly once.
/// </summary>
public sealed class PasswordResetTokenProvider : DataProtectorTokenProvider<ApplicationUser>
{
    public PasswordResetTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PasswordResetTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}

public static class PasswordResetTokenExtensions
{
    /// <summary>
    ///     Registered here rather than in Infrastructure because the token-provider base class
    ///     comes from the ASP.NET Core shared framework, which Infrastructure does not reference.
    /// </summary>
    public static IServiceCollection AddPasswordResetTokens(this IServiceCollection services)
    {
        services.AddTransient<PasswordResetTokenProvider>();
        services.Configure<IdentityOptions>(o =>
        {
            o.Tokens.PasswordResetTokenProvider = PasswordResetTokenProviderOptions.ProviderName;
            o.Tokens.ProviderMap[PasswordResetTokenProviderOptions.ProviderName] =
                new TokenProviderDescriptor(typeof(PasswordResetTokenProvider));
        });
        return services;
    }
}
