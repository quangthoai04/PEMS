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
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

public sealed class RestoreEmailContactSettingsCommandHandler
    : IRequestHandler<RestoreEmailContactSettingsCommand, EmailContactSettingsDto>
{
    public const string AuditAction = "RESTORE_EMAIL_CONTACT_SETTINGS_DEFAULT";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public RestoreEmailContactSettingsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IMediator mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<EmailContactSettingsDto> Handle(
        RestoreEmailContactSettingsCommand request, CancellationToken cancellationToken)
    {
        var code = request.TemplateCode?.Trim() ?? string.Empty;

        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateCode == code, cancellationToken);

        if (template is null || SystemEmailTemplates.Find(code) is null)
        {
            throw new NotFoundException(
                $"Mã template email '{code}' không nằm trong danh mục hệ thống.",
                EmailErrorCodes.TemplateNotFound);
        }

        var shipped = EmailContactPolicyDefaults.For(code);

        if (shipped.Requirement == EmailContactRequirement.REQUIRED)
        {
            var marker = "{{" + EmailTrustedBlocks.ContactInformationBlock + "}}";
            var viMissing = !(template.BodyVi?.Contains(marker, StringComparison.Ordinal) ?? false);
            var enMissing = !string.IsNullOrWhiteSpace(template.BodyEn)
                            && !template.BodyEn!.Contains(marker, StringComparison.Ordinal);

            if (viMissing || enMissing)
            {
                throw new BusinessRuleException(
                    $"Không thể phục hồi cấu hình liên hệ về mức Bắt buộc vì nội dung email hiện chưa có {marker}. Hãy phục hồi nội dung mẫu hoặc thêm lại khối liên hệ trước.",
                    EmailErrorCodes.TemplateRequiredContactBlockNotInBody);
            }
        }

        var now = VietnamTime.Now();
        var actor = _currentUser.UserId;

        var row = await _db.EmailContactPolicies
            .FirstOrDefaultAsync(
                p => p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == code, cancellationToken);

        var isNew = row is null;
        if (row is null)
        {
            row = new EmailContactPolicy
            {
                ScopeType = EmailContactScopeType.TEMPLATE,
                ScopeKey = code,
                CreatedAt = now,
                CreatedBy = actor,
            };
            _db.EmailContactPolicies.Add(row);
        }

        var oldSnapshot = isNew ? null : Snapshot(row);

        row.Requirement = shipped.Requirement;
        row.ContactSource = shipped.ContactSource;
        row.ShowEmail = shipped.ShowEmail;
        row.ShowPhone = shipped.ShowPhone;
        row.ShowDepartment = shipped.ShowDepartment;
        row.ShowCampus = shipped.ShowCampus;
        row.ShowSender = shipped.ShowSender;
        row.HeadingVi = shipped.HeadingVi == EmailContactPolicyDefaults.DefaultHeadingVi ? null : shipped.HeadingVi;
        row.HeadingEn = shipped.HeadingEn == EmailContactPolicyDefaults.DefaultHeadingEn ? null : shipped.HeadingEn;
        row.ReplyToSource = shipped.ReplyToSource;
        row.UpdatedAt = now;
        row.UpdatedBy = actor;

        var newSnapshot = Snapshot(row);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = template.CampusId,
            Action = AuditAction,
            EntityType = nameof(EmailContactPolicy),
            EntityId = template.EmailTemplateId, // Use TemplateId for linking
            Reason = $"Phục hồi cấu hình thông tin liên hệ mặc định cho mẫu {code}",
            CreatedAt = now,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "ContactSettings",
                    ValueFormat = "JSON",
                    DisplayOrder = 0,
                    OldValueText = oldSnapshot,
                    NewValueText = newSnapshot,
                    CreatedAt = now,
                },
            },
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(
            new GetEmailContactSettingsQuery { TemplateCode = code }, cancellationToken);
    }

    private static string Snapshot(EmailContactPolicy p)
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
}
