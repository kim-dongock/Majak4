using Google.Apis.Auth;
using MajakServer.Commands.Channel;
using MajakServer.Commands.Room;
using MajakServer.Commands.Game;
using MajakServer.Hubs;
using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

// 各種エンコーディング (Shift-JIS 等) を有効化
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// ─── 環境別 appsettings 追加ロード ──────────────────────────────
// ASPNETCORE_ENVIRONMENT=Alpha の場合に appsettings.Alpha.json を読み込む
var env = builder.Environment.EnvironmentName;
if (env is not "Development" and not "Production")
{
    builder.Configuration.AddJsonFile(
        $"appsettings.{env}.json",
        optional: true,
        reloadOnChange: false);
}

// ─── Configuration ────────────────────────────────────────────
builder.Services.Configure<RuntimeFlagOptions>(
    builder.Configuration.GetSection(RuntimeFlagOptions.SectionName));
builder.Services.Configure<ChannelServerSettings>(
    builder.Configuration.GetSection(ChannelServerSettings.SectionName));
builder.Services.Configure<AdminSettings>(
    builder.Configuration.GetSection(AdminSettings.SectionName));
builder.Services.Configure<GameAuthSettings>(
    builder.Configuration.GetSection(GameAuthSettings.SectionName));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// ─── DB Contexts ─────────────────────────────────────────────
builder.Services.AddSingleton<IParameterStoreService, ParameterStoreService>();
builder.Services.AddSingleton<GameDbContext>();
builder.Services.AddSingleton<GameDataContextFactory>();
builder.Services.AddSingleton<LogDbContext>();
builder.Services.AddSingleton<LogDataContextFactory>();

// ─── Repositories ─────────────────────────────────────────────
builder.Services.AddScoped<PlayerRepository>();
builder.Services.AddScoped<TournamentRepository>();
builder.Services.AddScoped<HistoryRepository>();
builder.Services.AddScoped<ItemRepository>();
builder.Services.AddScoped<LogRepository>();
builder.Services.AddScoped<GamePlayerRepository>();
builder.Services.AddScoped<ChannelRepository>();

// ─── Services ─────────────────────────────────────────────────
builder.Services.AddSingleton<RedisService>();
builder.Services.AddSingleton<PrimaryLeaderService>(sp =>
    new PrimaryLeaderService(
        sp.GetRequiredService<RedisService>(),
        sp.GetRequiredService<IOptions<ChannelServerSettings>>().Value));
builder.Services.AddSingleton<PlayerSessionService>();
builder.Services.AddSingleton<LobbySessionLeaseService>();
builder.Services.AddSingleton<AuthRefreshSessionService>();
builder.Services.AddSingleton<GameAuthTokenService>();
builder.Services.AddSingleton<AdminIdService>();
builder.Services.AddSingleton<MenteTimeService>();
builder.Services.AddSingleton<TournamentService>();
builder.Services.AddSingleton<GradeRankService>();
builder.Services.AddScoped<RatingService>();
builder.Services.AddScoped<GameMoneyService>();
builder.Services.AddScoped<TitleService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<MajItemService>();
builder.Services.AddScoped<MissionService>();
builder.Services.AddScoped<GameLogicService>();

// ─── Admin ────────────────────────────────────────────────────
builder.Services.AddScoped<AdminRepository>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddSingleton<ServerLoadService>(sp =>
    new ServerLoadService(
        sp.GetRequiredService<RedisService>(),
        sp.GetRequiredService<IOptions<ChannelServerSettings>>().Value));
builder.Services.AddSingleton<MasterCacheService>();
builder.Services.AddSingleton<ChannelMemberService>();
builder.Services.AddSingleton<RoomRegistryService>();
builder.Services.AddSingleton<GradeRankBackgroundService>();

// ─── Background Services ─────────────────────────────────────────────
builder.Services.AddHostedService<TournamentBackgroundService>();
builder.Services.AddHostedService<CupChannelBackgroundService>();
builder.Services.AddHostedService<AutoMatchingBackgroundService>();
builder.Services.AddHostedService<ServerStatusBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GradeRankBackgroundService>());

// ─── Channel Commands ─────────────────────────────────────────
builder.Services.AddScoped<GetDetailRecCommand>();
builder.Services.AddScoped<AutoMatchingCommand>();
builder.Services.AddScoped<CancelAutoMatchingCommand>();
builder.Services.AddScoped<AutoEnterRoomCommand>();
builder.Services.AddScoped<GetServerTimeCommand>();
builder.Services.AddScoped<MoneyReplenishmentCommand>();
builder.Services.AddScoped<ApplyEarnedMoneyCommand>();
builder.Services.AddScoped<YakumanBonusCommand>();
builder.Services.AddScoped<GetTitleCommand>();
builder.Services.AddScoped<GetMissionListCommand>();
builder.Services.AddScoped<RcvWeeklyRewardCommand>();
builder.Services.AddScoped<RcvSerialBonusCommand>();
builder.Services.AddScoped<ShopItemRequestCommand>();
builder.Services.AddScoped<CustomItemCommand>();
builder.Services.AddScoped<BuyCustomItemCommand>();
builder.Services.AddScoped<EquipCustomItemCommand>();
builder.Services.AddScoped<AvatarGearCommand>();
builder.Services.AddScoped<BuyMajItemCommand>();
builder.Services.AddScoped<SelectMajItemCommand>();
builder.Services.AddScoped<GetMajItemListCommand>();
builder.Services.AddScoped<GetGemCommand>();
builder.Services.AddScoped<RatingRankInfoCommand>();
builder.Services.AddScoped<InviteCommand>();
builder.Services.AddScoped<InviteResponseCommand>();
builder.Services.AddScoped<TournamentListCommand>();
builder.Services.AddScoped<TournamentRegistCommand>();
builder.Services.AddScoped<TournamentJoinCommand>();
builder.Services.AddScoped<TournamentJoinCancelCommand>();
builder.Services.AddScoped<TournamentDetailCommand>();
builder.Services.AddScoped<SetCustomItemCommand>();
// チャンネルライフサイクル (ProcessCommand_GetRoomList 等)
builder.Services.AddScoped<EnterChannelCommand>();
builder.Services.AddScoped<CreateRoomCommand>();
builder.Services.AddScoped<GetRoomListCommand>();
builder.Services.AddScoped<GetMemberListCommand>();
builder.Services.AddScoped<ExitChannelCommand>();
builder.Services.AddScoped<HanChatAllRelayCommand>();
builder.Services.AddScoped<HanChatOneToOneCommand>();
builder.Services.AddScoped<HanChatOneToOneStringCommand>();
builder.Services.AddScoped<HanChatOneToOneEndCommand>();
builder.Services.AddScoped<ViewRoomCommand>();
builder.Services.AddScoped<ComplaintCommand>();

// ─── Room Commands ────────────────────────────────────────────
builder.Services.AddScoped<RoomExitRoomCommand>();
builder.Services.AddScoped<SendOkButtonCommand>();
builder.Services.AddScoped<PushOkButtonCommand>();
builder.Services.AddScoped<RoomGetMembersCommand>();
builder.Services.AddScoped<RoomEnterRoomCommand>();
builder.Services.AddScoped<RoomAlterRoomCommand>();
builder.Services.AddScoped<RoomEmoticonCommand>();
builder.Services.AddScoped<EventInfoCommand>();
builder.Services.AddScoped<PaiInfoListCommand>();
builder.Services.AddScoped<IpAdapterInfoCommand>();
builder.Services.AddScoped<RoomStateCommand>();
builder.Services.AddScoped<TsumikomiCommand>();

