using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Saves the template-level contact settings.
///
/// <para>
/// Everything here is an enum or a boolean except the two headings, and that is the security design
/// rather than a simplification: an operator chooses WHICH fields the block shows, never what it
/// contains. There is no field for an address, a telephone number or a user id, so a template cannot be
/// made to present a hand-typed mailbox as the Host's — the values are read from <c>users</c>,
/// <c>campuses</c> and <c>departments</c> when the mail is sent. The headings are text, so they are
/// length-capped and stripped of markup on the way in.
/// </para>
/// <para>
/// <b>Not used by the template editor any more.</b> That screen saves content and contact settings through
/// one atomic <c>PUT /api/email-templates/{id}</c>, because saving them separately meant a body and a
/// policy could be left contradicting each other by a failure between the two calls. This endpoint stays
/// because it is a live, tested API route that changes the policy WITHOUT touching content — a legitimate
/// operation, and one whose removal would be a breaking change made for tidiness. It applies exactly the
/// same rules, via <see cref="EmailContactSettingsValidator"/>, judged against the STORED bodies since it
/// is not changing them.
/// </para>
/// </summary>
public sealed class UpdateEmailContactSettingsCommand : IRequest<EmailContactSettingsDto>
{
    public string TemplateCode { get; set; } = string.Empty;

    public string Requirement { get; set; } = nameof(EmailContactRequirement.OPTIONAL);
    public string ContactSource { get; set; } = nameof(EmailContactSource.SUPPORT_CONTACT);

    public bool ShowEmail { get; set; } = true;
    public bool ShowPhone { get; set; } = true;
    public bool ShowDepartment { get; set; }
    public bool ShowCampus { get; set; }
    public bool ShowSender { get; set; }

    public string? HeadingVi { get; set; }
    public string? HeadingEn { get; set; }

    public string ReplyToSource { get; set; } = nameof(EmailReplyToSource.NONE);
}

public sealed class UpdateEmailContactSettingsCommandHandler
    : IRequestHandler<UpdateEmailContactSettingsCommand, EmailContactSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public UpdateEmailContactSettingsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IMediator mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<EmailContactSettingsDto> Handle(
        UpdateEmailContactSettingsCommand request, CancellationToken cancellationToken)
    {
        var code = request.TemplateCode?.Trim() ?? string.Empty;

        _ = SystemEmailTemplates.Find(code)
            ?? throw new NotFoundException(
                $"Mã template email '{code}' không nằm trong danh mục hệ thống.",
                EmailErrorCodes.TemplateNotFound);

        var input = EmailContactSettingsValidator.Parse(
            request.Requirement, request.ContactSource,
            request.ShowEmail, request.ShowPhone, request.ShowDepartment,
            request.ShowCampus, request.ShowSender,
            request.HeadingVi, request.HeadingEn, request.ReplyToSource);

        // Judged against the bodies as STORED, because this endpoint does not change them. The combined
        // template save passes the bodies it is about to write instead — same validator, different pair.
        var bodies = await _db.EmailTemplates
            .AsNoTracking()
            .Where(t => t.TemplateCode == code)
            .Select(t => new { t.BodyVi, t.BodyEn })
            .FirstOrDefaultAsync(cancellationToken);

        EmailContactSettingsValidator.Validate(code, input, bodies?.BodyVi, bodies?.BodyEn);

        await EmailContactPolicyWriter.ApplyAsync(
            _db, code, input, _currentUser.UserId, VietnamTime.Now(), cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(
            new GetEmailContactSettingsQuery { TemplateCode = code }, cancellationToken);
    }
}
