namespace PEMS.Application.BusinessCardOcr.Services;

/// <summary>
/// Anti-spam / cost-control guard for OCR scans (02 prompt §9): sliding-window
/// per-user and per-IP rate limits + Idempotency-Key replay cache.
/// Singleton, in-memory (single-node deployment).
/// </summary>
public interface IBusinessCardOcrThrottle
{
    /// <summary>Default: 10 scans / 10 minutes per user.</summary>
    bool TryAcquireUser(ulong userId);

    /// <summary>Default: 30 scans / 10 minutes per client IP.</summary>
    bool TryAcquireIp(string ipAddress);

    bool TryGetIdempotentJob(ulong userId, string idempotencyKey, out ulong ocrJobId);

    void RememberIdempotentJob(ulong userId, string idempotencyKey, ulong ocrJobId);
}
