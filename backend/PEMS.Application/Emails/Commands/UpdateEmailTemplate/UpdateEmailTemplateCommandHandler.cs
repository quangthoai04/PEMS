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
using PEMS.Application.Emails.Contact;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

/// <summary>
/// Saves an operator's edit to a system template — its content AND its contact configuration — in one
/// transaction, and refuses everything else.
///
/// <para>
/// The previous implementation assigned twelve request properties onto the entity with no checks at
/// all — among them <c>Purpose</c>, <c>CampusId</c>, <c>Status</c>, <c>BodyFormat</c> and
/// <c>VariablesText</c>. Since <c>variables_text</c> is what the renderer validates a send against, an
/// operator could widen a template's own contract and then write placeholders no caller supplies: the
/// template would save cleanly and every send of it would fail afterwards. Those properties no longer
/// exist on the command, and the checks below cover what remains.
/// </para>
/// <para>
/// <b>Why the contact settings moved in here.</b> They were a separate endpoint with a separate
/// <c>SaveChangesAsync</c>, so a screen that changed both had to make two calls that could not be made
/// atomic from the client: the second failing left the first written, and the pair could be left
/// contradicting each other — a body carrying the contact block under a policy that says NONE, or a policy
/// that says REQUIRED over a body that no longer has anywhere to put the card. Worse, neither half could
/// accept a change to both at once, because each judged the incoming half against the other half as
/// STORED. Removing the block and switching to NONE is refused by the settings endpoint (the stored body
/// still has the block) and switching to REQUIRED while adding the block is refused by the content
/// endpoint (the stored policy still says OPTIONAL). The only way out was to save in a particular order
/// and hope; here both halves are validated against the values that will actually be stored.
/// </para>
/// </summary>
public sealed class UpdateEmailTemplateCommandHandler
    : IRequestHandler<UpdateEmailTemplateCommand, UpdateEmailTemplateResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailContactPolicyStore _contactPolicies;
    private readonly IMediator _mediator;

    public UpdateEmailTemplateCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IEmailContactPolicyStore contactPolicies,
        IMediator mediator)
    {
        _context = context;
        _currentUser = currentUser;
        _contactPolicies = contactPolicies;
        _mediator = mediator;
    }

    public async Task<UpdateEmailTemplateResponse> Handle(
        UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        // AsNoTracking deliberately: the content write is a conditional UPDATE issued by
        // EmailTemplateContentWriter, so a tracked entity would only offer a second, unconditional path
        // to the same row if some later edit added a stray SaveChangesAsync.
        var template = await _context.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmailTemplateId == request.EmailTemplateId, cancellationToken);

        if (template == null)
            throw new NotFoundException(nameof(PEMS.Domain.Entities.Emails.EmailTemplate), request.EmailTemplateId);

        var code = template.TemplateCode;
        var capability = EmailContactCapabilities.For(code);

        // ── 1. Which contact requirement this save is judged against ─────────
        // The INCOMING one when the request carries settings, the STORED one when it does not. This is the
        // whole reason the two saves had to merge: judging a body against the stored policy while the same
        // request is changing that policy refuses the one edit that fixes both halves at once.
        var storedRequirement = await EffectiveContactRequirement
            .ResolveAsync(_contactPolicies, code, cancellationToken);

        EmailContactSettingsInput? contactInput = null;

        if (request.ContactSettings is { } incoming)
        {
            // Parsed BEFORE the content is validated, because an unparseable requirement has no answer to
            // "may this body keep the block" — and guessing one would report the body as the fault.
            contactInput = EmailContactSettingsValidator.Parse(
                incoming.Requirement, incoming.ContactSource,
                incoming.ShowEmail, incoming.ShowPhone, incoming.ShowDepartment,
                incoming.ShowCampus, incoming.ShowSender,
                incoming.HeadingVi, incoming.HeadingEn, incoming.ReplyToSource);
        }

        var effectiveRequirement = capability.Supported
            ? contactInput?.Requirement ?? storedRequirement
            : EmailContactRequirement.NONE;

        // ── 2. Only a registered system template is editable ─────────────────
        // A row whose code is not in the registry is historical: it survives because a sent email or a
        // draft still points at it, and nothing in any release sends it. Editing it would change a
        // message that can never go out again, so it is refused rather than quietly accepted.
        var contract = EmailTemplateContracts.For(code, effectiveRequirement);
        if (contract is null)
        {
            throw new ConflictException(
                $"Mẫu {code} không thuộc danh mục mẫu hệ thống nên không thể chỉnh sửa. " +
                "Bản ghi này được giữ lại vì lịch sử email hoặc bản nháp còn tham chiếu đến nó.",
                EmailErrorCodes.TemplateCatalogFixed);
        }

        // ── 3. A concurrency token must be present ───────────────────────────
        // Only presence is checked here; whether it MATCHES is decided by the database inside the write
        // statement, because any comparison made here would leave a window between the check and the
        // write in which another request can land.
        if (request.ExpectedRevision is null)
        {
            throw new ConflictException(
                "Không xác định được phiên bản mẫu email đang sửa. Vui lòng mở lại mẫu rồi thử lại.",
                EmailErrorCodes.TemplateConcurrencyConflict);
        }

        // ── 4. Content against the one contract ──────────────────────────────
        // The same call the editor makes to render its field-level warnings, so a save can neither
        // succeed on content the screen flagged nor fail on content it called clean. Now judged against
        // the requirement resolved above, which is what makes "NONE + block still in the body" a refusal
        // here rather than a silently-blanked placeholder at send time.
        var issues = EmailTemplateContentValidator.Validate(
            contract, request.SubjectVi, request.BodyVi, request.SubjectEn, request.BodyEn);

        if (issues.Any(i => i.IsError))
            throw new EmailTemplateContentException(issues);

        // ── 5. Contact settings against the bodies being written ─────────────
        // Not against the stored ones. The pair validated here is the pair that will exist after the
        // commit, so the two halves cannot be left disagreeing by this save.
        if (contactInput is not null)
            EmailContactSettingsValidator.Validate(code, contactInput, request.BodyVi, request.BodyEn);

        // ── 6. Write the whitelist, and nothing else, conditionally ──────────
        // variables_text is a PROJECTION of the registry, not an operator-editable field. Rewriting it
        // from the contract keeps the column from drifting away from what the renderer enforces — the
        // drift that used to reach recipients as the literal text "Chưa có thông tin".
        // No trusted block is listed. variables_text is the operator-facing list of things they may
        // supply, and a block is the backend's — writing one here would advertise a field nobody may
        // fill in, and 03_verify.sql's E4 check refuses a catalog that does it.
        var variablesText = string.Join(",", contract.AllowedVariables
            .Where(v => !EmailTrustedBlocks.All.Contains(v)));

        // One transaction over both writes. The content write is raw SQL and the policy write goes through
        // the change tracker; they enlist in the same ambient transaction, so a failure in either — a
        // stale revision, a constraint, a cancelled request — rolls back both and leaves the revision
        // where it was.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var written = await EmailTemplateContentWriter.WriteAsync(
            _context,
            template.EmailTemplateId,
            request.ExpectedRevision.Value,
            new EmailTemplateContentWrite(
                request.Name, request.Description,
                request.SubjectVi, request.BodyVi, request.SubjectEn, request.BodyEn),
            variablesText,
            _currentUser.UserId,
            cancellationToken);

        if (contactInput is not null)
        {
            await EmailContactPolicyWriter.ApplyAsync(
                _context, code, contactInput, _currentUser.UserId, written.UpdatedAt, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        // Read back AFTER the commit, so the snapshot the editor re-baselines from is what the database
        // holds rather than what this handler believes it wrote.
        var contactSettings = capability.Supported
            ? await _mediator.Send(new GetEmailContactSettingsQuery { TemplateCode = code }, cancellationToken)
            : null;

        return new UpdateEmailTemplateResponse
        {
            EmailTemplateId = template.EmailTemplateId,
            TemplateCode = code,
            Success = true,
            Message = contactInput is null
                ? "Đã cập nhật nội dung mẫu email."
                : "Đã lưu nội dung mẫu và cấu hình thông tin liên hệ.",
            Revision = written.Revision,
            UpdatedAt = written.UpdatedAt,
            Name = request.Name,
            // Reported as the writer STORES them, not as they arrived. The five nullable columns are
            // written through NULLIF(…, ''), so an empty string becomes NULL — echoing '' back would give
            // the editor a baseline that a later reload disagrees with, and the screen would claim an
            // unsaved change on a field nobody had touched.
            Description = NullIfEmpty(request.Description),
            SubjectVi = NullIfEmpty(request.SubjectVi),
            BodyVi = NullIfEmpty(request.BodyVi),
            SubjectEn = NullIfEmpty(request.SubjectEn),
            BodyEn = NullIfEmpty(request.BodyEn),
            ContactSettings = contactSettings,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
