
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Mailing;


public static class EmailServiceCollectionExtensions
{
    public static void AddNimbusEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(EmailOptions.SectionName);
        var enabled = section.GetValue<bool>(nameof(EmailOptions.Enabled));

        var builder = services
            .AddOptions<EmailOptions>()
            .Bind(section);

        // Only demand valid SMTP settings when sending is actually on, so a
        // developer without credentials can still boot the API.
        if (enabled)
        {
            builder.ValidateDataAnnotations().ValidateOnStart();
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, NullEmailSender>();
        }
    }
}