// ─── Game Commands ────────────────────────────────────────────
builder.Services.AddScoped<GamePlayCommand>();
builder.Services.AddScoped<AgariRecCommand>();
builder.Services.AddScoped<HistoryCommand>();
builder.Services.AddScoped<GameReportCommand>();
builder.Services.AddScoped<ReplayNaviCommand>();
builder.Services.AddScoped<ReserveChanceCommand>();

// ─── SignalR ──────────────────────────────────────────────────
builder.Services.AddSignalR(opt =>
{
    opt.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    opt.KeepAliveInterval = TimeSpan.FromSeconds(15);
    opt.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
});

// ─── CORS ─────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000" })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
    // 管理サイト専用 CORS ポリシー (JWT Bearer 使用、Credentials 不要)
    opt.AddPolicy("AdminPolicy", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("AdminAllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:5174" })
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// ─── 起動時マスターキャッシュ初期化 ──────────────────────────────
// 1. プライマリリーダー確認 (Redis SETNX) → プライマリなら全マスターを Redis へ書き込む
// 2. 各サービスの InitAsync は Redis キャッシュを優先し、DB 接続を最小化する
var leaderService = app.Services.GetRequiredService<PrimaryLeaderService>();
await leaderService.TryAcquireOrRenewAsync();   // 起動時リーダー確認

var masterCache = app.Services.GetRequiredService<MasterCacheService>();
if (leaderService.IsLeader)
{
    // プライマリ: 全マスターを MySQL から読み込んで Redis へ書き込む (他サーバーも恩恵を受ける)
    await masterCache.BootstrapAsync();
}

using (var scope = app.Services.CreateScope())
{
    // 各サービスは MasterCacheService 経由で Redis → DB フォールバックでデータを取得
    var titleService = scope.ServiceProvider.GetRequiredService<TitleService>();
    await titleService.InitAsync();

    var itemService = scope.ServiceProvider.GetRequiredService<ItemService>();
    await itemService.InitAsync();

    var adminIdService = scope.ServiceProvider.GetRequiredService<AdminIdService>();
    await adminIdService.InitAsync();

    // 原典: HMajRootServer::Run の _LIMIT_PLAY_TIME EVTCODEMAST ロード相当
    var menteTimeService = scope.ServiceProvider.GetRequiredService<MenteTimeService>();
    await menteTimeService.InitAsync();

    var gradeRankService = scope.ServiceProvider.GetRequiredService<GradeRankService>();
    await gradeRankService.InitAsync();

    var tournamentService = scope.ServiceProvider.GetRequiredService<TournamentService>();
    await tournamentService.InitAsync();
}

app.UseForwardedHeaders();
app.UseCors();
app.MapHub<MajakGameHub>("/hubs/majak");
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

const string PendingGoogleIdTokenCookieName = "mj_pending_google_id_token";

static async Task<bool> IssueRefreshCookieAsync(HttpContext ctx, AuthRefreshSessionService refreshSessions, string memberNo)
{
    var token = await refreshSessions.IssueAsync(memberNo, ctx);
    if (string.IsNullOrWhiteSpace(token)) return false;

    ctx.Response.Cookies.Append(
        AuthRefreshSessionService.CookieName,
        token,
        AuthCookiePolicy.CreateRefreshCookieOptions(ctx.Request, refreshSessions.Ttl));
    return true;
}

static void ClearRefreshCookie(HttpContext ctx)
{
    ctx.Response.Cookies.Delete(
        AuthRefreshSessionService.CookieName,
        AuthCookiePolicy.CreateRefreshCookieDeleteOptions(ctx.Request));
}

static void SetPendingGoogleIdTokenCookie(HttpContext ctx, string idToken)
{
    ctx.Response.Cookies.Append(PendingGoogleIdTokenCookieName, idToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = ctx.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddMinutes(5),
        Path = "/",
    });
}

static void ClearPendingGoogleIdTokenCookie(HttpContext ctx)
{
    ctx.Response.Cookies.Delete(PendingGoogleIdTokenCookieName, new CookieOptions
    {
        HttpOnly = false,
        Secure = ctx.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
    });
}

static async Task InsertLoginLogOnceAsync(HttpContext ctx, LogRepository logRepo, string memberNo, byte eventType)
{
    try
    {
        await logRepo.InsertPlayerLoginLogOncePerJapanDayAsync(
            memberNo,
            eventType,
            ctx.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            ctx.Request.Headers.UserAgent.ToString());
    }
    catch (Exception ex)
    {
        ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Auth.LoginLog")
            .LogWarning(ex, "Player login log insert failed but auth continues. memberNo={MemberNo}", memberNo);
    }
}

static string IssueGameAccessToken(GameAuthTokenService gameAuth, string memberNo, string pix)
    => gameAuth.IssueAccessToken(memberNo, pix);

static GameAuthPrincipal? RequireGameAuth(HttpContext ctx, GameAuthTokenService gameAuth)
    => gameAuth.Validate(ctx.Request.Headers.Authorization.FirstOrDefault());

// ─── 管理サイト API (/api/admin/*) ───────────────────────────────────────
// 認証: POST /api/admin/auth/google のみ無認証。他は Bearer JWT が必須。
// JWT 検証は RequireAdminAuth() ローカル関数で行う。

// ── ヘルパー: JWT 検証 → ClaimsPrincipal ─────────────────────────────────
static IResult? RequireAdminAuth(HttpContext ctx, AdminAuthService auth,
    string? requiredRole = null)
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (authHeader is null || !authHeader.StartsWith("Bearer "))
        return Results.Unauthorized();

    var token = authHeader["Bearer ".Length..].Trim();
    var principal = auth.ValidateJwt(token);
    if (principal is null) return Results.Unauthorized();

    if (requiredRole is not null)
    {
        var role = principal.FindFirst("role")?.Value
            ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!string.Equals(role, requiredRole, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "super_admin", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    return null; // OK
}

static ulong? GetAdminNoClaim(HttpContext ctx, AdminAuthService auth)
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (authHeader is null || !authHeader.StartsWith("Bearer "))
        return null;

    var token = authHeader["Bearer ".Length..].Trim();
    var principal = auth.ValidateJwt(token);
    var adminNo = principal?.FindFirst("admin_no")?.Value;
    return ulong.TryParse(adminNo, out var parsed) ? parsed : null;
}

// ── POST /api/admin/auth/google  (認証不要) ───────────────────────────────
app.MapPost("/api/admin/auth/google", async (
    HttpContext ctx,
    AdminAuthService adminAuth) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<GoogleLoginRequest>();
    if (string.IsNullOrWhiteSpace(body?.IdToken))
        return Results.BadRequest(new { error = "idToken required" });

    var result = await adminAuth.LoginWithGoogleAsync(body.IdToken);
    if (!result.Success)
        return Results.Unauthorized();

    return Results.Ok(new { token = result.Token, adminNo = result.AdminNo, email = result.Email, role = result.Role });
}).RequireCors("AdminPolicy");

