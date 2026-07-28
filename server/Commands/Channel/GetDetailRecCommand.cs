using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using Microsoft.AspNetCore.SignalR;

namespace MajakServer.Commands.Channel;

/// <summary>


///

///   - memberNo / avatarId
///   - regular   : MJKHANGERAT    (m_stR_Record)
///   - hiClass   : MJK_HICLASSRAT (m_stH_Record)
///   - gradeMode : MJK_GRADERAT   (m_stG_Record) + gradeLevel/gradePoint/gradeMaxPoint

///

/// </summary>
public class GetDetailRecCommand : ICommand
{
    private readonly PlayerSessionService _session;
    private readonly TitleService         _titleService;

    public GetDetailRecCommand(PlayerSessionService session, TitleService titleService)
    {
        _session      = session;
        _titleService = titleService;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var me = ctx.Player;
        if (me == null) return;



        string targetId = First(ctx.GetString("pix"), ctx.GetString("memberNo"), ctx.GetString(GKey.Pix));
        if (string.IsNullOrEmpty(targetId)) return;
        string targetMemberNo = _session.ResolveMemberNo(targetId);

        MajakPlayer? target = _session.GetAllChannelPlayers(me.ChannelId)
            .FirstOrDefault(p => p.MemberNo == targetMemberNo);

        if (target == null) return;


        string trickTitleName = _titleService.GetTitleName(0, target.TrickTitleId);
        string majakTitleName = _titleService.GetTitleName(1, target.MajakTitleId);

        var resp = new
        {
            result   = 1,
            memberNo = target.Pix,
            pix      = target.Pix,
            avatarId = target.AvatarId,


            regular  = BuildRecord(target.RegularRecord),


            hiClass  = BuildRecord(target.HiClassRecord),


            gradeMode      = BuildRecord(target.GradeRecord),
            gradeLevel     = target.GradeRecord.Grade,
            gradePoint     = target.GradeRecord.GradePoint,
            gradeMaxPoint  = GradeLevelTable.GetMaxPoint(target.GradeRecord.Grade),


            trickTitle     = target.TrickTitleId,
            majakTitle     = target.MajakTitleId,
            trickTitleName,
            majakTitleName,
        };
        await ctx.Caller.SendAsync(Cmd.GetDetailRec, resp);
    }

    private static string First(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";


    private static object BuildRecord(RatingRecord r) => new
    {
        rating      = r.Rating,
        matchCnt    = r.MatchCnt,
        winCnt      = r.WinCnt,
        defeatCnt   = r.DefeatCnt,
        drawCnt     = r.DrawCnt,
        grade1      = r.Grade1,
        grade2      = r.Grade2,
        grade3      = r.Grade3,
        grade4      = r.Grade4,
        turnCnt     = r.TurnCnt,
        daidaCnt    = r.DaidaCnt,
        pointSum    = r.PointSum,
        kyokuCnt    = r.KyokuCnt,
        horaCnt     = r.HoraCnt,
        horaPoint   = r.HoraPoint,
        hojuCnt     = r.HojuCnt,
        hojuPoint   = r.HojuPoint,
        richiCnt    = r.RichiCnt,
        furoCnt     = r.FuroCnt,
        tipPoint    = r.TipPoint,
        tipMatchCnt = r.TipMatchCnt,
        tobiCnt     = r.TobiCnt,
        tobashiCnt  = r.TobashiCnt,
        doraCnt     = r.DoraCnt,
        uraDoraCnt  = r.UraDoraCnt,
        richiHoraCnt = r.RichiHoraCnt,
        grade       = r.Grade,
        gradePoint  = r.GradePoint,
        totExtraCount = r.TotExtraCount,
        disconnCnt  = r.DisconnCnt,
    };
}

