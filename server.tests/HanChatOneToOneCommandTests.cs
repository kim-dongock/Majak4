using MajakServer.Commands.Channel;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Services;
using System.Text.Json;

namespace MajakServer.Tests;

public sealed class HanChatOneToOneCommandTests
{
    private static MajakPlayer Player(string memberNo, string pix, string connectionId, string channelId) => new()
    {
        MemberNo = memberNo,
        Pix = pix,
        ConnectionId = connectionId,
        ChannelId = channelId,
        NickName = memberNo,
    };

    [Fact]
    public async Task Open_ValidSameChannelTarget_RelaysLegacyOpenPacket()
    {
        var session = new PlayerSessionService();
        var sender = Player("sender-id", "sender-pix", "sender-connection", "channel-1");
        var recipient = Player("recipient-id", "recipient-pix", "recipient-connection", "channel-1");
        session.Register(sender);
        session.Register(recipient);
        var (context, sent) = CommandTestHelper.MakeContext(sender, new() { ["target"] = recipient.Pix });

        await new HanChatOneToOneCommand(session).ExecuteAsync(context);

        var packet = CommandTestHelper.ToDict(Assert.Single(sent).packet);
        Assert.Equal(Cmd.HanChatOneToOne, sent[0].method);
        Assert.Equal(1, ((JsonElement)packet["result"]!).GetInt32());
        Assert.Equal(sender.Pix, ((JsonElement)packet["sender"]!).GetString());
        Assert.Equal(recipient.Pix, ((JsonElement)packet["target"]!).GetString());
    }

    [Fact]
    public async Task String_ValidSameChannelTarget_RelaysOnlyNonEmptyMessage()
    {
        var session = new PlayerSessionService();
        var sender = Player("sender-id", "sender-pix", "sender-connection", "channel-1");
        var recipient = Player("recipient-id", "recipient-pix", "recipient-connection", "channel-1");
        var outsider = Player("outsider-id", "outsider-pix", "outsider-connection", "channel-1");
        session.Register(sender);
        session.Register(recipient);
        session.Register(outsider);
        IReadOnlyList<string>? recipients = null;
        var (context, sent) = CommandTestHelper.MakeContext(sender, new()
        {
            ["target"] = recipient.Pix,
            [GKey.String] = "hello",
        }, onClients: connectionIds => recipients = connectionIds);

        await new HanChatOneToOneStringCommand(session).ExecuteAsync(context);

        var packet = CommandTestHelper.ToDict(Assert.Single(sent).packet);
        Assert.Equal(Cmd.HanChatOneToOneString, sent[0].method);
        Assert.Equal("hello", ((JsonElement)packet[GKey.String]!).GetString());
        Assert.NotNull(recipients);
        Assert.Equal(
            new[] { sender.ConnectionId, recipient.ConnectionId }.OrderBy(connectionId => connectionId),
            recipients.OrderBy(connectionId => connectionId));
        Assert.DoesNotContain(outsider.ConnectionId, recipients);
    }

    [Fact]
    public async Task Reject_ValidSameChannelTarget_RelaysLegacyRejectPacket()
    {
        var session = new PlayerSessionService();
        var rejector = Player("rejector-id", "rejector-pix", "rejector-connection", "channel-1");
        var requester = Player("requester-id", "requester-pix", "requester-connection", "channel-1");
        session.Register(rejector);
        session.Register(requester);
        IReadOnlyList<string>? recipients = null;
        var (context, sent) = CommandTestHelper.MakeContext(rejector, new()
        {
            ["target"] = requester.Pix,
            ["rejectType"] = 2,
        }, onClients: connectionIds => recipients = connectionIds);

        await new HanChatRejectCommand(session).ExecuteAsync(context);

        var packet = CommandTestHelper.ToDict(Assert.Single(sent).packet);
        Assert.Equal(Cmd.HanChatReject, sent[0].method);
        Assert.Equal(2, ((JsonElement)packet["rejectType"]!).GetInt32());
        Assert.Equal(rejector.Pix, ((JsonElement)packet["sender"]!).GetString());
        Assert.Equal(requester.Pix, ((JsonElement)packet["target"]!).GetString());
        Assert.NotNull(recipients);
        Assert.Equal(
            new[] { rejector.ConnectionId, requester.ConnectionId }.OrderBy(connectionId => connectionId),
            recipients.OrderBy(connectionId => connectionId));
    }

    [Fact]
    public async Task End_DifferentChannelTarget_DoesNotRelay()
    {
        var session = new PlayerSessionService();
        var sender = Player("sender-id", "sender-pix", "sender-connection", "channel-1");
        var recipient = Player("recipient-id", "recipient-pix", "recipient-connection", "channel-2");
        session.Register(sender);
        session.Register(recipient);
        var (context, sent) = CommandTestHelper.MakeContext(sender, new() { ["target"] = recipient.Pix });

        await new HanChatOneToOneEndCommand(session).ExecuteAsync(context);

        Assert.Empty(sent);
    }
}