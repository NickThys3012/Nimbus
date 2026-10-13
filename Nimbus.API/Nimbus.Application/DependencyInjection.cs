using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Application.Common.Behaviours;
using Nimbus.Application.Features.Balloons;
using Nimbus.Application.Features.LaunchLocations;
using Nimbus.Application.Features.Pilots;
using Nimbus.Application.Helpers;
namespace Nimbus.Application;

public static class DependencyInjection
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Register validators from Contracts assembly (shared with Web)
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<EmailHelpers>();
        services.AddScoped<IBalloonService, BalloonService>();
        services.AddScoped<IPilotService, PilotService>();
        services.AddScoped<ILaunchLocationService, LaunchLocationService>();
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehaviour<,>));
    }
}
