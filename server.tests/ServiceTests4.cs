using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MajakServer.Commands;
using MajakServer.Engine;
using MajakServer.Hubs;
using MajakServer.Infrastructure;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using System.Reflection;
using System.Text.Json;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// TournamentService マッチング処理テスト
// 原典: PreTournamentMatching / GoTournamentMatching
// ═══════════════════════════════════════════════════════════════════════════
public class TournamentMatchingTests
{
    private readonly Mock<TournamentRepository> _repoMock
        = new(MockBehavior.Loose);
    private readonly Mock<ILogger<TournamentService>> _loggerMock = new();
    private readonly List<(string method, object packet)> _hubSent = new();

    private Mock<IHubContext<MajakGameHub>> BuildHubMock()
    {
        var singleProxy = new Mock<ISingleClientProxy>();
        singleProxy.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
                _hubSent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(singleProxy.Object);

        var hubMock = new Mock<IHubContext<MajakGameHub>>();
        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        return hubMock;
    }

    private TournamentService BuildWithPlan(TournamentPlan plan,
        Dictionary<int, TournamentDetail>? details = null,
        PlayerSessionService? session = null)
    {
        var svc = TestTournamentServiceFactory.Create(_repoMock.Object, _loggerMock.Object, session: session);

        var plans = (System.Collections.Concurrent.ConcurrentDictionary<long, TournamentPlan>)
            typeof(TournamentService)
                .GetField("_plans", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(svc)!;
        plans[plan.SeqNo] = plan;

        if (details != null)
        {
            var detailsMap = (System.Collections.Concurrent.ConcurrentDictionary<long, Dictionary<int, TournamentDetail>>)
                typeof(TournamentService)
                    .GetField("_details", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(svc)!;
            detailsMap[plan.SeqNo] = details;
        }
        return svc;
    }

    private void SetupRepoBulk()
    {
        _repoMock.Setup(r => r.UpdatePlanStatusAsync(It.IsAny<TournamentPlan>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.MergeDetailsAsync(It.IsAny<IEnumerable<TournamentDetail>>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.BulkUpdateJoinStatusAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.InsertUserPresentsAsync(It.IsAny<IEnumerable<UserPresentRecord>>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateDetailResultAsync(It.IsAny<TournamentDetail>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateDetailResultsAsync(It.IsAny<IEnumerable<TournamentDetail>>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdatePlayerNumAsync(It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.MergeJoinAsync(
                It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((true, 1));
        _repoMock.Setup(r => r.MergePlannerManageAsync(It.IsAny<TournamentJoin>()))
            .ReturnsAsync(true);
    }

    private static bool HasPresent(IEnumerable<UserPresentRecord> rows,
        string memberNo, long presentNum, int presentKbn, int presentKind)
    {
        return rows.Any(row => row.MemberNo == memberNo
            && row.PresentNum == presentNum
            && row.PresentKbn == presentKbn
            && row.PresentKind == presentKind);
    }

    // ─── PreMatchingAsync ────────────────────────────────────────────────

    // シナリオ1: MatchStartDt 未到達 → 状態変化なし
    // 原典: clNow < clMatchStartDt → continue
    [Fact]
    public async Task PreMatchingAsync_NotYetMatchTime_StatusUnchanged()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayStatus = TournamentPlanStatus.Join,
            MatchStartDt = DateTime.Now.AddHours(1), // まだ
        };
        var svc = BuildWithPlan(plan);

        await svc.PreMatchingAsync();

        Assert.Equal(TournamentPlanStatus.Join, plan.PlayStatus);
    }

    // シナリオ2: 参加者不足 → Reject に遷移
    // 原典: bNext == FALSE → m_nPlayStatus = TRNMNT_PLAN_STATUS_REJECT
    [Fact]
    public async Task PreMatchingAsync_NotEnoughPlayers_StatusReject()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayStatus = TournamentPlanStatus.Join,
            MatchStartDt = DateTime.Now.AddMinutes(-1), // 過ぎた
            MaxPlayerNum = 4,
            MaxRoomNum = 1,
        };

        // 参加者が MaxPlayerNum/2 以下
        _repoMock.Setup(r => r.SelectJoinListAsync(It.IsAny<long>()))
            .ReturnsAsync(new List<TournamentJoin>
            {
                new TournamentJoin { MemberNo = "u1" },
            }); // 1人 ≤ 2 → Reject

        SetupRepoBulk();
        var svc = BuildWithPlan(plan);

        await svc.PreMatchingAsync();

        Assert.Equal(TournamentPlanStatus.Reject, plan.PlayStatus);
    }

    // シナリオ3: 参加者十分 → Wait に遷移 + DetailMap が作成される
    // 原典: m_nPlayStatus = TRNMNT_PLAN_STATUS_WAIT
    [Fact]
    public async Task PreMatchingAsync_EnoughPlayers_StatusWait()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayStatus = TournamentPlanStatus.Join,
            MatchStartDt = DateTime.Now.AddMinutes(-1),
            MaxPlayerNum = 4,
            MaxRoomNum = 1,
            PlayMode = 1,
            PlayNum = TournamentPlayNum.OnePlay,
            PlayTime = 5,
            PlayPhase = TournamentConst.PhaseFull, // 10
            GradeMoney = new long[4],
        };

        _repoMock.Setup(r => r.SelectJoinListAsync(It.IsAny<long>()))
            .ReturnsAsync(new List<TournamentJoin>
            {
                new TournamentJoin { MemberNo = "u1" },
                new TournamentJoin { MemberNo = "u2" },
                new TournamentJoin { MemberNo = "u3" },
                new TournamentJoin { MemberNo = "u4" },
            }); // 4人 > MaxPlayerNum/2

        SetupRepoBulk();
        var svc = BuildWithPlan(plan);

        await svc.PreMatchingAsync();

        Assert.Equal(TournamentPlanStatus.Wait, plan.PlayStatus);
        Assert.NotNull(svc.GetDetails(1));
    }

    // ─── GoMatchingAsync ─────────────────────────────────────────────────

    // シナリオ4: NextStartDt 未到達 → PLAY に遷移しない
    // 原典: clNow < clStartDt → continue
    [Fact]
    public async Task GoMatchingAsync_NotYetStartTime_StatusUnchanged()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayStatus = TournamentPlanStatus.Wait,
            NextStartDt = DateTime.Now.AddHours(1), // まだ
        };
        SetupRepoBulk();
        var svc = BuildWithPlan(plan);

        var starts = await svc.GoMatchingAsync();

        Assert.Empty(starts);
        Assert.Equal(TournamentPlanStatus.Wait, plan.PlayStatus);
    }

    // シナリオ5: NextStartDt 到達 → PLAY に遷移 + 開始情報を返す
    // 原典: m_nPlayStatus = TRNMNT_PLAN_STATUS_PLAY → AutoMatching 通知
    [Fact]
    public async Task GoMatchingAsync_StartTimeReached_StatusPlay()
    {
        const string channelId = "MAJAK20ZH5A001";
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            session.Register(new MajakPlayer
            {
                ConnectionId = $"conn{i}",
                MemberNo = $"u{i}",
                ChannelId = channelId,
            });
        }

        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayStatus = TournamentPlanStatus.Wait,
            NextStartDt = DateTime.Now.AddMinutes(-1), // 過ぎた
            RoomOption = "1200000010000",
        };

        var detail = new TournamentDetail
        {
            SeqNo = 1,
            SubId = 1,
            PlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            JoinMemberNo = new[] { "01", "02", "03", "04" },
        };
        // IsFinished = EndDt != default → 初期値は false (未終了)
        Assert.False(detail.IsFinished);

        SetupRepoBulk();
        var svc = BuildWithPlan(plan, new Dictionary<int, TournamentDetail> { [1] = detail }, session);

        var starts = await svc.GoMatchingAsync();

        Assert.Equal(TournamentPlanStatus.Play, plan.PlayStatus);
        Assert.Single(starts);
        Assert.True(starts[0].RoomId > 0);
        Assert.Equal(channelId, starts[0].ChannelId);
        Assert.Equal(4, starts[0].MemberNos.Count);
        Assert.NotNull(session.GetRoom(starts[0].RoomId));
        Assert.NotNull(session.GetPendingMatch(starts[0].RoomId));
    }

    [Fact]
    public async Task NotifyMatchStartAsync_SendsLegacyAutoMatchingPayload()
    {
        const string channelId = "MAJAK20ZH5A001";
        var session = new PlayerSessionService();
        session.Register(new MajakPlayer
        {
            ConnectionId = "conn1",
            MemberNo = "u1",
            ChannelId = channelId,
        });
        var hub = BuildHubMock();
        var tournament = TestTournamentServiceFactory.Create(_repoMock.Object, _loggerMock.Object, session: session, hub: hub.Object);
        var svc = new TournamentBackgroundService(
            tournament,
            session,
            hub.Object,
            null!,
            new Mock<ILogger<TournamentBackgroundService>>().Object);
        var method = typeof(TournamentBackgroundService).GetMethod(
            "NotifyMatchStartAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(svc, new object[]
        {
            new TournamentMatchStartInfo
            {
                SeqNo = 7,
                SubId = 3,
                RoomId = 12,
                RoomOption = "1200000010000",
                MemberNos = ["u1"],
            },
            CancellationToken.None,
        })!;

        Assert.Single(_hubSent);
        Assert.Equal(Cmd.MajAutoMatching, _hubSent[0].method);
        var packet = CommandTestHelper.ToDict(_hubSent[0].packet);
        Assert.Equal(GKey.ValueSuccess, ((JsonElement)packet[GKey.Result]!).GetString());
        Assert.Equal("u1", ((JsonElement)packet[GKey.Pix]!).GetString());
        Assert.Equal(channelId, ((JsonElement)packet[GKey.ChannelId]!).GetString());
        Assert.Equal(12, ((JsonElement)packet[GKey.RoomId]!).GetInt32());
        Assert.Equal("1200000010000", ((JsonElement)packet[GKey.RoomOption]!).GetString());
        Assert.Equal(GKey.ValueConnectForGameJoin, ((JsonElement)packet[GKey.ConnectFor]!).GetString());
    }

