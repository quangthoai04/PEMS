using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

public sealed class VisitPhotoFaceDetectionDto
{
    public ulong FaceDetectionId { get; set; }
    public uint FaceIndex { get; set; }
    public decimal BoundingBoxX { get; set; }
    public decimal BoundingBoxY { get; set; }
    public decimal BoundingBoxWidth { get; set; }
    public decimal BoundingBoxHeight { get; set; }
    public decimal DetectionConfidence { get; set; }

    /// <summary>DETECTED | CONFIRMED | IGNORED</summary>
    public string ReviewStatus { get; set; } = "DETECTED";
    public ulong? GuestMemberId { get; set; }
    public string? GuestFullName { get; set; }
    public ulong? FaceTagId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByName { get; set; }
}

public sealed class VisitPhotoFaceScanDto
{
    public ulong FaceScanId { get; set; }
    public ulong VisitPhotoId { get; set; }

    /// <summary>PENDING | PROCESSING | SUCCEEDED | FAILED | CONFIRMED</summary>
    public string Status { get; set; } = "PENDING";
    public string ProviderName { get; set; } = "GOOGLE_CLOUD_VISION";
    public uint? ImageWidth { get; set; }
    public uint? ImageHeight { get; set; }
    public uint DetectedFaceCount { get; set; }
    public uint ReviewedFaceCount { get; set; }
    public uint IgnoredFaceCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedByName { get; set; }
    public uint RowVersion { get; set; }
    public List<VisitPhotoFaceDetectionDto> Detections { get; set; } = new();
}

public sealed class TaggableGuestDto
{
    public ulong GuestMemberId { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>GUEST | EXTERNAL_SUPPORT</summary>
    public string MemberType { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
}

public static class FaceScanErrorCodes
{
    public const string Forbidden = "FACE_SCAN_FORBIDDEN";
    public const string ConfigInactive = "FACE_SCAN_CONFIG_INACTIVE";
    public const string PhotoNotActive = "FACE_SCAN_PHOTO_NOT_ACTIVE";
    public const string ScanInProgress = "FACE_SCAN_IN_PROGRESS";
    public const string ScanNotConfirmable = "FACE_SCAN_NOT_CONFIRMABLE";
    public const string ScanAlreadyConfirmed = "FACE_SCAN_ALREADY_CONFIRMED";
    public const string RowVersionMismatch = "FACE_SCAN_ROW_VERSION_MISMATCH";
    public const string IncompleteFaceList = "FACE_SCAN_INCOMPLETE_FACE_LIST";
    public const string UnknownFaceDetection = "FACE_SCAN_UNKNOWN_FACE_DETECTION";
    public const string DuplicateGuestInScan = "FACE_SCAN_DUPLICATE_GUEST";
    public const string GuestNotInInstance = "FACE_SCAN_GUEST_NOT_IN_INSTANCE";
    public const string InvalidFaceItem = "FACE_SCAN_INVALID_FACE_ITEM";
    public const string RateLimited = "FACE_SCAN_RATE_LIMITED";
    public const string QuotaExceeded = "FACE_SCAN_QUOTA_EXCEEDED";
}
