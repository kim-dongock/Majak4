namespace MajakServer.Models.Protocol;

/// <summary>
/// グレードモード レベル最大ポイントテーブル
/// 公式段位ポイント表
///
/// 各グレードレベルの最大ポイントを返す。
/// GetDetailRecCommand の gradeMaxPoint フィールドに使用する。
/// </summary>
public static class GradeLevelTable
{
    // GradeLevel はプロトコル・DBともに 10級=0 ... 1級=9, 初段=10 ... 九段=18。
    private static readonly int[] MaxPointByGrade =
    [
        30, 30, 30, 30, 60, 60, 60, 90, 90, 90,
        600, 1200, 1200, 2400, 2400, 2400, 4800, 4800, 4800,
    ];

    /// <summary>
    /// グレードレベルに対応する最大ポイントを返す。
    /// 公式段位ポイント表の昇段ポイントを返す。
    /// </summary>
    public static int GetMaxPoint(int gradeLevel)
        => gradeLevel >= 0 && gradeLevel < MaxPointByGrade.Length
            ? MaxPointByGrade[gradeLevel]
            : 0;
}