// ── GET /api/admin/dashboard ────────────────────────────────────────────
app.MapGet("/api/admin/dashboard", async (
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    var stats = await adminRepo.GetDashboardStatsAsync();
    return Results.Ok(stats);
}).RequireCors("AdminPolicy");

// ── GET /api/admin/users?keyword=&offset=&limit= ────────────────────────
app.MapGet("/api/admin/users", async (
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo,
    string? keyword, int offset = 0, int limit = 30) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    if (limit > 100) limit = 100;
    var players = await adminRepo.SearchPlayersAsync(keyword, offset, limit);
    return Results.Ok(players);
}).RequireCors("AdminPolicy");

// ── GET /api/admin/users/{memberNo} ─────────────────────────────────────
app.MapGet("/api/admin/users/{memberNo}", async (
    ulong memberNo,
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    var player = await adminRepo.GetPlayerDetailAsync(memberNo);
    return player is null ? Results.NotFound() : Results.Ok(player);
}).RequireCors("AdminPolicy");

// ── POST /api/admin/cash/adjust  (Operator 以上) ──────────────────────────
app.MapPost("/api/admin/cash/adjust", async (
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo,
    LogDbContext logDb) =>
{
    if (RequireAdminAuth(ctx, adminAuth, "operator") is { } err) return err;

    var body = await ctx.Request.ReadFromJsonAsync<CashAdjustRequest>();
    if (body is null || body.MemberNo == 0 || body.Amount == 0)
        return Results.BadRequest(new { error = "memberNo and non-zero amount required" });
    if (string.IsNullOrWhiteSpace(body.Memo))
        return Results.BadRequest(new { error = "memo required for admin Majak Cash adjustment" });

    // 残高取得 (調整前)
    var player = await adminRepo.GetPlayerDetailAsync(body.MemberNo);
    if (player is null) return Results.NotFound(new { error = "player not found" });

    var adjustment = await adminRepo.AdjustCashAsync(body.MemberNo, body.Amount);

    // cash_transaction_log (Log DB に書く)
    var operatorNo = GetAdminNoClaim(ctx, adminAuth);
    if (operatorNo is null) return Results.Unauthorized();

    await using var logConn = await logDb.CreateConnectionAsync();
    await using var logCmd  = new MySqlConnector.MySqlCommand(@"
        INSERT INTO cash_transaction_log
            (member_no, event_type, amount, balance_before, balance_after,
             paid_amount, free_amount, paid_before, paid_after, free_before, free_after,
             ref_id, memo, operator_no, client_ip, occurred_at)
        VALUES
            (@memberNo, @eventType, @amount, @before, @after,
             @paidAmount, @freeAmount, @paidBefore, @paidAfter, @freeBefore, @freeAfter,
             NULL, @memo, @opNo, @ip, CURRENT_TIMESTAMP(3))", logConn);
    logCmd.Parameters.AddWithValue("@memberNo",  body.MemberNo);
    logCmd.Parameters.AddWithValue("@eventType", body.Amount > 0 ? "ADMIN_GRANT_FREE" : "ADMIN_DEDUCT");
    logCmd.Parameters.AddWithValue("@amount",    body.Amount);
    logCmd.Parameters.AddWithValue("@before",    adjustment.TotalBefore);
    logCmd.Parameters.AddWithValue("@after",     adjustment.TotalAfter);
    logCmd.Parameters.AddWithValue("@paidAmount", adjustment.PaidAfter - adjustment.PaidBefore);
    logCmd.Parameters.AddWithValue("@freeAmount", adjustment.FreeAfter - adjustment.FreeBefore);
    logCmd.Parameters.AddWithValue("@paidBefore", adjustment.PaidBefore);
    logCmd.Parameters.AddWithValue("@paidAfter",  adjustment.PaidAfter);
    logCmd.Parameters.AddWithValue("@freeBefore", adjustment.FreeBefore);
    logCmd.Parameters.AddWithValue("@freeAfter",  adjustment.FreeAfter);
    logCmd.Parameters.AddWithValue("@memo",      body.Memo);
    logCmd.Parameters.AddWithValue("@opNo",      operatorNo.Value);
    logCmd.Parameters.AddWithValue("@ip",        (object?)ctx.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
    await logCmd.ExecuteNonQueryAsync();

    return Results.Ok(new
    {
        memberNo = body.MemberNo,
        balanceBefore = adjustment.TotalBefore,
        balanceAfter = adjustment.TotalAfter,
        paidCashBefore = adjustment.PaidBefore,
        paidCashAfter = adjustment.PaidAfter,
        freeCashBefore = adjustment.FreeBefore,
        freeCashAfter = adjustment.FreeAfter,
    });
}).RequireCors("AdminPolicy");

// ── GET /api/admin/gem/products ─────────────────────────────────────────
app.MapGet("/api/admin/cash/products", async (
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    return Results.Ok(await adminRepo.GetCashProductsAsync());
}).RequireCors("AdminPolicy");

// ── PUT /api/admin/gem/products/{productId} (Super Admin のみ) ──────────
app.MapPut("/api/admin/cash/products/{productId}", async (
    string productId,
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth, "super_admin") is { } err) return err;
    var body = await ctx.Request.ReadFromJsonAsync<CashProduct>();
    if (body is null || body.ProductId != productId)
        return Results.BadRequest(new { error = "productId mismatch" });
    await adminRepo.UpdateCashProductAsync(body);
    return Results.Ok(new { updated = true });
}).RequireCors("AdminPolicy");

// ── GET /api/admin/gem/revenue?days=30 ──────────────────────────────────
app.MapGet("/api/admin/cash/revenue", async (
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo,
    int days = 30) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    if (days > 365) days = 365;
    return Results.Ok(await adminRepo.GetDailyRevenueAsync(days));
}).RequireCors("AdminPolicy");

// ── GET /api/admin/accounts ─────────────────────────────────────────────
app.MapGet("/api/admin/accounts", async (
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth, "super_admin") is { } err) return err;
    return Results.Ok(await adminRepo.GetAdminAccountsAsync());
}).RequireCors("AdminPolicy");

// ── POST /api/admin/accounts (Super Admin のみ) ─────────────────────────
app.MapPost("/api/admin/accounts", async (
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth, "super_admin") is { } err) return err;
    var body = await ctx.Request.ReadFromJsonAsync<AdminAccountRequest>();
    if (string.IsNullOrWhiteSpace(body?.Email) ||
        body.Role is not ("super_admin" or "operator" or "viewer"))
        return Results.BadRequest(new { error = "email and valid role required" });
    var account = await adminRepo.UpsertAdminAccountAsync(body.Email, body.Role);
    return Results.Ok(account);
}).RequireCors("AdminPolicy");

// ── DELETE /api/admin/accounts/{email} (Super Admin のみ) ───────────────
app.MapDelete("/api/admin/accounts/{email}", async (
    string email,
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth, "super_admin") is { } err) return err;
    await adminRepo.SetAdminAccountActiveAsync(email, false);
    return Results.Ok(new { disabled = true });
}).RequireCors("AdminPolicy");

