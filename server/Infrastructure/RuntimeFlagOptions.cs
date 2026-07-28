namespace MajakServer.Infrastructure;

/// <summary>
/// ランタイムフラグ設定 — 原典: HMajRuntimeFlag.h / HMajRuntimeFlag.cpp
///
/// 原典の RuntimeFlag 名前空間は C++ グローバル変数で管理されていた。
/// C# 版は appsettings.json の "RuntimeFlag" セクションにバインドする。
///
/// appsettings.json 例:
/// "RuntimeFlag": {
///   "SecureLaunching":  true,
///   "Hangame2005":      false,
///   "BipSupport":       true,
///   "GameLog":          true,
///   "Mrs":              true,
///   "MrsV3":            true,
///   "LcsApi":           true,
///   "SecureSocket":     true,
///   "NetCafeIpCheck":   true
/// }
/// </summary>
public class RuntimeFlagOptions
{
    public const string SectionName = "RuntimeFlag";

    /// <summary>
    /// SecureLaunching — ランチャー認証を要求するか。
    /// false の場合はパスワード未送信でもチャンネル入場を許可する (デバッグ用)。
    /// 原典: RuntimeFlag::SecureLaunching
    /// </summary>
    public bool SecureLaunching { get; set; } = true;

    /// <summary>
    /// Hangame2005 — Hangame2005 プロトコルベースかどうか。
    /// 原典: RuntimeFlag::Hangame2005
    /// </summary>
    public bool Hangame2005 { get; set; } = false;

    /// <summary>
    /// BipSupport — BIP (プッシュ通知) サポート。
    /// 原典: RuntimeFlag::BIPSupport
    /// </summary>
    public bool BipSupport { get; set; } = true;

    /// <summary>
    /// GameLog — ゲームログ書き込みを行うか。
    /// 原典: RuntimeFlag::GameLog
    /// </summary>
    public bool GameLog { get; set; } = true;

    /// <summary>
    /// Mrs — MRS (マッチングリレーサービス) 使用。
    /// 原典: RuntimeFlag::MRS
    /// </summary>
    public bool Mrs { get; set; } = true;

    /// <summary>
    /// MrsV3 — MRS v3 使用。
    /// 原典: RuntimeFlag::MRSv3
    /// </summary>
    public bool MrsV3 { get; set; } = true;

    /// <summary>
    /// LcsApi — LCSAPI 使用。
    /// 原典: RuntimeFlag::LCSAPI
    /// </summary>
    public bool LcsApi { get; set; } = true;

    /// <summary>
    /// SecureSocket — セキュアソケット使用。
    /// 原典: RuntimeFlag::SecureSocket
    /// </summary>
    public bool SecureSocket { get; set; } = true;

    /// <summary>
    /// NetCafeIpCheck — ネットカフェ IP チェックを行うか。
    /// 原典: RuntimeFlag::NETCAFEIPCHK
    /// </summary>
    public bool NetCafeIpCheck { get; set; } = true;

    /// <summary>
    /// DebugEndAfterEast1 — ローカル検証用。東1局終了時に半荘終了扱いにする。
    /// </summary>
    public bool DebugEndAfterEast1 { get; set; } = false;
}
