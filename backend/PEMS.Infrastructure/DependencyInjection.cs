using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Interfaces;
using PEMS.Infrastructure.Common;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.ExternalServices.FaceRecognition;
using PEMS.Infrastructure.ExternalServices.Ocr;
using PEMS.Infrastructure.Identity;
using PEMS.Infrastructure.Logging;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Persistence.Repositories;

namespace PEMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Expose the EF Core context through the Application abstraction.
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICampusRepository, CampusRepository>();

        // Identity / auth services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<IOwnershipChecker, OwnershipChecker>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();

        // Cross-cutting
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddHttpContextAccessor();
        services.AddHttpClient();

        // External services (scaffolded)
        services.AddScoped<IFaceRecognitionService, FaceRecognitionService>();
        services.AddScoped<IOcrService, OcrService>();

        return services;
    }
}
