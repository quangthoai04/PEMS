using System.Threading.Channels;

namespace PEMS.Application.Galleries.Tts;

/// <summary>
/// In-process work queue between the request path (ensure/regenerate insert a PENDING
/// gallery_item_tts_audios row and enqueue its id) and the background worker that talks to EverAI.
/// Jobs also live in the DB, so anything lost from this queue on a restart is re-enqueued by the
/// worker's periodic sweep — the queue is a latency optimization, not the source of truth.
/// </summary>
public interface IGalleryTtsJobQueue
{
    void Enqueue(long ttsAudioId);

    /// <summary>Waits for the next job id (cancels with the worker's stopping token).</summary>
    ValueTask<long> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>Unbounded channel implementation (registered as a singleton; single background reader).</summary>
public sealed class GalleryTtsJobQueue : IGalleryTtsJobQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public void Enqueue(long ttsAudioId) => _channel.Writer.TryWrite(ttsAudioId);

    public ValueTask<long> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
