using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using Microsoft.Extensions.Options;

namespace MajakServer.Endpoints;

internal static class ChannelEndpoints
{
    internal static void MapChannelEndpoints(this WebApplication app)
    {
        app.MapGet("/api/channel/{chanelId}/server",
            async (string chanelId, ChannelRepository channels, ServerLoadService load, IOptions<ChannelServerSettings> settings) =>
            {
                var channel = await channels.GetChannelAsync(chanelId);
                if (channel is null) return Results.NotFound(new { error = "CHANNEL_NOT_FOUND" });
                if (!channel.IsActive)
                    return Results.Json(new { error = "CHANNEL_UNAVAILABLE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
                var serverUrl = settings.Value.ResolveAssignedUrl(channel.ServerUrl);
                if (!await load.IsServerActiveAsync(serverUrl))
                    return Results.Json(new { error = "CHANNEL_SERVER_INACTIVE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
                return Results.Ok(new { serverUrl });
            });

        app.MapPost("/api/channel/{chanelId}/enter",
            async (HttpContext ctx, string chanelId, ChannelEnterRequest request, ChannelMemberService service, PlayerSessionService sessions, GameAuthTokenService gameAuth) =>
            {
                var requestPix = request.Pix ?? request.MemberNo;
                if (string.IsNullOrWhiteSpace(requestPix)) return Results.BadRequest();
                var auth = gameAuth.Validate(ctx.Request.Headers.Authorization.FirstOrDefault());
                if (auth is null) return Results.Unauthorized();
                if (requestPix != auth.MemberNo && requestPix != auth.Pix
                    && sessions.ResolveMemberNo(requestPix) != auth.MemberNo)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                await service.EnterAsync(chanelId, auth.MemberNo, auth.Pix, request.Nickname ?? "",
                    request.Rating, request.Sex ?? "male", request.AvatarId ?? "");
                return Results.Ok();
            });

        app.MapPost("/api/channel/{chanelId}/leave",
            async (HttpContext ctx, string chanelId, ChannelLeaveRequest request, ChannelMemberService service, PlayerSessionService sessions, GameAuthTokenService gameAuth) =>
            {
                var requestPix = request.Pix ?? request.MemberNo;
                if (string.IsNullOrWhiteSpace(requestPix)) return Results.BadRequest();
                var auth = gameAuth.Validate(ctx.Request.Headers.Authorization.FirstOrDefault());
                if (auth is null) return Results.Unauthorized();
                if (requestPix != auth.MemberNo && requestPix != auth.Pix
                    && sessions.ResolveMemberNo(requestPix) != auth.MemberNo)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                await service.LeaveAsync(chanelId, auth.MemberNo);
                return Results.Ok();
            });

        app.MapGet("/api/channel/{chanelId}/members",
            async (string chanelId, ChannelMemberService service) =>
            {
                var members = await service.GetMembersAsync(chanelId);
                return Results.Ok(members.Select(member => new
                {
                    pix = member.Pix,
                    nickname = member.Nickname,
                    rating = member.Rating,
                    sex = member.Sex,
                    avatarId = member.AvatarId,
                }));
            });

        app.MapGet("/api/channel/{chanelId}/rooms",
            async (string chanelId, RoomRegistryService roomRegistry) =>
            {
                var rooms = await roomRegistry.GetChannelRoomsAsync(chanelId);
                return Results.Ok(rooms.Select(room => new
                {
                    roomId = room.RoomId,
                    title = room.Title,
                    isPrivate = room.IsPrivate,
                    memberCnt = room.MemberCnt,
                    memberMax = room.MemberMax,
                    maxViewer = room.MaxViewer,
                    roomOption = room.RoomOption,
                    serverUrl = room.ServerUrl,
                }));
            });

        app.MapGet("/api/channels", async (MasterCacheService masterCache, ChannelMemberService members, RoomRegistryService roomRegistry) =>
        {
            var channels = await masterCache.GetChannelListAsync("MAJAK4");
            var result = new List<object>();
            foreach (var channel in channels)
            {
                var currentMembers = await members.GetMembersAsync(channel.SubId);
                var currentRooms = await roomRegistry.GetChannelRoomsAsync(channel.SubId);
                result.Add(new
                {
                    chanelId = channel.ChanelId,
                    subId = channel.SubId,
                    chanelName = channel.ChanelName,
                    maxMember = channel.MaxMember,
                    maxRoom = channel.MaxRoom,
                    chanelType = channel.ChanelType,
                    unitMoney = channel.UnitMoney,
                    memberCnt = currentMembers.Count,
                    usedRoom = currentRooms.Count,
                });
            }
            return Results.Ok(result);
        });
    }
}