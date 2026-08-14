using MajakServer.Services;

namespace MajakServer.Infrastructure;

public sealed class PaifuArchiveCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly PaifuFileService _paifuFiles;
    private readonly ILogger<PaifuArchiveCleanupBackgroundService> _log;

    public PaifuArchiveCleanupBackgroundService(PaifuFileService paifuFiles, ILogger<PaifuArchiveCleanupBackgroundService> log)
    {
        _paifuFiles = paifuFiles;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _paifuFiles.RemoveExpiredAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Expired paifu archive cleanup failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
