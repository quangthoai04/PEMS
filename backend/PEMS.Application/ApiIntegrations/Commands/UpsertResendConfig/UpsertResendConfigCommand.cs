using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertResendConfig;

public sealed class UpsertResendConfigCommand : IRequest<ApiIntegrationDto>
{
    public string Name { get; set; } = "Resend - Gửi email hệ thống";
    public string? ApiKey { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "PEMS";
    public string? ReplyToEmail { get; set; }
    public string? ReplyToName { get; set; }
    public uint RateLimitPerMinute { get; set; } = 60;
    public uint MonthlyQuota { get; set; } = 10000;
    public int TimeoutSeconds { get; set; } = 30;
}
