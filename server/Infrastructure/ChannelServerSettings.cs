namespace MajakServer.Infrastructure;

/// <summary>
/// チャンネル → ゲームサーバー URL マッピング設定
/// AP-04 §8 参照
///
/// appsettings.json 例:
/// "ChannelServerSettings": {
///   "ServerUrl": "https://game.majak2.jp",
///   "ChannelUrlMap": {
///     "MAJAK20090A001": "https://game1.majak2.jp"
///   }
/// }
///
/// ChannelUrlMap にエントリがないチャンネル ID には ServerUrl を返す。
/// 開発環境では ServerUrl = "http://localhost:5000" を使用する。
/// </summary>
public class ChannelServerSettings
{
    public const string SectionName = "ChannelServerSettings";

    /// <summary>
    /// このサーバーインスタンスの公開 URL。
    /// ChannelUrlMap に該当エントリがない場合のデフォルト値。
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// このサーバーインスタンスがプライマリサーバーかどうか。
    ///
    /// true  (デフォルト): Cup・Tournament タイマーを実行する。
    ///         → 単一サーバー構成では常に true。
    /// false: Cup・Tournament タイマーを実行しない。
    ///         → 複数サーバー構成で、1台だけ true にして残りを false にする。
    ///
    /// レガシー相当: HMajChnlServer は CHANELMAST.MACHINE で自サーバー担当チャンネルのみ
    ///              タイマーを起動していた。C# 版では単純フラグで代替する。
    /// </summary>
    public bool IsPrimaryServer { get; set; } = true;

    /// <summary>
    /// 対局中に全プレイヤーが切断状態になったルームを、復帰可能状態として保持する秒数。
    /// ブラウザ更新・一時的な回線断はこの間に同じ座席へ再接続できる。
    /// </summary>
    public int ContinueRoomGraceSeconds { get; set; } = 300;

    /// <summary>
    /// チャンネル ID → サーバー URL の個別マッピング。
    /// マルチサーバー構成時に特定チャンネルを別サーバーに振り分ける場合に使用する。
    /// 省略可。
    /// </summary>
    public Dictionary<string, string> ChannelUrlMap { get; set; } = new();

    /// <summary>
    /// 指定チャンネル ID に対応するサーバー URL を返す。
    /// ChannelUrlMap にエントリがなければ ServerUrl を返す。
    /// </summary>
    public string ResolveUrl(string chanelId)
    {
        if (!string.IsNullOrWhiteSpace(chanelId)
            && ChannelUrlMap.TryGetValue(chanelId, out var mapped))
        {
            return mapped;
        }
        return ServerUrl;
    }
}
