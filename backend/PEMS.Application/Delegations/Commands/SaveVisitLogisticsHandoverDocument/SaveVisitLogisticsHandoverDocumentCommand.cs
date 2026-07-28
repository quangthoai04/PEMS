using MediatR;

namespace PEMS.Application.Delegations.Commands.SaveVisitLogisticsHandoverDocument;

/// <summary>
/// "Lưu vào hệ thống" button next to the existing "Tải PDF" (window.print) in TaskHandoverModal —
/// generates a real PDF of the fully-signed handover record (BORROW or RETURN) server-side, uploads
/// it to the delegation's Drive "Hậu cần" folder, and creates/updates a <c>documents</c> row
/// (OwnerType=LOGISTICS) so it shows up on "Quản lý tài liệu". Re-saving the SAME handover (e.g.
/// after a note edit before signing closed) reuses the one document row instead of piling up
/// duplicates — see the handler.
/// </summary>
public sealed class SaveVisitLogisticsHandoverDocumentCommand : IRequest<SaveVisitLogisticsHandoverDocumentResponse>
{
    public ulong LogisticsItemId { get; set; }

    /// <summary>BORROW | RETURN — see <c>PEMS.Domain.Constants.LogisticsHandoverTypes</c>.</summary>
    public string HandoverType { get; set; } = string.Empty;
}
