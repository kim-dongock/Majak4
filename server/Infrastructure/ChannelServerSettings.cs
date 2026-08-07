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
    /// Redis に保持するロビー接続リースの TTL。8 秒ごとのハートビートで更新する。
    /// </summary>
    public int LobbySessionLeaseSeconds { get; set; } = 90;

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
