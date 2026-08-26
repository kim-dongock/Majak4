using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Endpoints;

internal static class AnnouncementEndpoints
{
    internal static void MapAnnouncementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/announcements/startup", async (
            HttpContext context,
            GameAuthTokenService gameAuth,
            GameAnnouncementRepository announcements) =>
        {
            if (gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault()) is null)
                return Results.Unauthorized();
            var announcement = await announcements.GetStartupAsync();
            return announcement is null ? Results.NoContent() : Results.Ok(announcement);
        });

        app.MapGet("/api/announcements", async (
            HttpContext context,
            GameAuthTokenService gameAuth,
            GameAnnouncementRepository announcements,
            int offset = 0,
            int limit = 20) =>
        {
            if (gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault()) is null)
                return Results.Unauthorized();
            if (offset < 0) offset = 0;
            limit = Math.Clamp(limit, 1, 50);
            var items = await announcements.GetPublishedAsync(offset, limit);
            return Results.Ok(new { items, hasMore = items.Count == limit });
        });
    }
}