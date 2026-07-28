using MajakServer.Infrastructure;
using MajakServer.Models.Protocol;

namespace MajakServer.Services;

/// <summary>
/// 管理者 ID チェック — 原典: HMajAdminId.h / HMajAdminId.cpp (_USE_ADMIN_ID 条件コンパイル)
///
/// 原典の HMajAdminIdInfo はシングルトンで MAJADMINIDLIST (MemberNo + AdminSts) をリスト保持し、
/// ADCMD_IsAdminId() でチェックしていた。
/// C# 版は起動時に Redis または MySQL の管理者マスターをロードしてメモリキャッシュする。
/// テーブルが存在しない環境では空のセットとして動作し、全プレイヤーを非管理者として扱う。
/// </summary>
public class AdminIdService
{
    private readonly MasterCacheService _masterCache;

    // キャッシュ: MemberNo → AdminSts
    private Dictionary<string, int> _adminMap = new(StringComparer.Ordinal);

    public AdminIdService(MasterCacheService masterCache)
    {
        _masterCache = masterCache;
    }

    /// <summary>起動時に管理者 ID リストをロードする</summary>
    public async Task InitAsync()
    {
        try
        {
            // Redis または DB から取得し、インプロセスキャッシュに保持
            var list = await _masterCache.GetAdminIdListAsync();
            var adminMap = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                var memberNo = ToLegacyMemberNo(item.MemberNo);
                if (memberNo is null || adminMap.ContainsKey(memberNo))
                {
                    continue;
                }

                adminMap[memberNo] = item.AdminSts;
            }

            _adminMap = adminMap;
        }
        catch
        {
            // テーブル未作成 / 接続エラー → 空のキャッシュで続行
            _adminMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 指定 MemberNo が管理者かどうかを判定する — 原典: ADCMD_IsAdminId()
    /// </summary>
    public bool IsAdminId(string memberNo)
    {
        var legacyMemberNo = ToLegacyMemberNo(memberNo);
        return legacyMemberNo is not null && _adminMap.ContainsKey(legacyMemberNo);
    }

    /// <summary>管理者のステータス値を取得する (0 = 非管理者)</summary>
    public int GetAdminStatus(string memberNo)
    {
        var legacyMemberNo = ToLegacyMemberNo(memberNo);
        return legacyMemberNo is not null && _adminMap.TryGetValue(legacyMemberNo, out int sts) ? sts : 0;
    }

    /// <summary>実行時に管理者リストを再ロードする</summary>
    public async Task ReloadAsync() => await InitAsync();

    private static string? ToLegacyMemberNo(string? memberNo)
    {
        if (string.IsNullOrEmpty(memberNo))
        {
            return null;
        }

        return memberNo.Length > GameConst.MemberNoLen
            ? memberNo[..GameConst.MemberNoLen]
            : memberNo;
    }
}
