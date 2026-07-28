using System.Globalization;
using MajakServer.Models.Game;
using MajakServer.Models.Player;

namespace MajakServer.Models.Protocol;

/// <summary>
/// mjkroom / G::commandRoomInfo payload builder.

/// </summary>
public static class RoomStatePayload
{
    public const int LegacyRoomEmpty = 0;
    public const int LegacyRoomJoin = 1;
    public const int LegacyRoomJoinReady = 2;
    public const int LegacyRoomReady = 3;
    public const int LegacyRoomGameJoin = 4;
    public const int LegacyRoomGameView = 5;
    public const int LegacyRoomGameFull = 6;

    public static Dictionary<string, object?> Build(GameRoom room, string action = "changed")
    {
        var activeSeats = room.Seats
            .Select((member, seat) => new { member, seat })
            .Where(x => x.member != null && !x.member.IsOutPlayer)
            .Select(x => (member: x.member!, seat: x.seat))
            .ToList();
        var continueSeats = room.Seats
            .Select((member, seat) => new { member, seat })
            .Where(x => x.member?.IsOutPlayer == true)
            .Select(x => (member: x.member!, seat: x.seat))
            .ToList();

        string roomCreator = activeSeats.FirstOrDefault(x => x.member.MemberNo == room.CreatorNo).member?.Pix
            ?? continueSeats.FirstOrDefault(x => x.member.MemberNo == room.CreatorNo).member?.Pix
            ?? "";
        string roomHost = activeSeats.Count > 0 ? activeSeats[0].member.Pix : "";
        string roomInfo = BuildRoomInfo(room, activeSeats, continueSeats, roomHost, roomCreator);

        var packet = new Dictionary<string, object?>
        {
            ["action"] = action,
            ["roomId"] = room.RoomId,
            [GKey.RoomId] = room.RoomId,
            ["roomTitle"] = room.RoomTitle,
            [GKey.RoomTitle] = room.RoomTitle,
            ["privateYn"] = room.IsPrivate ? "Y" : "N",
            [GKey.PrivateYn] = room.IsPrivate ? "Y" : "N",
            ["isPrivate"] = room.IsPrivate,
            ["roomOption"] = room.RoomOption,
            [GKey.RoomOption] = room.RoomOption,
            ["roomState"] = GetLegacyRoomState(room),
            [GKey.RoomStateKey] = GetLegacyRoomState(room),
            ["roomCreator"] = roomCreator,
            [GKey.RoomCreator] = roomCreator,
            ["roomHost"] = roomHost,
            [GKey.RoomHost] = roomHost,
            ["roomPlaying"] = GetLegacyPlayState(room),
            [GKey.RoomPlaying] = GetLegacyPlayState(room),
            ["memberCnt"] = activeSeats.Count,
            [GKey.MemberCnt] = activeSeats.Count,
            ["viewerCnt"] = room.ViewerCount,
            [GKey.ViewerCnt] = room.ViewerCount,
            ["opMemberCnt"] = continueSeats.Count,
            [GKey.OpMemberCnt] = continueSeats.Count,
            ["roomLimitCnt"] = room.LimitCnt,
            [GKey.RoomLimitCnt] = room.LimitCnt,
            ["maxViewer"] = room.MaxViewer,
            [GKey.MaxViewer] = room.MaxViewer,
            ["roomInfo"] = roomInfo,
            [$"{GKey.RoomId}{room.RoomId}"] = roomInfo,
        };

        AddIndexedMembers(packet, activeSeats, continueSeats, room);
        return packet;
    }

    public static Dictionary<string, object?> BuildEmpty(int roomId, string action = "empty")
    {
        var values = new Dictionary<string, object?>
        {
            [GKey.RoomId] = roomId,
            [GKey.RoomTitle] = "",
            [GKey.PrivateYn] = "N",
            [GKey.RoomOption] = "",
            [GKey.RoomStateKey] = 0,
            [GKey.RoomCreator] = "",
            [GKey.RoomHost] = "",
            [GKey.RoomPlaying] = 0,
            [GKey.MemberCnt] = 0,
            [GKey.ViewerCnt] = 0,
            [GKey.OpMemberCnt] = 0,
        };
        string roomInfo = ToUrl(values);

        return new Dictionary<string, object?>
        {
            ["action"] = action,
            ["roomId"] = roomId,
            [GKey.RoomId] = roomId,
            ["roomTitle"] = "",
            [GKey.RoomTitle] = "",
            ["privateYn"] = "N",
            [GKey.PrivateYn] = "N",
            ["isPrivate"] = false,
            ["roomOption"] = "",
            [GKey.RoomOption] = "",
            ["roomState"] = 0,
            [GKey.RoomStateKey] = 0,
            ["roomCreator"] = "",
            [GKey.RoomCreator] = "",
            ["roomHost"] = "",
            [GKey.RoomHost] = "",
            ["roomPlaying"] = 0,
            [GKey.RoomPlaying] = 0,
            ["memberCnt"] = 0,
            [GKey.MemberCnt] = 0,
            ["viewerCnt"] = 0,
            [GKey.ViewerCnt] = 0,
            ["opMemberCnt"] = 0,
            [GKey.OpMemberCnt] = 0,
            ["roomInfo"] = roomInfo,
            [$"{GKey.RoomId}{roomId}"] = roomInfo,
        };
    }

