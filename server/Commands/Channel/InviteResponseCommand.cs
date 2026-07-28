using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Commands.Channel;

/// <summary>


///   keyMemberNo = 応答老EmemberNo, keyYesNo = valueYes/valueNo/valueDummy

/// </summary>
public class InviteResponseCommand : ICommand
{
    private readonly PlayerSessionService _session;

    public InviteResponseCommand(PlayerSessionService session) => _session = session;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        string inviterId = _session.ResolveMemberNo(ctx.GetString("inviterId"));
        string yesNo = ctx.GetString(GKey.YesNo);
        if (string.IsNullOrEmpty(yesNo))
        {
            string accept = ctx.GetString("accept");
            yesNo = accept == "1" || accept.Equals("true", StringComparison.OrdinalIgnoreCase)
                ? GKey.ValueYes
                : GKey.ValueNo;
        }
        if (string.IsNullOrEmpty(inviterId)) return;

        var inviter = _session.GetByMember(inviterId);
        if (inviter == null) return;
        if (inviter.ChannelId != player.ChannelId) return;

        await ctx.Clients.Client(inviter.ConnectionId).SendAsync(Cmd.InviteResponse, new Dictionary<string, object?>
        {
            [GKey.Pix] = player.Pix,
            [GKey.YesNo] = yesNo,
            ["memberNo"] = player.Pix,
            ["pix"] = player.Pix,
            ["accept"] = yesNo == GKey.ValueYes ? "1" : "0",
        });
    }
}
