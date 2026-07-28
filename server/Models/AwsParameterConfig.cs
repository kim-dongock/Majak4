namespace MajakServer.Models;

/// <summary>
/// AWS Parameter Store から取得する接続情報
/// 各フィールドは HangameCrypto で暗号化された Base64 文字列
/// </summary>
public class AwsParameterConfig
{
    // MySQL ゲーム DB 接続情報
    public string mysql_game_host     { get; set; } = string.Empty;
    public string mysql_game_port     { get; set; } = string.Empty;
    public string mysql_game_db       { get; set; } = string.Empty;
    public string mysql_game_user     { get; set; } = string.Empty;
    public string mysql_game_password { get; set; } = string.Empty;

    // MySQL ログ DB 接続情報
    public string mysql_log_host     { get; set; } = string.Empty;
    public string mysql_log_port     { get; set; } = string.Empty;
    public string mysql_log_db       { get; set; } = string.Empty;
    public string mysql_log_user     { get; set; } = string.Empty;
    public string mysql_log_password { get; set; } = string.Empty;

    // 旧ログ DB 接続情報。mysql_log_* 未設定時のみフォールバックする。
    public string mysql_host     { get; set; } = string.Empty;
    public string mysql_port     { get; set; } = string.Empty;
    public string mysql_db       { get; set; } = string.Empty;
    public string mysql_user     { get; set; } = string.Empty;
    public string mysql_password { get; set; } = string.Empty;
}
