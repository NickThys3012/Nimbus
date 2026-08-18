
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
            services.AddScoped<SmtpEmailSender>();
            services.AddScoped<IEmailSender>(sp => new AuditingEmailSender(
                sp.GetRequiredService<SmtpEmailSender>(),
                sp.GetRequiredService<IEmailAuditLogger>()));
        }
        else
        {
            services.AddScoped<NullEmailSender>();
            services.AddScoped<IEmailSender>(sp => new AuditingEmailSender(
                sp.GetRequiredService<NullEmailSender>(),
                sp.GetRequiredService<IEmailAuditLogger>()));
        }
    }
}
