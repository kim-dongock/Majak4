using MajakServer.Models.Game;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// ゲーム結果記録 — HMajDBObject::InsertMajak2Hist, UpdateResult_* 移植
/// </summary>
public class HistoryRepository
{
    private readonly LogRepository? _log;
    private readonly Func<string, Task<TransactionCodeMetadata?>>? _resolveTransactionCode;

    public HistoryRepository()
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public HistoryRepository(
        LogRepository log,
        LogDataContextFactory logDataContextFactory,
        GameDataContextFactory gameDataContextFactory,
        ILogger<HistoryRepository> logger)
    {
        _log = log;
        _resolveTransactionCode = eventCode =>
            TransactionCodeMetadataResolver.ResolveAsync(gameDataContextFactory, eventCode);
    }

    internal HistoryRepository(
        LogRepository log,
        Func<string, Task<TransactionCodeMetadata?>> resolveTransactionCode)
    {
        _log = log;
        _resolveTransactionCode = resolveTransactionCode;
    }

    /// <summary>
    /// game_session_log + game_player_result_log INSERT (MySQL 経路)
    /// HMajDBObject::InsertMajak2Hist — #ifndef _LOG_TO_MYSQL_DATABASE_ 経路
    /// </summary>
    public virtual async Task<long> InsertGameHistAsync(GameReport report)
    {
        ulong id = await RequireLog().InsertGameHistWithIdAsync(report);
        return checked((long)id);
    }

    /// <summary>
    /// MAJAK2TRAININGHIST INSERT — HMajDBObject::InsertMajak2TrainingHist.
    /// </summary>
    public virtual async Task InsertTrainingHistAsync(string channelId, int roomId,
        string roomOption, int playerCnt,
        (string MemberNo, int Point)[] players)
    {
        await RequireLog().InsertTrainingHistAsync(channelId, roomId, roomOption, playerCnt, players);
    }

    /// <summary>
    /// MAJAK3YAKUHIST INSERT — AgariRecInsert
    /// </summary>
    public virtual async Task InsertYakuHistAsync(string memberNo, string gameId, int yaku)
    {
        await RequireLog().InsertYakuHistAsync(memberNo, gameId, yaku);
    }

    /// <summary>
    /// GAMEMONEYHIST INSERT — WriteGameMoneyHist / InsertGameMoneyHist
    /// PROCODET から EVENTITLE/GAMEID/HISTVALID を取得する原典方式を使用
    /// </summary>
    public virtual async Task InsertGameMoneyHistAsync(
        string memberNo, string eventCode, long eventMoney,
        long preMoney, long afterMoney, string remoteAddr)
    {
        var metadata = await RequireTransactionCodeResolver()(eventCode);
        if (metadata is null) return;

        await RequireLog().InsertGameMoneyHistAsync(
            memberNo, eventCode, eventMoney, preMoney, afterMoney, remoteAddr,
            metadata.EventTitle, gameId: metadata.GameId, isValid: metadata.IsHistoryEnabled);
    }

    private LogRepository RequireLog()
        => _log ?? throw new InvalidOperationException("MySQL LogRepository is not configured.");

    private Func<string, Task<TransactionCodeMetadata?>> RequireTransactionCodeResolver()
        => _resolveTransactionCode
           ?? throw new InvalidOperationException("Transaction code metadata resolver is not configured.");
}
