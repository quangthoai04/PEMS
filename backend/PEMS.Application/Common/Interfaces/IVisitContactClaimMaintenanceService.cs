using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Common.Interfaces;

/// <summary>Outcome of one maintenance sweep (metrics for logging/telemetry).</summary>
public sealed record VisitContactClaimMaintenanceResult(int Expired, int Redacted);

/// <summary>
/// Periodic maintenance for identity claims (plan §16.8), runnable from a hosted job or a test:
///   • EXPIRE — PENDING claims past <c>expires_at</c> become EXPIRED (+90-day retention stamp, event,
///     outstanding tokens burned). The request is NOT cancelled: the contact simply stays unclaimed and
///     the registrant can resend/replace.
///   • REDACT — terminal claims (EXPIRED/DECLINED/CANCELLED/SUPERSEDED) past <c>retention_until</c> get
///     their full email, pending snapshot and token recipient emails cleared. The masked email, kind,
///     status, actors and timestamps are KEPT for investigation. APPLIED claims are never redacted here.
/// Both passes are idempotent (filters exclude already-processed rows) and batched.
/// </summary>
public interface IVisitContactClaimMaintenanceService
{
    Task<VisitContactClaimMaintenanceResult> RunOnceAsync(
        DateTime vietnamNow, int batchSize, CancellationToken cancellationToken);
}
