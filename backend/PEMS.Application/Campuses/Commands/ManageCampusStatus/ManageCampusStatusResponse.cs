using System;
using PEMS.Application.Campuses.Common;

namespace PEMS.Application.Campuses.Commands.ManageCampusStatus;

public sealed class ManageCampusStatusResponse
{
    public ulong CampusId { get; init; }
    public string Status { get; init; } = null!;
    public DateTime UpdatedAt { get; init; }
    public ulong? UpdatedBy { get; init; }
    public string Message { get; init; } = "Cập nhật trạng thái campus thành công.";

    /// <summary>
    /// Operational availability recomputed after the change (UC-86 §19.2 step 10), so the UI can
    /// tell "ACTIVE" apart from "ACTIVE + ready for registrations" without another round trip.
    /// </summary>
    public CampusOperationalReadinessDto? Readiness { get; init; }

    /// <summary>
    /// On a successful disable: STAFF/DEPARTMENT accounts of this campus whose sessions were
    /// revoked (users.status is never touched — this is an org-level lock). 0 on enable/no-op.
    /// </summary>
    public int AffectedAccountCount { get; init; }

    /// <summary>On a successful disable: number of active sessions revoked. 0 otherwise.</summary>
    public int RevokedSessionCount { get; init; }
}
