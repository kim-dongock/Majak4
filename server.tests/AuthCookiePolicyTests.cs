using MajakServer.Endpoints;
using MajakServer.Services;
using Microsoft.AspNetCore.Http;

namespace MajakServer.Tests;

public class AuthCookiePolicyTests
{
    [Theory]
    [InlineData("hange.jp", true)]
    [InlineData("alpha-app-majak4.hange.jp", true)]
    [InlineData("HANGE.JP", true)]
    [InlineData("studio35app.net", false)]
    [InlineData("fake-hange.jp", false)]
    [InlineData("hange.jp.example.com", false)]
    public void IsHangeServiceHost_MatchesOnlyHangeDomainBoundary(string host, bool expected)
    {
        Assert.Equal(expected, AuthEndpoints.IsHangeServiceHost(host));
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("https://localhost")]
    [InlineData("capacitor://localhost")]
    public void CreateRefreshCookieOptions_NativeOrigin_AllowsCrossSitePersistence(string origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Origin = origin;

        var options = AuthCookiePolicy.CreateRefreshCookieOptions(context.Request, TimeSpan.FromDays(30));

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.None, options.SameSite);
        Assert.Equal("/auth", options.Path);
        Assert.NotNull(options.Expires);
    }

    [Theory]
    [InlineData("http://localhost:5173", false)]
    [InlineData("https://majak4.studio35app.net", true)]
    public void CreateRefreshCookieOptions_WebOrigin_KeepsLaxPolicy(string origin, bool isHttps)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Origin = origin;
        context.Request.Scheme = isHttps ? "https" : "http";

        var options = AuthCookiePolicy.CreateRefreshCookieOptions(context.Request, TimeSpan.FromDays(30));

        Assert.Equal(isHttps, options.Secure);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
    }

    [Fact]
    public void CreateRefreshCookieDeleteOptions_NativeOrigin_MatchesIssuedCookiePolicy()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Origin = "http://localhost";

        var options = AuthCookiePolicy.CreateRefreshCookieDeleteOptions(context.Request);

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.None, options.SameSite);
        Assert.Equal("/auth", options.Path);
    }
}