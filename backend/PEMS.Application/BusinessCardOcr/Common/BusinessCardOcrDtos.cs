using System;

namespace PEMS.Application.BusinessCardOcr.Common;

public sealed class BusinessCardOcrParsedDto
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? Organization { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Address { get; set; }
}

public sealed class BusinessCardOcrMatchedPartnerDto
{
    public ulong PartnerId { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public string ProfileStatus { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>OCR draft returned by scan + job detail. Raw OCR text is never included.</summary>
public sealed class BusinessCardOcrJobDto
{
    public ulong OcrJobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public decimal? ConfidenceScore { get; set; }
    public ulong ScannedCardFileId { get; set; }
    public BusinessCardOcrParsedDto? Parsed { get; set; }
    public BusinessCardOcrMatchedPartnerDto? MatchedPartner { get; set; }
    public ulong? ConfirmedPartnerId { get; set; }
    public ulong? ConfirmedContactId { get; set; }
    public ulong? SourceVisitInstanceId { get; set; }
    public ulong? SourceGuestMemberId { get; set; }
    public ulong? SourceMinuteParticipantId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public sealed class ConfirmBusinessCardContactResponse
{
    public ulong OcrJobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public ulong PartnerId { get; set; }
    public ulong ContactId { get; set; }
    public ulong? VisitGuestPartnerLinkId { get; set; }
}

public static class BusinessCardOcrErrorCodes
{
    public const string Forbidden = "OCR_FORBIDDEN";
    public const string ConfigInactive = "OCR_CONFIG_INACTIVE";
    public const string RateLimited = "OCR_RATE_LIMITED";
    public const string QuotaExceeded = "OCR_QUOTA_EXCEEDED";
    public const string InvalidFile = "OCR_INVALID_FILE";
    public const string JobNotFound = "OCR_JOB_NOT_FOUND";
    public const string JobNotConfirmable = "OCR_JOB_NOT_CONFIRMABLE";
    public const string JobAlreadyConfirmed = "OCR_JOB_ALREADY_CONFIRMED";
    public const string ProviderFailed = "OCR_PROVIDER_FAILED";
}
