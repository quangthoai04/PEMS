using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Domain.Entities.ApiIntegrations;

/// <summary>
/// One cloud OCR run for a business card. The OCR result stays a DRAFT on this row
/// (status SUCCEEDED) until the user reviews and confirms it — only then is a
/// partner_contacts row created and the job flips to CONFIRMED. Raw OCR text and
/// the parsed JSON payload are stored encrypted only.
/// </summary>
[Table("business_card_ocr_jobs")]
public class BusinessCardOcrJob
{
    public const string StatusUploaded = "UPLOADED";
    public const string StatusProcessing = "PROCESSING";
    public const string StatusSucceeded = "SUCCEEDED";
    public const string StatusFailed = "FAILED";
    public const string StatusConfirmed = "CONFIRMED";
    public const string StatusDiscarded = "DISCARDED";

    [Key]
    [Column("ocr_job_id")]
    public ulong OcrJobId { get; set; }

    [Column("scanned_card_file_id")]
    public ulong ScannedCardFileId { get; set; }

    [Column("api_config_id")]
    public ulong ApiConfigId { get; set; }

    /// <summary>UPLOADED | PROCESSING | SUCCEEDED | FAILED | CONFIRMED | DISCARDED</summary>
    [Column("status")]
    public string Status { get; set; } = StatusUploaded;

    [Column("provider_name")]
    public string ProviderName { get; set; } = "GOOGLE_DOCUMENT_AI";

    [Column("provider_request_id")]
    public string? ProviderRequestId { get; set; }

    [Column("provider_processor_id")]
    public string? ProviderProcessorId { get; set; }

    [Column("provider_location")]
    public string? ProviderLocation { get; set; }

    [Column("raw_text_encrypted")]
    public string? RawTextEncrypted { get; set; }

    [Column("parsed_json_encrypted")]
    public string? ParsedJsonEncrypted { get; set; }

    [Column("parsed_full_name")]
    public string? ParsedFullName { get; set; }

    [Column("parsed_email")]
    public string? ParsedEmail { get; set; }

    [Column("parsed_phone")]
    public string? ParsedPhone { get; set; }

    [Column("parsed_job_title")]
    public string? ParsedJobTitle { get; set; }

    [Column("parsed_department_name")]
    public string? ParsedDepartmentName { get; set; }

    [Column("parsed_organization")]
    public string? ParsedOrganization { get; set; }

    [Column("parsed_website_url")]
    public string? ParsedWebsiteUrl { get; set; }

    [Column("parsed_address")]
    public string? ParsedAddress { get; set; }

    [Column("confidence_score")]
    public decimal? ConfidenceScore { get; set; }

    [Column("matched_partner_id")]
    public ulong? MatchedPartnerId { get; set; }

    [Column("confirmed_partner_id")]
    public ulong? ConfirmedPartnerId { get; set; }

    [Column("confirmed_contact_id")]
    public ulong? ConfirmedContactId { get; set; }

    [Column("source_visit_instance_id")]
    public ulong? SourceVisitInstanceId { get; set; }

    [Column("source_guest_member_id")]
    public ulong? SourceGuestMemberId { get; set; }

    [Column("source_minute_participant_id")]
    public ulong? SourceMinuteParticipantId { get; set; }

    [Column("file_sha256")]
    public string? FileSha256 { get; set; }

    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [Column("confirmed_by")]
    public ulong? ConfirmedBy { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    public virtual UploadedFile ScannedCardFile { get; set; } = null!;
    public virtual ApiConfiguration ApiConfiguration { get; set; } = null!;
    public virtual Partner? MatchedPartner { get; set; }
    public virtual Partner? ConfirmedPartner { get; set; }
    public virtual PartnerContact? ConfirmedContact { get; set; }
}
