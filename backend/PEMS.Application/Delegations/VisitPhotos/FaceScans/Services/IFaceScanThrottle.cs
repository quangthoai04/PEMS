namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Services;

/// <summary>
/// Anti-spam / cost-control guard for face-detection scans, mirroring the business-card OCR
/// throttle pattern (sliding-window per-user limit). Singleton, in-memory (single-node deployment).
/// </summary>
public interface IFaceScanThrottle
{
    /// <summary>Default: 20 scans / 10 minutes per user.</summary>
    bool TryAcquireUser(ulong userId);
}
