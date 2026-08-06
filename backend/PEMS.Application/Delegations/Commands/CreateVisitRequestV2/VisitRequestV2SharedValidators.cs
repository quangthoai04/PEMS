using FluentValidation;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Validation;
using static PEMS.Application.Delegations.Commands.CreateVisitRequestV2.LengthMessages;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// One wording for "this field is too long", so the API never falls back to FluentValidation's
/// English default next to a Vietnamese required-message on the same field.
/// </summary>
internal static class LengthMessages
{
    public static string TooLong(string field, int max) => $"{field} tối đa {max} ký tự.";
}

/// <summary>
/// The request-level registrant rules, shared by create-v2, pending-edit-v2 and resubmit-v2 the same
/// way <see cref="CampusVisitFormDtoValidator"/> is shared for per-campus content. Extracted so the
/// three write paths cannot drift: before this, edit and resubmit only null-checked the registrant,
/// so a direct edit call could blank a job title or store "090abc123" as a phone.
/// </summary>
public sealed class RegistrantInputV2Validator : AbstractValidator<RegistrantInputV2>
{
    public RegistrantInputV2Validator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên người đăng ký không được để trống.")
            .MaximumLength(150).WithMessage(TooLong("Họ tên người đăng ký", 150));
        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Đơn vị công tác không được để trống.")
            .MaximumLength(200).WithMessage(TooLong("Đơn vị công tác người đăng ký", 200));
        RuleFor(x => x.JobTitle)
            .NotEmpty().WithMessage("Chức vụ người đăng ký không được để trống.")
            .MaximumLength(150).WithMessage(TooLong("Chức vụ người đăng ký", 150));
        RuleFor(x => x.Nationality)
            .NotEmpty().WithMessage("Quốc tịch người đăng ký không được để trống.")
            .MaximumLength(100).WithMessage(TooLong("Quốc tịch người đăng ký", 100));
        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage(TooLong("Số điện thoại người đăng ký", 50))
            .MustBeAPhoneNumber("Số điện thoại người đăng ký không hợp lệ.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email người đăng ký không được để trống.")
            .EmailAddress().WithMessage("Email người đăng ký không đúng định dạng.")
            .MaximumLength(150).WithMessage(TooLong("Email người đăng ký", 150));
    }
}

/// <summary>
/// The per-campus operational contact — the person a campus actually calls on the day, and the only
/// contact a request has. Name, organization, job title and email are required and the phone is
/// optional; shared by create-v2, pending-edit-v2, resubmit-v2 and the amendment path, so the write
/// paths cannot drift.
///
/// <para>
/// The email matters most: it is the ONLY address the confirmation invitation for this campus is ever
/// bound to, and the column behind it is NOT NULL. The name and phone are a display snapshot — a
/// matching name is never evidence of the same person, and runtime authority is read from
/// <c>operational_contact_user_id</c> rather than from anything validated here.
/// </para>
/// </summary>
public sealed class OperationalContactV2Validator : AbstractValidator<ContactPointDto>
{
    public OperationalContactV2Validator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối phối hợp không được để trống.")
            .MaximumLength(150).WithMessage(TooLong("Họ tên đầu mối phối hợp", 150));
        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Đơn vị công tác đầu mối phối hợp không được để trống.")
            .MaximumLength(200).WithMessage(TooLong("Đơn vị công tác đầu mối phối hợp", 200));
        RuleFor(x => x.JobTitle)
            .NotEmpty().WithMessage("Chức vụ đầu mối phối hợp không được để trống.")
            .MaximumLength(150).WithMessage(TooLong("Chức vụ đầu mối phối hợp", 150));
        // Phone is OPTIONAL — blank submits. MustBeAPhoneNumber passes blank on purpose, and blank
        // normalizes to NULL on the way to a nullable column.
        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage(TooLong("Số điện thoại đầu mối phối hợp", 50))
            .MustBeAPhoneNumber("Số điện thoại đầu mối phối hợp không hợp lệ.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email đầu mối phối hợp không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối phối hợp không đúng định dạng.")
            .MaximumLength(150).WithMessage(TooLong("Email đầu mối phối hợp", 150));
    }
}

/// <summary>One guest row — every column required, shared by create-v2 and the amendment path.</summary>
public sealed class VisitorV2Validator : AbstractValidator<VisitorDto>
{
    public VisitorV2Validator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Họ tên khách không được để trống.")
            .MaximumLength(150).WithMessage(TooLong("Họ tên khách", 150));
        RuleFor(x => x.Nationality).NotEmpty().WithMessage("Quốc tịch khách không được để trống.")
            .MaximumLength(100).WithMessage(TooLong("Quốc tịch khách", 100));
        RuleFor(x => x.Organization).NotEmpty().WithMessage("Đơn vị công tác khách không được để trống.")
            .MaximumLength(200).WithMessage(TooLong("Đơn vị công tác khách", 200));
        RuleFor(x => x.JobTitle).NotEmpty().WithMessage("Chức vụ khách không được để trống.")
            .MaximumLength(150).WithMessage(TooLong("Chức vụ khách", 150));
    }
}

/// <summary>
/// One support-team row. The list may be EMPTY, but a row that EXISTS must be complete — the
/// visit_guest_members columns are NOT NULL, so a half-filled row is a failed insert, not a message.
/// Shared by create-v2 and the amendment path.
/// </summary>
public sealed class SupportTeamMemberV2Validator : AbstractValidator<SupportTeamMemberDto>
{
    public SupportTeamMemberV2Validator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Họ tên nhân sự hỗ trợ không được để trống.")
            .MaximumLength(150).WithMessage(TooLong("Họ tên nhân sự hỗ trợ", 150));
        RuleFor(x => x.JobTitle).NotEmpty().WithMessage("Chức vụ nhân sự hỗ trợ không được để trống.")
            .MaximumLength(150).WithMessage(TooLong("Chức vụ nhân sự hỗ trợ", 150));
        RuleFor(x => x.Organization).NotEmpty().WithMessage("Đơn vị công tác nhân sự hỗ trợ không được để trống.")
            .MaximumLength(200).WithMessage(TooLong("Đơn vị công tác nhân sự hỗ trợ", 200));
        RuleFor(x => x.Nationality).NotEmpty().WithMessage("Quốc tịch nhân sự hỗ trợ không được để trống.")
            .MaximumLength(100).WithMessage(TooLong("Quốc tịch nhân sự hỗ trợ", 100));
    }
}
