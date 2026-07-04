using System;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.BusinessCardOcr.Services;

/// <summary>
/// Cloud OCR provider abstraction (phase 1: Google Document AI OCR processor).
/// Implementations live in Infrastructure and are the ONLY code allowed to talk
/// to the cloud endpoint — the frontend never calls the provider directly.
/// </summary>
public interface IBusinessCardOcrProvider
{
    Task<BusinessCardOcrProviderResult> ExtractAsync(
        BusinessCardOcrProviderInput input,
        OcrProviderRuntimeConfig config,
        CancellationToken cancellationToken);

    /// <summary>Verifies processor access (GetProcessor) without running a full document.</summary>
    Task<BusinessCardOcrConnectionTestResult> TestConnectionAsync(
        OcrProviderRuntimeConfig config,
        CancellationToken cancellationToken);
}

public sealed class BusinessCardOcrProviderInput
{
    public byte[] FileBytes { get; init; } = Array.Empty<byte>();
    public string MimeType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
}

public sealed class BusinessCardOcrProviderResult
{
    public string ProviderName { get; init; } = "GOOGLE_DOCUMENT_AI";
    public string? ProviderRequestId { get; init; }
    public string RawText { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? JobTitle { get; init; }
    public string? DepartmentName { get; init; }
    public string? Organization { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? Address { get; init; }
}

public sealed class BusinessCardOcrConnectionTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
}
