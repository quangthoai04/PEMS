using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.BusinessCardOcr.Services;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Application.ApiIntegrations.Commands.TestApiIntegration;

public sealed class TestApiIntegrationCommandHandler
    : IRequestHandler<TestApiIntegrationCommand, ApiConnectionTestResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IBusinessCardOcrProvider _provider;
    private readonly IOcrCredentialResolver _credentialResolver;

    public TestApiIntegrationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IBusinessCardOcrProvider provider, IOcrCredentialResolver credentialResolver)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _provider = provider;
        _credentialResolver = credentialResolver;
    }

    public async Task<ApiConnectionTestResultDto> Handle(
        TestApiIntegrationCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        var config = await _db.ApiConfigurations
            .FirstOrDefaultAsync(c => c.ApiConfigId == request.ApiConfigId && c.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("ApiConfiguration", request.ApiConfigId);

        if (config.Purpose != BusinessCardOcrConstants.Purpose)
            throw new BusinessRuleException(
                "Chỉ hỗ trợ test kết nối cho cấu hình BUSINESS_CARD_OCR.",
                ApiIntegrationErrorCodes.InvalidPurpose);

        var now = _clock.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        bool success;
        string message;
        string? errorCode = null;

        var credential = _credentialResolver.Resolve(config);
        if (string.IsNullOrEmpty(credential))
        {
            success = false;
            message = "Chưa cấu hình credential (serviceAccountJson/secretRef).";
            errorCode = ApiIntegrationErrorCodes.CredentialRequired;
        }
        else
        {
            var runtime = new OcrProviderRuntimeConfig
            {
                ApiConfigId = config.ApiConfigId,
                Settings = OcrProviderSettings.Parse(config.SettingsJson),
                CredentialJson = credential,
                TimeoutSeconds = config.TimeoutSeconds,
            };
            var result = await _provider.TestConnectionAsync(runtime, cancellationToken);
            success = result.Success;
            message = result.Message;
            errorCode = result.ErrorCode;
        }
        stopwatch.Stop();

        config.LastTestStatus = success ? "SUCCESS" : "FAILED";
        config.LastTestedAt = now;
        config.LastTestMessage = message;
        config.UpdatedAt = now;
        config.UpdatedBy = _currentUser.UserId;

        // Sanitized log — endpoint + outcome only, never credentials/headers.
        _db.ApiRequestLogs.Add(new ApiRequestLog
        {
            ApiConfigId = config.ApiConfigId,
            RequestedBy = _currentUser.UserId,
            RelatedType = "CONNECTION_TEST",
            Endpoint = $"{config.BaseUrl}/v1/.../processors/(get)",
            Method = "GET",
            HttpStatus = success ? 200 : null,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
            Success = success,
            ErrorCode = errorCode,
            ErrorMessage = success ? null : message,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ApiConnectionTestResultDto
        {
            Success = success,
            Message = message,
            ErrorCode = errorCode,
            ResponseTimeMs = stopwatch.ElapsedMilliseconds,
            TestedAt = now,
        };
    }
}
