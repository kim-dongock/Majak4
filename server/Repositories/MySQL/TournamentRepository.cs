using MajakServer.Models.Game;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL.Entities;
using Microsoft.EntityFrameworkCore;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// トーナメント DB アクセス — 原典: HMajDBObject Tournament系メソッド群
///   MJK_TOURNAMENTPLAN   — TOURNAMENT_PLAN
///   MJK_TOURNAMENTDETAIL — TOURNAMENT_DETAIL
///   MJK_TOURNAMENTJOIN   — TOURNAMENT_JOIN
///   MJK_TOURNAMENTLIMIT  — TOURNAMENT_LIMIT
/// </summary>
public class TournamentRepository
{
    private readonly ILogger<TournamentRepository>? _logger;
    private readonly GameDataContextFactory? _gameDb;

    private static ulong ParseMemberNo(string memberNo)
        => MemberNoIds.Parse(memberNo);

    private static ulong? ParseNullableMemberNo(string? memberNo)
        => string.IsNullOrWhiteSpace(memberNo) || !MemberNoIds.TryParse(memberNo, out var memberNoValue)
            ? null
            : memberNoValue;

    public TournamentRepository()
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public TournamentRepository(
        ILogger<TournamentRepository> logger,
        GameDataContextFactory gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    // ───────────────────────────────────────────────────────── SELECT ──────

    /// <summary>
    /// アクティブなトーナメント計画を全件取得 — 原典: SelectTournamentPlan
    /// アーカイブ済み (End/Reject/Stop かつ ViewEndDt 過ぎ) は除外。
    /// </summary>
    public async Task<List<TournamentPlan>> SelectActivePlansAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        var now = DateTime.Now;
        var sessions = await db.TournamentSessions.AsNoTracking()
            .Where(session => session.ViewEndAt >= now)
            .OrderBy(session => session.PlayStatus)
            .ThenBy(session => session.PlayStartAt)
            .ToListAsync();
        return sessions.Select(ToPlan).ToList();
    }

    /// <summary>原典: SelectTournamentDetail</summary>
    public virtual async Task<List<TournamentDetail>> SelectDetailsAsync(long seqNo)
    {
        await using var db = await RequireGameDb().CreateAsync();
        ulong sessionId = checked((ulong)seqNo);
        var rooms = await db.TournamentRooms.AsNoTracking()
            .Where(room => room.SessionId == sessionId)
            .OrderBy(room => room.SubId)
            .ToListAsync();
        return rooms.Select(ToDetail).ToList();
    }

