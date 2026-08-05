using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Application.Common.Behaviours;
using Nimbus.Contracts;
namespace Nimbus.Application;

public static class DependencyInjection
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Register validators from Contracts assembly (shared with Web)
        services.AddValidatorsFromAssembly(typeof(ContractsMarker).Assembly);

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehaviour<,>));
    }
}
