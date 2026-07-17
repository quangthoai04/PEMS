using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Services;

/// <summary>See <see cref="IVisitContactClaimMaintenanceService"/>. Each batch commits in its own
/// transaction; a failure in one row's processing aborts only the current sweep, never corrupts state
/// (everything for a sweep is one atomic commit).</summary>
public sealed class VisitContactClaimMaintenanceService : IVisitContactClaimMaintenanceService
{
    private const int RetentionDays = 90;

    private readonly IApplicationDbContext _db;
    private readonly ILogger<VisitContactClaimMaintenanceService> _logger;

    public VisitContactClaimMaintenanceService(
        IApplicationDbContext db, ILogger<VisitContactClaimMaintenanceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<VisitContactClaimMaintenanceResult> RunOnceAsync(
        DateTime vietnamNow, int batchSize, CancellationToken cancellationToken)
    {
        var expired = await ExpireBatchAsync(vietnamNow, batchSize, cancellationToken);
        var redacted = await RedactBatchAsync(vietnamNow, batchSize, cancellationToken);
        if (expired > 0 || redacted > 0)
            _logger.LogInformation(
                "visit-contact-claim maintenance: {Expired} expired, {Redacted} redacted", expired, redacted);
        return new VisitContactClaimMaintenanceResult(expired, redacted);
    }

    private async Task<int> ExpireBatchAsync(DateTime now, int batchSize, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(ct);

        // FOR UPDATE + the status filter makes the sweep idempotent under concurrency: a second
        // runner blocks, then re-reads rows that are no longer PENDING and takes nothing.
        var due = await _db.VisitRequestIdentityChanges
            .FromSqlRaw(
                "SELECT * FROM visit_request_identity_changes " +
                "WHERE status = 'PENDING' AND expires_at <= {0} " +
                "ORDER BY identity_change_id LIMIT {1} FOR UPDATE", now, batchSize)
            .ToListAsync(ct);
        if (due.Count == 0)
        {
            await tx.CommitAsync(ct);
            return 0;
        }

        foreach (var claim in due)
        {
            await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _db, EmailActionTargetTypes.VisitRequestIdentityChange, claim.IdentityChangeId,
                "Lời mời đã hết hạn.", now, ct);

            claim.Status = IdentityChangeStatuses.Expired;
            claim.RetentionUntil = now.AddDays(RetentionDays);
            claim.UpdatedAt = now;

            _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = claim.IdentityChangeId,
                VisitRequestId = claim.VisitRequestId,
                EventType = "PRIMARY_CONTACT_INVITATION_EXPIRED",
                FromStatus = IdentityChangeStatuses.Pending,
                ToStatus = IdentityChangeStatuses.Expired,
                ActorUserId = null, // system transition
                EmailMasked = claim.NewEmailMasked,
                Reason = "EXPIRY_JOB",
                CreatedAt = now,
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return due.Count;
    }

    private async Task<int> RedactBatchAsync(DateTime now, int batchSize, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(ct);

        var due = await _db.VisitRequestIdentityChanges
            .FromSqlRaw(
                "SELECT * FROM visit_request_identity_changes " +
                "WHERE status IN ('EXPIRED','DECLINED','CANCELLED','SUPERSEDED') " +
                "  AND retention_until IS NOT NULL AND retention_until <= {0} " +
                "  AND redacted_at IS NULL " +
                "ORDER BY identity_change_id LIMIT {1} FOR UPDATE", now, batchSize)
            .ToListAsync(ct);
        if (due.Count == 0)
        {
            await tx.CommitAsync(ct);
            return 0;
        }

        foreach (var claim in due)
        {
            // Redact PII: full email + pending snapshot + free-text reason + the tokens' recipient
            // email. KEEP: visit_request_id, kind, status, masked email, actors, timestamps (plan §16.8).
            claim.NewEmailNormalized = null;
            claim.PendingSnapshotJson = null;
            claim.Reason = null;
            claim.RedactedAt = now;
            claim.UpdatedAt = now;

            var tokens = await _db.EmailActionTokens
                .Where(t => t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                            && t.TargetId == claim.IdentityChangeId
                            && t.RecipientEmail != claim.NewEmailMasked)
                .ToListAsync(ct);
            foreach (var token in tokens)
                token.RecipientEmail = claim.NewEmailMasked;

            _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = claim.IdentityChangeId,
                VisitRequestId = claim.VisitRequestId,
                EventType = "IDENTITY_CHANGE_REDACTED",
                FromStatus = claim.Status,
                ToStatus = claim.Status,
                ActorUserId = null, // system transition
                EmailMasked = claim.NewEmailMasked,
                Reason = "fields=new_email_normalized,pending_snapshot_json,reason,token_recipient_email",
                CreatedAt = now,
            });
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = null,
                Action = "IDENTITY_CHANGE_REDACTED",
                EntityType = "VisitRequestIdentityChange",
                EntityId = claim.IdentityChangeId,
                VisitRequestId = claim.VisitRequestId,
                SourceType = "RETENTION",
                Reason = $"retention_until={claim.RetentionUntil:yyyy-MM-dd}",
                CreatedAt = now,
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return due.Count;
    }
}
