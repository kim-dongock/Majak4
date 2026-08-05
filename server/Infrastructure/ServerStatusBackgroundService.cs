using MajakServer.Services;
using MajakServer.Hubs;
using MajakServer.Models.Protocol;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace MajakServer.Infrastructure;

/// <summary>
/// ゲームサーバー自己登録サービス — AP-04 §8 参照
///
/// 8 秒ごとに:
///   1. このサーバーのルーム数を Redis に登録 (ServerLoadService)
///   2. 全アクティブルームの TTL をリフレッシュ (RoomRegistryService)
///      → サーバーが落ちると TTL 更新が止まり、最大 30 秒後にルームが自動消滅する
///   3. プライマリリーダーロックを取得 / 更新 (PrimaryLeaderService)
///      → サーバーが落ちると TTL=30秒 後に別サーバーが自動昇格
///
/// グレースフルシャットダウン時 (ApplicationStopping):
///   - Redis からサーバーエントリを即座に削除
///   - 全ルームエントリを即座に削除 (ゴーストルーム防止)
///   - リーダーロックを即座に解放 (フェイルオーバー高速化)
/// </summary>
public class ServerStatusBackgroundService : BackgroundService
{
    private readonly PlayerSessionService               _session;
    private readonly ServerLoadService                  _load;
    private readonly RoomRegistryService                _roomRegistry;
    private readonly ChannelMemberService               _channelMembers;
    private readonly LobbySessionLeaseService           _lobbySessions;
    private readonly IOptions<ChannelServerSettings>    _settings;
    private readonly PrimaryLeaderService               _leader;
    private readonly IHubContext<MajakGameHub>          _hub;
    private readonly IHostApplicationLifetime           _lifetime;
    private readonly ILogger<ServerStatusBackgroundService> _log;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(8);

    public ServerStatusBackgroundService(
        PlayerSessionService               session,
        ServerLoadService                  load,
        RoomRegistryService                roomRegistry,
        ChannelMemberService               channelMembers,
        LobbySessionLeaseService           lobbySessions,
        IOptions<ChannelServerSettings>    settings,
        PrimaryLeaderService               leader,
        IHubContext<MajakGameHub>          hub,
        IHostApplicationLifetime           lifetime,
        ILogger<ServerStatusBackgroundService> log)
    {
        _session      = session;
        _load         = load;
        _roomRegistry = roomRegistry;
        _channelMembers = channelMembers;
        _lobbySessions = lobbySessions;
        _settings     = settings;
        _leader       = leader;
        _hub          = hub;
        _lifetime     = lifetime;
        _log          = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serverUrl = _settings.Value.ServerUrl;
        _log.LogInformation("[ServerStatus] 自己登録開始: {Url}", serverUrl);

        // グレースフルシャットダウン: Redis エントリを即座に削除
        _lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                // サーバーエントリ削除
                _load.UnregisterSelfAsync(serverUrl).GetAwaiter().GetResult();

                // 全ルームエントリ削除 (ゴーストルーム防止)
                var rooms = _session.GetAllRooms()
                    .Select(r => (r.RoomId, r.ChannelId))
                    .ToList();
                _roomRegistry.RemoveAllRoomsAsync(rooms).GetAwaiter().GetResult();

                // 担当チャンネルの Redis リースを即解放 (動的チャンネル割り当て)
                var channels = _session.GetActiveChannelIds().ToList();
                _load.ReleaseChannelsAsync(channels, serverUrl).GetAwaiter().GetResult();

                // プライマリリーダーロックを即解放 (フェイルオーバー高速化)
                _leader.Release();
                _lobbySessions.ReleaseAllAsync().GetAwaiter().GetResult();

                _log.LogInformation("[ServerStatus] Redis 登録解除完了 (ルーム {RoomCount} 件, チャンネル {ChanCount} 件)",
                    rooms.Count, channels.Count);
            }
            catch (Exception ex)
            {
                _log.LogWarning("[ServerStatus] Redis 登録解除失敗: {Msg}", ex.Message);
            }
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RemoveExpiredNoActivePlayingRoomsAsync();

                // サーバールーム数を登録
                int roomCount = _session.GetTotalRoomCount();
                await _load.RegisterSelfAsync(serverUrl, roomCount);

                // 全アクティブルームの TTL をパイプラインで一括リフレッシュ (PerformanceAnalysis §2-2)
                var activeRooms = _session.GetAllRooms().ToArray();
                var roomIds = activeRooms.Select(r => r.RoomId);
                await _roomRegistry.RefreshTtlBatchAsync(roomIds);
                await _roomRegistry.RefreshContinueRoomsAsync(activeRooms);

                // 担当チャンネルの Redis リース TTL を一括更新 (動的チャンネル割り当て)
                var chanelIds = _session.GetActiveChannelIds();
                await _load.RefreshChannelLeasesBatchAsync(chanelIds, serverUrl);
                await _roomRegistry.RefreshChannelSetTtlBatchAsync(chanelIds);

                // チャンネルメンバー HASH は現在のセッション状態で同期する。
                // HASH 全体の TTL 更新だけだと切断済みメンバーが残り続けるため、
                // 生存 ConnectionId を持つ PlayerSessionService を正とする。
                foreach (var chanelId in chanelIds)
                    await _channelMembers.SyncChannelAsync(chanelId, _session.GetAllChannelPlayers(chanelId));

                var lostLobbyConnections = await _lobbySessions.RefreshAllAsync();
                foreach (string connectionId in lostLobbyConnections)
                {
                    await _hub.Clients.Client(connectionId).SendAsync(Cmd.ForcedLogout, new
                    {
                        error = "LOBBY_SESSION_LEASE_LOST",
                        message = "接続情報の有効期限が切れたため切断されました。",
                    });
                }

                // プライマリリーダーロックを取得 / 更新
                await _leader.TryAcquireOrRenewAsync();
            }
            catch (Exception ex)
            {
                _log.LogWarning("[ServerStatus] Redis 更新失敗: {Msg}", ex.Message);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RemoveExpiredNoActivePlayingRoomsAsync()
    {
        var graceSeconds = Math.Max(30, _settings.Value.ContinueRoomGraceSeconds);
        var expiredRooms = _session.RemoveExpiredNoActivePlayingRooms(
            TimeSpan.FromSeconds(graceSeconds),
            DateTimeOffset.UtcNow);

        foreach (var room in expiredRooms)
        {
            foreach (var player in room.Seats.Where(seat => seat != null).Select(seat => seat!))
                await _roomRegistry.ClearContinueRoomAsync(player.MemberNo);
            await _roomRegistry.RemoveRoomAsync(room.RoomId, room.ChannelId);
            await _hub.Clients.Group($"room_{room.RoomId}")
                .SendAsync(Cmd.AutoExitRoom, new Dictionary<string, object?>
                {
                    [GKey.Pix] = "",
                    ["memberNo"] = "",
                    ["message"] = "対局者が全員退室したため、観戦を終了します。",
                    [Key.RoomForceExitReason] = 0,
                });
            await _hub.Clients.Group($"chanel_{room.ChannelId}")
                .SendAsync(Cmd.RoomState, RoomStatePayload.BuildEmpty(room.RoomId, "expired"));
            _log.LogInformation(
                "Expired no-active playing room. roomId={RoomId} channelId={ChannelId} graceSeconds={GraceSeconds}",
                room.RoomId, room.ChannelId, graceSeconds);
        }
    }
}
