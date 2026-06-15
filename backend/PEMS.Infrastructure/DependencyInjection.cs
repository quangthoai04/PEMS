using Application.Common.Interfaces;
using PEMS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Infrastructure.ExternalServices.FaceRecognition;
using PEMS.Infrastructure.ExternalServices.Ocr;
using PEMS.Infrastructure.Identity;
using PEMS.Infrastructure.Persistence.Repositories;

namespace PEMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICampusRepository, CampusRepository>();
        
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        services.AddScoped<IFaceRecognitionService, FaceRecognitionService>();
        services.AddScoped<IOcrService, OcrService>();

        return services;
    }
}
