using Microsoft.AspNetCore.SignalR;
using MajakServer.Commands;
using MajakServer.Engine;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using Microsoft.Extensions.Logging;

namespace MajakServer.Services;

/// <summary>

///

///   StartGameLogicAsync    ↁEStartGameLogic
///   GamePlayProcessAsync   ↁEGamePlayProcess
///   ProxyPlayAsync         ↁEProxyPlay
///   OnInitKyokuAsync       ↁEOnInitKyoku
///   OnEndKyokuAsync        ↁEOnEndKyoku
///   OnEndGameAsync         ↁEOnEndGame
///   GameReportProcessAsync ↁEGameReportProcess + GameReport
///   MakeGameReportAsync    ↁEMakeGameReport
///



/// </summary>
public class GameLogicService
{
    private const string ChanceItemCode = "C01";
    private const int DailyMissionConditionPlay = 2;
    private const int DailyMissionConditionTop = 3;
    private const int CasualPointConditionPlay = 1;
    private const int TrainingNpcMinTurnDelayMs = 5000;
    private const int CasualPointSubTypeNotTop = 0;
    private const int CasualPointSubTypeTop = 1;
    private const int CasualPointTonpuRate = 1;
    private const int CasualPointHanchanRate = 2;

    private static readonly (int Kind, int Point, string IconCode)[] GameIconMaster =
    [
        (1, 1, "g00131"), (1, 10, "g00132"), (1, 100, "g00133"), (1, 500, "g00134"), (1, 2000, "g00135"), (1, 5000, "g00136"),
        (2, 5, "g00137"), (2, 10, "g00138"), (2, 100, "g00139"), (2, 500, "g00140"), (2, 1000, "g00141"),
        (3, 1, "g00142"), (3, 10, "g00143"), (3, 50, "g00144"), (3, 100, "g00145"), (3, 300, "g00146"), (3, 1000, "g00147"),
        (4, 3, "g00148"), (4, 5, "g00149"), (4, 10, "g00150"), (4, 15, "g00151"), (4, 20, "g00152"),
    ];

    private readonly PlayerSessionService _session;
    private readonly HistoryRepository    _historyRepo;
    private readonly LogRepository        _mysqlLog;
    private readonly RatingService        _ratingService;
    private readonly PlayerRepository     _playerRepo;
    private readonly GameMoneyService     _moneyService;
    private readonly TitleService         _titleService;
    private readonly TournamentService    _tournament;
    private readonly GradeRankService     _gradeRank;
    private readonly ILogger<GameLogicService>? _log;
    private readonly RoomRegistryService? _roomRegistry;
    private readonly ITrainingAiEvaluator _trainingAiEvaluator;
    private readonly TrainingAiLevel      _trainingAiLevel;
    private readonly bool                 _testEnvironment;
    private readonly bool                 _debugEndAfterEast1;

    public GameLogicService(
        PlayerSessionService session,
        HistoryRepository    historyRepo,
        LogRepository        mysqlLog,
        RatingService        ratingService,
        PlayerRepository     playerRepo,
        GameMoneyService     moneyService,
        TitleService         titleService,
        TournamentService    tournament,
        GradeRankService     gradeRank,
        IConfiguration       config,
        ILogger<GameLogicService>? log = null,
        RoomRegistryService? roomRegistry = null)
    {
        _session       = session;
        _historyRepo   = historyRepo;
        _mysqlLog      = mysqlLog;
        _ratingService = ratingService;
        _playerRepo    = playerRepo;
        _moneyService  = moneyService;
        _titleService  = titleService;
        _tournament    = tournament;
        _gradeRank     = gradeRank;
        _log           = log;
        _roomRegistry  = roomRegistry;
        if (!Enum.TryParse(config["GameSettings:TrainingAiLevel"], ignoreCase: true, out _trainingAiLevel))
            _trainingAiLevel = TrainingAiLevel.Legacy;
        _trainingAiEvaluator = _trainingAiLevel switch
        {
            TrainingAiLevel.Advanced => new AdvancedTrainingAiEvaluator(),
            _ => new LegacyTrainingAiEvaluator(),
        };
        _testEnvironment = config.GetValue<bool>("GameSettings:TestEnvironment", false);
        _debugEndAfterEast1 = config.GetValue<bool>("RuntimeFlag:DebugEndAfterEast1", false);
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>


    ///   majak.InitHanchan(rule) ↁEAddToParser_HanchanInfo ↁESendDataToAll
    ///   ↁESendPaiInfoToAll ↁEOnInitKyoku
    /// </summary>
    public virtual async Task StartGameLogicAsync(GameRoom room, CommandContext ctx)
    {
        _log?.LogInformation("StartGameLogic begin. roomId={RoomId} roomState={RoomState} playerCount={PlayerCount} roomOption={RoomOption}", room.RoomId, room.State, room.PlayerCount, room.RoomOption);


        room.State = GameRoomState.Playing;
        room.ResetGameActions();
        room.ResetGameReportProcess();
        room.LastGameReportPayload = null;
        PrepareTrainingNpcProfiles(room);
        if (_roomRegistry != null)
        {
            await _roomRegistry.RegisterRoomAsync(
                room.RoomId, room.ChannelId, room.RoomTitle,
                room.IsPrivate, room.ActivePlayerCount, room.LimitCnt,
                room.ServerUrl, room.RoomOption, room.MaxViewer,
                RoomStatePayload.GetLegacyRoomState(room), 1);
            foreach (var seat in room.Seats.Where(seat => seat != null).Select(seat => seat!))
                await _roomRegistry.SetContinueRoomAsync(seat.MemberNo, room);
        }




        int gemGame = CalcGemGame(room);

        if (room.RoomOption.Length > 13)
        {
            var opt = room.RoomOption.ToCharArray();
            opt[13] = (char)('0' + gemGame);
            room.RoomOption = new string(opt);
        }
        PrepareGameClientReadyGate(room);

        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.AutoStart, BuildAutoStartPayload(room, gemGame));


        await ctx.Clients.Group($"chanel_{room.ChannelId}")
            .SendAsync(Cmd.RoomState, RoomStatePayload.Build(room, "game_started"));


        var rule = BuildRuleInfo(room);
        room.Engine.SetDebugEndAfterEast1(_debugEndAfterEast1);
        room.Engine.InitHanchan(rule);

        // エンジン order ↁEルーム playerPos の対応を保孁E

        Array.Fill(room.SeatToEngineOrder, -1);
        for (int i = 0; i < 4; i++)
        {
            room.SeatToEngineOrder[room.Engine.HanchanInfo.Player[i]] = i;
            var playerPos = room.Engine.HanchanInfo.Player[i];
            if (playerPos >= 0 && playerPos < room.Seats.Length && room.Seats[playerPos] != null)
            room.Seats[playerPos]!.EngineOrder = i;
        }

        _log?.LogInformation("StartGameLogic seat mapping. roomId={RoomId} engineToPlayerPos={EngineToPlayerPos} seatToEngineOrder={SeatToEngineOrder}",
            room.RoomId,
            string.Join(',', room.Engine.HanchanInfo.Player),
            string.Join(',', room.SeatToEngineOrder));


        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.GamePlay, BuildHanchanInfo(room));


