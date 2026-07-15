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

        // EverAI TTS narration for gallery items (hash + ensure/job service + in-process job queue).
        // The EverAI HTTP client + hosted worker are registered in Infrastructure.
        services.AddSingleton<PEMS.Application.Galleries.Tts.IGalleryTtsHashService,
            PEMS.Application.Galleries.Tts.GalleryTtsHashService>();
        services.AddSingleton<PEMS.Application.Galleries.Tts.IGalleryTtsJobQueue,
            PEMS.Application.Galleries.Tts.GalleryTtsJobQueue>();
        services.AddScoped<PEMS.Application.Galleries.Tts.IGalleryItemTtsService,
            PEMS.Application.Galleries.Tts.GalleryItemTtsService>();

        services.AddScoped<PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer, PEMS.Application.Emails.Utils.EmailImageLayoutNormalizer>();

        services.AddScoped<PEMS.Application.Notifications.Common.INotificationService, PEMS.Application.Notifications.Common.NotificationService>();

        // visit_requests.status aggregate (campus-independent approval) — mirrors the DB triggers.
        services.AddScoped<PEMS.Application.Delegations.Services.IVisitRequestAggregateStatusService, PEMS.Application.Delegations.Services.VisitRequestAggregateStatusService>();

        // Per-campus form v2 central dual-read resolver (PR-3).
        services.AddScoped<PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService, PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService>();

        return services;
    }
}