    private static string BuildRoomInfo(
        GameRoom room,
        IReadOnlyList<(MajakPlayer member, int seat)> activeSeats,
        IReadOnlyList<(MajakPlayer member, int seat)> continueSeats,
        string roomHost,
        string roomCreator)
    {
        var values = new Dictionary<string, object?>
        {
            [GKey.RoomId] = room.RoomId,
            [GKey.RoomTitle] = room.RoomTitle,
            [GKey.PrivateYn] = room.IsPrivate ? "Y" : "N",
            [GKey.RoomOption] = room.RoomOption,
            [GKey.RoomStateKey] = GetLegacyRoomState(room),
            [GKey.RoomCreator] = roomCreator,
            [GKey.RoomHost] = roomHost,
            [GKey.RoomPlaying] = GetLegacyPlayState(room),
            [GKey.MemberCnt] = activeSeats.Count,
            [GKey.ViewerCnt] = room.ViewerCount,
            [GKey.OpMemberCnt] = continueSeats.Count,
            [GKey.RoomLimitCnt] = room.LimitCnt,
            [GKey.MaxViewer] = room.MaxViewer,
        };

        for (int i = 0; i < activeSeats.Count; i++)
        {
            values[$"{GKey.Pix}{i}"] = activeSeats[i].member.Pix;
            values[$"{GKey.MemberPos}{i}"] = activeSeats[i].seat;
        }

        for (int i = 0; i < continueSeats.Count; i++)
        {
            values[$"{GKey.OpPix}{i}"] = continueSeats[i].member.Pix;
            values[$"{GKey.OpMemberPos}{i}"] = continueSeats[i].seat;
        }

        for (int i = 0; i < room.Viewers.Count; i++)
        {
            values[$"{GKey.ViewerId}{i}"] = room.Viewers[i].Pix;
            values[$"{GKey.ViewerPos}{i}"] = i;
        }

        return ToUrl(values);
    }

    private static void AddIndexedMembers(
        Dictionary<string, object?> packet,
        IReadOnlyList<(MajakPlayer member, int seat)> activeSeats,
        IReadOnlyList<(MajakPlayer member, int seat)> continueSeats,
        GameRoom room)
    {
        for (int i = 0; i < activeSeats.Count; i++)
        {
            packet[$"{GKey.Pix}{i}"] = activeSeats[i].member.Pix;
            packet[$"{GKey.MemberPos}{i}"] = activeSeats[i].seat;
        }

        for (int i = 0; i < continueSeats.Count; i++)
        {
            packet[$"{GKey.OpPix}{i}"] = continueSeats[i].member.Pix;
            packet[$"{GKey.OpMemberPos}{i}"] = continueSeats[i].seat;
        }

        for (int i = 0; i < room.Viewers.Count; i++)
        {
            packet[$"{GKey.ViewerId}{i}"] = room.Viewers[i].Pix;
            packet[$"{GKey.ViewerPos}{i}"] = i;
        }
    }

    private static string ToUrl(Dictionary<string, object?> values)
        => string.Join('&', values.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? string.Empty)}"));

    public static int GetLegacyRoomState(GameRoom room)
    {
        int activeMemberCnt = room.Seats.Count(seat => seat != null && !seat.IsOutPlayer);
        int continueMemberCnt = room.Seats.Count(seat => seat?.IsOutPlayer == true);
        if (activeMemberCnt <= 0 && continueMemberCnt <= 0 && room.ViewerCount <= 0)
            return LegacyRoomEmpty;

        if (room.State == GameRoomState.Playing)
            return room.MaxViewer > 0 && room.ViewerCount >= room.MaxViewer
                ? LegacyRoomGameFull
                : LegacyRoomGameView;

        int limitCnt = room.LimitCnt > 0 ? room.LimitCnt : room.Seats.Length;
        if (activeMemberCnt >= limitCnt)
            return LegacyRoomReady;

        return LegacyRoomJoin;
    }

    private static int GetLegacyPlayState(GameRoom room)
        => room.State == GameRoomState.Playing ? 1 : 0;
}