        await OnInitKyokuAsync(room, ctx);
        await StartGameActionsIfClientsReadyAsync(room, ctx);
    }

    public Task<bool> MarkGameClientReadyAsync(int roomId, string connectionId)
    {
        var room = _session.GetRoom(roomId);
        if (room == null || string.IsNullOrEmpty(connectionId)) return Task.FromResult(false);

        bool isAllReady;
        int readyCount;
        int expectedCount;
        lock (room.GameClientReadyLock)
        {
            PruneGameClientReadyLocked(room);
            room.GameClientReadyConnectionIds.Add(connectionId);
            var expected = GetExpectedGameClientConnectionIds(room);
            room.GameClientReadyConnectionIds.RemoveWhere(id => !expected.Contains(id));
            readyCount = room.GameClientReadyConnectionIds.Count;
            expectedCount = expected.Count;
            isAllReady = IsGameClientReadyLocked(room);
            if (isAllReady) room.GameClientReadyTcs?.TrySetResult(true);
        }

        _log?.LogInformation("Game client ready. roomId={RoomId} connectionId={ConnectionId} ready={ReadyCount}/{ExpectedCount} allReady={AllReady}",
            roomId, connectionId, readyCount, expectedCount, isAllReady);
        return Task.FromResult(isAllReady);
    }

    public async Task StartGameActionsAsync(GameRoom room, CommandContext ctx)
    {
        await StartGameActionsCoreAsync(room, ctx);
    }

    public async Task<bool> StartGameActionsIfClientsReadyAsync(GameRoom room, CommandContext ctx)
    {
        bool isAllReady;
        lock (room.GameClientReadyLock)
        {
            PruneGameClientReadyLocked(room);
            isAllReady = IsGameClientReadyLocked(room);
        }

        if (!isAllReady) return false;
        return await StartGameActionsCoreAsync(room, ctx);
    }

    private async Task<bool> StartGameActionsCoreAsync(GameRoom room, CommandContext ctx)
    {
        if (!room.TryStartGameActions())
        {
            await ProxyEmptySeatsAsync(room, ctx);
            return false;
        }

        _log?.LogInformation("Game actions starting after client ready. roomId={RoomId}", room.RoomId);
        await SendValidActionsToPlayersAsync(room, ctx);
        await ProxyEmptySeatsAsync(room, ctx);
        return true;
    }

    private static void PrepareGameClientReadyGate(GameRoom room)
    {
        lock (room.GameClientReadyLock)
        {
            room.GameClientReadyConnectionIds.Clear();
            room.GameClientReadyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private async Task WaitForGameClientsReadyAsync(GameRoom room, TimeSpan timeout)
    {
        Task readyTask;
        int expectedCount;
        lock (room.GameClientReadyLock)
        {
            expectedCount = GetExpectedGameClientConnectionIds(room).Count;
            if (expectedCount == 0 || IsGameClientReadyLocked(room)) return;
            readyTask = room.GameClientReadyTcs?.Task ?? Task.CompletedTask;
        }

        _log?.LogInformation("Waiting for game client ready. roomId={RoomId} expectedCount={ExpectedCount} timeoutMs={TimeoutMs}",
            room.RoomId, expectedCount, (int)timeout.TotalMilliseconds);

        var completed = await Task.WhenAny(readyTask, Task.Delay(timeout));
        if (completed == readyTask)
        {
            _log?.LogInformation("All game clients ready. roomId={RoomId}", room.RoomId);
            return;
        }

        int readyCount;
        lock (room.GameClientReadyLock)
        {
            PruneGameClientReadyLocked(room);
            readyCount = room.GameClientReadyConnectionIds.Count;
            expectedCount = GetExpectedGameClientConnectionIds(room).Count;
        }
        _log?.LogWarning("Game client ready wait timed out; continuing startup. roomId={RoomId} ready={ReadyCount}/{ExpectedCount}",
            room.RoomId, readyCount, expectedCount);
    }

    private static bool IsGameClientReadyLocked(GameRoom room)
    {
        var expected = GetExpectedGameClientConnectionIds(room);
        return expected.Count > 0 && expected.All(room.GameClientReadyConnectionIds.Contains);
    }

    private static void PruneGameClientReadyLocked(GameRoom room)
    {
        var expected = GetExpectedGameClientConnectionIds(room).ToHashSet();
        room.GameClientReadyConnectionIds.RemoveWhere(connectionId => !expected.Contains(connectionId));
    }

    private static List<string> GetExpectedGameClientConnectionIds(GameRoom room)
    {
        return room.Seats
            .Where(player => player != null && !player.IsViewer && !player.IsOutPlayer && !string.IsNullOrEmpty(player.ConnectionId))
            .Select(player => player!.ConnectionId)
            .Distinct()
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>

    /// 全員にブロードキャストする、E

    ///   majak.ProcessAction(nOrder, eAction, nBipaiIndex, nBipaiCount)
    ///   ↁESendPaiInfoToAll ↁESendDataToAll(ActionInfo) ↁEstate switch
    /// </summary>
    public virtual async Task GamePlayProcessAsync(GameRoom room, CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null)
        {
            _log?.LogWarning("GamePlayProcess skipped: missing player. roomId={RoomId} connectionId={ConnectionId}", room.RoomId, ctx.ConnectionId);
            return;
        }


        int  order      = ctx.GetInt("seatOrder");
        int  action     = ctx.GetInt("action");    // ACT enum 値
        var  bipaiIndex = ctx.GetIntArray("bipaiIndex");
        int  bipaiCount = bipaiIndex?.Length ?? 0;

        _log?.LogDebug("GamePlayProcess received. roomId={RoomId} memberNo={MemberNo} connectionId={ConnectionId} seatOrder={SeatOrder} action={Action} bipaiCount={BipaiCount} bipaiIndex={BipaiIndex} playerSeatPos={PlayerSeatPos} playerEngineOrder={PlayerEngineOrder} roomState={RoomState}",
            room.RoomId,
            player.MemberNo,
            ctx.ConnectionId,
            order,
            action,
            bipaiCount,
            bipaiIndex == null ? "" : string.Join(',', bipaiIndex),
            player.SeatPos,
            player.EngineOrder,
            room.State);

        if (order < 0 || order >= GameConst.PlayerMaxCount)
        {
            _log?.LogWarning("GamePlayProcess skipped: invalid order. roomId={RoomId} memberNo={MemberNo} seatOrder={SeatOrder}", room.RoomId, player.MemberNo, order);
            return;
        }

        // ── エンジン排他制御 (PerformanceAnalysis §1-2)

        await room.EngineLock.WaitAsync();
        try
        {

        var act = (Engine.Act)action;
        var indices = bipaiIndex ?? Array.Empty<int>();
        long actionSeq = ctx.GetLong("actionSeq");
        if (!ValidatePendingAction(room, order, actionSeq, act, indices))
        {
            _log?.LogWarning("GamePlayProcess ignored stale action. roomId={RoomId} memberNo={MemberNo} order={Order} act={Act} actionSeq={ActionSeq}",
                room.RoomId,
                player.MemberNo,
                order,
                act,
                actionSeq);
            return;
        }
        _log?.LogDebug("GamePlayProcess before engine. roomId={RoomId} order={Order} engineMode={EngineMode} act={Act} indices={Indices}",
            room.RoomId,
            order,
            room.Engine.Player[order].Mode,
            act,
            string.Join(',', indices));
        var result  = room.Engine.ProcessAction(order, act, indices, indices.Length);
        _log?.LogDebug("GamePlayProcess engine result. roomId={RoomId} order={Order} act={Act} result={Result} gameStatus={GameStatus}",
            room.RoomId,
            order,
            act,
            result,
            room.Engine.GameStatus);
        if (result != Engine.ActionResult.Ok)
        {

            var reason = $"Engine rejected action. roomId={room.RoomId} memberNo={player.MemberNo} playerSeatPos={player.SeatPos} playerEngineOrder={player.EngineOrder} order={order} playerMode={room.Engine.Player[order].Mode} act={act} result={result} bipaiIndex={string.Join(',', indices)}";
            _log?.LogWarning("GamePlayProcess aborting connection: {Reason}", reason);
            ctx.AbortConnectionWithReason(reason);
            return;
        }

        room.PendingActions[order] = null;


        var historyPaiInfo = await SendPaiInfoToAllAsync(room, ctx, isInit: false);
        var actionInfo = BuildActionInfo(room, order, action, indices, actionSeq);
        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.GamePlay, actionInfo);
        _log?.LogDebug("GamePlayProcess broadcast action. roomId={RoomId} order={Order} action={Action} leftCount={LeftCount}", room.RoomId, order, action, room.Engine.GetBipaiCount());


        if (historyPaiInfo != null) room.PlayHistory.Add(WrapHistoryPacket(Cmd.PaiInfoList, historyPaiInfo));
        room.PlayHistory.Add(actionInfo);


        _log?.LogDebug("GamePlayProcess post-action state. roomId={RoomId} gameStatus={GameStatus} roomState={RoomState}", room.RoomId, room.Engine.GameStatus, room.State);
        switch (room.Engine.GameStatus)
        {
            case Engine.GameStatus.Playing:
                break;
            case Engine.GameStatus.NewKyoku:
                _log?.LogInformation("GamePlayProcess entering OnInitKyoku. roomId={RoomId}", room.RoomId);
                await OnInitKyokuAsync(room, ctx);
                break;
            case Engine.GameStatus.EndKyoku:
                _log?.LogInformation("GamePlayProcess entering OnEndKyoku. roomId={RoomId}", room.RoomId);
                await OnEndKyokuAsync(room, ctx);
                break;
            case Engine.GameStatus.NotPlaying:
                _log?.LogInformation("GamePlayProcess entering OnEndGame. roomId={RoomId}", room.RoomId);
                room.ClearPendingActions();
                await OnEndGameAsync(room, ctx);
                return;
        }

        await SendValidActionsToPlayersAsync(room, ctx);



        // 期限到達時だぁEtimeout fallback として既定アクションを適用する、E
        await ProxyEmptySeatsAsync(room, ctx);
        }
        finally
        {
            room.EngineLock.Release();
        }
    }

    private async Task ProxyEmptySeatsAsync(GameRoom room, CommandContext ctx)
    {
        if (room.State != GameRoomState.Playing)
            return;
        if (PauseAutoProgressWhenNoActivePlayers(room, "empty-seat proxy loop"))
            return;

        for (int engineOrder = 0; engineOrder < GameConst.PlayerMaxCount; engineOrder++)
        {
            int seatPos = room.Engine.HanchanInfo.Player[engineOrder];
            if (seatPos < 0 || seatPos >= room.Seats.Length) continue;
            if (room.Seats[seatPos] != null) continue;
            if (room.Engine.Player[engineOrder].Mode == Engine.PlayerMode.None) continue;

            await ScheduleEmptySeatActionAsync(room, ctx, engineOrder);
            return;
        }
    }

    private async Task ScheduleEmptySeatActionAsync(GameRoom room, CommandContext ctx, int order)
    {
        var mode = room.Engine.Player[order].Mode;
        var currentPrompt = room.PendingActions[order];
        if (currentPrompt != null
            && currentPrompt.PlayerMode == mode
            && currentPrompt.DeadlineAt > DateTimeOffset.UtcNow)
            return;

        int speedNo = GetLegacySpeedNo(room);
        var speed = GetLegacySpeed(speedNo);
        int delayMs = mode == Engine.PlayerMode.Turn
            ? Math.Max(speed.Keep, TrainingNpcMinTurnDelayMs)
            : 1;
        var issuedAt = DateTimeOffset.UtcNow;
        var prompt = new PendingActionPrompt
        {
            ActionSeq = room.IssueActionSeq(),
            SeatOrder = order,
            PlayerMode = mode,
            IssuedAt = issuedAt,
            DeadlineAt = issuedAt.AddMilliseconds(delayMs),
        };
        room.PendingActions[order] = prompt;

        _log?.LogInformation("Training NPC action scheduled. roomId={RoomId} order={Order} mode={Mode} actionSeq={ActionSeq} roomOption={RoomOption} speedNo={SpeedNo} keepMs={KeepMs} delayMs={DelayMs} testEnvironment={TestEnvironment}",
            room.RoomId, order, mode, prompt.ActionSeq, room.RoomOption, speedNo, speed.Keep, delayMs, _testEnvironment);

        if (mode == Engine.PlayerMode.Turn)
        {
            await ctx.Clients.Group($"room_{room.RoomId}").SendAsync(Cmd.GamePlay, new
            {
                playType = "MJPID_ACTIONS",
                seatOrder = order,
                playerMode = mode.ToString(),
                actFlags = 0,
                horaErrorReason = "",
                actions = Array.Empty<object>(),
                tapCandidates = Array.Empty<int>(),
                timeLimit = Math.Max(1, (int)Math.Ceiling(delayMs / 1000.0)),
                actionSeq = prompt.ActionSeq,
                serverNow = issuedAt.ToUnixTimeMilliseconds(),
                deadlineAt = prompt.DeadlineAt.ToUnixTimeMilliseconds(),
            });
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            await room.EngineLock.WaitAsync();
            try
            {
                if (room.State != GameRoomState.Playing) return;
                int playerPos = room.Engine.HanchanInfo.Player[order];
                if (playerPos < 0 || playerPos >= room.Seats.Length || room.Seats[playerPos] != null) return;
                var pending = room.PendingActions[order];
                if (pending == null || pending.ActionSeq != prompt.ActionSeq) return;
                if (room.Engine.Player[order].Mode != prompt.PlayerMode) return;

                _log?.LogInformation("Training NPC action executing. roomId={RoomId} order={Order} mode={Mode} actionSeq={ActionSeq}",
                    room.RoomId, order, prompt.PlayerMode, prompt.ActionSeq);
                if (!await ProxyPlayAsync(room, ctx, order, useTrainingAi: true)) return;
                await ProxyEmptySeatsAsync(room, ctx);
            }
            finally
            {
                room.EngineLock.Release();
            }
        });
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>



    ///   練習場 NPC: TSU/RON を優先し、CEval で TAP/RIC を選抁E
    /// </summary>
    public async Task<bool> ProxyPlayAsync(GameRoom room, CommandContext ctx, int order, bool useTrainingAi = false)
    {
        if (PauseAutoProgressWhenNoActivePlayers(room, "proxy play"))
            return false;

        if (await TryScheduleDisconnectedPlayerTimeoutAsync(room, ctx, order, "proxy-play-disconnected"))
            return false;

        var ep = room.Engine.Player[order];
        Engine.Act eAct;
        int[] bipaiIdx = Array.Empty<int>();
        var actions = room.Engine.GetValidActions(order);

        switch (ep.Mode)
        {
            case Engine.PlayerMode.None:
                return false;  // 行動不要E

            case Engine.PlayerMode.Turn:
                if (useTrainingAi && actions.CanTsumo)
                {
                    eAct = Engine.Act.Tsu;
                }
                else if (useTrainingAi
                    && ep.RichiType == Engine.RichiType.None
                    && ep.Tehai.Count % 3 == 2)
                {
                    int aiType = room.Engine.HanchanInfo.Player[order];
                    var decision = _trainingAiEvaluator.Evaluate(room.Engine, order, aiType);
                    var discard = decision.DiscardBipaiIndex.HasValue
                        ? ep.Tehai.First(tile => tile.BipaiIndex == decision.DiscardBipaiIndex.Value)
                        : ep.Tehai.First(tile => tile.GetSerial() == decision.DiscardSerial);
                    eAct = decision.ShouldRiichi ? Engine.Act.Ric : Engine.Act.Tap;
                    bipaiIdx = new[] { discard.BipaiIndex };
                    ep.ResultRecord.DaidaCnt++;
                }
                else
                {
                    eAct = Engine.Act.Tap;
                    bipaiIdx = new[] { ep.Tehai.Count > 0 ? ep.Tehai.Last().BipaiIndex : 0 };
                    ep.ResultRecord.DaidaCnt++;
                }
                break;

            case Engine.PlayerMode.Furo:
            case Engine.PlayerMode.Chan:
                eAct = useTrainingAi && actions.CanRon ? Engine.Act.Ron : Engine.Act.Pas;
                break;

            default:
                eAct = Engine.Act.Pas;
                break;
        }

        _log?.LogInformation("Proxy action selected. roomId={RoomId} order={Order} mode={Mode} action={Action} trainingAi={TrainingAi} trainingAiLevel={TrainingAiLevel} bipaiIndex={BipaiIndex}",
            room.RoomId, order, ep.Mode, eAct, useTrainingAi, _trainingAiLevel, string.Join(',', bipaiIdx));
        var res = room.Engine.ProcessAction(order, eAct, bipaiIdx, bipaiIdx.Length);
        if (res != Engine.ActionResult.Ok && eAct == Engine.Act.Ron && actions.CanPass)
        {
            _log?.LogWarning("Proxy ron rejected; falling back to pass. roomId={RoomId} order={Order} mode={Mode} result={Result}",
                room.RoomId, order, ep.Mode, res);
            eAct = Engine.Act.Pas;
            bipaiIdx = Array.Empty<int>();
            res = room.Engine.ProcessAction(order, eAct, bipaiIdx, bipaiIdx.Length);
        }
        if (res != Engine.ActionResult.Ok)
        {
            _log?.LogWarning("Empty-seat proxy action rejected. roomId={RoomId} order={Order} mode={Mode} act={Act} result={Result}",
                room.RoomId, order, ep.Mode, eAct, res);
            return false;
        }

        room.PendingActions[order] = null;
        var historyPaiInfo = await SendPaiInfoToAllAsync(room, ctx, isInit: false);
        var actionInfo = BuildActionInfo(room, order, (int)eAct, bipaiIdx);
        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.GamePlay, actionInfo);
        if (historyPaiInfo != null) room.PlayHistory.Add(WrapHistoryPacket(Cmd.PaiInfoList, historyPaiInfo));
        room.PlayHistory.Add(actionInfo);

        switch (room.Engine.GameStatus)
        {
            case Engine.GameStatus.NewKyoku:   await OnInitKyokuAsync(room, ctx);  break;
            case Engine.GameStatus.EndKyoku:   await OnEndKyokuAsync(room, ctx);   break;
            case Engine.GameStatus.NotPlaying: await OnEndGameAsync(room, ctx);    break;
        }

        if (room.Engine.GameStatus != Engine.GameStatus.NotPlaying)
            await SendValidActionsToPlayersAsync(room, ctx);
        _log?.LogInformation("Proxy action completed. roomId={RoomId} order={Order} action={Action} gameStatus={GameStatus} leftCount={LeftCount} nextModes={NextModes}",
            room.RoomId, order, eAct, room.Engine.GameStatus, room.Engine.GetBipaiCount(), string.Join(',', room.Engine.Player.Select(player => player.Mode)));
    return true;
    }

    private bool PauseAutoProgressWhenNoActivePlayers(GameRoom room, string source)
    {
        if (room.State != GameRoomState.Playing || room.ActivePlayerCount > 0)
            return false;

        room.ClearPendingActions();
        room.NoActiveMembersSince ??= DateTimeOffset.UtcNow;
        _log?.LogWarning(
            "Auto game progress paused: no active players. roomId={RoomId} source={Source} playerCount={PlayerCount} viewerCount={ViewerCount} noActiveSince={NoActiveSince:o}",
            room.RoomId,
            source,
            room.PlayerCount,
            room.ViewerCount,
            room.NoActiveMembersSince);
        return true;
    }

    private static object BuildActionInfo(GameRoom room, int seatOrder, int action, int[] bipaiIndex, long actionSeq = 0)
    {
        return new
        {
            playType = "MJPID_ACTION",
            seatOrder,
            action,
            bipaiIndex,
            actionSeq,
            leftCount = room.Engine.GetBipaiCount(),
        };
    }

    private static object WrapHistoryPacket(string cmd, object payload)
        => new Dictionary<string, object?>
        {
            ["cmd"] = cmd,
            ["data"] = payload,
        };

    private async Task SendValidActionsToPlayersAsync(GameRoom room, CommandContext ctx)
    {
        if (PauseAutoProgressWhenNoActivePlayers(room, "valid action dispatch"))
            return;

        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            var actions = room.Engine.GetValidActions(order);
            if (!ShouldAutoResolvePassOnlyResponse(room, order, actions)) continue;

            _log?.LogDebug("MJPID_ACTIONS auto-resolving pass-only response. roomId={RoomId} order={Order} playerMode={PlayerMode}",
                room.RoomId,
                order,
                room.Engine.Player[order].Mode);
            await ExecuteServerDefaultActionAsync(room, ctx, order, Engine.Act.Pas, Array.Empty<int>(), "pass-only response");
            if (PauseAutoProgressWhenNoActivePlayers(room, "pass-only follow-up"))
                return;
            if (room.Engine.GameStatus != Engine.GameStatus.NotPlaying)
                await SendValidActionsToPlayersAsync(room, ctx);
            return;
        }

        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            int playerPos = room.Engine.HanchanInfo.Player[order];
            if (playerPos < 0 || playerPos >= room.Seats.Length)
            {
                room.PendingActions[order] = null;
                _log?.LogWarning("MJPID_ACTIONS skipped: invalid playerPos. roomId={RoomId} order={Order} playerPos={PlayerPos}", room.RoomId, order, playerPos);
                continue;
            }
            var player = room.Seats[playerPos];
            if (player == null || player.IsViewer || string.IsNullOrEmpty(player.ConnectionId))
            {
                if (player is { IsViewer: false } && room.Engine.Player[order].Mode != Engine.PlayerMode.None)
                {
                    if (await TryScheduleDisconnectedPlayerTimeoutAsync(room, ctx, order, "valid-actions-disconnected"))
                        return;
                }
                room.PendingActions[order] = null;
                _log?.LogDebug("MJPID_ACTIONS skipped: no active player connection. roomId={RoomId} order={Order} playerPos={PlayerPos} hasPlayer={HasPlayer} isViewer={IsViewer} hasConnection={HasConnection}",
                    room.RoomId,
                    order,
                    playerPos,
                    player != null,
                    player?.IsViewer ?? false,
                    !string.IsNullOrEmpty(player?.ConnectionId));
                continue;
            }

            var actions = room.Engine.GetValidActions(order);
            var actionItems = BuildActionItems(actions).ToArray();
            int actFlags = BuildActFlags(actions);
            if (actionItems.Length == 0 && actions.TapCandidates.Count == 0)
            {
                room.PendingActions[order] = null;
                _log?.LogDebug("MJPID_ACTIONS skipped: no valid actions. roomId={RoomId} order={Order} playerPos={PlayerPos} memberNo={MemberNo} playerMode={PlayerMode} actFlags={ActFlags}",
                    room.RoomId,
                    order,
                    playerPos,
                    player.MemberNo,
                    room.Engine.Player[order].Mode,
                    actFlags);
                continue;
            }

            var targetConnectionIds = ResolveLivePlayerConnectionIds(room, player);
            if (targetConnectionIds.Count == 0)
            {
                room.PendingActions[order] = null;
                _log?.LogWarning("MJPID_ACTIONS skipped: no live target connection. roomId={RoomId} order={Order} playerPos={PlayerPos} memberNo={MemberNo} seatConnectionId={SeatConnectionId}",
                    room.RoomId,
                    order,
                    playerPos,
                    player.MemberNo,
                    player.ConnectionId);
                continue;
            }

            _log?.LogDebug("MJPID_ACTIONS sending. roomId={RoomId} order={Order} playerPos={PlayerPos} memberNo={MemberNo} connectionId={ConnectionId} playerMode={PlayerMode} actFlags={ActFlags} actionCount={ActionCount} tapCount={TapCount}",
                room.RoomId,
                order,
                playerPos,
                player.MemberNo,
                string.Join(',', targetConnectionIds),
                room.Engine.Player[order].Mode,
                actFlags,
                actionItems.Length,
                actions.TapCandidates.Count);

            await IssueActionPromptAsync(room, ctx, order, actions, actionItems, actFlags, targetConnectionIds, "valid-actions");
        }
    }

    private async Task<bool> TryScheduleDisconnectedPlayerTimeoutAsync(GameRoom room, CommandContext ctx, int order, string reason)
    {
        if (order < 0 || order >= GameConst.PlayerMaxCount) return false;

        int playerPos = room.Engine.HanchanInfo.Player[order];
        if (playerPos < 0 || playerPos >= room.Seats.Length) return false;

        var player = room.Seats[playerPos];
        if (player == null || player.IsViewer) return false;
        if (!player.IsOutPlayer && !string.IsNullOrEmpty(player.ConnectionId)) return false;

        var mode = room.Engine.Player[order].Mode;
        if (mode == Engine.PlayerMode.None) return true;

        var currentPrompt = room.PendingActions[order];
        if (currentPrompt != null
            && currentPrompt.PlayerMode == mode
            && currentPrompt.DeadlineAt > DateTimeOffset.UtcNow)
        {
            _log?.LogDebug("Disconnected player keeps existing action deadline. roomId={RoomId} order={Order} playerPos={PlayerPos} memberNo={MemberNo} actionSeq={ActionSeq} deadlineAt={DeadlineAt:o} reason={Reason}",
                room.RoomId,
                order,
                playerPos,
                player.MemberNo,
                currentPrompt.ActionSeq,
                currentPrompt.DeadlineAt,
                reason);
            return true;
        }

        var actions = room.Engine.GetValidActions(order);
        if (ShouldAutoResolvePassOnlyResponse(room, order, actions)) return false;

        var actionItems = BuildActionItems(actions).ToArray();
        int actFlags = BuildActFlags(actions);
        if (actionItems.Length == 0 && actions.TapCandidates.Count == 0)
        {
            room.PendingActions[order] = null;
            _log?.LogInformation("MJPID_ACTIONS skipped disconnected player with no valid actions. roomId={RoomId} order={Order} playerPos={PlayerPos} memberNo={MemberNo} playerMode={PlayerMode} reason={Reason}",
                room.RoomId,
                order,
                playerPos,
                player.MemberNo,
                mode,
                reason);
            return true;
        }

        _log?.LogDebug("MJPID_ACTIONS scheduling timeout for disconnected player. roomId={RoomId} order={Order} playerPos={PlayerPos} memberNo={MemberNo} isOutPlayer={IsOutPlayer} hasConnection={HasConnection} playerMode={PlayerMode} reason={Reason}",
            room.RoomId,
            order,
            playerPos,
            player.MemberNo,
            player.IsOutPlayer,
            !string.IsNullOrEmpty(player.ConnectionId),
            mode,
            reason);
        await IssueActionPromptAsync(room, ctx, order, actions, actionItems, actFlags, Array.Empty<string>(), reason);
        return true;
    }

    private async Task IssueActionPromptAsync(
        GameRoom room,
        CommandContext ctx,
        int order,
        ValidActions actions,
        object[] actionItems,
        int actFlags,
        IReadOnlyList<string> targetConnectionIds,
        string reason)
    {
        int timeLimitSeconds = GetActionTimeLimitSeconds(room, actions);
        var issuedAt = DateTimeOffset.UtcNow;
        var prompt = new PendingActionPrompt
        {
            ActionSeq = room.IssueActionSeq(),
            SeatOrder = order,
            PlayerMode = room.Engine.Player[order].Mode,
            IssuedAt = issuedAt,
            DeadlineAt = issuedAt.AddSeconds(timeLimitSeconds),
        };
        room.PendingActions[order] = prompt;
        long serverNow = prompt.IssuedAt.ToUnixTimeMilliseconds();
        long deadlineAt = prompt.DeadlineAt.ToUnixTimeMilliseconds();

        _log?.LogDebug("MJPID_ACTIONS issuing prompt. roomId={RoomId} order={Order} connectionId={ConnectionId} playerMode={PlayerMode} actFlags={ActFlags} actionCount={ActionCount} tapCount={TapCount} actionSeq={ActionSeq} reason={Reason}",
            room.RoomId,
            order,
            string.Join(',', targetConnectionIds),
            room.Engine.Player[order].Mode,
            actFlags,
            actionItems.Length,
            actions.TapCandidates.Count,
            prompt.ActionSeq,
            reason);

        if (room.Engine.Player[order].Mode == Engine.PlayerMode.Turn)
        {
            await ctx.Clients.GroupExcept($"room_{room.RoomId}", targetConnectionIds)
                .SendAsync(Cmd.GamePlay, new
                {
                    playType = "MJPID_ACTIONS",
                    seatOrder = order,
                    playerMode = room.Engine.Player[order].Mode.ToString(),
                    actFlags = 0,
                    horaErrorReason = "",
                    actions = Array.Empty<object>(),
                    tapCandidates = Array.Empty<int>(),
                    timeLimit = timeLimitSeconds,
                    actionSeq = prompt.ActionSeq,
                    serverNow,
                    deadlineAt,
                });
        }

        if (targetConnectionIds.Count > 0)
        {
            await ctx.Clients.Clients(targetConnectionIds)
                .SendAsync(Cmd.GamePlay, new
                {
                    playType = "MJPID_ACTIONS",
                    seatOrder = order,
                    playerMode = room.Engine.Player[order].Mode.ToString(),
                    actFlags,
                    horaErrorReason = actions.HoraErrorReason,
                    actions = actionItems,
                    tapCandidates = actions.TapCandidates.ToArray(),
                    timeLimit = timeLimitSeconds,
                    actionSeq = prompt.ActionSeq,
                    serverNow,
                    deadlineAt,
                });
        }

        ScheduleActionTimeout(room, ctx, prompt);
    }

    private async Task ExecuteServerDefaultActionAsync(GameRoom room, CommandContext ctx, int order, Engine.Act act, int[] bipaiIdx, string reason)
    {
        long actionSeq = room.IssueActionSeq();
        var result = room.Engine.ProcessAction(order, act, bipaiIdx, bipaiIdx.Length);
        if (result != Engine.ActionResult.Ok)
        {
            _log?.LogWarning("Server default action rejected. roomId={RoomId} order={Order} act={Act} reason={Reason} result={Result}",
                room.RoomId,
                order,
                act,
                reason,
                result);
            return;
        }

        room.PendingActions[order] = null;
        var historyPaiInfo = await SendPaiInfoToAllAsync(room, ctx, isInit: false);
        var actionInfo = BuildActionInfo(room, order, (int)act, bipaiIdx, actionSeq);
        await ctx.Clients.Group($"room_{room.RoomId}").SendAsync(Cmd.GamePlay, actionInfo);
        if (historyPaiInfo != null) room.PlayHistory.Add(WrapHistoryPacket(Cmd.PaiInfoList, historyPaiInfo));
        room.PlayHistory.Add(actionInfo);

        switch (room.Engine.GameStatus)
        {
            case Engine.GameStatus.NewKyoku:
                await OnInitKyokuAsync(room, ctx);
                break;
            case Engine.GameStatus.EndKyoku:
                await OnEndKyokuAsync(room, ctx);
                break;
            case Engine.GameStatus.NotPlaying:
                room.ClearPendingActions();
                await OnEndGameAsync(room, ctx);
                break;
        }
    }

    private static bool ShouldAutoResolvePassOnlyResponse(GameRoom room, int order, ValidActions actions)
    {
        if (order < 0 || order >= GameConst.PlayerMaxCount) return false;
        var mode = room.Engine.Player[order].Mode;
        return mode is Engine.PlayerMode.Furo or Engine.PlayerMode.Chan
            && actions.CanPass
            && !HasNonPassAction(actions);
    }

    private bool ValidatePendingAction(GameRoom room, int order, long actionSeq, Engine.Act act, int[] bipaiIndex)
    {
        if (order < 0 || order >= GameConst.PlayerMaxCount) return false;
        var prompt = room.PendingActions[order];
        if (prompt == null) return actionSeq <= 0;
        if (actionSeq <= 0 || prompt.ActionSeq != actionSeq) return false;
        if (room.Engine.Player[order].Mode != prompt.PlayerMode) return false;
        if (DateTimeOffset.UtcNow >= prompt.DeadlineAt) return false;

        var actions = room.Engine.GetValidActions(order);
        return IsActionCurrentlyAllowed(actions, act, bipaiIndex);
    }

    private static bool IsActionCurrentlyAllowed(ValidActions actions, Engine.Act act, int[] bipaiIndex)
    {
        return act switch
        {
            Engine.Act.Pas => actions.CanPass,
            Engine.Act.Tsu => actions.CanTsumo,
            Engine.Act.Ron => actions.CanRon,
            Engine.Act.Tao => actions.CanTaopai,
            Engine.Act.Tap => bipaiIndex.Length == 1 && actions.TapCandidates.Contains(bipaiIndex[0]),
            Engine.Act.Ric => bipaiIndex.Length == 1 && actions.RichiCandidates.Contains(bipaiIndex[0]),
            Engine.Act.Ank => ContainsSameIndices(actions.AnkanCandidates, bipaiIndex),
            Engine.Act.Cha => bipaiIndex.Length == 1 && actions.ChakanCandidates.Contains(bipaiIndex[0]),
            Engine.Act.Hua => bipaiIndex.Length == 1 && actions.HuaCandidates.Contains(bipaiIndex[0]),
            Engine.Act.Kan => ContainsSameIndices(actions.KanCandidates, bipaiIndex),
            Engine.Act.Pon => ContainsSameIndices(actions.PonCandidates, bipaiIndex),
            Engine.Act.Chi => ContainsSameIndices(actions.ChiCandidates, bipaiIndex),
            _ => false,
        };
    }

    private static bool ContainsSameIndices(IEnumerable<int[]> candidates, int[] bipaiIndex)
        => candidates.Any(candidate => candidate.Length == bipaiIndex.Length && !candidate.Except(bipaiIndex).Any() && !bipaiIndex.Except(candidate).Any());

    public async Task SendCurrentActionPromptAsync(GameRoom room, CommandContext ctx, MajakPlayer player)
    {
        int order = player.EngineOrder;
        if (order < 0 || order >= GameConst.PlayerMaxCount)
        {
            await SendCurrentPublicTurnPromptAsync(room, ctx);
            return;
        }

        var prompt = room.PendingActions[order];
        if (prompt == null)
        {
            var currentActions = room.Engine.GetValidActions(order);
            var currentActionItems = BuildActionItems(currentActions).ToArray();
            if (currentActionItems.Length == 0 && currentActions.TapCandidates.Count == 0)
            {
                await SendCurrentPublicTurnPromptAsync(room, ctx);
                return;
            }

            var targetConnectionIds = ResolveLivePlayerConnectionIds(room, player);
            if (targetConnectionIds.Count == 0)
            {
                await SendCurrentPublicTurnPromptAsync(room, ctx);
                return;
            }

            await IssueActionPromptAsync(room, ctx, order, currentActions, currentActionItems, BuildActFlags(currentActions), targetConnectionIds, "current-prompt-reissue");
            return;
        }
        if (prompt.DeadlineAt <= DateTimeOffset.UtcNow)
        {
            await SendCurrentPublicTurnPromptAsync(room, ctx);
            return;
        }

        var actions = room.Engine.GetValidActions(order);
        var actionItems = BuildActionItems(actions).ToArray();
        if (actionItems.Length == 0 && actions.TapCandidates.Count == 0)
        {
            await SendCurrentPublicTurnPromptAsync(room, ctx);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        await ctx.Caller.SendAsync(Cmd.GamePlay, new
        {
            playType = "MJPID_ACTIONS",
            seatOrder = order,
            playerMode = room.Engine.Player[order].Mode.ToString(),
            actFlags = BuildActFlags(actions),
            actions = actionItems,
            tapCandidates = actions.TapCandidates.ToArray(),
            timeLimit = Math.Max(0, (int)Math.Ceiling((prompt.DeadlineAt - now).TotalSeconds)),
            actionSeq = prompt.ActionSeq,
            serverNow = now.ToUnixTimeMilliseconds(),
            deadlineAt = prompt.DeadlineAt.ToUnixTimeMilliseconds(),
        });
    }

    public virtual async Task SendGameResyncAsync(GameRoom room, CommandContext ctx, MajakPlayer player, bool includePrompt = true)
    {
        await SendPaiInfoAsync(room, ctx, player, isInit: true, includeAll: true);
        if (room.PlayHistory.Count > 0)
        {
            await ctx.Caller.SendAsync(Cmd.History, new
            {
                roomId = room.RoomId,
                historyCount = room.PlayHistory.Count,
                history = room.PlayHistory,
            });
        }
        if (room.LastGameReportPayload != null && room.State != GameRoomState.Playing)
        {
            await ctx.Caller.SendAsync(Cmd.GameReport, room.LastGameReportPayload);
            return;
        }
        if (includePrompt)
            await SendCurrentActionPromptAsync(room, ctx, player);
    }

    private static async Task SendCurrentPublicTurnPromptAsync(GameRoom room, CommandContext ctx)
    {
        var now = DateTimeOffset.UtcNow;
        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            var prompt = room.PendingActions[order];
            if (prompt == null || prompt.DeadlineAt <= now) continue;
            if (prompt.PlayerMode != Engine.PlayerMode.Turn) continue;
            if (room.Engine.Player[order].Mode != Engine.PlayerMode.Turn) continue;

            await ctx.Caller.SendAsync(Cmd.GamePlay, new
            {
                playType = "MJPID_ACTIONS",
                seatOrder = order,
                playerMode = Engine.PlayerMode.Turn.ToString(),
                actFlags = 0,
                horaErrorReason = "",
                actions = Array.Empty<object>(),
                tapCandidates = Array.Empty<int>(),
                timeLimit = Math.Max(0, (int)Math.Ceiling((prompt.DeadlineAt - now).TotalSeconds)),
                actionSeq = prompt.ActionSeq,
                serverNow = now.ToUnixTimeMilliseconds(),
                deadlineAt = prompt.DeadlineAt.ToUnixTimeMilliseconds(),
            });
            return;
        }
    }

    // AP-14 deadline fallback for an issued client prompt. This is deliberately
    // separate from legacy ProxyPlayAsync, which is reserved for disconnected players.
    private void ScheduleActionTimeout(GameRoom room, CommandContext ctx, PendingActionPrompt prompt)
    {
        int delayMs = Math.Max(1, (int)Math.Ceiling((prompt.DeadlineAt - DateTimeOffset.UtcNow).TotalMilliseconds));
        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            await room.EngineLock.WaitAsync();
            try
            {
                int order = prompt.SeatOrder;
                if (order < 0 || order >= GameConst.PlayerMaxCount) return;
                if (PauseAutoProgressWhenNoActivePlayers(room, "action timeout")) return;
                var currentPrompt = room.PendingActions[order];
                if (currentPrompt == null || currentPrompt.ActionSeq != prompt.ActionSeq) return;
                if (room.Engine.Player[order].Mode != prompt.PlayerMode) return;

                var actions = room.Engine.GetValidActions(order);
                if (!TryBuildTimeoutAction(room, order, out var timeoutAct, out var bipaiIdx)) return;
                if (!IsActionCurrentlyAllowed(actions, timeoutAct, bipaiIdx)) return;

                _log?.LogDebug("Action timeout default executing. roomId={RoomId} order={Order} mode={Mode} actionSeq={ActionSeq} delayMs={DelayMs}",
                    room.RoomId, order, prompt.PlayerMode, prompt.ActionSeq, delayMs);

                var result = room.Engine.ProcessAction(order, timeoutAct, bipaiIdx, bipaiIdx.Length);
                if (result != Engine.ActionResult.Ok) return;
                room.PendingActions[order] = null;

                var historyPaiInfo = await SendPaiInfoToAllAsync(room, ctx, isInit: false);
                var actionInfo = BuildActionInfo(room, order, (int)timeoutAct, bipaiIdx, prompt.ActionSeq);
                await ctx.Clients.Group($"room_{room.RoomId}").SendAsync(Cmd.GamePlay, actionInfo);
                if (historyPaiInfo != null) room.PlayHistory.Add(WrapHistoryPacket(Cmd.PaiInfoList, historyPaiInfo));
                room.PlayHistory.Add(actionInfo);

                switch (room.Engine.GameStatus)
                {
                    case Engine.GameStatus.NewKyoku:
                        await OnInitKyokuAsync(room, ctx);
                        break;
                    case Engine.GameStatus.EndKyoku:
                        await OnEndKyokuAsync(room, ctx);
                        break;
                    case Engine.GameStatus.NotPlaying:
                        room.ClearPendingActions();
                        await OnEndGameAsync(room, ctx);
                        return;
                }

                if (room.Engine.GameStatus != Engine.GameStatus.NotPlaying)
                {
                    await SendValidActionsToPlayersAsync(room, ctx);
                    await ProxyEmptySeatsAsync(room, ctx);
                }
            }
            finally
            {
                room.EngineLock.Release();
            }
        });
    }

    private static bool TryBuildTimeoutAction(GameRoom room, int order, out Engine.Act act, out int[] bipaiIdx)
    {
        bipaiIdx = Array.Empty<int>();
        switch (room.Engine.Player[order].Mode)
        {
            case Engine.PlayerMode.Turn:
                act = Engine.Act.Tap;
                if (room.Engine.Player[order].Tehai.Count == 0) return false;
                bipaiIdx = new[] { room.Engine.Player[order].Tehai.Last().BipaiIndex };
                room.Engine.Player[order].ResultRecord.DaidaCnt++;
                return true;
            case Engine.PlayerMode.Furo:
            case Engine.PlayerMode.Chan:
            case Engine.PlayerMode.Kyo:
            case Engine.PlayerMode.Aga:
                act = Engine.Act.Pas;
                return true;
            default:
                act = Engine.Act.Inv;
                return false;
        }
    }

    private List<string> ResolveLivePlayerConnectionIds(GameRoom room, MajakPlayer player)
    {
        var connectionIds = new List<string>();
        void Add(string connectionId)
        {
            if (!string.IsNullOrWhiteSpace(connectionId) && !connectionIds.Contains(connectionId))
                connectionIds.Add(connectionId);
        }

        Add(player.ConnectionId);

        var livePlayer = _session.GetByMember(player.MemberNo);
        if (livePlayer != null)
        {
            Add(livePlayer.ConnectionId);
            if (livePlayer.ConnectionId != player.ConnectionId)
                room.RefreshPlayerConnection(livePlayer);
        }

        return connectionIds;
    }

    private int GetActionTimeLimitSeconds(GameRoom room, ValidActions actions)
    {
        var speed = GetLegacySpeed(room);
        bool hasTap = actions.TapCandidates.Count > 0;
        bool hasCallOrWin = HasNonPassAction(actions) && !hasTap;

        if (actions.CanPass && !hasTap && !hasCallOrWin)
            return ApplyTestTimeScale(Math.Max(1, (int)Math.Ceiling(speed.Keep / 1000.0)));

        if (actions.CanPass && !hasTap && !actions.CanRon)
            return ApplyTestTimeScale(Math.Max(1, (int)Math.Ceiling(speed.Furo / 1000.0)));

        return ApplyTestTimeScale(Math.Max(1, (int)Math.Ceiling(speed.Full / 1000.0)));
    }

    private static bool HasNonPassAction(ValidActions actions)
        => actions.TapCandidates.Count > 0
            || actions.CanRon
            || actions.CanTsumo
            || actions.CanTaopai
            || actions.RichiCandidates.Count > 0
            || actions.AnkanCandidates.Count > 0
            || actions.ChakanCandidates.Count > 0
            || actions.HuaCandidates.Count > 0
            || actions.KanCandidates.Count > 0
            || actions.PonCandidates.Count > 0
            || actions.ChiCandidates.Count > 0;

    private int ApplyTestTimeScale(int seconds)
    {
        if (!_testEnvironment) return seconds;
        return Math.Max(1, (int)Math.Ceiling(seconds / 2.0));
    }

    private static (int Full, int Init, int Turn, int Furo, int Keep, int KyoRes) GetLegacySpeed(GameRoom room)
    {
        return GetLegacySpeed(GetLegacySpeedNo(room));
    }

    private static int GetLegacySpeedNo(GameRoom room)
    {
        int speedNo = room.RoomOption.Length > 2 && char.IsDigit(room.RoomOption[2])
            ? room.RoomOption[2] - '0'
            : 2;
        return Math.Clamp(speedNo, 0, 3);
    }

    private static (int Full, int Init, int Turn, int Furo, int Keep, int KyoRes) GetLegacySpeed(int speedNo)
    {
        return speedNo switch
        {
            0 => (7500, 20000, 2000, 2000, 100, 8000),
            1 => (9000, 25000, 2500, 2000, 500, 10000),
            2 => (10000, 30000, 3000, 2500, 1000, 15000),
            _ => (15000, 45000, 4500, 3500, 1200, 20000),
        };
    }

    private static IEnumerable<object> BuildActionItems(ValidActions actions)
    {
        if (actions.CanPass)   yield return ActionItem("Pass",  Engine.Act.Pas, Array.Empty<int>());
        if (actions.CanTsumo)  yield return ActionItem("Tsumo", Engine.Act.Tsu, Array.Empty<int>());
        if (actions.CanRon)    yield return ActionItem("Ron",   Engine.Act.Ron, Array.Empty<int>());
        if (actions.CanTaopai) yield return ActionItem("Pass",  Engine.Act.Tao, Array.Empty<int>());

        foreach (var index in actions.RichiCandidates)
            yield return ActionItem("Reach", Engine.Act.Ric, new[] { index });
        foreach (var indices in actions.AnkanCandidates)
            yield return ActionItem("Kan", Engine.Act.Ank, indices);
        foreach (var index in actions.ChakanCandidates)
            yield return ActionItem("Kan", Engine.Act.Cha, new[] { index });
        foreach (var index in actions.HuaCandidates)
            yield return ActionItem("Hua", Engine.Act.Hua, new[] { index });
        foreach (var indices in actions.KanCandidates)
            yield return ActionItem("Kan", Engine.Act.Kan, indices);
        foreach (var indices in actions.PonCandidates)
            yield return ActionItem("Pon", Engine.Act.Pon, indices);
        foreach (var indices in actions.ChiCandidates)
            yield return ActionItem("Chi", Engine.Act.Chi, indices);
    }

    private static int BuildActFlags(ValidActions actions)
    {
        int flags = 0;
        if (actions.CanPass)   flags |= 1 << (int)Engine.Act.Pas;
        if (actions.CanTsumo)  flags |= 1 << (int)Engine.Act.Tsu;
        if (actions.CanRon)    flags |= 1 << (int)Engine.Act.Ron;
        if (actions.CanTaopai) flags |= 1 << (int)Engine.Act.Tao;
        if (actions.TapCandidates.Count > 0)     flags |= 1 << (int)Engine.Act.Tap;
        if (actions.RichiCandidates.Count > 0)   flags |= 1 << (int)Engine.Act.Ric;
        if (actions.AnkanCandidates.Count > 0)   flags |= 1 << (int)Engine.Act.Ank;
        if (actions.ChakanCandidates.Count > 0)  flags |= 1 << (int)Engine.Act.Cha;
        if (actions.HuaCandidates.Count > 0)     flags |= 1 << (int)Engine.Act.Hua;
        if (actions.KanCandidates.Count > 0)     flags |= 1 << (int)Engine.Act.Kan;
        if (actions.PonCandidates.Count > 0)     flags |= 1 << (int)Engine.Act.Pon;
        if (actions.ChiCandidates.Count > 0)     flags |= 1 << (int)Engine.Act.Chi;
        return flags;
    }

    private static object ActionItem(string act, Engine.Act code, int[] bipaiIndex)
        => new { act, code = (int)code, bipaiIndex };

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>


    ///   ClearPlayHist ↁEAddPlayHist_HanchanInfo
    ///   ↁEAddToParser_KyokuInfo + SendDataToAll(clCmdInitKyoku)
    /// </summary>
    public async Task OnInitKyokuAsync(GameRoom room, CommandContext ctx)
    {
        _log?.LogInformation("OnInitKyoku begin. roomId={RoomId} curKyoku={CurKyoku} oyaOrder={OyaOrder} leftCount={LeftCount}",
            room.RoomId,
            room.Engine.HanchanInfo.CurKyoku,
            room.Engine.KyokuInfo.OyaOrder,
            room.Engine.GetBipaiCount());


        room.PlayHistory.Clear();


        room.PlayHistory.Add(BuildHanchanInfo(room));


        var historyPaiInfo = await SendPaiInfoToAllAsync(room, ctx, isInit: true);
        if (historyPaiInfo != null) room.PlayHistory.Add(WrapHistoryPacket(Cmd.PaiInfoList, historyPaiInfo));


        var ki = room.Engine.KyokuInfo;
        int oyaOrder = ki.OyaOrder;
        int waremeOdr = room.Engine.Rule.Wareme && ki.Dice.Length >= 2
            ? (oyaOrder + ki.Dice[0] + ki.Dice[1] + 1) % GameConst.PlayerMaxCount
            : -1;
        var kyokuInfo = new
        {
            playType    = "MJPID_INIKYO",
            kyokuCnt    = room.Engine.HanchanInfo.CurKyoku,
            oyaOrder,
            waremeOdr,
            riboCnt     = ki.RibouCount,
            renChanCnt  = room.Engine.HanchanInfo.RenchanCount,
            dice        = ki.Dice,
            leftCount   = room.Engine.GetBipaiCount(),
            memberPoints = room.Engine.Player.Select(p => p.GamePoint).ToArray(),
            yakitori     = room.Engine.Player.Select(p => p.IsYakitori).ToArray(),
            tip          = room.Engine.Player.Select(p => p.Tip).ToArray(),
        };
        room.PlayHistory.Add(kyokuInfo);
        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.GamePlay, kyokuInfo);
        _log?.LogInformation("OnInitKyoku sent MJPID_INIKYO. roomId={RoomId} oyaOrder={OyaOrder} waremeOdr={WaremeOdr} dice={Dice} memberPoints={MemberPoints}",
            room.RoomId,
            oyaOrder,
            waremeOdr,
            string.Join(',', ki.Dice),
            string.Join(',', room.Engine.Player.Select(p => p.GamePoint)));
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>




    /// </summary>
    public async Task OnEndKyokuAsync(GameRoom room, CommandContext ctx)
    {
        if (room.Engine.KyokuEnd == KyokuEnd.Hora)
            await ProcessHoraEndKyokuAsync(room, ctx);

        ApplyCupPointOnEndKyoku(room);
        UpdateEndKyokuTitleCounters(room);


        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.GamePlay, BuildKyoResultPayload(room));
    }

    private async Task ProcessHoraEndKyokuAsync(GameRoom room, CommandContext ctx)
    {
        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            if (!room.Engine.LastKyoResult.Hora[order]) continue;

            var player = FindSeatByEngineOrder(room, order);
            if (player == null) continue;

            var yaku = room.Engine.Player[order].Yaku;
            if (!yaku.IsYakuman)
            {
                if (!room.IsTrainingChannel
                    && HasYaku(yaku, HoraYaku.Ippatsu)
                    && HasYaku(yaku, HoraYaku.Tsumo))
                {
                    await SendAvatarGearAsync(room, ctx, player, avatarGearType: 2);
                }

                int horaDoraCnt = yaku.DoraCnt.Sum();
                if (horaDoraCnt > player.HoraDoraMax)
                    player.HoraDoraMax = horaDoraCnt;

                continue;
            }

            var yakumanNames = new List<string>();
            foreach (var info in yaku.List)
            {
                int yakuCode = (int)info.Name;
                if (yakuCode is < 100 or > 114) continue;

                if (!room.IsTrainingChannel)
                    await _historyRepo.InsertYakuHistAsync(player.MemberNo, GameConst.ServiceId, yakuCode);

                yakumanNames.Add(GetYakumanName(info.Name));
            }

            int yakumanBonusRate = GetYakumanBonusRate(room, _testEnvironment);
            if (yakumanBonusRate > 0 && Random.Shared.Next(100) < yakumanBonusRate)
                await SendYakumanBonusAsync(room, player, string.Join("・", yakumanNames), ctx);

            if (!room.IsTrainingChannel)
            {
                await SendAvatarGearAsync(room, ctx, player, avatarGearType: 1);
            }
        }
    }

    private static int GetYakumanBonusRate(GameRoom room, bool testEnvironment)
    {
        bool isLegacyTrainingChannel = room.SubId.Length > 2 && room.SubId[2] == 'T';
        if (room.IsBeginnerChannel || isLegacyTrainingChannel) return 0;
        if (testEnvironment) return 50;
        return room.IsAutoMatchChannel || room.IsTournamentChannel ? 3 : 10;
    }

    private static void ApplyCupPointOnEndKyoku(GameRoom room)
    {
        if (!room.IsCupChannel || room.Engine.KyokuEnd != KyokuEnd.Hora) return;

        var cupPoint = new int[GameConst.PlayerMaxCount];
        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            if (!room.Engine.LastKyoResult.Hora[order]) continue;

            var player = room.Engine.Player[order];
            var yaku = player.Yaku;
            switch (room.CupJudgementType)
            {
                case 0: // CUP_JTID_YAKU_ANY
                    cupPoint[order] += CountCupYakuAny(yaku, room.CupNormalYakuCondition, room.CupYakumanCondition);
                    break;
                case 1: // CUP_JTID_YAKU_FAN
                    cupPoint[order] += yaku.YakuhaiCnt.Sum();
                    break;
                case 2: // CUP_JTID_DORA_ALL
                    cupPoint[order] += yaku.DoraCnt.Sum();
                    break;
                case 3: // CUP_JTID_HORA_CNT
                    cupPoint[order]++;
                    break;
                case 4: // CUP_JTID_HORA_DIF
                    cupPoint[order]++;
                    if (player.CurAct == Engine.Act.Ron && room.Engine.LastKyoResult.HojuOrder >= 0)
                        cupPoint[room.Engine.LastKyoResult.HojuOrder]--;
                    break;
                case 5: // CUP_JTID_HORA_HAN
                    cupPoint[order] += yaku.IsYakuman ? yaku.HanSum * 13 : yaku.HanSum;
                    break;
                case 6: // CUP_JTID_HORA_TEN
                    cupPoint[order] += player.KyokuPoint;
                    break;
            }
        }

        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            var seat = FindSeatByEngineOrder(room, order);
            if (seat != null)
            {
                seat.CupRec.CupPoint += cupPoint[order];
                seat.CupPointGain += cupPoint[order];
            }
        }
    }

    private static int CountCupYakuAny(Yaku yaku, string normalMask, string yakumanMask)
    {
        int cupPoint = 0;
        foreach (var info in yaku.List)
        {
            int index = yaku.IsYakuman ? (int)info.Name - 100 : (int)info.Name;
            string mask = yaku.IsYakuman ? yakumanMask : normalMask;
            if (index >= 0 && index < mask.Length && mask[index] != '0')
                cupPoint++;
        }
        return cupPoint;
    }

    private static void UpdateEndKyokuTitleCounters(GameRoom room)
    {
        if (room.IsTrainingChannel || room.Engine.KyokuEnd != KyokuEnd.Hora || !room.IsHiClassChannel) return;

        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            var player = FindSeatByEngineOrder(room, order);
            if (!room.Engine.LastKyoResult.Hora[order] || player == null) continue;

            var yaku = room.Engine.Player[order].Yaku;
            if (!yaku.IsYakuman)
            {
                foreach (var info in yaku.List)
                {
                    int code = (int)info.Name;
                    if (code >= 0 && code < player.YakuCount.Length)
                        player.YakuCount[code]++;
                }
            }
            else
            {
                foreach (var info in yaku.List)
                {
                    int code = (int)info.Name - 100;
                    if (code >= 0 && code < player.YmanCount.Length)
                        player.YmanCount[code]++;
                }
            }

            if (player.TitleClear[22] == 0 && yaku.Mangan == 5 && yaku.DoraCnt.Sum() >= 12)
                player.TitleClear[22] = 2;
            if (player.TitleClear[25] == 0 && yaku.IsYakuman && yaku.List.Count >= 2)
                player.TitleClear[25] = 2;
        }
    }

    private static bool HasYaku(Yaku yaku, HoraYaku name)
        => yaku.List.Any(info => info.Name == name);

    private static MajakPlayer? FindSeatByEngineOrder(GameRoom room, int order)
    {
        int playerPos = order >= 0 && order < room.Engine.HanchanInfo.Player.Length
            ? room.Engine.HanchanInfo.Player[order]
            : -1;
        return playerPos >= 0 && playerPos < room.Seats.Length ? room.Seats[playerPos] : null;
    }

    private static string GetYakumanName(HoraYaku yaku) => yaku switch
    {
        HoraYaku.Daisangen     => "大三元",
        HoraYaku.Suuankou      => "四暗刻",
        HoraYaku.Suukantsu     => "四槓子",
        HoraYaku.Shosuushi     => "小四喜",
        HoraYaku.Chinroutou    => "清老頭",
        HoraYaku.Tsuisou       => "字一色",
        HoraYaku.Ryuisou       => "緑一色",
        HoraYaku.Churenpaotou  => "九連宝燈",
        HoraYaku.Kokushi       => "国士無双",
        HoraYaku.Tenhou        => "天和",
        HoraYaku.Chihou        => "地和",
        HoraYaku.Suuankou2     => "四暗刻単騎",
        HoraYaku.Daisuushi     => "大四喜",
        HoraYaku.Kokushi2      => "国士無双１３門待ち",
        HoraYaku.Churenpaotou2 => "純正九連宝燈",
        _ => yaku.ToString(),
    };

    private static object BuildKyoResultPayload(GameRoom room)
    {
        var engine = room.Engine;
        var result = engine.LastKyoResult;
        var yakuByPlayer = new Dictionary<int, object[]>();
        var totalsByPlayer = new Dictionary<int, object>();

        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
        {
            if (!result.Hora[i]) continue;
            var yaku = engine.Player[i].Yaku;
            yakuByPlayer[i] = yaku.List.Select(y => new
            {
                name = y.Name.ToString(),
                fan = y.Han,
                code = yaku.IsYakuman ? (int)y.Name - 100 : (int)y.Name,
                isYakuman = yaku.IsYakuman,
                tip = 0,
            }).Cast<object>().ToArray();
            totalsByPlayer[i] = new
            {
                totalFu = yaku.Fu,
                totalFan = yaku.HanSum,
                totalTen = yaku.Ten,
                tipBal = result.TipBal[i],
            };
        }

        int firstWinner = Enumerable.Range(0, GameConst.PlayerMaxCount).FirstOrDefault(i => result.Hora[i]);
        int selectedOdr = SelectKyoResultOrder(result, firstWinner);
        var firstYaku = engine.Player[firstWinner].Yaku;
        int doraCount = GetEndKyokuDoraCount(engine);
        int waremeOdr = engine.Rule.Wareme
            ? (engine.KyokuInfo.OyaOrder + engine.KyokuInfo.Dice[0] + engine.KyokuInfo.Dice[1] + 1) % GameConst.PlayerMaxCount
            : -1;
        int ribCnt = result.Pin is KyoResultPin.Ron or KyoResultPin.Tsumo
            ? Math.Max(0, result.RibBal.Max() / 1000)
            : engine.KyokuInfo.RibouCount;

        return new
        {
            playType = "MJPID_ENDKYO",
            kyokuEnd = (int)engine.KyokuEnd,
            pinType = (int)result.Pin,
            selectedOdr,
            contest = engine.Rule.Contest,
            waremeOdr,
            kyoNum = engine.HanchanInfo.CurKyoku,
            ribCnt,
            renCnt = engine.HanchanInfo.RenchanCount,
            dora = engine.KyokuInfo.Dora.Take(doraCount).Where(p => p.IsValid).Select(p => p.GetNextNumberPai().Code).ToArray(),
            uraDora = engine.KyokuInfo.UraDora.Take(doraCount).Where(p => p.IsValid).Select(p => p.GetNextNumberPai().Code).ToArray(),
            totalFu = firstYaku.Fu,
            totalFan = firstYaku.HanSum,
            totalTen = firstYaku.Ten,
            tipBal = result.TipBal[firstWinner],
            yakuByPlayer,
            totalsByPlayer,
            players = Enumerable.Range(0, GameConst.PlayerMaxCount).Select(i =>
            {
                var seat = FindSeatByEngineOrder(room, i);
                int playerPos = engine.HanchanInfo.Player[i];
                var npc = seat == null && room.IsTrainingChannel
                    ? room.TrainingNpcProfiles[playerPos]
                    : null;
                var player = engine.Player[i];
                int totalBal = result.TenBal[i] + result.PaoBal[i] + result.WarBal[i]
                    + result.RibBal[i] + result.RenBal[i];
                return new
                {
                    memberNo = seat?.Pix ?? string.Empty,
                    pix = seat?.Pix ?? string.Empty,
                    name = seat?.NickName ?? npc?.Name ?? string.Empty,
                    avatarId = seat?.AvatarId ?? npc?.AvatarId ?? string.Empty,
                    sex = seat?.Sex ?? npc?.Sex ?? string.Empty,
                    seatPos = i,
                    isOya = i == engine.KyokuInfo.OyaOrder,
                    point = player.GamePoint,
                    score = player.GamePoint,
                    tip = player.Tip,
                    yakitori = player.IsYakitori,
                    tenBal = totalBal,
                    tenBaseBal = result.TenBal[i],
                    paoBal = result.PaoBal[i],
                    warBal = result.WarBal[i],
                    ribBal = result.RibBal[i],
                    renBal = result.RenBal[i],
                    tipBal = result.TipBal[i],
                    isHora = result.Hora[i],
                    isHoju = i == result.HojuOrder,
                    isNagashiMangan = player.IsNagashiMangan,
                    isTempai = player.IsTempai,
                    isRichi = player.RichiType != RichiType.None,
                };
            }).ToArray(),
        };
    }

    private static int GetEndKyokuDoraCount(MajakGameLogic engine)
    {
        int count = engine.Rule.Contest == 1 ? 1 : engine.KyokuInfo.KanCount + 1;
        return Math.Clamp(count, 0, engine.KyokuInfo.Dora.Length);
    }

    private static int SelectKyoResultOrder(KyoResultSnapshot result, int firstWinner)
    {
        if (result.Pin == KyoResultPin.Ron && result.HojuOrder >= 0)
        {
            for (int i = 1; i < GameConst.PlayerMaxCount; i++)
            {
                int order = (result.HojuOrder + i) % GameConst.PlayerMaxCount;
                if (result.Hora[order]) return order;
            }
        }

        return firstWinner;
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>




    /// </summary>
    public async Task OnEndGameAsync(GameRoom room, CommandContext ctx)
    {

        if (room.IsCupChannel)
            ApplyCupPointOnEndGame(room);

        UpdateEndGameTitleCounters(room);



        if (room.IsCupChannel && room.CupJudgementType == 8 /* CUP_JTID_GAME_SUM */)
        {
            for (int i = 0; i < GameConst.PlayerMaxCount; i++)
            {
                var seat = room.Seats[i];
                if (seat == null || string.IsNullOrEmpty(seat.ConnectionId)) continue;

                int engineOrder = room.SeatToEngineOrder[i];

                int setTotal = room.Engine.Player[engineOrder].SetTotal;


                CalcCupEvtScore(seat.CupEvtRec, setTotal, room.CupPointSumType);


                await _playerRepo.UpdateCupEvtRatAsync(seat, room.CupId, room.CupSeq);


                //        pak.AddVal(info.m_nTotalPoint)
                //        pak.AddVal(info.m_nMatchCnt)
                //        pak.AddVal(info.m_nPoint, 7)
                //        pSocket->SendPacket(clSendEventPoint);
                var r = seat.CupEvtRec;
                await ctx.Clients.Client(seat.ConnectionId)
                    .SendAsync(Cmd.EventInfo, new
                    {
                        totalPoint = r.TotalPoint,
                        matchCnt   = r.MatchCnt,
                        points     = r.Points,   // int[7]
                    });
            }
        }


        room.State = GameRoomState.Finished;
        await GameReportProcessAsync(room, ctx);
    }

    private static void ApplyCupPointOnEndGame(GameRoom room)
    {
        if (room.CupJudgementType != 7 || room.Engine.GameEnd != Engine.GameEnd.Tobi) return; // CUP_JTID_KILL_DIF

        var cupPoint = new int[GameConst.PlayerMaxCount];
        if (room.Engine.KyokuEnd == KyokuEnd.Hora)
        {
            for (int order = 0; order < GameConst.PlayerMaxCount; order++)
            {
                var player = room.Engine.Player[order];
                switch (player.CurAct)
                {
                    case Engine.Act.Tsu:
                        for (int idx = 0; idx < GameConst.PlayerMaxCount; idx++)
                            if (room.Engine.Player[idx].GamePoint < 0)
                                cupPoint[order]++;
                        break;
                    case Engine.Act.Ron:
                        int hoju = room.Engine.LastKyoResult.HojuOrder;
                        if (hoju >= 0 && room.Engine.Player[hoju].GamePoint < 0)
                            cupPoint[order]++;
                        if (player.IsPao)
                        {
                            int pao = player.PaoOrder;
                            if (pao != hoju && pao >= 0 && room.Engine.Player[pao].GamePoint < 0)
                                cupPoint[order]++;
                        }
                        break;
                }
            }
        }
        else if (room.Engine.KyokuEnd == KyokuEnd.Nagashimangan)
        {
            for (int order = 0; order < GameConst.PlayerMaxCount; order++)
            {
                if (!room.Engine.Player[order].IsNagashiMangan) continue;
                for (int idx = 0; idx < GameConst.PlayerMaxCount; idx++)
                    if (room.Engine.Player[idx].GamePoint < 0)
                        cupPoint[order]++;
            }
        }

        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
            if (room.Engine.Player[order].GamePoint < 0)
                cupPoint[order]--;

        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            var seat = FindSeatByEngineOrder(room, order);
            if (seat != null)
            {
                seat.CupRec.CupPoint += cupPoint[order];
                seat.CupPointGain += cupPoint[order];
            }
        }
    }

    private static void UpdateEndGameTitleCounters(GameRoom room)
    {
        if (room.IsTrainingChannel) return;

        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            var seat = FindSeatByEngineOrder(room, order);
            if (seat == null) continue;

            var player = room.Engine.Player[order];
            if (seat.TitleClear[23] == 0 && player.SetRank == 0 && player.GamePoint >= 100000)
                seat.TitleClear[23] = 2;

            if (seat.TitleClear[24] == 0 && room.Engine.GameEnd == Engine.GameEnd.Tobi)
            {
                bool allOtherPlayersTobi = true;
                for (int idx = 0; idx < GameConst.PlayerMaxCount; idx++)
                {
                    if (idx != order && room.Engine.Player[idx].GamePoint >= 0)
                    {
                        allOtherPlayersTobi = false;
                        break;
                    }
                }

                if (allOtherPlayersTobi)
                    seat.TitleClear[24] = 2;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>


    ///   MakeGameReport ↁEGameReport
    ///     ↁECalcMoney_GambleType ↁECalcExperience_MajakType
    ///     ↁECalcRating_MajakType (or GradeMode)
    ///     ↁEUpdateResult_GambleType
    ///     ↁEInsertMajak2Hist (MySQL)
    ///     ↁESendDataToAll(clGameReportResponse)
    /// </summary>
    public async Task GameReportProcessAsync(GameRoom room, CommandContext ctx)
    {
        if (!room.TryBeginGameReportProcess())
        {
            _log?.LogWarning("GameReportProcess skipped duplicate invocation. roomId={RoomId} state={State}", room.RoomId, room.State);
            return;
        }

        try
        {
            _log?.LogInformation("GameReportProcess begin. roomId={RoomId} state={State}", room.RoomId, room.State);
            await GameReportProcessCoreAsync(room, ctx);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "GameReportProcess failed. roomId={RoomId} state={State}", room.RoomId, room.State);
            var failurePayload = BuildGameReportFailurePayload("exception");
            room.LastGameReportPayload = failurePayload;
            await ctx.Clients.Group($"room_{room.RoomId}")
                .SendAsync(Cmd.GameReport, failurePayload);
            await ctx.Clients.Group($"chanel_{room.ChannelId}")
                .SendAsync(Cmd.GameReport, failurePayload);
            await FinishGameReportRoomAsync(room, ctx);
        }
    }

    private async Task GameReportProcessCoreAsync(GameRoom room, CommandContext ctx)
    {

        var report = await MakeGameReportAsync(room, ctx);
        if (report == null)
        {
            room.ResetGameReportProcess();
            return;
        }


        CalcMoney(room, report);



        if (!ValidateMoneyReport(report, room))
        {
            _log?.LogWarning("GameReportProcess money validation failed. roomId={RoomId} unitMoney={UnitMoney} moneyChanges={MoneyChanges}",
                room.RoomId,
                room.UnitMoney,
                string.Join(',', report.Users.Where(u => u != null).Select(u => $"{u!.MemberNo}:{u.MoneyChange}")));
            var failurePayload = BuildGameReportFailurePayload("money_validation_failed");
            room.LastGameReportPayload = failurePayload;
            await ctx.Clients.Group($"room_{room.RoomId}")
                .SendAsync(Cmd.GameReport, failurePayload);
            await ctx.Clients.Group($"chanel_{room.ChannelId}")
                .SendAsync(Cmd.GameReport, failurePayload);
            await FinishGameReportRoomAsync(room, ctx);
            return;
        }


        if (room.IsGradeChannel)
            CalcGradeModeLeveUp(report, room);


        CalcExperience(report);


        CalcRating(report, room);


        // ここで UserResult.GemCount に獲得量を保持し、結果ペイロードでは最終保有数を送る、E
        await SendGetGemAsync(room, report, ctx);

        // プレイヤー状態を更新してメモリに反映
        foreach (var u in report.Users.Where(u => u != null))
        {
            var p = _session.GetByMember(u!.MemberNo);
            if (p == null) continue;
            var resultUpdate = BuildResultUpdatePlayer(p, u, room);

            p.GamMoney   += u.MoneyChange;
            p.Experience += u.ExperienceGain;
            p.ActiveRecord.Rating    = u.Rating;
            AddResultRecordDelta(p.ActiveRecord, u);
            if (room.IsHiClassChannel)
                UpdateHiClassStreaks(p, u);
            _ratingService.UpdatePlayerLevel(p);


            if (room.IsGradeChannel)
            {
                p.GradeRecord.Grade      = u.GradeLevel;
                p.GradeRecord.GradePoint = u.GradePoint;
            }

            if (!room.IsTrainingChannel)
            {


                //   IsGradeMode() ↁEMJKHANGERAT (GradeRecord)
                //   IsCompete()   ↁEMJKCOMPETERAT (CompeteRecord)
                //   IsHiClass()   ↁEMJK_HICLASSRAT (HiClassRecord)
                //   IsRegular()   ↁEMJKCOMMONRAT (RegularRecord / ActiveRecord)
                await _playerRepo.UpdateResultCommonRatAsync(p, !u.IsConnect, u.MoneyChange, u.ExperienceGain, u.GemCount, u.Ranking);
                if (room.IsGradeChannel)
                    await _playerRepo.UpdateGradeRatAsync(resultUpdate);
                else if (room.IsCompeteChannel)
                    await _playerRepo.UpdateCompeteRatAsync(resultUpdate);
                else if (room.IsHiClassChannel)
                    await _playerRepo.UpdateHiClassRatAsync(resultUpdate, u.Score, u.MoneyChange);
                else
                    await _playerRepo.UpdateRegularRatAsync(resultUpdate);

                if (room.IsCupChannel)
                {
                    p.CupRec.CupMatchCnt++;
                    await _playerRepo.UpdateCupRatAsync(p, room.CupId);
                    p.CupPointGain = 0;
                }

                if (room.IsGradeChannel)
                    await UpdateGradeResultSideEffectsAsync(p, u);
            }



            if (!room.IsTrainingChannel)
            {
                await AwardGameIconsAsync(p, u);
                await CheckTitleClearAsync(p, ctx);
            }
        }



        if (room.IsTournamentChannel && room.TournamentSeqNo > 0)
        {
            var gradePlayerMemberNos = report.Users
                .OrderBy(u => u?.Ranking ?? 99)
                .Where(u => u != null)
                .Select(u => u!.MemberNo)
                .ToArray();
            var gradeMemberNos = report.Users
                .OrderBy(u => u?.Ranking ?? 99)
                .Where(u => u != null)
                .Select((u, i) => $"{i + 1:D2}")
                .ToArray();
            var gradePointSums = report.Users
                .OrderBy(u => u?.Ranking ?? 99)
                .Where(u => u != null)
                .Select(u => u!.PointSum)
                .ToArray();

            await _tournament.ReportMatchEndAsync(
                room.TournamentSeqNo, room.TournamentSubId,
                gradePlayerMemberNos, gradeMemberNos, gradePointSums);
        }

        if (room.IsTrainingChannel)
        {
            var trainingPlayers = BuildTrainingHistoryPlayers(report);
            try
            {
                await _historyRepo.InsertTrainingHistAsync(report.ChannelId, report.RoomId,
                    report.RoomOption, trainingPlayers.Length, trainingPlayers);
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "MySQL training history insert failed but game report continues. roomId={RoomId}", room.RoomId);
            }
        }
        else
        {
            try
            {
                await _historyRepo.InsertGameHistAsync(report);
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "MySQL game history insert failed but game report continues. roomId={RoomId}", room.RoomId);
            }
        }

        await ApplyPlayParkMissionsAsync(report);
        await ApplyMissionEventCmsAsync(room, report, DateTime.Now);
        await ApplyResultMissionsAsync(room, report, DateTime.Now);
        await ApplyUsedBadaiFreeItemsAsync(report);
        await ApplyUsedChanceItemsAsync(report);


        var resultPayload = BuildGameResultPayload(room, report);

        if (room.IsTournamentChannel && room.TournamentSeqNo > 0)
        {
            var trnResult = _tournament.GetTournamentResultPayload(
                room.TournamentSeqNo, room.TournamentSubId);
            if (trnResult != null)
            {
                foreach (var kv in trnResult)
                    resultPayload[kv.Key] = kv.Value;
            }
        }

        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(Cmd.GameReport, resultPayload);
        await ctx.Clients.Group($"chanel_{room.ChannelId}")
            .SendAsync(Cmd.GameReport, resultPayload);
        room.LastGameReportPayload = resultPayload;
        _log?.LogInformation("GameReportProcess sent game report. roomId={RoomId} users={UserCount}", room.RoomId, report.Users.Count(u => u != null));

        ClearReservedChanceItems(report);

        await FinishGameReportRoomAsync(room, ctx);
    }

    private async Task FinishGameReportRoomAsync(GameRoom room, CommandContext ctx)
    {

        var playersForContinueClear = room.Seats.Where(seat => seat != null).Select(seat => seat!).ToArray();
        ClearOutPlayerSeats(room);
        if (_roomRegistry != null)
        {
            foreach (var player in playersForContinueClear)
                await _roomRegistry.ClearContinueRoomAsync(player.MemberNo);
        }
        room.State = GameRoomState.Waiting;
        room.CurrentKyoku = 0;
        room.LimitCnt = GameConst.PlayerMaxCount;
        room.PlayHistory.Clear();
        room.ClearOk();
        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
            room.OkButtonStates[i] = false;

        // チャンネルへルーム状態変更通知
        await ctx.Clients.Group($"chanel_{room.ChannelId}")
            .SendAsync(Cmd.RoomState, RoomStatePayload.Build(room, "game_ended"));


        // 60秒ごとに GradeRankBackgroundService ぁEKT_GAMECNTMAST へ書き込む、E
        _gradeRank?.AddGameClearCnt();
        room.CompleteGameReportProcess();
    }

    private async Task ApplyPlayParkMissionsAsync(GameReport report)
    {
        var now = DateTime.Now;
        foreach (var user in report.Users.Where(u => u != null))
        {
            if (user!.MemberNo == TournamentConst.NpcMemberNo) continue;
            var player = _session.GetByMember(user.MemberNo);
            if (player == null) continue;

            if (player.PlayParkDailyMissionAt?.Date != now.Date)
            {
                var daily = await _playerRepo.CallPlayParkMissionAsync(
                    player.MemberNo,
                    GameConst.PlayParkMissionTypeDay,
                    GameConst.PlayParkMissionNo,
                    GameConst.PlayParkProcTypeAdd,
                    1);
                if (daily.Ok)
                    player.PlayParkDailyMissionAt = now;
            }

            if (user.Ranking == 1 && player.PlayParkAttrMission < GameConst.PlayParkAttrMissionMax)
            {
                var attr = await _playerRepo.CallPlayParkMissionAsync(
                    player.MemberNo,
                    GameConst.PlayParkMissionTypeAttr,
                    GameConst.PlayParkMissionNo,
                    GameConst.PlayParkProcTypeAdd,
                    1);
                if (attr.Ok)
                    player.PlayParkAttrMission = attr.RetCount;
            }
        }
    }

    private async Task UpdateGradeResultSideEffectsAsync(MajakPlayer player, GameReport.UserResult user)
    {
        if (player.IsPro)
            await _playerRepo.SaveGradeModeProDataAsync(player, user, DateTime.Now);
        else
            await _playerRepo.MergeGradeRankAsync(BuildGradeRankUpdates(player, user));

        await AwardGradeTitleAsync(player, user);
        await AwardGradeBeginnerMoneyAsync(player, user);
    }

    private async Task ApplyMissionEventCmsAsync(GameRoom room, GameReport report, DateTime now)
    {
        bool hanchan = report.RoomOption.Length > 0 && report.RoomOption[0] == 'H';
        if (!room.IsGradeChannel)
            hanchan = false;
        if (!hanchan || now >= GameConst.MissionEventCmsEndTime)
            return;

        foreach (var user in report.Users.Where(u => u != null))
        {
            var player = _session.GetByMember(user!.MemberNo);
            if (player == null) continue;
            if (player.MissionEventCmsClearAt?.Date == now.Date) continue;

            if (await _playerRepo.CallPcMissionEventCmsAsync(
                    player.MemberNo,
                    GameConst.MissionEventCmsCode,
                    GameConst.MissionEventCmsNo))
            {
                player.MissionEventCmsClearAt = now;
            }
        }
    }

    private async Task ApplyResultMissionsAsync(GameRoom room, GameReport report, DateTime now)
    {
        if (room.SubId == "00T5A") return;

        bool tonpu = !string.IsNullOrEmpty(room.RoomOption) && room.RoomOption[0] == '0';
        int progressCount = tonpu ? 1 : 2;
        int casualCount = tonpu ? CasualPointTonpuRate : CasualPointHanchanRate;

        foreach (var user in report.Users.Where(user => user != null))
        {
            var player = _session.GetByMember(user!.MemberNo);
            if (player == null) continue;

            await SafeSetDailyMissionAsync(player.MemberNo, DailyMissionConditionPlay, progressCount);

            int casualSubType = CasualPointSubTypeNotTop;
            if (user.Ranking == 1)
            {
                await SafeSetDailyMissionAsync(player.MemberNo, DailyMissionConditionTop, 1);
                casualSubType = CasualPointSubTypeTop;
            }

            await SafeCallCasualPointUpdMissionAsync(
                player.MemberNo,
                CasualPointConditionPlay,
                casualSubType,
                casualCount,
                now);
        }
    }

    private async Task SafeSetDailyMissionAsync(string memberNo, int conditionType, int progressIncrement)
    {
        var task = _playerRepo.SetDailyMissionDirectAsync(memberNo, conditionType, progressIncrement);
        if (task != null) await task;
    }

    private async Task SafeCallCasualPointUpdMissionAsync(
        string memberNo,
        int conditionType,
        int conditionSubType,
        int count,
        DateTime procDt)
    {
        var task = _playerRepo.CallCasualPointUpdMissionAsync(memberNo, conditionType, conditionSubType, count, procDt);
        if (task != null) await task;
    }

    private async Task ApplyUsedBadaiFreeItemsAsync(GameReport report)
    {
        foreach (var user in report.Users.Where(user => user != null))
        {
            var player = _session.GetByMember(user!.MemberNo);
            if (player == null || string.IsNullOrEmpty(player.UsedBadaiFreeItem)) continue;

            var task = _playerRepo.UpdateItemQuantityAsync(player, player.MemberNo, player.UsedBadaiFreeItem, -1);
            if (task != null) await task;
        }
    }

    private async Task ApplyUsedChanceItemsAsync(GameReport report)
    {
        foreach (var user in report.Users.Where(user => user != null))
        {
            var player = _session.GetByMember(user!.MemberNo);
            if (player == null || !player.ReserveChanceItem) continue;
            if (!player.MajItems.Any(item => item.ItemCode == ChanceItemCode && item.IsValid && item.Qty > 0))
            {
                player.ReserveChanceItem = false;
                continue;
            }

            var task = _playerRepo.UpdateItemQuantityAsync(player, player.MemberNo, ChanceItemCode, -1);
            if (task != null) await task;
        }
    }

    private void ClearReservedChanceItems(GameReport report)
    {
        foreach (var user in report.Users.Where(user => user != null))
        {
            var player = _session.GetByMember(user!.MemberNo);
            if (player != null) player.ReserveChanceItem = false;
        }
    }

    private static MajakPlayer BuildResultUpdatePlayer(MajakPlayer player, GameReport.UserResult user, GameRoom room)
    {
        var delta = new Models.Player.RatingRecord
        {
            Rating = room.IsGradeChannel ? user.Rating : user.RatingChange,
            MatchCnt = user.MatchCnt,
            WinCnt = user.WinCnt,
            DefeatCnt = user.DefeatCnt,
            DrawCnt = user.DrawCnt,
            Grade1 = user.Ranking == 1 ? 1 : 0,
            Grade2 = user.Ranking == 2 ? 1 : 0,
            Grade3 = user.Ranking == 3 ? 1 : 0,
            Grade4 = user.Ranking == 4 ? 1 : 0,
            TurnCnt = user.TurnCnt,
            DaidaCnt = user.DaidaCnt,
            PointSum = user.PointSum,
            KyokuCnt = user.KyokuCnt,
            HoraCnt = user.HoraCnt,
            HoraPoint = user.HoraPoint,
            HojuCnt = user.HojuCnt,
            HojuPoint = user.HojuPoint,
            RichiCnt = user.RichiCnt,
            FuroCnt = user.FuroCnt,
            TobiCnt = user.TobiCnt,
            TobashiCnt = user.TobashiCnt,
            DoraCnt = user.DoraCnt,
            UraDoraCnt = user.UraDoraCnt,
            RichiHoraCnt = user.RichiHoraCnt,
            TipPoint = user.TipPoint,
            TipMatchCnt = user.TipMatchCnt,
            Grade = user.GradeLevel,
            GradePoint = user.GradePoint,
            TotExtraCount = user.UpdateExtra ? 1 : 0,
            ChannelId = room.ChannelId,
        };

        var update = new MajakPlayer
        {
            MemberNo = player.MemberNo,
            ChannelId = room.ChannelId,
            Rating = player.Rating,
            GamMoney = player.GamMoney,
            SLevel = player.SLevel,
            NLevel = player.NLevel,
            EarnedMoney = player.EarnedMoney,
            GamMoneyU = player.GamMoneyU,
            AllinCnt = player.AllinCnt,
            LastAllinDt = player.LastAllinDt,
            ActiveRecord = delta,
        };

        if (room.IsGradeChannel) update.GradeRecord = delta;
        else if (room.IsCompeteChannel) update.CompeteRecord = delta;
        else if (room.IsHiClassChannel) update.HiClassRecord = delta;
        else update.RegularRecord = delta;

        return update;
    }

    private async Task AwardGradeBeginnerMoneyAsync(MajakPlayer player, GameReport.UserResult user)
    {
        if (!user.UpdateBeginner) return;

        const long beginnerPresent = 5000;
        long preMoney = player.GamMoney;
        if (await _playerRepo.AddEarnedMoneyAsync(
            player.MemberNo,
            beginnerPresent,
            GameConst.EvtCodeGradeBeginnerPresent,
            preMoney))
        {
            player.EarnedMoney += beginnerPresent;
        }
    }

    private static IReadOnlyList<GradeRankUpdateItem> BuildGradeRankUpdates(MajakPlayer player, GameReport.UserResult user)
    {
        int rankDate = DateTime.Now.Year * 100 + DateTime.Now.Month;
        var rows = new List<GradeRankUpdateItem>
        {
            new()
            {
                RankDate = rankDate,
                RankKind = GameConst.RatingRankAll,
                MemberNo = player.MemberNo,
                Rating = user.Rating,
                Grade = user.GradeLevel,
                ExtraCount = 0,
                AvatarId = player.AvatarId,
                DispFlag = player.DispRange,
            },
            new()
            {
                RankDate = rankDate,
                RankKind = user.GradeLevel,
                MemberNo = player.MemberNo,
                Rating = user.Rating,
                Grade = user.GradeLevel,
                ExtraCount = 0,
                AvatarId = player.AvatarId,
                DispFlag = player.DispRange,
            },
        };

        if (user.UpdateExtra)
        {
            rows.Add(new GradeRankUpdateItem
            {
                RankDate = rankDate,
                RankKind = 19,
                MemberNo = player.MemberNo,
                Rating = user.Rating,
                Grade = user.GradeLevel,
                ExtraCount = 1,
                AvatarId = player.AvatarId,
                DispFlag = player.DispRange,
            });
        }

        return rows;
    }

    private async Task AwardGradeTitleAsync(MajakPlayer player, GameReport.UserResult user)
    {
        string? titleId = BuildGradeTitleId(user);
        if (titleId == null || player.GradeTitleList.Contains(titleId)) return;

        await _playerRepo.InsertOrEnableTitleAsync(player.MemberNo, titleId);
        player.GradeTitleList.Add(titleId);
    }

    private static string? BuildGradeTitleId(GameReport.UserResult user)
    {
        if (user.GradeUpDown == 0 && !user.UpdateExtra) return null;

        int titleNo = user.UpdateExtra ? 519 : 500 + user.GradeLevel;
        return string.Format(GameConst.RatingTitleFormat, titleNo);
    }

    private static (string MemberNo, int Point)[] BuildTrainingHistoryPlayers(GameReport report)
    {
        return report.Users
            .Where(u => u != null && !string.IsNullOrEmpty(u!.MemberNo))
            .OrderBy(u => u!.Ranking)
            .Select(u =>
            {
                int point = u!.SetPoint + u.SetUma + u.SetTor + u.SetTip;
                return (u.MemberNo, point);
            })
            .ToArray();
    }

    private static void AddResultRecordDelta(Models.Player.RatingRecord record, GameReport.UserResult user)
    {
        record.MatchCnt += user.MatchCnt;
        record.WinCnt += user.WinCnt;
        record.DefeatCnt += user.DefeatCnt;
        record.DrawCnt += user.DrawCnt;
        if (user.Ranking == 1) record.Grade1++;
        else if (user.Ranking == 2) record.Grade2++;
        else if (user.Ranking == 3) record.Grade3++;
        else if (user.Ranking == 4) record.Grade4++;
        record.TurnCnt += user.TurnCnt;
        record.DaidaCnt += user.DaidaCnt;
        record.PointSum += user.PointSum;
        record.KyokuCnt += user.KyokuCnt;
        record.HoraCnt += user.HoraCnt;
        record.HoraPoint += user.HoraPoint;
        record.HojuCnt += user.HojuCnt;
        record.HojuPoint += user.HojuPoint;
        record.RichiCnt += user.RichiCnt;
        record.FuroCnt += user.FuroCnt;
        record.TobiCnt += user.TobiCnt;
        record.TobashiCnt += user.TobashiCnt;
        record.DoraCnt += user.DoraCnt;
        record.UraDoraCnt += user.UraDoraCnt;
        record.RichiHoraCnt += user.RichiHoraCnt;
        record.TipPoint += user.TipPoint;
        record.TipMatchCnt += user.TipMatchCnt;
    }

    private async Task AwardGameIconsAsync(MajakPlayer player, GameReport.UserResult user)
    {
        foreach (string iconCode in GetGameIconCodesForReport(player, user))
            await _playerRepo.GrantGameIconAsync(player.MemberNo, iconCode);
    }

    private static IEnumerable<string> GetGameIconCodesForReport(MajakPlayer player, GameReport.UserResult user)
    {
        if (user.WinCnt > 0)
        {
            string? contWin = CheckAndGetGameIconCode(4, player.ContWinDefeat);
            if (contWin != null) yield return contWin;

            string? totalWin = CheckAndGetGameIconCode(2, player.ActiveRecord.WinCnt);
            if (totalWin != null) yield return totalWin;
        }

        string? totalMatch = CheckAndGetGameIconCode(1, player.ActiveRecord.MatchCnt);
        if (totalMatch != null) yield return totalMatch;

        string? tobashi = CheckAndGetGameIconCode(3, player.ActiveRecord.TobashiCnt);
        if (tobashi != null) yield return tobashi;
    }

    private static string? CheckAndGetGameIconCode(int kind, int point)
    {
        foreach (var item in GameIconMaster)
        {
            if (item.Kind == kind && item.Point == point)
                return item.IconCode;
        }
        return null;
    }

    private static void UpdateHiClassStreaks(MajakPlayer player, GameReport.UserResult user)
    {
        if (user.Ranking == 1)
        {
            player.H_ContTopNow++;
            if (player.H_ContTopNow > player.H_ContTopMax)
                player.H_ContTopMax = player.H_ContTopNow;
        }
        else
        {
            player.H_ContTopNow = 0;
        }
    }

    private static void ClearOutPlayerSeats(GameRoom room)
    {
        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
        {
            if (room.Seats[i]?.IsOutPlayer == true)
                room.Seats[i] = null;
        }
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>



    ///   ↁEpMajakPlayer->m_nSetRank/m_nSetTotal などからスコアを取征E
    ///

    ///   SetPoint / SetTotal / SetRank はエンジン (EnginePlayer) の計算値を使用する、E


    /// </summary>
    private async Task<GameReport?> MakeGameReportAsync(GameRoom room, CommandContext ctx)
    {
        var report = new GameReport
        {
            RoomId     = room.RoomId,
            ChannelId  = room.ChannelId,
            PrivateYn  = room.IsPrivate,
            RoomOption = ConvertReportRoomOption(room.RoomOption),
            MoneyRate  = room.MoneyRate,
            MinMoney   = room.MinMoney,
            MaxMoney   = room.MaxMoney,
        };


        var userScores = ctx.GetObjectArray("userScores");
        bool[] reportedEngineOrders = new bool[GameConst.PlayerMaxCount];

        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
        {
            var seat   = room.Seats[i];
            if (seat == null) continue;

            // エンジンのプレイヤーオーダーを取征E(SeatToEngineOrder マッピング)
            int engineOrder = room.SeatToEngineOrder[i];
            var ep = room.Engine.Player[engineOrder];
            if (engineOrder >= 0 && engineOrder < reportedEngineOrders.Length)
                reportedEngineOrders[engineOrder] = true;

            // クライアント統訁E(memberNo でマッチング)
            var s = userScores?.FirstOrDefault(x => x?.MemberNo == seat.MemberNo);

            // ─── スコアはすべてエンジン値を使用 ───────────────────────────
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo       = seat.MemberNo,
                IsConnect      = !seat.IsOutPlayer,
                // エンジン計算値 (改ざん不可)
                Ranking        = ep.SetRank + 1,         // 0-based ↁE1-based
                Score          = ep.GamePoint,
                GameScore      = ep.GamePoint,
                SetPoint       = ep.SetPoint,
                SetUma         = ep.SetUma,
                SetTor         = ep.SetTor,
                SetTip         = ep.SetTip,
                TipPoint       = ep.ResultRecord.TipPoint,
                TipMatchCnt    = ep.ResultRecord.TipMatchCnt,
                PointSum       = ep.ResultRecord.PointSum,
                Yakitori       = ep.IsYakitori,
                Chip           = ep.Tip,
                MoneyChange    = (long)ep.SetTotal * room.MoneyRate,
                PrevMoney      = seat.GamMoney,
                // エンジン統計値
                KyokuCnt       = ep.ResultRecord.KyokuCnt,
                HoraCnt        = ep.ResultRecord.HoraCnt,
                HoraPoint      = ep.ResultRecord.HoraPoint,
                HojuCnt        = ep.ResultRecord.HojuCnt,
                HojuPoint      = ep.ResultRecord.HojuPoint,
                RichiCnt       = ep.ResultRecord.RichiCnt,
                FuroCnt        = ep.ResultRecord.FuroCnt,
                TobiCnt        = ep.ResultRecord.TobiCnt,
                TobashiCnt     = ep.ResultRecord.TobashiCnt,
                DoraCnt        = ep.ResultRecord.DoraCnt,
                UraDoraCnt     = ep.ResultRecord.UraDoraCnt,
                RichiHoraCnt   = ep.ResultRecord.RichiHoraCnt,
                DaidaCnt       = ep.ResultRecord.DaidaCnt,

                TurnCnt        = s?.TurnCnt  ?? ep.ResultRecord.TurnCnt,
                WinCnt         = ep.SetRank == 0 ? 1 : 0,
                DefeatCnt      = ep.SetRank == GameConst.PlayerMaxCount - 1 ? 1 : 0,
                DrawCnt        = 0,
                MatchCnt       = 1,
                PrevNLevel     = seat.NLevel,

                PrevGradeLevel = seat.GradeRecord.Grade,
                PrevGradePoint = seat.GradeRecord.GradePoint,
                GradeLevel     = seat.GradeRecord.Grade,
                GradePoint     = seat.GradeRecord.GradePoint,
            };
        }

        if (room.IsTrainingChannel || room.IsTournamentChannel)
            AddNpcReportRows(report, room, reportedEngineOrders);

        await Task.CompletedTask;
        return report;
    }

    private static void AddNpcReportRows(GameReport report, GameRoom room, bool[] reportedEngineOrders)
    {
        for (int engineOrder = 0; engineOrder < GameConst.PlayerMaxCount; engineOrder++)
        {
            if (reportedEngineOrders[engineOrder]) continue;
            int playerPos = room.Engine.HanchanInfo.Player[engineOrder];
            int slot = playerPos >= 0 && playerPos < report.Users.Length && report.Users[playerPos] == null
                ? playerPos
                : Array.FindIndex(report.Users, u => u == null);
            if (slot < 0) return;

            var ep = room.Engine.Player[engineOrder];
            report.Users[slot] = new GameReport.UserResult
            {
                MemberNo = TournamentConst.NpcMemberNo,
                IsConnect = false,
                Connected = false,
                Ranking = ep.SetRank + 1,
                Score = ep.GamePoint,
                GameScore = ep.GamePoint,
                SetPoint = ep.SetPoint,
                SetUma = ep.SetUma,
                SetTor = ep.SetTor,
                SetTip = ep.SetTip,
                TipPoint = ep.ResultRecord.TipPoint,
                TipMatchCnt = ep.ResultRecord.TipMatchCnt,
                PointSum = ep.ResultRecord.PointSum,
                Yakitori = ep.IsYakitori,
                Chip = ep.Tip,
                MoneyChange = (long)ep.SetTotal * room.MoneyRate,
                KyokuCnt = ep.ResultRecord.KyokuCnt,
                HoraCnt = ep.ResultRecord.HoraCnt,
                HoraPoint = ep.ResultRecord.HoraPoint,
                HojuCnt = ep.ResultRecord.HojuCnt,
                HojuPoint = ep.ResultRecord.HojuPoint,
                RichiCnt = ep.ResultRecord.RichiCnt,
                FuroCnt = ep.ResultRecord.FuroCnt,
                TobiCnt = ep.ResultRecord.TobiCnt,
                TobashiCnt = ep.ResultRecord.TobashiCnt,
                DoraCnt = ep.ResultRecord.DoraCnt,
                UraDoraCnt = ep.ResultRecord.UraDoraCnt,
                RichiHoraCnt = ep.ResultRecord.RichiHoraCnt,
                DaidaCnt = ep.ResultRecord.DaidaCnt,
            };
        }
    }

    private static string ConvertReportRoomOption(string roomOption)
    {
        char[] converted = roomOption.ToCharArray();
        ConvertRoomOptionAt(converted, 0, "TH");
        ConvertRoomOptionAt(converted, 1, "0123");
        ConvertRoomOptionAt(converted, 2, "fqns");
        ConvertRoomOptionAt(converted, 3, "-K");
        ConvertRoomOptionAt(converted, 4, "-Y");
        ConvertRoomOptionAt(converted, 5, "-Rr");
        ConvertRoomOptionAt(converted, 6, "-o");
        ConvertRoomOptionAt(converted, 7, "-c");
        ConvertRoomOptionAt(converted, 8, "-A");
        ConvertRoomOptionAt(converted, 9, "-N");
        ConvertRoomOptionAt(converted, 10, "-W");
        ConvertRoomOptionAt(converted, 11, "-C");
        ConvertRoomOptionAt(converted, 12, "-dt");
        ConvertRoomOptionAt(converted, 13, "-gG");
        return new string(converted);
    }

    private static void ConvertRoomOptionAt(char[] roomOption, int index, string values)
    {
        if (index >= roomOption.Length) return;
        char c = roomOption[index];
        roomOption[index] = c >= '0' && c < '0' + values.Length ? values[c - '0'] : 'X';
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>



    ///   ↁEllFinalMoneyChange = MoneyChange - RoomCharge
    /// </summary>
    private void CalcMoney(GameRoom room, GameReport report)
    {

        // チャンネル SubId によって場代レートが変わめE(Beginner=100x, Grade=100x, Default=200x)
        long roomCharge = GetRoomChargeCommon(room);

        foreach (var u in report.Users.Where(u => u != null).Cast<GameReport.UserResult>())
        {
            long charge = roomCharge;
            var p = _session.GetByMember(u.MemberNo);
            long ratio = u.MoneyChangeRatio == 0 ? 1 : u.MoneyChangeRatio;
            long finalMoneyChange = u.MoneyChange > 0 ? u.MoneyChange * ratio : 0;


            long netMoney = (p?.GamMoney ?? 0) + finalMoneyChange;
            if (charge > netMoney) charge = Math.Max(0, netMoney);

            if (p != null && charge > 0 && TryUseChargeFreeItem(p, room.SubId, out string itemCode))
            {
                charge = 0;
                p.UsedBadaiFreeItem = itemCode;
            }

            if (p != null)
                p.RoomCharge = charge;
            u.DealerFee = charge;
            u.MoneyChange = finalMoneyChange - charge;
            u.CurrMoney = (p?.GamMoney ?? u.PrevMoney) + u.MoneyChange;
            u.CurrLent = u.PrevLent;
        }
    }

    private static bool TryUseChargeFreeItem(MajakPlayer player, string subId, out string itemCode)
    {
        string requiredItemCode = subId == "0085F" ? "MJ23" : "MJ20";
        itemCode = requiredItemCode;
        return player.MajItems.Any(item =>
            item.ItemCode == requiredItemCode && item.IsValid && item.Qty > 0);
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>


    ///   pMajRating->GetExperience(experience, horaPoint, hojuPoint)
    /// </summary>
    private void CalcExperience(GameReport report)
    {
        foreach (var u in report.Users.Where(u => u != null).Cast<GameReport.UserResult>())
        {
            var p = _session.GetByMember(u.MemberNo);
            if (p == null) continue;

            int gain = _ratingService.GetExperience(p.Experience, u.HoraPoint, u.HojuPoint);
            u.ExperienceGain = Math.Max(0, gain);
            u.Experience     = p.Experience + u.ExperienceGain;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// コイン変動値の整合性検証、E



    /// </summary>
    private bool ValidateMoneyReport(GameReport report, GameRoom room)
    {
        long unitMoney = room.UnitMoney;
        bool valid = true;
        foreach (var u in report.Users.Where(u => u != null).Cast<GameReport.UserResult>())
        {
            if (unitMoney == 0)
            {
                if (u.MoneyChange != 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[HACKING] MoneyChange={u.MoneyChange} but UnitMoney=0. MemberNo={u.MemberNo}");
                    valid = false;
                }
            }
            else
            {
                if (u.MoneyChange % unitMoney != 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[HACKING] MoneyChange={u.MoneyChange} not divisible by UnitMoney={unitMoney}. MemberNo={u.MemberNo}");
                    valid = false;
                }
            }
        }
        return valid;
    }


    // ─────────────────────────────────────────────────────────────
    /// <summary>



    ///   s_stLevelGradeMode[] (段位別 MaxPoint/InitPoint/降段対象)から判定、E
    ///   東風局 (SubId[3]=='6') はポイント働山めE/2にする、E
    /// </summary>
    private static void CalcGradeModeLeveUp(GameReport report, GameRoom room)
    {
        // チャンネルタイチE SubId[4] ('A'=通常, 'B'=上紁E 'C'=上上紁E 'D'=特選上紁E
        char chanType   = room.SubId.Length > 4 ? room.SubId[4] : 'A';

        int  pointDivide = room.SubId.Length > 3 && room.SubId[3] == '6' ? 2 : 1;

        foreach (var u in report.Users.Where(u => u != null).Cast<GameReport.UserResult>())
        {
            int level    = u.PrevGradeLevel;
            int nowPoint = u.PrevGradePoint;
            int rank     = u.Ranking;   // 1-based


            int getPoint = GradeModeGetPoint(level, chanType, rank) / pointDivide;

            // 段位情報をテーブルから引く
            var info     = GradeLevelInfo(level);
            var prevInfo = level > 0  ? GradeLevelInfo(level - 1) : info;
            var nextInfo = level < 18 ? GradeLevelInfo(level + 1) : info;  // GRADE_9_DAN=18

            int rawPoint = nowPoint + getPoint;
            int newPoint = Math.Max(rawPoint, 0);
            int newLevel = level;
            int upDown   = 0;  // GRADE_STAY
            bool updateBeginner = false;
            bool updateExtra = false;
            int gradeNextPoint = Math.Max(info.MaxPoint - rawPoint, 0);

            // 降段判宁E
            if (newPoint <= 0 && info.DownGrade)
            {
                newLevel = level - 1;
                newPoint = prevInfo.InitPoint;
                gradeNextPoint = info.MaxPoint;
                upDown   = 2;  // GRADE_DOWN
            }
            else if (newPoint <= 0)
            {
                newPoint = 0;  // 降段なしでポイント、EにクランチE
                gradeNextPoint = info.MaxPoint;
            }


            if (info.MaxPoint > 0 && newPoint >= info.MaxPoint)
            {
                gradeNextPoint = 0;
                upDown = 1;  // GRADE_UP
                if (level < 18 && nextInfo.MaxPoint > 0)
                {
                    newLevel = level + 1;
                    newPoint = nextInfo.InitPoint;
                }
                else
                {

                    newLevel = level;
                    newPoint = info.InitPoint;
                    updateExtra = true;
                }
                // 1段進級フラグ
                if (newLevel == 10 /*GRADE_1_DAN*/) updateBeginner = true;
            }


            u.GradeLevel    = newLevel;
            u.GradePoint    = newPoint;
            u.GradePointTmp = Math.Max(rawPoint, 0);
            u.GradeAddPoint = getPoint;
            u.GradeNextPoint = gradeNextPoint;
            u.GradeUpDown   = upDown;
            u.UpdateBeginner = updateBeginner;
            u.UpdateExtra    = updateExtra;
        }
    }


    // key: (gradeLevel, chanType), value: [1位点,2位点,3位点,4位点]
    private static readonly Dictionary<(int, char), int[]> s_GetPointTable = new()
    {
        // 通常チャンネル 'A'
        {(0,'A'),  new[]{30,  10,   0,    0}},
        {(1,'A'),  new[]{30,  10,   0,    0}},
        {(2,'A'),  new[]{30,  10,   0,    0}},
        {(3,'A'),  new[]{30,  10,   0,    0}},
        {(4,'A'),  new[]{30,  10,   0,    0}},
        {(5,'A'),  new[]{30,  10,   0,    0}},
        {(6,'A'),  new[]{30,  10,   0,    0}},
        {(7,'A'),  new[]{30,  10,   0,    0}},
        {(8,'A'),  new[]{30,  10,   0,  -10}},
        {(9,'A'),  new[]{30,  10,   0,  -20}},
        {(10,'A'), new[]{30,  10, -10,  -30}},
        // 上級チャンネル 'B'
        {(10,'B'), new[]{60,  20, -16,  -48}},
        {(11,'B'), new[]{60,  20, -18,  -54}},
        {(12,'B'), new[]{60,  20, -20,  -60}},
        {(13,'B'), new[]{60,  20, -22,  -66}},
        {(14,'B'), new[]{60,  20, -24,  -72}},
        {(15,'B'), new[]{60,  20, -26,  -78}},
        {(16,'B'), new[]{60,  20, -30,  -90}},
        {(17,'B'), new[]{60,  20, -34, -102}},
        {(18,'B'), new[]{60,  20, -38, -114}},
        // 上上級チャンネル 'C'
        {(11,'C'), new[]{90,  30, -21,  -63}},
        {(12,'C'), new[]{90,  30, -24,  -72}},
        {(13,'C'), new[]{90,  30, -27,  -81}},
        {(14,'C'), new[]{90,  30, -30,  -90}},
        {(15,'C'), new[]{90,  30, -33,  -99}},
        {(16,'C'), new[]{90,  30, -36, -108}},
        {(17,'C'), new[]{90,  30, -39, -117}},
        {(18,'C'), new[]{90,  30, -42, -126}},
        // 特選上級チャンネル 'D'
        {(13,'D'), new[]{120, 40, -28,  -84}},
        {(14,'D'), new[]{120, 40, -32,  -96}},
        {(15,'D'), new[]{120, 40, -36, -108}},
        {(16,'D'), new[]{120, 40, -40, -120}},
        {(17,'D'), new[]{120, 40, -44, -132}},
        {(18,'D'), new[]{120, 40, -48, -144}},
    };

    private static int GradeModeGetPoint(int level, char chanType, int rank)
    {
        if (s_GetPointTable.TryGetValue((level, chanType), out var pts))
        {
            int idx = Math.Clamp(rank - 1, 0, 3);
            return pts[idx];
        }
        return 0;
    }


    private record GradeLevelData(int InitPoint, int MaxPoint, bool DownGrade);
    private static readonly GradeLevelData[] s_LevelTable =
    [
        new(0,    30,  false), // 0: 10紁E
        new(0,    30,  false), // 1:  9紁E
        new(0,    30,  false), // 2:  8紁E
        new(0,    30,  false), // 3:  7紁E
        new(0,    60,  false), // 4:  6紁E
        new(0,    60,  false), // 5:  5紁E
        new(0,    60,  false), // 6:  4紁E
        new(0,    90,  false), // 7:  3紁E
        new(0,    90,  false), // 8:  2紁E
        new(0,    90,  false), // 9:  1紁E
        new(0,   600,  false), //10:  1段
        new(600, 1200,  true), //11:  2段
        new(600, 1200,  true), //12:  3段
        new(1200,2400,  true), //13:  4段
        new(1200,2400,  true), //14:  5段
        new(1200,2400,  true), //15:  6段
        new(2400,4800,  true), //16:  7段
        new(2400,4800,  true), //17:  8段
        new(2400,4800,  true), //18:  9段
    ];
    private static GradeLevelData GradeLevelInfo(int level)
        => level >= 0 && level < s_LevelTable.Length ? s_LevelTable[level] : new(0, 0, false);


    // ─────────────────────────────────────────────────────────────
    /// <summary>





    /// </summary>
    private void CalcRating(GameReport report, GameRoom room)
    {

        if (room.IsTrainingChannel) return;

        if (room.IsGradeChannel)
        {
            // ─── CalcRating_MajakTypeGradeMode ───────────────────────────────

            var gradePlayers = report.Users
                .Where(u => u != null && !string.IsNullOrEmpty(u!.MemberNo))
                .ToArray();
            if (gradePlayers.Length < 2) return;

            int ratingSum = gradePlayers.Sum(u =>
            {
                var p = _session.GetByMember(u!.MemberNo);
                return p?.ActiveRecord.Rating ?? 0;
            });
            int ratingAvg = ratingSum / gradePlayers.Length;

            foreach (var u in gradePlayers)
            {
                var p = _session.GetByMember(u!.MemberNo);
                int currRating = p?.ActiveRecord.Rating ?? 0;
                int newRating  = _ratingService.CalcGradeRating(
                    currRating, u.PointSum, u.MatchCnt, ratingAvg);
                u.RatingChange = newRating - currRating;
                u.Rating       = newRating;
            }
        }
        else
        {
            // ─── CalcRating_MajakType (Elo RATING_RULE_TYPE1) ────────────────

            //   K = 20, R_s = 400 (GGameInfo.m_stFactor からの代表値)
            //   TYPE1: Sigma(i!=j) winNotation * K * P_ij * Regulation
            var players = report.Users
                .Where(u => u != null && !string.IsNullOrEmpty(u!.MemberNo))
                .Select(u =>
                {
                    var p = _session.GetByMember(u!.MemberNo);
                    return new { User = u, Rating = p?.ActiveRecord.Rating ?? 0, MatchCnt = p?.ActiveRecord.MatchCnt ?? u!.MatchCnt };
                })
                .ToArray();

            if (players.Length < 2) return;

            int   n  = players.Length;
            float K  = room.RatingK;
            float Rs = room.RatingRs <= 0 ? 400f : room.RatingRs;

            if (room.RatingRuleType == 2)
            {
                CalcRatingType2(report, room, BuildRatingTeams(players.Select(p => (p.User!, p.Rating, p.MatchCnt)).ToArray()), K, Rs);
                return;
            }

            if (players.Any(p => p.User!.TeamId >= 0))
            {
                CalcRatingType1Team(report, room, BuildRatingTeams(players.Select(p => (p.User!, p.Rating, p.MatchCnt)).ToArray()), K, Rs);
                return;
            }

            for (int i = 0; i < n; i++)
            {
                float R_acquired = 0f;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (players[i].User!.Ranking == players[j].User!.Ranking) continue;

                    int   winNotation = players[i].User!.Ranking < players[j].User!.Ranking ? 1 : -1;
                    float P_ij    = 1f / (1f + MathF.Pow(10f,
                        -(players[i].Rating - players[j].Rating) / Rs));
                    float fA = winNotation * K;
                    float fB = winNotation == 1 ? 1f - P_ij : P_ij;
                    int teamCount = n;
                    float fC = 2f * (n - 1) / (n * (teamCount - 1));
                    float fD = MathF.Sqrt((float)n / teamCount);
                    R_acquired += fA * fB * fC * fD;
                }
                int acquired = ApplyRatingMemberFactors((int)R_acquired, players[i].Rating, players[i].MatchCnt, room);
                players[i].User!.RatingChange = acquired;
                players[i].User!.Rating       = players[i].Rating + acquired;
            }
        }
    }

    private static void CalcRatingType1Team(
        GameReport report,
        GameRoom room,
        RatingTeam[] teams,
        float k,
        float ratingScale)
    {
        if (teams.Length < 2) return;

        int memberCount = teams.Sum(team => team.Members.Length);
        int teamCount = teams.Length;
        for (int i = 0; i < teamCount; i++)
        {
            float teamAcquired = 0f;
            for (int j = 0; j < teamCount; j++)
            {
                if (i == j || teams[i].Ranking == teams[j].Ranking) continue;

                int winNotation = teams[i].Ranking < teams[j].Ranking ? 1 : -1;
                float probability = 1f / (1f + MathF.Pow(10f, -(teams[i].Rating - teams[j].Rating) / ratingScale));
                float resultFactor = winNotation == 1 ? 1f - probability : probability;
                float regulation = 2f * (memberCount - 1) / (memberCount * (teamCount - 1))
                    * MathF.Sqrt((float)memberCount / teamCount);
                teamAcquired += winNotation * k * resultFactor * regulation;
            }

            int baseAcquired = (int)(teamAcquired / teams[i].Members.Length);
            foreach (var member in teams[i].Members)
            {
                int acquired = ApplyRatingMemberFactors(baseAcquired, member.Rating, member.MatchCnt, room);
                member.User.RatingChange = acquired;
                member.User.Rating = member.Rating + acquired;
            }
        }
    }

    private readonly record struct RatingTeam(
        (GameReport.UserResult User, int Rating, int MatchCnt)[] Members,
        int Rating,
        int Ranking);

    private static RatingTeam[] BuildRatingTeams((GameReport.UserResult User, int Rating, int MatchCnt)[] players)
    {
        return players
            .GroupBy(p => p.User.TeamId >= 0 ? $"team:{p.User.TeamId}" : $"member:{p.User.MemberNo}")
            .Select(group => new RatingTeam(
                Members: group.ToArray(),
                Rating: group.Sum(p => p.Rating - 1000),
                Ranking: group.Min(p => p.User.Ranking)))
            .ToArray();
    }

    private static void CalcRatingType2(
        GameReport report,
        GameRoom room,
        RatingTeam[] teams,
        float k,
        float ratingScale)
    {
        int teamCount = teams.Length;
        if (teamCount < 2) return;

        float[,] probability = new float[5, 5];
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (i >= teamCount)
                    probability[i, j] = 0f;
                else if (j >= teamCount)
                    probability[i, j] = 1f;
                else
                    probability[i, j] = 1f / (1f + MathF.Pow(10f, -(teams[i].Rating - teams[j].Rating) / ratingScale));
            }
        }

        for (int i = 0; i < teamCount; i++)
        {
            int winNotation = teams[i].Ranking == 1 ? 1 : -1;
            float factor = winNotation * k;

            int a = i, b = -1, c = -1, d = -1, e = -1;
            for (int j = 0; j < 5; j++)
            {
                if (j == a) continue;
                if (b == -1) b = j;
                else if (c == -1) c = j;
                else if (d == -1) d = j;
                else if (e == -1) e = j;
            }

            if (teamCount == 2)
                probability[c, d] = 1f;

            float topProbability =
                probability[a, b] * probability[c, d] * probability[a, c]
                + probability[a, b] * probability[d, c] * probability[a, d]
                + probability[a, c] * probability[b, d] * probability[a, b]
                + probability[a, c] * probability[d, b] * probability[a, d]
                + probability[a, d] * probability[b, c] * probability[a, b]
                + probability[a, d] * probability[c, b] * probability[a, c];
            topProbability /= 3f;

            if (winNotation == 1)
                topProbability = 1f - topProbability;

            int teamAcquired = (int)(factor * topProbability);
            int baseAcquired = (int)((1.0f / teams[i].Members.Length) * teamAcquired);
            foreach (var member in teams[i].Members)
            {
                int acquired = baseAcquired;
                if (member.User.Ranking > 2 && acquired > -1)
                    acquired = -1;

                acquired = ApplyRatingMemberFactors(acquired, member.Rating, member.MatchCnt, room);
                member.User.RatingChange = acquired;
                member.User.Rating = member.Rating + acquired;
            }
        }
    }

    private static int ApplyRatingMemberFactors(int acquired, int rating, int matchCnt, GameRoom room)
    {
        if (rating < room.RatingNoviceThreshold)
            acquired *= room.RatingNoviceRate;
        else if (rating > room.RatingExpertThreshold)
            acquired *= room.RatingExpertRate;

        if (rating < room.RatingBonusThreshold && matchCnt < room.RatingNoviceThreshold)
            acquired += room.RatingBonus;

        return acquired;
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// commandMajAutoStart (mjkc4e) ペイロードを構築、E

    ///   + AddToParser_GetMemberListResponse (GSvcRoomServer + HMajRoomServer 牁E

    /// </summary>
    private static Dictionary<string, object?> BuildAutoStartPayload(GameRoom room, int gemGame)
    {

        string hostId    = room.Seats[0]?.Pix ?? "";
        string roomCreator = room.Seats.FirstOrDefault(p => p?.MemberNo == room.CreatorNo)?.Pix ?? "";
        int    playerCnt = room.Seats.Count(s => s != null);
        string reserveMemberNo = room.Seats.FirstOrDefault(p => p?.MemberNo == room.BanishInfo.ReserveMemberNo)?.Pix
            ?? room.Viewers.FirstOrDefault(p => p.MemberNo == room.BanishInfo.ReserveMemberNo)?.Pix
            ?? "";

        var payload = new Dictionary<string, object?>
        {
            // G::keyCount / keyRoomCreator / keyRoomHost
            [GKey.Count]            = playerCnt,
            [GKey.RoomCreator]      = roomCreator,
            [GKey.RoomHost]         = hostId,

            [GKey.PreBanishing]     = room.BanishInfo.PreBanishing ? 1 : 0,
            [GKey.ReserveBanishing] = room.BanishInfo.ReserveBanishing ? 1 : 0,
            [GKey.Pix]         = reserveMemberNo,

            [Key.GemGame]           = gemGame,
        };

        int seq = 0;
        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
        {
            var p = room.Seats[i];
            if (p == null) continue;

            // ── CHgPlayerInfo::LoadEx が解析するフィールチE(シーケンス付き) ──
            payload[$"{GKey.PlayerType}{seq}"]       = GKey.ValuePlayer;      // "v4e" = player
            payload[$"{GKey.PlayerPos}{seq}"]        = (int)p.SeatPos;
            payload[$"{GKey.Pix}{seq}"]         = p.Pix;
            payload[$"{GKey.AvatarId}{seq}"]         = p.AvatarId;
            payload[$"{GKey.Name}{seq}"]             = p.NickName;            // 表示吁E= NickName
            payload[$"{GKey.Sex}{seq}"]              = p.Sex;
            payload[$"{GKey.Age}{seq}"]              = 0;
            payload[$"{GKey.Location}{seq}"]         = "";
            payload[$"{GKey.TotMoney}{seq}"]         = p.GamMoney;
            payload[$"{GKey.MatchCnt}{seq}"]         = p.ActiveRecord.MatchCnt;
            payload[$"{GKey.WinCnt}{seq}"]           = p.ActiveRecord.WinCnt;
            payload[$"{GKey.DefeatCnt}{seq}"]        = p.ActiveRecord.DefeatCnt;
            payload[$"{GKey.DrawCnt}{seq}"]          = p.ActiveRecord.DrawCnt;
            payload[$"{GKey.DisconnCnt}{seq}"]       = p.ActiveRecord.DisconnCnt;
            payload[$"{GKey.Rating}{seq}"]           = p.Rating;
            payload[$"{GKey.SLevel}{seq}"]           = p.SLevel;
            payload[$"{GKey.NLevel}{seq}"]           = p.NLevel;
            payload[$"{GKey.GamMoney}{seq}"]         = p.GamMoney;
            payload[$"{GKey.GamRanking}{seq}"]       = 0;
            payload[$"{GKey.ReservedString}{seq}"]   = "";
            payload[$"{GKey.LastDisconn}{seq}"]      = "";
            payload[$"{GKey.ExScoreCnt}{seq}"]       = 0;
            payload[$"{GKey.DispRange}{seq}"]        = p.DispRange;
            // ── HMajRoomServer::AddToParser_GetMemberListResponse 追加フィールチE──
            payload[$"{Key.NickName}{seq}"]          = p.NickName;
            payload[$"{Key.TrickTitle}{seq}"]        = p.TrickTitle;
            payload[$"{Key.MajakTitle}{seq}"]        = p.MajakTitle;
            payload[$"{Key.RichiEffect}{seq}"]       = p.GetRichiEffect();
            // カスタムコスチューム (CUSTOMITEM_COSTUME_N = 30)
            int costumeId = p.GetCustomEquip(30);
            int costumeType = p.CustomItems.TryGetValue(costumeId, out var ci) ? ci.Kind : 0;
            payload[$"{Key.CustomCostume}{seq}"]     = costumeId;
            payload[$"{Key.CustomCostumeType}{seq}"] = costumeType;

            seq++;
        }

        return payload;
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// ジェムゲーム種別を抽選する、E


    ///   NOT_GEM_GAME=0 / ONE_GEM_GAME=1 / BIG_GEM_GAME=2
    /// </summary>
    private static int CalcGemGame(GameRoom room)
    {
        // CanPlayGemGame: SubId[0] == 'T' (_MODIFY_GEM_COUNT 定義済みの場吁E
        bool canPlay = room.SubId.Length >= 1 && room.SubId[0] == 'T';
        if (!canPlay) return 0; // NOT_GEM_GAME

        int x = Random.Shared.Next(10000);
        return x < 1000 ? 2 :   // BIG_GEM_GAME
               x < 5000 ? 1 :   // ONE_GEM_GAME (big=1000, hit=4000 ↁE1000+4000=5000)
               0;                // NOT_GEM_GAME
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// ジェムゲーム結果通知 (S→C, commandGetGem / mjkc22e)、E

    ///   static const int gemtbl[] = {0, 2, 20};  // NOT/ONE/BIG
    ///   nOrder == 0 or 1 (1佁E2佁E が対象 (_MODIFY_GEM_COUNT)

    ///   副作用: SetDailyMission(MSN_CT_GETGEM=5, 1)
    ///           UpdateResult_GambleType で MJKCOMMONRAT.GEMCNT += nGemCount / GAMEMONEYHIST 追加
    /// </summary>
    private async Task SendGetGemAsync(GameRoom room, GameReport report, CommandContext ctx)
    {



        foreach (var user in report.Users.Where(u => u != null).Cast<GameReport.UserResult>())
        {
            var player = _session.GetByMember(user.MemberNo);
            if (player == null || string.IsNullOrEmpty(player.ConnectionId)) continue;

            int order = user.Ranking - 1;
            int gemCount = CalcGemCountToGet(room, order, player);
            if (gemCount <= 0) continue;

            user.GemCount = gemCount;


            int preGemCount = player.GemCount;
            player.GemCount += gemCount;

            try
            {
                await _historyRepo.InsertGameMoneyHistAsync(
                    player.MemberNo, GameConst.EvtCodeDragonGem,
                    gemCount, preGemCount, player.GemCount, player.IpAddress);
            }
            catch { }

            // ─── S→C 通知: commandGetGem (mjkc22e) ──────────────────────

            await ctx.Clients.Client(player.ConnectionId)
                .SendAsync(Cmd.GetGem, new Dictionary<string, object>
                {
                    [GKey.Count] = gemCount,   // G::keyCount (k25e)
                });



            await _playerRepo.SetDailyMissionDirectAsync(player.MemberNo, conditionType: 5, progressIncrement: 1);
        }
    }

    private static int CalcGemCountToGet(GameRoom room, int order, MajakPlayer player)
    {
        if (order != 0 && order != 1) return 0;

        int[] gemTbl = { 0, 2, 20 };
        int gemGame = (room.RoomOption.Length > 13 && room.RoomOption[13] >= '0' && room.RoomOption[13] <= '2')
            ? (room.RoomOption[13] - '0') : 0;

        int gemCount = 0;
        bool canPlayGemGame = room.SubId.Length >= 1 && room.SubId[0] == 'T';
        if (canPlayGemGame)
        {
            gemCount = gemTbl[gemGame];
        }
        else
        {
            gemCount = GetGemCountBySubId(room.SubId, order);
            if (gemCount > 0)
            {
                int ratio = 1;
                if (HasValidMajItem(player, "MJ21")) ratio = 2;
                if (HasValidMajItem(player, "MJ22")) ratio = ratio == 1 ? 3 : 4;
                gemCount *= ratio;
            }
        }

        if (order == 0 && player.IsNetCafeIp)
        {
            if (!canPlayGemGame && gemCount > 0)
                gemCount += gemTbl[1];
            else if (canPlayGemGame && gemGame == 0)
                gemCount = gemTbl[1];
        }

        return gemCount;
    }

    private static bool HasValidMajItem(MajakPlayer player, string itemCode)
    {
        return player.MajItems.Any(i => i.ItemCode == itemCode && i.EndDt >= DateTime.Now);
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>


    ///   1. 全チャンネルプレイヤーの EARNEDMONEY += YAKUMANBONUS_MONEY(200) めEDB 更新



    /// </summary>
    private async Task SendYakumanBonusAsync(GameRoom room, MajakPlayer horaPlayer,
        string yakuName, CommandContext ctx)
    {


        var allPlayers = _session.GetAllChannelPlayers(room.ChannelId).ToList();
        bool updated = await _playerRepo.UpdateEarnedMoneyByYakumanBonusAsync(allPlayers.Select(p => p.MemberNo));
        if (!updated) return;

        foreach (var p in allPlayers)
            p.EarnedMoney += GameConst.YakumanBonusMoney;

        // S→C: commandYakumanBonus をチャンネル全員へブロードキャスチE

        //        + G::keyMemberNo / G::keyGamMoney / MAJ::keyYakuName
        await ctx.Clients.Group($"chanel_{room.ChannelId}")
            .SendAsync(Cmd.YakumanBonus, new Dictionary<string, object>
            {
                [GKey.Pix] = horaPlayer.Pix,   // G::keyMemberNo (k3e)
                [GKey.GamMoney] = GameConst.YakumanBonusMoney, // G::keyGamMoney (k34e) = 200
                [Key.YakuName]  = yakuName,               // MAJ::keyYakuName (mjkk62e)
            });
    }


    private static void PrepareTrainingNpcProfiles(GameRoom room)
    {
        Array.Clear(room.TrainingNpcProfiles);
        if (!room.IsTrainingChannel) return;

        var avatarIndexes = Enumerable.Range(0, 32).ToList();
        int npcNumber = 1;
        for (int playerPos = 0; playerPos < room.Seats.Length; playerPos++)
        {
            if (room.Seats[playerPos] != null) continue;
            int selectedIndex = Random.Shared.Next(avatarIndexes.Count);
            int avatarIndex = avatarIndexes[selectedIndex];
            avatarIndexes.RemoveAt(selectedIndex);
            bool female = avatarIndex >= 16;
            int characterNumber = avatarIndex % 16 + 1;
            room.TrainingNpcProfiles[playerPos] = new TrainingNpcProfile(
                $"NPC {npcNumber++}",
                $"thumbnail_{characterNumber:00}{(female ? 'f' : 'm')}.png",
                female ? "female" : "male");
        }
    }

    private static object? BuildHanchanMemberInfo(GameRoom room, int order)
    {
        int playerPos = room.Engine.HanchanInfo.Player[order];
        var player = playerPos >= 0 && playerPos < room.Seats.Length ? room.Seats[playerPos] : null;
        if (player == null)
        {
            var npc = room.IsTrainingChannel && playerPos >= 0 && playerPos < room.TrainingNpcProfiles.Length
                ? room.TrainingNpcProfiles[playerPos]
                : null;
            return npc == null ? null : new
            {
                memberNo = string.Empty,
                pix = string.Empty,
                name = npc.Name,
                avatarId = npc.AvatarId,
                sex = npc.Sex,
                playerPos,
                seatPos = playerPos,
                engineOrder = order,
                isNpc = true,
            };
        }

        int costumeId = player.GetCustomEquip(30);
        int costumeType = player.CustomItems.TryGetValue(costumeId, out var customItem) ? customItem.Kind : 0;
        return new
        {
            memberNo = player.Pix,
            pix = player.Pix,
            name = player.NickName,
            avatarId = player.AvatarId,
            sex = player.Sex,
            playerPos,
            seatPos = playerPos,
            engineOrder = order,
            isProxy = player.IsOutPlayer,
            mjkk46e = player.TrickTitle,
            trickTitle = player.TrickTitle,
            mjkk47e = player.MajakTitle,
            majakTitle = player.MajakTitle,
            mjkk54e = player.GetRichiEffect(),
            richiEffect = player.GetRichiEffect(),
            mjkk136e = costumeId,
            customCostume = costumeId,
            mjkk137e = costumeType,
            customCostumeType = costumeType,
            skillCnt = player.ActiveRecord.MatchCnt,
            skillCount = player.ActiveRecord.MatchCnt,
        };
    }

    private static object BuildHanchanInfo(GameRoom room)
    {
        var hi = room.Engine.HanchanInfo;
        return new
        {
            playType = "MJPID_INIHAN",
            chicha   = hi.Chicha,
            players  = hi.Player,
            memberInfo = Enumerable.Range(0, GameConst.PlayerMaxCount)
                .Select(order => BuildHanchanMemberInfo(room, order))
                .ToArray(),
        };
    }



    /// </summary>
    private static Engine.RuleInfo BuildRuleInfo(GameRoom room)
    {
        var opt = room.RoomOption;
        char SubId2 = room.SubId.Length >= 3 ? room.SubId[2] : '0';
        char SubId3 = room.SubId.Length >= 4 ? room.SubId[3] : '5';

        Engine.RuleInfo rule;
        if (SubId2 == 'G')  // Grade
        {
            rule = new Engine.RuleInfo
            {
                Hanchan  = (SubId3 == '7'),
                Kuitan   = true,
                AkaDora  = 2,
                Yakitori = false,
                Wareme   = false,
                Nagashi  = true,
                Tip      = false,
                Ron      = 1,
                Uma      = 1,
                Contest  = 0,
                GradeGame = true,
            };
        }
        else if (SubId2 == 'R') // Rated
        {
            rule = new Engine.RuleInfo
            {
                Hanchan  = true,
                Kuitan   = true,
                AkaDora  = 0,
                Yakitori = false,
                Wareme   = false,
                Nagashi  = false,
                Tip      = false,
                Ron      = 0,
                Uma      = SubId3 == '0' ? 2 : 1,
                Contest  = SubId3 - '0' + 1,
            };
        }
        else
        {
            // Normal / Cup / Training / Tournament  Euse RoomOption string
            rule = new Engine.RuleInfo
            {
                Hanchan  = opt.Length > 0  && opt[0]  == '1',
                Kuitan   = opt.Length > 3  && opt[3]  == '0',
                Yakitori = opt.Length > 4  && opt[4]  == '1',
                Tip      = opt.Length > 11 && opt[11] == '1',
                Ron      = Math.Min(opt.Length > 12 ? opt[12] - '0' : 0, 2),
                Uma      = Math.Min(opt.Length > 1  ? opt[1]  - '0' : 0, 3),
                Wareme   = opt.Length > 10 && opt[10] == '1',
                Nagashi  = true,
                AkaDora  = Math.Min(opt.Length > 5  ? opt[5]  - '0' : 0, 2),
                Contest  = 0,
            };
        }
        return rule;
    }

    /// <summary>


    /// </summary>
    private async Task<object?> SendPaiInfoToAllAsync(GameRoom room, CommandContext ctx, bool isInit)
    {
        bool effectiveIsInit = isInit || room.Engine.GameStatus == Engine.GameStatus.NewKyoku;
        _log?.LogDebug("SendPaiInfoToAll begin. roomId={RoomId} isInit={IsInit}", room.RoomId, effectiveIsInit);

        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            int playerPos = room.Engine.HanchanInfo.Player[order];
            if (playerPos < 0 || playerPos >= room.Seats.Length)
            {
                _log?.LogWarning("SendPaiInfoToAll skipped: invalid playerPos. roomId={RoomId} order={Order} playerPos={PlayerPos} isInit={IsInit}", room.RoomId, order, playerPos, effectiveIsInit);
                continue;
            }
            var p = room.Seats[playerPos];
            if (p == null || p.IsViewer)
            {
                if (p == null && room.IsTrainingChannel)
                    _log?.LogDebug("SendPaiInfoToAll skipped expected training NPC. roomId={RoomId} order={Order} playerPos={PlayerPos} isInit={IsInit}", room.RoomId, order, playerPos, effectiveIsInit);
                else
                    _log?.LogWarning("SendPaiInfoToAll skipped: player missing/viewer. roomId={RoomId} order={Order} playerPos={PlayerPos} hasPlayer={HasPlayer} isViewer={IsViewer} isInit={IsInit}", room.RoomId, order, playerPos, p != null, p?.IsViewer ?? false, effectiveIsInit);
                continue;
            }

            int seatOrder  = order;
            int openMask   = 1 << seatOrder;
            int skipMask   = openMask;


            if (room.IsTrainingChannel || room.IsTournamentChannel) openMask = (1 << (MajakConst.PlayerMaxCount + 1)) - 1;

            var buf = Engine.BipaiInfo.Create();
            room.Engine.GetBipai(ref buf, openMask, skipMask);
            _log?.LogDebug("SendPaiInfoToAll player buffer. roomId={RoomId} order={Order} playerPos={PlayerPos} memberNo={MemberNo} connectionId={ConnectionId} openMask={OpenMask} skipMask={SkipMask} paiCount={PaiCount} isInit={IsInit}",
                room.RoomId,
                order,
                playerPos,
                p.MemberNo,
                p.ConnectionId,
                openMask,
                skipMask,
                buf.PaiCnt,
                effectiveIsInit);

            if (buf.PaiCnt > 0)
            {
                await ctx.Clients.Client(p.ConnectionId)
                    .SendAsync(Cmd.PaiInfoList, new
                    {
                        bInit    = effectiveIsInit,
                        openPos  = seatOrder,
                        paiCount = buf.PaiCnt,
                        pai      = buf.Pai.Take(buf.PaiCnt)
                                  .Select(pc => new { code = pc.Code, idx = pc.BipaiIndex, red = pc.IsRed })
                                          .ToArray(),
                    });
            }
        }


        int vOpenMask = 1 << MajakConst.PlayerMaxCount;
        if (room.RoomOption.Length > 6 && room.RoomOption[6] == '1')
            vOpenMask = (1 << (MajakConst.PlayerMaxCount + 1)) - 1;
        var vBuf = Engine.BipaiInfo.Create();
        room.Engine.GetBipai(ref vBuf, vOpenMask, 1 << MajakConst.PlayerMaxCount);
        object? viewerPayload = null;
        if (vBuf.PaiCnt > 0)
        {
            viewerPayload = new
            {
                bInit    = effectiveIsInit,
                openPos  = MajakConst.PlayerMaxCount,
                paiCount = vBuf.PaiCnt,
                pai      = vBuf.Pai.Take(vBuf.PaiCnt)
                                   .Select(pc => new { code = pc.Code, idx = pc.BipaiIndex, red = pc.IsRed })
                                   .ToArray(),
            };
            foreach (var viewer in room.Viewers)
            {
                await ctx.Clients.Client(viewer.ConnectionId)
                    .SendAsync(Cmd.PaiInfoList, viewerPayload);
            }
        }
        return viewerPayload;
    }

    public async Task SendPaiInfoAsync(GameRoom room, CommandContext ctx, MajakPlayer player, bool isInit, bool includeAll)
    {
        int openPos = player.IsViewer ? MajakConst.PlayerMaxCount : player.EngineOrder;
        if (!player.IsViewer && (openPos < 0 || openPos >= MajakConst.PlayerMaxCount))
        {
            int seatPos = (int)player.SeatPos;
            openPos = seatPos >= 0 && seatPos < room.SeatToEngineOrder.Length
                ? room.SeatToEngineOrder[seatPos]
                : MajakConst.PlayerMaxCount;
            if (openPos < 0 || openPos >= MajakConst.PlayerMaxCount)
            {
                _log?.LogWarning("SendPaiInfo skipped: engine order not resolved. roomId={RoomId} memberNo={MemberNo} seatPos={SeatPos} engineOrder={EngineOrder}",
                    room.RoomId,
                    player.MemberNo,
                    player.SeatPos,
                    player.EngineOrder);
                return;
            }
        }
        int openMask = 1 << openPos;
        int skipMask = includeAll ? 0 : openMask;

        if (openPos < MajakConst.PlayerMaxCount)
        {
            if (room.IsTrainingChannel || room.IsTournamentChannel)
                openMask = (1 << (MajakConst.PlayerMaxCount + 1)) - 1;
        }
        else if (room.RoomOption.Length > 6 && room.RoomOption[6] == '1')
        {
            openMask = (1 << (MajakConst.PlayerMaxCount + 1)) - 1;
        }

        var buf = Engine.BipaiInfo.Create();
        room.Engine.GetBipai(ref buf, openMask, skipMask);
        if (buf.PaiCnt <= 0) return;

        await ctx.Clients.Client(player.ConnectionId)
            .SendAsync(Cmd.PaiInfoList, new
            {
                bInit    = isInit,
                openPos,
                resyncSnapshot = includeAll && openPos < MajakConst.PlayerMaxCount,
                currentHand = includeAll && openPos < MajakConst.PlayerMaxCount
                    ? room.Engine.Player[openPos].Tehai
                        .Select(pc => new { code = pc.Code, idx = pc.BipaiIndex, red = pc.IsRed })
                        .ToArray()
                    : Array.Empty<object>(),
                paiCount = buf.PaiCnt,
                pai      = buf.Pai.Take(buf.PaiCnt)
                          .Select(pc => new { code = pc.Code, idx = pc.BipaiIndex, red = pc.IsRed })
                                  .ToArray(),
            });
    }


    private static IReadOnlyDictionary<string, object?> BuildGameReportFailurePayload(string reason)
        => new Dictionary<string, object?>
        {
            [GKey.Count] = 0,
            [GKey.Result] = GKey.ValueFailure,
            ["result"] = 0,
            ["playerCnt"] = 0,
            ["reason"] = reason,
        };

    private Dictionary<string, object?> BuildGameResultPayload(GameRoom room, GameReport report)
    {
        var payload = new Dictionary<string, object?>
        {
            [GKey.Count] = room.PlayerCount,
            [GKey.Result] = GKey.ValueSuccess,
            [GKey.GameId] = GameConst.ServiceId,
            [GKey.TotalMember] = room.PlayerCount,
            [GKey.SubId] = room.SubId,
            [GKey.ChannelId] = room.ChannelId,
            [GKey.RoomId] = room.RoomId,
            [GKey.ReportingType] = GKey.ValueReportingGamble,
            [GKey.IsForDisconn] = false,

            // Web client compatibility fields.
            ["result"] = 1,
            ["playerCnt"] = room.PlayerCount,
            ["gameId"] = GameConst.ServiceId,
            ["hasTor"] = room.Engine.Rule.Yakitori,
            ["hasTip"] = room.Engine.Rule.Tip,
            ["isTournament"] = room.IsTournamentChannel,
            ["gameEnd"] = room.Engine.GameEnd.ToString(),
            ["gameEndValue"] = (int)room.Engine.GameEnd,
            ["isHanchanRule"] = room.Engine.Rule.Hanchan,
        };

        var users = Enumerable.Range(0, GameConst.PlayerMaxCount)
            .Where(i => report.Users[i] != null)
            .Select((i, seq) =>
            {
                var u = report.Users[i]!;
                var p = _session.GetByMember(u.MemberNo);
                var seat = room.Seats[i];
                var npc = seat == null && room.IsTrainingChannel ? room.TrainingNpcProfiles[i] : null;
                var avatarPlayer = seat ?? p;
                int setBal = u.SetPoint + u.SetUma + u.SetTor + u.SetTip;
                long currentMoney = p?.GamMoney ?? (u.PrevMoney + u.MoneyChange);
                int costumeId = avatarPlayer?.GetCustomEquip(30) ?? 0;
                int costumeType = costumeId != 0 && avatarPlayer?.CustomItems.TryGetValue(costumeId, out var customItem) == true
                    ? customItem.Kind
                    : 0;
                int matchCnt = p?.ActiveRecord.MatchCnt ?? u.MatchCnt;
                int winCnt = p?.ActiveRecord.WinCnt ?? u.WinCnt;
                int defeatCnt = p?.ActiveRecord.DefeatCnt ?? u.DefeatCnt;
                int drawCnt = p?.ActiveRecord.DrawCnt ?? u.DrawCnt;
                int rating = p?.ActiveRecord.Rating ?? u.Rating;
                int nLevel = p?.NLevel ?? 0;
                string sLevel = p?.SLevel ?? "";
                int experience = p?.Experience ?? u.Experience;
                int gemCount = p?.GemCount ?? u.GemCount;
                long roomCharge = p?.RoomCharge ?? 0;
                long feeWinner = p?.FeeWinner ?? 0;
                string usedItemCode = p?.UsedBadaiFreeItem ?? "";
                bool usedChance = p?.ReserveChanceItem == true;
                int[] exScores = BuildGameResultExScores(room, p, rating, matchCnt);

                payload[$"{GKey.UsedChance}{seq}"] = usedChance;
                payload[$"{GKey.Pix}{seq}"] = seat?.Pix ?? string.Empty;
                payload[$"{GKey.MatchCnt}{seq}"] = matchCnt;
                payload[$"{GKey.WinCnt}{seq}"] = winCnt;
                payload[$"{GKey.DefeatCnt}{seq}"] = defeatCnt;
                payload[$"{GKey.DrawCnt}{seq}"] = drawCnt;
                payload[$"{GKey.Rating}{seq}"] = rating;
                payload[$"{GKey.GamRanking}{seq}"] = u.Ranking;
                payload[$"{GKey.SLevel}{seq}"] = sLevel;
                payload[$"{GKey.NLevel}{seq}"] = nLevel;
                payload[$"{GKey.DayCnt}{seq}"] = 0;
                payload[$"{GKey.ReservedString}{seq}"] = "";
                payload[$"{GKey.GamMoney}{seq}"] = currentMoney;
                payload[$"{GKey.TotMoney}{seq}"] = currentMoney;
                payload[$"{GKey.MoneyRanking}{seq}"] = 0;
                payload[$"{GKey.ExScoreCnt}{seq}"] = exScores.Length;
                for (int exIndex = 0; exIndex < exScores.Length; exIndex++)
                    payload[$"{GKey.ExScoreValue}{seq}_{exIndex}"] = exScores[exIndex];
                payload[$"{Key.Experience}{seq}"] = experience;
                payload[$"{Key.RoomCharge}{seq}"] = roomCharge;
                payload[$"{Key.WinMoneyCut}{seq}"] = 0;
                payload[$"{Key.FeeWinner}{seq}"] = feeWinner;
                payload[$"{Key.ItemCode}{seq}"] = usedItemCode;
                payload[$"{Key.GradeGetPoint}{seq}"] = u.GradeAddPoint;
                payload[$"{Key.GradeCurrPoint}{seq}"] = u.GradePointTmp;
                payload[$"{Key.GradeNextPoint}{seq}"] = u.GradeNextPoint;
                payload[$"{Key.GradeGetRating}{seq}"] = u.RatingChange;
                payload[$"{Key.GradePrevLevel}{seq}"] = u.PrevGradeLevel;
                payload[$"{Key.GradeCurrLevel}{seq}"] = u.GradeLevel;
                payload[$"{Key.GradeUpDown}{seq}"] = u.GradeUpDown;
                payload[$"{Key.GradeBeginner}{seq}"] = u.UpdateBeginner ? 1 : 0;
                payload[$"{Key.GradeExtraStage}{seq}"] = u.UpdateExtra ? 1 : 0;
                payload[$"{Key.GemCount}{seq}"] = gemCount;

                return new
                {
                    seq,
                    seatPos      = i,
                    memberNo     = seat?.Pix ?? (npc != null ? TournamentConst.NpcMemberNo : string.Empty),
                    pix          = seat?.Pix ?? (npc != null ? TournamentConst.NpcMemberNo : string.Empty),
                    name         = seat?.NickName ?? npc?.Name ?? string.Empty,
                    avatarId     = seat?.AvatarId ?? npc?.AvatarId ?? string.Empty,
                    sex          = avatarPlayer?.Sex ?? npc?.Sex ?? string.Empty,
                    charaId      = costumeId,
                    customCostume = costumeId,
                    customCostumeType = costumeType,
                    ranking      = u.Ranking,
                    rank         = Math.Max(0, u.Ranking - 1),
                    point        = u.GameScore,
                    setBal,
                    setTen       = u.SetPoint,
                    setUma       = u.SetUma,
                    setTor       = u.SetTor,
                    setTip       = u.SetTip,
                    rating,
                    ratingChange = u.RatingChange,
                    matchCnt,
                    winCnt,
                    defeatCnt,
                    drawCnt,
                    prevNlevel   = u.PrevNLevel,
                    nlevel       = nLevel,
                    slevel       = sLevel,
                    gammoney     = currentMoney,
                    moneyChange  = u.MoneyChange,
                    coinGain     = Math.Max(0, (long)setBal * room.MoneyRate),
                    coinNeed     = GetNextLevelMoneyNeed(p?.NLevel ?? 0, currentMoney),
                    dealerFee    = u.DealerFee,
                    gemCount,
                    experience,
                    expGain      = u.ExperienceGain,
                    usedChance,
                    horaCnt      = u.HoraCnt,
                    horaPoint    = u.HoraPoint,
                    hojuCnt      = u.HojuCnt,
                    richiCnt     = u.RichiCnt,
                    furoCnt      = u.FuroCnt,
                    doraCnt      = u.DoraCnt,
                    richiHoraCnt = u.RichiHoraCnt,

                    prevGradeLevel  = u.PrevGradeLevel,
                    gradeLevel      = u.GradeLevel,
                    prevGradePoint  = u.PrevGradePoint,
                    gradePoint      = u.GradePoint,
                    gradeAddPoint   = u.GradeAddPoint,
                    gradeNextPoint  = u.GradeNextPoint,
                    gradeUpDown     = u.GradeUpDown,
                    updateBeginner  = u.UpdateBeginner ? 1 : 0,
                };
            })
            .ToList();

        payload["users"] = users;
        return payload;
    }

    private static int[] BuildGameResultExScores(GameRoom room, MajakPlayer? player, int rating, int matchCnt)
    {
        if (player == null) return Array.Empty<int>();
        if (room.IsCupChannel)
            return new[] { rating, matchCnt, player.CupRec.CupPoint, player.CupRec.CupMatchCnt };
        return new[] { rating, matchCnt };
    }

    private static long GetNextLevelMoneyNeed(int level, long gamMoney)
    {
        ReadOnlySpan<long> tbl = stackalloc long[] { 1, 500, 1500, 3000, 10000, 30000, 100000, 500000, 1000000, 5000000 };
        if (level < 0 || level >= tbl.Length) return 0;
        return tbl[level] - gamMoney;
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// アバターギア通知をチャンネル全員へ送信、E

    ///   _type=1(YAKUMAN): MAJ::keyMemorialShop bit0 = 1
    ///   _type=2(RICHIIPPATSUTSUMO): MAJ::keyMemorialShop bit1 = 2
    ///   コマンチE MAJ::commandAvatarGear = "mjkc16e"
    /// </summary>
    private async Task SendAvatarGearAsync(GameRoom room, CommandContext ctx,
        MajakPlayer player, int avatarGearType)
    {
        // MemorialShop ビットフィールチE (1 << (type-1))

        int memorialShopBit = 1 << (avatarGearType - 1);



        bool dbOk = false;
        try
        {
            await _playerRepo.UpsertShopListAsync(player.MemberNo, avatarGearType);
            dbOk = true;
        }
        catch { /* Continue packet delivery even if DB persistence fails. */ }


        player.MemorialShop |= memorialShopBit;




        var packet = new Dictionary<string, object>
        {
            [Key.MemorialShop] = dbOk ? (object)memorialShopBit : 0,
        };


        // .NET では送信先クライアントが公閁EID で対象を識別できるよう memberNo を付与する、E
        packet["memberNo"] = player.Pix;
        packet["pix"] = player.Pix;

        await ctx.Clients.Group($"chanel_{room.ChannelId}")
            .SendAsync(Cmd.AvatarGear, packet);
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>




    /// </summary>
    private async Task CheckTitleClearAsync(MajakPlayer player, CommandContext ctx)
    {
        var r  = player.RegularRecord;
        var hi = player.HiClassRecord;
        var yr = player.YakuCount;  // 0-27 通常役カウンチE
        var ym = player.YmanCount;  // 0-14 役満カウンチE

        int RegularSum(Func<Models.Player.RatingRecord, int> selector) => selector(r) + selector(hi);
        int RegularAvr(Func<Models.Player.RatingRecord, int> value, Func<Models.Player.RatingRecord, int> count) =>
            RegularSum(value) / RegularSum(count);
        double RegularPer(Func<Models.Player.RatingRecord, int> value, Func<Models.Player.RatingRecord, int> count) =>
            RegularSum(value) * 100.0 / RegularSum(count);
        int HiAvr(Func<Models.Player.RatingRecord, int> value, Func<Models.Player.RatingRecord, int> count) => selectorSafe(value, count);
        int selectorSafe(Func<Models.Player.RatingRecord, int> value, Func<Models.Player.RatingRecord, int> count) => value(hi) / count(hi);
        double HiPer(Func<Models.Player.RatingRecord, int> value, Func<Models.Player.RatingRecord, int> count) =>
            value(hi) * 100.0 / count(hi);
        int HoraYakuCnt(int yaku) => yaku >= 100 ? ym[yaku - 100] : yr[yaku];
        static int TrickCode(int atr, int lev) => atr * 3 + lev;
        static string TitleId(int type, int code) => type == 1 && code >= 1000
            ? $"mjkc{code - 1000:000}"
            : $"mjk{(type == 0 ? 's' : 't')}{code:000}";

        var titlesToAdd = new List<(int Type, int Code, string TitleId)>();

        // ── ATR_N (奥義) ─────────────────────────────────────────
        if (player.TrickLevel[0] == 0)
        {
            if (RegularSum(x => x.MatchCnt) >= 30 && RegularSum(x => x.WinCnt) >= 10
                && RegularSum(x => x.TobashiCnt) >= 2 && RegularSum(x => x.DoraCnt) >= 100)
                AddTrickTitle(0, 1);
        }
        else if (player.TrickLevel[0] == 1)
        {
            if (RegularSum(x => x.MatchCnt) >= 200 && RegularSum(x => x.WinCnt) >= 100
                && RegularSum(x => x.DefeatCnt) >= 100 && RegularSum(x => x.TobashiCnt) >= 25
                && RegularSum(x => x.DoraCnt) >= 1000 && RegularSum(x => x.UraDoraCnt) >= 500)
                AddTrickTitle(0, 2);
        }

        // ── ATR_F (風) ──────────────────────────────────────────
        if (player.TrickLevel[1] == 0)
        {
            if (RegularSum(x => x.MatchCnt) >= 120 && RegularSum(x => x.Grade1) >= 50
                && RegularSum(x => x.TobashiCnt) >= 15 && RegularSum(x => x.HoraCnt) > 0
                && RegularAvr(x => x.HoraPoint, x => x.HoraCnt) >= 6000)
                AddTrickTitle(1, 1);
        }
        else if (player.TrickLevel[1] == 1)
        {
            if (hi.MatchCnt >= 600 && HoraYakuCnt((int)HoraYaku.Tsumo) >= 100
                && HoraYakuCnt((int)HoraYaku.Richi) >= 200 && HoraYakuCnt((int)HoraYaku.Ippatsu) >= 50
                && HoraYakuCnt((int)HoraYaku.Wrichi) >= 3 && hi.HoraCnt > 0
                && HiAvr(x => x.HoraPoint, x => x.HoraCnt) >= 6200)
                AddTrickTitle(1, 2);
        }

        // ── ATR_W (水) ──────────────────────────────────────────
        if (player.TrickLevel[2] == 0)
        {
            if (RegularSum(x => x.MatchCnt) >= 50 && RegularSum(x => x.RichiCnt) >= 150
                && RegularSum(x => x.WinCnt) >= 40 && RegularSum(x => x.KyokuCnt) > 0
                && RegularPer(x => x.RichiCnt, x => x.KyokuCnt) >= 15)
                AddTrickTitle(2, 1);
        }
        else if (player.TrickLevel[2] == 1)
        {
            if (hi.MatchCnt >= 400 && HoraYakuCnt((int)HoraYaku.Chitoitsu) >= 100
                && HoraYakuCnt((int)HoraYaku.Tanyao) >= 300 && HoraYakuCnt((int)HoraYaku.Pinfu) >= 300
                && HoraYakuCnt((int)HoraYaku.Yakuhai) >= 500 && hi.KyokuCnt > 0
                && HiPer(x => x.RichiCnt, x => x.KyokuCnt) >= 17)
                AddTrickTitle(2, 2);
        }

        // ── ATR_E (火) ──────────────────────────────────────────
        if (player.TrickLevel[3] == 0)
        {
            if (RegularSum(x => x.MatchCnt) >= 100 && RegularSum(x => x.HojuCnt) <= 500
                && RegularSum(x => x.TobiCnt) <= 30 && RegularSum(x => x.KyokuCnt) > 0
                && RegularAvr(x => x.HojuCnt, x => x.KyokuCnt) <= 18)
                AddTrickTitle(3, 1);
        }
        else if (player.TrickLevel[3] == 1)
        {
            if (hi.MatchCnt >= 500 && HoraYakuCnt((int)HoraYaku.Sanankou) >= 12
                && HoraYakuCnt((int)HoraYaku.Toitoi) >= 70 && HoraYakuCnt((int)HoraYaku.Shosangen) >= 3
                && HoraYakuCnt((int)HoraYaku.Sanshokudoukou) >= 1 && hi.KyokuCnt > 0
                && HiPer(x => x.HojuCnt, x => x.KyokuCnt) <= 17)
                AddTrickTitle(3, 2);
        }

        // ── ATR_A (地) ──────────────────────────────────────────
        if (player.TrickLevel[4] == 0)
        {
            if (RegularSum(x => x.MatchCnt) >= 70 && RegularSum(x => x.UraDoraCnt) >= 130
                && RegularSum(x => x.HoraCnt) >= 250 && RegularPer(x => x.DoraCnt, x => x.HoraCnt) >= 120)
                AddTrickTitle(4, 1);
        }
        else if (player.TrickLevel[4] == 1)
        {
            if (hi.MatchCnt >= 300
                && HoraYakuCnt((int)HoraYaku.Rinshan) >= 2
                && HoraYakuCnt((int)HoraYaku.Haitei) >= 2
                && HoraYakuCnt((int)HoraYaku.Houtei) >= 6
                && HoraYakuCnt((int)HoraYaku.Chankan) >= 1
                && hi.HoraCnt > 0 && hi.RichiHoraCnt > 0
                && HiPer(x => x.UraDoraCnt, x => x.RichiHoraCnt) >= 40)
                AddTrickTitle(4, 2);
        }

        // ── 麻雀称号 (TitleClear[] ビッチE ───────────────────────
        if (player.TitleClear[6] == 0 && hi.MatchCnt >= 1) player.TitleClear[6] = 2;
        if (player.TitleClear[7] == 0 && hi.Grade1 >= 2) player.TitleClear[7] = 2;
        if (player.TitleClear[8] == 0 && hi.TobashiCnt >= 1) player.TitleClear[8] = 2;
        if (player.TitleClear[9] == 0 && hi.DoraCnt >= 108) player.TitleClear[9] = 2;
        if (player.TitleClear[10] == 0 && hi.WinCnt >= 100) player.TitleClear[10] = 2;
        if (player.TitleClear[11] == 0 && hi.MatchCnt >= 100) player.TitleClear[11] = 2;
        if (player.TitleClear[12] == 0 && hi.TobashiCnt >= 30) player.TitleClear[12] = 2;
        if (player.TitleClear[13] == 0 && hi.TobiCnt >= 49) player.TitleClear[13] = 2;
        if (player.TitleClear[14] == 0 && ym.Sum() >= 3) player.TitleClear[14] = 2;
        if (player.TitleClear[15] == 0 && hi.MatchCnt >= 300)
        {
            int gradeSum = hi.Grade1 * 10 + hi.Grade2 * 20 + hi.Grade3 * 30 + hi.Grade4 * 40;
            int matchSum = hi.Grade1 + hi.Grade2 + hi.Grade3 + hi.Grade4;
            if (gradeSum <= matchSum * 24) player.TitleClear[15] = 2;
        }
        if (player.TitleClear[16] == 0 && hi.MatchCnt >= 150 && hi.HoraCnt > 0
            && HiAvr(x => x.HoraPoint, x => x.HoraCnt) >= 6700) player.TitleClear[16] = 2;
        if (player.TitleClear[17] == 0 && hi.MatchCnt >= 700 && hi.KyokuCnt > 0
            && HiPer(x => x.HojuCnt, x => x.KyokuCnt) <= 13) player.TitleClear[17] = 2;
        if (player.TitleClear[18] == 0 && hi.MatchCnt >= 600 && hi.KyokuCnt > 0
            && HiPer(x => x.FuroCnt, x => x.KyokuCnt) >= 45) player.TitleClear[18] = 2;
        if (player.TitleClear[19] == 0 && hi.MatchCnt >= 500 && hi.KyokuCnt > 0
            && HiPer(x => x.FuroCnt, x => x.KyokuCnt) <= 20) player.TitleClear[19] = 2;
        if (player.TitleClear[20] == 0 && hi.MatchCnt >= 400 && hi.HojuCnt > 0
            && HiAvr(x => x.HojuPoint, x => x.HojuCnt) >= -5900) player.TitleClear[20] = 2;
        if (player.TitleClear[21] == 0 && hi.MatchCnt >= 200
            && HiPer(x => x.TobiCnt, x => x.MatchCnt) <= 4) player.TitleClear[21] = 2;
        if (player.TitleClear[28] == 0 && yr.Take(27).Count(x => x != 0) >= 24) player.TitleClear[28] = 2;
        if (player.TitleClear[29] == 0 && hi.MatchCnt >= 300
            && HiPer(x => x.Grade1, x => x.MatchCnt) >= 28) player.TitleClear[29] = 2;
        if (player.TitleClear[30] == 0 && hi.MatchCnt >= 500 && hi.KyokuCnt > 0
            && HiPer(x => x.RichiCnt, x => x.KyokuCnt) <= 15
            && HoraYakuCnt((int)HoraYaku.Tsumo) * 100 >= hi.HoraCnt * 20) player.TitleClear[30] = 2;
        if (player.TitleClear[31] == 0 && player.H_ContTopMax >= 7) player.TitleClear[31] = 2;

        for (int i = 1; i < player.TitleClear.Length; i++)
        {
            if (player.TitleClear[i] == 2)
            {
                int code = i;
                titlesToAdd.Add((1, code, TitleId(1, code)));
            }
        }

        if (titlesToAdd.Count == 0) return;

        try
        {
            await _playerRepo.InsertOrEnableTitlesAsync(player.MemberNo, titlesToAdd.Select(x => x.TitleId));
        }
        catch
        {
            return;
        }

        foreach (var title in titlesToAdd)
        {
            if (title.Type == 0)
                player.TrickLevel[title.Code / 3] = title.Code % 3;
            else
                player.TitleClear[title.Code] = 1;
        }

        // ─── commandGetTitle S→C 通知 ─────────────────────────────

        //   サービス: G::serviceRoom / コマンチE MAJ::commandGetTitle = "mjkc19e"
        //   フィールチE keyTitleType{N}(mjkk48e0...), keyTitleCode{N}(mjkk49e0...),
        //              keyTitleName{N}(mjkk50e0...), count
        var packet = new Dictionary<string, object>();
        int cnt = 0;
        foreach (var title in titlesToAdd)
        {
            packet[Key.TitleType + cnt] = title.Type;
            packet[Key.TitleCode + cnt] = title.Code;
            packet[Key.TitleName + cnt] = _titleService.GetTitleName(title.TitleId) ?? "";
            cnt++;
        }
        packet[GKey.Count] = cnt;

        await ctx.Clients.Client(player.ConnectionId)
            .SendAsync(Cmd.GetTitle, packet);

        void AddTrickTitle(int atr, int lev)
        {
            int code = TrickCode(atr, lev);
            titlesToAdd.Add((0, code, TitleId(0, code)));
        }
    }

    private static int ToLegacyTitleCode(string titleId)
    {
        if (titleId.StartsWith("mjks") && int.TryParse(titleId[4..], out int trickCode))
            return trickCode;
        if (titleId.StartsWith("mjkt") && int.TryParse(titleId[4..], out int majakCode))
            return majakCode;
        if (titleId.StartsWith("mjkc") && int.TryParse(titleId[4..], out int customCode))
            return customCode + 1000;
        return 0;
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// チャンネル種別に応じた場代を返す、E

    ///   _MODIFY_BADAI 定義晁E(現行ビルチE:


    /// </summary>
    private long GetRoomChargeCommon(GameRoom room)
    {


        //   citor = m_baDaiMap.find(m_pChnlInfo->m_szSubId);
        //   if (citor == m_baDaiMap.end()) return 0;
        //   return citor->second;
        if (s_baDaiMap.TryGetValue(room.SubId, out int badai))
            return badai;
        return 0L;
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>




    /// </summary>
    private static long GetFeeWinner(GameRoom room, long winMoney)
    {
        if (room.IsGradeChannel && room.SubId.Length > 4 && room.SubId[4] == 'A')
            return (winMoney + 9) / 10;
        return (winMoney + 49) / 50;
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// ゲーム開始時の所持コイン上限 (オールイン上限)、E

    ///   if(pRoomInfo->m_nUnitMoney &lt; UNITMONEY_STARTLIMIT) return 1;
    ///   return pRoomInfo->m_nUnitMoney * pRoomInfo->GetGameRate() * 35;

    /// </summary>
    private static long GetStartLimitMoney(GameRoom room)
    {

        const int unitMoneyStartLimit = 10;
        long unitMoney = room.UnitMoney;
        if (room.IsHiEventChannel) return unitMoney * room.GameRate * 35;
        if (unitMoney < unitMoneyStartLimit) return 1;
        return unitMoney * room.GameRate * 35;
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>


    /// </summary>
    private static readonly Dictionary<string, int> s_baDaiMap = new()
    {
        ["00000"] = 500,   // サークル
        ["0075B"] = 500,   // 流れ本場特殁E
        ["0086B"] = 500,   // 流れ本場通常
        ["0083B"] = 500,   // 流れ本場ピンキー特殁E
        ["0082B"] = 500,   // 流れ本場ピンキー通常
        ["0085D"] = 1500,  // 流れミドル
        ["0085F"] = 3000,  // 流れハイ
        ["0ZG6A"] = 500,
        ["0ZG6B"] = 500,   // 段位段位卓(通常)
        ["0ZG6C"] = 500,   // 段位高段位卓(通常)
        ["0ZG6D"] = 500,   // 段位十段位卓(通常)
        ["0ZG7A"] = 500,
        ["0ZG7B"] = 500,   // 段位段位卓(通常)
        ["0ZG7C"] = 500,   // 段位高段位卓(通常)
        ["0ZG7D"] = 500,   // 段位十段位卓(通常)
        ["0ZC5F"] = 0,     // 杯
    };

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// SubId ↁE[1佁Egem 数, 2佁Egem 数] のマッチE(_MODIFY_GEM_COUNT)、E

    /// </summary>
    private static readonly Dictionary<string, int[]> s_gemCountMap = new()
    {
        ["0085D"] = new[] { 2, 1 },    // 流れミドル
        ["0085F"] = new[] { 5, 2 },    // 流れハイ
        ["0ZG6A"] = new[] { 1, 0 },
        ["0ZG6B"] = new[] { 2, 0 },    // 段位段位卓(通常)
        ["0ZG6C"] = new[] { 3, 1 },    // 段位高段位卓(通常)
        ["0ZG6D"] = new[] { 4, 2 },    // 段位十段位卓(通常)
        ["0ZG7A"] = new[] { 1, 0 },
        ["0ZG7B"] = new[] { 2, 0 },    // 段位段位卓(通常)
        ["0ZG7C"] = new[] { 3, 1 },    // 段位高段位卓(通常)
        ["0ZG7D"] = new[] { 4, 2 },    // 段位十段位卓(通常)
    };

    /// <summary>

    /// ranking=0 (1佁E ↁEgems[0], ranking=1 (2佁E ↁEgems[1]
    /// </summary>
    internal static int GetGemCountBySubId(string subId, int ranking)
    {
        if (!s_gemCountMap.TryGetValue(subId, out var gems)) return 0;
        return ranking == 0 ? gems[0] : gems[1];
    }

    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    /// <summary>


    ///

    /// SUM_MIX=2: 高い頁E5 件 + 低い頁E2 件の合訁E(訁E7 件)

    /// </summary>
    internal static void CalcCupEvtScore(CupEvtRecord rec, int newPoint, int sumType)
    {
        const int SUM_MAX = 1;
        const int SUM_MIX = 2;
        const int SUM_SUC = 3;

        switch (sumType)
        {
            case SUM_MAX:
            case SUM_MIX:
            {
                int maxSlots = sumType == SUM_MIX ? 7 : 5;

                if (rec.MatchCnt < maxSlots)
                {

                    int idx = 0;
                    for (idx = 0; idx < rec.MatchCnt; idx++)
                    {
                        if (newPoint > rec.Points[idx])
                        {
                            // idx 以降を 1 つ後ろへずらぁE
                            for (int j = rec.MatchCnt; j > idx; j--)
                                rec.Points[j] = rec.Points[j - 1];
                            break;
                        }
                    }
                    rec.Points[idx] = newPoint;
                }
                else
                {

                    int idx = 0;
                    for (idx = 0; idx < 5; idx++)
                    {
                        if (newPoint > rec.Points[idx])
                        {
                            for (int j = 4; j > idx; j--)
                                rec.Points[j] = rec.Points[j - 1];
                            rec.Points[idx] = newPoint;
                            break;
                        }
                    }
                    // SUM_MIX: 下佁E2 件 (index 5-6) も更新
                    if (sumType == SUM_MIX && idx == 5)
                    {
                        for (idx = 6; idx >= 5; idx--)
                        {
                            if (newPoint < rec.Points[idx])
                            {
                                for (int j = 5; j < idx; j++)
                                    rec.Points[j] = rec.Points[j + 1];
                                rec.Points[idx] = newPoint;
                                break;
                            }
                        }
                    }
                }


                rec.TotalPoint = 0;
                for (int i = 0; i < maxSlots; i++)
                    rec.TotalPoint += rec.Points[i];
                break;
            }

            case SUM_SUC:
            {
                // 末尾からシフトして先頭に新スコアを挿入 (直迁E5 件)
                for (int i = 4; i > 0; i--)
                    rec.Points[i] = rec.Points[i - 1];
                rec.Points[0] = newPoint;

                int sum = 0;
                for (int i = 0; i < 5; i++)
                    sum += rec.Points[i];
                if (sum > rec.TotalPoint)
                    rec.TotalPoint = sum;
                break;
            }
        }

        rec.MatchCnt++;
    }
}

