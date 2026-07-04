using MediatR;
using PEMS.Application.BusinessCardOcr.Common;

namespace PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;

/// <summary>
/// POST /api/business-card-ocr/jobs/{ocrJobId}/confirm-contact — the ONLY path that
/// turns an OCR draft into a partner_contacts row. Runs once per job.
/// </summary>
public sealed class ConfirmBusinessCardContactCommand : IRequest<ConfirmBusinessCardContactResponse>
{
    public ulong OcrJobId { get; set; }
    public ulong PartnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? Note { get; set; }
    public bool IsPrimary { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public ulong? GuestMemberId { get; set; }
    public ulong? MinuteParticipantId { get; set; }
}