// ── GET /api/admin/users/pending ────────────────────────────────────────
// 承認待ちプレイヤー一覧 (terms同意済み・未承認)
app.MapGet("/api/admin/users/pending", async (
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo,
    int offset = 0, int limit = 50) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    var list  = await adminRepo.GetPendingPlayersAsync(offset, limit);
    var total = await adminRepo.CountPendingPlayersAsync();
    return Results.Ok(new { total, offset, limit, items = list });
}).RequireCors("AdminPolicy");

// ── POST /api/admin/users/{memberNo}/approve ─────────────────────────────
app.MapPost("/api/admin/users/{memberNo}/approve", async (
    ulong memberNo,
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    var adminNo = GetAdminNoClaim(ctx, adminAuth);
    if (adminNo is null) return Results.Unauthorized();
    var ok = await adminRepo.ApprovePlayerAsync(memberNo, adminNo.Value);
    return ok ? Results.Ok(new { approved = true }) : Results.NotFound(new { error = "NOT_PENDING" });
}).RequireCors("AdminPolicy");

// ── POST /api/admin/users/{memberNo}/suspend ─────────────────────────────
app.MapPost("/api/admin/users/{memberNo}/suspend", async (
    ulong memberNo,
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    var body = await ctx.Request.ReadFromJsonAsync<SuspendRequest>();
    var adminNo = GetAdminNoClaim(ctx, adminAuth);
    if (adminNo is null) return Results.Unauthorized();
    var ok = await adminRepo.SuspendPlayerAsync(memberNo, adminNo.Value, body?.Reason ?? "");
    return ok ? Results.Ok(new { suspended = true }) : Results.NotFound(new { error = "NOT_FOUND" });
}).RequireCors("AdminPolicy");

// ── POST /api/admin/users/{memberNo}/unsuspend ───────────────────────────
app.MapPost("/api/admin/users/{memberNo}/unsuspend", async (
    ulong memberNo,
    HttpContext ctx,
    AdminAuthService adminAuth,
    AdminRepository adminRepo) =>
{
    if (RequireAdminAuth(ctx, adminAuth) is { } err) return err;
    var adminNo = GetAdminNoClaim(ctx, adminAuth);
    if (adminNo is null) return Results.Unauthorized();
    var ok = await adminRepo.UnsuspendPlayerAsync(memberNo, adminNo.Value);
    return ok ? Results.Ok(new { unsuspended = true }) : Results.NotFound(new { error = "NOT_FOUND" });
}).RequireCors("AdminPolicy");

// ─── 管理者向け公知送信 ────────────────────────────────────────────────────
// POST /api/admin/notice   body: { "message": "...", "color": 0 }
// 原典: HMajRootServer::SendNoticeToAll — 全接続クライアントへ公知メッセージを送信
app.MapPost("/api/admin/notice", async (
    HttpContext httpCtx,
    MajakServer.Infrastructure.GradeRankBackgroundService noticeSvc) =>
{
    var body = await httpCtx.Request.ReadFromJsonAsync<NoticeRequest>();
    if (body == null || string.IsNullOrWhiteSpace(body.Message))
        return Results.BadRequest(new { error = "message required" });

    await noticeSvc.SendNoticeToAllAsync(body.Message, body.Color);
    return Results.Ok(new { sent = true });
});

// ─── 認証 REST API ─────────────────────────────────────────────────
// POST /auth/majak-login
// AP-02 §1 準拠: Hangame ログインクッキーを復号してプレイヤー情報を返す。
//
// リクエスト:
//   Content-Type: application/json
//   { "loginCookie": "hangametest=<URL_ENCODED_CSV>" }
//   または HTTP Cookie: login=hangametest=<URL_ENCODED_CSV>
//
// クッキー値プレフィックス:
//   production : "hangame="
//   alpha/test  : "hangametest="
//
// 失敗: 401 (復号失敗またはクッキー未設定)
app.MapPost("/auth/majak-login", async Task<IResult> (
    HttpContext ctx,
    HttpRequest req,
    GamePlayerRepository gamePlayers,
    PlayerRepository playerRepo,
    PlayerSessionService sessions,
    GameAuthTokenService gameAuth) =>
{
    // 1) Body から loginCookie を取得 (なければ HTTP Cookie ヘッダーから)
    string? cookieValue = null;
    MajakLoginRequest? body = null;
    if (req.HasJsonContentType())
    {
        body = await req.ReadFromJsonAsync<MajakLoginRequest>();
        cookieValue = body?.LoginCookie;
    }
    if (string.IsNullOrWhiteSpace(cookieValue))
    {
        // HTTP Cookie ヘッダーの "login" クッキーから取得
        cookieValue = ctx.Request.Cookies["login"];
    }

    if (string.IsNullOrWhiteSpace(cookieValue))
        return Results.Unauthorized();

    // 2) 復号
    var fields = HangameCookieDecryptor.ParseCookie(cookieValue);
    if (fields is null)
        return Results.Unauthorized();

    if (!fields.TryGetValue("userid",   out var memberNo) || string.IsNullOrEmpty(memberNo))
        return Results.Unauthorized();

    fields.TryGetValue("name",     out var name);
    fields.TryGetValue("sex",      out var sex);
    fields.TryGetValue("avatarid", out var avatarId);
    fields.TryGetValue("password", out var cookiePassword);
    var password = LegacyLaunchPassword.Extract(body?.KeyPwd);
    var passwordSource = !string.IsNullOrEmpty(password) ? "body.keyPwd" : "";
    if (string.IsNullOrEmpty(password))
    {
        password = LegacyLaunchPassword.Extract(body?.LaunchUrl);
        passwordSource = !string.IsNullOrEmpty(password) ? "body.launchUrl" : "";
    }
    if (string.IsNullOrEmpty(password))
    {
        password = LegacyLaunchPassword.Extract(body?.Referrer);
        passwordSource = !string.IsNullOrEmpty(password) ? "body.referrer" : "";
    }
    if (string.IsNullOrEmpty(password))
    {
        password = LegacyLaunchPassword.Extract(req.Headers.Referer.ToString());
        passwordSource = !string.IsNullOrEmpty(password) ? "header.referer" : "";
    }
    if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(cookiePassword))
    {
        password = cookiePassword;
        passwordSource = "cookie.password";
    }

    ctx.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Auth.HangeLogin")
        .LogInformation(
            "Hange login parsed. MemberNoLength={MemberNoLength}, PasswordSource={PasswordSource}, PasswordLength={PasswordLength}, CookiePasswordLength={CookiePasswordLength}",
            memberNo.Length,
            string.IsNullOrEmpty(passwordSource) ? "none" : passwordSource,
            password?.Length ?? 0,
            cookiePassword?.Length ?? 0);

    // 3) 環境判定 (プレフィックスで判断)
    var isTest = cookieValue.TrimStart().StartsWith("hangametest=",
                     StringComparison.OrdinalIgnoreCase);

    var account = await gamePlayers.GetAccountAsync(memberNo);
    if (account is not null)
    {
        await gamePlayers.RefreshLoginAsync(memberNo, name ?? account.DisplayName, isTest);
        await playerRepo.SetDailyMissionAsync(memberNo, conditionType: 1, progressIncrement: 1);
    }
    var pix = sessions.IssuePix(memberNo);

    return Results.Ok(new
    {
        pix,
        accessToken = IssueGameAccessToken(gameAuth, memberNo, pix),
        memberNo = pix,
        name       = name    ?? string.Empty,
        sex        = account?.SexCode ?? string.Empty,
        avatarId   = account?.AvatarId ?? string.Empty,
        password   = password ?? string.Empty,
        isTestEnv  = isTest,
        requiresRegistration = account is null,
        accountStatus  = account?.AccountStatus ?? 0,
        termsAgreed    = account?.TermsAgreed ?? false,
        // accountStatus: 0=承認待ち 1=プレイ可能 2=停止中
    });
});

