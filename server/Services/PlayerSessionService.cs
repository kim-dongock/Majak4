using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using MajakServer.Models.Player;
using MajakServer.Models.Game;

namespace MajakServer.Services;

// ─── オートマッチング予約状態 ──────────────────────────────────────────────
// 原典: WAITINGPLAYER + AddReservePlayer 内部状態の合成
// mjkc2e 送信 → 全員 mjkc6e 完了 or 5秒タイムアウト まで保持する。

public class PendingAutoMatch
{
    public int      RoomId          { get; init; }
    public string   ChannelId       { get; init; } = "";
    public string[] ExpectedMembers { get; init; } = Array.Empty<string>();
    public HashSet<string> EnteredMembers  { get; } = new();
    public HashSet<string> RemovedMembers  { get; } = new();
    public string   RoomOption      { get; init; } = "";
    // AutoStart 用プレイヤー情報 (mjkc4e 送信時に使用)
    public IReadOnlyList<MajakPlayer> Players { get; init; } = Array.Empty<MajakPlayer>();
}

public sealed record ServerDisconnectReason(string Source, string Reason, DateTimeOffset At);

/// <summary>
/// 接続中プレイヤーセッション管理 (メモリ専用)
/// — 原典: GChnlServer 内のプレイヤーマップ + RoomSessionManager
/// Thread-safe: ConcurrentDictionary 使用
/// </summary>
public class PlayerSessionService
{
    private sealed class MemberEntryGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class MemberEntryLease : IDisposable
    {
        private PlayerSessionService? _owner;
        private readonly string _memberNo;
        private readonly MemberEntryGate _gate;

        public MemberEntryLease(PlayerSessionService owner, string memberNo, MemberEntryGate gate)
        {
            _owner = owner;
            _memberNo = memberNo;
            _gate = gate;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseMemberEntryLock(_memberNo, _gate);
        }
    }

    // ConnectionId → MajakPlayer
    private readonly ConcurrentDictionary<string, MajakPlayer> _byConnId = new();
    private readonly ConcurrentDictionary<string, string> _authMemberByConnId = new();
    private readonly ConcurrentDictionary<string, string> _authPixByConnId = new();
    // MemberNo → ConnectionId
    private readonly ConcurrentDictionary<string, string> _memberToConn = new();
    private readonly ConcurrentDictionary<string, string> _pixToMemberNo = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _memberNoToPix = new(StringComparer.Ordinal);
    // RoomId → GameRoom
    private readonly ConcurrentDictionary<int, GameRoom> _rooms = new();

    // チャンネル別インデックス (PerformanceAnalysis §2-1)
    // GetChannelMembers / GetAllChannelPlayers の O(N) 全体スキャンを O(1) に改善する。
    // chanelId → (ConnectionId → MajakPlayer)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MajakPlayer>> _byChannel = new();
    private readonly ConcurrentDictionary<string, ServerDisconnectReason> _disconnectReasons = new();
    private readonly Dictionary<string, MemberEntryGate> _memberEntryGates = new(StringComparer.Ordinal);
    private readonly object _memberEntryGatesSync = new();

    private int _nextRoomId = 0;

    // ─── プレイヤー ───────────────────────────────────────────────

    public async ValueTask<IDisposable> AcquireMemberEntryLockAsync(string memberNo)
    {
        MemberEntryGate gate;
        lock (_memberEntryGatesSync)
        {
            if (!_memberEntryGates.TryGetValue(memberNo, out gate!))
            {
                gate = new MemberEntryGate();
                _memberEntryGates[memberNo] = gate;
            }
            gate.ReferenceCount++;
        }

        await gate.Semaphore.WaitAsync();
        return new MemberEntryLease(this, memberNo, gate);
    }

    private void ReleaseMemberEntryLock(string memberNo, MemberEntryGate gate)
    {
        gate.Semaphore.Release();
        lock (_memberEntryGatesSync)
        {
            gate.ReferenceCount--;
            if (gate.ReferenceCount == 0
                && _memberEntryGates.TryGetValue(memberNo, out var current)
                && ReferenceEquals(current, gate))
            {
                _memberEntryGates.Remove(memberNo);
            }
        }
    }

