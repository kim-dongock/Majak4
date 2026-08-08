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
    public sealed record CollectionTitle(string TitleId, string TitleName, bool IsEquipped);

    private readonly PlayerRepository  _playerRepo;
    private readonly MasterCacheService _masterCache;

    // TitleService は scoped のため、起動時にロードしたスナップショットを全 scope で共有する。
    private static IReadOnlyDictionary<string, string> _titleCache = new Dictionary<string, string>();

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

    public async Task<(List<CollectionTitle> MajakTitles, List<CollectionTitle> TrickTitles)>
        GetCollectionAsync(MajakPlayer player)
    {
        var ownedTitleIds = await _playerRepo.GetTitleListAsync(player.MemberNo);
        var majakTitles = new List<CollectionTitle>();
        var trickTitles = new List<CollectionTitle>();
        foreach (string titleId in ownedTitleIds.Distinct().OrderBy(titleId => titleId))
        {
            if (!_titleCache.TryGetValue(titleId, out string? titleName)) continue;
            if (IsTrickTitle(titleId))
                trickTitles.Add(new CollectionTitle(titleId, titleName, titleId == player.TrickTitle));
            else if (IsMajakTitle(titleId))
                majakTitles.Add(new CollectionTitle(titleId, titleName, titleId == player.MajakTitle));
        }
        return (majakTitles, trickTitles);
    }

    public async Task<bool> EquipOwnedTitleAsync(MajakPlayer player, bool isTrick, string? titleId)
    {
        string normalizedTitleId = titleId?.Trim() ?? "";
        if (normalizedTitleId.Length > 0)
        {
            if (isTrick ? !IsTrickTitle(normalizedTitleId) : !IsMajakTitle(normalizedTitleId)) return false;
            if (!_titleCache.ContainsKey(normalizedTitleId)) return false;
            if (!await _playerRepo.HasActiveTitleAsync(player.MemberNo, normalizedTitleId)) return false;
        }

        if (!await _playerRepo.UpdateEquippedTitleAsync(player.MemberNo, isTrick, normalizedTitleId)) return false;
        if (isTrick)
        {
            player.TrickTitle = normalizedTitleId;
            player.TrickTitleId = ToTitleCode(normalizedTitleId, "mjks");
        }
        else
        {
            player.MajakTitle = normalizedTitleId;
            player.MajakTitleId = normalizedTitleId.StartsWith("mjkc", StringComparison.Ordinal)
                ? 1000 + ToTitleCode(normalizedTitleId, "mjkc")
                : ToTitleCode(normalizedTitleId, "mjkt");
        }
        return true;
    }

    private static bool IsTrickTitle(string titleId)
        => titleId.StartsWith("mjks", StringComparison.Ordinal);

    private static bool IsMajakTitle(string titleId)
        => titleId.StartsWith("mjkt", StringComparison.Ordinal)
            || titleId.StartsWith("mjkc", StringComparison.Ordinal);

    private static int ToTitleCode(string titleId, string prefix)
        => titleId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(titleId[prefix.Length..], out int code) ? code : 0;

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
