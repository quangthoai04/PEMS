using Application.Authentication.Services;
using Application.CampusManagement.Services;
using Microsoft.Extensions.DependencyInjection;

namespace PEMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICampusService, CampusService>();
        
        return services;
    }
}