app.MapPost("/auth/majak-register", async Task<IResult> (
    HttpContext ctx,
    MajakRegisterRequest body,
    GamePlayerRepository gamePlayers,
    PlayerRepository playerRepo,
    PlayerSessionService sessions,
    GameAuthTokenService gameAuth) =>
{
    var cookieValue = body.LoginCookie;
    if (string.IsNullOrWhiteSpace(cookieValue))
        cookieValue = ctx.Request.Cookies["login"];

    var fields = string.IsNullOrWhiteSpace(cookieValue)
        ? null
        : HangameCookieDecryptor.ParseCookie(cookieValue);
    if (fields is null
        || !fields.TryGetValue("userid", out var memberNo)
        || string.IsNullOrWhiteSpace(memberNo))
    {
        return Results.Unauthorized();
    }

    var sexCode = body.Sex?.ToUpperInvariant() ?? string.Empty;
    if (sexCode is not ("M" or "F"))
        return Results.BadRequest(new { error = "INVALID_SEX" });
    if (!AvatarCatalog.IsValid(sexCode, body.AvatarId))
        return Results.BadRequest(new { error = "INVALID_AVATAR" });

    fields.TryGetValue("name", out var displayName);
    var isTest = cookieValue!.TrimStart().StartsWith(
        "hangametest=", StringComparison.OrdinalIgnoreCase);

    var account = await gamePlayers.GetAccountAsync(memberNo);
    if (account is null)
    {
        await gamePlayers.RegisterAsync(
            memberNo,
            displayName ?? string.Empty,
            sexCode,
            body.AvatarId!,
            isTest);
account = new GamePlayerAccount(displayName ?? string.Empty, sexCode, body.AvatarId!, 0, null);
    }
    await playerRepo.SetDailyMissionAsync(memberNo, conditionType: 1, progressIncrement: 1);
    var pix = sessions.IssuePix(memberNo);

    return Results.Ok(new
    {
        pix,
        accessToken = IssueGameAccessToken(gameAuth, memberNo, pix),
        memberNo = pix,
        name = displayName ?? account.DisplayName,
        sex = account.SexCode,
        avatarId = account.AvatarId,
        isTestEnv = isTest,
        requiresRegistration = false,
        accountStatus = account.AccountStatus,
        termsAgreed   = account.TermsAgreed,
    });
});

// POST /auth/agree-terms
// 利用規約同意を記録する。承認待ち状態 (account_status=0) は変わらない。
app.MapPost("/auth/agree-terms", async Task<IResult> (
    HttpContext ctx,
    HttpRequest req,
    GamePlayerRepository gamePlayers) =>
{
    string? cookieValue = null;
    if (req.HasJsonContentType())
    {
        var body2 = await req.ReadFromJsonAsync<MajakLoginRequest>();
        cookieValue = body2?.LoginCookie;
    }
    if (string.IsNullOrWhiteSpace(cookieValue))
        cookieValue = ctx.Request.Cookies["login"];

    var fields = string.IsNullOrWhiteSpace(cookieValue)
        ? null : HangameCookieDecryptor.ParseCookie(cookieValue);
    if (fields is null || !fields.TryGetValue("userid", out var memberNo)
                       || string.IsNullOrWhiteSpace(memberNo))
        return Results.Unauthorized();

    var account = await gamePlayers.GetAccountAsync(memberNo);
    if (account is null) return Results.NotFound(new { error = "ACCOUNT_NOT_FOUND" });
    if (account.AccountStatus == 2) return Results.StatusCode(StatusCodes.Status403Forbidden);

    await gamePlayers.AgreeToTermsAsync(memberNo);
    return Results.Ok(new { accountStatus = 0, termsAgreed = true });
});

// ─── チャンネル一覧 REST API ──────────────────────────────────
// GET /api/channels
// HANGAME.CHANELMAST × CHANELWT から直接取得する。
// チャンネルサーバーを使用しない新規実装向け。
// ─── チャンネルサーバー URL 解決 REST API ─────────────────────────
// GET /api/channel/{chanelId}/server
// Redis チャンネルリースから担当サーバー URL を返す。
// 割り当て済みならそのまま返し、未割り当てなら担当チャンネル数最小のサーバーに動的割り当てする。
app.MapGet("/api/channel/{chanelId}/server",
    async (string chanelId, ServerLoadService load) =>
    {
        var url = await load.ResolveChannelServerAsync(chanelId);
        return Results.Ok(new { serverUrl = url });
    });

// ─── チャンネルメンバー管理 REST API ──────────────────────────────
// AP-04 §8: ロビーは WebSocket 不使用。チャンネルのメンバーリストは Redis で管理する。

// POST /api/channel/{chanelId}/enter  body: {pix, nickname, rating, sex, avatarId}
app.MapPost("/api/channel/{chanelId}/enter",
    async (HttpContext ctx, string chanelId, ChannelEnterRequest req, ChannelMemberService svc, PlayerSessionService sessions, GameAuthTokenService gameAuth) =>
    {
        var requestPix = req.Pix ?? req.MemberNo;
        if (string.IsNullOrWhiteSpace(requestPix)) return Results.BadRequest();
        var auth = RequireGameAuth(ctx, gameAuth);
        if (auth is null) return Results.Unauthorized();
        if (requestPix != auth.MemberNo && requestPix != auth.Pix
            && sessions.ResolveMemberNo(requestPix) != auth.MemberNo)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var pix = auth.Pix;
        await svc.EnterAsync(chanelId, pix, req.Nickname ?? "",
            req.Rating, req.Sex ?? "male", req.AvatarId ?? "");
        return Results.Ok();
    });

// POST /api/channel/{chanelId}/leave  body: {pix}
app.MapPost("/api/channel/{chanelId}/leave",
    async (HttpContext ctx, string chanelId, ChannelLeaveRequest req, ChannelMemberService svc, PlayerSessionService sessions, GameAuthTokenService gameAuth) =>
    {
        var requestPix = req.Pix ?? req.MemberNo;
        if (string.IsNullOrWhiteSpace(requestPix)) return Results.BadRequest();
        var auth = RequireGameAuth(ctx, gameAuth);
        if (auth is null) return Results.Unauthorized();
        if (requestPix != auth.MemberNo && requestPix != auth.Pix
            && sessions.ResolveMemberNo(requestPix) != auth.MemberNo)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var pix = auth.Pix;
        await svc.LeaveAsync(chanelId, pix);
        return Results.Ok();
    });

// GET /api/channel/{chanelId}/members
app.MapGet("/api/channel/{chanelId}/members",
    async (string chanelId, ChannelMemberService svc) =>
    {
        var members = await svc.GetMembersAsync(chanelId);
        return Results.Ok(members.Select(m => new
        {
            pix = m.MemberNo,
            nickname = m.Nickname,
            rating   = m.Rating,
            sex      = m.Sex,
            avatarId = m.AvatarId,
        }));
    });

