using FluentValidation;

namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;

/// <summary>
/// Full-form re-validation for the Visitor resubmit — the whole form is validated like a
/// brand-new submit, but the advance window is 24h (spec "gửi lại sau reject: lịch mới
/// ≥ now + 24 giờ"). Campus-set equality with the original request is business validation
/// in the handler (needs the database).
/// </summary>
public sealed class ResubmitRejectedVisitRequestCommandValidator
    : AbstractValidator<ResubmitRejectedVisitRequestCommand>
{
    public ResubmitRejectedVisitRequestCommandValidator()
    {
        this.ApplyVisitRequestFormRules(minStartAdvanceHours: 24);
    }
}
