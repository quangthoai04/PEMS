using System;
using System.IO;

namespace PEMS.Application.Common.Models;

/// <summary>
/// A live, streamed Google Drive download — used to serve larger binaries (e.g. an area cover video)
/// without buffering the whole file into memory. <see cref="Stream"/> reads straight from the Drive
/// response and MUST be disposed by the caller (disposing it also releases the underlying HTTP
/// response). When a byte range was requested and Drive honoured it, <see cref="IsPartial"/> is true and
/// <see cref="RangeStart"/>/<see cref="RangeEnd"/>/<see cref="TotalLength"/> describe the slice.
/// </summary>
public sealed class GoogleDriveDownloadResult : IDisposable, IAsyncDisposable
{
    public required Stream Stream { get; init; }

    /// <summary>Total size of the whole file in bytes, when known (from Content-Range / Content-Length).</summary>
    public long? TotalLength { get; init; }

    /// <summary>Number of bytes in this response body, when known.</summary>
    public long? ContentLength { get; init; }

    /// <summary>Inclusive start offset of the returned slice (0 for a full response).</summary>
    public long RangeStart { get; init; }

    /// <summary>Inclusive end offset of the returned slice.</summary>
    public long RangeEnd { get; init; }

    /// <summary>True when Drive returned 206 Partial Content for a requested range.</summary>
    public bool IsPartial { get; init; }

    public string? ContentType { get; init; }

    public void Dispose() => Stream.Dispose();

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}
