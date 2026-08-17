using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Validation;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;
using static PEMS.Application.Delegations.Commands.CreateVisitRequestV2.LengthMessages;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// STRUCTURAL (payload-shape) validation for authenticated create-v2. Runs in the MediatR
/// <c>ValidationBehaviour</c> pipeline BEFORE the handler, so a malformed payload never reaches the
/// service and never opens a transaction. This is a boundary guard only — <see cref="PEMS.Infrastructure.Services.VisitRequestV2CreateService"/>
/// STILL revalidates every DB/clock-dependent rule (campus existence/ACTIVE, Staff-Leader routing,
/// not-in-the-past, partner, min-duration) inside the transaction; the validator never replaces it.
///
/// Every create carries a per-campus payload: one entry per campus, each with its own form content, which
/// the service turns into that campus's detail row. There is no schema discriminator and no alternative
/// request-level shape to choose between.
///
/// System-derived fields (visitScope, hasMixedCampusDetails, status/revision, coordinator/approval state,
/// visitorUserId) are NOT part of <see cref="VisitRequestFormDataV2"/>, so "the client cannot send them"
/// is enforced by the DTO shape itself — there is nothing to reject here.
/// </summary>
public sealed class CreateVisitRequestV2CommandValidator : AbstractValidator<CreateVisitRequestV2Command>
{
    public CreateVisitRequestV2CommandValidator()
    {
        RuleFor(x => x.Form).NotNull().WithMessage("Thiếu dữ liệu biểu mẫu.");
        RuleFor(x => x.Form).SetValidator(new VisitRequestFormDataV2Validator()!).When(x => x.Form is not null);
    }
}

/// <summary>Structural rules for the whole v2 payload (request-level fields + the campus collection).</summary>
public sealed class VisitRequestFormDataV2Validator : AbstractValidator<VisitRequestFormDataV2>
{
    // Structural collection ceilings — defence-in-depth against oversized payloads; the DB has its own
    // per-request campus uniqueness. Kept generous so legitimate delegations are never blocked.
    private const int MaxCampuses = 10;

    public VisitRequestFormDataV2Validator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Thiếu submissionId.")
            .MaximumLength(100);

        // ── Registrant — the SAME shared child validator used by pending-edit-v2 and resubmit-v2,
        //    so the three write paths cannot drift. The contact is validated per campus below:
        //    there is no request-level one to check here. ──
        RuleFor(x => x.Registrant).NotNull().WithMessage("Thiếu thông tin người đăng ký.");
        RuleFor(x => x.Registrant!).SetValidator(new RegistrantInputV2Validator()).When(x => x.Registrant is not null);

        // ── Campus collection ──
        RuleFor(x => x.CampusVisits)
            .NotEmpty().WithMessage("Phải có ít nhất 1 cơ sở.");
        RuleFor(x => x.CampusVisits)
            .Must(cs => cs is null || cs.Count <= MaxCampuses)
            .WithMessage($"Không được vượt quá {MaxCampuses} cơ sở trong một đơn.");
        RuleFor(x => x.CampusVisits)
            .Must(NoDuplicateCampus).WithMessage("Không được chọn trùng cơ sở.")
            .When(x => x.CampusVisits is { Count: > 0 });

        RuleForEach(x => x.CampusVisits).SetValidator(new CampusVisitFormDtoValidator());
    }

    private static bool NoDuplicateCampus(IList<CampusVisitFormDto>? campuses)
    {
        if (campuses is null) return true;
        var codes = campuses
            .Where(c => !string.IsNullOrWhiteSpace(c.CampusId))
            .Select(c => c.CampusId.Trim().ToUpperInvariant())
            .ToList();
        return codes.Count == codes.Distinct().Count();
    }
}

/// <summary>Structural rules for ONE fully-resolved campus snapshot (v2 sends every campus independently).</summary>
public sealed class CampusVisitFormDtoValidator : AbstractValidator<CampusVisitFormDto>
{
    // Must match VisitRequestV2CreateService.MinDurationMinutes — the validator is not stricter than the service.
    private const int MinDurationMinutes = 30;
    private const int MaxMembers = 200;

    private static readonly HashSet<string> AllowedVisitTypes = new(StringComparer.Ordinal)
    {
        "CAMPUS_TOUR", "MEETING", "WORKSHOP", "SIGNING_CEREMONY", "EXCHANGE", "OTHER",
    };

