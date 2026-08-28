using Google.Apis.Auth;
using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Endpoints;

internal static class AuthEndpoints
{
    private const string PendingGoogleIdTokenCookieName = "mj_pending_google_id_token";

    internal static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/check-nickname", async (string? name, GamePlayerRepository gamePlayers) =>
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length < 4 || trimmed.Length > 16)
                return Results.Ok(new { available = false, reason = "LENGTH" });

            var available = await gamePlayers.IsNicknameAvailableAsync(trimmed);
            return Results.Ok(new { available, reason = available ? "" : "TAKEN" });
        });

        app.MapPost("/auth/refresh", async Task<IResult> (
            HttpContext context,
            GamePlayerRepository gamePlayers,
            PlayerRepository playerRepository,
            LogRepository logRepository,
            PlayerSessionService sessions,
            AuthRefreshSessionService refreshSessions,
            GameAuthTokenService gameAuth) =>
        {
            var currentToken = context.Request.Cookies[AuthRefreshSessionService.CookieName];
            var memberNo = await refreshSessions.ValidateAsync(currentToken, context);
            if (string.IsNullOrWhiteSpace(memberNo))
            {
                ClearRefreshCookie(context);
                return Results.NoContent();
            }

            var account = await gamePlayers.GetAccountAsync(memberNo);
            if (account is null)
            {
                await refreshSessions.RevokeAsync(currentToken);
                ClearRefreshCookie(context);
                return Results.NoContent();
            }

            await refreshSessions.RevokeAsync(currentToken);
            await gamePlayers.RefreshLoginAsync(account.MemberNo, account.DisplayName, false);
            await playerRepository.SetDailyMissionAsync(account.MemberNo, conditionType: 1, progressIncrement: 1);
            var pix = sessions.IssuePix(account.MemberNo);
            if (await IssueRefreshCookieAsync(context, refreshSessions, account.MemberNo))
                await InsertLoginLogOnceAsync(context, logRepository, account.MemberNo, 1);

            return Results.Ok(new
            {
                pix,
                accessToken = gameAuth.IssueAccessToken(account.MemberNo, pix),
                memberNo = pix,
                name = account.DisplayName,
                sex = account.SexCode,
                birthYear = account.BirthYear,
                avatarId = account.AvatarId,
                requiresRegistration = false,
                accountStatus = account.AccountStatus,
                termsAgreed = account.TermsAgreed,
            });
        });

        app.MapPost("/auth/logout", async Task<IResult> (HttpContext context, AuthRefreshSessionService refreshSessions) =>
        {
            var currentToken = context.Request.Cookies[AuthRefreshSessionService.CookieName];
            await refreshSessions.RevokeAsync(currentToken);
            ClearRefreshCookie(context);
            return Results.Ok(new { result = "ok" });
        });

        app.MapPost("/auth/agree-terms", async Task<IResult> (
            HttpContext context,
            HttpRequest request,
            GamePlayerRepository gamePlayers) =>
        {
            string? cookieValue = null;
            if (request.HasJsonContentType())
            {
                var body = await request.ReadFromJsonAsync<MajakLoginRequest>();
                cookieValue = body?.LoginCookie;
            }
            if (string.IsNullOrWhiteSpace(cookieValue))
                cookieValue = context.Request.Cookies["login"];

            var fields = string.IsNullOrWhiteSpace(cookieValue)
                ? null
                : HangameCookieDecryptor.ParseCookie(cookieValue);
            if (fields is null || !fields.TryGetValue("userid", out var memberNo)
                || string.IsNullOrWhiteSpace(memberNo))
                return Results.Unauthorized();

            var account = await gamePlayers.GetAccountAsync(memberNo);
            if (account is null) return Results.NotFound(new { error = "ACCOUNT_NOT_FOUND" });
            if (account.AccountStatus == 2) return Results.StatusCode(StatusCodes.Status403Forbidden);

            await gamePlayers.AgreeToTermsAsync(memberNo);
            return Results.Ok(new { accountStatus = account.AccountStatus, termsAgreed = true });
        });

        app.MapPost("/auth/majak-register", async Task<IResult> (
            HttpContext context,
            MajakRegisterRequest body,
            GamePlayerRepository gamePlayers,
            PlayerRepository playerRepository,
            GameMoneyService moneyService,
            PlayerSessionService sessions,
            GameAuthTokenService gameAuth) =>
        {
            var cookieValue = body.LoginCookie;
            if (string.IsNullOrWhiteSpace(cookieValue)) cookieValue = context.Request.Cookies["login"];
            var fields = string.IsNullOrWhiteSpace(cookieValue) ? null : HangameCookieDecryptor.ParseCookie(cookieValue);
            if (fields is null || !fields.TryGetValue("userid", out var memberNo) || string.IsNullOrWhiteSpace(memberNo))
                return Results.Unauthorized();
            var sexCode = body.Sex?.ToUpperInvariant() ?? string.Empty;
            if (sexCode is not ("M" or "F")) return Results.BadRequest(new { error = "INVALID_SEX" });
            if (!AvatarCatalog.IsValid(sexCode, body.AvatarId)) return Results.BadRequest(new { error = "INVALID_AVATAR" });
            fields.TryGetValue("name", out var displayName);
            var isTest = cookieValue!.TrimStart().StartsWith("hangametest=", StringComparison.OrdinalIgnoreCase);
            var account = await gamePlayers.GetAccountAsync(memberNo);
            if (account is null)
            {
                await gamePlayers.RegisterAsync(memberNo, displayName ?? string.Empty, sexCode, body.AvatarId!, isTest);
                await moneyService.SetNewPlayerInitialMoneyWithHistoryAsync(memberNo, context.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
                account = new GamePlayerAccount(displayName ?? string.Empty, sexCode, null, body.AvatarId!, 1, null);
            }
            await playerRepository.SetDailyMissionAsync(memberNo, conditionType: 1, progressIncrement: 1);
            var pix = sessions.IssuePix(memberNo);
            return Results.Ok(new { pix, accessToken = gameAuth.IssueAccessToken(memberNo, pix), memberNo = pix, name = displayName ?? account.DisplayName, sex = account.SexCode, birthYear = account.BirthYear, avatarId = account.AvatarId, isTestEnv = isTest, requiresRegistration = false, accountStatus = account.AccountStatus, termsAgreed = account.TermsAgreed });
        });

        app.MapPost("/auth/majak-login", async Task<IResult> (HttpContext context, HttpRequest request, GamePlayerRepository gamePlayers, PlayerRepository playerRepository, PlayerSessionService sessions, GameAuthTokenService gameAuth) =>
        {
            MajakLoginRequest? body = null;
            string? cookieValue = null;
            if (request.HasJsonContentType()) { body = await request.ReadFromJsonAsync<MajakLoginRequest>(); cookieValue = body?.LoginCookie; }
            cookieValue ??= context.Request.Cookies["login"];
            if (string.IsNullOrWhiteSpace(cookieValue)) return Results.Unauthorized();
            var fields = HangameCookieDecryptor.ParseCookie(cookieValue);
            if (fields is null || !fields.TryGetValue("userid", out var memberNo) || string.IsNullOrEmpty(memberNo)) return Results.Unauthorized();
            fields.TryGetValue("name", out var name); fields.TryGetValue("password", out var cookiePassword);
            var password = LegacyLaunchPassword.Extract(body?.KeyPwd) ?? LegacyLaunchPassword.Extract(body?.LaunchUrl) ?? LegacyLaunchPassword.Extract(body?.Referrer) ?? LegacyLaunchPassword.Extract(request.Headers.Referer.ToString()) ?? cookiePassword ?? string.Empty;
            var isTest = cookieValue.TrimStart().StartsWith("hangametest=", StringComparison.OrdinalIgnoreCase);
            var account = await gamePlayers.GetAccountAsync(memberNo);
            if (account is not null) { await gamePlayers.RefreshLoginAsync(memberNo, name ?? account.DisplayName, isTest); await playerRepository.SetDailyMissionAsync(memberNo, 1, 1); }
            var pix = sessions.IssuePix(memberNo);
            return Results.Ok(new { pix, accessToken = gameAuth.IssueAccessToken(memberNo, pix), memberNo = pix, name = name ?? string.Empty, sex = account?.SexCode ?? string.Empty, avatarId = account?.AvatarId ?? string.Empty, password, isTestEnv = isTest, requiresRegistration = account is null, accountStatus = account?.AccountStatus ?? 0, termsAgreed = account?.TermsAgreed ?? false });
        });

        app.MapPost("/auth/google-login-redirect", async Task<IResult> (HttpContext context, GamePlayerRepository gamePlayers, PlayerRepository playerRepository, LogRepository logRepository, IConfiguration configuration, ILogger<AuthEndpointsMarker> logger, PlayerSessionService sessions, AuthRefreshSessionService refreshSessions) =>
        {
            var clientAppUrl = configuration["ClientAppUrl"]?.TrimEnd('/') ?? $"{context.Request.Scheme}://{context.Request.Host}";
            var idToken = (await context.Request.ReadFormAsync())["credential"].ToString();
            if (string.IsNullOrWhiteSpace(idToken)) { ClearPendingGoogleIdTokenCookie(context); return Results.Redirect($"{clientAppUrl}/?googleAuth=error"); }
            var clientId = configuration["AdminSettings:GoogleClientId"];
            if (string.IsNullOrWhiteSpace(clientId)) { ClearPendingGoogleIdTokenCookie(context); return Results.Redirect($"{clientAppUrl}/?googleAuth=error"); }
            GoogleJsonWebSignature.Payload payload;
            try { payload = await ValidateGoogleTokenAsync(idToken, clientId); }
            catch (InvalidJwtException exception) { logger.LogWarning("Google redirect token validation failed: {Message}", exception.Message); ClearPendingGoogleIdTokenCookie(context); return Results.Redirect($"{clientAppUrl}/?googleAuth=error"); }
            var account = await gamePlayers.GetAccountByGoogleSubAsync(payload.Subject);
            if (account is null) { SetPendingGoogleIdTokenCookie(context, idToken); return Results.Redirect($"{clientAppUrl}/?googleAuth=register"); }
            await gamePlayers.RefreshLoginAsync(account.MemberNo, account.DisplayName, false);
            await playerRepository.SetDailyMissionAsync(account.MemberNo, 1, 1);
            sessions.IssuePix(account.MemberNo);
            if (!await IssueRefreshCookieAsync(context, refreshSessions, account.MemberNo))
            {
                logger.LogError("Google redirect login could not issue a refresh session.");
                ClearPendingGoogleIdTokenCookie(context);
                return Results.Redirect($"{clientAppUrl}/?googleAuth=error");
            }
            await InsertLoginLogOnceAsync(context, logRepository, account.MemberNo, 0);
            ClearPendingGoogleIdTokenCookie(context);
            return Results.Redirect($"{clientAppUrl}/?googleAuth=login");
        });

        app.MapPost("/auth/google-login", async Task<IResult> (HttpContext context, GooglePlayerLoginRequest body, GamePlayerRepository gamePlayers, PlayerRepository playerRepository, LogRepository logRepository, IConfiguration configuration, ILogger<AuthEndpointsMarker> logger, PlayerSessionService sessions, AuthRefreshSessionService refreshSessions, GameAuthTokenService gameAuth) =>
        {
            var clientId = configuration["AdminSettings:GoogleClientId"];
            if (string.IsNullOrWhiteSpace(clientId)) return Results.Problem("Google client ID not configured.");
            var idToken = string.IsNullOrWhiteSpace(body.IdToken) ? context.Request.Cookies[PendingGoogleIdTokenCookieName] : body.IdToken;
            if (string.IsNullOrWhiteSpace(idToken)) return Results.Unauthorized();
            GoogleJsonWebSignature.Payload payload;
            try { payload = await ValidateGoogleTokenAsync(idToken, clientId); }
            catch (InvalidJwtException exception) { logger.LogWarning("Google ID token validation failed: {Message}", exception.Message); return Results.Unauthorized(); }
            var account = await gamePlayers.GetAccountByGoogleSubAsync(payload.Subject);
            if (account is null) { SetPendingGoogleIdTokenCookie(context, idToken); return Results.Ok(new { memberNo = string.Empty, name = string.Empty, sex = string.Empty, avatarId = string.Empty, requiresRegistration = true, accountStatus = 0, termsAgreed = false }); }
            await gamePlayers.RefreshLoginAsync(account.MemberNo, account.DisplayName, false);
            await playerRepository.SetDailyMissionAsync(account.MemberNo, 1, 1);
            var pix = sessions.IssuePix(account.MemberNo);
            if (await IssueRefreshCookieAsync(context, refreshSessions, account.MemberNo)) await InsertLoginLogOnceAsync(context, logRepository, account.MemberNo, 0);
            ClearPendingGoogleIdTokenCookie(context);
            return Results.Ok(new { pix, accessToken = gameAuth.IssueAccessToken(account.MemberNo, pix), memberNo = pix, name = account.DisplayName, sex = account.SexCode, birthYear = account.BirthYear, avatarId = account.AvatarId, requiresRegistration = false, accountStatus = account.AccountStatus, termsAgreed = account.TermsAgreed });
        });

        app.MapPost("/auth/google-register", async Task<IResult> (HttpContext context, GooglePlayerRegisterRequest body, GamePlayerRepository gamePlayers, PlayerRepository playerRepository, LogRepository logRepository, GameMoneyService moneyService, IConfiguration configuration, ILogger<AuthEndpointsMarker> logger, PlayerSessionService sessions, AuthRefreshSessionService refreshSessions, GameAuthTokenService gameAuth) =>
        {
            var clientId = configuration["AdminSettings:GoogleClientId"];
            if (string.IsNullOrWhiteSpace(clientId)) return Results.Problem("Google client ID not configured.");
            var idToken = string.IsNullOrWhiteSpace(body.IdToken) ? context.Request.Cookies[PendingGoogleIdTokenCookieName] : body.IdToken;
            if (string.IsNullOrWhiteSpace(idToken)) return Results.Unauthorized();
            GoogleJsonWebSignature.Payload payload;
            try { payload = await ValidateGoogleTokenAsync(idToken, clientId); }
            catch (InvalidJwtException exception) { logger.LogWarning("Google register token validation failed: {Message}", exception.Message); return Results.Unauthorized(); }
            var email = string.IsNullOrWhiteSpace(payload.Email) ? null : payload.Email.Trim().ToLowerInvariant();
            if (email is { Length: > 254 }) return Results.BadRequest(new { error = "INVALID_EMAIL" });
            var nickname = body.DisplayName?.Trim() ?? string.Empty;
            if (nickname.Length < 4 || nickname.Length > 16) return Results.BadRequest(new { error = "NICKNAME_INVALID_LENGTH" });
            if (!await gamePlayers.IsNicknameAvailableAsync(nickname)) return Results.BadRequest(new { error = "NICKNAME_TAKEN" });
            var existing = await gamePlayers.GetAccountByGoogleSubAsync(payload.Subject);
            var sexCode = body.Sex?.ToUpperInvariant() ?? string.Empty;
            if (sexCode is not ("M" or "F")) return Results.BadRequest(new { error = "INVALID_SEX" });
            if (body.BirthYear is null || body.BirthYear < 1900 || body.BirthYear > DateTime.UtcNow.Year) return Results.BadRequest(new { error = "INVALID_BIRTH_YEAR" });
            if (string.IsNullOrWhiteSpace(body.AvatarId) || !AvatarCatalog.IsValid(sexCode, body.AvatarId)) return Results.BadRequest(new { error = "INVALID_AVATAR" });
            var memberNo = existing?.MemberNo ?? (await gamePlayers.RegisterGoogleAsync(payload.Subject, email, nickname, sexCode, (ushort)body.BirthYear.Value, body.AvatarId!)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var displayName = existing?.DisplayName ?? nickname;
            if (existing is null) await moneyService.SetNewPlayerInitialMoneyWithHistoryAsync(memberNo, context.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
            else await gamePlayers.RefreshLoginAsync(memberNo, displayName, false);
            await playerRepository.SetDailyMissionAsync(memberNo, 1, 1);
            var pix = sessions.IssuePix(memberNo);
            if (await IssueRefreshCookieAsync(context, refreshSessions, memberNo)) await InsertLoginLogOnceAsync(context, logRepository, memberNo, existing is null ? (byte)2 : (byte)0);
            ClearPendingGoogleIdTokenCookie(context);
            return Results.Ok(new { pix, accessToken = gameAuth.IssueAccessToken(memberNo, pix), memberNo = pix, name = displayName, sex = existing?.SexCode ?? sexCode, birthYear = existing?.BirthYear ?? body.BirthYear, avatarId = existing?.AvatarId ?? body.AvatarId, requiresRegistration = false, accountStatus = existing?.AccountStatus ?? 1, termsAgreed = existing?.TermsAgreed ?? true });
        });
    }

    private static Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string token, string clientId)
        => GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });

    private static void SetPendingGoogleIdTokenCookie(HttpContext context, string idToken)
        => context.Response.Cookies.Append(PendingGoogleIdTokenCookieName, idToken, new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddMinutes(30), Path = "/" });

    private static void ClearPendingGoogleIdTokenCookie(HttpContext context)
        => context.Response.Cookies.Delete(PendingGoogleIdTokenCookieName, new CookieOptions { HttpOnly = false, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/" });

    private static async Task<bool> IssueRefreshCookieAsync(HttpContext context, AuthRefreshSessionService refreshSessions, string memberNo)
    {
        var token = await refreshSessions.IssueAsync(memberNo, context);
        if (string.IsNullOrWhiteSpace(token)) return false;
        context.Response.Cookies.Append(AuthRefreshSessionService.CookieName, token, AuthCookiePolicy.CreateRefreshCookieOptions(context.Request, refreshSessions.Ttl));
        return true;
    }

    private static void ClearRefreshCookie(HttpContext context)
        => context.Response.Cookies.Delete(AuthRefreshSessionService.CookieName, AuthCookiePolicy.CreateRefreshCookieDeleteOptions(context.Request));

    private static async Task InsertLoginLogOnceAsync(HttpContext context, LogRepository logRepository, string memberNo, byte eventType)
    {
        try
        {
            await logRepository.InsertPlayerLoginLogOncePerJapanDayAsync(memberNo, eventType, context.Connection.RemoteIpAddress?.ToString() ?? string.Empty, context.Request.Headers.UserAgent.ToString());
        }
        catch (Exception exception)
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Auth.LoginLog")
                .LogWarning(exception, "Player login log insert failed but auth continues. memberNo={MemberNo}", memberNo);
        }
    }
}

internal sealed class AuthEndpointsMarker;