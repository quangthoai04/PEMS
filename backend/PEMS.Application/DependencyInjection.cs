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
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        // FluentValidation runs as a MediatR pipeline behaviour for every request.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

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

        // visit_requests.status aggregate (campus-independent approval) — mirrors the DB triggers.
        services.AddScoped<PEMS.Application.Delegations.Services.IVisitRequestAggregateStatusService, PEMS.Application.Delegations.Services.VisitRequestAggregateStatusService>();

        // Central per-campus form content resolver — the one place campus scope and detail lookup live.
        services.AddScoped<PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService, PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService>();

        // Student visit photo Drive-folder provisioning (VR-{request}/{campus} tree, one row per request).
        services.AddScoped<PEMS.Application.Delegations.VisitPhotos.IVisitPhotoFolderService,
            PEMS.Application.Delegations.VisitPhotos.VisitPhotoFolderService>();

        return services;
    }
}
