namespace MajakServer.Models.Protocol;

/// <summary>
/// グレードモード レベル最大ポイントテーブル
/// 原典: s_stLevelGradeMode[] in HMajCommon.h
///
/// 各グレードレベルの最大ポイントを返す。
/// GetDetailRecCommand の gradeMaxPoint フィールドに使用する。
/// </summary>
public static class GradeLevelTable
{
    // 原典 s_stLevelGradeMode: { m_nGradeLevel, m_nInitPoint, m_nMinPoint, m_nMaxPoint, m_bDownGrade }
    // GRADE_10_KYU=100, GRADE_9_KYU=101, ... GRADE_1_KYU=109,
    // GRADE_1_DAN=1,  GRADE_2_DAN=2, ..., GRADE_9_DAN=9
    private static readonly Dictionary<int, int> MaxPointByGrade = new()
    {
        [100] = 30,    // GRADE_10_KYU
        [101] = 30,    // GRADE_9_KYU
        [102] = 30,    // GRADE_8_KYU
        [103] = 30,    // GRADE_7_KYU
        [104] = 60,    // GRADE_6_KYU
        [105] = 60,    // GRADE_5_KYU
        [106] = 60,    // GRADE_4_KYU
        [107] = 90,    // GRADE_3_KYU
        [108] = 90,    // GRADE_2_KYU
        [109] = 90,    // GRADE_1_KYU
        [1]   = 600,   // GRADE_1_DAN
        [2]   = 1200,  // GRADE_2_DAN
        [3]   = 1200,  // GRADE_3_DAN
        [4]   = 2400,  // GRADE_4_DAN
        [5]   = 2400,  // GRADE_5_DAN
        [6]   = 2400,  // GRADE_6_DAN
        [7]   = 4800,  // GRADE_7_DAN
        [8]   = 4800,  // GRADE_8_DAN
        [9]   = 4800,  // GRADE_9_DAN
    };

    /// <summary>
    /// グレードレベルに対応する最大ポイントを返す。
    /// 原典: stDetailRec.m_nGradeMaxPoint = s_stLevelGradeMode[nIdx].m_nMaxPoint
    /// </summary>
    public static int GetMaxPoint(int gradeLevel)
        => MaxPointByGrade.TryGetValue(gradeLevel, out var v) ? v : 0;
}
