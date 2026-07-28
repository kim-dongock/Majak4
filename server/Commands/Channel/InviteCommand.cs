using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Commands.Channel;

/// <summary>
/// room:invite  ゲーム招征EC→S ハンドラ
///


///
/// リクエストフィールチE




///   k64e           : キャンセルフラグ
///
/// 応筁E(S→C to target):
///   イベント名 : "InviteGame"  (G::commandInviteGameToMember)


///   k67e       : ルームパスワーチE

///   k64e       : キャンセルフラグ (false)
///
/// </summary>
public class InviteCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public InviteCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null || player.RoomId == null) return;

        string targetMemberNo = _session.ResolveMemberNo(ctx.GetString("targetMemberNo"));
        int requestRoomId = ctx.GetInt(GKey.RoomId);
        if (requestRoomId == 0) requestRoomId = ctx.GetInt("roomId");
        if (requestRoomId != 0 && requestRoomId != player.RoomId.Value) return;

        string inviteMessage  = ctx.GetString(GKey.InviteGameString);
        if (string.IsNullOrEmpty(inviteMessage)) inviteMessage = ctx.GetString("inviteMessage");
        if (string.IsNullOrEmpty(inviteMessage)) inviteMessage = "一緒に対戦しませんか！";
        bool cancel = ctx.GetBool(GKey.YesNo);

        if (string.IsNullOrEmpty(targetMemberNo)) return;



        var target = _session.GetByMember(targetMemberNo);
        if (target == null) return;
        if (target.ChannelId != player.ChannelId) return;
        if (target.RejectInvite) return;

        var room = _session.GetRoom(player.RoomId.Value);
        string roomPwd = room?.Password ?? "";



        var packet = new Dictionary<string, object?>
        {
            [GKey.Pix] = player.Pix,
            [GKey.RoomId] = player.RoomId.Value,
            [GKey.RoomPwd] = roomPwd,
            [GKey.InviteGameString] = inviteMessage,
            [GKey.YesNo] = cancel,
            ["inviterId"] = player.Pix,
            ["pix"] = player.Pix,
            ["roomId"] = player.RoomId.Value,
            ["roomPwd"] = roomPwd,
            ["inviteMessage"] = inviteMessage,
        };

        if (room != null)
        {
            packet[GKey.RoomTitle] = room.RoomTitle;
            packet[GKey.RoomOption] = room.RoomOption;
            packet["roomName"] = room.RoomTitle;
            packet["roomOption"] = room.RoomOption;
        }

        await ctx.Clients.Client(target.ConnectionId).SendAsync(Cmd.InviteGame, packet);
    }
}
