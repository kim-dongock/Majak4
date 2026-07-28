using Google.Apis.Auth;
using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MajakServer.Services;

/// <summary>
/// 管理サイト認証サービス。
/// Google ID トークンを検証し、サーバー発行 JWT を返す。
/// </summary>
public class AdminAuthService
{
    private readonly AdminSettings _settings;
    private readonly AdminRepository _adminRepo;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        IOptions<AdminSettings> settings,
        AdminRepository adminRepo,
        ILogger<AdminAuthService> logger)
    {
        _settings = settings.Value;
        _adminRepo = adminRepo;
        _logger = logger;
    }

    // ─── Google ID トークン検証 ───────────────────────────────────────────
    public async Task<AdminLoginResult> LoginWithGoogleAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.GoogleClientId))
            return AdminLoginResult.Fail("GoogleClientId is not configured.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_settings.GoogleClientId]
                });
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("Google ID token validation failed: {Msg}", ex.Message);
            return AdminLoginResult.Fail("Invalid Google token.");
        }

        var email = payload.Email?.ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
            return AdminLoginResult.Fail("Email not found in token.");

        // DB から管理者アカウントを取得、なければ設定ファイルの AllowedEmails をフォールバック
        var account = await _adminRepo.FindAdminAccountAsync(email);
        if (account is null)
        {
            var inConfig = _settings.AllowedEmails
                .Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (!inConfig)
            {
                _logger.LogWarning("Unauthorized admin login attempt: {Email}", email);
                return AdminLoginResult.Fail("Account not authorized.");
            }
            // 設定ファイルのみに存在 → operator ロールで自動登録
            account = await _adminRepo.UpsertAdminAccountAsync(email, "operator");
        }

        if (!account.IsActive)
            return AdminLoginResult.Fail("Account is disabled.");

        var jwt = IssueJwt(account.AdminNo, account.Email, account.Role);
        _logger.LogInformation("Admin login: adminNo={AdminNo} ({Role})", account.AdminNo, account.Role);
        return AdminLoginResult.Ok(jwt, account.AdminNo, account.Email, account.Role);
    }

    // ─── JWT 発行 ────────────────────────────────────────────────────────
    private string IssueJwt(ulong adminNo, string email, string role)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(_settings.JwtExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   adminNo.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("admin_no", adminNo.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:   _settings.JwtIssuer,
            audience: _settings.JwtAudience,
            claims:   claims,
            expires:  expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ─── JWT 検証 (ミドルウェア用) ───────────────────────────────────────
    public ClaimsPrincipal? ValidateJwt(string jwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret));
            var result  = handler.ValidateToken(jwt, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = _settings.JwtIssuer,
                ValidateAudience         = true,
                ValidAudience            = _settings.JwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.FromSeconds(30),
            }, out _);
            return result;
        }
        catch
        {
            return null;
        }
    }
}

// ─── 結果 DTO ─────────────────────────────────────────────────────────────
public record AdminLoginResult(bool Success, string? Token, ulong? AdminNo, string? Email, string? Role, string? Error)
{
    public static AdminLoginResult Ok(string token, ulong adminNo, string email, string role)
        => new(true, token, adminNo, email, role, null);
    public static AdminLoginResult Fail(string error)
        => new(false, null, null, null, null, error);
}
