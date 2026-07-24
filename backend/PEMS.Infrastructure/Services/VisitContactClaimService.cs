using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;

namespace PEMS.Infrastructure.Services;

/// <summary>See <see cref="IVisitContactClaimService"/>. Token minting commits via its own SaveChanges
/// (the caller invokes this AFTER its business transaction committed); the email is best-effort.</summary>
public sealed class VisitContactClaimService : IVisitContactClaimService
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailActionTokenService _tokens;
    private readonly IEmailService _email;
    private readonly IDateTimeService _clock;
    private readonly ILogger<VisitContactClaimService> _logger;
    private readonly string _frontendBaseUrl;

    public VisitContactClaimService(
        IApplicationDbContext db,
        IEmailActionTokenService tokens,
        IEmailService email,
        IDateTimeService clock,
        ILogger<VisitContactClaimService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _tokens = tokens;
        _email = email;
        _clock = clock;
        _logger = logger;
        _frontendBaseUrl = (configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    }

    public async Task<string?> SendInvitationAsync(ulong identityChangeId, CancellationToken cancellationToken)
    {
        var claim = await _db.VisitRequestIdentityChanges
            .FirstOrDefaultAsync(c => c.IdentityChangeId == identityChangeId, cancellationToken);
        if (claim is null || claim.Status != IdentityChangeStatuses.Pending
            || claim.ChangeKind != IdentityChangeKinds.InitialClaim
            || string.IsNullOrWhiteSpace(claim.NewEmailNormalized))
            return null;

        var request = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == claim.VisitRequestId)
            // A claim covers the WHOLE request → mixed content has no single business name.
            .Select(v => new
            {
                v.RequestCode,
                DelegationName = v.HasMixedCampusDetails
                    ? "Khác nhau theo cơ sở"
                    : v.CampusInstances.Select(c => c.FormDetail!.DelegationName).FirstOrDefault(),
                v.RegistrantFullName,
                v.ContactPersonFullName
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (request is null)
            return null;

        var now = _clock.VietnamNow;
        var raw = _tokens.GenerateRawToken();
        _db.EmailActionTokens.Add(new EmailActionToken
        {
            TokenHash = _tokens.Hash(raw),
            ActionContext = EmailActionContexts.VisitContactClaim,
            // One decision-group per claim: superseding/invalidation targets every token of this claim.
            ActionGroupKey = $"CONTACT_CLAIM:{claim.IdentityChangeId}",
            TargetType = EmailActionTargetTypes.VisitRequestIdentityChange,
            TargetId = claim.IdentityChangeId,
            IntendedAction = EmailIntendedActions.Accept,
            RecipientUserId = null, // B may not have an account yet — SSO can auto-provision at login
            RecipientEmail = claim.NewEmailNormalized!,
            ExpiresAt = claim.ExpiresAt,
            ResultStatus = EmailActionResultStatuses.Pending,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        // Frontend claim page — it calls GET /api/public/visit-contact-claims/{token} for the masked
        // summary, then requires Google login before POST accept/decline. Never a direct-execute link.
        var claimUrl = $"{_frontendBaseUrl}/visit-contact-claim/{raw}";
        try
        {
            var subject = $"[FPTU PEMS] Xác nhận vai trò đầu mối liên hệ — {request.RequestCode}";
            var html = $@"
<p>Xin chào {System.Net.WebUtility.HtmlEncode(request.ContactPersonFullName)},</p>
<p>Bạn được <b>{System.Net.WebUtility.HtmlEncode(request.RegistrantFullName)}</b> chỉ định làm <b>đầu mối liên hệ</b>
cho đơn đăng ký tham quan <b>{System.Net.WebUtility.HtmlEncode(request.DelegationName)}</b>
(mã đơn <b>{request.RequestCode}</b>) tại Trường Đại học FPT.</p>
<p>Để xác nhận, vui lòng mở liên kết dưới đây và <b>đăng nhập bằng đúng tài khoản Google của email này</b>,
sau đó bấm <b>Chấp nhận</b>:</p>
<p><a href=""{claimUrl}"">{claimUrl}</a></p>
<p>Liên kết có hiệu lực đến <b>{claim.ExpiresAt:HH:mm dd/MM/yyyy}</b>. Nếu bạn không nhận vai trò này,
bạn có thể từ chối trong trang trên hoặc bỏ qua email (lời mời sẽ tự hết hạn; đơn đăng ký không bị hủy).</p>
<p>Trân trọng,<br/>FPTU PEMS</p>";
            await _email.SendAsync(claim.NewEmailNormalized!, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort: the token row is already committed — the registrant can resend.
            _logger.LogError(ex,
                "visit-contact-claim invitation email failed for identity change {IdentityChangeId}",
                claim.IdentityChangeId);
        }

        return raw;
    }

    /// <summary>Transfer sibling of <see cref="SendInvitationAsync"/> — its own token context, frontend
    /// page and wording (the invited person is REPLACING an existing ACTIVE contact).</summary>
    public async Task<string?> SendTransferInvitationAsync(ulong identityChangeId, CancellationToken cancellationToken)
    {
        var claim = await _db.VisitRequestIdentityChanges
            .FirstOrDefaultAsync(c => c.IdentityChangeId == identityChangeId, cancellationToken);
        if (claim is null || claim.Status != IdentityChangeStatuses.Pending
            || claim.ChangeKind != IdentityChangeKinds.Transfer
            || string.IsNullOrWhiteSpace(claim.NewEmailNormalized))
            return null;

        var request = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == claim.VisitRequestId)
            // A claim covers the WHOLE request → mixed content has no single business name.
            .Select(v => new
            {
                v.RequestCode,
                DelegationName = v.HasMixedCampusDetails
                    ? "Khác nhau theo cơ sở"
                    : v.CampusInstances.Select(c => c.FormDetail!.DelegationName).FirstOrDefault(),
                v.ContactPersonFullName
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (request is null)
            return null;

        var now = _clock.VietnamNow;
        var raw = _tokens.GenerateRawToken();
        _db.EmailActionTokens.Add(new EmailActionToken
        {
            TokenHash = _tokens.Hash(raw),
            ActionContext = EmailActionContexts.VisitContactTransfer,
            ActionGroupKey = $"CONTACT_TRANSFER:{claim.IdentityChangeId}",
            TargetType = EmailActionTargetTypes.VisitRequestIdentityChange,
            TargetId = claim.IdentityChangeId,
            IntendedAction = EmailIntendedActions.Accept,
            RecipientUserId = null, // the new contact may not have an account yet — SSO provisions at login
            RecipientEmail = claim.NewEmailNormalized!,
            ExpiresAt = claim.ExpiresAt,
            ResultStatus = EmailActionResultStatuses.Pending,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        var transferUrl = $"{_frontendBaseUrl}/visit-contact-transfer/{raw}";
        try
        {
            var subject = $"[FPTU PEMS] Lời mời nhận vai trò đầu mối liên hệ — {request.RequestCode}";
            var html = $@"
<p>Xin chào,</p>
<p>Bạn được đề nghị <b>tiếp nhận vai trò đầu mối liên hệ</b> cho đơn đăng ký tham quan
<b>{System.Net.WebUtility.HtmlEncode(request.DelegationName)}</b> (mã đơn <b>{request.RequestCode}</b>)
tại Trường Đại học FPT, thay cho đầu mối hiện tại.</p>
<p>Để xác nhận, vui lòng mở liên kết dưới đây và <b>đăng nhập bằng đúng tài khoản Google của email này</b>,
sau đó bấm <b>Chấp nhận</b>. Đầu mối hiện tại vẫn giữ quyền cho tới khi bạn xác nhận:</p>
<p><a href=""{transferUrl}"">{transferUrl}</a></p>
<p>Liên kết có hiệu lực đến <b>{claim.ExpiresAt:HH:mm dd/MM/yyyy}</b> (24 giờ). Nếu bạn không nhận vai trò
này, bạn có thể từ chối trong trang trên hoặc bỏ qua email — mọi quyền hiện tại được giữ nguyên.</p>
<p>Trân trọng,<br/>FPTU PEMS</p>";
            await _email.SendAsync(claim.NewEmailNormalized!, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "visit-contact-transfer invitation email failed for identity change {IdentityChangeId}",
                claim.IdentityChangeId);
        }

        return raw;
    }

    public async Task<Domain.Entities.Delegations.VisitRequestIdentityChange?> LockClaimAsync(
        ulong identityChangeId, CancellationToken cancellationToken)
    {
        var rows = await _db.VisitRequestIdentityChanges
            .FromSqlRaw(
                "SELECT * FROM visit_request_identity_changes WHERE identity_change_id = {0} FOR UPDATE",
                identityChangeId)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<Domain.Entities.Delegations.VisitRequestIdentityChange?> LockPendingInitialClaimAsync(
        ulong visitRequestId, CancellationToken cancellationToken)
        => await LockPendingChangeAsync(visitRequestId, IdentityChangeKinds.InitialClaim, cancellationToken);

    public async Task<Domain.Entities.Delegations.VisitRequestIdentityChange?> LockPendingChangeAsync(
        ulong visitRequestId, string? changeKind, CancellationToken cancellationToken)
    {
        // The DB pending-guard allows at most ONE PENDING row per request/relation, so SingleOrDefault
        // is safe for both the kind-filtered and the any-kind form.
        var rows = changeKind is null
            ? await _db.VisitRequestIdentityChanges
                .FromSqlRaw(
                    "SELECT * FROM visit_request_identity_changes " +
                    "WHERE visit_request_id = {0} AND status = 'PENDING' FOR UPDATE", visitRequestId)
                .ToListAsync(cancellationToken)
            : await _db.VisitRequestIdentityChanges
                .FromSqlRaw(
                    "SELECT * FROM visit_request_identity_changes " +
                    "WHERE visit_request_id = {0} AND change_kind = {1} AND status = 'PENDING' FOR UPDATE",
                    visitRequestId, changeKind)
                .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }
}
