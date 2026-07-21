using System.Linq;
using FluentValidation;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitRequestV2;

/// <summary>
/// STRUCTURAL validation for pending-edit v2 (MediatR pipeline, before the handler). Campus content rules are
/// the SAME as create-v2 — each campus is projected through <see cref="CampusVisitEditV2Dto.ToFormDto"/> and
/// validated by the shared <see cref="CampusVisitFormDtoValidator"/> — plus the edit-specific shape rules
/// (expected request row version present, an existing instance must carry its expected row version). The edit
/// service still re-validates every DB/clock/concurrency rule in-transaction.
/// </summary>
public sealed class UpdatePendingVisitRequestV2CommandValidator : AbstractValidator<UpdatePendingVisitRequestV2Command>
{
    private const int MaxCampuses = 10;

    public UpdatePendingVisitRequestV2CommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0UL);
        RuleFor(x => x.Edit).NotNull().WithMessage("Thiếu dữ liệu biểu mẫu.");

        When(x => x.Edit is not null, () =>
        {
            RuleFor(x => x.Edit.ExpectedRequestRowVersion)
                .GreaterThanOrEqualTo(0).WithMessage("Thiếu phiên bản dữ liệu của đơn (row version).");

            // Request-level identity — the SAME shared child validators as create-v2, so a direct
            // edit call cannot blank the registrant's job title / nationality or store a non-number
            // phone (previously the registrant was only null-checked here).
            RuleFor(x => x.Edit.Registrant).NotNull().WithMessage("Thiếu thông tin người đăng ký.");
            RuleFor(x => x.Edit.Registrant!).SetValidator(new RegistrantInputV2Validator())
                .When(x => x.Edit.Registrant is not null);
            RuleFor(x => x.Edit.PrimaryContact).NotNull().WithMessage("Thiếu thông tin đầu mối liên hệ.");
            RuleFor(x => x.Edit.PrimaryContact!).SetValidator(new PrimaryContactV2Validator())
                .When(x => x.Edit.PrimaryContact is not null);

            RuleFor(x => x.Edit.CampusVisits)
                .NotEmpty().WithMessage("Phải có ít nhất 1 cơ sở.");
            RuleFor(x => x.Edit.CampusVisits)
                .Must(cs => cs is null || cs.Count <= MaxCampuses)
                .WithMessage($"Không được vượt quá {MaxCampuses} cơ sở trong một đơn.");
            RuleFor(x => x.Edit.CampusVisits)
                .Must(cs => cs is null || cs
                    .Where(c => !string.IsNullOrWhiteSpace(c.CampusId))
                    .Select(c => c.CampusId.Trim().ToUpperInvariant())
                    .GroupBy(c => c).All(g => g.Count() == 1))
                .WithMessage("Không được chọn trùng cơ sở.")
                .When(x => x.Edit.CampusVisits is { Count: > 0 });
            RuleFor(x => x.Edit.CampusVisits)
                .Must(cs => cs is null || cs
                    .Where(c => c.VisitInstanceId.HasValue)
                    .GroupBy(c => c.VisitInstanceId!.Value).All(g => g.Count() == 1))
                .WithMessage("Một lịch thăm không được xuất hiện hai lần trong yêu cầu sửa.")
                .When(x => x.Edit.CampusVisits is { Count: > 0 });

            RuleForEach(x => x.Edit.CampusVisits).ChildRules(campus =>
            {
                // An EXISTING instance must carry its expected row version (optimistic concurrency contract).
                campus.RuleFor(c => c.ExpectedRowVersion)
                    .NotNull().WithMessage("Thiếu phiên bản dữ liệu của lịch thăm hiện có (row version).")
                    .When(c => c.VisitInstanceId.HasValue);
                // Content rules — identical to create-v2, via the shared projection.
                campus.RuleFor(c => c.ToFormDto()).SetValidator(new CampusVisitFormDtoValidator());
            });
        });
    }
}
