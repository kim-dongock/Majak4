namespace MajakServer.Services;

public static class AuthCookiePolicy
{
    public static CookieOptions CreateRefreshCookieOptions(HttpRequest request, TimeSpan ttl)
    {
        bool nativeOrigin = IsNativeAppOrigin(request.Headers.Origin.ToString());
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = nativeOrigin || request.IsHttps,
            SameSite = nativeOrigin ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.Add(ttl),
            Path = "/auth",
        };
    }

    public static CookieOptions CreateRefreshCookieDeleteOptions(HttpRequest request)
    {
        bool nativeOrigin = IsNativeAppOrigin(request.Headers.Origin.ToString());
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = nativeOrigin || request.IsHttps,
            SameSite = nativeOrigin ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/auth",
        };
    }

    internal static bool IsNativeAppOrigin(string? origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme.Equals("capacitor", StringComparison.OrdinalIgnoreCase))
            return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && uri.IsDefaultPort
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}