    /// <summary>プレイヤーの参加情報を取得 — 原典: SelectTournamentJoin</summary>
    public virtual async Task<TournamentJoin?> SelectJoinAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var participant = await db.TournamentParticipants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
        return participant is null ? null : ToJoin(participant);
    }

    /// <summary>トーナメントの参加者リストを取得 — 原典: SelectTournamentJoinList</summary>
    public virtual async Task<List<TournamentJoin>> SelectJoinListAsync(long seqNo)
    {
        await using var db = await RequireGameDb().CreateAsync();
        ulong sequence = checked((ulong)seqNo);
        var participants = await db.TournamentParticipants.AsNoTracking()
            .Where(item => item.JoinSequenceNo == sequence && item.JoinStatus == 2)
            .ToListAsync();
        return participants.Select(ToJoin).ToList();
    }

    /// <summary>時間制限リストを取得 — 原典: SelectTournamentLimit</summary>
    public async Task<List<TournamentLimit>> SelectLimitsAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        return await db.TournamentLimits.AsNoTracking()
            .Where(limit => limit.IsValid)
            .OrderBy(limit => limit.LimitNo)
            .Select(limit => new TournamentLimit
            {
                LimitNo = limit.LimitNo,
                LimitValid = 1,
                LimitStartDt = limit.LimitStartAt,
                LimitEndDt = limit.LimitEndAt,
            })
            .ToListAsync();
    }

    // ───────────────────────────────────────────────────────── INSERT ──────

    /// <summary>
    /// トーナメント計画を登録する — 原典: MakeTournamentPlan
    /// SEQNO はシーケンスで自動採番し plan.SeqNo に返す。
    /// </summary>
    public virtual async Task<bool> InsertPlanAsync(TournamentPlan plan)
    {
        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            var session = new TournamentSessionEntity
            {
                JoinStartAt = plan.JoinStartDt,
                MatchStartAt = plan.MatchStartDt,
                PlayStartAt = plan.PlayStartDt,
                PlayEndAt = plan.PlayEndDt,
                ViewEndAt = plan.ViewEndDt,
                NextStartAt = plan.NextStartDt,
                NextCutAt = plan.NextCutDt,
                PlaySchedule = plan.PlaySchedule,
                PlayStatus = checked((byte)plan.PlayStatus),
                PlayPhase = checked((byte)plan.PlayPhase),
                PlayerCount = 0,
                MaxPlayerCount = checked((ushort)plan.MaxPlayerNum),
                MaxRoomCount = checked((ushort)plan.MaxRoomNum),
                SessionName = plan.PlayName,
                RoomOption = plan.RoomOption,
                PrivateInfo = plan.Password,
                MaxViewerCount = checked((ushort)plan.MaxViewer),
                PlayCount = checked((byte)plan.PlayNum),
                PlayTime = checked((byte)plan.PlayTime),
                PlayMode = checked((byte)plan.PlayMode),
                JoinMoney = plan.JoinMoney,
                PrizeMoney1 = plan.GradeMoney[0],
                PrizeMoney2 = plan.GradeMoney[1],
                PrizeMoney3 = plan.GradeMoney[2],
                PrizeMoney4 = plan.GradeMoney[3],
                PlanMemberNo = ParseNullableMemberNo(plan.PlanMemberNo),
            };
            db.TournamentSessions.Add(session);
            await db.SaveChangesAsync();
            plan.SeqNo = checked((long)session.SessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Tournament plan insert failed. Sequence={Sequence}, Table={Table}, Organizer={Organizer}",
                "MJK_TOURNAMENT_SEQ", "MJK_TOURNAMENTPLAN", plan.PlanMemberNo);
            return false;
        }
    }

    // ───────────────────────────────────────────────────────── MERGE ──────

    /// <summary>
    /// 参加/キャンセル/離脱を MERGE で更新 — 原典: MergeTournamentJoinPlayer
    /// </summary>
    public virtual async Task<(bool Ok, int UpdatedCount)> MergeJoinAsync(
        string memberNo, long seqNo, int status, string joinMemberNo = "00")
    {
        try
        {
            var parsedMemberNo = ParseMemberNo(memberNo);
            var normalizedJoinMemberNo = string.IsNullOrWhiteSpace(joinMemberNo) ? "00" : joinMemberNo;
            await using var db = await RequireGameDb().CreateAsync();
            var participant = await db.TournamentParticipants
                .SingleOrDefaultAsync(item => item.MemberNo == parsedMemberNo);
            var now = DateTime.Now;
            if (participant is null)
            {
                db.TournamentParticipants.Add(new TournamentParticipantEntity
                {
                    MemberNo = parsedMemberNo,
                    SessionId = checked((ulong)seqNo),
                    JoinSequenceNo = checked((ulong)seqNo),
                    JoinMemberNo = normalizedJoinMemberNo,
                    JoinStatus = checked((byte)status),
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            else
            {
                participant.SessionId = checked((ulong)seqNo);
                participant.JoinSequenceNo = checked((ulong)seqNo);
                participant.JoinMemberNo = normalizedJoinMemberNo;
                participant.JoinStatus = checked((byte)status);
                participant.UpdatedAt = now;
            }
            await db.SaveChangesAsync();
            return (true, 1);
        }
        catch (Exception ex)
        {
            LogFailure(ex, nameof(MergeJoinAsync));
            return (false, 0);
        }
    }

    /// <summary>主催者開催回数を MERGE — 原典: MergeTournamentJoinPlanner</summary>
    public virtual async Task<bool> MergePlannerManageAsync(TournamentJoin planner)
    {
        try
        {
            var memberNoValue = ParseMemberNo(planner.MemberNo);
            await using var db = await RequireGameDb().CreateAsync();
            var participant = await db.TournamentParticipants
                .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
            var now = DateTime.Now;
            if (participant is null)
            {
                participant = new TournamentParticipantEntity
                {
                    MemberNo = memberNoValue,
                    SessionId = 0,
                    JoinSequenceNo = 0,
                    JoinMemberNo = "00",
                    JoinStatus = 0,
                    CreatedAt = now,
                };
                db.TournamentParticipants.Add(participant);
            }
            participant.TotalManageCount = checked((uint)planner.TotManageNum);
            participant.ManageCount = checked((uint)planner.ManageNum);
            if (planner.LastManageDt != default) participant.LastManageAt = planner.LastManageDt;
            participant.UpdatedAt = now;
            return await db.SaveChangesAsync() > 0;
        }
        catch (Exception ex)
        {
            LogFailure(ex, nameof(MergePlannerManageAsync));
            return false;
        }
    }

    /// <summary>トーナメント配布用 UserPresent を作成 — 原典: InsertUserPresent</summary>
    public virtual async Task<bool> InsertUserPresentsAsync(IEnumerable<UserPresentRecord> presents)
    {
        var rows = presents.ToList();
        if (rows.Count == 0) return true;

        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            var now = DateTime.Now;
            db.PlayerPresents.AddRange(rows.Select(present => new PlayerPresentEntity
            {
                MemberNo = ParseMemberNo(present.MemberNo),
                ReceiveStatus = 0,
                PresentAmount = present.PresentNum,
                PresentType = checked((byte)present.PresentKbn),
                PresentKind = checked((byte)present.PresentKind),
                PresentInfo = present.PresentInfo,
                PresentRefId = present.PresentId,
                SentAt = now,
            }));
            return await db.SaveChangesAsync() == rows.Count;
        }
        catch (Exception ex)
        {
            LogFailure(ex, nameof(InsertUserPresentsAsync));
            return false;
        }
    }

    // ───────────────────────────────────────────────────────── UPDATE ──────

    /// <summary>計画の PlayerNum を更新</summary>
    public virtual async Task<bool> UpdatePlayerNumAsync(long seqNo, int delta)
    {
        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            ulong sessionId = checked((ulong)seqNo);
            var session = await db.TournamentSessions.SingleOrDefaultAsync(item => item.SessionId == sessionId);
            if (session is null) return false;
            session.PlayerCount = checked((ushort)(session.PlayerCount + delta));
            return await db.SaveChangesAsync() > 0;
        }
        catch (Exception ex)
        {
            LogFailure(ex, nameof(UpdatePlayerNumAsync));
            return false;
        }
    }

    /// <summary>計画ステータスと次ラウンド情報を更新 — 原典: UpdateTournamentPlan</summary>
    public virtual async Task<bool> UpdatePlanStatusAsync(TournamentPlan plan)
    {
        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            ulong sessionId = checked((ulong)plan.SeqNo);
            var session = await db.TournamentSessions.SingleOrDefaultAsync(item => item.SessionId == sessionId);
            if (session is null) return false;
            session.PlayStatus = checked((byte)plan.PlayStatus);
            session.PlayPhase = checked((byte)plan.PlayPhase);
            session.PlayerCount = checked((ushort)plan.PlayerNum);
            session.NextStartAt = plan.NextStartDt;
            session.NextCutAt = plan.NextCutDt;
            session.ResultMemberNo1 = ParseNullableMemberNo(plan.ResultMemberNo[0]);
            session.ResultMemberNo2 = ParseNullableMemberNo(plan.ResultMemberNo[1]);
            session.ResultMemberNo3 = ParseNullableMemberNo(plan.ResultMemberNo[2]);
            session.ResultMemberNo4 = ParseNullableMemberNo(plan.ResultMemberNo[3]);
            return await db.SaveChangesAsync() > 0;
        }
        catch (Exception ex)
        {
            LogFailure(ex, nameof(UpdatePlanStatusAsync));
            return false;
        }
    }

    /// <summary>対局詳細をバルク MERGE — 原典: MergeTournamentDetail</summary>
    public virtual async Task<bool> MergeDetailsAsync(IEnumerable<TournamentDetail> details)
    {
        try
        {
            var rows = details.ToList();
            if (rows.Count == 0) return true;
            await using var db = await RequireGameDb().CreateAsync();
            var keys = rows.Select(detail => new
            {
                SessionId = checked((ulong)detail.SeqNo),
                SubId = checked((ushort)detail.SubId),
            }).ToList();
            var sessionIds = keys.Select(key => key.SessionId).Distinct().ToArray();
            var existing = await db.TournamentRooms
                .Where(room => sessionIds.Contains(room.SessionId))
                .ToDictionaryAsync(room => (room.SessionId, room.SubId));
            foreach (var detail in rows)
            {
                var key = (checked((ulong)detail.SeqNo), checked((ushort)detail.SubId));
                if (!existing.TryGetValue(key, out var room))
                {
                    room = new TournamentRoomEntity { SessionId = key.Item1, SubId = key.Item2 };
                    db.TournamentRooms.Add(room);
                }
                ApplyDetail(room, detail);
            }
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            LogFailure(ex, nameof(MergeDetailsAsync));
            return false;
        }
    }

    /// <summary>参加者ステータスを一括更新 — 原典: UpdateTournamentJoinPlayerEnd/Exit</summary>
    public virtual async Task<bool> BulkUpdateJoinStatusAsync(IEnumerable<string> memberNos, int status)
    {
        var ids = memberNos
            .Where(memberNo => MemberNoIds.TryParse(memberNo, out _))
            .Select(ParseMemberNo)
            .Distinct()
            .ToArray();
        if (ids.Length == 0) return true;
        try
        {
            await using var db = await RequireGameDb().CreateAsync();
            byte joinStatus = checked((byte)status);
            await db.TournamentParticipants
                .Where(participant => ids.Contains(participant.MemberNo))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(participant => participant.JoinStatus, joinStatus)
                    .SetProperty(participant => participant.UpdatedAt, DateTime.Now));
            return true;
        }
        catch (Exception ex)
        {
            LogFailure(ex, nameof(BulkUpdateJoinStatusAsync));
            return false;
        }
    }

    /// <summary>対局結果を対局詳細に反映 — 原典: UpdateResultTournamentMode</summary>
    public virtual async Task<bool> UpdateDetailResultAsync(TournamentDetail detail)
    {
        return await UpdateDetailResultsAsync([detail]);
    }

    /// <summary>複数の対局結果をまとめて反映する。</summary>
    public virtual async Task<bool> UpdateDetailResultsAsync(IEnumerable<TournamentDetail> details)
    {
        try
        {
            var rows = details.ToList();
            if (rows.Count == 0) return true;
            await using var db = await RequireGameDb().CreateAsync();
            var sessionIds = rows.Select(detail => checked((ulong)detail.SeqNo)).Distinct().ToArray();
            var rooms = await db.TournamentRooms
                .Where(room => sessionIds.Contains(room.SessionId))
                .ToDictionaryAsync(room => (room.SessionId, room.SubId));
            foreach (var detail in rows)
            {
                var key = (checked((ulong)detail.SeqNo), checked((ushort)detail.SubId));
                if (!rooms.TryGetValue(key, out var room)) return false;
                ApplyDetailResult(room, detail);
            }
            return await db.SaveChangesAsync() > 0;
        }
        catch (Exception ex)
        {
            LogFailure(ex, nameof(UpdateDetailResultsAsync));
            return false;
        }
    }

    // ─────────────────────────────────────────────── private helpers ──────

    private void LogFailure(Exception exception, string operation)
        => _logger?.LogError(exception,
            "Tournament repository operation failed. Operation={Operation}", operation);

    private GameDataContextFactory RequireGameDb()
        => _gameDb ?? throw new InvalidOperationException("MySQL GameDataContextFactory is not configured.");

    private static TournamentPlan ToPlan(TournamentSessionEntity session)
    {
        var plan = new TournamentPlan
        {
            SeqNo = checked((long)session.SessionId),
            PlayName = session.SessionName,
            PlayStatus = session.PlayStatus,
            PlayPhase = session.PlayPhase,
            PlayerNum = session.PlayerCount,
            MaxPlayerNum = session.MaxPlayerCount,
            MaxRoomNum = session.MaxRoomCount,
            RoomOption = session.RoomOption,
            Password = session.PrivateInfo ?? string.Empty,
            MaxViewer = session.MaxViewerCount,
            PlayNum = session.PlayCount,
            PlayTime = session.PlayTime,
            PlayMode = session.PlayMode,
            JoinMoney = session.JoinMoney,
            GradeMoney = [session.PrizeMoney1, session.PrizeMoney2, session.PrizeMoney3, session.PrizeMoney4],
            PlanMemberNo = MemberNoIds.Format(session.PlanMemberNo),
            ResultMemberNo = [
                MemberNoIds.Format(session.ResultMemberNo1),
                MemberNoIds.Format(session.ResultMemberNo2),
                MemberNoIds.Format(session.ResultMemberNo3),
                MemberNoIds.Format(session.ResultMemberNo4),
            ],
            PlaySchedule = session.PlaySchedule,
            JoinStartDt = session.JoinStartAt,
            MatchStartDt = session.MatchStartAt,
            PlayStartDt = session.PlayStartAt,
            PlayEndDt = session.PlayEndAt,
            ViewEndDt = session.ViewEndAt,
            NextStartDt = session.NextStartAt,
            NextCutDt = session.NextCutAt,
        };
        var playTime = TournamentTables.GetPlayTime(plan.PlayTime);
        if (playTime is not null)
            plan.NextEndDt = plan.NextStartDt.AddMinutes(playTime.PlayTimeMax);
        if (!string.IsNullOrEmpty(plan.PlaySchedule))
            plan.StartPlanDtAll = [.. plan.PlaySchedule.Split('|', StringSplitOptions.RemoveEmptyEntries)];
        return plan;
    }

    private static void ApplyDetailResult(TournamentRoomEntity room, TournamentDetail detail)
    {
        room.EndedAt = detail.EndDt;
        room.ScoreTmp1 = detail.PointTmp[0];
        room.ScoreTmp2 = detail.PointTmp[1];
        room.ScoreTmp3 = detail.PointTmp[2];
        room.ScoreTmp4 = detail.PointTmp[3];
        room.Score1 = detail.Point[0];
        room.Score2 = detail.Point[1];
        room.Score3 = detail.Point[2];
        room.Score4 = detail.Point[3];
        room.Rank1MemberNo = ParseNullableMemberNo(detail.GradePlayerMemberNo[0]);
        room.Rank2MemberNo = ParseNullableMemberNo(detail.GradePlayerMemberNo[1]);
        room.Rank3MemberNo = ParseNullableMemberNo(detail.GradePlayerMemberNo[2]);
        room.Rank4MemberNo = ParseNullableMemberNo(detail.GradePlayerMemberNo[3]);
        room.Grade1MemberNo = detail.GradeMemberNo[0];
        room.Grade2MemberNo = detail.GradeMemberNo[1];
        room.Grade3MemberNo = detail.GradeMemberNo[2];
        room.Grade4MemberNo = detail.GradeMemberNo[3];
    }

    private static TournamentDetail ToDetail(TournamentRoomEntity room)
        => new()
        {
            SeqNo = checked((long)room.SessionId),
            SubId = room.SubId,
            RoomId = room.RoomId,
            PlayerMemberNo = [MemberNoIds.Format(room.MemberNo1), MemberNoIds.Format(room.MemberNo2), MemberNoIds.Format(room.MemberNo3), MemberNoIds.Format(room.MemberNo4)],
            JoinMemberNo = [room.JoinMemberNo1 ?? string.Empty, room.JoinMemberNo2 ?? string.Empty, room.JoinMemberNo3 ?? string.Empty, room.JoinMemberNo4 ?? string.Empty],
            PointTmp = [room.ScoreTmp1, room.ScoreTmp2, room.ScoreTmp3, room.ScoreTmp4],
            Point = [room.Score1, room.Score2, room.Score3, room.Score4],
            GradePlayerMemberNo = [MemberNoIds.Format(room.Rank1MemberNo), MemberNoIds.Format(room.Rank2MemberNo), MemberNoIds.Format(room.Rank3MemberNo), MemberNoIds.Format(room.Rank4MemberNo)],
            GradeMemberNo = [room.Grade1MemberNo ?? string.Empty, room.Grade2MemberNo ?? string.Empty, room.Grade3MemberNo ?? string.Empty, room.Grade4MemberNo ?? string.Empty],
            StartPlanDt = room.PlanStartAt,
            EndPlanDt = room.PlanEndAt,
            StartDt = room.StartedAt ?? default,
            EndDt = room.EndedAt ?? default,
        };

    private static TournamentJoin ToJoin(TournamentParticipantEntity participant)
        => new()
        {
            MemberNo = MemberNoIds.Format(participant.MemberNo),
            JoinSeqNo = checked((long)participant.JoinSequenceNo),
            JoinMemberNo = participant.JoinMemberNo,
            JoinStatus = participant.JoinStatus,
            TotManageNum = checked((int)participant.TotalManageCount),
            ManageNum = checked((int)participant.ManageCount),
            LastManageDt = participant.LastManageAt ?? default,
        };

    private static void ApplyDetail(TournamentRoomEntity room, TournamentDetail detail)
    {
        room.RoomId = checked((ushort)detail.RoomId);
        room.PlanStartAt = detail.StartPlanDt;
        room.PlanEndAt = detail.EndPlanDt;
        room.MemberNo1 = ParseNullableMemberNo(detail.PlayerMemberNo[0]);
        room.MemberNo2 = ParseNullableMemberNo(detail.PlayerMemberNo[1]);
        room.MemberNo3 = ParseNullableMemberNo(detail.PlayerMemberNo[2]);
        room.MemberNo4 = ParseNullableMemberNo(detail.PlayerMemberNo[3]);
        room.JoinMemberNo1 = detail.JoinMemberNo[0];
        room.JoinMemberNo2 = detail.JoinMemberNo[1];
        room.JoinMemberNo3 = detail.JoinMemberNo[2];
        room.JoinMemberNo4 = detail.JoinMemberNo[3];
        room.ScoreTmp1 = detail.PointTmp[0];
        room.ScoreTmp2 = detail.PointTmp[1];
        room.ScoreTmp3 = detail.PointTmp[2];
        room.ScoreTmp4 = detail.PointTmp[3];
        room.Score1 = detail.Point[0];
        room.Score2 = detail.Point[1];
        room.Score3 = detail.Point[2];
        room.Score4 = detail.Point[3];
        room.Rank1MemberNo = ParseNullableMemberNo(detail.GradePlayerMemberNo[0]);
        room.Rank2MemberNo = ParseNullableMemberNo(detail.GradePlayerMemberNo[1]);
        room.Rank3MemberNo = ParseNullableMemberNo(detail.GradePlayerMemberNo[2]);
        room.Rank4MemberNo = ParseNullableMemberNo(detail.GradePlayerMemberNo[3]);
        room.Grade1MemberNo = detail.GradeMemberNo[0];
        room.Grade2MemberNo = detail.GradeMemberNo[1];
        room.Grade3MemberNo = detail.GradeMemberNo[2];
        room.Grade4MemberNo = detail.GradeMemberNo[3];
        room.StartedAt = detail.StartDt == default ? null : detail.StartDt;
        room.EndedAt = detail.EndDt == default ? null : detail.EndDt;
    }

}
