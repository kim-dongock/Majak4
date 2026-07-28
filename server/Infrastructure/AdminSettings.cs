namespace MajakServer.Infrastructure;

/// <summary>
/// 管理サイト認証・認可設定。
/// appsettings.json / Parameter Store の "AdminSettings" セクション。
/// </summary>
public class AdminSettings
{
    public const string SectionName = "AdminSettings";

    /// <summary>Google OAuth 2.0 クライアント ID (管理サイト用)。</summary>
    public string GoogleClientId { get; set; } = string.Empty;

    /// <summary>サーバー発行 JWT の署名シークレット (最低 32 文字)。</summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>JWT issuer クレーム値。</summary>
    public string JwtIssuer { get; set; } = "majak2-admin";

    /// <summary>JWT audience クレーム値。</summary>
    public string JwtAudience { get; set; } = "majak2-admin-site";

    /// <summary>JWT 有効時間 (分)。デフォルト 480 分 = 8 時間。</summary>
    public int JwtExpiryMinutes { get; set; } = 480;

    /// <summary>
    /// 起動時ブートストラップ用の許可 Google アカウントリスト。
    /// DB の admin_account テーブルが優先される。
    /// 空の場合は DB のみで管理する。
    /// </summary>
    public string[] AllowedEmails { get; set; } = [];
}
