using MediatR;
using PEMS.Application.BusinessCardOcr.Common;

namespace PEMS.Application.BusinessCardOcr.Commands.ScanBusinessCard;

/// <summary>
/// POST /api/business-card-ocr/scan (multipart) — uploads a card image, calls
/// Google Document AI through the backend, stores the OCR draft on
/// business_card_ocr_jobs and returns it for user review. Nothing is written to
/// partner_contacts here.
/// </summary>
public sealed class ScanBusinessCardCommand : IRequest<BusinessCardOcrJobDto>
{
    public byte[] FileBytes { get; set; } = System.Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    public ulong? VisitInstanceId { get; set; }
    public ulong? GuestMemberId { get; set; }
    public ulong? MinuteParticipantId { get; set; }
    /// <summary>Preselected partner (scan opened from partner detail) — skips auto match.</summary>
    public ulong? PartnerId { get; set; }

    /// <summary>Idempotency-Key header value (replay-safe retries).</summary>
    public string? IdempotencyKey { get; set; }
    /// <summary>Client IP captured by the controller for rate limiting.</summary>
    public string? ClientIp { get; set; }
}
