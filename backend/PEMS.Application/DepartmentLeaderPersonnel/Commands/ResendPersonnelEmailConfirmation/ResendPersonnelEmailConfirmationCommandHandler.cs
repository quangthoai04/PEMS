using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.ResendPersonnelEmailConfirmation;

/// <summary>
/// Spec §13. Rate-limited by a per-account cooldown plus a maximum resend count, and issuing a fresh
/// token supersedes the previous one so an old link stops working immediately. The delivery outcome is
/// reported truthfully rather than assumed from a 200.
/// </summary>
public sealed class ResendPersonnelEmailConfirmationCommandHandler
    : IRequestHandler<ResendPersonnelEmailConfirmationCommand, ResendPersonnelEmailConfirmationResponse>
{
    /// <summary>Matches the shared account-confirmation policy so the two flows behave identically.</summary>
    private const int CooldownSeconds = 60;
    private const int MaxResends = 5;

    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;
    private readonly IAccountEmailConfirmationService _confirmations;
    private readonly IEmailService _emailService;
    private readonly IDateTimeService _clock;

    public ResendPersonnelEmailConfirmationCommandHandler(
        IApplicationDbContext db,
        IDepartmentLeaderPersonnelScopeService scopeService,
        IAccountEmailConfirmationService confirmations,
        IEmailService emailService,
        IDateTimeService clock)
    {
        _db = db;
        _scopeService = scopeService;
        _confirmations = confirmations;
        _emailService = emailService;
        _clock = clock;
    }

    public async Task<ResendPersonnelEmailConfirmationResponse> Handle(
        ResendPersonnelEmailConfirmationCommand request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);
        var target = await _scopeService.GetScopedPersonnelAsync(scope, request.UserId, cancellationToken);

        if (target.Status != UserStatuses.PendingEmailConfirmation)
            throw new BusinessRuleException(
                "Chỉ có thể gửi lại email xác nhận cho tài khoản đang chờ xác nhận.",
                DepartmentLeaderErrorCodes.PersonnelNotPending);

        var now = _clock.VietnamNow;

        var latest = await _db.AccountEmailConfirmations
            .Where(c => c.UserId == target.UserId)
            .OrderByDescending(c => c.ConfirmationId)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.Status == AccountEmailConfirmationStatuses.Pending)
        {
            if (latest.CreatedAt.AddSeconds(CooldownSeconds) > now)
                throw new BusinessRuleException(
                    "Vui lòng đợi một lát trước khi gửi lại email xác nhận.",
                    DepartmentLeaderErrorCodes.ResendTooSoon);

            if (latest.ResendCount >= MaxResends)
                throw new BusinessRuleException(
                    "Đã đạt số lần gửi lại tối đa. Vui lòng chỉnh sửa email của nhân sự hoặc liên hệ quản trị hệ thống.",
                    DepartmentLeaderErrorCodes.ResendLimitReached);
        }

        // The address comes from the row — the request never supplies one.
        var email = target.Email;
        var rawToken = await _confirmations.IssuePendingAsync(
            target.UserId, email, isResend: true, cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = scope.ActorUserId,
            CampusId = scope.CampusId,
            Action = DepartmentPersonnelAuditActions.ResendPersonnelConfirmation,
            EntityType = "User",
            EntityId = target.UserId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "ConfirmationResend",
                    OldValueText = null,
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        email = DepartmentPersonnelAudit.MaskEmail(email),
                        departmentId = scope.DepartmentId,
                    }),
                },
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        var delivery = await _emailService.TrySendAsync(
            email,
            AccountConfirmationEmail.Subject,
            AccountConfirmationEmail.BuildHtml(target.FullName, _confirmations.BuildConfirmUrl(rawToken)),
            cancellationToken);

        var deliveryStatus = AccountConfirmationEmail.MapStatus(delivery.Status);
        var resendCount = (latest?.Status == AccountEmailConfirmationStatuses.Pending ? latest.ResendCount : 0) + 1;

        return new ResendPersonnelEmailConfirmationResponse
        {
            Success = true,
            UserId = target.UserId,
            Email = email,
            EmailNotificationStatus = deliveryStatus,
            ResendCount = resendCount,
            Message = deliveryStatus == DepartmentPersonnelEmails.StatusSent
                ? "Đã gửi lại email xác nhận."
                : "Chưa gửi được email xác nhận. Vui lòng thử lại sau.",
        };
    }
}
