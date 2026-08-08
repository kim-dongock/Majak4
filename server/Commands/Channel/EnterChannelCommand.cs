using MajakServer.Infrastructure;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MajakServer.Commands.Channel;

/// <summary>
/// c1e チャンネル入室、E

/// </summary>
public class EnterChannelCommand : ICommand
{
    private const int EvtBillingFree = 2;
    private const string Majak2CupEventCode = "MAJAK2CUP";

    private readonly PlayerSessionService            _session;
    private readonly PlayerRepository                _playerRepo;
    private readonly RatingService                   _ratingService;
    private readonly GameMoneyService                _moneyService;
    private readonly ItemService                     _itemService;
    private readonly TitleService                    _titleService;
    private readonly RoomRegistryService             _roomRegistry;
    private readonly ChannelMemberService            _channelMemberSvc;
    private readonly IOptions<ChannelServerSettings> _channelSettings;
    private readonly ServerLoadService               _loadService;
    private readonly GradeRankService                _gradeRank;
    private readonly MasterCacheService              _masterCache;
    private readonly AdminIdService                  _adminIdService;
    private readonly MenteTimeService                _menteTime;
    private readonly ILogger<EnterChannelCommand>    _logger;
    private readonly LobbySessionLeaseService?       _lobbySessions;

    public EnterChannelCommand(
        PlayerSessionService            session,
        PlayerRepository                playerRepo,
        RatingService                   ratingService,
        GameMoneyService                moneyService,
        ItemService                     itemService,
        TitleService                    titleService,
        RoomRegistryService             roomRegistry,
        ChannelMemberService            channelMemberSvc,
        IOptions<ChannelServerSettings> channelSettings,
        ServerLoadService               loadService,
        GradeRankService                gradeRank,
        MasterCacheService              masterCache,
        AdminIdService                  adminIdService,
        MenteTimeService                menteTime,
        ILogger<EnterChannelCommand>    logger,
        LobbySessionLeaseService?       lobbySessions = null)
    {
        _session          = session;
        _playerRepo       = playerRepo;
        _ratingService    = ratingService;
        _moneyService     = moneyService;
        _itemService      = itemService;
        _titleService     = titleService;
        _roomRegistry     = roomRegistry;
        _channelMemberSvc = channelMemberSvc;
        _channelSettings  = channelSettings;
        _loadService      = loadService;
        _gradeRank        = gradeRank;
        _masterCache      = masterCache;
        _adminIdService   = adminIdService;
        _menteTime        = menteTime;
        _logger           = logger;
        _lobbySessions    = lobbySessions;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        string channelId = First(ctx.GetString(GKey.ChannelId), ctx.GetString("channelId"));
        string pix       = First(ctx.GetString("pix"), ctx.GetString(GKey.Pix), ctx.GetString("memberNo"));
        string memberNo  = !string.IsNullOrWhiteSpace(ctx.AuthMemberNo)
            ? ctx.AuthMemberNo
            : _session.ResolveMemberNo(pix);
        using var memberEntryLease = await _session.AcquireMemberEntryLockAsync(memberNo);
        string nickname  = First(ctx.GetString(GKey.Name), ctx.GetString("name"), ctx.GetString("nickname"));
        string avatarId  = First(ctx.GetString(GKey.AvatarId), ctx.GetString("avatarId"));
        string password  = ctx.GetString("password");
        string tabId     = ctx.GetString("tabId");

        _logger.LogInformation(
            "EnterChannel requested. channelId={ChannelId} subId={SubId} memberNo={MemberNo} connectionId={ConnectionId}",
            channelId, ExtractSubId(channelId), memberNo, ctx.ConnectionId);

        if (!string.IsNullOrWhiteSpace(ctx.AuthMemberNo)
            && !string.IsNullOrWhiteSpace(pix)
            && pix != memberNo
            && pix != ctx.AuthPix
            && _session.ResolveMemberNo(pix) != memberNo)
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("AUTH_REQUIRED", "Authentication could not be verified. Please log in again.", channelId, pix));
            return;
        }

        if (string.IsNullOrWhiteSpace(tabId))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("INVALID_TAB_SESSION", "接続情報を確認できません。画面を再読み込みしてください。", channelId, memberNo));
            return;
        }

        LobbySessionLeaseHandle? lobbyLease = null;
        if (_lobbySessions != null)
        {
            var leaseAttempt = await _lobbySessions.TryAcquireAsync(memberNo, ctx.ConnectionId, tabId);
            if (leaseAttempt.Status == LobbySessionLeaseStatus.Denied)
            {
                await ctx.Caller.SendAsync(Cmd.EnterChannel,
                    FailPayload("USER_MULTI_LOGIN", "同じIDで既に接続しています。", channelId, memberNo));
                return;
            }
            lobbyLease = leaseAttempt.Lease;
        }
        await using var pendingLobbyLease = lobbyLease;

        if (ctx.GetBool("abandonPreviousRoom"))
            await TryAbandonPreviousRoomAsync(
                ctx,
                memberNo,
                channelId,
                ctx.GetInt("abandonRoomId"),
                ctx.GetBool("abandonRoomAfterFatalError"));

        var current = _session.GetByConn(ctx.ConnectionId);
        bool sameConnectionSameChannel = current != null && current.MemberNo == memberNo && current.ChannelId == channelId;
        if (sameConnectionSameChannel)
        {
            current!.NickName = nickname;
            current.AvatarId = avatarId;
            current.Password = password;
            current.TabId = tabId;
            current.IpAddress = ctx.RemoteIpAddress;
            await ctx.Groups.AddToGroupAsync(ctx.ConnectionId, $"chanel_{channelId}");
            lobbyLease?.Commit();
            _logger.LogInformation(
                "EnterChannel refreshing same connection already in channel. channelId={ChannelId} subId={SubId} memberNo={MemberNo} connectionId={ConnectionId}",
                channelId, ExtractSubId(channelId), memberNo, ctx.ConnectionId);
            await SendSameConnectionRefreshAsync(ctx, current, channelId);
            return;
        }

        var existing = _session.GetByMember(memberNo);
        if (existing != null && existing.ConnectionId != ctx.ConnectionId)
        {
            bool isSameTab = existing.TabId == tabId;
            bool isContinuePlayer = false;
            if (existing.RoomId is int existingRoomId)
            {
                var existingRoom = _session.GetRoom(existingRoomId);
                if (existingRoom?.State == Models.Game.GameRoomState.Playing)
                {
                    isContinuePlayer = existingRoom.Seats.Any(
                        seat => seat?.MemberNo == memberNo && seat.IsOutPlayer);
                    if (!isContinuePlayer)
                    {
                        await ctx.Caller.SendAsync(Cmd.EnterChannel,
                            FailPayload("USER_MULTI_LOGIN", "同じIDで既にゲームに参加しています。", channelId, memberNo));
                        return;
                    }
                }
            }

            if (!isSameTab)
            {
                await ctx.Caller.SendAsync(Cmd.EnterChannel,
                    FailPayload("USER_MULTI_LOGIN", "同じIDで既に接続しています。", channelId, memberNo));
                return;
            }

            await RemoveSameTabPreviousSessionAsync(ctx, existing, isContinuePlayer);

            if (isContinuePlayer)
            {
                _logger.LogInformation(
                    "EnterChannel continuing disconnected game player. memberNo={MemberNo} previousConnectionId={PreviousConnectionId}",
                    memberNo, existing.ConnectionId);
            }
        }

        var player = sameConnectionSameChannel
            ? current!
            : new MajakPlayer
            {
                ConnectionId = ctx.ConnectionId,
                MemberNo     = memberNo,
                Pix          = _session.GetPixByMemberNo(memberNo) ?? pix,
                NickName     = nickname,
                AvatarId     = avatarId,
                TabId        = tabId,
                ChannelId    = channelId,
                Password     = password,
                IpAddress    = ctx.RemoteIpAddress,
                IsGuestId    = ctx.Player?.IsGuestId ?? false,
                IsNetCafeIp  = ctx.Player?.IsNetCafeIp ?? false,
            };

        player.IsAdminId = _adminIdService.IsAdminId(memberNo);

        if (!player.IsAdminId && _menteTime.IsLimitPlayTime())
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("LIMIT_PLAY_TIME", "現在はサービス利用制限時間帯のため入場できません。", channelId, memberNo));
            return;
        }

        string subId           = ExtractSubId(channelId);
        bool isGradeChannel    = subId.Length > 2 && subId[2] == 'G';
        bool isCompeteChannel  = subId.Length > 2 && subId[2] == 'R';
        bool isTrainingChannel = subId.Length > 2 && subId[2] == 'T';
        bool isHiClassChannel  = !isGradeChannel;
        bool isCircleChannel   = subId == "00000";
        bool isBeginnerChannel = subId.Length > 0 && subId[0] == '1';

        if (await ShouldRejectStoppedCupEnterAsync(channelId, memberNo, subId))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("SERVICE_MAINTENANCE", "現在は開催時間ではありません。\nスケジュールを確認してください。", channelId, memberNo));
            return;
        }

        var cupConfigs = await _masterCache.GetCupConfigsAsync();
        var thisCup    = cupConfigs.FirstOrDefault(c => c.ChannelId == channelId);
        int? cupRatId  = GetFestiveCupRatId(subId, thisCup);

        if (player.IsGuestId)
        {
            try
            {
                await _playerRepo.ResetGuestGameRecordsAsync(memberNo);
            }
            catch
            {
                await ctx.Caller.SendAsync(Cmd.EnterChannel,
                    FailPayload("GENERAL_DB_ERROR", "ユーザー情報の取得に失敗しました。", channelId, memberNo));
                return;
            }
        }

        if (!await _playerRepo.ExistsCommonRatAsync(memberNo))
        {
            await _moneyService.CreateCommonRatWithDefaultMoneyHistAsync(memberNo, GameConst.DefaultMoney, ctx.RemoteIpAddress);
        }

        if (!await _playerRepo.LoadCommonRatAsync(player))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("GENERAL_DB_ERROR", "ユーザー情報の取得に失敗しました。", channelId, memberNo));
            return;
        }

        if (!isTrainingChannel)
            await _playerRepo.EnsureSubRecordAsync(memberNo, isGradeChannel, isCompeteChannel, isHiClassChannel, cupRatId);

        if (!await _playerRepo.LoadHangeRatAsync(player) && !isGradeChannel)
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("GENERAL_DB_ERROR", "戦績情報の取得に失敗しました。", channelId, memberNo));
            return;
        }

        bool loadedHiClassRat = await _playerRepo.LoadHiClassRatAsync(player);
        if (isHiClassChannel && !isTrainingChannel && !loadedHiClassRat)
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("GENERAL_DB_ERROR", "上級戦績情報の取得に失敗しました。", channelId, memberNo));
            return;
        }

        if (isCompeteChannel && !await _playerRepo.LoadCompeteRatAsync(player))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("GENERAL_DB_ERROR", "競技戦績情報の取得に失敗しました。", channelId, memberNo));
            return;
        }

        if (cupRatId.HasValue && !await _playerRepo.LoadCupRatAsync(player, cupRatId.Value))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("GENERAL_DB_ERROR", "カップ戦績情報の取得に失敗しました。", channelId, memberNo));
            return;
        }

        await _playerRepo.LoadSkinListAsync(player);
        var titles = await _playerRepo.GetTitleListAsync(memberNo);

        bool loadedGradeRat = await _playerRepo.LoadGradeRatAsync(player);
        if (isGradeChannel && !loadedGradeRat)
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("GENERAL_DB_ERROR", "段位戦績情報の取得に失敗しました。", channelId, memberNo));
            return;
        }

        if (isCircleChannel)
        {
            player.CircleInfo = await _playerRepo.GetCircleInfoAsync(memberNo);
            if (player.CircleInfo.Count == 0)
            {
                await ctx.Caller.SendAsync(Cmd.EnterChannel,
                    FailPayload("NOT_CIRCLE_MEMBER", "このチャンネルに入れるのは認められたサークルの参加者のみです。", channelId, memberNo));
                return;
            }
        }

        await _itemService.EnsureDefaultItemsAsync(player);
        await _itemService.LoadMajItemsAsync(player);
        _logger.LogInformation(
            "EnterChannel majItems loaded. memberNo={MemberNo} count={Count} items={Items}",
            memberNo,
            player.MajItems.Count,
            string.Join("; ", player.MajItems.Select(item =>
                $"{item.ItemCode}:use={(item.UseFlag ? 1 : 0)},qty={item.Qty},buy={item.BuyDt:O},end={item.EndDt:O}")));
        foreach (var t in titles)
        {
            if (t.StartsWith("mjkt") && int.TryParse(t[4..], out int n) && n < 32)
                player.TitleClear[n] = 1;
            if (t.StartsWith("mjks") && int.TryParse(t[4..], out int trickCode))
            {
                int atr = trickCode / 3;
                int lev = trickCode % 3;
                if (atr >= 0 && atr < player.TrickLevel.Length && lev > player.TrickLevel[atr])
                    player.TrickLevel[atr] = lev;
            }
            if (t.StartsWith("mjkt5"))
                player.GradeTitleList.Add(t);
        }

        var receiveGiftTask = _playerRepo.ReceiveGeneralEventGiftAsync(player);
        if (receiveGiftTask != null && !await receiveGiftTask)
        {
            _logger.LogWarning(
                "EnterChannel login event gift receive failed but login continues. memberNo={MemberNo} eventCode={EventCode} eventNo={EventNo}",
                memberNo, GameConst.LoginGiftEventCode, GameConst.LoginGiftEventNo);
        }

        if (isTrainingChannel)
        {
            player.GamMoney   = 100_000;
            player.Experience = 0;
        }
        else
        {
            long prevMoney  = player.GamMoney;
            int  prevRating = player.Rating;

            player.GamMoney += player.GamMoneyU;
            if (player.GamMoney < 0) player.GamMoney = 0;
            player.GamMoney += player.EarnedMoney;
            if (player.GamMoney < 0) player.GamMoney = 0;

            _ratingService.UpdatePlayerLevel(player);

            if (player.Rating != prevRating || player.GamMoney != prevMoney
                || player.EarnedMoney != 0 || player.GamMoneyU != 0)
            {
                await _playerRepo.UpdateCommonRatAsync(player);
                player.EarnedMoney = 0;
                player.GamMoneyU   = 0;
            }
        }

        if (isBeginnerChannel && (player.RegularRecord.MatchCnt > 10 || player.GamMoney < 0))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("BEGINNER_LIMIT", "このロビーに入れるのは対局数 10 以下までです。", channelId, memberNo));
            return;
        }

        if (isGradeChannel && !_ratingService.CheckEnterGradeMode(player.GradeRecord.Grade, player.GamMoney, subId))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("GRADE_LIMIT", "このグレードチャンネルには入場できません。必要マネーまたは段位条件を満たしていません。", channelId, memberNo));
            return;
        }

        player.ActiveRecord = isGradeChannel    ? player.GradeRecord
                            : isCompeteChannel  ? player.CompeteRecord
                            :                     player.RegularRecord;
        player.IsPro = _gradeRank.IsPro(memberNo);
        player.ProPictureUrl = player.IsPro ? _gradeRank.GetProPictureUrl(memberNo) : "";

        bool isHiEventCup = thisCup != null && IsHiEventSubId(subId);
        if (isHiEventCup)
        {
            await _playerRepo.LoadCupEvtRatAsync(player, thisCup!.CupId, thisCup.EntryLimited);
        }

        if (ShouldRejectCupEntry(thisCup, player))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("CUP_ENTRY_LIMIT", "本戦には予選を通過したプレイヤーだけが入場できます。", channelId, memberNo));
            return;
        }

        if (ShouldRejectCupMinLevel(thisCup, player))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("CUP_MINLEVEL", $"このロビーは{_ratingService.GetSLevel(thisCup!.MinLevel)}以上で入場できます。", channelId, memberNo));
            return;
        }

        if (ShouldRejectCupMaxLevel(thisCup, player))
        {
            await ctx.Caller.SendAsync(Cmd.EnterChannel,
                FailPayload("CUP_MAXLEVEL", $"このロビーに入れるのは{_ratingService.GetSLevel(thisCup!.MaxLevel)}までです。", channelId, memberNo));
            return;
        }

        _ratingService.UpdatePlayerLevel(player);
        _session.Register(player);
        lobbyLease?.Commit();

        await _playerRepo.SetDailyMissionAsync(memberNo, conditionType: 1, progressIncrement: 1);
        await ctx.Groups.AddToGroupAsync(ctx.ConnectionId, $"chanel_{channelId}");

        _ = _loadService.ClaimChannelAsync(channelId, _channelSettings.Value.ServerUrl);
        _ = _channelMemberSvc.EnterAsync(
            channelId, memberNo, nickname,
            player.ActiveRecord.Rating, player.Sex, avatarId);
        _logger.LogInformation(
            "EnterChannel member registered. channelId={ChannelId} subId={SubId} memberNo={MemberNo}",
            channelId, ExtractSubId(channelId), memberNo);

        int[] exScores = isGradeChannel
            ? new[] { player.GradeRecord.Rating, player.GradeRecord.MatchCnt }
            : new[] { player.RegularRecord.Rating, player.RegularRecord.MatchCnt };
        if (thisCup != null)
            exScores = exScores.Concat(new[] { player.CupRec.CupPoint, player.CupRec.CupMatchCnt }).ToArray();
        string playerLocation = player.RoomId.HasValue ? "ルーム" : "ロビー";

        if (!sameConnectionSameChannel)
        {
            await ctx.Clients.OthersInGroup($"chanel_{channelId}")
                .SendAsync(Cmd.AddMember, new
                {
                    memberNo   = player.Pix,
                    pix        = player.Pix,
                    k3e        = player.Pix,
                    avatarId   = avatarId,
                    k7e        = avatarId,
                    name       = nickname,
                    nickname   = player.NickName,
                    mjkk34e    = player.NickName,
                    sex        = player.Sex,
                    k11e       = player.Sex,
                    location   = playerLocation,
                    k12e       = playerLocation,
                    matchCnt   = player.ActiveRecord.MatchCnt,
                    k26e       = player.ActiveRecord.MatchCnt,
                    winCnt     = player.ActiveRecord.WinCnt,
                    k27e       = player.ActiveRecord.WinCnt,
                    defeatCnt  = player.ActiveRecord.DefeatCnt,
                    k28e       = player.ActiveRecord.DefeatCnt,
                    drawCnt    = player.ActiveRecord.DrawCnt,
                    k29e       = player.ActiveRecord.DrawCnt,
                    rating     = player.ActiveRecord.Rating,
                    k31e       = player.ActiveRecord.Rating,
                    slevel     = player.SLevel,
                    k32e       = player.SLevel,
                    nlevel     = player.NLevel,
                    k33e       = player.NLevel,
                    gammoney   = player.GamMoney,
                    k34e       = player.GamMoney,
                    dispRange  = player.DispRange,
                    exScoreCnt = exScores.Length,
                    k151e      = exScores.Length,
                    exScores,
                    gradeCurrLevel = player.GradeRecord.Grade,
                    mjkk70e    = player.GradeRecord.Grade,
                    roomId     = player.RoomId ?? 0,
                });
        }

        if (!string.IsNullOrEmpty(player.NickName) && player.NickName != nickname)
        {
            string noticeMsg = $"{player.NickName} ({memberNo}) がチャンネルに入場しました";
            await ctx.Clients.OthersInGroup($"chanel_{channelId}")
                .SendAsync(Cmd.Notice, NoticePayload.Channel(noticeMsg, channelId));
        }
        else if (player.MajakTitleId >= 1000)
        {
            string? titleName = _titleService.GetTitleName($"mjkt{player.MajakTitleId}");
            if (!string.IsNullOrEmpty(titleName))
            {
                string noticeMsg = $"{titleName} の {memberNo} が入場しました。";
                await ctx.Clients.OthersInGroup($"chanel_{channelId}")
                    .SendAsync(Cmd.Notice, NoticePayload.Channel(noticeMsg, channelId));
            }
        }
        if (player.CupEvtRec.EntryTitle == 202)
        {
            string noticeMsg = $"雀龍ロビーに{memberNo}が入場しました。";
            await ctx.Clients.Group($"chanel_{channelId}")
                .SendAsync(Cmd.Notice, NoticePayload.Channel(noticeMsg, channelId));
        }

        var channelMembers = _session.GetAllChannelPlayers(channelId).Select(m => new
        {
            memberNo = m.Pix,
            pix      = m.Pix,
            nickname = m.NickName,
            avatarId = m.AvatarId,
            rating   = m.ActiveRecord.Rating,
            nlevel   = m.NLevel,
            slevel   = m.SLevel,
            sex      = m.Sex,
            matchCnt = m.ActiveRecord.MatchCnt,
            winCnt   = m.ActiveRecord.WinCnt,
            defeatCnt = m.ActiveRecord.DefeatCnt,
            drawCnt  = m.ActiveRecord.DrawCnt,
            roomId   = m.RoomId ?? 0,
            location = FormatMemberLocation(m.RoomId),
        });
        var channelRooms = (await _roomRegistry.GetChannelRoomsAsync(channelId))
            .Select(r => new { registry = r, session = _session.GetRoom(r.RoomId) })
            .Where(x => x.session is not { HasNoActiveMembers: true }
                || x.session.State == Models.Game.GameRoomState.Playing)
            .Select(x => x.session is not null
                ? GetRoomListCommand.BuildRoomListEntry(x.session, x.registry.ServerUrl)
                : new Dictionary<string, object?>
            {
                ["roomId"] = x.registry.RoomId,
                ["title"] = x.registry.Title,
                ["isPrivate"] = x.registry.IsPrivate,
                ["memberCnt"] = x.registry.MemberCnt,
                ["memberMax"] = x.registry.MemberMax,
                ["roomOption"] = x.registry.RoomOption,
                ["serverUrl"] = x.registry.ServerUrl,
                ["maxViewer"] = x.registry.MaxViewer,
                ["state"] = x.registry.State > 0 ? x.registry.State : null,
                ["roomPlaying"] = x.registry.RoomPlaying > 0 ? x.registry.RoomPlaying : null,
            });
        var channelList = await _masterCache.GetChannelListAsync();
        var channelInfo = channelList.FirstOrDefault(c => c.SubId == subId || c.ChanelId == channelId);
        var chanelName  = ChannelRepository.RepairDisplayName(subId, channelInfo?.ChanelName ?? channelId);
        string trickTitleName = _titleService.GetTitleName(player.TrickTitle) ?? "";
        string majakTitleName = _titleService.GetTitleName(player.MajakTitle) ?? "";
        var now = DateTime.Now;
        GameMoneyService.RefreshReplenishmentDay(player, now);
        int restAllInCnt = Math.Max(0, GameConst.AllinCountMax - player.AllinCnt);
        string eventCode = isHiEventCup ? Majak2CupEventCode : "";
        int eventNo = isHiEventCup ? thisCup!.CupId : 0;
        string skinInfo = string.Concat(player.SkinList.Select(s =>
        {
            int daysLeft = Math.Max(0, (int)(s.EndDate - now).TotalDays) + 1;
            return $"{s.SkinNo}\t{daysLeft}\t{(s.AttachFlag ? 1 : 0)}\t";
        }));
        var tournamentRecovery = _session.FindTournamentRecoveryRoom(channelId, memberNo);
        int tournamentRoomId = tournamentRecovery?.Room.RoomId ?? 0;
        int tournamentRoomOrder = Math.Max(0, tournamentRecovery?.SeatOrder ?? 0);
        _logger.LogInformation(
            "EnterChannel sending response. channelId={ChannelId} subId={SubId} memberNo={MemberNo} members={MemberCount} rooms={RoomCount}",
            channelId, ExtractSubId(channelId), memberNo, channelMembers.Count(), channelRooms.Count());
        await ctx.Caller.SendAsync(Cmd.EnterChannel, new
        {
            result      = 1,
            k1e         = GKey.ValueSuccess,
            memberNo    = player.Pix,
            pix         = player.Pix,
            k3e         = player.Pix,
            avatarId    = player.AvatarId,
            k7e         = player.AvatarId,
            sex         = player.Sex,
            k11e        = player.Sex,
            location    = FormatMemberLocation(player.RoomId),
            k12e        = FormatMemberLocation(player.RoomId),
            channelName = chanelName,
            gammoney    = player.GamMoney,
            k34e        = player.GamMoney,
            matchCnt    = player.ActiveRecord.MatchCnt,
            k26e        = player.ActiveRecord.MatchCnt,
            winCnt      = player.ActiveRecord.WinCnt,
            k27e        = player.ActiveRecord.WinCnt,
            defeatCnt   = player.ActiveRecord.DefeatCnt,
            k28e        = player.ActiveRecord.DefeatCnt,
            drawCnt     = player.ActiveRecord.DrawCnt,
            k29e        = player.ActiveRecord.DrawCnt,
            rating      = player.ActiveRecord.Rating,
            k31e        = player.ActiveRecord.Rating,
            nlevel      = player.NLevel,
            k33e        = player.NLevel,
            slevel      = player.SLevel,
            k32e        = player.SLevel,
            gemcount    = player.GemCount,
            mjkk55e     = player.GemCount,
            cashCount   = player.CashCount,
            experience  = player.Experience,
            mjkk36e     = player.Experience,
            lentMoney   = 0,
            mjkk41e     = 0,
            restAllInCnt,
            mjkk43e     = restAllInCnt,
            allInCnt    = player.AllinCnt,
            mjkk44e     = player.AllinCnt,
            tricktitle  = player.TrickTitle,
            mjkk46e     = player.TrickTitleId,
            majaktitle  = player.MajakTitle,
            mjkk47e     = player.MajakTitleId,
            trickTitleName,
            mjkk51e     = trickTitleName,
            majakTitleName,
            mjkk52e     = majakTitleName,
            grade       = player.GradeRecord.Grade,
            exScoreCnt  = exScores.Length,
            k151e       = exScores.Length,
            exScores,
            judgementType = thisCup?.JudgementType ?? -1,
            cupPointSumType = thisCup?.CupPointSumType ?? 0,
            eventCode,
            smmk18e = eventCode,
            eventNo,
            smmk17e = eventNo,
            totalPoint = player.CupEvtRec.TotalPoint,
            matchCntEvent = player.CupEvtRec.MatchCnt,
            points = player.CupEvtRec.Points,
            netCafe     = player.IsNetCafeIp,
            dispRange   = player.DispRange,
            k448e       = player.DispRange,
            majItems    = player.MajItems.Select(i => new
            {
                itemCode = i.ItemCode,
                buyDate  = ToEpochSeconds(i.BuyDt),
                endDate  = ToEpochSeconds(i.EndDt),
                quantity = i.Qty,
                useFlag  = i.UseFlag ? 1 : 0,
            }).ToArray(),
            customEquips = player.CustomItems
                .Where(kv => kv.Value.Equip == 1)
                .Select(kv => new { customType = kv.Value.Kind, customId = kv.Key })
                .ToArray(),
            circles = player.CircleInfo
                .Select(kv => new { circleId = kv.Key, circleName = kv.Value })
                .ToArray(),
            skinDataCount = player.SkinList.Count,
            skinInfo,
            mjkk39e = player.SkinList.Count,
            mjkk40e = skinInfo,
            tournamentRoomId,
            mjkk102e = tournamentRoomId,
            tournamentRoomOrder,
            mjkk103e = tournamentRoomOrder,
            members     = channelMembers,
            rooms       = channelRooms,
        });
        _logger.LogInformation(
            "EnterChannel succeeded. channelId={ChannelId} subId={SubId} memberNo={MemberNo} members={MemberCount} rooms={RoomCount}",
            channelId, ExtractSubId(channelId), memberNo, channelMembers.Count(), channelRooms.Count());

        var presents = await _playerRepo.GetUserPresentAsync(player);
        if (presents.Count > 0)
        {
            var packet = new Dictionary<string, object>();
            for (int i = 0; i < presents.Count; i++)
            {
                var p = presents[i];
                string msg = !string.IsNullOrEmpty(p.PresentInfo)
                    ? "おしらせ\t" + p.PresentInfo
                    : BuildPresentMessage(p.PresentKbn, p.PresentNum, p.PresentId);
                packet[Key.DeliveryMessage + i] = msg;
            }
            packet[GKey.Count] = presents.Count;

            await ctx.Caller.SendAsync(Cmd.DeliveryMessage, packet);
        }
    }

    private async Task SendSameConnectionRefreshAsync(
        CommandContext ctx,
        MajakPlayer player,
        string channelId)
    {
        var channelMembers = _session.GetAllChannelPlayers(channelId).Select(member => new
        {
            memberNo = member.Pix,
            pix = member.Pix,
            nickname = member.NickName,
            avatarId = member.AvatarId,
            rating = member.ActiveRecord.Rating,
            nlevel = member.NLevel,
            slevel = member.SLevel,
            sex = member.Sex,
            matchCnt = member.ActiveRecord.MatchCnt,
            winCnt = member.ActiveRecord.WinCnt,
            defeatCnt = member.ActiveRecord.DefeatCnt,
            drawCnt = member.ActiveRecord.DrawCnt,
            roomId = member.RoomId ?? 0,
            location = FormatMemberLocation(member.RoomId),
        }).ToArray();
        var channelRooms = (await _roomRegistry.GetChannelRoomsAsync(channelId))
            .Select(room => new { registry = room, session = _session.GetRoom(room.RoomId) })
            .Where(room => room.session is not { HasNoActiveMembers: true }
                || room.session.State == Models.Game.GameRoomState.Playing)
            .Select(room => room.session is not null
                ? GetRoomListCommand.BuildRoomListEntry(room.session, room.registry.ServerUrl)
                : new Dictionary<string, object?>
                {
                    ["roomId"] = room.registry.RoomId,
                    ["title"] = room.registry.Title,
                    ["isPrivate"] = room.registry.IsPrivate,
                    ["memberCnt"] = room.registry.MemberCnt,
                    ["memberMax"] = room.registry.MemberMax,
                    ["roomOption"] = room.registry.RoomOption,
                    ["serverUrl"] = room.registry.ServerUrl,
                    ["maxViewer"] = room.registry.MaxViewer,
                    ["state"] = room.registry.State > 0 ? room.registry.State : null,
                    ["roomPlaying"] = room.registry.RoomPlaying > 0 ? room.registry.RoomPlaying : null,
                })
            .ToArray();

        await ctx.Caller.SendAsync(Cmd.EnterChannel, new
        {
            result = 1,
            k1e = GKey.ValueSuccess,
            memberNo = player.Pix,
            pix = player.Pix,
            k3e = player.Pix,
            avatarId = player.AvatarId,
            k7e = player.AvatarId,
            sex = player.Sex,
            k11e = player.Sex,
            location = FormatMemberLocation(player.RoomId),
            k12e = FormatMemberLocation(player.RoomId),
            gammoney = player.GamMoney,
            k34e = player.GamMoney,
            gemcount = player.GemCount,
            mjkk55e = player.GemCount,
            cashCount = player.CashCount,
            matchCnt = player.ActiveRecord.MatchCnt,
            k26e = player.ActiveRecord.MatchCnt,
            winCnt = player.ActiveRecord.WinCnt,
            k27e = player.ActiveRecord.WinCnt,
            defeatCnt = player.ActiveRecord.DefeatCnt,
            k28e = player.ActiveRecord.DefeatCnt,
            drawCnt = player.ActiveRecord.DrawCnt,
            k29e = player.ActiveRecord.DrawCnt,
            rating = player.ActiveRecord.Rating,
            k31e = player.ActiveRecord.Rating,
            nlevel = player.NLevel,
            k33e = player.NLevel,
            slevel = player.SLevel,
            k32e = player.SLevel,
            customEquips = player.CustomItems
                .Where(item => item.Value.Equip == 1)
                .Select(item => new { customType = item.Value.Kind, customId = item.Key })
                .ToArray(),
            members = channelMembers,
            rooms = channelRooms,
        });
    }

    private static string First(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? "";

    private async Task<bool> TryAbandonPreviousRoomAsync(
        CommandContext ctx,
        string memberNo,
        string channelId,
        int roomId,
        bool allowActiveMember)
    {
        if (roomId <= 0) return false;

        var room = _session.GetRoom(roomId);
        if (room == null
            || room.ChannelId != channelId
            || (!allowActiveMember && room.State != Models.Game.GameRoomState.Playing))
            return false;

        var continuedPlayer = room.Seats.FirstOrDefault(
            seat => seat?.MemberNo == memberNo && (allowActiveMember || seat.IsOutPlayer));
        var continuedViewer = allowActiveMember
            ? room.Viewers.FirstOrDefault(viewer => viewer.MemberNo == memberNo)
            : null;
        var abandoningMember = continuedPlayer ?? continuedViewer;
        if (abandoningMember == null) return false;

        bool isViewer = continuedViewer != null;
        int seatPos = isViewer ? room.Viewers.IndexOf(continuedViewer!) : Array.IndexOf(room.Seats, continuedPlayer);
        string roomHost = room.Seats
            .Where(seat => seat != null && seat.MemberNo != memberNo && !seat.IsOutPlayer)
            .Select(seat => seat!.MemberNo)
            .FirstOrDefault() ?? "";

        _session.RemovePendingMatchMember(roomId, memberNo);
        if (abandoningMember.RoomId == roomId)
        {
            _session.LeaveRoom(abandoningMember);
        }
        else
        {
            if (isViewer)
                room.RemoveViewer(memberNo);
            else
                room.RemovePlayer(memberNo);
            if (room.IsEmpty) _session.RemoveRoom(roomId);
        }
        abandoningMember.RoomId = null;
        abandoningMember.IsOutPlayer = false;
        await _roomRegistry.ClearContinueRoomAsync(memberNo);

        await ctx.Clients.Group($"room_{roomId}")
            .SendAsync(Cmd.DeleteMember, Commands.Room.RoomGetMembersCommand.BuildDeleteMemberPayload(
                roomHost, abandoningMember, isViewer ? GKey.ValueViewer : GKey.ValuePlayer, seatPos));
        if (!string.IsNullOrWhiteSpace(abandoningMember.ConnectionId))
            await ctx.Groups.RemoveFromGroupAsync(abandoningMember.ConnectionId, $"room_{roomId}");

        var updatedRoom = _session.GetRoom(roomId);
        var roomState = updatedRoom == null
            ? RoomStatePayload.BuildEmpty(roomId, "abandoned")
            : RoomStatePayload.Build(updatedRoom, "abandoned");
        if (updatedRoom == null)
            await _roomRegistry.RemoveRoomAsync(roomId, channelId);
        else
            await _roomRegistry.UpdateMemberCountAsync(roomId, channelId, updatedRoom.ActivePlayerCount);

        await ctx.Clients.Group($"chanel_{channelId}").SendAsync(Cmd.RoomState, roomState);
        _logger.LogInformation(
            "Abandoned room member. channelId={ChannelId} roomId={RoomId} memberNo={MemberNo} allowActiveMember={AllowActiveMember} roomRemoved={RoomRemoved}",
            channelId, roomId, memberNo, allowActiveMember, updatedRoom == null);
        return true;
    }

    private async Task RemoveSameTabPreviousSessionAsync(
        CommandContext ctx,
        MajakPlayer existing,
        bool isContinuePlayer)
    {
        if (!isContinuePlayer && existing.RoomId is int roomId)
        {
            var room = _session.GetRoom(roomId);
            if (room != null)
            {
                int seatPos = (int)existing.SeatPos;
                string roomHost = room.Seats
                    .Where(seat => seat != null && seat.MemberNo != existing.MemberNo)
                    .Select(seat => seat!.MemberNo)
                    .FirstOrDefault() ?? "";

                _session.RemovePendingMatchMember(roomId, existing.MemberNo);
                _session.LeaveRoom(existing);
                await ctx.Clients.Group($"room_{roomId}")
                    .SendAsync(Cmd.DeleteMember, Commands.Room.RoomGetMembersCommand.BuildDeleteMemberPayload(
                        roomHost, existing, existing.IsViewer ? GKey.ValueViewer : GKey.ValuePlayer, seatPos));

                if (room.State == Models.Game.GameRoomState.Waiting
                    && seatPos >= 0 && seatPos < GameConst.PlayerMaxCount)
                {
                    room.OkButtonStates[seatPos] = false;
                    var okPayload = new Dictionary<string, object>();
                    for (int index = 0; index < GameConst.PlayerMaxCount; index++)
                        okPayload[$"{Key.OkButton}{index}"] = room.OkButtonStates[index] ? 1 : 0;
                    await ctx.Clients.Group($"room_{roomId}").SendAsync(Cmd.SendOkButton, okPayload);
                }

                await ctx.Groups.RemoveFromGroupAsync(existing.ConnectionId, $"room_{roomId}");
                var updatedRoom = _session.GetRoom(roomId);
                if (updatedRoom == null)
                {
                    _session.ExpirePendingMatch(roomId);
                    await _roomRegistry.RemoveRoomAsync(roomId, existing.ChannelId);
                    await ctx.Clients.Group($"chanel_{existing.ChannelId}")
                        .SendAsync(Cmd.RoomState, RoomStatePayload.BuildEmpty(roomId, "left"));
                }
                else
                {
                    await _roomRegistry.UpdateMemberCountAsync(roomId, existing.ChannelId, updatedRoom.ActivePlayerCount);
                    await ctx.Clients.Group($"chanel_{existing.ChannelId}")
                        .SendAsync(Cmd.RoomState, RoomStatePayload.Build(updatedRoom, "left"));
                }
            }
        }

        if (!string.IsNullOrEmpty(existing.ChannelId))
        {
            await ctx.Groups.RemoveFromGroupAsync(existing.ConnectionId, $"chanel_{existing.ChannelId}");
            await ctx.Clients.Group($"chanel_{existing.ChannelId}").SendAsync(Cmd.DeleteMember, new
            {
                memberNo = existing.Pix,
                pix = existing.Pix,
                k3e = existing.Pix,
            });
            await _channelMemberSvc.LeaveAsync(existing.ChannelId, existing.MemberNo);
        }
        _session.Remove(existing.ConnectionId);
    }

    private object FailPayload(string error, string message, string channelId, string memberNo)
    {
        _logger.LogWarning(
            "EnterChannel failed. error={Error} message={Message} channelId={ChannelId} subId={SubId} memberNo={MemberNo}",
            error, message, channelId, ExtractSubId(channelId), memberNo);

        return new
        {
            result = 0,
            k1e    = GKey.ValueFailure,
            error,
            message,
            k2e    = message,
        };
    }

    private async Task<bool> ShouldRejectStoppedCupEnterAsync(string channelId, string memberNo, string subId)
        => subId.Length > 2
           && subId[2] == 'C'
           && await _playerRepo.GetCupStatusAsync(channelId) == 2
           && !_session.IsContinuePlayerInChannel(channelId, memberNo);

    public static int? GetFestiveCupRatId(string subId, CupConfig? cup)
        => cup != null && subId.Length > 4 && subId[2] == 'C' && subId[4] == 'A'
            ? cup.CupId
            : null;

    private static bool ShouldRejectCupEntry(Infrastructure.CupConfig? cup, MajakPlayer player)
        => cup is { EntryLimited: true }
           && cup.ConditionBilling != EvtBillingFree
           && player.CupEvtRec.EntryTitle == 0;

    private static bool ShouldRejectCupMinLevel(Infrastructure.CupConfig? cup, MajakPlayer player)
        => cup is { MinLevel: > 0 } && player.NLevel < cup.MinLevel;

    private static bool ShouldRejectCupMaxLevel(Infrastructure.CupConfig? cup, MajakPlayer player)
        => cup is { MaxLevel: > 0 } && player.NLevel > cup.MaxLevel;

    private static string ExtractSubId(string channelId)
        => channelId.Length >= 11 ? channelId[6..11] : channelId;

    private static bool IsHiEventSubId(string subId)
        => subId.Length > 4 && subId[2] == 'C' && subId[4] >= 'F' && subId[4] != 'Z';

    private static string FormatMemberLocation(int? roomId)
        => roomId is > 0 ? $"{roomId.Value}番部屋" : "ロビー";

    private static long ToEpochSeconds(DateTime dateTime)
        => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Local)).ToUnixTimeSeconds();

    private static string BuildPresentMessage(int kbn, long num, string titleId) => kbn switch
    {
        1 => $"マネー獲得\tおめでとうございます！\nトーナメント大会開催によりマネー{num}円を獲得しました。",
        2 => $"マネー獲得\tおめでとうございます！\nトーナメント大会入賞によりマネー{num}円を獲得しました。",
        3 => $"大会中止のお知らせ\t登録したトーナメント大会は参加人数不足により開催できませんでした。\nマネー{num}円を返却いたします。",
        4 => $"大会中止のお知らせ\t参加登録したトーナメント大会は参加人数不足により開催できませんでした。\nマネー{num}円を返却いたします。",
        5 => $"大会中止のお知らせ\t登録したトーナメント大会はメンテナンスにより開催できませんでした。\nマネー{num}円を返却いたします。",
        6 => $"大会中止のお知らせ\t参加登録したトーナメント大会はメンテナンスにより開催できませんでした。\nマネー{num}円を返却いたします。",
        7 => $"麻雀称号獲得\tおめでとうございます！\nトーナメント大会の開催数が特定条件を満たしたため、麻雀称号【{titleId}】を獲得しました。",
        8 => $"おしらせ\t対局中に問題があったため、参加していたトーナメント大会から棄権しました。大会参加費としてマネー{num}円をお返しさせていただきます。",
        _ => $"おしらせ\t{num}",
    };
}
