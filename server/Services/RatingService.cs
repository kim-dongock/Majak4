using MajakServer.Models.Player;
using MajakServer.Models.Protocol;

namespace MajakServer.Services;

/// <summary>
/// レーティング計算サービス — HMajRatingCommon 移植
/// </summary>
public class RatingService
{
    // NLevel 境界値 (コイン基準) — HMajRatingCommon.cpp s_llMajNLevel
    private static readonly long[] NLevelThresholds =
    {
        0L,         // 0
        1L,         // 1
        500L,       // 2
        1500L,      // 3
        3000L,      // 4
        10000L,     // 5
        30000L,     // 6
        100000L,    // 7
        500000L,    // 8
        1000000L,   // 9
        5000000L,   // 10
    };

    // SLevel 文字列 (NLevel インデックス対応) — PC_MAJAK2_HIST.sql V_SLEVEL 完全準拠
    private static readonly string[] SLevelNames =
    {
        "無一文",   // 0  (≤ 0)
        "金欠",     // 1  (1 ～ 499)
        "庶民",     // 2  (500 ～ 1499)
        "平民",     // 3  (1500 ～ 2999)
        "一般人",   // 4  (3000 ～ 9999)
        "中流",     // 5  (10000 ～ 29999)
        "上流",     // 6  (30000 ～ 99999)
        "金持ち",   // 7  (100000 ～ 499999)
        "富豪",     // 8  (500000 ～ 999999)
        "大富豪",   // 9  (1000000 ～ 4999999)
        "財閥",     // 10 (≥ 5000000)
    };

    // Experience 境界値 — s_nMajExperience
    private static readonly int[] ExperienceThresholds =
    {
        0, 75, 200, 400, 600, 850, 1100, 1400, 1700, 2000, 2300
    };

    /// <summary>GetNLevel — コインでレベル計算</summary>
    public int GetNLevel(long gamMoney)
    {
        for (int i = NLevelThresholds.Length - 1; i >= 0; i--)
            if (gamMoney >= NLevelThresholds[i]) return i;
        return 0;
    }

    /// <summary>GetSLevel — NLevel で文字列レベルを返す</summary>
    public static string GetSLevelName(int nLevel)
    {
        if (nLevel < 0 || nLevel >= SLevelNames.Length) return SLevelNames[0];
        return SLevelNames[nLevel];
    }

    public string GetSLevel(int nLevel) => GetSLevelName(nLevel);

    /// <summary>GetMoneyByRating — レーティングでコイン換算 (保険/参考用)</summary>
    public long GetMoneyByRating(int rating)
    {
        if (rating <= 0) return 0;
        if (rating < 1400)
            return (long)(100000 * Math.Pow(2, (double)(rating - 1400) / 100));
        else
            return (long)(100000 + 100000 * Math.Pow(2, (double)(rating - 1500) / 100));
    }

    /// <summary>GetExperience — アガリ/フジ点数で経験値増分計算</summary>
    /// <remarks>
    /// 原典: HMajRatingCommon::GetExperience
    ///   nGetExperience = (nHoraSoten * 3 + nHojuSoten) / 100
    ///   現在の経験値区分内で上限チェックあり
    /// </remarks>
    public int GetExperience(int currentExp, int horaSoten, int hojuSoten)
    {
        int add = (horaSoten * 3 + hojuSoten) / 100;
        for (int i = ExperienceThresholds.Length - 1; i >= 0; i--)
        {
            if (currentExp >= ExperienceThresholds[i])
            {
                // 原典: nExperience + nGetExperience < s_nMajExperience[i]
                return currentExp + add < ExperienceThresholds[i]
                    ? ExperienceThresholds[i] - currentExp
                    : add;
            }
        }
        return 0;
    }

    /// <summary>
    /// グレードモードレーティング計算 — CalcRating_MajakTypeGradeMode 移植
    ///
    /// 原典: HMajRoomServer::CalcRating_MajakTypeGradeMode (HMajRoomServer.cpp)
    ///   fMatchCountCorrect = MATCH_COUNT &lt;= nMatchCount
    ///                        ? PLAYNUM_CORRECT_HIGH
    ///                        : (1.0 - nMatchCount * PLAYNUM_CORRECT_LOW)
    ///   fSelfCorrect = (nRatingAve - nRating) / CORRECT_BASE
    ///   fGetRate = fMatchCountCorrect * (nPointSum + fSelfCorrect) * SCALE
    ///   nGetRate = (int)fGetRate
    ///   return currRating + nGetRate
    ///
    /// 【修正履歴】旧実装は Elo 式 (順位ベース) を使用していたが、
    ///   レガシー C++ は pointSum + 平均レート補正の方式のため修正。
    /// </summary>
    /// <param name="currRating">現在のレーティング</param>
    /// <param name="pointSum">対局スコア (ゲームポイント合計)</param>
    /// <param name="matchCnt">試合数 (補正係数に使用)</param>
    /// <param name="ratingAvg">全プレイヤーの平均レーティング</param>
    public int CalcGradeRating(int currRating, int pointSum, int matchCnt, int ratingAvg)
    {
        // 原典: RATING_CARC_MATCH_COUNT <= nMatchCount ? HIGH : (1.0 - nMatchCount * LOW)
        float matchCorrect = matchCnt >= GameConst.RatingCarcMatchCount
            ? (float)GameConst.RatingCarcPlayNumCorrectHigh
            : (float)(1.0 - matchCnt * GameConst.RatingCarcPlayNumCorrectLow);

        // 原典: (nRatingAve - nRating) / CORRECT_BASE
        float selfCorrect = (ratingAvg - currRating) / (float)GameConst.RatingCarcCorrectBase;

        // 原典: fMatchCountCorrect * ((float)nPointSum + fSelfCorrect) * SCALE
        float getRate = matchCorrect * (pointSum + selfCorrect) * (float)GameConst.RatingCarcScale;
        int nGetRate  = (int)getRate;

        return currRating + nGetRate;
    }

    /// <summary>プレイヤーレーティング/レベル更新 (共通処理)</summary>
    public void UpdatePlayerLevel(MajakPlayer player)
    {
        player.NLevel = GetNLevel(player.GamMoney);
        player.SLevel = GetSLevel(player.NLevel);
    }

    /// <summary>
    /// 段位モードチャンネル進入チェック — 公式段位ポイント表
    ///
    ///   'A' (通常卓)   : 四段未満
    ///   'B' (段位卓)   : 初段以上、所持5000円以上
    ///   'C' (高段位卓) : 四段以上、所持10000円以上
    ///   'D' (十段位卓) : 七段以上、所持30000円以上
    /// </summary>
    /// <param name="gradeLevel">プレイヤーの現在段位 (GRADE_LEVEL enum 相当の int)</param>
    /// <param name="gamMoney">プレイヤーの保有ゲームマネー</param>
    /// <param name="subId">チャンネルの subId (例: "0ZG6A")</param>
    /// <returns>進入許可なら true</returns>
    public bool CheckEnterGradeMode(int gradeLevel, long gamMoney, string subId)
    {
        if (string.IsNullOrEmpty(subId) || subId.Length < 5) return false;
        char chanelType = subId[4];

        return chanelType switch
        {
            'A' => gamMoney >= 500    && gradeLevel >= 0  && gradeLevel <= 12,
            'B' => gamMoney >= 5_000  && gradeLevel >= 10 && gradeLevel <= 18,
            'C' => gamMoney >= 10_000 && gradeLevel >= 13 && gradeLevel <= 18,
            'D' => gamMoney >= 30_000 && gradeLevel >= 16 && gradeLevel <= 18,
            _   => false,
        };
    }
}
