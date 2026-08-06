using Microsoft.AspNetCore.SignalR;
using MajakServer.Commands;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Commands.Channel;

/// <summary>
/// Legacy HANCHAT one-to-one flow: hc6e opens the chat, hc7e relays text,
/// and hc8e closes it. Packets are delivered only to the two participants.
/// </summary>
public abstract class HanChatOneToOneCommandBase : ICommand
{
    private readonly PlayerSessionService _session;

    protected HanChatOneToOneCommandBase(PlayerSessionService session) => _session = session;

    public abstract Task ExecuteAsync(CommandContext ctx);

    protected bool TryGetParticipants(CommandContext ctx, out MajakPlayer sender, out MajakPlayer recipient)
    {
        if (ctx.Player is not { } currentPlayer)
        {
            sender = null!;
            recipient = null!;
            return false;
        }

        sender = currentPlayer;
        recipient = null!;

        var target = ctx.GetString(GKey.Target);
        if (string.IsNullOrWhiteSpace(target)) target = ctx.GetString("target");
        if (string.IsNullOrWhiteSpace(target) || target == currentPlayer.Pix) return false;

        var foundRecipient = _session.GetByMember(target)
            ?? _session.GetAllChannelPlayers(currentPlayer.ChannelId)
                .FirstOrDefault(player => player.Pix == target);
        if (foundRecipient == null || foundRecipient.ChannelId != currentPlayer.ChannelId) return false;

        recipient = foundRecipient;
        return true;
    }

    protected static Dictionary<string, object?> BuildPacket(MajakPlayer sender, MajakPlayer recipient, string message = "") => new()
    {
        ["result"] = 1,
        ["sendMember"] = BuildMember(sender),
        ["receiveMember"] = BuildMember(recipient),
        ["sender"] = sender.Pix,
        ["target"] = recipient.Pix,
        ["string"] = message,
        [GKey.String] = message,
    };

    protected static object BuildMember(MajakPlayer player) => new
    {
        pix = player.Pix,
        memberNo = player.Pix,
        name = player.NickName,
        avatarId = player.AvatarId,
        sex = player.Sex,
    };

    protected static Task SendToParticipants(CommandContext ctx, string command, MajakPlayer sender, MajakPlayer recipient, Dictionary<string, object?> packet)
        => ctx.Clients.Clients(new[] { sender.ConnectionId, recipient.ConnectionId }
            .Where(connectionId => !string.IsNullOrWhiteSpace(connectionId))
            .Distinct(StringComparer.Ordinal)
            .ToArray())
            .SendAsync(command, packet);
}

public sealed class HanChatOneToOneCommand : HanChatOneToOneCommandBase
{
    public HanChatOneToOneCommand(PlayerSessionService session) : base(session) { }

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        if (!TryGetParticipants(ctx, out var sender, out var recipient)) return;
        await SendToParticipants(ctx, Cmd.HanChatOneToOne, sender, recipient, BuildPacket(sender, recipient));
    }
}

public sealed class HanChatOneToOneStringCommand : HanChatOneToOneCommandBase
{
    public HanChatOneToOneStringCommand(PlayerSessionService session) : base(session) { }

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        if (!TryGetParticipants(ctx, out var sender, out var recipient)) return;
        var message = ctx.GetString(GKey.String);
        if (string.IsNullOrWhiteSpace(message)) message = ctx.GetString("string");
        if (string.IsNullOrWhiteSpace(message)) return;
        await SendToParticipants(ctx, Cmd.HanChatOneToOneString, sender, recipient, BuildPacket(sender, recipient, message));
    }
}

public sealed class HanChatOneToOneEndCommand : HanChatOneToOneCommandBase
{
    public HanChatOneToOneEndCommand(PlayerSessionService session) : base(session) { }

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        if (!TryGetParticipants(ctx, out var sender, out var recipient)) return;
        await SendToParticipants(ctx, Cmd.HanChatOneToOneEnd, sender, recipient, BuildPacket(sender, recipient));
    }
}