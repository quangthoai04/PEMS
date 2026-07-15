using System;
using System.IO;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicGalleryMediaStream;

/// <summary>
/// Anonymous, gallery-scoped file proxy that <b>streams</b> the bytes (optionally a byte range) instead
/// of buffering the whole file — needed to serve an area cover MP4 video for &lt;video&gt; playback
/// without loading it all into memory (UC §13). Authorization is identical to the buffered proxy
/// (public-visible gallery media / active cover / READY narration); anything else is a controlled 404.
/// </summary>
public sealed record GetPublicGalleryMediaStreamQuery(ulong FileId, long? RangeFrom, long? RangeTo)
    : IRequest<PublicGalleryMediaStreamResult>;

/// <summary>
/// A live streamed response for the public media proxy. <see cref="Stream"/> reads straight from the
/// underlying store (Google Drive / disk) and, together with the whole result, MUST be disposed by the
/// controller after the body has been written.
/// </summary>
public sealed class PublicGalleryMediaStreamResult : IAsyncDisposable, IDisposable
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }

    /// <summary>Total size of the whole file in bytes, when known.</summary>
    public long? TotalLength { get; init; }

    /// <summary>Number of bytes in this response body, when known.</summary>
    public long? ContentLength { get; init; }

    /// <summary>Inclusive start offset of the returned slice.</summary>
    public long RangeStart { get; init; }

    /// <summary>Inclusive end offset of the returned slice.</summary>
    public long RangeEnd { get; init; }

    /// <summary>True when this is a 206 Partial Content response for a requested range.</summary>
    public bool IsPartial { get; init; }

    /// <summary>Whether the endpoint should advertise <c>Accept-Ranges: bytes</c> for this file.</summary>
    public bool SupportsRange { get; init; }

    private IAsyncDisposable? _owned;

    /// <summary>Attaches an extra resource (e.g. the Drive download) to dispose together with this result.</summary>
    public PublicGalleryMediaStreamResult Owning(IAsyncDisposable owned)
    {
        _owned = owned;
        return this;
    }

    public async ValueTask DisposeAsync()
    {
        if (_owned is not null) await _owned.DisposeAsync();
        else await Stream.DisposeAsync();
    }

    public void Dispose()
    {
        if (_owned is not null) _owned.DisposeAsync().AsTask().GetAwaiter().GetResult();
        else Stream.Dispose();
    }
}