    public void Register(MajakPlayer player)
    {
        if (string.IsNullOrWhiteSpace(player.Pix))
            player.Pix = GetPixByMemberNo(player.MemberNo) ?? IssuePix(player.MemberNo);
        else
        {
            if (_memberNoToPix.TryGetValue(player.MemberNo, out var oldPix) && oldPix != player.Pix)
                _pixToMemberNo.TryRemove(oldPix, out _);
            _pixToMemberNo[player.Pix] = player.MemberNo;
            _memberNoToPix[player.MemberNo] = player.Pix;
        }
        _byConnId[player.ConnectionId] = player;
        _memberToConn[player.MemberNo] = player.ConnectionId;
        // チャンネル別インデックスへも登録
        _byChannel
            .GetOrAdd(player.ChannelId, _ => new ConcurrentDictionary<string, MajakPlayer>())
            [player.ConnectionId] = player;
    }

    public void Remove(string connectionId)
    {
        if (_byConnId.TryRemove(connectionId, out var p))
        {
            ((ICollection<KeyValuePair<string, string>>)_memberToConn)
                .Remove(new KeyValuePair<string, string>(p.MemberNo, connectionId));
            // チャンネル別インデックスからも削除
            if (_byChannel.TryGetValue(p.ChannelId, out var ch))
            {
                ch.TryRemove(connectionId, out _);
                // チャンネルが空になったら辞書からも削除 (メモリリーク防止)
                if (ch.IsEmpty) _byChannel.TryRemove(p.ChannelId, out _);
            }
        }
        _authMemberByConnId.TryRemove(connectionId, out _);
        _authPixByConnId.TryRemove(connectionId, out _);
        _disconnectReasons.TryRemove(connectionId, out _);
    }

    public void SetAuthenticatedMember(string connectionId, string memberNo)
        => SetAuthenticatedMember(connectionId, memberNo, "");