// GET /api/channel/{chanelId}/rooms
// AP-04 §8 確定版: Redis TTL ルームから取得。
// サーバーが落ちると TTL 更新が止まり、最大 30 秒後にルームが自動消滅 (ゴーストルーム防止)。
app.MapGet("/api/channel/{chanelId}/rooms",
    async (string chanelId, RoomRegistryService roomRegistry) =>
    {
        var rooms = await roomRegistry.GetChannelRoomsAsync(chanelId);
        return Results.Ok(rooms.Select(r => new
        {
            roomId     = r.RoomId,
            title      = r.Title,
            isPrivate  = r.IsPrivate,
            memberCnt  = r.MemberCnt,
            memberMax  = r.MemberMax,
            maxViewer  = r.MaxViewer,
            roomOption = r.RoomOption,
            serverUrl  = r.ServerUrl,
        }));
    });

// ─── 最小ルーム数サーバー選択 API ──────────────────────────────────
// GET /api/room/best-server
// AP-04 §8: ルーム作成時にルーム数が最小の生存サーバー URL を返す。
// Redis が利用不可の場合は ChannelServerSettings.ServerUrl を返す (フォールバック)。
app.MapGet("/api/room/best-server",
    async (ServerLoadService load) =>
    {
        var url = await load.GetBestServerAsync();
        return Results.Ok(new { serverUrl = url });
    });

// ─── プレイヤープロフィール REST API ─────────────────────────────────
// GET /api/player/profile?memberNo={id}
// MJKCOMMONRAT からゲームコイン・称号などのプレイヤー情報を返す。
// チャンネル未入室状態でニックネーム・コイン表示に使用する。
app.MapGet("/api/player/profile", async (HttpContext ctx, string? memberNo, PlayerRepository playerRepo, RatingService ratingService, GameMoneyService moneyService, PlayerSessionService sessions, GameAuthTokenService gameAuth) =>
{
    var auth = RequireGameAuth(ctx, gameAuth);
    if (auth is null) return Results.Unauthorized();
    memberNo = auth.MemberNo;

    var player = new MajakServer.Models.Player.MajakPlayer { MemberNo = memberNo };

    // MJKCOMMONRAT が未存在なら初回作成
    if (!await playerRepo.ExistsCommonRatAsync(memberNo))
        await moneyService.CreateCommonRatWithDefaultMoneyHistAsync(memberNo, MajakServer.Models.Protocol.GameConst.DefaultMoney, "");

    await playerRepo.LoadCommonRatAsync(player);
    ratingService.UpdatePlayerLevel(player);

    return Results.Ok(new
    {
        gamMoney   = player.GamMoney,
        slevel     = player.SLevel,
        nlevel     = player.NLevel,
        rating     = player.Rating,
        trickTitle = player.TrickTitle,
        majakTitle = player.MajakTitle,
        gemCount   = player.GemCount,
        cashCount  = player.CashCount,
    });
});

// GET /api/shop/cash-products
// キャッシュ購入画面向け。ゲーム認証済みユーザーに有効な Web 商品だけを公開する。
app.MapGet("/api/shop/cash-products", async (HttpContext ctx, AdminRepository adminRepo, GameAuthTokenService gameAuth) =>
{
    if (RequireGameAuth(ctx, gameAuth) is null) return Results.Unauthorized();

    var products = await adminRepo.GetActiveWebCashProductsAsync();
    return Results.Ok(products.Select(product => new
    {
        productId = product.ProductId,
        displayName = product.DisplayName,
        cashAmount = product.CashAmount,
        priceJpy = product.PriceJpy,
    }));
});

// GET /api/shop/convenience-items
// billing_item_master の販売中アイテムをキャッシュショップに公開する。
app.MapGet("/api/shop/convenience-items", async (HttpContext ctx, ItemRepository itemRepo, GameAuthTokenService gameAuth) =>
{
    if (RequireGameAuth(ctx, gameAuth) is null) return Results.Unauthorized();
    return Results.Ok(await itemRepo.GetActiveBillingShopItemsAsync());
});

// GET /api/player/continue-room?memberNo={id}
// 対局中に切断したプレイヤーの続行先ルームを Redis から取得する。
app.MapGet("/api/player/continue-room", async (HttpContext ctx, string? memberNo, RoomRegistryService roomRegistry, PlayerSessionService sessions, GameAuthTokenService gameAuth) =>
{
    var auth = RequireGameAuth(ctx, gameAuth);
    if (auth is null) return Results.Unauthorized();
    memberNo = auth.MemberNo;

    var room = await roomRegistry.GetContinueRoomAsync(memberNo);
    if (room == null)
        return Results.Ok(new { found = false });

    return Results.Ok(new
    {
        found = true,
        pix = sessions.GetPixByMemberNo(auth.MemberNo) ?? auth.Pix,
        roomId = room.RoomId,
        chanelId = room.ChanelId,
        channelId = room.ChanelId,
        title = room.Title,
        serverUrl = room.ServerUrl,
        roomOption = room.RoomOption,
        updatedAt = room.UpdatedAt,
    });
});

app.MapGet("/api/channels", async (MasterCacheService masterCache, ChannelMemberService members, RoomRegistryService roomRegistry) =>
{
    var channels = await masterCache.GetChannelListAsync("MAJAK4");
    var result = new List<object>();
    foreach (var c in channels)
    {
        var currentMembers = await members.GetMembersAsync(c.SubId);
        var currentRooms = await roomRegistry.GetChannelRoomsAsync(c.SubId);
        result.Add(new
        {
            chanelId   = c.ChanelId,
            subId      = c.SubId,
            chanelName = c.ChanelName,
            maxMember  = c.MaxMember,
            maxRoom    = c.MaxRoom,
            chanelType = c.ChanelType,
            unitMoney  = c.UnitMoney,
            memberCnt  = currentMembers.Count,
            usedRoom   = currentRooms.Count,
        });
    }
    return Results.Ok(result);
});

