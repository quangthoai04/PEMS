using FluentValidation;

namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;

/// <summary>
/// Full-form re-validation for the Visitor pending edit. Same shared rule set as the
/// UC-17 submit, but the advance window is 24h (not 72h) — spec "Visitor sửa đơn trước
/// khi duyệt: lịch còn cách hiện tại ≥ 24 giờ".
/// </summary>
public sealed class UpdatePendingVisitRequestCommandValidator
    : AbstractValidator<UpdatePendingVisitRequestCommand>
{
    public UpdatePendingVisitRequestCommandValidator()
    {
        this.ApplyVisitRequestFormRules(minStartAdvanceHours: 24);
    }
}