    [Fact]
    public async Task GoMatchingAsync_MissingLobbyMember_MarksJoinExitAndUsesPresentPlayers()
    {
        const string channelId = "MAJAK20ZH5A001";
        var session = new PlayerSessionService();
        foreach (var memberNo in new[] { "u1", "u2", "u3" })
        {
            session.Register(new MajakPlayer
            {
                ConnectionId = $"conn-{memberNo}",
                MemberNo = memberNo,
                ChannelId = channelId,
            });
        }
        var plan = new TournamentPlan
        {
            SeqNo = 2,
            PlayStatus = TournamentPlanStatus.Wait,
            NextStartDt = DateTime.Now.AddMinutes(-1),
            RoomOption = "1200000010000",
        };
        var detail = new TournamentDetail
        {
            SeqNo = 2,
            SubId = 1,
            PlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            JoinMemberNo = new[] { "01", "02", "03", "04" },
        };
        SetupRepoBulk();
        var svc = BuildWithPlan(plan, new Dictionary<int, TournamentDetail> { [1] = detail }, session);

        var starts = await svc.GoMatchingAsync();

        Assert.Single(starts);
        Assert.Equal(new[] { "u1", "u2", "u3" }, starts[0].MemberNos);
        _repoMock.Verify(r => r.BulkUpdateJoinStatusAsync(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "u4" })),
            TournamentJoinStatus.Exit), Times.Once);
    }

    [Fact]
    public void GetTournamentResultPayload_UsesOriginalMemberNoScoresAfterRanking()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 9,
            PlayNum = TournamentPlayNum.TwoPlay,
            PlayPhase = 10,
            PlayMode = TournamentPlayMode.TwoWin,
            MaxPlayerNum = 8,
        };
        var detail = new TournamentDetail
        {
            SeqNo = 9,
            SubId = 1,
            PlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            JoinMemberNo = new[] { "01", "02", "03", "04" },
            PointTmp = new[] { 10, 50, 30, 40 },
            Point = new[] { 30, 70, 60, 50 },
            GradePlayerMemberNo = new[] { "u2", "u3", "u1", "u4" },
            GradeMemberNo = new[] { "02", "03", "01", "04" },
        };
        var session = new PlayerSessionService();
        var p1 = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1" };
        var p2 = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2" };
        var p3 = new MajakPlayer { ConnectionId = "c3", MemberNo = "u3" };
        var p4 = new MajakPlayer { ConnectionId = "c4", MemberNo = "u4" };
        session.Register(p1);
        session.Register(p2);
        session.Register(p3);
        session.Register(p4);
        var svc = BuildWithPlan(plan, new Dictionary<int, TournamentDetail> { [1] = detail }, session);

        var payload = svc.GetTournamentResultPayload(9, 1)!;

        Assert.Equal(4, payload[Key.TournamentTotalReportCnt]);
        Assert.Equal($"{p2.Pix}\t1\t70\t50\t20\t", payload[$"{Key.TournamentTotalReport}0"]);
        Assert.Equal($"{p3.Pix}\t1\t60\t30\t30\t", payload[$"{Key.TournamentTotalReport}1"]);
        Assert.NotEqual(p2.MemberNo, p2.Pix);
    }

    // ─── PostMatchingAsync ────────────────────────────────────────────────

    // シナリオ6: 全対局終了 → END に遷移
    // 原典: allDone == true → TRNMNT_PLAN_STATUS_END
    [Fact]
    public async Task PostMatchingAsync_AllFinished_StatusEnd()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayStatus = TournamentPlanStatus.Play,
            PlayPhase = TournamentConst.PhaseFull, // maxPhase = 10 → 決勝終了
            MaxPlayerNum = 4,
            PlayMode = 1,
            PlayNum = TournamentPlayNum.OnePlay,
            MaxRoomNum = 1,
            GradeMoney = new long[4],
            ResultMemberNo = new string[4],
            NextEndDt = DateTime.Now.AddHours(10), // タイムアウトはまだ
        };

        var detail = new TournamentDetail
        {
            SeqNo = 1,
            SubId = 1,
            PlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            JoinMemberNo = new[] { "01", "02", "03", "04" },
            GradeMemberNo = new string[4],
            GradePlayerMemberNo = new string[4],
            EndDt = DateTime.Now.AddMinutes(-1), // 終了済み
        };
        Assert.True(detail.IsFinished);

        _repoMock.Setup(r => r.SelectJoinListAsync(It.IsAny<long>()))
            .ReturnsAsync(new List<TournamentJoin>
            {
                new TournamentJoin { MemberNo = "u1" },
                new TournamentJoin { MemberNo = "u2" },
            });
        SetupRepoBulk();
        var svc = BuildWithPlan(plan, new Dictionary<int, TournamentDetail> { [1] = detail });

        await svc.PostMatchingAsync();

        Assert.Equal(TournamentPlanStatus.End, plan.PlayStatus);
    }

    [Fact]
    public async Task PostMatchingAsync_FinalEnd_InsertsLegacyTournamentResultPresents()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 11,
            PlayStatus = TournamentPlanStatus.Play,
            PlayPhase = TournamentConst.PhaseFull,
            MaxPlayerNum = 4,
            PlayMode = TournamentPlayMode.OneWin,
            PlayNum = TournamentPlayNum.OnePlay,
            MaxRoomNum = 1,
            JoinMoney = 100,
            PlayerNum = 4,
            PlanMemberNo = "host",
            GradeMoney = new long[] { 1000, 500, 0, 250 },
            ResultMemberNo = new string[4],
            NextEndDt = DateTime.Now.AddHours(10),
        };
        var detail = new TournamentDetail
        {
            SeqNo = 11,
            SubId = 1,
            PlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            JoinMemberNo = new[] { "01", "02", "03", "04" },
            GradePlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            GradeMemberNo = new[] { "01", "02", "03", "04" },
            EndDt = DateTime.Now.AddMinutes(-1),
        };
        _repoMock.Setup(r => r.SelectJoinListAsync(plan.SeqNo))
            .ReturnsAsync(new List<TournamentJoin>
            {
                new() { MemberNo = "u1" },
                new() { MemberNo = "u2" },
            });
        SetupRepoBulk();
        var svc = BuildWithPlan(plan, new Dictionary<int, TournamentDetail> { [1] = detail });

        await svc.PostMatchingAsync();

        _repoMock.Verify(r => r.InsertUserPresentsAsync(It.Is<IEnumerable<UserPresentRecord>>(rows =>
            rows.Count() == 4
            && HasPresent(rows, "u1", 1000, TournamentPresentKind.ResultGrade, TournamentPresentItemKind.Money)
            && HasPresent(rows, "u2", 500, TournamentPresentKind.ResultGrade, TournamentPresentItemKind.Money)
            && HasPresent(rows, "u4", 250, TournamentPresentKind.ResultGrade, TournamentPresentItemKind.Money)
            && HasPresent(rows, "host", 360, TournamentPresentKind.ResultPlan, TournamentPresentItemKind.Money))),
            Times.Once);
    }

    [Fact]
    public async Task PostMatchingAsync_FinalEnd_UpdatesPlannerManageAndQueuesTitlePresent()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 12,
            PlayStatus = TournamentPlanStatus.Play,
            PlayPhase = 20,
            MaxPlayerNum = 16,
            PlayMode = TournamentPlayMode.OneWin,
            PlayNum = TournamentPlayNum.OnePlay,
            MaxRoomNum = 4,
            PlanMemberNo = "host",
            GradeMoney = new long[4],
            ResultMemberNo = new string[4],
            NextEndDt = DateTime.Now.AddHours(10),
        };
        var detail = new TournamentDetail
        {
            SeqNo = 12,
            SubId = 1,
            PlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            JoinMemberNo = new[] { "01", "02", "03", "04" },
            GradePlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            GradeMemberNo = new[] { "01", "02", "03", "04" },
            EndDt = DateTime.Now.AddMinutes(-1),
        };
        _repoMock.Setup(r => r.SelectJoinListAsync(plan.SeqNo))
            .ReturnsAsync(new List<TournamentJoin>
            {
                new() { MemberNo = "u1" },
                new() { MemberNo = "u2" },
            });
        _repoMock.Setup(r => r.SelectJoinAsync("host"))
            .ReturnsAsync(new TournamentJoin { MemberNo = "host", TotManageNum = 2, ManageNum = 0 });
        SetupRepoBulk();
        var svc = BuildWithPlan(plan, new Dictionary<int, TournamentDetail> { [1] = detail });

        await svc.PostMatchingAsync();

        _repoMock.Verify(r => r.MergePlannerManageAsync(It.Is<TournamentJoin>(planner =>
            planner.MemberNo == "host" && planner.TotManageNum == 3 && planner.ManageNum == 1)), Times.Once);
        _repoMock.Verify(r => r.InsertUserPresentsAsync(It.Is<IEnumerable<UserPresentRecord>>(rows =>
            rows.Count() == 1
            && HasPresent(rows, "host", 1, TournamentPresentKind.Title, TournamentPresentItemKind.MajakTitle)
            && rows.Single().PresentId == "mjkt600")), Times.Once);
    }

    [Fact]
    public async Task StopTournamentsByLimitAsync_LimitOverlap_PersistsPlanStopAndJoinExit()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 9,
            PlayStatus = TournamentPlanStatus.Join,
            JoinStartDt = DateTime.Now.AddMinutes(-10),
            PlayEndDt = DateTime.Now.AddHours(1),
            MaxRoomNum = 2,
            PlanMemberNo = "host",
            JoinMoney = 100,
            GradeMoney = new long[] { 1000, 0, 0, 0 },
        };

        _repoMock.Setup(r => r.SelectJoinListAsync(plan.SeqNo))
            .ReturnsAsync(new List<TournamentJoin>
            {
                new() { MemberNo = "u1", JoinStatus = TournamentJoinStatus.Join },
                new() { MemberNo = "u2", JoinStatus = TournamentJoinStatus.Join },
            });
        SetupRepoBulk();
        var svc = BuildWithPlan(plan);
        var limits = (List<TournamentLimit>)typeof(TournamentService)
            .GetField("_limits", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(svc)!;
        limits.Add(new TournamentLimit
        {
            LimitValid = 1,
            LimitStartDt = DateTime.Now.AddMinutes(-1),
            LimitEndDt = DateTime.Now.AddMinutes(30),
        });

        await svc.StopTournamentsByLimitAsync();

        Assert.Equal(TournamentPlanStatus.Stop, plan.PlayStatus);
        _repoMock.Verify(r => r.UpdatePlanStatusAsync(It.Is<TournamentPlan>(p =>
            p.SeqNo == plan.SeqNo && p.PlayStatus == TournamentPlanStatus.Stop)), Times.Once);
        _repoMock.Verify(r => r.BulkUpdateJoinStatusAsync(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "u1", "u2" })),
            TournamentJoinStatus.Exit), Times.Once);
        _repoMock.Verify(r => r.InsertUserPresentsAsync(It.Is<IEnumerable<UserPresentRecord>>(rows =>
            rows.Count() == 3
            && HasPresent(rows, "u1", 100, TournamentPresentKind.StopJoin, TournamentPresentItemKind.Money)
            && HasPresent(rows, "u2", 100, TournamentPresentKind.StopJoin, TournamentPresentItemKind.Money)
            && HasPresent(rows, "host", 1100, TournamentPresentKind.StopPlan, TournamentPresentItemKind.Money))),
            Times.Once);
    }

    [Fact]
    public async Task ReportMatchEnd_PreservesOriginalMemberNoForRankedMembers()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayNum = TournamentPlayNum.OnePlay,
            PlayMode = TournamentPlayMode.TwoWin,
            PlayPhase = TournamentConst.PhaseFull,
        };
        var detail = new TournamentDetail
        {
            SeqNo = 1,
            SubId = 1,
            PlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            JoinMemberNo = new[] { "07", "03", "11", "04" },
            GradeMemberNo = new string[4],
            GradePlayerMemberNo = new string[4],
        };
        SetupRepoBulk();
        var svc = BuildWithPlan(plan, new Dictionary<int, TournamentDetail> { [1] = detail });

        await svc.ReportMatchEndAsync(1, 1,
            new[] { "u3", "u1", "u4", "u2" },
            new[] { "01", "02", "03", "04" },
            new[] { 30, 10, -5, -20 });

        Assert.Equal(new[] { "u3", "u1", "u4", "u2" }, detail.GradePlayerMemberNo);
        Assert.Equal(new[] { "11", "07", "04", "03" }, detail.GradeMemberNo);
        Assert.Equal(new[] { 10, -20, 30, -5 }, detail.Point);
        _repoMock.Verify(r => r.BulkUpdateJoinStatusAsync(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "u4", "u2" })),
            TournamentJoinStatus.End), Times.Once);
    }

    [Fact]
    public async Task ReportMatchEnd_TwoPlayFirstGame_StoresPointTmpAndTotalByOriginalMemberNo()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayNum = TournamentPlayNum.TwoPlay,
            PlayPhase = TournamentConst.PhaseHalf,
        };
        var detail = new TournamentDetail
        {
            SeqNo = 1,
            SubId = 1,
            PlayerMemberNo = new[] { "u1", "u2", "u3", "u4" },
            JoinMemberNo = new[] { "07", "03", "11", "04" },
            GradeMemberNo = new string[4],
            GradePlayerMemberNo = new string[4],
        };
        SetupRepoBulk();
        var svc = BuildWithPlan(plan, new Dictionary<int, TournamentDetail> { [1] = detail });

        await svc.ReportMatchEndAsync(1, 1,
            new[] { "u4", "u2", "u1", "u3" },
            new[] { "01", "02", "03", "04" },
            new[] { 40, 20, 0, -10 });

        Assert.Equal(new[] { 0, 20, -10, 40 }, detail.PointTmp);
        Assert.Equal(new[] { 0, 20, -10, 40 }, detail.Point);
    }

    // ─── JoinAsync ────────────────────────────────────────────────────────

    // シナリオ7: 参加費あり → GamMoney が減少
    // 原典: pPlayer->AddGamMoney(-m_llJoinMoney)
    [Fact]
    public async Task JoinAsync_WithJoinMoney_DeductsMoney()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            PlayStatus = TournamentPlanStatus.Join,
            JoinStartDt = DateTime.Now.AddHours(-1),
            MatchStartDt = DateTime.Now.AddHours(2),
            JoinMoney = 1000,
            MaxPlayerNum = 4,
        };
        SetupRepoBulk();
        var svc = BuildWithPlan(plan);
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 5000 };

        var (ok, _) = await svc.JoinAsync(1, player, "");

        Assert.True(ok);
        Assert.Equal(4000, player.GamMoney); // 5000 - 1000
        Assert.Equal(1, plan.PlayerNum); // 0 + 1 = 1 (JoinAsync で PlayerNum++)
    }

    // シナリオ8: プランなし → (false, 0)
    [Fact]
    public async Task JoinAsync_PlanNotFound_ReturnsFalse()
    {
        var svc = TestTournamentServiceFactory.Create(_repoMock.Object, _loggerMock.Object);
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 5000 };

        var (ok, count) = await svc.JoinAsync(999, player, "");

        Assert.False(ok);
        Assert.Equal(0, count);
    }

    // ─── CancelJoinAsync ─────────────────────────────────────────────────

    // シナリオ9: キャンセル → GamMoney が返還される
    // 原典: pPlayer->AddGamMoney(plan->m_llJoinMoney) (返金)
    [Fact]
    public async Task CancelJoinAsync_WithJoinMoney_RefundsMoney()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            JoinMoney = 1000,
            PlayerNum = 1,
        };
        SetupRepoBulk();
        var svc = BuildWithPlan(plan);
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 4000 };

        var (ok, _) = await svc.CancelJoinAsync(1, player);

        Assert.True(ok);
        Assert.Equal(5000, player.GamMoney); // 4000 + 1000
        Assert.Equal(0, plan.PlayerNum);     // 1 - 1 = 0
    }

    // シナリオ10: キャンセル後 PlayerNum は 0 以下にならない
    // 原典: Math.Max(0, plan.PlayerNum - 1)
    [Fact]
    public async Task CancelJoinAsync_PlayerNumAlreadyZero_StaysZero()
    {
        var plan = new TournamentPlan
        {
            SeqNo = 1,
            JoinMoney = 0,
            PlayerNum = 0,  // 既に0
        };
        SetupRepoBulk();
        var svc = BuildWithPlan(plan);
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 5000 };

        await svc.CancelJoinAsync(1, player);

        Assert.Equal(0, plan.PlayerNum); // 0 以下にならない
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameRoom モデルテスト
// 原典: HMajRoomInfo / HMajRoomServer 状態管理
// ═══════════════════════════════════════════════════════════════════════════
public class GameRoomModelTests
{
    // シナリオ1: PlayerCount — 有効な座席のみカウント
    [Fact]
    public void PlayerCount_OnlyCountsOccupiedSeats()
    {
        var room = new GameRoom { RoomId = 1 };
        var p1 = new MajakPlayer { MemberNo = "u1" };
        var p2 = new MajakPlayer { MemberNo = "u2" };

        room.AddPlayer(p1, 0);
        room.AddPlayer(p2, 1);

        Assert.Equal(2, room.PlayerCount);
    }

    [Fact]
    public void ActivePlayerCount_ExcludesOutPlayers()
    {
        var room = new GameRoom { RoomId = 1 };
        var p1 = new MajakPlayer { MemberNo = "u1" };
        var p2 = new MajakPlayer { MemberNo = "u2" };

        room.AddPlayer(p1, 0);
        room.AddPlayer(p2, 1);
        p2.IsOutPlayer = true;

        Assert.Equal(2, room.PlayerCount);
        Assert.Equal(1, room.ActivePlayerCount);
        Assert.False(room.HasNoActiveMembers);
    }

    [Fact]
    public void HasNoActiveMembers_WhenOnlyOutPlayersRemain()
    {
        var room = new GameRoom { RoomId = 1 };
        var p1 = new MajakPlayer { MemberNo = "u1" };

        room.AddPlayer(p1, 0);
        p1.IsOutPlayer = true;

        Assert.Equal(1, room.PlayerCount);
        Assert.Equal(0, room.ActivePlayerCount);
        Assert.True(room.HasNoActiveMembers);
    }

