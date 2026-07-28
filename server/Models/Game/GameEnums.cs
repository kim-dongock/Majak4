global using HoraYaku = MajakServer.Engine.HoraYaku;
global using KyokuEnd = MajakServer.Engine.KyokuEnd;

namespace MajakServer.Models.Game;

/// <summary>
/// ゲーム終了タイプ — 原典: MajakDef.h GAMEEND enum
/// (Engine.GameEnd と同値; Models 層でのみ使用)
/// </summary>
public enum GameEnd
{
    None  = 0,
    Set   = 1,
    Stop  = 2,
    Tobi  = 3,
    Hora  = 4,
}

/// <summary>
/// プレイヤーアクションタイプ — 原典: MajakDef.h ACT enum
/// (Engine.Act と同値)
/// </summary>
public enum Act
{
    Inv, Pas, Chi, Pon, Kan, Ron, Tap,
    Ank, Cha, Ric, Tao, Tsu, Hua,
    Shu, Kou, Lbu,
}

/// <summary>
/// ゲームルーム状態
/// </summary>
public enum GameRoomState
{
    Waiting   = 0,  // 待機中
    Starting  = 1,  // 開始待機 (OKボタン収集中)
    Playing   = 2,  // ゲーム中
    Finished  = 3,  // 結果処理中
}
