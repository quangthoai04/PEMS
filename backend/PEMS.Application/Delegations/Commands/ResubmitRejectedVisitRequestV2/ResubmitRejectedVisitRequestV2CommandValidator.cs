using System.Linq;
using FluentValidation;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequestV2;

/// <summary>
/// STRUCTURAL validation for resubmit v2 (MediatR pipeline). Same campus-content rules as create/pending-edit
/// via the shared <see cref="CampusVisitFormDtoValidator"/> projection, plus the resubmit-specific shape: EVERY
/// slot must reference an existing instance (visitInstanceId + expected row version) — the campus set is fixed.
/// The edit service re-validates everything (including set equality against the DB) in-transaction.
/// </summary>
public sealed class ResubmitRejectedVisitRequestV2CommandValidator
    : AbstractValidator<ResubmitRejectedVisitRequestV2Command>
{
    public ResubmitRejectedVisitRequestV2CommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0UL);
        RuleFor(x => x.Edit).NotNull().WithMessage("Thiếu dữ liệu biểu mẫu.");

        When(x => x.Edit is not null, () =>
        {
            RuleFor(x => x.Edit.ExpectedRequestRowVersion)
                .GreaterThanOrEqualTo(0).WithMessage("Thiếu phiên bản dữ liệu của đơn (row version).");
            // Request-level identity — the SAME shared child validators as create-v2. Resubmit
            // previously only null-checked these, so a rejected request could be resubmitted with a
            // blank registrant/contact and pass structural validation.
            RuleFor(x => x.Edit.Registrant).NotNull().WithMessage("Thiếu thông tin người đăng ký.");
            RuleFor(x => x.Edit.Registrant!).SetValidator(new RegistrantInputV2Validator())
                .When(x => x.Edit.Registrant is not null);

            RuleFor(x => x.Edit.CampusVisits)
                .NotEmpty().WithMessage("Phải có ít nhất 1 cơ sở.");
            RuleFor(x => x.Edit.CampusVisits)
                .Must(cs => cs is null || cs.All(c => c.VisitInstanceId.HasValue))
                .WithMessage("Không thể đổi danh sách cơ sở khi gửi lại đơn (mỗi cơ sở phải giữ nguyên lịch thăm hiện có).")
                .When(x => x.Edit.CampusVisits is { Count: > 0 });
            RuleFor(x => x.Edit.CampusVisits)
                .Must(cs => cs is null || cs
                    .Where(c => c.VisitInstanceId.HasValue)
                    .GroupBy(c => c.VisitInstanceId!.Value).All(g => g.Count() == 1))
                .WithMessage("Một lịch thăm không được xuất hiện hai lần trong yêu cầu gửi lại.")
                .When(x => x.Edit.CampusVisits is { Count: > 0 });

            RuleForEach(x => x.Edit.CampusVisits).ChildRules(campus =>
            {
                campus.RuleFor(c => c.ExpectedRowVersion)
                    .NotNull().WithMessage("Thiếu phiên bản dữ liệu của lịch thăm (row version).")
                    .When(c => c.VisitInstanceId.HasValue);
                campus.RuleFor(c => c.ToFormDto()).SetValidator(new CampusVisitFormDtoValidator());
            });
        });
    }
}