// ─── Google 認証 REST API ──────────────────────────────────────────
// POST /auth/google-login-redirect (Google GIS redirect mode)
// Google Sign-In redirect mode から送信される form credential を受け取り、
// 既存会員は refresh cookie 発行後にトップへ戻し、未登録会員は登録フローへ戻す。
app.MapPost("/auth/google-login-redirect", async Task<IResult> (
    HttpContext ctx,
    GamePlayerRepository gamePlayers,
    PlayerRepository playerRepo,
    LogRepository logRepo,
    IConfiguration config,
    ILogger<Program> logger,
    PlayerSessionService sessions,
    AuthRefreshSessionService refreshSessions) =>
{
    var clientAppUrl = config["ClientAppUrl"]?.TrimEnd('/') ?? $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var form = await ctx.Request.ReadFormAsync();
    var idToken = form["credential"].ToString();

    if (string.IsNullOrWhiteSpace(idToken))
    {
        logger.LogWarning("Google redirect login rejected because the credential was missing.");
        ClearPendingGoogleIdTokenCookie(ctx);
        return Results.Redirect($"{clientAppUrl}/?googleAuth=error");
    }

    var clientId = config["AdminSettings:GoogleClientId"];
    if (string.IsNullOrWhiteSpace(clientId))
    {
        logger.LogError("Google redirect login failed: Google client ID not configured.");
        ClearPendingGoogleIdTokenCookie(ctx);
        return Results.Redirect($"{clientAppUrl}/?googleAuth=error");
    }

    GoogleJsonWebSignature.Payload payload;
    try
    {
        payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
            new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
    }
    catch (InvalidJwtException ex)
    {
        logger.LogWarning("Google redirect token validation failed: {Msg}", ex.Message);
        ClearPendingGoogleIdTokenCookie(ctx);
        return Results.Redirect($"{clientAppUrl}/?googleAuth=error");
    }

    var account = await gamePlayers.GetAccountByGoogleSubAsync(payload.Subject);
    if (account is null)
    {
        SetPendingGoogleIdTokenCookie(ctx, idToken);
        return Results.Redirect($"{clientAppUrl}/?googleAuth=register");
    }

    await gamePlayers.RefreshLoginAsync(account.MemberNo, account.DisplayName, false);
    await playerRepo.SetDailyMissionAsync(account.MemberNo, conditionType: 1, progressIncrement: 1);
    sessions.IssuePix(account.MemberNo);
    if (await IssueRefreshCookieAsync(ctx, refreshSessions, account.MemberNo))
    {
        await InsertLoginLogOnceAsync(ctx, logRepo, account.MemberNo, 0);
    }
    ClearPendingGoogleIdTokenCookie(ctx);
    return Results.Redirect($"{clientAppUrl}/");
});

// POST /auth/google-login  { idToken: string }
// Google ID トークンを検証し、プレイヤー情報を返す。
// 未登録の場合 requiresRegistration=true を返す (memberNo は空文字)。
app.MapPost("/auth/google-login", async Task<IResult> (
    HttpContext ctx,
    GooglePlayerLoginRequest body,
    GamePlayerRepository gamePlayers,
    PlayerRepository playerRepo,
    LogRepository logRepo,
    IConfiguration config,
    ILogger<Program> logger,
    PlayerSessionService sessions,
    AuthRefreshSessionService refreshSessions,
    GameAuthTokenService gameAuth) =>
{
    var clientId = config["AdminSettings:GoogleClientId"];
    if (string.IsNullOrWhiteSpace(clientId))
        return Results.Problem("Google client ID not configured.");

    var idToken = string.IsNullOrWhiteSpace(body.IdToken)
        ? ctx.Request.Cookies[PendingGoogleIdTokenCookieName]
        : body.IdToken;
    if (string.IsNullOrWhiteSpace(idToken)) return Results.Unauthorized();

    GoogleJsonWebSignature.Payload payload;
    try
    {
        payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
            new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
    }
    catch (InvalidJwtException ex)
    {
        logger.LogWarning("Google ID token validation failed: {Msg}", ex.Message);
        return Results.Unauthorized();
    }

    var googleSub = payload.Subject;

    var account = await gamePlayers.GetAccountByGoogleSubAsync(googleSub);
    if (account is null)
    {
        // 未登録 → フロントで会員登録フォームを表示させる
        SetPendingGoogleIdTokenCookie(ctx, idToken);
        return Results.Ok(new
        {
            memberNo             = string.Empty,
            name                 = string.Empty,
            sex                  = string.Empty,
            avatarId             = string.Empty,
            requiresRegistration = true,
            accountStatus        = 0,
            termsAgreed          = false,
        });
    }
    await gamePlayers.RefreshLoginAsync(account.MemberNo, account.DisplayName, false);
    await playerRepo.SetDailyMissionAsync(account.MemberNo, conditionType: 1, progressIncrement: 1);
    var pix = sessions.IssuePix(account.MemberNo);
    if (await IssueRefreshCookieAsync(ctx, refreshSessions, account.MemberNo))
    {
        await InsertLoginLogOnceAsync(ctx, logRepo, account.MemberNo, 0);
    }
    ClearPendingGoogleIdTokenCookie(ctx);

    return Results.Ok(new
    {
        pix,
        accessToken          = IssueGameAccessToken(gameAuth, account.MemberNo, pix),
        memberNo             = pix,
        name                 = account.DisplayName,
        sex                  = account.SexCode,
        avatarId             = account.AvatarId,
        requiresRegistration = false,
        accountStatus        = account.AccountStatus,
        termsAgreed          = account.TermsAgreed,
    });
});

// POST /auth/refresh
// HttpOnly refresh cookie を検証し、Google 画面なしで新しい pix と refresh token を発行する。
app.MapPost("/auth/refresh", async Task<IResult> (
    HttpContext ctx,
    GamePlayerRepository gamePlayers,
    PlayerRepository playerRepo,
    LogRepository logRepo,
    PlayerSessionService sessions,
    AuthRefreshSessionService refreshSessions,
    GameAuthTokenService gameAuth) =>
{
    var currentToken = ctx.Request.Cookies[AuthRefreshSessionService.CookieName];
    var memberNo = await refreshSessions.ValidateAsync(currentToken, ctx);
    if (string.IsNullOrWhiteSpace(memberNo))
    {
        ClearRefreshCookie(ctx);
        return Results.NoContent();
    }

    var account = await gamePlayers.GetAccountAsync(memberNo);
    if (account is null)
    {
        await refreshSessions.RevokeAsync(currentToken);
        ClearRefreshCookie(ctx);
        return Results.NoContent();
    }

    await refreshSessions.RevokeAsync(currentToken);
    await gamePlayers.RefreshLoginAsync(account.MemberNo, account.DisplayName, false);
    await playerRepo.SetDailyMissionAsync(account.MemberNo, conditionType: 1, progressIncrement: 1);
    var pix = sessions.IssuePix(account.MemberNo);
    if (await IssueRefreshCookieAsync(ctx, refreshSessions, account.MemberNo))
    {
        await InsertLoginLogOnceAsync(ctx, logRepo, account.MemberNo, 1);
    }

    return Results.Ok(new
    {
        pix,
        accessToken          = IssueGameAccessToken(gameAuth, account.MemberNo, pix),
        memberNo             = pix,
        name                 = account.DisplayName,
        sex                  = account.SexCode,
        avatarId             = account.AvatarId,
        requiresRegistration = false,
        accountStatus        = account.AccountStatus,
        termsAgreed          = account.TermsAgreed,
    });
});

app.MapPost("/auth/logout", async Task<IResult> (
    HttpContext ctx,
    AuthRefreshSessionService refreshSessions) =>
{
    var currentToken = ctx.Request.Cookies[AuthRefreshSessionService.CookieName];
    await refreshSessions.RevokeAsync(currentToken);
    ClearRefreshCookie(ctx);
    return Results.Ok(new { result = "ok" });
});

