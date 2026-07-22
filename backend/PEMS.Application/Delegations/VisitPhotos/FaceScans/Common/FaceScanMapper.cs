using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

/// <summary>Builds the API-facing scan+detections DTO, resolving requester/reviewer/guest display names.</summary>
public static class FaceScanMapper
{
    public static async Task<VisitPhotoFaceScanDto> ToDtoAsync(
        IApplicationDbContext db, ulong faceScanId, CancellationToken cancellationToken)
    {
        var scan = await db.VisitPhotoFaceScans.AsNoTracking()
            .FirstOrDefaultAsync(s => s.FaceScanId == faceScanId, cancellationToken)
            ?? throw new NotFoundException("VisitPhotoFaceScan", faceScanId);

        var dtos = await ToDtosAsync(db, new[] { scan }, cancellationToken);
        return dtos[0];
    }

    public static async Task<List<VisitPhotoFaceScanDto>> ToDtosAsync(
        IApplicationDbContext db, IReadOnlyList<VisitPhotoFaceScan> scans, CancellationToken cancellationToken)
    {
        if (scans.Count == 0) return new List<VisitPhotoFaceScanDto>();

        var scanIds = scans.Select(s => s.FaceScanId).ToList();
        var detections = await db.VisitPhotoFaceDetections.AsNoTracking()
            .Where(d => scanIds.Contains(d.FaceScanId))
            .OrderBy(d => d.FaceIndex)
            .ToListAsync(cancellationToken);

        var userIds = new HashSet<ulong>();
        foreach (var s in scans)
        {
            if (s.RequestedBy is { } rb) userIds.Add(rb);
            if (s.ConfirmedBy is { } cb) userIds.Add(cb);
        }
        foreach (var d in detections)
            if (d.ReviewedBy is { } rvb) userIds.Add(rvb);

        var userNames = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var guestIds = detections.Where(d => d.GuestMemberId.HasValue)
            .Select(d => d.GuestMemberId!.Value).Distinct().ToList();
        var guestNames = guestIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await db.VisitGuestMembers.AsNoTracking().Where(g => guestIds.Contains(g.GuestMemberId))
                .ToDictionaryAsync(g => g.GuestMemberId, g => g.FullName, cancellationToken);

        var detectionsByScan = detections.GroupBy(d => d.FaceScanId).ToDictionary(g => g.Key, g => g.ToList());

        return scans.Select(scan => new VisitPhotoFaceScanDto
        {
            FaceScanId = scan.FaceScanId,
            VisitPhotoId = scan.VisitPhotoId,
            Status = scan.Status,
            ProviderName = scan.ProviderName,
            ImageWidth = scan.ImageWidth,
            ImageHeight = scan.ImageHeight,
            DetectedFaceCount = scan.DetectedFaceCount,
            ReviewedFaceCount = scan.ReviewedFaceCount,
            IgnoredFaceCount = scan.IgnoredFaceCount,
            ErrorCode = scan.ErrorCode,
            ErrorMessage = scan.ErrorMessage,
            RequestedAt = scan.RequestedAt,
            RequestedByName = scan.RequestedBy is { } r && userNames.TryGetValue(r, out var rn) ? rn : null,
            CompletedAt = scan.CompletedAt,
            ConfirmedAt = scan.ConfirmedAt,
            ConfirmedByName = scan.ConfirmedBy is { } c && userNames.TryGetValue(c, out var cn) ? cn : null,
            RowVersion = scan.RowVersion,
            Detections = (detectionsByScan.TryGetValue(scan.FaceScanId, out var list) ? list : new List<VisitPhotoFaceDetection>())
                .Select(d => new VisitPhotoFaceDetectionDto
                {
                    FaceDetectionId = d.FaceDetectionId,
                    FaceIndex = d.FaceIndex,
                    BoundingBoxX = d.BoundingBoxX,
                    BoundingBoxY = d.BoundingBoxY,
                    BoundingBoxWidth = d.BoundingBoxWidth,
                    BoundingBoxHeight = d.BoundingBoxHeight,
                    DetectionConfidence = d.DetectionConfidence,
                    ReviewStatus = d.ReviewStatus,
                    GuestMemberId = d.GuestMemberId,
                    GuestFullName = d.GuestMemberId is { } g && guestNames.TryGetValue(g, out var gn) ? gn : null,
                    FaceTagId = d.FaceTagId,
                    ReviewedAt = d.ReviewedAt,
                    ReviewedByName = d.ReviewedBy is { } rv && userNames.TryGetValue(rv, out var rvn) ? rvn : null,
                }).ToList(),
        }).ToList();
    }
}
