using MajakServer.Commands.Channel;
using MajakServer.Commands.Room;
using MajakServer.Commands.Game;
using MajakServer.Endpoints;
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
builder.Services.AddScoped<EconomyPolicyRepository>();
builder.Services.AddScoped<GameAnnouncementRepository>();

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
builder.Services.AddScoped<IGameEconomyPolicyService, GameEconomyPolicyService>();
builder.Services.AddScoped<TitleService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<MajItemService>();
builder.Services.AddScoped<MissionService>();
builder.Services.AddScoped<GameLogicService>();
builder.Services.AddSingleton<PaifuObjectStore>();
builder.Services.AddSingleton<PaifuFileService>();
builder.Services.AddSingleton<PaifuArchiveUploadQueue>();

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
builder.Services.AddHostedService<PaifuArchiveCleanupBackgroundService>();
builder.Services.AddHostedService<PaifuArchiveUploadBackgroundService>();

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
builder.Services.AddScoped<HanChatRejectCommand>();
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
    opt.MaximumParallelInvocationsPerClient = 2;
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
app.MapChannelEndpoints();
app.MapPlayerEndpoints();
app.MapAuthEndpoints();
app.MapAnnouncementEndpoints();
app.MapAdminEndpoints();

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

internal sealed record CollectionEquipRequest(string Category, string? TitleId);
internal sealed record AccountProfileUpdateRequest(int? BirthYear, string? AvatarId);

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
internal sealed record GameAnnouncementRequest(string Title, string Body, bool IsPublished, bool IsStartup);

/// <summary>POST /api/admin/cash/adjust のリクエストボディ</summary>
internal sealed record CashAdjustRequest(ulong MemberNo, int Amount, string Memo);
internal sealed record CurrencyAdjustRequest(ulong MemberNo, string? Currency, long Amount, string Memo);
internal sealed record EconomyPolicyRequest(long InitialGp, long FreeReplenishTargetGp, int FreeReplenishDailyLimit);
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
    [property: System.Text.Json.Serialization.JsonPropertyName("birthYear")]
    int? BirthYear,
    [property: System.Text.Json.Serialization.JsonPropertyName("avatarId")]
    string? AvatarId);