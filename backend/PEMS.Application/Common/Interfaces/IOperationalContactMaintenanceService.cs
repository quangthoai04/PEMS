using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Common.Interfaces;

/// <summary>Outcome of one maintenance sweep (metrics for logging/telemetry).</summary>
public sealed record OperationalContactMaintenanceResult(int Expired, int Redacted);

/// <summary>
/// Periodic maintenance for per-campus operational-contact invitations, runnable from a hosted job or
/// a test:
///   • EXPIRE — PENDING invitations past <c>expires_at</c> become EXPIRED (+90-day retention stamp,
///     event, outstanding tokens burned). Nothing else moves: an expired INITIAL_CONFIRMATION leaves
///     the campus without a contact (so the global gate stays shut and the registrant can resend or
///     replace), and an expired TRANSFER leaves the current contact exactly where they were.
///   • REDACT — terminal invitations (EXPIRED/DECLINED/CANCELLED/SUPERSEDED) past
///     <c>retention_until</c> get their full email, pending snapshot and token recipient addresses
///     cleared. The masked address, kind, status, actors and timestamps are KEPT for investigation.
///     APPLIED invitations are never redacted here.
/// Both passes are idempotent (filters exclude already-processed rows) and batched.
/// </summary>
public interface IOperationalContactMaintenanceService
{
    Task<OperationalContactMaintenanceResult> RunOnceAsync(
        DateTime vietnamNow, int batchSize, CancellationToken cancellationToken);
}
