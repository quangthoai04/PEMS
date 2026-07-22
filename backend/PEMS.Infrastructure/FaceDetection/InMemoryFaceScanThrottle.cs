using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Services;

namespace PEMS.Infrastructure.FaceDetection;

/// <summary>Sliding-window in-memory rate limiter (single-node) — 20 scans/10min per user.</summary>
public sealed class InMemoryFaceScanThrottle : IFaceScanThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private const int UserLimit = 20;

    private readonly ConcurrentDictionary<ulong, List<DateTimeOffset>> _hits = new();

    public bool TryAcquireUser(ulong userId)
    {
        var now = DateTimeOffset.UtcNow;
        var list = _hits.GetOrAdd(userId, _ => new List<DateTimeOffset>());
        lock (list)
        {
            list.RemoveAll(t => now - t > Window);
            if (list.Count >= UserLimit) return false;
            list.Add(now);
            return true;
        }
    }
}
