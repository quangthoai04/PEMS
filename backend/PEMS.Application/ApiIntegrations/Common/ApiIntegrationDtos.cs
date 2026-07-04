using System;
using System.Collections.Generic;

namespace PEMS.Application.ApiIntegrations.Common;

/// <summary>
/// Config DTO — secrets are NEVER included. HasCredential/SecretRef only describe
/// whether/where a credential exists.
/// </summary>
public sealed class ApiIntegrationDto
{
    public ulong ApiConfigId { get; set; }
    public string ApiCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ProviderName { get; set; }
    public string? Purpose { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DataSensitivity { get; set; } = "CONFIDENTIAL";
    public bool AllowsProviderTraining { get; set; }
    public uint? RetentionDays { get; set; }
    public uint? RateLimitPerMinute { get; set; }
    public uint? MonthlyQuota { get; set; }
    public int TimeoutSeconds { get; set; }
    public string? LastTestStatus { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public string? LastTestMessage { get; set; }
    public bool HasCredential { get; set; }
    public string? SecretRef { get; set; }
    /// <summary>Provider settings (non-secret): projectId, location, processorId, endpoint…</summary>
    public string? ProjectId { get; set; }
    public string? Location { get; set; }
    public string? ProcessorId { get; set; }
    public string? Endpoint { get; set; }
    public int? MaxFileSizeMb { get; set; }
    public List<string> AllowedMimeTypes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ApiConnectionTestResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public DateTime TestedAt { get; set; }
}

public sealed class ApiQuotaDto
{
    public ulong ApiUsageQuotaId { get; set; }
    public ulong ApiConfigId { get; set; }
    public ulong? CampusId { get; set; }
    public string CampusScopeKey { get; set; } = "GLOBAL";
    public string PeriodYyyymm { get; set; } = string.Empty;
    public int MonthlyLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public sealed class ApiRequestLogDto
{
    public ulong ApiRequestLogId { get; set; }
    public ulong ApiConfigId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int? HttpStatus { get; set; }
    public int? ResponseTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ApiRequestLogListResponse
{
    public List<ApiRequestLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
