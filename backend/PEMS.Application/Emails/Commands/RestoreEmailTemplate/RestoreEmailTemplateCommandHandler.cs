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
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.RestoreEmailTemplate;

/// <summary>
/// Restores one template to the shipped defaults — its six operator-editable content fields AND its
/// contact configuration — in one transaction, with an audit row recording what was replaced.
///
/// <para>
/// <b>Why the contact policy is restored here too.</b> "Khôi phục mặc định" was two buttons, and the pair
/// could not be pressed atomically: restoring the content put the shipped body back (with or without the
/// contact placeholder, whichever the shipped wording has) while leaving a policy the operator had
/// changed, so a template could land in exactly the contradiction both halves refuse — a shipped body
/// carrying the block under a stored policy of NONE, or a shipped body without it under REQUIRED. Since
/// the shipped content and the shipped policy are consistent with each other by construction, restoring
/// them together is the only version of "restore" that always produces a valid template.
/// </para>
/// <para>
/// Ordering matters within the transaction: the shipped policy is validated against the shipped BODIES,
/// not against whatever the row currently holds, because the content write in this same transaction is
/// about to replace them.
/// </para>
/// </summary>
public sealed class RestoreEmailTemplateCommandHandler
    : IRequestHandler<RestoreEmailTemplateCommand, RestoreEmailTemplateResponse>
{
    /// <summary>The audit action name; asserted by tests so it cannot be renamed by accident.</summary>
    public const string AuditAction = "RESTORE_EMAIL_TEMPLATE_DEFAULT";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMS.Application.Emails.Contact.IEmailContactPolicyStore _contactPolicies;
    private readonly IMediator _mediator;

    public RestoreEmailTemplateCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        PEMS.Application.Emails.Contact.IEmailContactPolicyStore contactPolicies,
        IMediator mediator)
    {
        _context = context;
        _currentUser = currentUser;
        _contactPolicies = contactPolicies;
        _mediator = mediator;
    }

    public async Task<RestoreEmailTemplateResponse> Handle(
        RestoreEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmailTemplateId == request.EmailTemplateId, cancellationToken);

        if (template == null)
            throw new NotFoundException(nameof(PEMS.Domain.Entities.Emails.EmailTemplate), request.EmailTemplateId);

        var capability = PEMS.Application.Emails.Contact.EmailContactCapabilities.For(template.TemplateCode);

        // ── 1. Only a registered system template can be restored ─────────────
        // Judged against the SHIPPED contact requirement, because that is what this restore is about to
        // write. Reading the currently-configured one — which is what happened while restore was two
        // buttons — would validate the shipped body against a policy the same operation is replacing, and
        // refuse a restore whose only fault was that the operator had previously changed the level.
        var shippedContact = capability.Supported
            ? PEMS.Application.Emails.Contact.EmailContactSettingsInput.ShippedFor(template.TemplateCode)
            : null;

        var contactRequirement = shippedContact?.Requirement ?? EmailContactRequirement.NONE;

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

        // The shipped policy against the shipped bodies — the pair that will exist after the commit. A
        // failure here is a defect in what this application ships, not in anything the operator did, and
        // is worth refusing loudly rather than writing a template that no send could use.
        if (shippedContact is not null)
        {
            PEMS.Application.Emails.Contact.EmailContactSettingsValidator.Validate(
                template.TemplateCode, shippedContact, shipped.BodyVi, shipped.BodyEn);
        }

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

        // The policy, in the same transaction. Its previous values are snapshotted for the audit row
        // below for the same reason the content is: an operator who restores by mistake has to be able to
        // find out what their configuration used to be.
        string? oldContactSnapshot = null;
        string? newContactSnapshot = null;

        if (shippedContact is not null)
        {
            var existing = await _context.EmailContactPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.ScopeType == EmailContactScopeType.TEMPLATE
                         && p.ScopeKey == template.TemplateCode,
                    cancellationToken);

            oldContactSnapshot = existing is null ? null : ContactSnapshot(existing);

            var row = await PEMS.Application.Emails.Contact.EmailContactPolicyWriter.ApplyAsync(
                _context, template.TemplateCode, shippedContact,
                _currentUser.UserId, written.UpdatedAt, cancellationToken);

            newContactSnapshot = ContactSnapshot(row);
        }

        // The replaced text is recorded, not just the fact of a restore: an operator who restores by
        // mistake needs their wording to still exist somewhere, and "content was reset" alone does not
        // give it back. Bodies can be long, so they are stored as one JSON document per side rather than
        // as six change rows.
        var audit = new AuditLog
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
        };

        // A second change row rather than a second audit entry: one restore is one event, and splitting it
        // in two would make the history read as though somebody had pressed two buttons — which is exactly
        // the arrangement this replaces.
        if (newContactSnapshot is not null)
        {
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "ContactSettings",
                ValueFormat = "JSON",
                DisplayOrder = 1,
                OldValueText = oldContactSnapshot,
                NewValueText = newContactSnapshot,
                CreatedAt = written.UpdatedAt,
            });
        }

        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var contactSettings = shippedContact is null
            ? null
            : await _mediator.Send(
                new PEMS.Application.Emails.Contact.GetEmailContactSettingsQuery
                {
                    TemplateCode = template.TemplateCode,
                },
                cancellationToken);

        return new RestoreEmailTemplateResponse
        {
            EmailTemplateId = template.EmailTemplateId,
            TemplateCode = template.TemplateCode,
            Success = true,
            Message = shippedContact is null
                ? "Đã phục hồi nội dung mặc định của mẫu email."
                : "Đã phục hồi nội dung và cấu hình thông tin liên hệ mặc định của mẫu email.",
            Revision = written.Revision,
            UpdatedAt = written.UpdatedAt,
            Name = shipped.Name,
            Description = shipped.Description,
            SubjectVi = shipped.SubjectVi,
            BodyVi = shipped.BodyVi,
            SubjectEn = shipped.SubjectEn,
            BodyEn = shipped.BodyEn,
            ContactSettings = contactSettings,
            ContactSettingsRestored = shippedContact is not null,
        };
    }

    private static string ContactSnapshot(PEMS.Domain.Entities.Emails.EmailContactPolicy p)
        => JsonSerializer.Serialize(new
        {
            p.Requirement,
            p.ContactSource,
            p.ShowEmail,
            p.ShowPhone,
            p.ShowDepartment,
            p.ShowCampus,
            p.ShowSender,
            p.HeadingVi,
            p.HeadingEn,
            p.ReplyToSource,
        });

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
