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
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public string? ReplyToEmail { get; set; }
    public string? ReplyToName { get; set; }
    public int? MaxFileSizeMb { get; set; }
    public List<string> AllowedMimeTypes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── Capabilities (computed per caller; the handlers stay the real gate) ──

    /// <summary>
    /// DATABASE = managed via this console; ENVIRONMENT = configured on the server (read-only here);
    /// HYBRID = both, and the console owns only part of it.
    ///
    /// <para>
    /// Google Drive is the HYBRID case, and the reason the third value exists. Its client id, secret,
    /// redirect URI and fourteen folder ids are deployment configuration; only the refresh token — the one
    /// piece that expires and has to be replaced without a redeploy — lives in the database. Calling the
    /// card ENVIRONMENT (as it was) told an ADMIN there was nothing to do here, which was wrong about
    /// exactly the field they needed.
    /// </para>
    /// </summary>
    public string ManagementSource { get; set; } = "DATABASE";
    public bool CanEdit { get; set; }
    public bool CanTest { get; set; }
    public bool CanToggleStatus { get; set; }
    public bool CanConfigureQuota { get; set; }

    /// <summary>ADMIN may start the OAuth consent round-trip that (re)issues this integration's credential.</summary>
    public bool CanConnectOAuth { get; set; }

    /// <summary>ADMIN may clear the stored OAuth credential.</summary>
    public bool CanDisconnectOAuth { get; set; }

    /// <summary>
    /// NOT_CONFIGURED | CONNECTED | ERROR — what the card says about the credential, never anything about
    /// its value. ERROR means a stored credential exists and its last connection test failed.
    /// </summary>
    public string CredentialStatus { get; set; } = ApiIntegrationCredentialStatuses.NotConfigured;
}

/// <summary>The complete vocabulary of <see cref="ApiIntegrationDto.CredentialStatus"/>.</summary>
public static class ApiIntegrationCredentialStatuses
{
    public const string NotConfigured = "NOT_CONFIGURED";
    public const string Connected = "CONNECTED";
    public const string Error = "ERROR";
}

/// <summary>
/// Where to send the ADMIN's browser to grant consent. The URL carries the client id (public), the scope
/// and a sealed <c>state</c> — never the client secret, and never anything derived from the stored token.
/// </summary>
public sealed class GoogleDriveOAuthStartResultDto
{
    public string AuthorizationUrl { get; set; } = string.Empty;
}

/// <summary>
/// The outcome of Google's callback, as the controller needs it to build a redirect. Deliberately has no
/// field that could carry a credential, a Google error description, or a token response.
/// </summary>
public sealed class GoogleDriveOAuthCallbackResultDto
{
    public bool Success { get; set; }

    /// <summary>
    /// One of <see cref="PEMS.Application.Common.Storage.GoogleDriveOAuthRedirectReasons"/> when
    /// <see cref="Success"/> is false; null otherwise.
    /// </summary>
    public string? Reason { get; set; }
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
