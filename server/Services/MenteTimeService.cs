using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

/// <summary>
/// 法定保護ユーザー時刻制限 (Mente Time) サービス
/// — 原典: HMajRootServer::m_stMenteStartTime / m_stMenteEndTime
///         + HMajChnlServer::IsLimitPlayTime  (_LIMIT_PLAY_TIME 条件コンパイル)
///
/// 起動時に EVTCODEMAST (EVTCODE='5221', EVTNO=0) から
/// プレイ禁止時刻帯 (EvtStartDt 〜 EvtEndDt) をロードする。
/// チャンネル入室時に管理者以外のプレイヤーがこの時刻帯にアクセスすると拒否する。
///
/// EVTCODEMAST にレコードが無い、または DB 接続に失敗した場合は無効化される
/// (= 全プレイヤー入場可)。
/// </summary>
public class MenteTimeService
{
    // 原典: HMajDef.h EVTCD_LIMIT_PLAY_TIME / EVTNO_LIMIT_PLAY_TIME
    public const string EvtCodeLimitPlayTime = "5221";
    public const int    EvtNoLimitPlayTime   = 0;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MenteTimeService> _logger;

    private DateTime _startTime = DateTime.MinValue;
    private DateTime _endTime   = DateTime.MinValue;

    public MenteTimeService(IServiceScopeFactory scopeFactory, ILogger<MenteTimeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>起動時に EVTCODEMAST からメンテ時刻を読み込む</summary>
    public async Task InitAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
            var info = await repo.GetEvtCodeMastAsync(EvtCodeLimitPlayTime, EvtNoLimitPlayTime);
            if (info != null)
            {
                _startTime = info.EvtStartDt;
                _endTime   = info.EvtEndDt;
                _logger.LogInformation(
                    "MenteTimeService: limit window loaded. start={Start:yyyy-MM-dd HH:mm} end={End:yyyy-MM-dd HH:mm}",
                    _startTime, _endTime);
            }
            else
            {
                _logger.LogInformation("MenteTimeService: EVTCODEMAST(EVTCODE='{Code}') not found — disabled.",
                    EvtCodeLimitPlayTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MenteTimeService: failed to load EVTCODEMAST — disabled.");
        }
    }

    /// <summary>実行時にメンテ時刻を再ロードする</summary>
    public Task ReloadAsync() => InitAsync();

    /// <summary>
    /// 現在時刻がメンテナンス制限時間内かを判定する。
    /// 原典: HMajChnlServer::IsLimitPlayTime
    ///   if (ctStart <= ctNow && ctNow <= ctEnd) return TRUE;
    /// </summary>
    public bool IsLimitPlayTime()
    {
        if (_startTime == DateTime.MinValue || _endTime == DateTime.MinValue)
            return false;
        var now = DateTime.Now;
        return _startTime <= now && now <= _endTime;
    }
}
