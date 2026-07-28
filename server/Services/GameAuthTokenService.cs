using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MajakServer.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MajakServer.Services;

public sealed class GameAuthTokenService
{
    private readonly GameAuthSettings _settings;

    public GameAuthTokenService(IOptions<GameAuthSettings> settings)
    {
        _settings = settings.Value;
    }

    public string IssueAccessToken(string memberNo, string pix)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ResolveSecret()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(Math.Clamp(_settings.AccessTokenMinutes, 1, 1440));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, memberNo),
            new Claim("member_no", memberNo),
            new Claim("pix", pix),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _settings.JwtIssuer,
            audience: _settings.JwtAudience,
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public GameAuthPrincipal? Validate(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;
        if (jwt.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            jwt = jwt[7..].Trim();

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ResolveSecret()));
            var principal = handler.ValidateToken(jwt, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _settings.JwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);

            var memberNo = principal.FindFirstValue("member_no") ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(memberNo)) return null;
            return new GameAuthPrincipal(memberNo, principal.FindFirstValue("pix") ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    private string ResolveSecret()
    {
        if (!string.IsNullOrWhiteSpace(_settings.JwtSecret)) return _settings.JwtSecret;
        throw new InvalidOperationException("GameAuth:JwtSecret is not configured.");
    }
}

public sealed record GameAuthPrincipal(string MemberNo, string Pix)
{
    public bool HasNumericMemberNo => ulong.TryParse(MemberNo, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
