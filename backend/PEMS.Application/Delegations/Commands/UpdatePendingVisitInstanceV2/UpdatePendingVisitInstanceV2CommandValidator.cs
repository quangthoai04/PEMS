using FluentValidation;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitInstanceV2;

/// <summary>
/// STRUCTURAL validation for a per-campus pending edit (MediatR pipeline, before the handler). The
/// campus's content is held to exactly the create-v2 bar via the shared
/// <see cref="CampusVisitFormDtoValidator"/> — editing one campus of a request must not be able to
/// produce a campus that could not have been created.
///
/// <para>
/// The handler and the edit service still re-validate every DB/clock/concurrency/authority rule
/// in-transaction; nothing here is load-bearing for authorization.
/// </para>
/// </summary>
public sealed class UpdatePendingVisitInstanceV2CommandValidator
    : AbstractValidator<UpdatePendingVisitInstanceV2Command>
{
    public UpdatePendingVisitInstanceV2CommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0UL);
        RuleFor(x => x.VisitInstanceId).GreaterThan(0UL);
        RuleFor(x => x.Content).NotNull().WithMessage("Thiếu dữ liệu biểu mẫu của cơ sở.");

        When(x => x.Content is not null, () =>
        {
            // Optimistic concurrency is part of the contract, not an optional extra: without the
            // expected version a stale form would silently overwrite whatever arrived in between.
            RuleFor(x => x.Content.ExpectedRowVersion)
                .NotNull().WithMessage("Thiếu phiên bản dữ liệu của lịch thăm (row version).");
            // This command always targets an EXISTING instance (VisitInstanceId is a required route
            // param above), so its operational contact is always a read-only replay, never a fresh
            // write — Create-level completeness does not apply (see
            // CampusVisitFormDtoValidator.requireCompleteOperationalContact).
            RuleFor(x => x.Content.ToFormDto())
                .SetValidator(new CampusVisitFormDtoValidator(requireCompleteOperationalContact: false));
        });

        // Approving without naming a Host is not a thing that exists (§8): an ASSIGNED campus with no
        // Host is a corrupt row, so the payload is refused rather than the invariant being repaired
        // afterwards.
        When(x => x.ApproveAfterSave is not null, () =>
        {
            RuleFor(x => x.ApproveAfterSave!.HostUserId)
                .GreaterThan(0UL).WithMessage("Khi duyệt yêu cầu, bạn phải chọn host chính thức.");
            RuleFor(x => x.ApproveAfterSave!.DecisionNote).MaximumLength(500);
        });
    }
}
