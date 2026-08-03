using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Emails.Commands.RestoreEmailTemplate;

/// <summary>
/// Restores one template's six operator-editable fields to the shipped defaults, in one conditional
/// write, with an audit row recording what was replaced.
/// </summary>
public sealed class RestoreEmailTemplateCommandHandler
    : IRequestHandler<RestoreEmailTemplateCommand, RestoreEmailTemplateResponse>
{
    /// <summary>The audit action name; asserted by tests so it cannot be renamed by accident.</summary>
    public const string AuditAction = "RESTORE_EMAIL_TEMPLATE_DEFAULT";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMS.Application.Emails.Contact.IEmailContactPolicyStore _contactPolicies;

    public RestoreEmailTemplateCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        PEMS.Application.Emails.Contact.IEmailContactPolicyStore contactPolicies)
    {
        _context = context;
        _currentUser = currentUser;
        _contactPolicies = contactPolicies;
    }

    public async Task<RestoreEmailTemplateResponse> Handle(
        RestoreEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmailTemplateId == request.EmailTemplateId, cancellationToken);

        if (template == null)
            throw new NotFoundException(nameof(PEMS.Domain.Entities.Emails.EmailTemplate), request.EmailTemplateId);

        // ── 1. Only a registered system template can be restored ─────────────
        // Against the CONFIGURED contact requirement: restoring the shipped CONTENT must not be refused
        // because the shipped POLICY says something the operator has since changed. The two are restored
        // by two different buttons on purpose (§10), and this one does not touch the policy.
        var contactRequirement = await PEMS.Application.Emails.Contact.EffectiveContactRequirement
            .ResolveAsync(_contactPolicies, template.TemplateCode, cancellationToken);

        var contract = EmailTemplateContracts.For(template.TemplateCode, contactRequirement);
        if (contract is null)
        {
            throw new ConflictException(
                $"Mẫu {template.TemplateCode} không thuộc danh mục mẫu hệ thống nên không có nội dung mặc định để phục hồi.",
                EmailErrorCodes.TemplateCatalogFixed);
        }

        // A row outside ACTIVE is legacy: it survives because history points at it, and no release sends
        // it. Rewriting its content would change a message that can never go out again, so restore stops
        // here rather than quietly "fixing" a row nobody reads.
        if (!string.Equals(template.Status, "ACTIVE", StringComparison.Ordinal))
        {
            throw new ConflictException(
                $"Mẫu {template.TemplateCode} đang ở trạng thái {template.Status} nên không thể phục hồi nội dung mặc định.",
                EmailErrorCodes.TemplateCatalogFixed);
        }

        // ── 2. The shipped default must exist ────────────────────────────────
        var shipped = EmailTemplateDefaults.For(template.TemplateCode);
        if (shipped is null)
        {
            throw new ConflictException(
                $"Không có nội dung mặc định được ghi nhận cho mẫu {template.TemplateCode}.",
                EmailErrorCodes.TemplateDefaultUnavailable);
        }

        if (request.ExpectedRevision is null)
        {
            throw new ConflictException(
                "Không xác định được phiên bản mẫu email đang phục hồi. Vui lòng mở lại mẫu rồi thử lại.",
                EmailErrorCodes.TemplateConcurrencyConflict);
        }

        // ── 3. The default is validated before it is written ─────────────────
        // Not ceremony: the defaults are extracted from the canonical seed, and if a seed edit ever
        // introduced a placeholder no caller supplies, restoring it would hand the operator a template
        // that saves cleanly and then fails every send. Better to refuse the restore and say which
        // variable is wrong than to write known-broken content back over their work.
        var issues = EmailTemplateContentValidator.Validate(
            contract, shipped.SubjectVi, shipped.BodyVi, shipped.SubjectEn, shipped.BodyEn);

        if (issues.Any(i => i.IsError))
            throw new EmailTemplateContentException(issues);

        // ── 4. Conditional write + audit, together or not at all ─────────────
        // No trusted block is listed — see UpdateEmailTemplateCommandHandler for why.
        var variablesText = string.Join(",", contract.AllowedVariables
            .Where(v => !EmailTrustedBlocks.All.Contains(v)));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var written = await EmailTemplateContentWriter.WriteAsync(
            _context,
            template.EmailTemplateId,
            request.ExpectedRevision.Value,
            new EmailTemplateContentWrite(
                shipped.Name, shipped.Description,
                shipped.SubjectVi, shipped.BodyVi, shipped.SubjectEn, shipped.BodyEn),
            variablesText,
            _currentUser.UserId,
            cancellationToken);

        // The replaced text is recorded, not just the fact of a restore: an operator who restores by
        // mistake needs their wording to still exist somewhere, and "content was reset" alone does not
        // give it back. Bodies can be long, so they are stored as one JSON document per side rather than
        // as six change rows.
        _context.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = template.CampusId,
            Action = AuditAction,
            EntityType = nameof(PEMS.Domain.Entities.Emails.EmailTemplate),
            EntityId = template.EmailTemplateId,
            Reason = $"Phục hồi nội dung mặc định cho mẫu {template.TemplateCode}",
            CreatedAt = written.UpdatedAt,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "Content",
                    ValueFormat = "JSON",
                    DisplayOrder = 0,
                    OldValueText = Snapshot(
                        template.Name, template.Description,
                        template.SubjectVi, template.BodyVi, template.SubjectEn, template.BodyEn,
                        template.Revision),
                    NewValueText = Snapshot(
                        shipped.Name, shipped.Description,
                        shipped.SubjectVi, shipped.BodyVi, shipped.SubjectEn, shipped.BodyEn,
                        written.Revision),
                    CreatedAt = written.UpdatedAt,
                },
            },
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RestoreEmailTemplateResponse
        {
            EmailTemplateId = template.EmailTemplateId,
            TemplateCode = template.TemplateCode,
            Success = true,
            Message = "Đã phục hồi nội dung mặc định của mẫu email.",
            Revision = written.Revision,
            UpdatedAt = written.UpdatedAt,
            Name = shipped.Name,
            Description = shipped.Description,
            SubjectVi = shipped.SubjectVi,
            BodyVi = shipped.BodyVi,
            SubjectEn = shipped.SubjectEn,
            BodyEn = shipped.BodyEn,
        };
    }

    private static string Snapshot(
        string name, string? description,
        string? subjectVi, string? bodyVi, string? subjectEn, string? bodyEn, uint revision)
        => JsonSerializer.Serialize(new
        {
            revision,
            name,
            description,
            subjectVi,
            bodyVi,
            subjectEn,
            bodyEn,
        });
}
