using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Infrastructure.Common;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.ExternalServices.FaceRecognition;
using PEMS.Infrastructure.ExternalServices.Ocr;
using PEMS.Infrastructure.FileStorage;
using PEMS.Infrastructure.Identity;
using PEMS.Infrastructure.Logging;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Persistence.Repositories;
using PEMS.Infrastructure.Security;
using PEMS.Infrastructure.Services;

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
        services.AddScoped<PEMS.Application.Common.Security.IRoleAccessPolicy, PEMS.Application.Common.Security.RoleAccessPolicy>();
        services.AddScoped<IOwnershipChecker, OwnershipChecker>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IFeidIdentityVerifier, FeidIdentityVerifier>();

        // Cross-cutting
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailActionTokenService, EmailActionTokenService>();
        services.AddHttpContextAccessor();
        services.AddHttpClient();

        // Content / upload security (defence in depth)
        services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();
        services.AddSingleton<IFileValidationService, FileValidationService>();

        // File storage (uploads / email attachments / inline images) — disk-backed by default.
        services.AddScoped<IFileStorageService, PEMS.Infrastructure.FileStorage.LocalFileStorageService>();

        // Google Drive integration: config + REST storage client + purpose→folder resolver.
        // The resolver lets the shared FileUploadService pick a folder from a FilePurpose without any
        // handler hard-coding a folder id.
        services.Configure<PEMS.Application.Common.Storage.GoogleDriveOptions>(
            configuration.GetSection(PEMS.Application.Common.Storage.GoogleDriveOptions.SectionName));
        services.AddScoped<IGoogleDriveStorageService,
            PEMS.Infrastructure.FileStorage.GoogleDrive.GoogleDriveStorageService>();
        services.AddScoped<IFileStorageFolderResolver,
            PEMS.Infrastructure.FileStorage.GoogleDrive.GoogleDriveFolderResolver>();

        // Visit request flow services (UC-17)
        services.AddScoped<IVisitRequestService, VisitRequestService>();
        services.AddScoped<IUserProvisionService, UserProvisionService>();
        services.AddScoped<IApprovalRoutingService, ApprovalRoutingService>();

        // External services (scaffolded)
        services.AddScoped<IFaceRecognitionService, FaceRecognitionService>();
        services.AddScoped<IOcrService, OcrService>();

        // Background jobs — scheduled visit reminder dispatch (visit_instance_reminder_settings).
        services.AddHostedService<PEMS.Infrastructure.BackgroundJobs.VisitReminderDispatchHostedService>();

        return services;
    }
}
