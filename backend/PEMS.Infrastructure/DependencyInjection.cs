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

        // Shared pessimistic row locks (role change vs. responsibility-creating flows — see
        // IUserMutationLockService). Scoped: it must join the ambient DbContext transaction.
        services.AddScoped<IUserMutationLockService, PEMS.Infrastructure.Persistence.MySqlUserMutationLockService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICampusRepository, CampusRepository>();

        // Identity / auth services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPureV2SchemaReadiness, PEMS.Infrastructure.Services.PureV2SchemaReadinessService>();
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
        // The one renderer for system email — preview and real send share it, so what an operator
        // previews is what the recipient receives. Scoped because it reads the DbContext per request.
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        // Render → record in email history → send → record the outcome, in one place, so those four
        // steps cannot drift apart across the handlers that send system mail.
        services.AddScoped<PEMS.Application.Emails.Common.ISystemEmailDispatcher,
            PEMS.Application.Emails.Common.SystemEmailDispatcher>();
        services.Configure<PEMS.Application.Emails.Common.EmailRecipientOptions>(
            configuration.GetSection(PEMS.Application.Emails.Common.EmailRecipientOptions.SectionName));
        // Report/invoice mail: store the PDF, record the message AND its attachment linkage, deliver, and
        // fail the command when delivery did not happen. Shared by all six report senders so none of them
        // can send a "see attached" message with nothing attached.
        services.AddScoped<PEMS.Application.Reports.Common.IReportEmailSender,
            PEMS.Application.Reports.Common.ReportEmailSender>();
        // Manual mail (compose / draft-send / reply): record one message, send one MIME for the whole
        // TO+CC+BCC envelope, write back the outcome that actually happened. Shared so the three handlers
        // cannot disagree again about what a CC is.
        services.AddScoped<PEMS.Application.Emails.Common.IManualEmailSender,
            PEMS.Application.Emails.Common.ManualEmailSender>();
        // Whether a reader who was not on a message may still open it because of the business object it is
        // attached to. Borrowed from the visit access rule — never granted by role alone.
        services.AddScoped<PEMS.Application.Emails.Common.ISentEmailObjectScope,
            PEMS.Application.Emails.Common.SentEmailObjectScope>();
        // A file is readable because of what references it. Resolves those references and asks each
        // module's own rule, so /api/files/{id} can no longer be used to walk around a screen's scope.
        services.AddScoped<PEMS.Application.Files.Common.IFileAccessAuthorizationService,
            PEMS.Application.Files.Common.FileAccessAuthorizationService>();
        services.AddScoped<IEmailActionTokenService, EmailActionTokenService>();
        services.AddScoped<PEMS.Application.Accounts.Common.IAccountEmailConfirmationService,
            PEMS.Infrastructure.Email.AccountEmailConfirmationService>();
        services.AddScoped<PEMS.Application.Accounts.Common.IAccountEmailConfirmationMaintenance,
            PEMS.Application.Accounts.Common.AccountEmailConfirmationMaintenance>();
        services.AddHttpContextAccessor();
        services.AddHttpClient();

        // Server-derived request metadata (IP / User-Agent) — never taken from the body.
        services.AddScoped<IRequestMetadataService, RequestMetadataService>();

        // Human verification (Cloudflare Turnstile) for the UC-17 OTP recovery flow.
        services.AddScoped<IHumanVerificationService, TurnstileHumanVerificationService>();

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

        // Visit request flow services (UC-17). Pure V2 only — the V1 create service was removed with
        // its unreachable handlers; the retired V1 endpoints answer 410 without touching a service.
        services.AddScoped<IVisitRequestV2CreateService, VisitRequestV2CreateService>();
        services.AddScoped<IVisitRequestV2EditService, VisitRequestV2EditService>();
        services.AddScoped<IUserProvisionService, UserProvisionService>();
        services.AddScoped<IApprovalRoutingService, ApprovalRoutingService>();

        // Per-campus v2 identity claim (plan §16.4/§16.8): invitation token+email + expiry/redaction.
        services.AddScoped<IVisitContactClaimService, VisitContactClaimService>();
        services.AddScoped<IVisitContactClaimMaintenanceService, VisitContactClaimMaintenanceService>();

        // Per-campus v2 safe edit + amendments (plan §16.6, Phase E).
        services.AddScoped<IVisitSafeEditService, VisitSafeEditService>();
        services.AddScoped<IVisitAmendmentService, VisitAmendmentService>();

        // External services (scaffolded)
        services.AddScoped<IFaceRecognitionService, FaceRecognitionService>();
        services.AddScoped<IOcrService, OcrService>();

        // Business Card OCR (Google Document AI) — docs/PARTNER_canh/02.
        services.AddSingleton<ISecretProtector, PEMS.Infrastructure.Security.AesGcmSecretProtector>();
        services.AddSingleton<PEMS.Infrastructure.Ocr.GoogleServiceAccountTokenProvider>();
        services.AddSingleton<PEMS.Application.BusinessCardOcr.Services.IBusinessCardTextParser,
            PEMS.Infrastructure.Ocr.BusinessCardTextParser>();
        services.AddScoped<PEMS.Application.BusinessCardOcr.Services.IBusinessCardOcrProvider,
            PEMS.Infrastructure.Ocr.GoogleDocumentAiBusinessCardOcrProvider>();
        services.AddScoped<PEMS.Application.BusinessCardOcr.Services.IOcrCredentialResolver,
            PEMS.Infrastructure.Ocr.OcrCredentialResolver>();
        services.AddSingleton<PEMS.Application.BusinessCardOcr.Services.IBusinessCardOcrThrottle,
            PEMS.Infrastructure.Ocr.InMemoryBusinessCardOcrThrottle>();

        // News auto-translation (Google Cloud Translation v3) — reuses the OCR credential
        // resolution + service-account token pipeline.
        services.AddScoped<PEMS.Application.News.Services.INewsTranslationService,
            PEMS.Infrastructure.Translation.GoogleNewsTranslationService>();

        // Shared content-translation abstraction (Gallery names/titles). Same single Google provider —
        // registered against the same implementation so credential/quota logic is never duplicated.
        services.AddScoped<PEMS.Application.Translation.IContentTranslationService,
            PEMS.Infrastructure.Translation.GoogleNewsTranslationService>();

        // Visit-photo face detection (Google Cloud Vision FACE_DETECTION) — reuses the same
        // credential resolution + service-account token pipeline as OCR/Translation on purpose.
        services.AddScoped<PEMS.Application.Delegations.VisitPhotos.FaceScans.Services.IFaceDetectionProvider,
            PEMS.Infrastructure.FaceDetection.GoogleVisionFaceDetectionProvider>();
        services.AddSingleton<PEMS.Application.Delegations.VisitPhotos.FaceScans.Services.IFaceScanThrottle,
            PEMS.Infrastructure.FaceDetection.InMemoryFaceScanThrottle>();

        // Background jobs — scheduled visit reminder dispatch (visit_instance_reminder_settings).
        // The decisions live in the scoped service; the hosted service only wakes it up.
        services.AddScoped<PEMS.Application.Delegations.Reminders.IVisitReminderDispatchService,
            PEMS.Application.Delegations.Reminders.VisitReminderDispatchService>();
        services.AddHostedService<PEMS.Infrastructure.BackgroundJobs.VisitReminderDispatchHostedService>();

        // Background job — HO visibility alert for multi-campus requests with an unprocessed
        // campus close to its planned start (spec §5 HO rule).
        services.AddHostedService<PEMS.Infrastructure.BackgroundJobs.HoUnprocessedCampusAlertHostedService>();

        // Background job — identity-claim expiry (72h) + retention redaction (90d), plan §16.8.
        services.AddHostedService<PEMS.Infrastructure.BackgroundJobs.VisitContactClaimMaintenanceHostedService>();

        // Background job — pending-amendment expiry (window passed / instance started), plan §16.6.
        services.AddHostedService<PEMS.Infrastructure.BackgroundJobs.VisitAmendmentExpiryHostedService>();

        // Background job — pending-account email-confirmation maintenance (P0 #1): expire overdue tokens
        // and auto-cancel long-unconfirmed pending accounts, releasing any reserved Head slot.
        services.AddHostedService<PEMS.Infrastructure.BackgroundJobs.AccountEmailConfirmationMaintenanceHostedService>();

        return services;
    }
}
