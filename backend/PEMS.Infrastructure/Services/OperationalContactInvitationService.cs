using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// See <see cref="IOperationalContactInvitationService"/>. Token minting commits through its own
/// SaveChanges — callers invoke this AFTER their business transaction has committed — and the email
/// itself is best-effort.
/// </summary>
public sealed class OperationalContactInvitationService : IOperationalContactInvitationService
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailActionTokenService _tokens;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IDateTimeService _clock;
    private readonly ILogger<OperationalContactInvitationService> _logger;
    private readonly string _frontendBaseUrl;

    public OperationalContactInvitationService(
        IApplicationDbContext db,
        IEmailActionTokenService tokens,
        ISystemEmailDispatcher dispatcher,
        IDateTimeService clock,
        ILogger<OperationalContactInvitationService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _tokens = tokens;
        _dispatcher = dispatcher;
        _clock = clock;
        _logger = logger;
        _frontendBaseUrl = (configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    }

    public async Task<string?> SendInvitationAsync(ulong identityChangeId, CancellationToken cancellationToken)
    {
        var change = await _db.VisitRequestIdentityChanges
            .FirstOrDefaultAsync(c => c.IdentityChangeId == identityChangeId, cancellationToken);
        if (change is null
            || change.Status != IdentityChangeStatuses.Pending
            || string.IsNullOrWhiteSpace(change.NewEmailNormalized))
            return null;

        var isTransfer = change.ChangeKind == IdentityChangeKinds.Transfer;

        // Everything the email says comes from THIS campus. A person invited to one campus is never
        // shown another campus's delegation, dates or contact.
        var campus = await (
            from c in _db.VisitRequestCampuses.AsNoTracking()
            join site in _db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            join v in _db.VisitRequests.AsNoTracking() on c.VisitRequestId equals v.VisitRequestId
            where c.VisitInstanceId == change.VisitInstanceId
            select new
            {
                v.RequestCode,
                CampusName = site.Name,
                c.PlannedStartAt,
                c.PlannedEndAt,
                DelegationName = c.FormDetail!.DelegationName,
                CurrentContactName = c.FormDetail!.OperationalContactFullName,
            }).FirstOrDefaultAsync(cancellationToken);
        if (campus is null)
            return null;

        var now = _clock.VietnamNow;
        var raw = _tokens.GenerateRawToken();

        _db.EmailActionTokens.Add(new EmailActionToken
        {
            TokenHash = _tokens.Hash(raw),
            ActionContext = isTransfer
                ? EmailActionContexts.VisitContactTransfer
                : EmailActionContexts.VisitContactClaim,
            // One decision group per invitation VERSION: a resend bumps token_version, so superseding
            // and de-duplicating both key off a value that changes when the link changes.
            ActionGroupKey = $"OP_CONTACT_CONFIRM:{change.IdentityChangeId}:{change.TokenVersion}",
            TargetType = EmailActionTargetTypes.VisitRequestIdentityChange,
            TargetId = change.IdentityChangeId,
            IntendedAction = EmailIntendedActions.Accept,
            // The invited person may not have an account yet — SSO provisions one when they sign in.
            RecipientUserId = null,
            RecipientEmail = change.NewEmailNormalized!,
            ExpiresAt = change.ExpiresAt,
            ResultStatus = EmailActionResultStatuses.Pending,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        // The frontend confirmation page: it reads the masked summary anonymously, then requires a
        // matching sign-in before it will POST accept or decline. Never a direct-execute link.
        var url = $"{_frontendBaseUrl}/operational-contact-confirmation/{raw}";

        try
        {
            var variables = new Dictionary<string, string>
            {
                ["contactFullName"] = InvitedPersonName(change, campus.CurrentContactName),
                ["requestCode"] = campus.RequestCode,
                ["delegationName"] = campus.DelegationName,
                ["campusName"] = campus.CampusName,
                // Same name and same shape as every other visit email's window (participant, student,
                // department): a per-campus invitation that named its time differently from the rest of
                // the system would be one more spelling for the reader and the template author to keep
                // in step.
                ["plannedTime"] =
                    $"{campus.PlannedStartAt:HH:mm dd/MM/yyyy} - {campus.PlannedEndAt:HH:mm dd/MM/yyyy}",
            };
            if (isTransfer)
                // Naming the person handing the role over is what makes an unexpected invitation
                // verifiable to the person receiving it.
                variables["currentContactName"] = campus.CurrentContactName ?? string.Empty;

            await _dispatcher.SendAsync(new SystemEmailRequest(
                isTransfer ? SystemEmailTemplates.VisitContactTransfer : SystemEmailTemplates.VisitContactClaim,
                // The invitee's own name is not known here — the invitation carries only their address —
                // so the envelope claims no display name it cannot support.
                new EmailRecipient(change.NewEmailNormalized!),
                variables,
                TrustedBlocks: new Dictionary<string, string>
                {
                    // The single-use link exists only inside the action block, which is also why the
                    // rendered body is not kept in the email history.
                    [EmailTrustedBlocks.ActionBlock] =
                        EmailComposition.ContactRoleInvitationBlock(url, change.ExpiresAt),
                },
                RelatedType: "VisitRequestIdentityChange",
                RelatedId: change.IdentityChangeId), cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort: the token row is already committed, so a resend recovers. Failing here must
            // not roll back a confirmation state that is already correct.
            _logger.LogError(ex,
                "operational-contact invitation email failed for identity change {IdentityChangeId} (kind {Kind}, token version {TokenVersion})",
                change.IdentityChangeId, change.ChangeKind, change.TokenVersion);
        }

        return raw;
    }

    /// <summary>
    /// The name to greet the INVITED person by.
    ///
    /// <para>
    /// It used to be <c>change.NewEmailNormalized</c> — the invitation opened "Kính gửi
    /// anh.nguyen@example.com", which is not a person's name and reads as machine-generated to exactly
    /// the reader who is being asked to take on a responsibility. The name is already stored: every
    /// writer of an invitation captures the proposed person's details in
    /// <c>pending_snapshot_json</c>, so that is the source.
    /// </para>
    /// <para>
    /// Order matters, and the second step is deliberately restricted to INITIAL_CONFIRMATION. There the
    /// campus's own snapshot IS the invited person (creation and the pre-decision replace both write the
    /// campus row and then invite that same address), so it is a correct fallback. For a TRANSFER the
    /// campus snapshot is the person handing the role OVER — it is passed separately as
    /// <c>currentContactName</c> — and greeting person B by person A's name would be worse than
    /// greeting them neutrally.
    /// </para>
    /// </summary>
    private static string InvitedPersonName(
        Domain.Entities.Delegations.VisitRequestIdentityChange change, string? campusContactName)
    {
        var fromSnapshot = PendingContactSnapshot.Read(change.PendingSnapshotJson)?.ResolvedFullName;
        if (!string.IsNullOrWhiteSpace(fromSnapshot))
            return fromSnapshot!.Trim();

        if (change.ChangeKind != IdentityChangeKinds.Transfer
            && !string.IsNullOrWhiteSpace(campusContactName))
            return campusContactName!.Trim();

        // Legacy rows written before the snapshot existed, and rows the retention sweep has redacted.
        // A neutral form of address is the honest answer — never the address itself.
        return NeutralSalutation;
    }

    /// <summary>Used when no name is recoverable. Vietnamese business mail opens this way.</summary>
    private const string NeutralSalutation = "Quý Anh/Chị";

    public async Task<Domain.Entities.Delegations.VisitRequestIdentityChange?> LockChangeAsync(
        ulong identityChangeId, CancellationToken cancellationToken)
    {
        var rows = await _db.VisitRequestIdentityChanges
            .FromSqlRaw(
                "SELECT * FROM visit_request_identity_changes WHERE identity_change_id = {0} FOR UPDATE",
                identityChangeId)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<Domain.Entities.Delegations.VisitRequestIdentityChange?> LockPendingChangeForInstanceAsync(
        ulong visitInstanceId, CancellationToken cancellationToken)
    {
        // The DB pending-guard allows at most ONE PENDING row per campus, so SingleOrDefault is safe.
        var rows = await _db.VisitRequestIdentityChanges
            .FromSqlRaw(
                "SELECT * FROM visit_request_identity_changes " +
                "WHERE visit_instance_id = {0} AND status = 'PENDING' FOR UPDATE", visitInstanceId)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }
}