    public void SetAuthenticatedMember(string connectionId, string memberNo, string pix)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(memberNo)) return;
        _authMemberByConnId[connectionId] = memberNo;
        if (!string.IsNullOrWhiteSpace(pix)) _authPixByConnId[connectionId] = pix;
    }

    public string? GetAuthenticatedMember(string connectionId)
        => _authMemberByConnId.TryGetValue(connectionId, out var memberNo) ? memberNo : null;

    public string? GetAuthenticatedPix(string connectionId)
        => _authPixByConnId.TryGetValue(connectionId, out var pix) ? pix : null;

    public void RecordDisconnectReason(string connectionId, string source, string reason)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return;
        _disconnectReasons[connectionId] = new ServerDisconnectReason(source, reason, DateTimeOffset.UtcNow);
    }

    public ServerDisconnectReason? PeekDisconnectReason(string connectionId)
        => _disconnectReasons.TryGetValue(connectionId, out var reason) ? reason : null;

    public MajakPlayer? GetByConn(string connectionId)
        => _byConnId.TryGetValue(connectionId, out var p) ? p : null;

    public MajakPlayer? GetByMember(string memberNo)
    {
        if (_memberToConn.TryGetValue(memberNo, out var connId))
            return GetByConn(connId);
        return null;
    }

    public bool IsCurrentConnection(string memberNo, string connectionId)
        => _memberToConn.TryGetValue(memberNo, out var currentConnectionId)
            && currentConnectionId == connectionId;

    public string IssuePix(string memberNo)
    {
        if (string.IsNullOrWhiteSpace(memberNo)) return string.Empty;

        if (_memberNoToPix.TryGetValue(memberNo, out var existingPix))
            return existingPix;

        string pix;
        do
        {
            pix = "pix" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }
        while (!_pixToMemberNo.TryAdd(pix, memberNo));

        _memberNoToPix[memberNo] = pix;
        return pix;
    }

    public string? GetPixByMemberNo(string memberNo)
        => _memberNoToPix.TryGetValue(memberNo, out var pix) ? pix : null;

    public string ResolveMemberNo(string publicOrMemberNo)
        => _pixToMemberNo.TryGetValue(publicOrMemberNo, out var memberNo) ? memberNo : publicOrMemberNo;

    public void SetPlayerChannel(MajakPlayer player, string channelId)
    {
        if (string.IsNullOrEmpty(channelId) || player.ChannelId == channelId) return;

        if (_byChannel.TryGetValue(player.ChannelId, out var oldChannel))
        {
            oldChannel.TryRemove(player.ConnectionId, out _);
            if (oldChannel.IsEmpty) _byChannel.TryRemove(player.ChannelId, out _);
        }

        player.ChannelId = channelId;
        _byChannel
            .GetOrAdd(channelId, _ => new ConcurrentDictionary<string, MajakPlayer>())
            [player.ConnectionId] = player;
    }

    /// <summary>ロビー待機中プレイヤー (ルーム未参加) を返す。O(channel size)</summary>
    public IEnumerable<MajakPlayer> GetChannelMembers(string channelId)
    {
        if (!_byChannel.TryGetValue(channelId, out var ch))
            return Enumerable.Empty<MajakPlayer>();
        return ch.Values.Where(p => p.RoomId == null);
    }

    /// <summary>
    /// チャンネル内の全プレイヤー (ロビー + ルーム参加中) を返す。O(channel size)
    /// 原典: HMajChnlServer::m_vecPlayer — 全メンバーを含む
    /// </summary>
    public IEnumerable<MajakPlayer> GetAllChannelPlayers(string channelId)
    {
        if (!_byChannel.TryGetValue(channelId, out var ch))
            return Enumerable.Empty<MajakPlayer>();
        return ch.Values;
    }

    // ─── ルーム ────────────────────────────────────────────────────

    public GameRoom CreateRoom(string channelId, MajakPlayer owner, string roomOption,
        long moneyRate, long minMoney, long maxMoney, bool isPrivate,
        string roomTitle = "", string roomPassword = "", string roomType = "",
        int maxViewer = 12,
        int cupId = 0, int cupSeq = 0, int cupJudgementType = -1, int cupPointSumType = 0,
        int cupMaxMatchCntLimit = -1, int cupConditionRegular = 0, int cupConditionBilling = 0, bool cupEntryLimited = false,
        string cupNormalYakuCondition = "", string cupYakumanCondition = "",
        string subId = "",
        long unitMoney = 0, int minCnt = 0,
        int roomId = 0)
    {
        int resolvedRoomId = roomId > 0 ? roomId : AllocateNextRoomId();
        if (roomId > 0 && _rooms.ContainsKey(resolvedRoomId))
            throw new InvalidOperationException($"Room slot {resolvedRoomId} is already in use.");

        var room = new GameRoom
        {
            RoomId           = resolvedRoomId,
            ChannelId        = channelId,
            RoomTitle        = roomTitle,
            IsPrivate        = isPrivate,
            RoomType         = roomType,
            Password         = roomPassword,
            RoomOption       = roomOption,
            MoneyRate        = moneyRate,
            UnitMoney        = unitMoney > 0 ? unitMoney : moneyRate,
            MinMoney         = minMoney,
            MaxMoney         = maxMoney,
            MinCnt           = minCnt,
            SubId            = subId,
            MaxViewer        = maxViewer,
            CreatorNo        = owner.MemberNo,  // 原典: m_pRoomInfo->m_szCreatorId
            CupId            = cupId,
            CupSeq           = cupSeq,
            CupJudgementType = cupJudgementType,
            CupPointSumType  = cupPointSumType,
            CupMaxMatchCntLimit = cupMaxMatchCntLimit,
            CupConditionRegular = cupConditionRegular,
            CupConditionBilling = cupConditionBilling,
            CupEntryLimited     = cupEntryLimited,
            CupNormalYakuCondition = cupNormalYakuCondition,
            CupYakumanCondition    = cupYakumanCondition,
        };
        // オーナーは 0番席
        room.AddPlayer(owner, 0);
        if (!_rooms.TryAdd(resolvedRoomId, room))
        {
            owner.RoomId = null;
            throw new InvalidOperationException($"Room slot {resolvedRoomId} is already in use.");
        }
        return room;
    }

    /// <summary>
    /// オートマッチング用の予約ルームを作成する。
    /// 原典: GoAutoMatching → AddReservePlayer はまだ着席させず、mjkc6e AutoEnterRoom で入室を確定する。
    /// </summary>
    public GameRoom CreateReservedRoom(string channelId, string roomOption,
        long moneyRate, long minMoney, long maxMoney, bool isPrivate,
        string roomTitle = "", string roomPassword = "",
        int maxViewer = 12,
        int cupId = 0, int cupSeq = 0, int cupJudgementType = -1, int cupPointSumType = 0,
        int cupMaxMatchCntLimit = -1, int cupConditionRegular = 0, int cupConditionBilling = 0, bool cupEntryLimited = false,
        string cupNormalYakuCondition = "", string cupYakumanCondition = "",
        string subId = "", long unitMoney = 0)
    {
        int roomId = AllocateNextRoomId();
        var room = new GameRoom
        {
            RoomId           = roomId,
            ChannelId        = channelId,
            RoomTitle        = roomTitle,
            IsPrivate        = isPrivate,
            Password         = roomPassword,
            RoomOption       = roomOption,
            MoneyRate        = moneyRate,
            UnitMoney        = unitMoney,
            MinMoney         = minMoney,
            MaxMoney         = maxMoney,
            MaxViewer        = maxViewer,
            CupId            = cupId,
            CupSeq           = cupSeq,
            CupJudgementType = cupJudgementType,
            CupPointSumType  = cupPointSumType,
            CupMaxMatchCntLimit = cupMaxMatchCntLimit,
            CupConditionRegular = cupConditionRegular,
            CupConditionBilling = cupConditionBilling,
            CupEntryLimited     = cupEntryLimited,
            CupNormalYakuCondition = cupNormalYakuCondition,
            CupYakumanCondition    = cupYakumanCondition,
            SubId            = subId,
        };
        _rooms[roomId] = room;
        return room;
    }

    private int AllocateNextRoomId()
    {
        int roomId;
        do
        {
            roomId = Interlocked.Increment(ref _nextRoomId);
        }
        while (_rooms.ContainsKey(roomId));
        return roomId;
    }

    public GameRoom? GetRoom(int roomId)
        => _rooms.TryGetValue(roomId, out var r) ? r : null;

    public bool RemoveRoom(int roomId)
        => _rooms.TryRemove(roomId, out _);

    public GameRoom? RemovePlayingRoomIfNoActivePlayers(int roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return null;
        lock (room)
        {
            if (room.State != GameRoomState.Playing || !room.HasNoActivePlayers)
                return null;
            if (!_rooms.TryRemove(roomId, out var removed))
                return null;
            ExpirePendingMatch(roomId);
            return removed;
        }
    }

    public IReadOnlyList<GameRoom> RemoveNoActivePlayingRooms()
    {
        var removed = new List<GameRoom>();
        foreach (var room in _rooms.Values.ToArray())
        {
            var emptyRoom = RemovePlayingRoomIfNoActivePlayers(room.RoomId);
            if (emptyRoom != null) removed.Add(emptyRoom);
        }
        return removed;
    }

    public IEnumerable<GameRoom> GetChannelRooms(string channelId)
        => _rooms.Values.Where(r => r.ChannelId == channelId);

    public (GameRoom Room, int SeatOrder)? FindTournamentRecoveryRoom(string channelId, string memberNo)
    {
        foreach (var room in _rooms.Values.Where(room =>
                     room.ChannelId == channelId
                     && room.TournamentSeqNo > 0
                     && room.State is GameRoomState.Waiting or GameRoomState.Playing))
        {
            int seatOrder = Array.FindIndex(room.Seats, seat => seat?.MemberNo == memberNo);
            if (seatOrder >= 0)
                return (room, seatOrder);

            var pending = GetPendingMatch(room.RoomId);
            if (pending != null && IsPendingMatchMember(pending, memberNo))
                return (room, -1);
        }

        return null;
    }

    public bool IsContinuePlayerInChannel(string channelId, string memberNo)
        => _rooms.Values.Any(room => room.ChannelId == channelId
            && room.State == GameRoomState.Playing
            && CanReconnectToRoom(room, memberNo));

    public bool CanReconnectToRoom(GameRoom room, string memberNo)
        => room.State == GameRoomState.Playing
            && room.Seats.Any(seat => seat?.MemberNo == memberNo && seat.IsOutPlayer);

    /// <summary>
    /// チャンネル内で現在セッションが把握している最大ルーム番号を返す。
    /// 原典の m_nMaxRoom は CHANELMAST 由来だが、ここでは Redis/マスタ参照を行わない。
    /// </summary>
    public int GetKnownChannelRoomSlotCount(string channelId)
        => _rooms.Values
            .Where(r => r.ChannelId == channelId)
            .Select(r => r.RoomId)
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>予約中オートマッチングルームかどうか。原典: GetReservePlayerCount() &gt; 0。</summary>
    public bool HasPendingMatch(int roomId)
        => _pendingMatches.ContainsKey(roomId);

    /// <summary>プレイヤーをルームに入室。空席を自動配置</summary>
    public bool JoinRoom(int roomId, MajakPlayer player)
    {
        var room = GetRoom(roomId);
        if (room == null) return false;
        lock (room)
        {
            if (room.State != GameRoomState.Waiting) return false;
            if (player.RoomId is int currentRoomId && currentRoomId != roomId) return false;
            if (room.Seats.Any(s => s?.MemberNo == player.MemberNo) || room.Viewers.Any(v => v.MemberNo == player.MemberNo)) return false;

            int limitCnt = room.LimitCnt > 0 ? room.LimitCnt : room.Seats.Length;
            int maxPlayers = Math.Min(limitCnt, room.Seats.Length);

            for (int i = 0; i < maxPlayers; i++)
            {
                if (room.Seats[i] == null)
                {
                    room.AddPlayer(player, i);
                    return true;
                }
            }
            return false; // 満席
        }
    }

    public void LeaveRoom(MajakPlayer player)
    {
        if (player.RoomId == null) return;
        var room = GetRoom(player.RoomId.Value);
        if (room == null) return;

        lock (room)
        {
            if (player.IsViewer || room.Viewers.Any(v => v.MemberNo == player.MemberNo))
            {
                room.RemoveViewer(player.MemberNo);
                player.IsViewer = false;
            }
            else
            {
                room.RemovePlayer(player.MemberNo);
            }
            player.RoomId = null;

            if (room.IsEmpty)
                _rooms.TryRemove(room.RoomId, out _);
        }
    }

    /// <summary>
    /// 対局中の切断処理 (続行サポート用)。
    /// 原典: DispatchRoomSocketClose の OutPlayer 管理
    ///   通常の LeaveRoom と異なり、座席からは除去しない。
    ///   IsOutPlayer フラグを立て、メンバーID→接続IDのマッピングのみ解除する。
    /// </summary>
    public bool DisconnectFromRoom(MajakPlayer player, string disconnectedConnectionId)
    {
        bool detachedSeat = false;
        if (player.RoomId is int roomId && _rooms.TryGetValue(roomId, out var room))
        {
            lock (room)
            {
                var roomPlayer = room.Seats.FirstOrDefault(seat => seat?.MemberNo == player.MemberNo);
                if (roomPlayer != null && roomPlayer.ConnectionId == disconnectedConnectionId)
                {
                    roomPlayer.IsOutPlayer = true;
                    roomPlayer.ConnectionId = "";
                    detachedSeat = true;
                }
                if (room.HasNoActivePlayers)
                    room.NoActiveMembersSince ??= DateTimeOffset.UtcNow;
                else
                    room.NoActiveMembersSince = null;
            }
        }
        if (detachedSeat)
        {
            player.IsOutPlayer = true;
            if (player.ConnectionId == disconnectedConnectionId)
                player.ConnectionId = "";
        }
        Remove(disconnectedConnectionId);
        // RoomId は保持したまま (IsOutPlayer=true で座席に残る)
        // player.IsOutPlayer はHub側で設定済み
        return detachedSeat;
    }

    public bool DisconnectFromRoom(MajakPlayer player)
        => DisconnectFromRoom(player, player.ConnectionId);

    public int RebindPlayingRoomPlayer(int roomId, MajakPlayer player)
    {
        var room = GetRoom(roomId);
        if (room?.State != GameRoomState.Playing) return -1;

        lock (room)
        {
            int seatIndex = Array.FindIndex(room.Seats, seat => seat?.MemberNo == player.MemberNo);
            if (seatIndex < 0) return -1;

            var seat = room.Seats[seatIndex]!;
            seat.ConnectionId = player.ConnectionId;
            seat.NickName = player.NickName;
            seat.AvatarId = player.AvatarId;
            seat.Password = player.Password;
            seat.IpAddress = player.IpAddress;
            seat.ChannelId = player.ChannelId;
            seat.IsOutPlayer = false;

            player.RoomId = roomId;
            player.SeatPos = seat.SeatPos;
            player.EngineOrder = seat.EngineOrder;
            player.IsOutPlayer = false;
            room.NoActiveMembersSince = null;
            _memberToConn[player.MemberNo] = player.ConnectionId;
            return seatIndex;
        }
    }

    /// <summary>
    /// 続行プレイヤーの再接続処理。
    /// 原典: AutoJoinRoom の IsContinuePlayer → FindContinuePlayer 分岐
    ///   OutPlayer として残っていた席に新しい ConnectionId で復帰する。
    /// 戻り値: 復帰した座席番号、見つからなければ -1
    /// </summary>
    public int ReconnectToRoom(int roomId, MajakPlayer player)
    {
        var room = GetRoom(roomId);
        if (room == null) return -1;

        lock (room)
        {
            // 同一 MemberNo で IsOutPlayer=true の席を検索
            bool hasDisconnectedSeat = room.Seats.Any(
                seat => seat?.MemberNo == player.MemberNo && seat.IsOutPlayer);
            return hasDisconnectedSeat ? RebindPlayingRoomPlayer(roomId, player) : -1;
        }
    }

    // ─── オートマッチングキュー ────────────────────────────────────────────

    private sealed record WaitingMatchPlayer(string MemberNo, DateTime EnqueuedAt);

    private readonly ConcurrentDictionary<string, List<WaitingMatchPlayer>> _matchQueues = new();

    public void EnqueueMatching(string channelId, string memberNo)
    {
        var q = _matchQueues.GetOrAdd(channelId, _ => new List<WaitingMatchPlayer>());
        lock (q)
        {
            q.RemoveAll(x => x.MemberNo == memberNo);
            q.Add(new WaitingMatchPlayer(memberNo, DateTime.UtcNow));
        }
    }

    public void DequeueMatching(string channelId, string memberNo)
    {
        if (!_matchQueues.TryGetValue(channelId, out var q)) return;
        lock (q)
        {
            q.RemoveAll(x => x.MemberNo == memberNo);
        }
    }

    /// <summary>4人マッチング試行。原典 GoAutoMatching と同じく待機時間で rating 許容幅を広げる。</summary>
    public string[]? TryMatch(string channelId, Func<string, int?> getRating)
    {
        if (!_matchQueues.TryGetValue(channelId, out var q)) return null;
        lock (q)
        {
            if (q.Count < 4) return null;

            var now = DateTime.UtcNow;
            foreach (var baseEntry in q.OrderBy(x => x.EnqueuedAt).ToList())
            {
                int? baseRating = getRating(baseEntry.MemberNo);
                if (baseRating == null) continue;

                int deltaRating = 50 + Math.Max(0, (int)(now - baseEntry.EnqueuedAt).TotalSeconds) * 10;
                var matched = new List<WaitingMatchPlayer> { baseEntry };
                foreach (var candidate in q)
                {
                    if (candidate.MemberNo == baseEntry.MemberNo) continue;
                    int? candidateRating = getRating(candidate.MemberNo);
                    if (candidateRating == null) continue;
                    if (candidateRating < baseRating.Value - deltaRating || candidateRating > baseRating.Value + deltaRating)
                        continue;
                    if (!CheckKeepList(matched, candidate.MemberNo)) continue;

                    matched.Add(candidate);
                    if (matched.Count >= 4) break;
                }

                if (matched.Count < 4) continue;

                foreach (var entry in matched)
                    q.Remove(entry);
                return matched.Select(x => x.MemberNo).ToArray();
            }

            return null;
        }
    }

    private bool CheckKeepList(IEnumerable<WaitingMatchPlayer> matched, string candidateMemberNo)
    {
        foreach (var entry in matched)
        {
            if (entry.MemberNo == candidateMemberNo) return false;

            var keepPlayer = GetByMember(entry.MemberNo);
            if (keepPlayer?.PreMatchMemberNos.Any(id => id == candidateMemberNo) == true)
                return false;
        }
        return true;
    }

    /// <summary>マッチング待機中のプレイヤーが存在する全チャンネル ID を返す</summary>
    public IEnumerable<string> GetMatchableChannelIds()
        => _matchQueues.Where(kv =>
        {
            lock (kv.Value) return kv.Value.Count >= 4;
        }).Select(kv => kv.Key);

    // ─── オートマッチング予約管理 (FAILEROOM 相当) ────────────────────────
    // 原典: AddReservePlayer / FindReservePlayer / RemoveReservePlayer
    // mjkc2e 送信後、mjkc6e (AutoEnterRoom) で全員揃うまでの中間状態を管理する。
    // 5秒以内に全員が mjkc6e を送らなければ FAILEROOM タイマーが発火する。

    private readonly ConcurrentDictionary<int, PendingAutoMatch> _pendingMatches = new();

    /// <summary>予約登録 (GoAutoMatching → AddReservePlayer 相当)</summary>
    public void RegisterPendingMatch(PendingAutoMatch match)
        => _pendingMatches[match.RoomId] = match;

    public PendingAutoMatch? GetPendingMatch(int roomId)
        => _pendingMatches.TryGetValue(roomId, out var m) ? m : null;

    public bool IsPendingMatchMember(PendingAutoMatch match, string memberNo)
    {
        lock (match)
        {
            return match.ExpectedMembers.Contains(memberNo) && !match.RemovedMembers.Contains(memberNo);
        }
    }

    public void RemovePendingMatchMember(int roomId, string memberNo)
    {
        if (!_pendingMatches.TryGetValue(roomId, out var match)) return;
        lock (match)
        {
            match.RemovedMembers.Add(memberNo);
            match.EnteredMembers.Remove(memberNo);
        }
    }

    /// <summary>
    /// AutoEnterRoom (mjkc6e) 確定処理。
    /// 返り値: (全員揃った, PendingAutoMatch) — 全員揃った場合は pending を削除済み。
    /// </summary>
    public (bool AllEntered, PendingAutoMatch? Match) ConfirmAutoEntry(int roomId, string memberNo)
    {
        if (!_pendingMatches.TryGetValue(roomId, out var match)) return (false, null);

        lock (match)
        {
            if (match.RemovedMembers.Contains(memberNo)) return (false, match);
            match.EnteredMembers.Add(memberNo);
            if (match.EnteredMembers.Count >= match.ExpectedMembers.Length)
            {
                _pendingMatches.TryRemove(roomId, out _);
                return (true, match);
            }
            return (false, match);
        }
    }

    /// <summary>FAILEROOM タイムアウト時に呼ぶ。未揃いなら取り除いて返す。</summary>
    public PendingAutoMatch? ExpirePendingMatch(int roomId)
    {
        _pendingMatches.TryRemove(roomId, out var match);
        return match;
    }

    // ─── サーバー負荷情報 ────────────────────────────────────────────────

    /// <summary>
    /// 現在のアクティブルーム総数を返す。
    /// ServerStatusBackgroundService が Redis へのルーム数登録に使用する。
    /// </summary>
    public int GetTotalRoomCount() => _rooms.Count;

    /// <summary>
    /// 全アクティブルームを返す。
    /// ServerStatusBackgroundService が Redis TTL リフレッシュに使用する。
    /// </summary>
    public IEnumerable<GameRoom> GetAllRooms() => _rooms.Values;

    /// <summary>
    /// このサーバーに WebSocket 接続しているプレイヤーが存在するチャンネル ID 一覧を返す。
    /// ServerStatusBackgroundService がチャンネルリース TTL のハートビートに使用する。
    /// </summary>
    public IEnumerable<string> GetActiveChannelIds()
        => _byChannel
            .Where(kv => !kv.Value.IsEmpty)
            .Select(kv => kv.Key)
            .Distinct();

    // ─── REST ポーリング用マッチング結果ストア ───────────────────────────────
    // AutoMatchingBackgroundService がルーム作成後にここへ結果を書き込む。
    // REST クライアント (LobbyScreen) は GET /api/matching/status でポーリングし、
    // 結果があれば RoomScreen へ遷移する。

    public sealed record MatchResult(
        int    RoomId,
        string ChannelId,
        string ServerUrl,
        string RoomOption,
        int    GemGame);

    private readonly ConcurrentDictionary<string, MatchResult> _matchResults = new();

    /// <summary>AutoMatchingBackgroundService から呼び出す。</summary>
    public void StoreMatchResult(string memberNo, MatchResult result)
        => _matchResults[memberNo] = result;

    /// <summary>
    /// 結果を取り出して返す (一度読んだら削除)。
    /// REST GET /api/matching/status から呼び出す。
    /// </summary>
    public MatchResult? TakeMatchResult(string memberNo)
    {
        _matchResults.TryRemove(memberNo, out var r);
        return r;
    }
}
