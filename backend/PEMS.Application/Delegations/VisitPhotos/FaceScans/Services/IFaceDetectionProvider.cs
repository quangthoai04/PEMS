using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Services;

/// <summary>
/// Cloud face-detection provider abstraction (Google Cloud Vision FACE_DETECTION). Implementations
/// live in Infrastructure and are the ONLY code allowed to talk to the cloud endpoint — the frontend
/// never calls the provider directly. Returns normalized (0..1) bounding boxes only: no landmark,
/// no emotion/likelihood field, no raw response and no face embedding is ever produced or stored.
/// </summary>
public interface IFaceDetectionProvider
{
    Task<FaceDetectionProviderResult> DetectFacesAsync(
        FaceDetectionProviderInput input,
        FaceDetectionRuntimeConfig config,
        CancellationToken cancellationToken);

    /// <summary>Verifies the endpoint/credential/project are usable without a real business photo.</summary>
    Task<FaceDetectionConnectionTestResult> TestConnectionAsync(
        FaceDetectionRuntimeConfig config,
        CancellationToken cancellationToken);
}

public sealed class FaceDetectionProviderInput
{
    public byte[] FileBytes { get; init; } = System.Array.Empty<byte>();
    public string MimeType { get; init; } = string.Empty;
}

public sealed class FaceDetectionProviderResult
{
    public string ProviderName { get; init; } = "GOOGLE_CLOUD_VISION";
    public string? ProviderRequestId { get; init; }
    public uint ImageWidth { get; init; }
    public uint ImageHeight { get; init; }
    public IReadOnlyList<DetectedFaceDto> Faces { get; init; } = System.Array.Empty<DetectedFaceDto>();
}

/// <summary>One detected face, already normalized to a 0..1 axis-aligned box.</summary>
public sealed class DetectedFaceDto
{
    public decimal BoundingBoxX { get; init; }
    public decimal BoundingBoxY { get; init; }
    public decimal BoundingBoxWidth { get; init; }
    public decimal BoundingBoxHeight { get; init; }
    public decimal DetectionConfidence { get; init; }
}

public sealed class FaceDetectionConnectionTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
}
