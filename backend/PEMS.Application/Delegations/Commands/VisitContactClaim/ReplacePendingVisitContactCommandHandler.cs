using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.VisitContactClaim;

/// <summary>
/// The verified REGISTRANT fixes a wrong/unclaimed primary contact (plan §16.4 "nhập lại email đúng"):
/// any PENDING claim + tokens are SUPERSEDED, the request-level contact snapshot is rewritten and either
/// (a) new email == registrant email → the registrant is linked immediately (ACTIVE, no claim), or
/// (b) different email → a fresh PENDING INITIAL_CLAIM (72h) is created and a new invitation goes out
/// after commit. Only while the contact is still PENDING_CONFIRMATION — an ACTIVE contact can only be
/// changed via the TRANSFER workflow, never by this command.
/// </summary>
public sealed class ReplacePendingVisitContactCommandHandler
    : IRequestHandler<ReplacePendingVisitContactCommand, VisitContactClaimManageResponse>
{
    private const int ClaimValidityHours = 72;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitContactClaimService _claimService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public ReplacePendingVisitContactCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitContactClaimService claimService, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _claimService = claimService;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactClaimManageResponse> Handle(
        ReplacePendingVisitContactCommand request, CancellationToken cancellationToken)
    {
        var actorId = RegistrantClaimGuard.EnsureAuthenticatedVisitor(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        var newEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(request.Email);
        var newEmailMasked = VisitRequestFingerprintBuilder.MaskEmail(newEmailNorm);

        ulong? claimIdToSend = null;
        VisitRequest visit;
        uint resendCount = 0;

        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            visit = await RegistrantClaimGuard.LoadRequestForRegistrantAsync(
                _db, request.VisitRequestId, actorId, cancellationToken);

            var registrantEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(visit.RegistrantEmail);
            var linksRegistrant = newEmailNorm == registrantEmailNorm;

            // A NON-registrant contact email must not belong to an internal (non-VISITOR) account —
            // same rule the create flows enforce.
            if (!linksRegistrant)
            {
                var internalOwner = await _db.Users.AsNoTracking()
                    .Where(u => u.Email == newEmailNorm && u.Role.RoleCode != RoleCodes.Visitor)
                    .Select(u => (ulong?)u.UserId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (internalOwner is not null)
                    throw new ConflictException(
                        "Email này thuộc tài khoản nội bộ nên không thể làm đầu mối liên hệ khách.",
                        VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount);
            }

            // ── Supersede the current PENDING claim (if any) + its tokens ──
            var oldClaim = await _claimService.LockPendingInitialClaimAsync(
                visit.VisitRequestId, cancellationToken);
            if (oldClaim is not null)
            {
                await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                    _db, EmailActionTargetTypes.VisitRequestIdentityChange, oldClaim.IdentityChangeId,
                    "Đầu mối liên hệ đã được thay thế.", now, cancellationToken);

                oldClaim.Status = IdentityChangeStatuses.Superseded;
                oldClaim.SupersededAt = now;
                oldClaim.RetentionUntil = now.AddDays(90);
                oldClaim.UpdatedAt = now;
                _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
                {
                    IdentityChangeId = oldClaim.IdentityChangeId,
                    VisitRequestId = visit.VisitRequestId,
                    EventType = "PRIMARY_CONTACT_INVITATION_SUPERSEDED",
                    FromStatus = IdentityChangeStatuses.Pending,
                    ToStatus = IdentityChangeStatuses.Superseded,
                    ActorUserId = actorId,
                    EmailMasked = oldClaim.NewEmailMasked,
                    Reason = "Registrant replaced the pending contact.",
                    CorrelationId = correlationId,
                    CreatedAt = now,
                });
            }

            // ── Rewrite the request-level contact snapshot ──
            var oldMasked = VisitRequestFingerprintBuilder.MaskEmail(
                VisitRequestFingerprintBuilder.NormalizeEmail(visit.ContactPersonEmail));
            visit.ContactPersonFullName = request.FullName.Trim();
            visit.ContactPersonOrganization = string.IsNullOrWhiteSpace(request.Organization) ? null : request.Organization.Trim();
            visit.ContactPersonPhone = request.Phone.Trim();
            visit.ContactPersonEmail = newEmailNorm;
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;
            visit.RowVersion += 1;

            if (linksRegistrant)
            {
                // Registrant IS the contact: the registrant account is already OTP/SSO-verified → link now.
                visit.VisitorUserId = actorId;
                visit.PrimaryContactAccessStatus = PrimaryContactAccessStatuses.Active;
                visit.PrimaryContactVerifiedAt = now;
            }
            else
            {
                var claim = new VisitRequestIdentityChange
                {
                    VisitRequestId = visit.VisitRequestId,
                    ChangeKind = IdentityChangeKinds.InitialClaim,
                    TargetRelation = "PRIMARY_CONTACT",
                    ConfirmationMethod = "GOOGLE_SSO",
                    NewEmailNormalized = newEmailNorm,
                    NewEmailMasked = newEmailMasked,
                    PendingSnapshotJson = JsonSerializer.Serialize(new
                    {
                        fullName = visit.ContactPersonFullName,
                        organization = visit.ContactPersonOrganization,
                        phone = visit.ContactPersonPhone,
                        email = newEmailNorm,
                    }, Json),
                    Status = IdentityChangeStatuses.Pending,
                    ExpectedRequestRowVersion = (uint)visit.RowVersion,
                    RequestedBy = actorId,
                    RequestedAt = now,
                    ExpiresAt = now.AddHours(ClaimValidityHours),
                    ResendCount = 0,
                    CreatedAt = now,
                };
                _db.VisitRequestIdentityChanges.Add(claim);
                await _db.SaveChangesAsync(cancellationToken); // resolve claim id for its event FK

                _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
                {
                    IdentityChangeId = claim.IdentityChangeId,
                    VisitRequestId = visit.VisitRequestId,
                    EventType = "PRIMARY_CONTACT_INVITATION_CREATED",
                    FromStatus = null,
                    ToStatus = IdentityChangeStatuses.Pending,
                    ActorUserId = actorId,
                    EmailMasked = newEmailMasked,
                    CorrelationId = correlationId,
                    CreatedAt = now,
                });
                claimIdToSend = claim.IdentityChangeId;
            }

            var audit = new AuditLog
            {
                ActorUserId = actorId,
                Action = "PRIMARY_CONTACT_REPLACED",
                EntityType = "VisitRequest",
                EntityId = visit.VisitRequestId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                CreatedAt = now,
            };
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "contact_person_email",
                OldValueText = oldMasked,          // masked only — audit never stores the full old/new email
                NewValueText = newEmailMasked,
                CreatedAt = now,
            });
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "primary_contact_access_status",
                OldValueText = PrimaryContactAccessStatuses.PendingConfirmation,
                NewValueText = visit.PrimaryContactAccessStatus,
                CreatedAt = now,
            });
            _db.AuditLogs.Add(audit);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        // AFTER commit: invitation for the fresh claim (different email only).
        if (claimIdToSend is not null)
            await _claimService.SendInvitationAsync(claimIdToSend.Value, cancellationToken);

        return new VisitContactClaimManageResponse(
            visit.VisitRequestId,
            visit.PrimaryContactAccessStatus,
            claimIdToSend is null ? null : IdentityChangeStatuses.Pending,
            resendCount,
            claimIdToSend is null
                ? "Bạn đã trở thành đầu mối liên hệ của đơn (email trùng với email người đăng ký)."
                : "Đã cập nhật đầu mối liên hệ và gửi lời mời xác nhận tới email mới.");
    }
}

/// <summary>Structural validation for the replace-contact payload.</summary>
public sealed class ReplacePendingVisitContactCommandValidator
    : AbstractValidator<ReplacePendingVisitContactCommand>
{
    public ReplacePendingVisitContactCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối liên hệ không được để trống.").MaximumLength(150);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại đầu mối liên hệ không được để trống.").MaximumLength(50);
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email đầu mối liên hệ không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối liên hệ không đúng định dạng.")
            .MaximumLength(150);
    }
}
