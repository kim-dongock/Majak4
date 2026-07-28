using MajakServer.Commands;
using MajakServer.Models.Protocol;

namespace MajakServer.Commands.Channel;

/// <summary>
/// c63e — チャット通報 C→S ハンドラ
/// 原典:
///   client/legacy/client/HgChnlM/HgChannelWnd.cpp::OnMsgChatNoticeAccepted
///   client/legacy/client/HgGmM/HgGameWnd.cpp::OnMsgChatNoticeAccepted
/// フィールド:
///   k22e gameId, k3e memberNo, k4e opMemberNo, k24e channelId, k42e roomId,
///   k81e reportingType, k41e reason text, k63e chat description
/// </summary>
public class ComplaintCommand : ICommand
{
    private readonly ILogger<ComplaintCommand> _log;

    public ComplaintCommand(ILogger<ComplaintCommand> log) => _log = log;

    public Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        string memberNo = player?.MemberNo ?? ctx.GetString(GKey.Pix);
        string targetMemberNo = ctx.GetString(GKey.OpPix);
        string channelId = First(ctx.GetString(GKey.ChannelId), player?.ChannelId ?? "");
        int roomId = ctx.GetInt(GKey.RoomId, player?.RoomId ?? 0);
        int reportingType = ctx.GetInt(GKey.ReportingType);
        string reason = ctx.GetString(GKey.String);
        string description = ctx.GetString(GKey.Description);

        _log.LogInformation(
            "Complaint received. memberNo={MemberNo} targetMemberNo={TargetMemberNo} channelId={ChannelId} roomId={RoomId} reportingType={ReportingType} reason={Reason} descriptionLength={DescriptionLength}",
            memberNo,
            targetMemberNo,
            channelId,
            roomId,
            reportingType,
            reason,
            description.Length);

        return Task.CompletedTask;
    }

    private static string First(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? "";
}