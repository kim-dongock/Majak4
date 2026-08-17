using System.Threading.Channels;

namespace MajakServer.Services;

public sealed class PaifuArchiveUploadQueue
{
    private readonly Channel<PaifuArchiveWorkItem> _items = Channel.CreateUnbounded<PaifuArchiveWorkItem>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public bool TryEnqueue(PaifuArchiveWorkItem item) => _items.Writer.TryWrite(item);

    public IAsyncEnumerable<PaifuArchiveWorkItem> ReadAllAsync(CancellationToken cancellationToken)
        => _items.Reader.ReadAllAsync(cancellationToken);
}