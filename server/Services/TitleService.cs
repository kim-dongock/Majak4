using MajakServer.Infrastructure;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

/// <summary>
/// 称号取得/適用サービス — HMajDBObject 称号関連移植
/// </summary>
public class TitleService
{
    private readonly PlayerRepository  _playerRepo;
    private readonly MasterCacheService _masterCache;

    // 起動時インプロセスキャッシュ (Redis 未接続時のフォールバック)
    private Dictionary<string, string> _titleCache = new();

    public TitleService(PlayerRepository playerRepo, MasterCacheService masterCache)
    {
        _playerRepo  = playerRepo;
        _masterCache = masterCache;
    }

    public async Task InitAsync()
    {
        // Redis または DB からデータを取得し、インプロセスキャッシュに保持
        _titleCache = await _masterCache.GetTitleMastAsync();
    }

    public string? GetTitleName(string titleId)
        => _titleCache.TryGetValue(titleId, out var n) ? n : null;

    /// <summary>
    /// HMajTitleInfo::GetTitleName(type, code) 相当。
    /// type: 0=トリック称号(mjks), 1=麻雀称号(mjkt/mjkc)
    /// </summary>
    public string GetTitleName(int titleType, int titleCode)
    {
        string titleId = titleType == 1 && titleCode >= 1000
            ? $"mjkc{titleCode - 1000:000}"
            : $"mjk{(titleType == 0 ? 's' : 't')}{titleCode:000}";
        return _titleCache.TryGetValue(titleId, out var name) ? name : "";
    }

    /// <summary>
    /// 称号取得処理 — ProcessCommand_GetTitle 移植
    /// titleType: 1=トリック, 2=麻雀
    /// </summary>
    public async Task<(bool Ok, string TrickTitle, string MajakTitle, string TitleName)>
        GetTitleAsync(MajakPlayer player, int titleType, string titleCode)
    {
        // 称号有効性確認
        if (!_titleCache.ContainsKey(titleCode))
            return (false, player.TrickTitle, player.MajakTitle, "");

        await _playerRepo.InsertOrEnableTitleAsync(player.MemberNo, titleCode);

        string titleName = _titleCache.GetValueOrDefault(titleCode, "");

        if (titleType == 1) // トリック称号 (mjks*)
        {
            player.TrickTitle = titleCode;
            if (int.TryParse(titleCode.Replace("mjks", ""), out int t))
                player.TrickTitleId = t;
        }
        else if (titleType == 2) // 麻雀称号 (mjkt* or mjkc*)
        {
            player.MajakTitle = titleCode;
        }

        // MJKCOMMONRAT.TRICKTITLE / MAJAKTITLE を更新
        await UpdateTitlesInDbAsync(player);

        return (true, player.TrickTitle, player.MajakTitle, titleName);
    }

    /// <summary>MJKCOMMONRAT の称号カラムを更新</summary>
    private async Task UpdateTitlesInDbAsync(MajakPlayer player)
    {
        // PlayerRepository に称号専用 UPDATE がないため UpdateCommonRat を再利用
        // (UpdateCommonRat は SLEVEL, NLEVEL 等も更新するため十分)
        await _playerRepo.UpdateCommonRatAsync(player);
    }

    /// <summary>グレードモード初期称号付与 (10級) — RATING_TITLE_10KYU</summary>
    public async Task EnsureInitialGradeTitleAsync(MajakPlayer player)
    {
        await _playerRepo.InsertOrEnableTitleAsync(player.MemberNo, GameConst.RatingTitle10Kyu);
    }
}