// POST /auth/google-register  { idToken, displayName, sex, avatarId }
// 利用規約同意・ニックネーム・性別・アバターを受け取り、新規会員登録する。
app.MapPost("/auth/google-register", async Task<IResult> (
    HttpContext ctx,
    GooglePlayerRegisterRequest body,
    GamePlayerRepository gamePlayers,
    PlayerRepository playerRepo,
    LogRepository logRepo,
    IConfiguration config,
    ILogger<Program> logger,
    PlayerSessionService sessions,
    AuthRefreshSessionService refreshSessions,
    GameAuthTokenService gameAuth) =>
{
    var clientId = config["AdminSettings:GoogleClientId"];
    if (string.IsNullOrWhiteSpace(clientId))
        return Results.Problem("Google client ID not configured.");

    var idToken = string.IsNullOrWhiteSpace(body.IdToken)
        ? ctx.Request.Cookies[PendingGoogleIdTokenCookieName]
        : body.IdToken;
    if (string.IsNullOrWhiteSpace(idToken)) return Results.Unauthorized();

    GoogleJsonWebSignature.Payload payload;
    try
    {
        payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
            new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
    }
    catch (InvalidJwtException ex)
    {
        logger.LogWarning("Google register token validation failed: {Msg}", ex.Message);
        return Results.Unauthorized();
    }

    var googleSub = payload.Subject;

    // ニックネームバリデーション (4 文字以上 16 文字以内)
    var nickname = body.DisplayName?.Trim() ?? string.Empty;
    if (nickname.Length < 4 || nickname.Length > 16)
        return Results.BadRequest(new { error = "NICKNAME_INVALID_LENGTH" });

    var available = await gamePlayers.IsNicknameAvailableAsync(nickname);
    if (!available)
        return Results.BadRequest(new { error = "NICKNAME_TAKEN" });

    var sexCode = body.Sex?.ToUpperInvariant() ?? string.Empty;
    if (sexCode is not ("M" or "F"))
        return Results.BadRequest(new { error = "INVALID_SEX" });
    if (string.IsNullOrWhiteSpace(body.AvatarId) || !AvatarCatalog.IsValid(sexCode, body.AvatarId))
        return Results.BadRequest(new { error = "INVALID_AVATAR" });

    // 既に登録済みの場合はそのまま返す (冪等)
    var existing = await gamePlayers.GetAccountByGoogleSubAsync(googleSub);
    if (existing is not null)
    {
        await gamePlayers.RefreshLoginAsync(existing.MemberNo, existing.DisplayName, false);
        await playerRepo.SetDailyMissionAsync(existing.MemberNo, conditionType: 1, progressIncrement: 1);
        var existingPix = sessions.IssuePix(existing.MemberNo);
        if (await IssueRefreshCookieAsync(ctx, refreshSessions, existing.MemberNo))
        {
            await InsertLoginLogOnceAsync(ctx, logRepo, existing.MemberNo, 0);
        }
        return Results.Ok(new
        {
            pix                  = existingPix,
            accessToken          = IssueGameAccessToken(gameAuth, existing.MemberNo, existingPix),
            memberNo             = existingPix,
            name                 = existing.DisplayName,
            sex                  = existing.SexCode,
            avatarId             = existing.AvatarId,
            requiresRegistration = false,
            accountStatus        = existing.AccountStatus,
            termsAgreed          = existing.TermsAgreed,
        });
    }

    var memberNo = (await gamePlayers.RegisterGoogleAsync(googleSub, nickname, sexCode, body.AvatarId!))
        .ToString(System.Globalization.CultureInfo.InvariantCulture);
    await playerRepo.SetDailyMissionAsync(memberNo, conditionType: 1, progressIncrement: 1);
    var pix = sessions.IssuePix(memberNo);
    if (await IssueRefreshCookieAsync(ctx, refreshSessions, memberNo))
    {
        await InsertLoginLogOnceAsync(ctx, logRepo, memberNo, 2);
    }

    logger.LogInformation("Google registration: memberNo={MemberNo}", memberNo);
    ClearPendingGoogleIdTokenCookie(ctx);

    return Results.Ok(new
    {
        pix,
        accessToken          = IssueGameAccessToken(gameAuth, memberNo, pix),
        memberNo = pix,
        name                 = nickname,
        sex                  = sexCode,
        avatarId             = body.AvatarId,
        requiresRegistration = false,
        accountStatus        = 0,
        termsAgreed          = true,
    });
});

// GET /auth/check-nickname?name=xxx
// ニックネームの使用可否を返す。4 文字未満は即 false。
app.MapGet("/auth/check-nickname", async (string? name, GamePlayerRepository gamePlayers) =>
{
    var trimmed = name?.Trim() ?? string.Empty;
    if (trimmed.Length < 4 || trimmed.Length > 16)
        return Results.Ok(new { available = false, reason = "LENGTH" });

    var available = await gamePlayers.IsNicknameAvailableAsync(trimmed);
    return Results.Ok(new { available, reason = available ? "" : "TAKEN" });
});

app.Run();

// ── リクエストモデル ────────────────────────────────────────────────
/// <summary>POST /auth/majak-login のリクエストボディ</summary>
internal sealed record MajakLoginRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("loginCookie")]
    string? LoginCookie,
    [property: System.Text.Json.Serialization.JsonPropertyName("keyPwd")]
    string? KeyPwd,
    [property: System.Text.Json.Serialization.JsonPropertyName("launchUrl")]
    string? LaunchUrl,
    [property: System.Text.Json.Serialization.JsonPropertyName("referrer")]
    string? Referrer
);

internal sealed record MajakRegisterRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("loginCookie")]
    string? LoginCookie,
    [property: System.Text.Json.Serialization.JsonPropertyName("sex")]
    string? Sex,
    [property: System.Text.Json.Serialization.JsonPropertyName("avatarId")]
    string? AvatarId
);

internal static partial class LegacyLaunchPassword
{
    public static string? Extract(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return null;

        var match = KeyPwdRegex().Match(source + "&");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var length))
            return null;

        var value = WebUtility.UrlDecode(match.Groups[2].Value);
        if (value.Length <= length)
            return value;

        return value[..length];
    }

    [GeneratedRegex(@"[?&#;]k111e:(\d+)=([^&]*)", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPwdRegex();
}

/// <summary>POST /api/channel/{chanelId}/enter のリクエストボディ</summary>
internal sealed record ChannelEnterRequest(
    string? Pix, string? MemberNo, string? Nickname, double Rating, string? Sex, string? AvatarId);

/// <summary>POST /api/channel/{chanelId}/leave のリクエストボディ</summary>
internal sealed record ChannelLeaveRequest(string? Pix, string? MemberNo);

/// <summary>POST /api/admin/notice のリクエストボディ</summary>
internal sealed record NoticeRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("message")]
    string Message,
    [property: System.Text.Json.Serialization.JsonPropertyName("color")]
    int Color = 0);

/// <summary>POST /api/admin/cash/adjust のリクエストボディ</summary>
internal sealed record CashAdjustRequest(ulong MemberNo, int Amount, string Memo);
internal sealed record SuspendRequest(string? Reason);

/// <summary>POST /api/admin/accounts のリクエストボディ</summary>
internal sealed record AdminAccountRequest(string Email, string Role);

/// <summary>POST /api/admin/auth/google のリクエストボディ</summary>
internal sealed record GoogleLoginRequest(string IdToken);

/// <summary>POST /auth/google-login のリクエストボディ</summary>
internal sealed record GooglePlayerLoginRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("idToken")]
    string IdToken);

/// <summary>POST /auth/google-register のリクエストボディ</summary>
internal sealed record GooglePlayerRegisterRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("idToken")]
    string IdToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("displayName")]
    string? DisplayName,
    [property: System.Text.Json.Serialization.JsonPropertyName("sex")]
    string? Sex,
    [property: System.Text.Json.Serialization.JsonPropertyName("avatarId")]
    string? AvatarId);