using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Behaviours;
using System.Reflection;

namespace PEMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        // FluentValidation runs as a MediatR pipeline behaviour for every request.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        return services;
    }
}
