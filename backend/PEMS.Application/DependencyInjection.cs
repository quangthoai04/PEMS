using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Behaviours;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using System.Reflection;

namespace PEMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Global QuestPDF license declaration for community usage across all PDF renderers/exports
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        // FluentValidation runs as a MediatR pipeline behaviour for every request.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        // Send idempotency (G11 / R-103). Registered AFTER validation so a malformed request is refused
        // before a reservation is taken — a rejected payload must not spend the user's key. It does
        // nothing at all unless the request implements IIdempotentEmailSend.
        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(PEMS.Application.Emails.Idempotency.EmailSendIdempotencyBehaviour<,>));

        // The reservation the current request is running under: written by the behaviour, read by
        // ReportEmailSender at the moment it is about to call the provider.
        services.AddScoped<PEMS.Application.Emails.Idempotency.EmailSendAttempt>();

        // Shared file-upload foundation (reused by avatar / gallery / news / document / minutes …).
        // The folder resolver + low-level Drive client are registered in Infrastructure.
        services.AddSingleton<IFileChecksumService, FileChecksumService>();
        services.AddSingleton<IFileValidationPolicy, FileValidationPolicy>();
        services.AddScoped<IFileObjectKeyBuilder, FileObjectKeyBuilder>();
        services.AddScoped<IFileUploadService, FileUploadService>();

        // Gallery external (YouTube) media — metadata-only files rows, no Drive upload.
        services.AddScoped<PEMS.Application.Galleries.Common.IGalleryExternalMediaService,
            PEMS.Application.Galleries.Common.GalleryExternalMediaService>();

        // Gallery auto-translation coordinator (VI → EN, batch + dedupe, never throws for provider errors).
        services.AddScoped<PEMS.Application.Galleries.Common.IGalleryTranslationCoordinator,
            PEMS.Application.Galleries.Common.GalleryTranslationCoordinator>();

        services.AddScoped<PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer, PEMS.Application.Emails.Utils.EmailImageLayoutNormalizer>();

        services.AddScoped<PEMS.Application.Notifications.Common.INotificationService, PEMS.Application.Notifications.Common.NotificationService>();

        // Department Leader personnel scope — the single authorization gate behind /api/department-leader.
        // Registered here (not in Infrastructure) because it only needs the DbContext abstraction and the
        // current-user claims; keeping one implementation is what stops each handler from growing its own.
        services.AddScoped<PEMS.Application.DepartmentLeaderPersonnel.Common.IDepartmentLeaderPersonnelScopeService,
            PEMS.Application.DepartmentLeaderPersonnel.Common.DepartmentLeaderPersonnelScopeService>();

        // visit_requests.status aggregate (campus-independent approval) — mirrors the DB triggers.
        services.AddScoped<PEMS.Application.Delegations.Services.IVisitRequestAggregateStatusService, PEMS.Application.Delegations.Services.VisitRequestAggregateStatusService>();

        // Central per-campus form content resolver — the one place campus scope and detail lookup live.
        services.AddScoped<PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService, PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService>();

        // Student visit photo Drive-folder provisioning (VR-{request}/{campus} tree, one row per request).
        services.AddScoped<PEMS.Application.Delegations.VisitPhotos.IVisitPhotoFolderService,
            PEMS.Application.Delegations.VisitPhotos.VisitPhotoFolderService>();

        // Archives report exports (PDF/Excel/CSV) to the flat "Report" Drive folder + documents row.
        services.AddScoped<PEMS.Application.Reports.Common.IReportArchiveService,
            PEMS.Application.Reports.Common.ReportArchiveService>();

        // "Báo cáo Lịch trình" for one campus instance — shared by the VisitProcess download and the
        // setup-progress email, so the two can never render a different document from the same data.
        services.AddScoped<PEMS.Application.Delegations.Queries.ExportScheduleReport.IScheduleReportArtifactService,
            PEMS.Application.Delegations.Queries.ExportScheduleReport.ScheduleReportArtifactService>();

        // Default TO/CC/BCC of the Host's setup-progress email, derived from the instance rather than
        // from whatever the compose screen had loaded.
        services.AddScoped<PEMS.Application.Delegations.SetupProgressEmail.IVisitSetupProgressRecipientResolver,
            PEMS.Application.Delegations.SetupProgressEmail.VisitSetupProgressRecipientResolver>();

        // Renders the setup-progress message + its Schedule Report from one read, for both "mở soạn thư"
        // and "đồng bộ" — so a refresh cannot produce a body the prepare would not have produced.
        services.AddScoped<PEMS.Application.Delegations.SetupProgressEmail.IVisitSetupProgressComposer,
            PEMS.Application.Delegations.SetupProgressEmail.VisitSetupProgressComposer>();

        // One send pipeline behind every manual path — compose, reply and the setup-progress send — so
        // the content, envelope and attachment rules are one implementation rather than three.
        services.AddScoped<PEMS.Application.Emails.Common.IDirectEmailSender,
            PEMS.Application.Emails.Common.DirectEmailSender>();

        return services;
    }
}
