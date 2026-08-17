using MajakServer.Services;

namespace MajakServer.Infrastructure;

public sealed class PaifuArchiveUploadBackgroundService : BackgroundService
{
    private readonly PaifuArchiveUploadQueue _queue;
    private readonly PaifuFileService _paifuFiles;
    private readonly ILogger<PaifuArchiveUploadBackgroundService> _log;

    public PaifuArchiveUploadBackgroundService(
        PaifuArchiveUploadQueue queue,
        PaifuFileService paifuFiles,
        ILogger<PaifuArchiveUploadBackgroundService> log)
    {
        _queue = queue;
        _paifuFiles = paifuFiles;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _paifuFiles.StoreCompletedGameAsync(item, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Background paifu archive write failed. roomId={RoomId}", item.RoomId);
            }
        }
    }
}