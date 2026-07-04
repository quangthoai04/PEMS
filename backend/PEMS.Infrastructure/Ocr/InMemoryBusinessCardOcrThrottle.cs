using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.BusinessCardOcr.Services;

namespace PEMS.Infrastructure.Ocr;

/// <summary>
/// Sliding-window in-memory rate limiter + idempotency cache (single-node).
/// Defaults per 02 prompt §9: 10 scans/10min per user, 30 scans/10min per IP.
/// </summary>
public sealed class InMemoryBusinessCardOcrThrottle : IBusinessCardOcrThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private const int UserLimit = 10;
    private const int IpLimit = 30;
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _hits = new();
    private readonly ConcurrentDictionary<string, (ulong JobId, DateTimeOffset ExpiresAt)> _idempotency = new();

    public bool TryAcquireUser(ulong userId) => TryAcquire($"u:{userId}", UserLimit);

    public bool TryAcquireIp(string ipAddress) => TryAcquire($"ip:{ipAddress}", IpLimit);

    public bool TryGetIdempotentJob(ulong userId, string idempotencyKey, out ulong ocrJobId)
    {
        ocrJobId = 0;
        if (_idempotency.TryGetValue(Key(userId, idempotencyKey), out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                ocrJobId = entry.JobId;
                return true;
            }
            _idempotency.TryRemove(Key(userId, idempotencyKey), out _);
        }
        return false;
    }

    public void RememberIdempotentJob(ulong userId, string idempotencyKey, ulong ocrJobId)
        => _idempotency[Key(userId, idempotencyKey)] = (ocrJobId, DateTimeOffset.UtcNow.Add(IdempotencyTtl));

    private static string Key(ulong userId, string idempotencyKey) => $"{userId}:{idempotencyKey}";

    private bool TryAcquire(string key, int limit)
    {
        var now = DateTimeOffset.UtcNow;
        var list = _hits.GetOrAdd(key, _ => new List<DateTimeOffset>());
        lock (list)
        {
            list.RemoveAll(t => now - t > Window);
            if (list.Count >= limit) return false;
            list.Add(now);
            return true;
        }
    }
}
