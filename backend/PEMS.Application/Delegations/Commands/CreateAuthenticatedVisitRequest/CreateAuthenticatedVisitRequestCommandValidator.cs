using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.CreateAuthenticatedVisitRequest;

/// <summary>
/// Shape validation for the authenticated create. Reuses the exact same shared form
/// rule set as the public flow (no rule drift), plus the per-campus processing shape:
/// a known mode, ASSIGN_HOST must carry a host id, other modes must NOT, and every
/// processing entry must reference a selected campus. Role/scope/DB checks (who may
/// self-host where, candidate eligibility) live in the handler inside the transaction.
/// </summary>
public sealed class CreateAuthenticatedVisitRequestCommandValidator
    : AbstractValidator<CreateAuthenticatedVisitRequestCommand>
{
    private static readonly string[] KnownModes =
    {
        CampusSubmissionModes.SendForReview,
        CampusSubmissionModes.SelfHost,
        CampusSubmissionModes.AssignHost,
    };

    public CreateAuthenticatedVisitRequestCommandValidator()
    {
        this.ApplyVisitRequestFormRules();

        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Thiếu mã phiên gửi đơn.")
            .Must(v => Guid.TryParse(v, out _)).WithMessage("Mã phiên gửi đơn không hợp lệ.");

        RuleForEach(x => x.CampusProcessing).ChildRules(p =>
        {
            p.RuleFor(c => c.CampusId)
                .NotEmpty().WithMessage("Thiếu cơ sở cho lựa chọn xử lý.");

            p.RuleFor(c => c.Mode)
                .NotEmpty()
                .Must(m => KnownModes.Contains(m))
                .WithMessage("Chế độ xử lý cơ sở không hợp lệ.")
                .WithErrorCode(VisitRequestErrorCodes.InvalidCampusSubmissionMode);

            p.RuleFor(c => c.HostUserId)
                .NotNull()
                .When(c => c.Mode == CampusSubmissionModes.AssignHost)
                .WithMessage("Chế độ gán host phải chọn host cụ thể.");

            p.RuleFor(c => c.HostUserId)
                .Null()
                .When(c => c.Mode == CampusSubmissionModes.SendForReview)
                .WithMessage("Chế độ gửi duyệt không được kèm host.");
        });

        RuleFor(x => x.CampusProcessing)
            .Must((cmd, processing) =>
            {
                if (processing is null || processing.Count == 0) return true;
                var selected = cmd.CampusVisits
                    .Select(s => s.CampusId?.Trim().ToUpperInvariant())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToHashSet();
                return processing.All(p =>
                    !string.IsNullOrWhiteSpace(p.CampusId)
                    && selected.Contains(p.CampusId.Trim().ToUpperInvariant()));
            })
            .WithMessage("Lựa chọn xử lý tham chiếu cơ sở không nằm trong danh sách cơ sở đã chọn.")
            .WithErrorCode(VisitRequestErrorCodes.DirectModeCampusNotSelected);

        RuleFor(x => x.CampusProcessing)
            .Must(processing =>
            {
                if (processing is null) return true;
                var codes = processing
                    .Where(p => !string.IsNullOrWhiteSpace(p.CampusId))
                    .Select(p => p.CampusId.Trim().ToUpperInvariant())
                    .ToList();
                return codes.Count == codes.Distinct().Count();
            })
            .WithMessage("Không được khai báo trùng lựa chọn xử lý cho cùng một cơ sở.");
    }
}