    /// <param name="requireCompleteOperationalContact">
    /// True (the default, and always true for create) for a campus whose operational contact is a
    /// FRESH write — completeness is enforced via <see cref="OperationalContactV2Validator"/>, same as
    /// every other create-time field. False for an EXISTING campus being replayed through an edit,
    /// resubmit or amendment payload — its contact is read-only there, so only
    /// <see cref="OperationalContactReplayV2Validator"/>'s shape/length rules apply, and the caller is
    /// responsible for enforcing that the snapshot did not actually change (see that validator's doc
    /// comment). The caller decides which applies per campus, from whether it already has a
    /// VisitInstanceId — that field does not survive the projection into this DTO, so it cannot be
    /// decided in here.
    /// </param>
    public CampusVisitFormDtoValidator(bool requireCompleteOperationalContact = true)
    {
        RuleFor(c => c.CampusId).NotEmpty().WithMessage("Vui lòng chọn cơ sở.").MaximumLength(50);

        // Clock-independent schedule shape only; the service revalidates "not in the past" against VietnamNow.
        RuleFor(c => c)
            .Must(c => c.PlannedEndAt > c.PlannedStartAt)
            .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");
        RuleFor(c => c)
            .Must(c => (c.PlannedEndAt - c.PlannedStartAt).TotalMinutes >= MinDurationMinutes)
            .WithMessage("Mỗi buổi thăm phải kéo dài tối thiểu 30 phút.");

        // ── Per-campus visit content ──
        RuleFor(c => c.DelegationName)
            .NotEmpty().WithMessage("Tên đoàn không được để trống.")
            .MaximumLength(200).WithMessage(TooLong("Tên đoàn", 200));
        RuleFor(c => c.VisitType)
            .NotEmpty().WithMessage("Loại hình tham quan không được để trống.")
            .Must(t => AllowedVisitTypes.Contains(t)).WithMessage("Loại hình tham quan không hợp lệ.");
        RuleFor(c => c.VisitTypeOther)
            .NotEmpty().WithMessage("Vui lòng ghi rõ loại hình tham quan khác.")
            .MaximumLength(200).WithMessage(TooLong("Loại hình tham quan khác", 200))
            .When(c => c.VisitType == "OTHER");
        RuleFor(c => c.Purpose)
            .NotEmpty().WithMessage("Mục đích thăm không được để trống.")
            .MaximumLength(2000).WithMessage(TooLong("Mục đích thăm", 2000));
        RuleFor(c => c.WorkingContent)
            .NotEmpty().WithMessage("Nội dung làm việc không được để trống.")
            .MaximumLength(4000).WithMessage(TooLong("Nội dung làm việc", 4000));

        // ── Per-campus operational (working) contact — a snapshot, never a login ──
        RuleFor(c => c.OperationalContact).NotNull().WithMessage("Thiếu đầu mối phối hợp của cơ sở.");
        RuleFor(c => c.OperationalContact!)
            .SetValidator(requireCompleteOperationalContact
                ? new OperationalContactV2Validator()
                : new OperationalContactReplayV2Validator())
            .When(c => c.OperationalContact is not null);

        // ── Additional per-campus requirements ──
        RuleFor(c => c.WorkingLanguage)
            .Must(l => l is "EN" or "VI").WithMessage("Ngôn ngữ làm việc phải là EN hoặc VI.");
        RuleFor(c => c.TransportationNote)
            .MaximumLength(2000).WithMessage(TooLong("Nhận diện phương tiện di chuyển", 2000))
            .Must(note => string.IsNullOrEmpty(note) || (!note.Contains('<') && !note.Contains('>')))
            .WithMessage("Nhận diện phương tiện di chuyển không được chứa HTML/script.");
        RuleFor(c => c.MediaConsentStatus)
            .NotEmpty().WithMessage("Trạng thái truyền thông không được để trống.")
            .Must(s => s is "AGREED" or "DECLINED").WithMessage("Trạng thái truyền thông không hợp lệ.");
        // "Ghi chú gửi FPTU" — the guest's one general remark about this campus. Deliberately NOT
        // conditioned on MediaConsentStatus: it is not a justification for the consent answer, so all
        // four combinations (AGREED/DECLINED × note/no note) are valid. Blank is normalized to NULL
        // at the write boundary, so a whitespace-only note never reaches the column as content.
        RuleFor(c => c.Notes)
            .MaximumLength(2000).WithMessage(TooLong("Ghi chú gửi FPTU", 2000));

        // ── People (per-campus, independent) ──
        // A campus with nobody coming is not a visit. The form has always required at least one guest,
        // but the server only null-checked the list, so a direct call — or an edit/resubmit, which run
        // through this same validator — could store a campus whose delegation is empty, and the detail
        // screen then showed a delegation of 0 people. Support members stay optional (below): they are
        // an FPTU-side convenience, not the reason the campus is receiving anyone.
        RuleFor(c => c.Visitors)
            .NotNull().WithMessage("Danh sách khách không hợp lệ.")
            .NotEmpty().WithMessage("Mỗi cơ sở phải có ít nhất 1 khách.")
            .Must(v => v is null || v.Count <= MaxMembers).WithMessage($"Tối đa {MaxMembers} khách mỗi cơ sở.");
        RuleForEach(c => c.Visitors).SetValidator(new VisitorV2Validator());

        RuleFor(c => c.ExternalSupportMembers)
            .NotNull().WithMessage("Danh sách nhân sự hỗ trợ không hợp lệ.")
            .Must(s => s is null || s.Count <= MaxMembers).WithMessage($"Tối đa {MaxMembers} nhân sự hỗ trợ mỗi cơ sở.");
        RuleForEach(c => c.ExternalSupportMembers).SetValidator(new SupportTeamMemberV2Validator());

        // ── "Đầu mối là ai trong đoàn?" — the member key (NP-03) ──
        // Structural half of the rule only: the payload must NAME exactly one of its own rows. Whether
        // that row may hold the role, and which guest_member_id it becomes, is decided inside the
        // transaction by OperationalContactLink — the ids do not exist yet at this point.
        //
        // Checked here as well as there because this runs before a transaction is opened: a payload
        // whose keys are duplicated or whose contact names nobody is a client fault, and telling it so
        // without touching the database is both cheaper and clearer than a rolled-back write.
        RuleFor(c => c)
            .Must(c => MemberKeysAreDistinct(c))
            .WithMessage("Danh sách thành viên có định danh trùng nhau. Vui lòng tải lại biểu mẫu.");
        RuleFor(c => c)
            .Must(c => ContactKeyNamesAMember(c))
            .WithMessage(OperationalContactMessages.MemberNotInDelegation)
            .When(c => !string.IsNullOrWhiteSpace(c.OperationalContactClientMemberKey));

        // ── One person, one member row — across BOTH lists (ID-02) ──
        // Guests and support staff are two doors into `visit_guest_members`, and until now nothing
        // compared them: the importer de-duplicated inside the list it was importing and each array
        // was validated on its own, so the same human written into both was stored as two rows with
        // two different guest_member_ids. Every id-first rule downstream then correctly read that as
        // two people — which is how the biên bản ended up listing somebody twice with no way to tell
        // that the two rows were one person. Validating the MERGED list is the only place that can
        // see it. The client applies the same rule and asks first; this is the layer that does not
        // depend on the client having done so.
        RuleFor(c => c)
            .Must(c => MemberDuplicatePolicy.FindDuplicates(MemberIdentitiesOf(c)).Count == 0)
            .WithMessage(c => MemberDuplicatePolicy.DescribeConflicts(
                MemberDuplicatePolicy.FindDuplicates(MemberIdentitiesOf(c))))
            .WithErrorCode(MemberDuplicatePolicy.DuplicateCode);
    }

