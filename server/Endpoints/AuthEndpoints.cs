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
            if (account.IsHangeAccount && IsHangeServiceHost(context.Request.Host.Host))
            {
                var refreshedHangeAccount = await TryRefreshHangeProfileAsync(context, gamePlayers, account);
                if (refreshedHangeAccount is null)
                {
                    ClearRefreshCookie(context);
                    return Results.Unauthorized();
                }
                account = refreshedHangeAccount;
            }
            else
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
            if (fields is null) return Results.Unauthorized();
            var userNo = HangameCookieDecryptor.GetUserNo(fields);
            if (string.IsNullOrWhiteSpace(userNo))
                return Results.Unauthorized();

            var account = await gamePlayers.GetAccountByHangeUserNoAsync(userNo);
            if (account is null) return Results.NotFound(new { error = "ACCOUNT_NOT_FOUND" });
            if (account.AccountStatus == 2) return Results.StatusCode(StatusCodes.Status403Forbidden);

            await gamePlayers.AgreeToTermsAsync(account.MemberNo);
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
            if (fields is null) return Results.Unauthorized();
            var userNo = HangameCookieDecryptor.GetUserNo(fields);
            if (string.IsNullOrWhiteSpace(userNo))
                return Results.Unauthorized();
            var sexCode = body.Sex?.ToUpperInvariant() ?? string.Empty;
            if (sexCode is not ("M" or "F")) return Results.BadRequest(new { error = "INVALID_SEX" });
            if (!AvatarCatalog.IsValid(sexCode, body.AvatarId)) return Results.BadRequest(new { error = "INVALID_AVATAR" });
            var displayName = HangameCookieDecryptor.GetDisplayName(fields);
            var isTest = cookieValue!.TrimStart().StartsWith("hangametest=", StringComparison.OrdinalIgnoreCase);
            var account = await gamePlayers.GetAccountByHangeUserNoAsync(userNo);
            if (account is null)
            {
                var memberNo = (await gamePlayers.RegisterHangeAsync(userNo, displayName, sexCode, body.AvatarId!, isTest)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await moneyService.SetNewPlayerInitialMoneyWithHistoryAsync(memberNo, context.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
                account = await gamePlayers.GetAccountAsync(memberNo);
                if (account is null) return Results.Problem("Hange account registration failed.");
            }
            await playerRepository.SetDailyMissionAsync(account.MemberNo, conditionType: 1, progressIncrement: 1);
            var pix = sessions.IssuePix(account.MemberNo);
            return Results.Ok(new { pix, accessToken = gameAuth.IssueAccessToken(account.MemberNo, pix), memberNo = pix, name = displayName, sex = account.SexCode, birthYear = account.BirthYear, avatarId = account.AvatarId, isTestEnv = isTest, requiresRegistration = false, accountStatus = account.AccountStatus, termsAgreed = account.TermsAgreed });
        });

        app.MapPost("/auth/majak-login", async Task<IResult> (HttpContext context, HttpRequest request, GamePlayerRepository gamePlayers, PlayerRepository playerRepository, GameMoneyService moneyService, LogRepository logRepository, PlayerSessionService sessions, AuthRefreshSessionService refreshSessions, GameAuthTokenService gameAuth) =>
        {
            MajakLoginRequest? body = null;
            string? cookieValue = null;
            if (request.HasJsonContentType()) { body = await request.ReadFromJsonAsync<MajakLoginRequest>(); cookieValue = body?.LoginCookie; }
            cookieValue ??= context.Request.Cookies["login"];
            if (string.IsNullOrWhiteSpace(cookieValue)) return Results.Unauthorized();
            var fields = HangameCookieDecryptor.ParseCookie(cookieValue);
            if (fields is null) return Results.Unauthorized();
            var userNo = HangameCookieDecryptor.GetUserNo(fields);
            if (string.IsNullOrWhiteSpace(userNo)) return Results.Unauthorized();
            if (fields.TryGetValue("valid", out var valid) && !string.IsNullOrWhiteSpace(valid) && !string.Equals(valid, "Y", StringComparison.OrdinalIgnoreCase)) return Results.Unauthorized();
            fields.TryGetValue("sex", out var cookieSex);
            fields.TryGetValue("birthday", out var birthday);
            fields.TryGetValue("avatarid", out var cookieAvatarId);
            fields.TryGetValue("password", out var cookiePassword);
            var password = LegacyLaunchPassword.Extract(body?.KeyPwd) ?? LegacyLaunchPassword.Extract(body?.LaunchUrl) ?? LegacyLaunchPassword.Extract(body?.Referrer) ?? LegacyLaunchPassword.Extract(request.Headers.Referer.ToString()) ?? cookiePassword ?? string.Empty;
            var isTest = cookieValue.TrimStart().StartsWith("hangametest=", StringComparison.OrdinalIgnoreCase);
            var displayName = HangameCookieDecryptor.GetDisplayName(fields);
            var sexCode = cookieSex?.Trim().ToUpperInvariant() ?? string.Empty;
            var birthYear = ParseHangeBirthYear(birthday);
            var avatarId = cookieAvatarId?.Trim() ?? string.Empty;
            if (sexCode is not ("M" or "F") || string.IsNullOrWhiteSpace(avatarId) || avatarId.Length > 255) return Results.Unauthorized();

            var account = await gamePlayers.GetAccountByHangeUserNoAsync(userNo);
            if (account is null)
            {
                var memberNo = (await gamePlayers.RegisterHangeAsync(userNo, displayName, sexCode, avatarId, isTest, birthYear)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await moneyService.SetNewPlayerInitialMoneyWithHistoryAsync(memberNo, context.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
                account = await gamePlayers.GetAccountAsync(memberNo);
                if (account is null) return Results.Problem("Hange account registration failed.");
            }
            await gamePlayers.RefreshHangeLoginAsync(account.MemberNo, displayName, sexCode, birthYear, avatarId, isTest);
            account = await gamePlayers.GetAccountAsync(account.MemberNo);
            if (account is null) return Results.Problem("Hange account refresh failed.");

            await playerRepository.SetDailyMissionAsync(account.MemberNo, 1, 1);
            var pix = sessions.IssuePix(account.MemberNo);
            if (!await IssueRefreshCookieAsync(context, refreshSessions, account.MemberNo)) return Results.Problem("Hange refresh session could not be issued.");
            await InsertLoginLogOnceAsync(context, logRepository, account.MemberNo, 0);
            return Results.Ok(new { pix, accessToken = gameAuth.IssueAccessToken(account.MemberNo, pix), memberNo = pix, name = account.DisplayName, sex = account.SexCode, birthYear = account.BirthYear, avatarId = account.AvatarId, password, isTestEnv = isTest, requiresRegistration = false, accountStatus = account.AccountStatus, termsAgreed = account.TermsAgreed });
        });

        if (app.Configuration.GetValue("Authentication:GoogleEnabled", true))
        {
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
    }

    private static Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string token, string clientId)
        => GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });

    private static ushort? ParseHangeBirthYear(string? birthday)
    {
        if (string.IsNullOrWhiteSpace(birthday)) return null;
        var digits = new string(birthday.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length < 4 || !ushort.TryParse(digits[..4], out var year)) return null;
        return year >= 1900 && year <= DateTime.UtcNow.Year ? year : null;
    }

    internal static bool IsHangeServiceHost(string? host)
        => string.Equals(host, "hange.jp", StringComparison.OrdinalIgnoreCase)
            || host?.EndsWith(".hange.jp", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<GamePlayerAccount?> TryRefreshHangeProfileAsync(
        HttpContext context,
        GamePlayerRepository gamePlayers,
        GamePlayerAccount refreshAccount)
    {
        var cookieValue = context.Request.Cookies["login"];
        if (string.IsNullOrWhiteSpace(cookieValue)) return null;

        var fields = HangameCookieDecryptor.ParseCookie(cookieValue);
        if (fields is null) return null;
        if (fields.TryGetValue("valid", out var valid)
            && !string.IsNullOrWhiteSpace(valid)
            && !string.Equals(valid, "Y", StringComparison.OrdinalIgnoreCase))
            return null;

        var userNo = HangameCookieDecryptor.GetUserNo(fields);
        if (string.IsNullOrWhiteSpace(userNo)) return null;
        var hangeAccount = await gamePlayers.GetAccountByHangeUserNoAsync(userNo);
        if (hangeAccount?.MemberNo != refreshAccount.MemberNo) return null;

        fields.TryGetValue("sex", out var cookieSex);
        fields.TryGetValue("birthday", out var birthday);
        fields.TryGetValue("avatarid", out var cookieAvatarId);
        var sexCode = cookieSex?.Trim().ToUpperInvariant() ?? string.Empty;
        var avatarId = cookieAvatarId?.Trim() ?? string.Empty;
        if (sexCode is not ("M" or "F") || string.IsNullOrWhiteSpace(avatarId) || avatarId.Length > 255)
            return null;

        await gamePlayers.RefreshHangeLoginAsync(
            refreshAccount.MemberNo,
            HangameCookieDecryptor.GetDisplayName(fields),
            sexCode,
            ParseHangeBirthYear(birthday),
            avatarId,
            cookieValue.TrimStart().StartsWith("hangametest=", StringComparison.OrdinalIgnoreCase));
        return await gamePlayers.GetAccountAsync(refreshAccount.MemberNo) ?? refreshAccount;
    }

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