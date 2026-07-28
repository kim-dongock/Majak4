namespace MajakServer.Infrastructure;

public sealed class GameAuthSettings
{
    public const string SectionName = "GameAuth";

    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "majak2-game";
    public string JwtAudience { get; set; } = "majak2-client";
    public int AccessTokenMinutes { get; set; } = 15;
}