    /// <summary>
    /// One campus's members as the duplicate check sees them — visitors then support, in payload
    /// order, so a reported "#2" is the row the user is looking at.
    /// </summary>
    private static IEnumerable<MemberIdentityInput> MemberIdentitiesOf(CampusVisitFormDto c) =>
        (c.Visitors ?? new List<VisitorDto>())
            .Select((v, i) => new MemberIdentityInput(
                MemberDuplicatePolicy.GuestKind, i, v.FullName, v.JobTitle, v.Organization,
                v.OrganizationPartnerId, v.Nationality))
            .Concat((c.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
                .Select((m, i) => new MemberIdentityInput(
                    MemberDuplicatePolicy.SupportKind, i, m.FullName, m.JobTitle, m.Organization,
                    m.OrganizationPartnerId, m.Nationality)));

    /// <summary>Every non-empty member key in ONE campus, visitors and support together.</summary>
    private static IEnumerable<string> MemberKeysOf(CampusVisitFormDto c) =>
        (c.Visitors ?? new List<VisitorDto>()).Select(v => v.ClientMemberKey)
            .Concat((c.ExternalSupportMembers ?? new List<SupportTeamMemberDto>()).Select(m => m.ClientMemberKey))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!);

    /// <summary>
    /// A key that appears twice is not an identity, and the contact would resolve to whichever row was
    /// enumerated first — the exact silent mis-aiming this replaced. Scope is the CAMPUS, because that
    /// is where members live: two campuses of one request keep their own independent copies of the same
    /// people, and requiring keys to be unique across the request would refuse an ordinary "áp dụng cho
    /// các cơ sở còn lại".
    /// </summary>
    private static bool MemberKeysAreDistinct(CampusVisitFormDto c)
    {
        var keys = MemberKeysOf(c).ToList();
        return keys.Count == keys.Distinct(StringComparer.Ordinal).Count();
    }

    private static bool ContactKeyNamesAMember(CampusVisitFormDto c) =>
        MemberKeysOf(c).Count(k => string.Equals(k, c.OperationalContactClientMemberKey, StringComparison.Ordinal)) == 1;
}
