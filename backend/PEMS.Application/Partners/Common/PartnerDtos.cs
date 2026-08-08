using System;
using System.Collections.Generic;

namespace PEMS.Application.Partners.Common;

public sealed class PartnerListItemDto
{
    public ulong PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string PartnerType { get; set; } = "UNIVERSITY";
    public ulong OwnerCampusId { get; set; }
    public string OwnerCampusName { get; set; } = string.Empty;
    public string? CreatorName { get; set; }
    public string ProfileStatus { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string CooperationStatus { get; set; } = string.Empty;
    public ulong? LogoFileId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    /// <summary>Actions the CURRENT user may run on this row (view/edit/approve/reject).</summary>
    public List<string> AllowedActions { get; set; } = new();

    public string? EnglishName { get; set; }
    public string? EnglishShortName { get; set; }
    public bool HasEnglishTranslation { get; set; }
}


public sealed class PartnerListResponse
{
    public List<PartnerListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool CanCreate { get; set; }
}

public sealed class PartnerDetailDto
{
    public ulong PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string PartnerType { get; set; } = "UNIVERSITY";
    public string CooperationStatus { get; set; } = string.Empty;
    public string ProfileStatus { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public ulong? LogoFileId { get; set; }
    public ulong? CoverFileId { get; set; }
    /// <summary>Backend proxy URL (<c>/api/files/{id}/content</c>) for <see cref="LogoFileId"/>, or null.</summary>
    public string? LogoUrl { get; set; }
    /// <summary>Backend proxy URL (<c>/api/files/{id}/content</c>) for <see cref="CoverFileId"/>, or null.</summary>
    public string? CoverUrl { get; set; }
    public string? PublicSlug { get; set; }
    public ulong OwnerCampusId { get; set; }
    public string OwnerCampusName { get; set; } = string.Empty;
    public ulong? CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ReviewNote { get; set; }
    public ulong? ReviewedBy { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public List<string> AllowedActions { get; set; } = new();

    public string? EnglishName { get; set; }
    public string? EnglishShortName { get; set; }
    public string? EnglishDescription { get; set; }
    public string? EnglishAddress { get; set; }
    public bool HasEnglishTranslation { get; set; }
}

public sealed class PartnerContactDto
{
    public ulong ContactId { get; set; }
    public ulong PartnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? Note { get; set; }
    public string SourceType { get; set; } = "MANUAL";
    public ulong? ScannedCardFileId { get; set; }
    public ulong? AvatarFileId { get; set; }
    public string? AvatarUrl { get; set; }
    public decimal? OcrConfidence { get; set; }
    public bool IsPrimary { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
}

public sealed class PartnerAliasDto
{
    public ulong PartnerAliasId { get; set; }
    public ulong PartnerId { get; set; }
    public string AliasName { get; set; } = string.Empty;
    public string AliasNameKey { get; set; } = string.Empty;
    public string Source { get; set; } = "MANUAL";
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
}

public sealed class PartnerDocumentDto
{
    public ulong DocumentId { get; set; }
    public ulong FileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DocumentCategory { get; set; }
    public string Status { get; set; } = "PUBLISHED";
    public string? OriginalFilename { get; set; }
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatorName { get; set; }
}

public sealed class PartnerMatchDto
{
    /// <summary>NONE | SUGGESTED | PENDING_APPROVAL | APPROVED (of the best candidate).</summary>
    public string MatchStatus { get; set; } = "NONE";
    public ulong? PartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string? ProfileStatus { get; set; }
    public decimal? Confidence { get; set; }
    public string? Reason { get; set; }

    /// <summary>Top scored candidates (best first). Empty when nothing matched.</summary>
    public List<PartnerMatchCandidateDto> Candidates { get; set; } = new();
}

/// <summary>One partner suggested by the matcher, with the display fields the link picker needs.</summary>
public sealed class PartnerMatchCandidateDto
{
    public ulong PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string ProfileStatus { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public ulong OwnerCampusId { get; set; }
    public string? OwnerCampusName { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    /// <summary>0–100 match score (higher = stronger).</summary>
    public decimal MatchScore { get; set; }
    public string? MatchReason { get; set; }
    /// <summary>Whether the CURRENT user is allowed to link a guest to this partner (campus scope).</summary>
    public bool CanLink { get; set; }
}

public sealed class VisitGuestPartnerLinkDto
{
    public ulong LinkId { get; set; }
    public ulong VisitRequestId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public ulong? GuestMemberId { get; set; }
    public ulong? MinuteParticipantId { get; set; }
    public ulong PartnerId { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerProfileStatus { get; set; } = string.Empty;
    public ulong? PartnerContactId { get; set; }
    public string? PartnerContactName { get; set; }
    public string MatchSource { get; set; } = "MANUAL";
    public string MatchStatus { get; set; } = "CONFIRMED";
    public decimal? ConfidenceScore { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PublicPartnerDto
{
    public ulong PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string PartnerType { get; set; } = "UNIVERSITY";
    public ulong? LogoFileId { get; set; }
    public ulong? CoverFileId { get; set; }
    public string? PublicSlug { get; set; }
}

public sealed class PartnerVisitHistoryDto
{
    public ulong VisitInstanceId { get; set; }
    public ulong VisitRequestId { get; set; }
    public string DelegationName { get; set; } = string.Empty;
    public ulong CampusId { get; set; }
    public string CampusName { get; set; } = string.Empty;
    public DateTime PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public string? HostName { get; set; }
    public string LinkType { get; set; } = "DIRECT";
}
