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
/// See <see cref="IOperationalContactInvitationService"/>.
///
/// <para>
/// Two halves, and the split is what makes a caller's write atomic with its link:
/// <see cref="MintInvitationTokensAsync"/> only ADDS the token rows to the caller's own
/// <see cref="IApplicationDbContext"/> — no SaveChanges, no transaction of its own — so a caller that
/// mints inside its transaction commits the identity change and its links together, and a token
/// failure rolls both back. <see cref="DispatchInvitationEmailAsync"/> is then called after that
/// commit.
/// </para>
/// <para>
/// There used to be a third method, a <c>SendInvitationAsync</c> wrapper that minted, ran its OWN
/// SaveChanges and sent. It was removed rather than documented, because a convenience that mints after
/// somebody else's commit cannot be used safely: every one of its callers (replace, reinvite, resend,
/// create) was writing a PENDING invitation first and minting afterwards, so a token failure left an
/// invitation nobody could answer and nobody could replace. The absence of the wrapper is the
/// guardrail — a caller now has to hold a transaction open to get a token at all.
/// </para>
/// <para>
/// The email itself is best-effort either way: once the links are durable, a mail-provider failure is
/// recovered by a resend rather than by undoing a change the user was told had happened.
/// </para>
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

    public async Task<OperationalContactInvitationTokens?> MintInvitationTokensAsync(
        ulong identityChangeId, CancellationToken cancellationToken)
    {
        var change = await _db.VisitRequestIdentityChanges
            .FirstOrDefaultAsync(c => c.IdentityChangeId == identityChangeId, cancellationToken);
        if (change is null
            || change.Status != IdentityChangeStatuses.Pending
            || string.IsNullOrWhiteSpace(change.NewEmailNormalized))
            return null;

        var isTransfer = change.ChangeKind == IdentityChangeKinds.Transfer;
        var now = _clock.VietnamNow;

        // ── TWO tokens, one per intended action (see the interface docs). NOT flushed here: the
        //    caller's transaction is what makes them durable together with the identity change. ──
        var groupKey = $"OP_CONTACT_CONFIRM:{change.IdentityChangeId}:{change.TokenVersion}";
        var context = isTransfer
            ? EmailActionContexts.VisitContactTransfer
            : EmailActionContexts.VisitContactClaim;

        string Mint(string intendedAction)
        {
            var raw = _tokens.GenerateRawToken();
            _db.EmailActionTokens.Add(new EmailActionToken
            {
                TokenHash = _tokens.Hash(raw),
                ActionContext = context,
                ActionGroupKey = groupKey,
                TargetType = EmailActionTargetTypes.VisitRequestIdentityChange,
                TargetId = change.IdentityChangeId,
                IntendedAction = intendedAction,
                // The invited person may not have an account yet — accepting provisions/links one.
                RecipientUserId = null,
                RecipientEmail = change.NewEmailNormalized!,
                ExpiresAt = change.ExpiresAt,
                ResultStatus = EmailActionResultStatuses.Pending,
                CreatedAt = now,
            });
            return raw;
        }

        return new OperationalContactInvitationTokens(
            Mint(EmailIntendedActions.Accept), Mint(EmailIntendedActions.Decline));
    }

    public async Task DispatchInvitationEmailAsync(
        ulong identityChangeId, OperationalContactInvitationTokens tokens, CancellationToken cancellationToken)
    {
        var change = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentityChangeId == identityChangeId, cancellationToken);
        if (change is null || string.IsNullOrWhiteSpace(change.NewEmailNormalized))
            return;

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
            return;

        // The frontend confirmation page: it reads the masked summary anonymously, shows the CURRENT
        // visit details, and POSTs the answer when the reader confirms on the page.
        var url = $"{_frontendBaseUrl}/operational-contact-confirmation/{tokens.AcceptToken}";
        var declineUrl = $"{_frontendBaseUrl}/operational-contact-confirmation/{tokens.DeclineToken}";

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
                // The specific deadline for THIS invitation's one-time links — genuinely per-send data, so
                // it belongs in the template as a variable rather than baked into the action block (see
                // EmailComposition.ContactRoleInvitationBlock).
                ["contactExpiresAt"] = change.ExpiresAt.ToString("HH:mm dd/MM/yyyy"),
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
                        EmailComposition.ContactRoleInvitationBlock(url, declineUrl),
                },
                RelatedType: "VisitRequestIdentityChange",
                RelatedId: change.IdentityChangeId), cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort, and deliberately so — this is the ONE failure in the invitation flow that
            // must not undo anything. The identity change and its tokens are already committed, so
            // the invitation is real and answerable; only the delivery failed, and a resend recovers
            // it. Throwing here would turn a mail-provider outage into a rolled-back transfer, or
            // (worse, and what used to happen) into a 500 on top of a change that had already stuck.
            _logger.LogError(ex,
                "operational-contact invitation email failed for identity change {IdentityChangeId} (kind {Kind}, token version {TokenVersion})",
                change.IdentityChangeId, change.ChangeKind, change.TokenVersion);
        }
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