    [Fact]
    public void RemovePlayingRoomIfNoActivePlayers_RemovesImmediatelyEvenWithViewer()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            ConnectionId = "c1",
            MemberNo = "u1",
            ChannelId = "ch1",
        };
        var viewer = new MajakPlayer { ConnectionId = "v1", MemberNo = "viewer1", ChannelId = "ch1" };
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        Assert.True(room.AddViewer(viewer));
        player.IsOutPlayer = true;

        var removed = session.RemovePlayingRoomIfNoActivePlayers(room.RoomId);

        Assert.Same(room, removed);
        Assert.Null(session.GetRoom(room.RoomId));
    }

    [Fact]
    public void RemovePlayingRoomIfNoActivePlayers_KeepsRoomWhenPlayerRemains()
    {
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var disconnected = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);
        Assert.True(session.JoinRoom(room.RoomId, disconnected));
        room.State = GameRoomState.Playing;
        disconnected.IsOutPlayer = true;

        var removed = session.RemovePlayingRoomIfNoActivePlayers(room.RoomId);

        Assert.Null(removed);
        Assert.Same(room, session.GetRoom(room.RoomId));
        Assert.Same(disconnected, room.Seats[(int)disconnected.SeatPos]);
    }

    [Fact]
    public void ReconnectToRoom_ClearsNoActiveMembersSince()
    {
        var session = new PlayerSessionService();
        var oldPlayer = new MajakPlayer
        {
            ConnectionId = "old",
            MemberNo = "u1",
            ChannelId = "ch1",
        };
        var room = session.CreateRoom("ch1", oldPlayer, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        oldPlayer.IsOutPlayer = true;
        room.NoActiveMembersSince = DateTimeOffset.UtcNow.AddMinutes(-1);

        var newPlayer = new MajakPlayer { ConnectionId = "new", MemberNo = "u1", ChannelId = "ch1" };
        int seat = session.ReconnectToRoom(room.RoomId, newPlayer);

        Assert.Equal(0, seat);
        Assert.False(room.HasNoActiveMembers);
        Assert.Null(room.NoActiveMembersSince);
    }

    [Fact]
    public void ReconnectToRoom_AllowsOutPlayerWhilePlayingRoomExists()
    {
        var session = new PlayerSessionService();
        var oldPlayer = new MajakPlayer
        {
            ConnectionId = "old",
            MemberNo = "u1",
            ChannelId = "ch1",
        };
        var room = session.CreateRoom("ch1", oldPlayer, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        oldPlayer.IsOutPlayer = true;

        var newPlayer = new MajakPlayer { ConnectionId = "new", MemberNo = "u1", ChannelId = "ch1" };
        int seat = session.ReconnectToRoom(room.RoomId, newPlayer);

        Assert.Equal(0, seat);
        Assert.False(oldPlayer.IsOutPlayer);
        Assert.Same(oldPlayer, room.Seats[0]);
        Assert.Equal(GameRoomState.Playing, room.State);
    }

    [Fact]
    public void RebindPlayingRoomPlayer_ReplacesActiveSeatConnection()
    {
        var session = new PlayerSessionService();
        var oldPlayer = new MajakPlayer
        {
            ConnectionId = "old",
            MemberNo = "u1",
            ChannelId = "ch1",
            EngineOrder = 2,
        };
        session.Register(oldPlayer);
        var room = session.CreateRoom("ch1", oldPlayer, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        oldPlayer.EngineOrder = 2;

        var newPlayer = new MajakPlayer { ConnectionId = "new", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(newPlayer);

        int seat = session.RebindPlayingRoomPlayer(room.RoomId, newPlayer);

        Assert.Equal(0, seat);
        Assert.Equal("new", room.Seats[0]!.ConnectionId);
        Assert.False(room.Seats[0]!.IsOutPlayer);
        Assert.Equal(room.RoomId, newPlayer.RoomId);
        Assert.Equal((uint)0, newPlayer.SeatPos);
        Assert.Equal(2, newPlayer.EngineOrder);
        Assert.Same(newPlayer, session.GetByMember("u1"));
    }

    [Fact]
    public void DisconnectFromRoom_StaleConnectionDoesNotDetachReboundSeat()
    {
        var session = new PlayerSessionService();
        var oldPlayer = new MajakPlayer
        {
            ConnectionId = "old",
            MemberNo = "u1",
            ChannelId = "ch1",
            EngineOrder = 1,
        };
        session.Register(oldPlayer);
        var room = session.CreateRoom("ch1", oldPlayer, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;

        var newPlayer = new MajakPlayer { ConnectionId = "new", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(newPlayer);
        Assert.Equal(0, session.RebindPlayingRoomPlayer(room.RoomId, newPlayer));

        bool disconnected = session.DisconnectFromRoom(oldPlayer, "old");

        Assert.False(disconnected);
        Assert.Equal("new", room.Seats[0]!.ConnectionId);
        Assert.False(room.Seats[0]!.IsOutPlayer);
        Assert.Equal(room.RoomId, newPlayer.RoomId);
        Assert.Same(newPlayer, session.GetByMember("u1"));
    }

    [Fact]
    public async Task ContinueRoom_OutPlayerInPlayingRoomIsReturnedWithoutDeadline()
    {
        var registry = new RoomRegistryService(TestMasterCacheFactory.CreateRedisService());
        var player = new MajakPlayer
        {
            MemberNo = "u1",
            IsOutPlayer = true,
        };
        var room = new GameRoom
        {
            RoomId = 1235,
            ChannelId = "ch1",
            RoomTitle = "continue room",
            RoomOption = "opt",
            State = GameRoomState.Playing,
        };
        room.Seats[0] = player;

        await registry.RegisterRoomAsync(room.RoomId, room.ChannelId, room.RoomTitle,
            isPrivate: false, memberCnt: 0, memberMax: 4,
            serverUrl: "http://server-a", roomOption: room.RoomOption);
        await registry.SetContinueRoomAsync(player.MemberNo, room);

        var continuedRoom = await registry.GetContinueRoomAsync(player.MemberNo);

        Assert.NotNull(continuedRoom);
        Assert.Equal(room.RoomId, continuedRoom.RoomId);
        Assert.Same(player, room.Seats[0]);
        Assert.Equal(GameRoomState.Playing, room.State);
    }

    [Fact]
    public void DisconnectFromRoom_AfterReconnectMarksRoomSeatOut()
    {
        var session = new PlayerSessionService();
        var oldPlayer = new MajakPlayer
        {
            ConnectionId = "old",
            MemberNo = "u1",
            ChannelId = "ch1",
        };
        var room = session.CreateRoom("ch1", oldPlayer, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        oldPlayer.IsOutPlayer = true;

        var newPlayer = new MajakPlayer { ConnectionId = "new", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(newPlayer);
        Assert.Equal(0, session.ReconnectToRoom(room.RoomId, newPlayer));

        session.DisconnectFromRoom(newPlayer);
        var noActiveSince = room.NoActiveMembersSince;
        var removed = session.RemovePlayingRoomIfNoActivePlayers(room.RoomId);

        Assert.True(room.Seats[0]?.IsOutPlayer);
        Assert.True(room.HasNoActiveMembers);
        Assert.NotNull(noActiveSince);
        Assert.Same(room, removed);
        Assert.Null(session.GetRoom(room.RoomId));
    }

    [Fact]
    public async Task ContinueRoom_FallbackRequiresLiveRoomEntry()
    {
        var registry = new RoomRegistryService(TestMasterCacheFactory.CreateRedisService());
        var player = new MajakPlayer
        {
            MemberNo = "u1",
            IsOutPlayer = true,
        };
        var room = new GameRoom
        {
            RoomId = 1234,
            ChannelId = "ch1",
            RoomTitle = "continue room",
            RoomOption = "opt",
            State = GameRoomState.Playing,
        };
        room.Seats[0] = player;

        await registry.RegisterRoomAsync(room.RoomId, room.ChannelId, room.RoomTitle,
            isPrivate: false, memberCnt: 0, memberMax: 4,
            serverUrl: "http://server-a", roomOption: room.RoomOption);
        await registry.SetContinueRoomAsync(player.MemberNo, room);

        var found = await registry.GetContinueRoomAsync(player.MemberNo);
        Assert.NotNull(found);
        Assert.Equal(room.RoomId, found.RoomId);
        Assert.Equal("http://server-a", found.ServerUrl);

        await registry.RemoveRoomAsync(room.RoomId, room.ChannelId);

        Assert.Null(await registry.GetContinueRoomAsync(player.MemberNo));
    }

    // シナリオ2: IsEmpty — 全席空なら true
    [Fact]
    public void IsEmpty_NoPlayers_ReturnsTrue()
    {
        var room = new GameRoom { RoomId = 1 };
        Assert.True(room.IsEmpty);
    }

    // シナリオ3: RemovePlayer → 座席が空になる
    [Fact]
    public void RemovePlayer_ClearsSlot()
    {
        var room = new GameRoom { RoomId = 1 };
        var p = new MajakPlayer { MemberNo = "u1" };
        room.AddPlayer(p, 0);

        room.RemovePlayer("u1");

        Assert.Null(room.Seats[0]);
        Assert.Equal(0, room.PlayerCount);
    }

    // シナリオ4: AddPlayer → RoomId が設定される
    [Fact]
    public void AddPlayer_SetsRoomId()
    {
        var room = new GameRoom { RoomId = 42 };
        var p = new MajakPlayer { MemberNo = "u1" };

        room.AddPlayer(p, 1);

        Assert.Equal(42, p.RoomId);
        Assert.Equal(1u, p.SeatPos);
    }

    // シナリオ5: OkButtonStates — 初期値はすべて false
    [Fact]
    public void OkButtonStates_Default_AllFalse()
    {
        var room = new GameRoom();
        Assert.All(room.OkButtonStates, s => Assert.False(s));
    }

    // シナリオ6: PlayHistory — 初期は空
    [Fact]
    public void PlayHistory_Default_IsEmpty()
    {
        var room = new GameRoom();
        Assert.Empty(room.PlayHistory);
    }

    // シナリオ7: BanishInfo — 初期値確認
    [Fact]
    public void BanishInfo_Default_IsNotBanishing()
    {
        var room = new GameRoom();
        Assert.False(room.BanishInfo.PreBanishing);
        Assert.False(room.BanishInfo.ReserveBanishing);
        Assert.Null(room.BanishInfo.ReserveMemberNo); // 原典: NULL 初期値
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajakPlayer モデルテスト
// 原典: HMajPlayer 各プロパティ / メソッド
// ═══════════════════════════════════════════════════════════════════════════
public class MajakPlayerModelTests
{
    // シナリオ1: GetRichiEffect — MajItems に CAT_RICHI アイテムがある場合
    // 原典: GetRichiEffect → m_mapMajItem から CAT_RICHI の UseFlag=true を検索
    [Fact]
    public void GetRichiEffect_NoItems_Returns0()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        Assert.Equal(0, player.GetRichiEffect());
    }

    [Fact]
    public void GetRichiEffect_Item001Active_Returns1()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item001",
            UseFlag = true,
            EndDt = DateTime.Now.AddDays(7),
            Qty = 1,
        });

        Assert.Equal(1, player.GetRichiEffect());
    }

    [Fact]
    public void GetRichiEffect_Item002Active_Returns2()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item002",
            UseFlag = true,
            EndDt = DateTime.Now.AddDays(7),
            Qty = 1,
        });

        Assert.Equal(2, player.GetRichiEffect());
    }

    // シナリオ2: GetCustomEquip — 装備中のカスタムアイテムを返す
    [Fact]
    public void GetCustomEquip_WithEquipped_ReturnsCustomId()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        player.CustomItems[100001] = new UserCustomItem { Kind = 30, Equip = 1 };
        player.CustomItems[100002] = new UserCustomItem { Kind = 30, Equip = 0 };

        int result = player.GetCustomEquip(30); // Kind=30のカテゴリ
        Assert.Equal(100001, result);
    }

    [Fact]
    public void GetCustomEquip_NoEquipped_Returns0()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        Assert.Equal(0, player.GetCustomEquip(1));
    }

    // シナリオ3: RejectInvite フラグの初期値
    [Fact]
    public void RejectInvite_Default_IsFalse()
    {
        var player = new MajakPlayer();
        Assert.False(player.RejectInvite);
    }

    // シナリオ4: IsAdminId 初期値
    [Fact]
    public void IsAdminId_Default_IsFalse()
    {
        var player = new MajakPlayer();
        Assert.False(player.IsAdminId);
    }

    // シナリオ5: GradeRecord — 初期値
    [Fact]
    public void GradeRecord_Default_GradeIsZero()
    {
        var player = new MajakPlayer();
        Assert.Equal(0, player.GradeRecord.Grade); // 初期値は0 (DB から設定される)
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameLogicService 静的ヘルパーテスト
// CalcGemGame の確率ロジック / BuildRuleInfo の RoomOption 解析
// ═══════════════════════════════════════════════════════════════════════════
public class GameLogicHelperTests
{
    private static GameLogicService BuildService(PlayerSessionService? session = null, PlayerRepository? playerRepo = null, HistoryRepository? historyRepo = null, LogRepository? logRepo = null, RoomRegistryService? roomRegistry = null, bool testEnvironment = false, string? trainingAiLevel = null)
    {
        session ??= new PlayerSessionService();
        var histMock = new Mock<HistoryRepository>(MockBehavior.Loose);
        var logMock = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        var playerMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        var titleMock = new Mock<TitleService>(MockBehavior.Loose, (PlayerRepository)null!, TestMasterCacheFactory.Create());
        var moneyMock = new Mock<GameMoneyService>(MockBehavior.Loose,
            (PlayerRepository)null!, (RatingService)null!, (HistoryRepository?)null);
        return new GameLogicService(session, historyRepo ?? histMock.Object, logRepo ?? logMock.Object,
            new RatingService(), playerRepo ?? playerMock.Object, moneyMock.Object, titleMock.Object, null!, null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GameSettings:TestEnvironment"] = testEnvironment.ToString(),
                    ["GameSettings:TrainingAiLevel"] = trainingAiLevel,
                })
                .Build(), roomRegistry: roomRegistry);
    }

    [Fact]
    public async Task StartGameLogic_RegistersContinueRoomsForPlayers()
    {
        var session = new PlayerSessionService();
        var players = Enumerable.Range(0, 4)
            .Select(i => new MajakPlayer
            {
                ConnectionId = $"c{i}",
                MemberNo = $"u{i}",
                ChannelId = "ch1",
            })
            .ToArray();
        foreach (var player in players) session.Register(player);
        var room = session.CreateRoom("ch1", players[0], "", 1, 0, 0, false);
        room.ServerUrl = "http://game";
        for (int i = 1; i < players.Length; i++)
            Assert.True(session.JoinRoom(room.RoomId, players[i]));

        var registry = new RoomRegistryService(TestMasterCacheFactory.CreateRedisService());
        var service = BuildService(session, roomRegistry: registry);
        var (ctx, _) = CommandTestHelper.MakeContext(players[0]);

        await service.StartGameLogicAsync(room, ctx);

        Assert.DoesNotContain(room.PendingActions, prompt => prompt != null);
        foreach (var player in players)
        {
            var continueRoom = await registry.GetContinueRoomAsync(player.MemberNo);
            Assert.NotNull(continueRoom);
            Assert.Equal(room.RoomId, continueRoom.RoomId);
            Assert.Equal("http://game", continueRoom.ServerUrl);
        }
    }

    [Fact]
    public async Task StartGameLogic_TwoPlayerTraining_DoesNotMoveNpcBeforeClientReady()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 12;
        room.Seats[2] = null;
        room.Seats[3] = null;
        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService().StartGameLogicAsync(room, ctx);

        var actionPackets = sent
            .Where(packet => packet.method == Cmd.GamePlay)
            .Select(packet => CommandTestHelper.ToDict(packet.packet))
            .Where(packet => packet.TryGetValue("playType", out var playType)
                && ((JsonElement)playType!).GetString() == "MJPID_ACTION")
            .ToArray();
        Assert.Empty(actionPackets);
        Assert.DoesNotContain(room.PendingActions, prompt => prompt != null);
    }

    [Fact]
    public async Task StartGameLogic_SoloTrainingStartsActionsWhenClientReadyArrivesDuringInit()
    {
        var session = new PlayerSessionService();
        var host = new MajakPlayer
        {
            MemberNo = "solo",
            NickName = "Solo",
            ConnectionId = "c0",
            ChannelId = "ch1",
        };
        var room = session.CreateRoom("ch1", host, "120000001000000", 1, 0, 0, false, subId: "00T5A", roomId: 14);

        var sent = new List<(string method, object packet)>();
        GameLogicService service = null!;
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                sent.Add((method, args[0]!));
                if (method == Cmd.AutoStart)
                    service.MarkGameClientReadyAsync(room.RoomId, host.ConnectionId).GetAwaiter().GetResult();
            })
            .Returns(Task.CompletedTask);
        var singleProxy = new Mock<ISingleClientProxy>();
        singleProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        clients.Setup(c => c.Client(It.IsAny<string>())).Returns(singleProxy.Object);
        clients.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(proxy.Object);
        clients.Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>())).Returns(proxy.Object);

        var ctx = new CommandContext { Player = host, Clients = clients.Object };
        service = BuildService(session, testEnvironment: true);

        await service.StartGameLogicAsync(room, ctx);
        await WaitUntilAsync(() => room.PendingActions.Any(prompt => prompt != null)
            || sent.Any(packet => packet.method == Cmd.GamePlay
                && CommandTestHelper.ToDict(packet.packet).TryGetValue("playType", out var playType)
                && ((JsonElement)playType!).GetString() == "MJPID_ACTION"));

        Assert.True(room.PendingActions.Any(prompt => prompt != null)
            || sent.Any(packet => packet.method == Cmd.GamePlay
                && CommandTestHelper.ToDict(packet.packet).TryGetValue("playType", out var playType)
                && ((JsonElement)playType!).GetString() == "MJPID_ACTION"));
    }

    [Fact]
    public async Task ProxyEmptySeats_TrainingEmptyDealer_AutoDiscardsAndAdvancesTurn()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 13;
        room.State = GameRoomState.Playing;
        room.Seats[3] = null;
        room.Engine.HanchanInfo.Player = new[] { 3, 0, 1, 2 };
        room.SeatToEngineOrder[0] = 1;
        room.SeatToEngineOrder[1] = 2;
        room.SeatToEngineOrder[2] = 3;
        room.SeatToEngineOrder[3] = 0;
        Assert.Equal(PlayerMode.Turn, room.Engine.Player[0].Mode);

        int[] handSerials = { 0, 1, 2, 3, 5, 9, 10, 11, 18, 19, 20, 31, 32, 4 };
        room.Engine.Player[0].Tehai.Clear();
        for (int index = 0; index < handSerials.Length; index++)
        {
            PaiCode tile = PaiCode.MakeSerial(handSerials[index]);
            tile.BipaiIndex = 100 + index;
            room.Engine.Player[0].Tehai.Add(tile);
        }
        int lastDrawnBipaiIndex = room.Engine.Player[0].Tehai[^1].BipaiIndex;
        int expectedBipaiIndex = room.Engine.Player[0].Tehai
            .First(tile => tile.GetSerial() == 31)
            .BipaiIndex;

        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);
        var method = typeof(GameLogicService)
            .GetMethod("ProxyEmptySeatsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(BuildService(testEnvironment: true), new object[] { room, ctx })!;
        Assert.Empty(sent.Where(packet => packet.method == Cmd.GamePlay)
            .Select(packet => CommandTestHelper.ToDict(packet.packet))
            .Where(packet => ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION"));
        await WaitUntilAsync(() => room.PendingActions.Skip(1).Any(prompt => prompt != null));

        var actionPackets = sent
            .Where(packet => packet.method == Cmd.GamePlay)
            .Select(packet => CommandTestHelper.ToDict(packet.packet))
            .Where(packet => ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION")
            .ToArray();
        var npcAction = Assert.Single(actionPackets, packet =>
            ((JsonElement)packet["seatOrder"]!).GetInt32() == 0
            && ((JsonElement)packet["action"]!).GetInt32() == (int)MajakServer.Engine.Act.Tap);
        Assert.Equal((int)MajakServer.Engine.Act.Tap, ((JsonElement)npcAction["action"]!).GetInt32());
        int selectedBipaiIndex = Assert.Single(((JsonElement)npcAction["bipaiIndex"]!).EnumerateArray()).GetInt32();
        Assert.Equal(expectedBipaiIndex, selectedBipaiIndex);
        Assert.NotEqual(lastDrawnBipaiIndex, selectedBipaiIndex);
        Assert.NotEqual(PlayerMode.Turn, room.Engine.Player[0].Mode);
        Assert.True(
            room.Engine.Player.Skip(1).Any(player => player.Mode == PlayerMode.Turn)
            || room.PendingActions.Skip(1).Any(prompt => prompt != null));
    }

    [Fact]
    public async Task ProxyPlay_TrainingAiDeclaresEvaluatedRiichi()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 16;
        room.State = GameRoomState.Playing;
        room.Seats[3] = null;
        room.Engine.HanchanInfo.Player = new[] { 3, 0, 1, 2 };
        Array.Fill(room.Engine.KyokuInfo.Dora, PaiCode.Invalid);
        Array.Fill(room.Engine.KyokuInfo.UraDora, PaiCode.Invalid);

        int[] handSerials = { 0, 1, 2, 12, 13, 14, 24, 25, 26, 31, 31, 3, 4, 33 };
        room.Engine.Player[0].Tehai.Clear();
        for (int index = 0; index < handSerials.Length; index++)
        {
            PaiCode tile = PaiCode.MakeSerial(handSerials[index]);
            tile.BipaiIndex = 100 + index;
            room.Engine.Player[0].Tehai.Add(tile);
        }
        int expectedBipaiIndex = room.Engine.Player[0].Tehai[^1].BipaiIndex;

        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);

        bool processed = await BuildService(testEnvironment: true)
            .ProxyPlayAsync(room, ctx, order: 0, useTrainingAi: true);

        Assert.True(processed);
        var actionPacket = Assert.Single(sent
            .Where(packet => packet.method == Cmd.GamePlay)
            .Select(packet => CommandTestHelper.ToDict(packet.packet))
            .Where(packet => ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION")
            .Where(packet => ((JsonElement)packet["seatOrder"]!).GetInt32() == 0)
            .Where(packet => ((JsonElement)packet["action"]!).GetInt32() == (int)MajakServer.Engine.Act.Ric));
        Assert.Equal((int)MajakServer.Engine.Act.Ric, ((JsonElement)actionPacket["action"]!).GetInt32());
        int selectedBipaiIndex = Assert.Single(((JsonElement)actionPacket["bipaiIndex"]!).EnumerateArray()).GetInt32();
        Assert.Equal(expectedBipaiIndex, selectedBipaiIndex);
    }

    [Fact]
    public async Task ProxyPlay_AdvancedTrainingAiUsesConfiguredDefensivePolicy()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 17;
        room.State = GameRoomState.Playing;
        room.Seats[3] = null;
        room.Engine.HanchanInfo.Player = new[] { 3, 0, 1, 2 };
        Array.Fill(room.Engine.KyokuInfo.Dora, PaiCode.Invalid);
        Array.Fill(room.Engine.KyokuInfo.UraDora, PaiCode.Invalid);

        int[] handSerials = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 27, 31 };
        room.Engine.Player[0].Tehai.Clear();
        for (int index = 0; index < handSerials.Length; index++)
        {
            PaiCode tile = PaiCode.MakeSerial(handSerials[index]);
            tile.BipaiIndex = 100 + index;
            room.Engine.Player[0].Tehai.Add(tile);
        }
        room.Engine.Player[1].RichiType = RichiType.Richi;
        room.Engine.Player[1].Sutehai.Add(PaiCode.MakeSerial(31));
        int expectedBipaiIndex = room.Engine.Player[0].Tehai
            .First(tile => tile.GetSerial() == 31)
            .BipaiIndex;

        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);

        bool processed = await BuildService(testEnvironment: true, trainingAiLevel: "Advanced")
            .ProxyPlayAsync(room, ctx, order: 0, useTrainingAi: true);

        Assert.True(processed);
        var actionPacket = sent
            .Where(packet => packet.method == Cmd.GamePlay)
            .Select(packet => CommandTestHelper.ToDict(packet.packet))
            .First(packet => ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION"
                && ((JsonElement)packet["seatOrder"]!).GetInt32() == 0
                && ((JsonElement)packet["bipaiIndex"]!).GetArrayLength() == 1);
        int selectedBipaiIndex = Assert.Single(((JsonElement)actionPacket["bipaiIndex"]!).EnumerateArray()).GetInt32();
        Assert.Equal(expectedBipaiIndex, selectedBipaiIndex);
    }

    [Fact]
    public async Task ProxyPlay_AdvancedSettingKeepsDisconnectedProxyOnLastTile()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 18;
        room.State = GameRoomState.Playing;
        int order = Array.FindIndex(room.Engine.Player, player => player.Mode == PlayerMode.Turn);
        Assert.InRange(order, 0, GameConst.PlayerMaxCount - 1);
        int expectedBipaiIndex = room.Engine.Player[order].Tehai[^1].BipaiIndex;
        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[room.Engine.HanchanInfo.Player[order]]!);

        bool processed = await BuildService(testEnvironment: true, trainingAiLevel: "Advanced")
            .ProxyPlayAsync(room, ctx, order, useTrainingAi: false);

        Assert.True(processed);
        var actionPacket = sent
            .Where(packet => packet.method == Cmd.GamePlay)
            .Select(packet => CommandTestHelper.ToDict(packet.packet))
            .First(packet => ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION"
                && ((JsonElement)packet["seatOrder"]!).GetInt32() == order
                && ((JsonElement)packet["action"]!).GetInt32() == (int)MajakServer.Engine.Act.Tap);
        int selectedBipaiIndex = Assert.Single(((JsonElement)actionPacket["bipaiIndex"]!).EnumerateArray()).GetInt32();
        Assert.Equal(expectedBipaiIndex, selectedBipaiIndex);
    }

    [Fact]
    public async Task ProxyEmptySeats_RestartsScanWhenLaterNpcActivatesEarlierNpc()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 15;
        room.State = GameRoomState.Playing;
        room.Seats[2] = null;
        room.Seats[3] = null;
        room.Engine.HanchanInfo.Player = new[] { 1, 0, 3, 2 };
        room.SeatToEngineOrder[0] = 1;
        room.SeatToEngineOrder[1] = 0;
        room.SeatToEngineOrder[2] = 3;
        room.SeatToEngineOrder[3] = 2;

        var npcTile = room.Engine.Player[2].Tehai[0];
        foreach (var enginePlayer in room.Engine.Player)
        {
            enginePlayer.Tehai.Clear();
            enginePlayer.Mode = PlayerMode.None;
        }
        room.Engine.Player[2].Tehai.Add(npcTile);
        room.Engine.Player[2].Mode = PlayerMode.Turn;
        typeof(MajakGameLogic)
            .GetField("_currOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(room.Engine, 2);

        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);
        var method = typeof(GameLogicService)
            .GetMethod("ProxyEmptySeatsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(BuildService(testEnvironment: true), new object[] { room, ctx })!;
        await WaitUntilAsync(() => room.PendingActions[0] != null
            && room.Engine.Player[2].Mode == PlayerMode.None
            && room.Engine.Player[3].Mode == PlayerMode.None);

        var npcDiscards = sent
            .Where(packet => packet.method == Cmd.GamePlay)
            .Select(packet => CommandTestHelper.ToDict(packet.packet))
            .Where(packet => ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION")
            .Where(packet => ((JsonElement)packet["action"]!).GetInt32() == (int)MajakServer.Engine.Act.Tap)
            .Select(packet => ((JsonElement)packet["seatOrder"]!).GetInt32())
            .ToArray();
        Assert.Equal(new[] { 2, 3 }, npcDiscards);
        Assert.Equal(PlayerMode.Turn, room.Engine.Player[0].Mode);
        Assert.Equal(PlayerMode.None, room.Engine.Player[2].Mode);
        Assert.Equal(PlayerMode.None, room.Engine.Player[3].Mode);
        Assert.NotNull(room.PendingActions[0]);
    }

    [Fact]
    public async Task SendGameResync_SendsPaiInfoBeforePlayHistory()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 14;
        room.State = GameRoomState.Playing;
        room.Seats[0]!.EngineOrder = 0;
        room.PlayHistory.Add(new { playType = "MJPID_INIHAN" });
        room.PlayHistory.Add(new { playType = "MJPID_INIKYO" });
        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService().SendGameResyncAsync(room, ctx, room.Seats[0]!);

        int paiInfoIndex = sent.FindIndex(packet => packet.method == Cmd.PaiInfoList);
        int historyIndex = sent.FindIndex(packet => packet.method == Cmd.History);
        Assert.True(paiInfoIndex >= 0);
        Assert.True(historyIndex > paiInfoIndex);
        var history = CommandTestHelper.ToDict(sent[historyIndex].packet);
        Assert.Equal(2, ((JsonElement)history["historyCount"]!).GetInt32());
    }

    [Fact]
    public async Task SendGameResync_BeforeAllClientsReady_DoesNotIssuePrompt()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 16;
        room.State = GameRoomState.Playing;
        int order = Array.FindIndex(room.Engine.Player, player => player.Mode == PlayerMode.Turn);
        int playerPos = room.Engine.HanchanInfo.Player[order];
        var player = room.Seats[playerPos]!;
        player.EngineOrder = order;
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await BuildService().SendGameResyncAsync(room, ctx, player, includePrompt: false);

        Assert.Null(room.PendingActions[order]);
        Assert.DoesNotContain(sent, packet => packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_ACTIONS");
    }

    // ─── CalcGemGame ─────────────────────────────────────────────────────
    // 原典: static const HITTBL t = {4000, 1000}
    //   0~999  → BIG_GEM_GAME=2  (10%)
    //   1000~4999 → ONE_GEM_GAME=1 (40%)
    //   5000~9999 → NOT_GEM_GAME=0 (50%)

    private static int InvokeCalcGemGame(GameRoom room)
    {
        var method = typeof(GameLogicService)
            .GetMethod("CalcGemGame",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)method.Invoke(null, new object[] { room })!;
    }

    private static Dictionary<string, object?> InvokeBuildAutoStartPayload(GameRoom room, int gemGame = 0)
    {
        var method = typeof(GameLogicService)
            .GetMethod("BuildAutoStartPayload", BindingFlags.NonPublic | BindingFlags.Static)!;
        return CommandTestHelper.ToDict(method.Invoke(null, new object[] { room, gemGame })!);
    }

    // シナリオ1: SubId[0]=='T' でない場合 → 常に 0
    [Fact]
    public void CalcGemGame_SubIdNotT_AlwaysReturnsZero()
    {
        var room = new GameRoom { SubId = "XYZAB" };

        // 100回試行して常に 0 であることを確認
        for (int i = 0; i < 100; i++)
            Assert.Equal(0, InvokeCalcGemGame(room));
    }

    // シナリオ2: SubId[0]=='T' の場合 → 0, 1, 2 のいずれか
    [Fact]
    public void CalcGemGame_SubIdStartsWithT_Returns0to2()
    {
        var room = new GameRoom { SubId = "T0N5A" };

        var results = new HashSet<int>();
        for (int i = 0; i < 10000; i++)
            results.Add(InvokeCalcGemGame(room));

        // 10000回試行すれば0,1,2が全て出るはず
        Assert.Contains(0, results);
        Assert.Contains(1, results);
        Assert.Contains(2, results);
        Assert.All(results, r => Assert.InRange(r, 0, 2));
    }

    [Fact]
    public void BuildAutoStartPayload_UsesRoomBanishInfo()
    {
        var room = new GameRoom { CreatorNo = "host" };
        room.Seats[1] = new MajakPlayer { MemberNo = "banishUser", Pix = "pix-banish" };
        room.BanishInfo.PreBanishing = true;
        room.BanishInfo.ReserveBanishing = true;
        room.BanishInfo.ReserveMemberNo = "banishUser";

        var payload = InvokeBuildAutoStartPayload(room);

        Assert.Equal(1, ((JsonElement)payload[GKey.PreBanishing]!).GetInt32());
        Assert.Equal(1, ((JsonElement)payload[GKey.ReserveBanishing]!).GetInt32());
        Assert.Equal("pix-banish", ((JsonElement)payload[GKey.Pix]!).GetString());
    }

    [Fact]
    public void BuildMemberListPayload_UsesRoomBanishInfo()
    {
        var room = new GameRoom { CreatorNo = "host" };
        room.Seats[1] = new MajakPlayer { MemberNo = "banishUser", Pix = "pix-banish" };
        room.BanishInfo.PreBanishing = true;
        room.BanishInfo.ReserveBanishing = true;
        room.BanishInfo.ReserveMemberNo = "banishUser";

        var payload = MajakServer.Commands.Room.RoomGetMembersCommand.BuildMemberListPayload(room);

        Assert.Equal(1, Convert.ToInt32(payload[GKey.PreBanishing]));
        Assert.Equal(1, Convert.ToInt32(payload[GKey.ReserveBanishing]));
        Assert.Equal("pix-banish", payload[GKey.Pix]);
    }

    [Fact]
    public void BuildMemberListPayload_IncludesLegacyCustomCostumeFields()
    {
        var player = new MajakPlayer { MemberNo = "host", NickName = "Host", CustomItems = new() };
        player.CustomItems[100001] = new UserCustomItem { Kind = 30, Equip = 1 };
        var room = new GameRoom { CreatorNo = "host" };
        room.Seats[0] = player;

        var payload = MajakServer.Commands.Room.RoomGetMembersCommand.BuildMemberListPayload(room);

        Assert.Equal(100001, Convert.ToInt32(payload[$"{Key.CustomCostume}0"]));
        Assert.Equal(30, Convert.ToInt32(payload[$"{Key.CustomCostumeType}0"]));
    }

    [Fact]
    public void BuildMemberListPayload_IncludesOkButtonReadyState()
    {
        var player = new MajakPlayer { MemberNo = "readyUser", NickName = "Ready User", SeatPos = 1 };
        var room = new GameRoom { CreatorNo = "host" };
        room.Seats[1] = player;
        room.OkButtonStates[1] = true;

        var payload = MajakServer.Commands.Room.RoomGetMembersCommand.BuildMemberListPayload(room);

        Assert.Equal(true, payload["ready0"]);
        Assert.Equal(true, payload["isReady0"]);
        Assert.Equal(1, Convert.ToInt32(payload["okButton0"]));
    }

    [Fact]
    public void BuildMemberListPayload_MarksHostByPixInStructuredMembers()
    {
        var host = new MajakPlayer { MemberNo = "host-member-no", Pix = "host-pix", NickName = "Host", SeatPos = 0 };
        var room = new GameRoom { CreatorNo = host.MemberNo };
        room.Seats[0] = host;

        var payload = MajakServer.Commands.Room.RoomGetMembersCommand.BuildMemberListPayload(room);
        var members = Assert.IsAssignableFrom<IEnumerable<object>>(payload["members"]);
        var hostMember = JsonSerializer.SerializeToElement(Assert.Single(members));

        Assert.Equal("host-pix", payload[GKey.RoomHost]);
        Assert.True(hostMember.GetProperty("isHost").GetBoolean());
    }

    [Fact]
    public void BuildAddMemberPayload_IncludesLegacyRoomExtensionFields()
    {
        var player = new MajakPlayer
        {
            MemberNo = "host",
            NickName = "Host",
            SeatPos = 0,
            TrickTitle = "7",
            MajakTitle = "8",
            CustomItems = new(),
        };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "item001",
            UseFlag = true,
            EndDt = DateTime.Now.AddDays(1),
            Qty = 1,
        });
        player.CustomItems[100001] = new UserCustomItem { Kind = 30, Equip = 1 };
        var room = new GameRoom { CreatorNo = "host" };
        room.Seats[0] = player;

        var payload = MajakServer.Commands.Room.RoomGetMembersCommand.BuildAddMemberPayload(room, player);

        Assert.Equal("Host", payload[Key.NickName]);
        Assert.Equal("7", payload[Key.TrickTitle]);
        Assert.Equal("8", payload[Key.MajakTitle]);
        Assert.Equal(1, payload[Key.RichiEffect]);
        Assert.Equal(100001, payload[Key.CustomCostume]);
        Assert.Equal(30, payload[Key.CustomCostumeType]);
    }

    [Fact]
    public void BuildDeleteMemberPayload_UsesLegacyRoomMemberFields()
    {
        var player = new MajakPlayer { MemberNo = "u1", NickName = "User One" };

        var payload = MajakServer.Commands.Room.RoomGetMembersCommand.BuildDeleteMemberPayload(
            "host", player, GKey.ValueViewer, 3);

        Assert.Equal("host", payload[GKey.RoomHost]);
        Assert.Equal(GKey.ValueViewer, payload[GKey.PlayerType]);
        Assert.Equal(3, payload[GKey.PlayerPos]);
        Assert.Equal("u1", payload[GKey.Pix]);
        Assert.Equal("User One", payload[GKey.Name]);
        Assert.False(payload.ContainsKey("roomHost"));
        Assert.False(payload.ContainsKey("playerType"));
        Assert.False(payload.ContainsKey("playerPos"));
        Assert.False(payload.ContainsKey("seatPos"));
        Assert.False(payload.ContainsKey("memberNo"));
        Assert.False(payload.ContainsKey("name"));
        Assert.False(payload.ContainsKey("nickName"));
    }

    // ─── BuildRuleInfo ────────────────────────────────────────────────────
    // 原典: RoomOption 文字列から ルールを解析

    private static Engine.RuleInfo InvokeBuildRuleInfo(GameRoom room)
    {
        var method = typeof(GameLogicService)
            .GetMethod("BuildRuleInfo",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Engine.RuleInfo)method.Invoke(null, new object[] { room })!;
    }

    private static Dictionary<string, object?> InvokeBuildHanchanInfo(GameRoom room)
    {
        var method = typeof(GameLogicService)
            .GetMethod("BuildHanchanInfo",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return CommandTestHelper.ToDict(method.Invoke(null, new object[] { room })!);
    }

    private static void InvokePrepareTrainingNpcProfiles(GameRoom room)
    {
        var method = typeof(GameLogicService)
            .GetMethod("PrepareTrainingNpcProfiles",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        method.Invoke(null, new object[] { room });
    }

    private static Dictionary<string, object?> InvokeBuildGameResultPayload(GameRoom room, GameReport report)
    {
        var method = typeof(GameLogicService)
            .GetMethod("BuildGameResultPayload",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
        return CommandTestHelper.ToDict(method.Invoke(BuildService(), new object[] { room, report })!);
    }

    private static async Task<List<Dictionary<string, object?>>> InvokeSendPaiInfoToAll(GameRoom room)
    {
        var sent = new List<object>();
        var clientProxy = new Mock<ISingleClientProxy>();
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                if (method == Cmd.PaiInfoList) sent.Add(args[0]!);
            })
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(clientProxy.Object);
        var ctx = new CommandContext { Clients = clientsMock.Object };
        var method = typeof(GameLogicService)
            .GetMethod("SendPaiInfoToAllAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(BuildService(), new object[] { room, ctx, true })!;
        return sent.Select(CommandTestHelper.ToDict).ToList();
    }

    private static async Task<GameReport> InvokeMakeGameReport(GameRoom room)
    {
        var method = typeof(GameLogicService)
            .GetMethod("MakeGameReportAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctx = new CommandContext { Payload = new Dictionary<string, object?>() };
        return (await (Task<GameReport?>)method.Invoke(BuildService(), new object[] { room, ctx })!)!;
    }

    private static async Task InvokeSendGetGemAsync(GameLogicService svc, GameRoom room, GameReport report, CommandContext ctx)
    {
        var method = typeof(GameLogicService)
            .GetMethod("SendGetGemAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(svc, new object[] { room, report, ctx })!;
    }

    private static async Task InvokeCheckTitleClearAsync(GameLogicService svc, MajakPlayer player, CommandContext ctx)
    {
        var method = typeof(GameLogicService)
            .GetMethod("CheckTitleClearAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(svc, new object[] { player, ctx })!;
    }

    private static async Task InvokeAwardGameIconsAsync(GameLogicService svc, MajakPlayer player, GameReport.UserResult user)
    {
        var method = typeof(GameLogicService)
            .GetMethod("AwardGameIconsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(svc, new object[] { player, user })!;
    }

    private static async Task InvokeUpdateGradeResultSideEffectsAsync(GameLogicService svc, MajakPlayer player, GameReport.UserResult user)
    {
        var method = typeof(GameLogicService)
            .GetMethod("UpdateGradeResultSideEffectsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(svc, new object[] { player, user })!;
    }

    private static async Task InvokeApplyPlayParkMissionsAsync(GameLogicService svc, GameReport report)
    {
        var method = typeof(GameLogicService)
            .GetMethod("ApplyPlayParkMissionsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(svc, new object[] { report })!;
    }

    private static async Task InvokeApplyMissionEventCmsAsync(GameLogicService svc, GameRoom room, GameReport report, DateTime now)
    {
        var method = typeof(GameLogicService)
            .GetMethod("ApplyMissionEventCmsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(svc, new object[] { room, report, now })!;
    }

    private static string[] InvokeGetGameIconCodesForReport(MajakPlayer player, GameReport.UserResult user)
    {
        var method = typeof(GameLogicService)
            .GetMethod("GetGameIconCodesForReport", BindingFlags.NonPublic | BindingFlags.Static)!;
        return ((IEnumerable<string>)method.Invoke(null, new object[] { player, user })!).ToArray();
    }

    private static int InvokeCalcGemCountToGet(GameRoom room, int order, MajakPlayer player)
    {
        var method = typeof(GameLogicService)
            .GetMethod("CalcGemCountToGet", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)method.Invoke(null, new object[] { room, order, player })!;
    }

    private static GameRoom BuildPaiInfoRoom(string subId)
    {
        var room = new GameRoom { RoomId = 80, SubId = subId, RoomOption = "120000001000000" };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"p{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
            }, seat);
        }
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, AkaDora = 1, Uma = 0, Contest = 0 });
        return room;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5_000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("The expected game state was not reached.");
            await Task.Delay(20);
        }
    }

    // シナリオ: DATAFORMAT_HANCHANINFO.nPlayer[order] は engine order → room/player position
    // 原典: HMajRoomServer::AddToParser_HanchanInfo / AddToParser_PlayHistory
    [Fact]
    public void BuildHanchanInfo_KeepsRoomSeatAndEngineOrderSeparate()
    {
        var room = new GameRoom { RoomId = 10 };
        room.AddPlayer(new MajakPlayer { MemberNo = "seat0", NickName = "P0", ConnectionId = "c0" }, 0);
        room.AddPlayer(new MajakPlayer { MemberNo = "seat1", NickName = "P1", ConnectionId = "c1" }, 1);
        room.AddPlayer(new MajakPlayer { MemberNo = "seat2", NickName = "P2", ConnectionId = "c2" }, 2);
        room.AddPlayer(new MajakPlayer { MemberNo = "seat3", NickName = "P3", ConnectionId = "c3" }, 3);
        room.Engine.HanchanInfo = new Engine.HanchanInfo
        {
            Chicha = 1,
            CurKyoku = 0,
            RenchanCount = 0,
            Player = new[] { 2, 0, 3, 1 },
        };

        var packet = InvokeBuildHanchanInfo(room);

        Assert.Equal("MJPID_INIHAN", ((JsonElement)packet["playType"]!).GetString());
        Assert.Equal(1, ((JsonElement)packet["chicha"]!).GetInt32());
        Assert.Equal(new[] { 2, 0, 3, 1 }, ((JsonElement)packet["players"]!).EnumerateArray().Select(e => e.GetInt32()).ToArray());

        var memberInfo = ((JsonElement)packet["memberInfo"]!).EnumerateArray().ToArray();
        Assert.Equal("seat2", memberInfo[0].GetProperty("memberNo").GetString());
        Assert.Equal(2, memberInfo[0].GetProperty("playerPos").GetInt32());
        Assert.Equal(2, memberInfo[0].GetProperty("seatPos").GetInt32());
        Assert.Equal(0, memberInfo[0].GetProperty("engineOrder").GetInt32());
        Assert.Equal("seat0", memberInfo[1].GetProperty("memberNo").GetString());
        Assert.Equal(0, memberInfo[1].GetProperty("playerPos").GetInt32());
        Assert.Equal(1, memberInfo[1].GetProperty("engineOrder").GetInt32());
    }

    [Fact]
    public void TrainingNpcProfile_IsStableAcrossHanchanAndGameResultPayloads()
    {
        var room = new GameRoom { RoomId = 11, SubId = "00T5A", RoomOption = "120000001000000" };
        room.AddPlayer(new MajakPlayer { MemberNo = "host", Pix = "host-pix", NickName = "Host", ConnectionId = "c0" }, 0);
        room.Engine.InitHanchan(new RuleInfo());
        room.Engine.HanchanInfo = new Engine.HanchanInfo
        {
            Player = new[] { 0, 1, 2, 3 },
        };
        InvokePrepareTrainingNpcProfiles(room);

        var npcProfiles = room.TrainingNpcProfiles.Where(profile => profile != null).Select(profile => profile!).ToArray();
        Assert.Equal(new[] { "NPC 1", "NPC 2", "NPC 3" }, npcProfiles.Select(profile => profile.Name).ToArray());
        Assert.Equal(3, npcProfiles.Select(profile => profile.AvatarId).Distinct().Count());
        Assert.All(npcProfiles, profile =>
        {
            Assert.StartsWith("thumbnail_", profile.AvatarId);
            Assert.EndsWith(".png", profile.AvatarId);
        });

        var hanchanPayload = InvokeBuildHanchanInfo(room);
        var memberInfo = ((JsonElement)hanchanPayload["memberInfo"]!).EnumerateArray().ToArray();
        Assert.Equal("NPC 1", memberInfo[1].GetProperty("name").GetString());
        Assert.Equal(npcProfiles[0].AvatarId, memberInfo[1].GetProperty("avatarId").GetString());

        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "host", Ranking = 1 };
        for (int seat = 1; seat < GameConst.PlayerMaxCount; seat++)
            report.Users[seat] = new GameReport.UserResult { MemberNo = TournamentConst.NpcMemberNo, Ranking = seat + 1 };
        var resultPayload = InvokeBuildGameResultPayload(room, report);
        var users = ((JsonElement)resultPayload["users"]!).EnumerateArray().ToArray();

        Assert.Equal("NPC 1", users[1].GetProperty("name").GetString());
        Assert.Equal(memberInfo[1].GetProperty("avatarId").GetString(), users[1].GetProperty("avatarId").GetString());
        Assert.Equal(TournamentConst.NpcMemberNo, users[1].GetProperty("pix").GetString());
    }

    [Fact]
    public async Task OnInitKyoku_SendsLegacyKyokuInfoFieldsAndStoresHistory()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 81;
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, AkaDora = 1, Uma = 0, Contest = 0, Yakitori = true });
        room.Engine.HanchanInfo.CurKyoku = 4;
        room.Engine.HanchanInfo.RenchanCount = 3;
        room.Engine.KyokuInfo.RibouCount = 2;
        room.Engine.KyokuInfo.Dice[0] = 1;
        room.Engine.KyokuInfo.Dice[1] = 2;
        for (int order = 0; order < GameConst.PlayerMaxCount; order++)
        {
            room.Engine.Player[order].GamePoint = 25000 + order * 1000;
            if (order % 2 == 1) room.Engine.Player[order].ClearYakitori();
            room.Engine.Player[order].Tip = order + 5;
        }
        room.PlayHistory.Add(new { playType = "OLD" });

        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService().OnInitKyokuAsync(room, ctx);

        var packet = CommandTestHelper.ToDict(Assert.Single(sent, s => s.method == Cmd.GamePlay).packet);
        Assert.Equal("MJPID_INIKYO", ((JsonElement)packet["playType"]!).GetString());
        Assert.Equal(4, ((JsonElement)packet["kyokuCnt"]!).GetInt32());
        Assert.Equal(2, ((JsonElement)packet["riboCnt"]!).GetInt32());
        Assert.Equal(3, ((JsonElement)packet["renChanCnt"]!).GetInt32());
        Assert.Equal(-1, ((JsonElement)packet["waremeOdr"]!).GetInt32());
        Assert.Equal(new[] { 1, 2 }, ((JsonElement)packet["dice"]!).EnumerateArray().Select(e => e.GetInt32()).Take(2).ToArray());
        Assert.Equal(new[] { 25000, 26000, 27000, 28000 }, ((JsonElement)packet["memberPoints"]!).EnumerateArray().Select(e => e.GetInt32()).ToArray());
        Assert.Equal(new[] { true, false, true, false }, ((JsonElement)packet["yakitori"]!).EnumerateArray().Select(e => e.GetBoolean()).ToArray());
        Assert.Equal(new[] { 5, 6, 7, 8 }, ((JsonElement)packet["tip"]!).EnumerateArray().Select(e => e.GetInt32()).ToArray());

        Assert.Equal(2, room.PlayHistory.Count);
        Assert.Equal("MJPID_INIHAN", ((JsonElement)CommandTestHelper.ToDict(room.PlayHistory[0])["playType"]!).GetString());
        Assert.Equal("MJPID_INIKYO", ((JsonElement)CommandTestHelper.ToDict(room.PlayHistory[1])["playType"]!).GetString());
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(true, 0)]
    public async Task OnInitKyoku_WaremeOdr_FollowsWaremeRule(bool wareme, int expectedWaremeOdr)
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = wareme ? 83 : 82;
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, Wareme = wareme });
        room.Engine.KyokuInfo.Dice[0] = 1;
        room.Engine.KyokuInfo.Dice[1] = 2;
        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService().OnInitKyokuAsync(room, ctx);

        var packet = CommandTestHelper.ToDict(Assert.Single(sent, s => s.method == Cmd.GamePlay).packet);
        Assert.Equal(expectedWaremeOdr, ((JsonElement)packet["waremeOdr"]!).GetInt32());
    }

    [Fact]
    public async Task SendCurrentActionPrompt_ReissuesPromptWhenReturningPlayerHasNoPendingAction()
    {
        var session = new PlayerSessionService();
        var room = new GameRoom { RoomId = 90, ChannelId = "ch1", RoomOption = "00201210110122" };
        for (int seat = 0; seat < GameConst.PlayerMaxCount; seat++)
        {
            var player = new MajakPlayer
            {
                MemberNo = $"u{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
                ChannelId = "ch1",
                EngineOrder = seat,
            };
            session.Register(player);
            room.AddPlayer(player, seat);
            room.Seats[seat]!.EngineOrder = seat;
        }
        room.State = GameRoomState.Playing;
        room.Engine.Player[2].Mode = PlayerMode.Turn;
        room.Engine.Player[2].Tehai.Add(new PaiCode(0, 1) { BipaiIndex = 77 });

        var returningPlayer = room.Seats[2]!;
        var (ctx, sent) = CommandTestHelper.MakeContext(returningPlayer);

        await BuildService(session).SendCurrentActionPromptAsync(room, ctx, returningPlayer);

        var packet = CommandTestHelper.ToDict(sent.Last(s => s.method == Cmd.GamePlay).packet);
        Assert.Equal("MJPID_ACTIONS", ((JsonElement)packet["playType"]!).GetString());
        Assert.Equal(2, ((JsonElement)packet["seatOrder"]!).GetInt32());
        Assert.Equal("Turn", ((JsonElement)packet["playerMode"]!).GetString());
        Assert.True(((JsonElement)packet["actionSeq"]!).GetInt64() > 0);
        Assert.NotNull(room.PendingActions[2]);
        Assert.Equal(room.PendingActions[2]!.ActionSeq, ((JsonElement)packet["actionSeq"]!).GetInt64());
    }

    [Fact]
    public async Task MakeGameReport_UsesSeatToEngineOrderMappingAndOutPlayerFlag()
    {
        var room = new GameRoom { RoomId = 11, ChannelId = "ch1", MoneyRate = 1, RoomOption = "12301210110122" };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"seat{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
            }, seat);
        }

        room.Seats[2]!.IsOutPlayer = true;
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, AkaDora = 1, Yakitori = true });
        room.Engine.HanchanInfo.Player = new[] { 2, 0, 3, 1 };
        Array.Fill(room.SeatToEngineOrder, -1);
        room.SeatToEngineOrder[2] = 0;
        room.SeatToEngineOrder[0] = 1;
        room.SeatToEngineOrder[3] = 2;
        room.SeatToEngineOrder[1] = 3;

        room.Engine.Player[0].SetRank = 0;
        room.Engine.Player[0].GamePoint = 42000;
        room.Engine.Player[0].SetPoint = 12000;
        room.Engine.Player[0].SetTotal = 12000;
        room.Engine.Player[0].Tip = 27;
        room.Engine.Player[0].ResultRecord.TipPoint = 4;
        room.Engine.Player[0].ResultRecord.TipMatchCnt = 1;

        var report = await InvokeMakeGameReport(room);

        Assert.Equal("seat2", report.Users[2]!.MemberNo);
        Assert.False(report.Users[2]!.IsConnect);
        Assert.Equal(1, report.Users[2]!.Ranking);
        Assert.Equal(42000, report.Users[2]!.Score);
        Assert.Equal(42000, report.Users[2]!.GameScore);
        Assert.Equal(12000, report.Users[2]!.SetPoint);
        Assert.Equal(4, report.Users[2]!.TipPoint);
        Assert.Equal(1, report.Users[2]!.TipMatchCnt);
        Assert.True(report.Users[2]!.Yakitori);
        Assert.Equal(27, report.Users[2]!.Chip);
        Assert.Equal("H2s-Yro-AN-CtG", report.RoomOption);
        Assert.Equal("12301210110122", room.RoomOption);
    }

    [Fact]
    public async Task MakeGameReport_TrainingEmptyEngineSeats_AddsNpcRows()
    {
        var room = new GameRoom { RoomId = 12, ChannelId = "ch1", SubId = "00T5A", MoneyRate = 1 };
        room.AddPlayer(new MajakPlayer
        {
            MemberNo = "seat0",
            NickName = "P0",
            ConnectionId = "c0",
        }, 0);
        room.Engine.InitHanchan(new RuleInfo { Yakitori = true, Tip = true });
        room.Engine.HanchanInfo = new Engine.HanchanInfo { Player = new[] { 0, 1, 2, 3 } };
        Array.Fill(room.SeatToEngineOrder, -1);
        room.SeatToEngineOrder[0] = 0;

        room.Engine.Player[1].SetRank = 1;
        room.Engine.Player[1].GamePoint = 26000;
        room.Engine.Player[1].SetPoint = 1000;
        room.Engine.Player[1].SetTotal = 1000;
        room.Engine.Player[1].Tip = 25;

        var report = await InvokeMakeGameReport(room);

        Assert.Equal("seat0", report.Users[0]!.MemberNo);
        Assert.Equal(TournamentConst.NpcMemberNo, report.Users[1]!.MemberNo);
        Assert.False(report.Users[1]!.IsConnect);
        Assert.False(report.Users[1]!.Connected);
        Assert.Equal(2, report.Users[1]!.Ranking);
        Assert.Equal(26000, report.Users[1]!.Score);
        Assert.Equal(1000, report.Users[1]!.SetPoint);
        Assert.True(report.Users[1]!.Yakitori);
        Assert.Equal(25, report.Users[1]!.Chip);
    }

    // シナリオ: SendPaiInfoToAll はトーナメントチャンネルのプレイヤーに全公開情報を送る
    // 原典: HMajRoomServer::SendPaiInfo -> CT_TRAINING || CT_TOURNAMENT で openMask 全開
    [Fact]
    public async Task SendPaiInfoToAll_PaiPayloadUsesNumericTileCode()
    {
        var packets = await InvokeSendPaiInfoToAll(BuildPaiInfoRoom("00N5A"));

        Assert.Equal(4, packets.Count);
        var pai = ((JsonElement)packets[0]["pai"]!).EnumerateArray().ToArray();
        Assert.NotEmpty(pai);
        var first = pai[0];
        Assert.Equal(JsonValueKind.Number, first.GetProperty("code").ValueKind);
        Assert.InRange(first.GetProperty("code").GetInt32(), 1, 0x37);
        Assert.Equal(JsonValueKind.Number, first.GetProperty("idx").ValueKind);
        Assert.Equal(JsonValueKind.True, first.GetProperty("red").ValueKind);
    }

    [Fact]
    public async Task SendPaiInfoToAll_TournamentPlayerReceivesOpenHandInfo()
    {
        var normalPackets = await InvokeSendPaiInfoToAll(BuildPaiInfoRoom("00N5A"));
        var tournamentPackets = await InvokeSendPaiInfoToAll(BuildPaiInfoRoom("00H5A"));

        Assert.Equal(4, normalPackets.Count);
        Assert.Equal(4, tournamentPackets.Count);
        var normalMax = normalPackets.Max(packet => ((JsonElement)packet["paiCount"]!).GetInt32());
        var tournamentMin = tournamentPackets.Min(packet => ((JsonElement)packet["paiCount"]!).GetInt32());
        Assert.True(tournamentMin > normalMax,
            $"Tournament full-open PaiInfo should expose more tiles than normal own-hand PaiInfo. normalMax={normalMax}, tournamentMin={tournamentMin}");
    }

    // シナリオ: GamePlayProcess は ProcessAction 後に smmc4e → playing/MJPID_ACTION の順で送る
    // 原典: HMajRoomServer::GamePlayProcess response 1 SendPaiInfoToAll → response 2 AddToParser_ActionInfo
    [Fact]
    public async Task GamePlayProcess_SendsPaiInfoBeforeActionPacket()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 81;
        int order = Array.FindIndex(room.Engine.Player, p => p.Mode == PlayerMode.Turn);
        Assert.InRange(order, 0, GameConst.PlayerMaxCount - 1);
        var player = room.Seats[room.Engine.HanchanInfo.Player[order]]!;
        player.EngineOrder = order;
        var tapBipaiIndex = Assert.Single(room.Engine.GetValidActions(order).TapCandidates.TakeLast(1));
        room.PendingActions[player.EngineOrder] = new PendingActionPrompt
        {
            ActionSeq = 123,
            SeatOrder = player.EngineOrder,
            PlayerMode = room.Engine.Player[player.EngineOrder].Mode,
            IssuedAt = DateTimeOffset.UtcNow,
            DeadlineAt = DateTimeOffset.UtcNow.AddSeconds(5),
        };

        var sent = new List<(string method, object packet)>();
        var singleClientProxy = new Mock<ISingleClientProxy>();
        singleClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var multiClientProxy = new Mock<IClientProxy>();
        multiClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(singleClientProxy.Object);
        clientsMock.Setup(c => c.Group($"room_{room.RoomId}")).Returns(groupProxy.Object);
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(multiClientProxy.Object);
        clientsMock.Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>())).Returns(multiClientProxy.Object);
        var ctx = new CommandContext
        {
            Player = player,
            Clients = clientsMock.Object,
            Payload = new Dictionary<string, object?>
            {
                ["seatOrder"] = player.EngineOrder,
                ["action"] = (int)MajakServer.Engine.Act.Tap,
                ["bipaiIndex"] = new[] { tapBipaiIndex },
                ["actionSeq"] = 123L,
            },
        };

        await BuildService().GamePlayProcessAsync(room, ctx);

        var firstPaiInfoIndex = sent.FindIndex(packet => packet.method == Cmd.PaiInfoList);
        var firstActionIndex = sent.FindIndex(packet =>
            packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_ACTION");

        Assert.InRange(firstPaiInfoIndex, 0, int.MaxValue);
        Assert.InRange(firstActionIndex, 0, int.MaxValue);
        Assert.True(firstPaiInfoIndex < firstActionIndex, $"Expected PaiInfo before action packet. order={string.Join(',', sent.Select(s => s.method))}");
        var actionPacket = CommandTestHelper.ToDict(sent[firstActionIndex].packet);
        Assert.Equal(player.EngineOrder, ((JsonElement)actionPacket["seatOrder"]!).GetInt32());
        Assert.Equal((int)MajakServer.Engine.Act.Tap, ((JsonElement)actionPacket["action"]!).GetInt32());
        Assert.False(actionPacket.ContainsKey("leftCount"));
        Assert.False(actionPacket.ContainsKey("isYakuman"));
        Assert.False(actionPacket.ContainsKey("mangan"));
        Assert.False(actionPacket.ContainsKey("hanSum"));
        Assert.Contains(room.PlayHistory, history =>
            ((JsonElement)CommandTestHelper.ToDict(history)["playType"]!).GetString() == "MJPID_ACTION");
    }

    // シナリオ: deadline を過ぎた MJPID_ACTION は stale input として無視する
    [Fact]
    public async Task GamePlayProcess_ExpiredPendingActionIsIgnored()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 182;
        var player = room.Seats[room.Engine.HanchanInfo.Player[room.Engine.KyokuInfo.OyaOrder]]!;
        player.EngineOrder = room.Engine.KyokuInfo.OyaOrder;
        var tapTile = room.Engine.Player[player.EngineOrder].Tehai.Last();
        room.PendingActions[player.EngineOrder] = new PendingActionPrompt
        {
            ActionSeq = 123,
            SeatOrder = player.EngineOrder,
            PlayerMode = room.Engine.Player[player.EngineOrder].Mode,
            IssuedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
            DeadlineAt = DateTimeOffset.UtcNow.AddMilliseconds(-1),
        };

        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            ["seatOrder"] = player.EngineOrder,
            ["action"] = (int)MajakServer.Engine.Act.Tap,
            ["bipaiIndex"] = new[] { tapTile.BipaiIndex },
            ["actionSeq"] = 123L,
        });

        await BuildService().GamePlayProcessAsync(room, ctx);

        Assert.Empty(sent);
        Assert.NotNull(room.PendingActions[player.EngineOrder]);
        Assert.Equal(123, room.PendingActions[player.EngineOrder]!.ActionSeq);
        Assert.Empty(room.PlayHistory);
    }

    // シナリオ: server timeout が先に処理した後に同じ actionSeq の遅延 MJPID_ACTION が届いても切断しない
    [Fact]
    public async Task GamePlayProcess_LateActionAfterTimeoutIsIgnoredWithoutAbort()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 183;
        var player = room.Seats[room.Engine.HanchanInfo.Player[room.Engine.KyokuInfo.OyaOrder]]!;
        player.EngineOrder = room.Engine.KyokuInfo.OyaOrder;
        var tapTile = room.Engine.Player[player.EngineOrder].Tehai.Last();
        room.PendingActions[player.EngineOrder] = null;
        string? abortReason = null;
        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            ["seatOrder"] = player.EngineOrder,
            ["action"] = (int)MajakServer.Engine.Act.Tap,
            ["bipaiIndex"] = new[] { tapTile.BipaiIndex },
            ["actionSeq"] = 123L,
        }, reason => abortReason = reason);

        await BuildService().GamePlayProcessAsync(room, ctx);

        Assert.Empty(sent);
        Assert.Null(abortReason);
        Assert.Empty(room.PlayHistory);
    }

    // シナリオ: ProcessAction 失敗時は GamePlayProcess が FALSE を返すレガシー経路に合わせて CloseSocket 相当
    // 原典: HMajRoomServer::GamePlayProcess invalid action → FALSE、ProcessCommand_GamePlay が CloseSocket
    [Fact]
    public async Task GamePlayProcess_InvalidAction_AbortsConnectionWithoutPackets()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 82;
        var player = room.Seats[room.Engine.HanchanInfo.Player[room.Engine.KyokuInfo.OyaOrder]]!;
        player.EngineOrder = room.Engine.KyokuInfo.OyaOrder;
        string? abortReason = null;
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                ["seatOrder"] = player.EngineOrder,
                ["action"] = (int)MajakServer.Engine.Act.Pas,
                ["bipaiIndex"] = Array.Empty<int>(),
            },
            reason => abortReason = reason);

        await BuildService().GamePlayProcessAsync(room, ctx);

        Assert.Empty(sent);
        Assert.Contains("Engine rejected action", abortReason);
        Assert.Empty(room.PlayHistory);
    }

    // シナリオ: action 後に GAMESTATUS_NEWKYOKU へ進んだ場合、MJPID_ACTION の後に MJPID_INIKYO を送る
    // 原典: GamePlayProcess response 2 AddToParser_ActionInfo → response 3 OnInitKyoku
    [Fact]
    public async Task GamePlayProcess_NewKyoku_SendsInikyoAfterActionAndResetsHistory()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 83;
        var player = room.Seats[0]!;
        player.EngineOrder = 0;
        room.Engine.Player[0].Mode = PlayerMode.Kyo;
        for (int order = 1; order < 4; order++) room.Engine.Player[order].Mode = PlayerMode.None;

        var sent = new List<(string method, object packet)>();
        var singleClientProxy = new Mock<ISingleClientProxy>();
        singleClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var multiClientProxy = new Mock<IClientProxy>();
        multiClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(singleClientProxy.Object);
        clientsMock.Setup(c => c.Group($"room_{room.RoomId}")).Returns(groupProxy.Object);
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(multiClientProxy.Object);
        clientsMock.Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>())).Returns(multiClientProxy.Object);
        var ctx = new CommandContext
        {
            Player = player,
            Clients = clientsMock.Object,
            Payload = new Dictionary<string, object?>
            {
                ["seatOrder"] = 0,
                ["action"] = (int)MajakServer.Engine.Act.Pas,
                ["bipaiIndex"] = Array.Empty<int>(),
            },
        };

        await BuildService().GamePlayProcessAsync(room, ctx);

        var actionIndex = sent.FindIndex(packet =>
            packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_ACTION");
        var inikyoIndex = sent.FindIndex(packet =>
            packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_INIKYO");

        Assert.InRange(actionIndex, 0, int.MaxValue);
        Assert.InRange(inikyoIndex, 0, int.MaxValue);
        Assert.True(actionIndex < inikyoIndex, $"Expected MJPID_ACTION before MJPID_INIKYO. order={string.Join(',', sent.Select(s => s.method))}");
        Assert.DoesNotContain(room.PlayHistory, history =>
            ((JsonElement)CommandTestHelper.ToDict(history)["playType"]!).GetString() == "MJPID_ACTION");
        Assert.Contains(room.PlayHistory, history =>
            ((JsonElement)CommandTestHelper.ToDict(history)["playType"]!).GetString() == "MJPID_INIHAN");
        Assert.Contains(room.PlayHistory, history =>
            ((JsonElement)CommandTestHelper.ToDict(history)["playType"]!).GetString() == "MJPID_INIKYO");
    }

    // シナリオ: action 後に GAMESTATUS_ENDKYOKU へ進んだ場合、MJPID_ACTION の後に OnEndKyoku を実行する
    // 原典: GamePlayProcess response 2 AddToParser_ActionInfo → case GAMESTATUS_ENDKYOKU: OnEndKyoku()
    [Fact]
    public async Task GamePlayProcess_EndKyoku_SendsEndKyoAfterActionThenPromptsKyoPass()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 85;
        var player = room.Seats[0]!;
        player.EngineOrder = 0;
        room.Engine.Player[0].Mode = PlayerMode.Turn;
        for (int order = 1; order < 4; order++) room.Engine.Player[order].Mode = PlayerMode.None;
        room.Engine.Player[0].Tehai.Clear();
        foreach (var serial in new[] { 0, 8, 9, 17, 18, 26, 27, 28, 29, 1, 2, 3, 4, 5 })
        {
            var pai = PaiCode.MakeSerial(serial);
            pai.BipaiIndex = serial + 100;
            room.Engine.Player[0].Tehai.Add(pai);
        }

        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            ["seatOrder"] = 0,
            ["action"] = (int)MajakServer.Engine.Act.Tao,
            ["bipaiIndex"] = Array.Empty<int>(),
        });

        await BuildService().GamePlayProcessAsync(room, ctx);

        var actionIndex = sent.FindIndex(packet =>
            packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_ACTION");
        var endKyoIndex = sent.FindIndex(packet =>
            packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_ENDKYO");
        var actionsIndex = sent.FindIndex(packet =>
            packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_ACTIONS");
        var sentOrder = string.Join(',', sent.Select(s => s.method));

        Assert.True(actionIndex >= 0, $"Expected MJPID_ACTION. order={sentOrder}");
        Assert.True(endKyoIndex >= 0, $"Expected MJPID_ENDKYO. order={sentOrder}");
        Assert.True(actionsIndex >= 0, $"Expected MJPID_ACTIONS. order={sentOrder}");
        Assert.True(actionIndex < endKyoIndex, $"Expected MJPID_ACTION before MJPID_ENDKYO. order={sentOrder}");
        Assert.True(endKyoIndex < actionsIndex, $"Expected MJPID_ENDKYO before MJPID_ACTIONS. order={sentOrder}");
        Assert.Equal(KyokuEnd.Taopai, room.Engine.KyokuEnd);
    }

    // シナリオ: action 後に GAMESTATUS_NOTPLAYING へ進んだ場合、MJPID_ACTION の後に OnEndGame を実行して終了通知する
    // 原典: GamePlayProcess response 2 AddToParser_ActionInfo → case GAMESTATUS_NOTPLAYING: OnEndGame(); return TRUE
    [Fact]
    public async Task GamePlayProcess_NotPlaying_SendsActionThenGameReportAndStopsPrompts()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 84;
        var player = room.Seats[0]!;
        player.EngineOrder = 0;
        room.Engine.HanchanInfo.CurKyoku = 7;
        room.Engine.KyokuInfo.Renchan = false;
        room.Engine.KyokuInfo.EndKyokuWithHora = false;
        room.Engine.Player[0].Mode = PlayerMode.Kyo;
        for (int order = 1; order < 4; order++) room.Engine.Player[order].Mode = PlayerMode.None;
        room.PendingActions[0] = new PendingActionPrompt
        {
            ActionSeq = 123,
            SeatOrder = 0,
            PlayerMode = PlayerMode.Kyo,
            IssuedAt = DateTimeOffset.UtcNow,
            DeadlineAt = DateTimeOffset.UtcNow.AddSeconds(5),
        };

        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            ["seatOrder"] = 0,
            ["action"] = (int)MajakServer.Engine.Act.Pas,
            ["bipaiIndex"] = Array.Empty<int>(),
            ["actionSeq"] = 123L,
        });

        await BuildService().GamePlayProcessAsync(room, ctx);

        var actionIndex = sent.FindIndex(packet =>
            packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_ACTION");
        var reportIndex = sent.FindIndex(packet => packet.method == Cmd.GameReport);
        var roomStateIndex = sent.FindIndex(packet => packet.method == Cmd.RoomState);

        var sentOrder = string.Join(',', sent.Select(s => s.method));
        Assert.True(actionIndex >= 0, $"Expected MJPID_ACTION. order={sentOrder}");
        Assert.True(reportIndex >= 0, $"Expected game report. order={sentOrder}");
        Assert.True(roomStateIndex >= 0, $"Expected room state. order={sentOrder}");
        Assert.True(actionIndex < reportIndex, $"Expected MJPID_ACTION before game report. order={sentOrder}");
        Assert.DoesNotContain(sent, packet =>
            packet.method == Cmd.GamePlay
            && ((JsonElement)CommandTestHelper.ToDict(packet.packet)["playType"]!).GetString() == "MJPID_ACTIONS");
        Assert.All(room.PendingActions, Assert.Null);
        Assert.Equal(GameRoomState.Waiting, room.State);
        Assert.Empty(room.PlayHistory);
    }

    // シナリオ: GameReportProcess はレガシーの Report/ClearOutPlayerList/LimitCnt reset に合わせてルーム状態を戻す
    // 原典: GameReportProcess → Report() → GameReport() → ClearOutPlayerList() → LimitCnt=maxPlayer → SendChannelChangeRoomInfo
    [Fact]
    public async Task GameReportProcess_ResetsRoomAndClearsOutPlayersAfterReport()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 86;
        room.State = GameRoomState.Finished;
        room.LimitCnt = 2;
        room.PlayHistory.Add(new { playType = "MJPID_ACTION" });
        room.OkButtonStates[0] = true;
        room.OkButtonStates[1] = true;
        for (int seat = 0; seat < 4; seat++) room.SeatToEngineOrder[seat] = seat;
        room.Seats[1]!.IsOutPlayer = true;
        var (ctx, sent) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService().GameReportProcessAsync(room, ctx);

        var reportIndex = sent.FindIndex(packet => packet.method == Cmd.GameReport);
        var roomStateIndex = sent.FindIndex(packet => packet.method == Cmd.RoomState);
        var sentOrder = string.Join(',', sent.Select(s => s.method));

        Assert.True(reportIndex >= 0, $"Expected game report. order={sentOrder}");
        Assert.True(roomStateIndex >= 0, $"Expected room state. order={sentOrder}");
        Assert.True(reportIndex < roomStateIndex, $"Expected game report before room state. order={sentOrder}");
        Assert.NotNull(room.Seats[0]);
        Assert.Null(room.Seats[1]);
        Assert.Equal(GameRoomState.Waiting, room.State);
        Assert.Equal(GameConst.PlayerMaxCount, room.LimitCnt);
        Assert.Empty(room.PlayHistory);
        Assert.All(room.OkButtonStates, Assert.False);
    }

    [Fact]
    public async Task GameReportProcess_UsesLegacyResultCommonRatUpdate()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 87;
        room.State = GameRoomState.Finished;
        for (int seat = 0; seat < 4; seat++) room.SeatToEngineOrder[seat] = seat;
        room.Seats[1]!.IsOutPlayer = true;

        var session = new PlayerSessionService();
        foreach (var player in room.Seats.Where(p => p != null).Cast<MajakPlayer>())
        {
            player.ActiveRecord = player.HiClassRecord;
            session.Register(player);
        }

        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.UpdateResultCommonRatAsync(
                It.IsAny<MajakPlayer>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateHiClassRatAsync(It.IsAny<MajakPlayer>(), It.IsAny<int>(), It.IsAny<long>()))
            .Returns(Task.CompletedTask);
        var (ctx, _) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService(session, repo.Object).GameReportProcessAsync(room, ctx);

        repo.Verify(r => r.UpdateResultCommonRatAsync(
            It.Is<MajakPlayer>(p => p.MemberNo == "p0"), false,
            It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        repo.Verify(r => r.UpdateResultCommonRatAsync(
            It.Is<MajakPlayer>(p => p.MemberNo == "p1"), true,
            It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        repo.Verify(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()), Times.Never);
    }

    [Fact]
    public async Task GameReportProcess_NormalRoom_UpdatesDailyAndCasualPointMissions()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 187;
        room.State = GameRoomState.Finished;
        room.RoomOption = "0200000010000";
        for (int seat = 0; seat < 4; seat++) room.SeatToEngineOrder[seat] = seat;
        room.Engine.Player[0].SetRank = 0;
        room.Engine.Player[1].SetRank = 1;

        var session = new PlayerSessionService();
        foreach (var player in room.Seats.Where(p => p != null).Cast<MajakPlayer>())
            session.Register(player);

        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.SetDailyMissionDirectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.CallCasualPointUpdMissionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        var (ctx, _) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService(session, repo.Object).GameReportProcessAsync(room, ctx);

        repo.Verify(r => r.SetDailyMissionDirectAsync("p0", 2, 1), Times.Once);
        repo.Verify(r => r.SetDailyMissionDirectAsync("p0", 3, 1), Times.Once);
        repo.Verify(r => r.CallCasualPointUpdMissionAsync("p0", 1, 1, 1, It.IsAny<DateTime>()), Times.Once);
        repo.Verify(r => r.SetDailyMissionDirectAsync("p1", 2, 1), Times.Once);
        repo.Verify(r => r.CallCasualPointUpdMissionAsync("p1", 1, 0, 1, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task GameReportProcess_UsedBadaiFreeItem_DecrementsItemQuantity()
    {
        var room = BuildPaiInfoRoom("0ZG6A");
        room.RoomId = 189;
        room.State = GameRoomState.Finished;
        room.UnitMoney = 100;
        for (int seat = 0; seat < 4; seat++) room.SeatToEngineOrder[seat] = seat;
        foreach (var player in room.Seats.Where(p => p != null).Cast<MajakPlayer>())
            player.GamMoney = 10000;
        room.Seats[0]!.MajItems.Add(new MajItemInfo
        {
            ItemCode = "MJ20",
            Qty = 1,
            UseFlag = true,
            EndDt = DateTime.Now.AddDays(1),
        });

        var session = new PlayerSessionService();
        foreach (var player in room.Seats.Where(p => p != null).Cast<MajakPlayer>())
            session.Register(player);

        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.UpdateItemQuantityAsync(It.IsAny<MajakPlayer>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        var (ctx, _) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService(session, repo.Object).GameReportProcessAsync(room, ctx);

        repo.Verify(r => r.UpdateItemQuantityAsync(
            It.Is<MajakPlayer>(p => p.MemberNo == "p0"), "p0", "MJ20", -1), Times.Once);
    }

    [Fact]
    public async Task GameReportProcess_TrainingRoom_DoesNotUpdateResultMissions()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 188;
        room.State = GameRoomState.Finished;
        for (int seat = 0; seat < 4; seat++) room.SeatToEngineOrder[seat] = seat;

        var session = new PlayerSessionService();
        foreach (var player in room.Seats.Where(p => p != null).Cast<MajakPlayer>())
            session.Register(player);

        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        var (ctx, _) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService(session, repo.Object).GameReportProcessAsync(room, ctx);

        repo.Verify(r => r.SetDailyMissionDirectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        repo.Verify(r => r.CallCasualPointUpdMissionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task GameReportProcess_NormalRoom_WritesMySqlGameHistOnce()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 88;
        room.State = GameRoomState.Finished;
        for (int seat = 0; seat < 4; seat++) room.SeatToEngineOrder[seat] = seat;

        var history = new Mock<HistoryRepository>(MockBehavior.Loose);
        var log = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        history.Setup(r => r.InsertGameHistAsync(It.IsAny<GameReport>())).ReturnsAsync(1L);
        var (ctx, _) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService(historyRepo: history.Object, logRepo: log.Object).GameReportProcessAsync(room, ctx);

        history.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Once);
        history.Verify(r => r.InsertTrainingHistAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<(string MemberNo, int Point)[]>()), Times.Never);
        log.Verify(r => r.InsertTrainingHistAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<(string MemberNo, int Point)[]>()), Times.Never);
        log.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Never);
    }

    [Fact]
    public async Task GameReportProcess_DuplicateInvocation_DoesNotDoubleWriteHistory()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 1888;
        room.State = GameRoomState.Finished;
        for (int seat = 0; seat < 4; seat++) room.SeatToEngineOrder[seat] = seat;

        var history = new Mock<HistoryRepository>(MockBehavior.Loose);
        var log = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        history.Setup(r => r.InsertGameHistAsync(It.IsAny<GameReport>())).ReturnsAsync(1L);
        var (ctx, _) = CommandTestHelper.MakeContext(room.Seats[0]!);
        var service = BuildService(historyRepo: history.Object, logRepo: log.Object);

        await service.GameReportProcessAsync(room, ctx);
        await service.GameReportProcessAsync(room, ctx);

        history.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Once);
        log.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Never);
    }

    [Fact]
    public async Task GameReportProcess_TrainingRoom_WritesMySqlTrainingHistOnce()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 89;
        room.State = GameRoomState.Finished;
        for (int seat = 0; seat < 4; seat++) room.SeatToEngineOrder[seat] = seat;

        var history = new Mock<HistoryRepository>(MockBehavior.Loose);
        var log = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        history.Setup(r => r.InsertTrainingHistAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<(string MemberNo, int Point)[]>()))
            .Returns(Task.CompletedTask);
        var (ctx, _) = CommandTestHelper.MakeContext(room.Seats[0]!);

        await BuildService(historyRepo: history.Object, logRepo: log.Object).GameReportProcessAsync(room, ctx);

        history.Verify(r => r.InsertTrainingHistAsync(room.ChannelId, room.RoomId, It.IsAny<string>(), 4, It.IsAny<(string MemberNo, int Point)[]>()), Times.Once);
        history.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Never);
        log.Verify(r => r.InsertTrainingHistAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<(string MemberNo, int Point)[]>()), Times.Never);
        log.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Never);
    }

    [Fact]
    public async Task SendGetGem_RecordsDragonGemHistoryWithoutDirectGemcntUpdate()
    {
        var player = new MajakPlayer { MemberNo = "p0", ConnectionId = "c0", GemCount = 10, IpAddress = "127.0.0.1" };
        var session = new PlayerSessionService();
        session.Register(player);
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.SetDailyMissionDirectAsync("p0", 5, 1)).Returns(Task.CompletedTask);
        var hist = new Mock<HistoryRepository>(MockBehavior.Loose);
        hist.Setup(h => h.InsertGameMoneyHistAsync("p0", GameConst.EvtCodeDragonGem, 2, 10, 12, "127.0.0.1"))
            .Returns(Task.CompletedTask);
        var room = new GameRoom
        {
            SubId = "T0N5A",
            RoomOption = new string('0', 13) + "10",
        };
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "p0", Ranking = 1 };
        var (ctx, _) = CommandTestHelper.MakeContext(player);

        await InvokeSendGetGemAsync(BuildService(session, repo.Object, hist.Object), room, report, ctx);

        Assert.Equal(2, report.Users[0]!.GemCount);
        Assert.Equal(12, player.GemCount);
        hist.Verify(h => h.InsertGameMoneyHistAsync("p0", GameConst.EvtCodeDragonGem, 2, 10, 12, "127.0.0.1"), Times.Once);
        repo.Verify(r => r.IncrementGemCountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ApplyPlayParkMissions_StaleDailyAndWinner_CallLegacyProcedureAndUpdateState()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            MemberNo = "p0",
            ConnectionId = "c0",
            PlayParkDailyMissionAt = DateTime.Now.AddDays(-1),
            PlayParkAttrMission = 12,
        };
        var nonWinner = new MajakPlayer
        {
            MemberNo = "p1",
            ConnectionId = "c1",
            PlayParkDailyMissionAt = DateTime.Now,
            PlayParkAttrMission = 4,
        };
        session.Register(player);
        session.Register(nonWinner);

        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.CallPlayParkMissionAsync("p0", GameConst.PlayParkMissionTypeDay, GameConst.PlayParkMissionNo, GameConst.PlayParkProcTypeAdd, 1))
            .ReturnsAsync((true, 0));
        repo.Setup(r => r.CallPlayParkMissionAsync("p0", GameConst.PlayParkMissionTypeAttr, GameConst.PlayParkMissionNo, GameConst.PlayParkProcTypeAdd, 1))
            .ReturnsAsync((true, 37));
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "p0", Ranking = 1 };
        report.Users[1] = new GameReport.UserResult { MemberNo = "p1", Ranking = 2 };

        await InvokeApplyPlayParkMissionsAsync(BuildService(session, repo.Object), report);

        Assert.Equal(DateTime.Now.Date, player.PlayParkDailyMissionAt!.Value.Date);
        Assert.Equal(37, player.PlayParkAttrMission);
        Assert.Equal(4, nonWinner.PlayParkAttrMission);
        repo.Verify(r => r.CallPlayParkMissionAsync("p0", GameConst.PlayParkMissionTypeDay, GameConst.PlayParkMissionNo, GameConst.PlayParkProcTypeAdd, 1), Times.Once);
        repo.Verify(r => r.CallPlayParkMissionAsync("p0", GameConst.PlayParkMissionTypeAttr, GameConst.PlayParkMissionNo, GameConst.PlayParkProcTypeAdd, 1), Times.Once);
        repo.Verify(r => r.CallPlayParkMissionAsync("p1", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ApplyPlayParkMissions_SkipsNpcAndAttrAlreadyAtLegacyLimit()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            MemberNo = "p0",
            ConnectionId = "c0",
            PlayParkDailyMissionAt = DateTime.Now,
            PlayParkAttrMission = GameConst.PlayParkAttrMissionMax,
        };
        session.Register(player);
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = TournamentConst.NpcMemberNo, Ranking = 1 };
        report.Users[1] = new GameReport.UserResult { MemberNo = "p0", Ranking = 1 };

        await InvokeApplyPlayParkMissionsAsync(BuildService(session, repo.Object), report);

        repo.Verify(r => r.CallPlayParkMissionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ApplyPlayParkMissions_ProcedureFailure_DoesNotMutatePlayerState()
    {
        var originalDaily = DateTime.Now.AddDays(-1);
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            MemberNo = "p0",
            ConnectionId = "c0",
            PlayParkDailyMissionAt = originalDaily,
            PlayParkAttrMission = 12,
        };
        session.Register(player);

        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.CallPlayParkMissionAsync("p0", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((false, 0));
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "p0", Ranking = 1 };

        await InvokeApplyPlayParkMissionsAsync(BuildService(session, repo.Object), report);

        Assert.Equal(originalDaily, player.PlayParkDailyMissionAt);
        Assert.Equal(12, player.PlayParkAttrMission);
        repo.Verify(r => r.CallPlayParkMissionAsync("p0", GameConst.PlayParkMissionTypeDay, GameConst.PlayParkMissionNo, GameConst.PlayParkProcTypeAdd, 1), Times.Once);
        repo.Verify(r => r.CallPlayParkMissionAsync("p0", GameConst.PlayParkMissionTypeAttr, GameConst.PlayParkMissionNo, GameConst.PlayParkProcTypeAdd, 1), Times.Once);
    }

    [Fact]
    public async Task ApplyMissionEventCms_GradeHanchanInsideEvent_CallsLegacyProcedureOncePerDay()
    {
        var now = new DateTime(2014, 12, 25, 10, 0, 0);
        var session = new PlayerSessionService();
        var player = new MajakPlayer { MemberNo = "p0", ConnectionId = "c0" };
        var alreadyCleared = new MajakPlayer
        {
            MemberNo = "p1",
            ConnectionId = "c1",
            MissionEventCmsClearAt = now.AddHours(-1),
        };
        session.Register(player);
        session.Register(alreadyCleared);

        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.CallPcMissionEventCmsAsync("p0", GameConst.MissionEventCmsCode, GameConst.MissionEventCmsNo))
            .ReturnsAsync(true);
        var room = new GameRoom { SubId = "00G5A" };
        var report = new GameReport { RoomOption = "H" };
        report.Users[0] = new GameReport.UserResult { MemberNo = "p0" };
        report.Users[1] = new GameReport.UserResult { MemberNo = "p1" };

        await InvokeApplyMissionEventCmsAsync(BuildService(session, repo.Object), room, report, now);

        Assert.Equal(now, player.MissionEventCmsClearAt);
        Assert.Equal(now.AddHours(-1), alreadyCleared.MissionEventCmsClearAt);
        repo.Verify(r => r.CallPcMissionEventCmsAsync("p0", GameConst.MissionEventCmsCode, GameConst.MissionEventCmsNo), Times.Once);
        repo.Verify(r => r.CallPcMissionEventCmsAsync("p1", It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ApplyMissionEventCms_NonGradeTonpuOrExpiredEvent_SkipsLegacyProcedure()
    {
        var player = new MajakPlayer { MemberNo = "p0", ConnectionId = "c0" };
        var report = new GameReport { RoomOption = "H" };
        report.Users[0] = new GameReport.UserResult { MemberNo = "p0" };

        async Task Invoke(GameRoom room, GameReport gameReport, DateTime now)
        {
            var session = new PlayerSessionService();
            session.Register(player);
            var repo = new Mock<PlayerRepository>(MockBehavior.Loose);

            await InvokeApplyMissionEventCmsAsync(BuildService(session, repo.Object), room, gameReport, now);

            repo.Verify(r => r.CallPcMissionEventCmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        await Invoke(new GameRoom { SubId = "00N5A" }, report, new DateTime(2014, 12, 25, 10, 0, 0));
        await Invoke(new GameRoom { SubId = "00G5A" }, new GameReport { RoomOption = "T" }, new DateTime(2014, 12, 25, 10, 0, 0));
        await Invoke(new GameRoom { SubId = "00G5A" }, report, GameConst.MissionEventCmsEndTime);
    }

    [Fact]
    public async Task ApplyMissionEventCms_ProcedureFailure_DoesNotMutateClearDate()
    {
        var now = new DateTime(2014, 12, 25, 10, 0, 0);
        var original = now.AddDays(-1);
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            MemberNo = "p0",
            ConnectionId = "c0",
            MissionEventCmsClearAt = original,
        };
        session.Register(player);

        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.CallPcMissionEventCmsAsync("p0", GameConst.MissionEventCmsCode, GameConst.MissionEventCmsNo))
            .ReturnsAsync(false);
        var room = new GameRoom { SubId = "00G5A" };
        var report = new GameReport { RoomOption = "H" };
        report.Users[0] = new GameReport.UserResult { MemberNo = "p0" };

        await InvokeApplyMissionEventCmsAsync(BuildService(session, repo.Object), room, report, now);

        Assert.Equal(original, player.MissionEventCmsClearAt);
        repo.Verify(r => r.CallPcMissionEventCmsAsync("p0", GameConst.MissionEventCmsCode, GameConst.MissionEventCmsNo), Times.Once);
    }

    [Fact]
    public async Task UpdateGradeResultSideEffects_ProPlayer_SavesProDataAndSkipsGradeRank()
    {
        var player = new MajakPlayer
        {
            MemberNo = "pro1",
            IsPro = true,
            ProPictureUrl = "https://example.invalid/pro.png",
        };
        var user = new GameReport.UserResult { MemberNo = "pro1", Ranking = 2, GradeLevel = 11, Rating = 1600 };
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.SaveGradeModeProDataAsync(player, user, It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        await InvokeUpdateGradeResultSideEffectsAsync(BuildService(playerRepo: repo.Object), player, user);

        repo.Verify(r => r.SaveGradeModeProDataAsync(player, user, It.IsAny<DateTime>()), Times.Once);
        repo.Verify(r => r.MergeGradeRankAsync(It.IsAny<IEnumerable<GradeRankUpdateItem>>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGradeResultSideEffects_NormalPlayer_MergesGradeRankAndSkipsProData()
    {
        var player = new MajakPlayer { MemberNo = "p0", IsPro = false };
        var user = new GameReport.UserResult { MemberNo = "p0", Ranking = 1, GradeLevel = 10, Rating = 1500 };
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.MergeGradeRankAsync(It.IsAny<IEnumerable<GradeRankUpdateItem>>()))
            .Returns(Task.CompletedTask);

        await InvokeUpdateGradeResultSideEffectsAsync(BuildService(playerRepo: repo.Object), player, user);

        repo.Verify(r => r.MergeGradeRankAsync(It.Is<IEnumerable<GradeRankUpdateItem>>(rows => rows.Any(row => row.MemberNo == "p0"))), Times.Once);
        repo.Verify(r => r.SaveGradeModeProDataAsync(It.IsAny<MajakPlayer>(), It.IsAny<GameReport.UserResult>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task CheckTitleClear_HiClassFirstMatch_EnablesTitleAndSendsGetTitle()
    {
        var player = new MajakPlayer { MemberNo = "u1", ConnectionId = "c1" };
        player.HiClassRecord.MatchCnt = 1;
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.InsertOrEnableTitlesAsync("u1", It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "mjkt006" }))))
            .Returns(Task.CompletedTask)
            .Verifiable();
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await InvokeCheckTitleClearAsync(BuildService(playerRepo: repo.Object), player, ctx);

        repo.Verify();
        Assert.Equal(1, player.TitleClear[6]);
        var packet = CommandTestHelper.ToDict(Assert.Single(sent, s => s.method == Cmd.GetTitle).packet);
        Assert.Equal(1, ((JsonElement)packet[GKey.Count]!).GetInt32());
        Assert.Equal(1, ((JsonElement)packet[$"{Key.TitleType}0"]!).GetInt32());
        Assert.Equal(6, ((JsonElement)packet[$"{Key.TitleCode}0"]!).GetInt32());
    }

    [Fact]
    public async Task AwardGameIcons_WinnerUsesLegacyThresholdsAndRepositoryHook()
    {
        var player = new MajakPlayer { MemberNo = "u1", ContWinDefeat = 3 };
        player.ActiveRecord.MatchCnt = 10;
        player.ActiveRecord.WinCnt = 5;
        player.ActiveRecord.TobashiCnt = 1;
        var user = new GameReport.UserResult { MemberNo = "u1", WinCnt = 1 };
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.GrantGameIconAsync("u1", It.IsAny<string>())).Returns(Task.CompletedTask);

        var iconCodes = InvokeGetGameIconCodesForReport(player, user);
        await InvokeAwardGameIconsAsync(BuildService(playerRepo: repo.Object), player, user);

        Assert.Equal(new[] { "g00148", "g00137", "g00132", "g00142" }, iconCodes);
        repo.Verify(r => r.GrantGameIconAsync("u1", "g00148"), Times.Once);
        repo.Verify(r => r.GrantGameIconAsync("u1", "g00137"), Times.Once);
        repo.Verify(r => r.GrantGameIconAsync("u1", "g00132"), Times.Once);
        repo.Verify(r => r.GrantGameIconAsync("u1", "g00142"), Times.Once);
    }

    [Fact]
    public void AwardGameIcons_NonWinnerSkipsWinnerOnlyThresholds()
    {
        var player = new MajakPlayer { MemberNo = "u1", ContWinDefeat = 3 };
        player.ActiveRecord.MatchCnt = 1;
        player.ActiveRecord.WinCnt = 5;
        player.ActiveRecord.TobashiCnt = 10;
        var user = new GameReport.UserResult { MemberNo = "u1", WinCnt = 0 };

        var iconCodes = InvokeGetGameIconCodesForReport(player, user);

        Assert.Equal(new[] { "g00131", "g00143" }, iconCodes);
    }

    [Fact]
    public void CalcGemCountToGet_UsesLegacySubIdGemCountMap()
    {
        var room = new GameRoom { SubId = "0ZG6C" };
        var player = new MajakPlayer();

        Assert.Equal(3, InvokeCalcGemCountToGet(room, order: 0, player));
        Assert.Equal(1, InvokeCalcGemCountToGet(room, order: 1, player));
        Assert.Equal(0, InvokeCalcGemCountToGet(room, order: 2, player));
    }

    [Fact]
    public void CalcGemCountToGet_AppliesGemDoubleTripleItems()
    {
        var room = new GameRoom { SubId = "0ZG6C" };
        var player = new MajakPlayer();
        player.MajItems.Add(new MajItemInfo { ItemCode = "MJ21", EndDt = DateTime.Now.AddDays(1) });
        Assert.Equal(6, InvokeCalcGemCountToGet(room, 0, player));

        player.MajItems.Clear();
        player.MajItems.Add(new MajItemInfo { ItemCode = "MJ22", EndDt = DateTime.Now.AddDays(1) });
        Assert.Equal(9, InvokeCalcGemCountToGet(room, 0, player));

        player.MajItems.Add(new MajItemInfo { ItemCode = "MJ21", EndDt = DateTime.Now.AddDays(1) });
        Assert.Equal(12, InvokeCalcGemCountToGet(room, 0, player));
    }

    [Fact]
    public void CalcGemCountToGet_NetCafeAddsLegacyBonusGem()
    {
        var player = new MajakPlayer { IsNetCafeIp = true };

        Assert.Equal(3, InvokeCalcGemCountToGet(new GameRoom { SubId = "0ZG6A" }, 0, player));
        Assert.Equal(2, InvokeCalcGemCountToGet(new GameRoom { SubId = "T0N5A", RoomOption = new string('0', 14) }, 0, player));
    }

    // シナリオ: MJPID_ACTIONS は操作対象者へ候補あり、他メンバーへ候補なしターン通知を送る
    // 原典: レガシークライアントは全員が PutTurnMark で現在手番を更新する
    [Fact]
    public async Task SendValidActionsToPlayers_SendsSanitizedTurnNoticeToOtherClients()
    {
        var room = new GameRoom { RoomId = 77 };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"p{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
            }, seat);
        }
        room.Engine.HanchanInfo = new HanchanInfo { Chicha = 0, Player = new[] { 0, 1, 2, 3 }, CurKyoku = 0, RenchanCount = 0 };
        room.Engine.Player[0].GamePoint = 25000;
        room.Engine.Player[0].Mode = PlayerMode.Turn;
        var tile = PaiCode.MakeSerial(0);
        tile.BipaiIndex = 5;
        room.Engine.Player[0].Tehai.Add(tile);

        var actorSent = new List<object>();
        var othersSent = new List<object>();
        IReadOnlyList<string>? excludedConnections = null;
        var actorProxy = new Mock<IClientProxy>();
        actorProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((_, args, _) => actorSent.Add(args[0]!))
            .Returns(Task.CompletedTask);
        var othersProxy = new Mock<IClientProxy>();
        othersProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((_, args, _) => othersSent.Add(args[0]!))
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(actorProxy.Object);
        clientsMock.Setup(c => c.GroupExcept($"room_{room.RoomId}", It.IsAny<IReadOnlyList<string>>()))
            .Callback<string, IReadOnlyList<string>>((_, excluded) => excludedConnections = excluded)
            .Returns(othersProxy.Object);
        var ctx = new CommandContext { Clients = clientsMock.Object };
        var method = typeof(GameLogicService)
            .GetMethod("SendValidActionsToPlayersAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(BuildService(), new object[] { room, ctx })!;

        Assert.Single(actorSent);
        Assert.Single(othersSent);
        Assert.Equal(new[] { "c0" }, excludedConnections?.ToArray());

        var actorPacket = CommandTestHelper.ToDict(actorSent[0]);
        Assert.Equal("MJPID_ACTIONS", ((JsonElement)actorPacket["playType"]!).GetString());
        Assert.Equal(0, ((JsonElement)actorPacket["seatOrder"]!).GetInt32());
        Assert.Equal("Turn", ((JsonElement)actorPacket["playerMode"]!).GetString());
        Assert.Equal(new[] { 5 }, ((JsonElement)actorPacket["tapCandidates"]!).EnumerateArray().Select(e => e.GetInt32()).ToArray());
        Assert.True(((JsonElement)actorPacket["actionSeq"]!).GetInt64() > 0);
        Assert.True(((JsonElement)actorPacket["serverNow"]!).GetInt64() > 0);
        Assert.True(((JsonElement)actorPacket["deadlineAt"]!).GetInt64() > ((JsonElement)actorPacket["serverNow"]!).GetInt64());
        Assert.NotNull(room.PendingActions[0]);
        Assert.Equal(((JsonElement)actorPacket["actionSeq"]!).GetInt64(), room.PendingActions[0]!.ActionSeq);

        var othersPacket = CommandTestHelper.ToDict(othersSent[0]);
        Assert.Equal("MJPID_ACTIONS", ((JsonElement)othersPacket["playType"]!).GetString());
        Assert.Equal(0, ((JsonElement)othersPacket["seatOrder"]!).GetInt32());
        Assert.Equal("Turn", ((JsonElement)othersPacket["playerMode"]!).GetString());
        Assert.Equal(0, ((JsonElement)othersPacket["actFlags"]!).GetInt32());
        Assert.Empty(((JsonElement)othersPacket["actions"]!).EnumerateArray());
        Assert.Empty(((JsonElement)othersPacket["tapCandidates"]!).EnumerateArray());
        Assert.Equal(((JsonElement)actorPacket["actionSeq"]!).GetInt64(), ((JsonElement)othersPacket["actionSeq"]!).GetInt64());
        Assert.Equal(((JsonElement)actorPacket["deadlineAt"]!).GetInt64(), ((JsonElement)othersPacket["deadlineAt"]!).GetInt64());
    }

    [Fact]
    public async Task SendValidActionsToPlayers_DisconnectedTurnPlayerWaitsForDeadline()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 91;
        int order = Array.FindIndex(room.Engine.Player, player => player.Mode == PlayerMode.Turn);
        Assert.InRange(order, 0, GameConst.PlayerMaxCount - 1);
        int playerPos = room.Engine.HanchanInfo.Player[order];
        room.SeatToEngineOrder[playerPos] = order;
        room.Seats[playerPos]!.ConnectionId = "";
        room.Seats[playerPos]!.IsOutPlayer = true;

        var sent = new List<object>();
        var clientProxy = new Mock<ISingleClientProxy>();
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                if (method == Cmd.GamePlay) sent.Add(args[0]!);
            })
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(clientProxy.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy.Object);
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>()))
            .Returns(groupProxy.Object);
        clientsMock.Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns(groupProxy.Object);
        var ctx = new CommandContext { Clients = clientsMock.Object };
        var method = typeof(GameLogicService)
            .GetMethod("SendValidActionsToPlayersAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(BuildService(), new object[] { room, ctx })!;

        var packets = sent.Select(CommandTestHelper.ToDict).ToArray();
        Assert.DoesNotContain(packets, packet =>
            ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION"
            && ((JsonElement)packet["seatOrder"]!).GetInt32() == order);

        var promptPacket = Assert.Single(packets, packet =>
            ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTIONS"
            && ((JsonElement)packet["seatOrder"]!).GetInt32() == order);
        Assert.Equal("Turn", ((JsonElement)promptPacket["playerMode"]!).GetString());
        Assert.Equal(0, ((JsonElement)promptPacket["actFlags"]!).GetInt32());
        Assert.Empty(((JsonElement)promptPacket["actions"]!).EnumerateArray());
        Assert.Empty(((JsonElement)promptPacket["tapCandidates"]!).EnumerateArray());
        Assert.NotNull(room.PendingActions[order]);
        Assert.Equal(((JsonElement)promptPacket["actionSeq"]!).GetInt64(), room.PendingActions[order]!.ActionSeq);
        Assert.True(room.PendingActions[order]!.DeadlineAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SendValidActionsToPlayers_PausesWhenOnlyDisconnectedPlayersAndViewerRemain()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 92;
        room.State = GameRoomState.Playing;
        Assert.True(room.AddViewer(new MajakPlayer { ConnectionId = "viewer", MemberNo = "viewer1", ChannelId = "ch1" }));
        foreach (var seat in room.Seats.Where(seat => seat != null).Select(seat => seat!))
        {
            seat.ConnectionId = "";
            seat.IsOutPlayer = true;
        }

        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Throws(new InvalidOperationException("No active players should be prompted"));
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Throws(new InvalidOperationException("No active players should be prompted"));
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Throws(new InvalidOperationException("All-out room should not broadcast proxy actions"));
        clientsMock.Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>())).Throws(new InvalidOperationException("All-out room should not broadcast turn notices"));
        var ctx = new CommandContext { Clients = clientsMock.Object };
        var method = typeof(GameLogicService)
            .GetMethod("SendValidActionsToPlayersAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(BuildService(), new object[] { room, ctx })!;

        Assert.Equal(0, room.ActivePlayerCount);
        Assert.Equal(1, room.ViewerCount);
        Assert.NotNull(room.NoActiveMembersSince);
        Assert.Empty(room.PlayHistory);
        Assert.All(room.PendingActions, Assert.Null);
    }

    // シナリオ: 復帰 resync は操作対象でないユーザーにも現在の Turn 表示を復元する
    // 原典: 通常送信時の MJPID_ACTIONS(Turn) は操作対象以外にも sanitized 通知を送る
    [Fact]
    public async Task SendCurrentActionPrompt_ResyncSendsSanitizedTurnNoticeForOtherPlayer()
    {
        var room = new GameRoom { RoomId = 82 };
        room.Engine.Player[1].Mode = PlayerMode.Turn;
        room.PendingActions[1] = new PendingActionPrompt
        {
            ActionSeq = 456,
            SeatOrder = 1,
            PlayerMode = PlayerMode.Turn,
            IssuedAt = DateTimeOffset.UtcNow,
            DeadlineAt = DateTimeOffset.UtcNow.AddSeconds(20),
        };
        var resyncPlayer = new MajakPlayer { MemberNo = "p3", EngineOrder = 3 };
        var callerSent = new List<object>();
        var callerProxy = new Mock<IClientProxy>();
        callerProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                if (method == Cmd.GamePlay) callerSent.Add(args[0]!);
            })
            .Returns(Task.CompletedTask);
        var ctx = new CommandContext { Caller = callerProxy.Object };

        await BuildService().SendCurrentActionPromptAsync(room, ctx, resyncPlayer);

        var packet = CommandTestHelper.ToDict(Assert.Single(callerSent));
        Assert.Equal("MJPID_ACTIONS", ((JsonElement)packet["playType"]!).GetString());
        Assert.Equal(1, ((JsonElement)packet["seatOrder"]!).GetInt32());
        Assert.Equal("Turn", ((JsonElement)packet["playerMode"]!).GetString());
        Assert.Equal(0, ((JsonElement)packet["actFlags"]!).GetInt32());
        Assert.Empty(((JsonElement)packet["actions"]!).EnumerateArray());
        Assert.Empty(((JsonElement)packet["tapCandidates"]!).EnumerateArray());
        Assert.Equal(456, ((JsonElement)packet["actionSeq"]!).GetInt64());
        Assert.True(((JsonElement)packet["deadlineAt"]!).GetInt64() > ((JsonElement)packet["serverNow"]!).GetInt64());
    }

    // シナリオ: Furo/Chan の pass-only 応答待ちはクライアント入力を出さずサーバー側で PAS 解決する
    // 原典: MODE_FURO / MODE_CHAN は捨て牌への応答待ちであり PutTurnMark 対象ではない
    [Fact]
    public async Task SendValidActionsToPlayers_AutoResolvesFuroPassOnlyWithoutActionPrompt()
    {
        var room = new GameRoom { RoomId = 78 };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"p{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
            }, seat);
        }
        room.Engine.HanchanInfo = new HanchanInfo { Chicha = 0, Player = new[] { 0, 1, 2, 3 }, CurKyoku = 0, RenchanCount = 0 };
        room.Engine.Player[0].Order = 0;
        room.Engine.Player[0].Mode = PlayerMode.Furo;

        var actorSent = new List<object>();
        var groupSent = new List<object>();
        var actorProxy = new Mock<IClientProxy>();
        actorProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((_, args, _) => actorSent.Add(args[0]!))
            .Returns(Task.CompletedTask);
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                if (method == Cmd.GamePlay) groupSent.Add(args[0]!);
            })
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(actorProxy.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy.Object);
        clientsMock.Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Throws(new InvalidOperationException("Furo pass-only should not be broadcast as a turn notice"));
        var ctx = new CommandContext { Clients = clientsMock.Object };
        var method = typeof(GameLogicService)
            .GetMethod("SendValidActionsToPlayersAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(BuildService(), new object[] { room, ctx })!;

        Assert.Empty(actorSent);
        var passPacket = CommandTestHelper.ToDict(Assert.Single(groupSent));
        Assert.Equal("MJPID_ACTION", ((JsonElement)passPacket["playType"]!).GetString());
        Assert.Equal(0, ((JsonElement)passPacket["seatOrder"]!).GetInt32());
        Assert.Equal((int)MajakServer.Engine.Act.Pas, ((JsonElement)passPacket["action"]!).GetInt32());
        Assert.True(((JsonElement)passPacket["actionSeq"]!).GetInt64() > 0);
        Assert.Null(room.PendingActions[0]);
    }

    // シナリオ: 接続中の Furo/Chan pass-only は prompt/deadline を作らず即時 PAS を実行する
    [Fact]
    public async Task SendValidActionsToPlayers_PassOnlyFuroExecutesDefaultPassImmediately()
    {
        var room = new GameRoom { RoomId = 79, RoomOption = "120000001000000" };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"p{seat}",
                NickName = $"P{seat}",
                ConnectionId = $"c{seat}",
            }, seat);
        }
        room.Engine.HanchanInfo = new HanchanInfo { Chicha = 0, Player = new[] { 0, 1, 2, 3 }, CurKyoku = 0, RenchanCount = 0 };
        room.Engine.Player[0].Order = 0;
        room.Engine.Player[0].Mode = PlayerMode.Furo;

        var actorProxy = new Mock<IClientProxy>();
        actorProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);
        var groupSent = new List<object>();
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                if (method == Cmd.GamePlay) groupSent.Add(args[0]!);
            })
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(actorProxy.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy.Object);
        var ctx = new CommandContext { Clients = clientsMock.Object };
        var method = typeof(GameLogicService)
            .GetMethod("SendValidActionsToPlayersAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(BuildService(), new object[] { room, ctx })!;

        var passPacket = groupSent.Select(CommandTestHelper.ToDict).FirstOrDefault(packet =>
            ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION"
            && ((JsonElement)packet["seatOrder"]!).GetInt32() == 0
            && ((JsonElement)packet["action"]!).GetInt32() == (int)MajakServer.Engine.Act.Pas);
        Assert.NotNull(passPacket);
        Assert.Null(room.PendingActions[0]);
        Assert.Contains(room.PlayHistory, history =>
            ((JsonElement)CommandTestHelper.ToDict(history)["playType"]!).GetString() == "MJPID_ACTION");
    }

    // シナリオ: 接続中でも Turn deadline 到達時は AP-14 のサーバー fallback として末尾 TAP を実行する
    [Fact]
    public async Task ScheduleActionTimeout_ConnectedTurnExecutesDefaultTapAfterDeadline()
    {
        var room = BuildPaiInfoRoom("00N5A");
        room.RoomId = 80;
        int order = Array.FindIndex(room.Engine.Player, player => player.Mode == PlayerMode.Turn);
        Assert.InRange(order, 0, GameConst.PlayerMaxCount - 1);
        var validBefore = room.Engine.GetValidActions(order);
        var fallbackBipaiIndex = Assert.Single(validBefore.TapCandidates.TakeLast(1));
        var hand = room.Engine.Player[order].Tehai;
        var fallbackHandIndex = hand.FindIndex(tile => tile.BipaiIndex == fallbackBipaiIndex);
        Assert.InRange(fallbackHandIndex, 0, hand.Count - 1);
        var fallbackTile = hand[fallbackHandIndex];
        hand.RemoveAt(fallbackHandIndex);
        hand.Add(fallbackTile);
        var player = room.Seats[room.Engine.HanchanInfo.Player[order]]!;
        player.EngineOrder = order;
        var prompt = new PendingActionPrompt
        {
            ActionSeq = 321,
            SeatOrder = order,
            PlayerMode = PlayerMode.Turn,
            IssuedAt = DateTimeOffset.UtcNow,
            DeadlineAt = DateTimeOffset.UtcNow.AddMilliseconds(50),
        };
        room.PendingActions[order] = prompt;

        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        var method = typeof(GameLogicService)
            .GetMethod("ScheduleActionTimeout", BindingFlags.NonPublic | BindingFlags.Instance)!;

        method.Invoke(BuildService(), new object[] { room, ctx, prompt });
        await Task.Delay(200);

        var validAfter = room.Engine.GetValidActions(order);
        var tapPacket = sent.Where(packet => packet.method == Cmd.GamePlay).Select(packet => CommandTestHelper.ToDict(packet.packet)).FirstOrDefault(packet =>
            ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION"
            && ((JsonElement)packet["seatOrder"]!).GetInt32() == order
            && ((JsonElement)packet["action"]!).GetInt32() == (int)MajakServer.Engine.Act.Tap);
        Assert.True(tapPacket != null, $"Expected timeout TAP packet. pending={(room.PendingActions[order] == null ? "null" : room.PendingActions[order]!.ActionSeq.ToString())} mode={room.Engine.Player[order].Mode} tapCandidates={string.Join(',', validAfter.TapCandidates)} history={room.PlayHistory.Count} sent={string.Join(';', sent.Select(packet => $"{packet.method}:{System.Text.Json.JsonSerializer.Serialize(packet.packet)}"))}");
        Assert.True(room.PendingActions[order] == null || room.PendingActions[order]!.ActionSeq != prompt.ActionSeq);
        Assert.Contains(room.PlayHistory, history =>
            CommandTestHelper.ToDict(history).TryGetValue("playType", out var playType)
            && ((JsonElement)playType!).GetString() == "MJPID_ACTION");
    }

    [Fact]
    public async Task ScheduleActionTimeout_TrainingContinuesIntoNextEmptySeat()
    {
        var room = BuildPaiInfoRoom("00T5A");
        room.RoomId = 81;
        room.State = GameRoomState.Playing;
        int order = Array.FindIndex(room.Engine.Player, player => player.Mode == PlayerMode.Turn);
        Assert.InRange(order, 0, GameConst.PlayerMaxCount - 1);
        int npcOrder = (order + 1) % GameConst.PlayerMaxCount;
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };
        for (int i = 0; i < GameConst.PlayerMaxCount; i++)
        {
            room.SeatToEngineOrder[i] = i;
            if (room.Seats[i] != null) room.Seats[i]!.EngineOrder = i;
            if (i != order) room.Engine.Player[i].Tehai.Clear();
        }
        room.Seats[npcOrder] = null;

        var player = room.Seats[order]!;
        var prompt = new PendingActionPrompt
        {
            ActionSeq = 322,
            SeatOrder = order,
            PlayerMode = PlayerMode.Turn,
            IssuedAt = DateTimeOffset.UtcNow,
            DeadlineAt = DateTimeOffset.UtcNow.AddMilliseconds(50),
        };
        room.PendingActions[order] = prompt;

        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        var method = typeof(GameLogicService)
            .GetMethod("ScheduleActionTimeout", BindingFlags.NonPublic | BindingFlags.Instance)!;

        method.Invoke(BuildService(), new object[] { room, ctx, prompt });
        await WaitUntilAsync(() => room.PendingActions[order]?.ActionSeq != prompt.ActionSeq
            && room.Engine.Player[npcOrder].Mode == PlayerMode.None
            && room.PendingActions.Where((pending, pendingOrder) => pendingOrder != npcOrder)
                .Any(pending => pending != null));
        await room.EngineLock.WaitAsync();
        room.EngineLock.Release();

        var tapOrders = sent
            .Where(packet => packet.method == Cmd.GamePlay)
            .Select(packet => CommandTestHelper.ToDict(packet.packet))
            .Where(packet => ((JsonElement)packet["playType"]!).GetString() == "MJPID_ACTION")
            .Where(packet => ((JsonElement)packet["action"]!).GetInt32() == (int)MajakServer.Engine.Act.Tap)
            .Select(packet => ((JsonElement)packet["seatOrder"]!).GetInt32())
            .ToArray();
        Assert.Contains(order, tapOrders);
        Assert.Contains(npcOrder, tapOrders);
        Assert.NotEqual(PlayerMode.Turn, room.Engine.Player[npcOrder].Mode);
    }

    // シナリオ3: RoomOption[0]='1' → Hanchan=true
    // 原典: room.RoomOption[0] == '1' → 半荘
    [Fact]
    public void BuildRuleInfo_RoomOptionHanchan_SetsHanchan()
    {
        var room = new GameRoom
        {
            SubId = "0ZN5A",  // G でも R でもない → Normal
            RoomOption = "1200000010000",
        };
        var rule = InvokeBuildRuleInfo(room);
        Assert.True(rule.Hanchan);
    }

    // シナリオ4: RoomOption[0]='0' → Hanchan=false (東風)
    [Fact]
    public void BuildRuleInfo_RoomOptionTonpu_NotHanchan()
    {
        var room = new GameRoom
        {
            SubId = "0ZN5A",
            RoomOption = "0200000010000",
        };
        var rule = InvokeBuildRuleInfo(room);
        Assert.False(rule.Hanchan);
    }

    // シナリオ5: SubId[2]='G' → グレードモードルール (Kuitan=true, AkaDora=2)
    // 原典: SubId2 == 'G' → Grade ルール固定
    [Fact]
    public void BuildRuleInfo_GradeMode_SetsGradeRules()
    {
        var room = new GameRoom
        {
            SubId = "00G6A",  // index2='G'
            RoomOption = "0000000000000",
        };
        var rule = InvokeBuildRuleInfo(room);
        Assert.False(rule.Hanchan);
        Assert.True(rule.Kuitan);
        Assert.Equal(2, rule.AkaDora);
        Assert.False(rule.Yakitori);
    }

    [Fact]
    public void BuildRuleInfo_GradeHanchanSubId7_SetsHanchan()
    {
        var room = new GameRoom
        {
            SubId = "00G7A",
            RoomOption = "0000000000000",
        };
        var rule = InvokeBuildRuleInfo(room);
        Assert.True(rule.Hanchan);
    }

    // シナリオ6: SubId[2]='R' → レートモードルール (Hanchan=true, AkaDora=0)
    [Fact]
    public void BuildRuleInfo_RatedMode_SetsRatedRules()
    {
        var room = new GameRoom
        {
            SubId = "00RNA",  // index2='R'
            RoomOption = "0000000000000",
        };
        var rule = InvokeBuildRuleInfo(room);
        Assert.True(rule.Hanchan);
        Assert.Equal(0, rule.AkaDora);
    }

    // シナリオ7: RoomOption[3]='0' → Kuitan=true (喰い断 ON)
    // 原典: opt[3] == '0' → Kuitan=true
    [Fact]
    public void BuildRuleInfo_Kuitan_WhenOpt3Is0()
    {
        var room = new GameRoom
        {
            SubId = "0ZN5A",
            RoomOption = "1200000010000", // opt[3]='0' → Kuitan
        };
        var rule = InvokeBuildRuleInfo(room);
        Assert.True(rule.Kuitan);
    }
}
