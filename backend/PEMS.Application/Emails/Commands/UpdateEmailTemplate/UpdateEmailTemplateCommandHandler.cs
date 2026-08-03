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

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

/// <summary>
/// Saves an operator's edit to a system template's content (UC-44), and refuses everything else.
///
/// <para>
/// The previous implementation assigned twelve request properties onto the entity with no checks at
/// all — among them <c>Purpose</c>, <c>CampusId</c>, <c>Status</c>, <c>BodyFormat</c> and
/// <c>VariablesText</c>. Since <c>variables_text</c> is what the renderer validates a send against, an
/// operator could widen a template's own contract and then write placeholders no caller supplies: the
/// template would save cleanly and every send of it would fail afterwards. Those properties no longer
/// exist on the command, and the four checks below cover what remains.
/// </para>
/// </summary>
public sealed class UpdateEmailTemplateCommandHandler
    : IRequestHandler<UpdateEmailTemplateCommand, UpdateEmailTemplateResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMS.Application.Emails.Contact.IEmailContactPolicyStore _contactPolicies;

    public UpdateEmailTemplateCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        PEMS.Application.Emails.Contact.IEmailContactPolicyStore contactPolicies)
    {
        _context = context;
        _currentUser = currentUser;
        _contactPolicies = contactPolicies;
    }

    public async Task<UpdateEmailTemplateResponse> Handle(
        UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        // AsNoTracking deliberately: the write is a conditional UPDATE issued by
        // EmailTemplateContentWriter, so a tracked entity would only offer a second, unconditional path
        // to the same row if some later edit added a stray SaveChangesAsync.
        var template = await _context.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmailTemplateId == request.EmailTemplateId, cancellationToken);

        if (template == null)
            throw new NotFoundException(nameof(PEMS.Domain.Entities.Emails.EmailTemplate), request.EmailTemplateId);

        // ── 1. Only a registered system template is editable ─────────────────
        // A row whose code is not in the registry is historical: it survives because a sent email or a
        // draft still points at it, and nothing in any release sends it. Editing it would change a
        // message that can never go out again, so it is refused rather than quietly accepted.
        // Judged against the CONFIGURED contact requirement, not the shipped one, so the save agrees with
        // what the editor was told and with what the send will do. Reading the shipped default here made
        // the two disagree in both directions on any template whose policy had been changed.
        var contactRequirement = await PEMS.Application.Emails.Contact.EffectiveContactRequirement
            .ResolveAsync(_contactPolicies, template.TemplateCode, cancellationToken);

        var contract = EmailTemplateContracts.For(template.TemplateCode, contactRequirement);
        if (contract is null)
        {
            throw new ConflictException(
                $"Mẫu {template.TemplateCode} không thuộc danh mục mẫu hệ thống nên không thể chỉnh sửa. " +
                "Bản ghi này được giữ lại vì lịch sử email hoặc bản nháp còn tham chiếu đến nó.",
                EmailErrorCodes.TemplateCatalogFixed);
        }

        // ── 2. A concurrency token must be present ───────────────────────────
        // Only presence is checked here; whether it MATCHES is decided by the database inside the write
        // statement, because any comparison made here would leave a window between the check and the
        // write in which another request can land.
        if (request.ExpectedRevision is null)
        {
            throw new ConflictException(
                "Không xác định được phiên bản mẫu email đang sửa. Vui lòng mở lại mẫu rồi thử lại.",
                EmailErrorCodes.TemplateConcurrencyConflict);
        }

        // ── 3. Content against the one contract ──────────────────────────────
        // The same call the editor makes to render its field-level warnings, so a save can neither
        // succeed on content the screen flagged nor fail on content it called clean.
        var issues = EmailTemplateContentValidator.Validate(
            contract, request.SubjectVi, request.BodyVi, request.SubjectEn, request.BodyEn);

        if (issues.Any(i => i.IsError))
            throw new EmailTemplateContentException(issues);

        // ── 4. Write the whitelist, and nothing else, conditionally ──────────
        // variables_text is a PROJECTION of the registry, not an operator-editable field. Rewriting it
        // from the contract keeps the column from drifting away from what the renderer enforces — the
        // drift that used to reach recipients as the literal text "Chưa có thông tin".
        // No trusted block is listed. variables_text is the operator-facing list of things they may
        // supply, and a block is the backend's — writing one here would advertise a field nobody may
        // fill in, and 03_verify.sql's E4 check refuses a catalog that does it.
        var variablesText = string.Join(",", contract.AllowedVariables
            .Where(v => !EmailTrustedBlocks.All.Contains(v)));

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

        return new UpdateEmailTemplateResponse
        {
            EmailTemplateId = template.EmailTemplateId,
            Success = true,
            Message = "Đã cập nhật nội dung mẫu email.",
            Revision = written.Revision,
            UpdatedAt = written.UpdatedAt,
        };
    }
}
