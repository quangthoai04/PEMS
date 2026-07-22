using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Commands.ConfirmFaceTags;

public sealed class ConfirmFaceTagsCommandHandler : IRequestHandler<ConfirmFaceTagsCommand, VisitPhotoFaceScanDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public ConfirmFaceTagsCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<VisitPhotoFaceScanDto> Handle(ConfirmFaceTagsCommand request, CancellationToken cancellationToken)
    {
        var scan = await _db.VisitPhotoFaceScans
            .FirstOrDefaultAsync(s => s.FaceScanId == request.FaceScanId, cancellationToken)
            ?? throw new NotFoundException("VisitPhotoFaceScan", request.FaceScanId);

        var scope = await VisitPhotoFaceScanAccess.ResolveStaffAsync(
            _db, _currentUser, scan.VisitInstanceId, cancellationToken);
        var userId = scope.UserId;

        if (scan.RowVersion != request.RowVersion)
            throw new ConflictException(
                "Dữ liệu lần quét đã thay đổi — vui lòng tải lại trước khi xác nhận.",
                FaceScanErrorCodes.RowVersionMismatch);

        if (scan.Status == VisitPhotoFaceScan.StatusConfirmed)
            throw new ConflictException(
                "Lần quét này đã được xác nhận trước đó.", FaceScanErrorCodes.ScanAlreadyConfirmed);
        if (scan.Status != VisitPhotoFaceScan.StatusSucceeded)
            throw new BusinessRuleException(
                "Chỉ xác nhận được khi lần quét đã hoàn tất thành công.", FaceScanErrorCodes.ScanNotConfirmable);

        var detections = await _db.VisitPhotoFaceDetections
            .Where(d => d.FaceScanId == scan.FaceScanId)
            .ToListAsync(cancellationToken);
        var detectionById = detections.ToDictionary(d => d.FaceDetectionId);

        // One guest cannot appear twice in the same scan.
        var guestIds = request.Faces
            .Where(f => !f.Ignored && f.GuestMemberId.HasValue)
            .Select(f => f.GuestMemberId!.Value)
            .ToList();
        if (guestIds.Distinct().Count() != guestIds.Count)
            throw new BusinessRuleException(
                "Một khách không được gán cho hai khuôn mặt trong cùng lần quét.",
                FaceScanErrorCodes.DuplicateGuestInScan);

        // Anti-IDOR: re-check every guest belongs to THIS exact visit instance.
        Dictionary<ulong, VisitGuestMember> guestById = new();
        if (guestIds.Count > 0)
        {
            var distinctGuestIds = guestIds.Distinct().ToList();
            var validGuests = await _db.VisitInstanceGuestMembers.AsNoTracking()
                .Where(l => l.VisitInstanceId == scan.VisitInstanceId && distinctGuestIds.Contains(l.GuestMemberId))
                .Select(l => l.GuestMember)
                .ToListAsync(cancellationToken);
            if (validGuests.Count != distinctGuestIds.Count)
                throw new BusinessRuleException(
                    "Một hoặc nhiều khách không thuộc đúng đoàn của chuyến thăm này.",
                    FaceScanErrorCodes.GuestNotInInstance);
            guestById = validGuests.ToDictionary(g => g.GuestMemberId);
        }

        var now = _clock.VietnamNow;
        var requestFaceDict = request.Faces.ToDictionary(f => f.FaceDetectionId);

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var detection in detections)
            {
                requestFaceDict.TryGetValue(detection.FaceDetectionId, out var item);
                var isGuest = item != null && !item.Ignored && item.GuestMemberId.HasValue;

                if (!isGuest || !guestById.TryGetValue(item!.GuestMemberId!.Value, out var guest))
                {
                    detection.ReviewStatus = VisitPhotoFaceDetection.ReviewStatusIgnored;
                    detection.GuestMemberId = null;
                    detection.FaceTagId = null;
                    detection.ReviewedBy = userId;
                    detection.ReviewedAt = now;
                }
                else
                {
                    var tag = new PhotoFaceTag
                    {
                        FileId = scan.FileId,
                        VisitRequestId = scan.VisitRequestId,
                        GuestMemberId = guest.GuestMemberId,
                        DisplayName = guest.FullName,
                        PersonNameKey = PartnerNormalization.NormalizeKey(guest.FullName),
                        BoundingBoxX = Math.Round(detection.BoundingBoxX, 4),
                        BoundingBoxY = Math.Round(detection.BoundingBoxY, 4),
                        BoundingBoxWidth = Math.Round(detection.BoundingBoxWidth, 4),
                        BoundingBoxHeight = Math.Round(detection.BoundingBoxHeight, 4),
                        TagStatus = "ACTIVE",
                        CreatedAt = now,
                        CreatedBy = userId,
                    };
                    _db.PhotoFaceTags.Add(tag);
                    await _db.SaveChangesAsync(cancellationToken); // assigns FaceTagId

                    detection.ReviewStatus = VisitPhotoFaceDetection.ReviewStatusConfirmed;
                    detection.GuestMemberId = guest.GuestMemberId;
                    detection.FaceTagId = tag.FaceTagId;
                    detection.ReviewedBy = userId;
                    detection.ReviewedAt = now;
                }
            }

            scan.ReviewedFaceCount = (uint)detections.Count(d => d.ReviewStatus == VisitPhotoFaceDetection.ReviewStatusConfirmed);
            scan.IgnoredFaceCount = (uint)detections.Count(d => d.ReviewStatus == VisitPhotoFaceDetection.ReviewStatusIgnored);
            scan.Status = VisitPhotoFaceScan.StatusConfirmed;
            scan.ConfirmedBy = userId;
            scan.ConfirmedAt = now;
            scan.UpdatedAt = now;
            scan.RowVersion += 1;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                Action = "VISIT_PHOTO_FACE_TAGS_CONFIRMED",
                EntityType = "VisitPhotoFaceScan",
                EntityId = scan.FaceScanId,
                VisitRequestId = scan.VisitRequestId,
                VisitInstanceId = scan.VisitInstanceId,
                Reason = $"confirmed={scan.ReviewedFaceCount},ignored={scan.IgnoredFaceCount}",
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await FaceScanMapper.ToDtoAsync(_db, scan.FaceScanId, cancellationToken);
    }
}
