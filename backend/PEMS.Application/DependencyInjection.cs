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

        services.AddScoped<PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer, PEMS.Application.Emails.Utils.EmailImageLayoutNormalizer>();

        services.AddScoped<PEMS.Application.Notifications.Common.INotificationService, PEMS.Application.Notifications.Common.NotificationService>();
        services.AddScoped<PEMS.Application.Notifications.Common.INotificationTargetResolver, PEMS.Application.Notifications.Common.NotificationTargetResolver>();

        return services;
    }
}
