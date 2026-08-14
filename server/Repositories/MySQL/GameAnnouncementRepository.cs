using MySqlConnector;

namespace MajakServer.Repositories.MySQL;

public sealed record GameAnnouncement(
    long AnnouncementId,
    string Title,
    string Body,
    bool IsPublished,
    bool IsStartup,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class GameAnnouncementRepository
{
    private readonly GameDbContext _db;

    public GameAnnouncementRepository(GameDbContext db) => _db = db;

    public async Task<IReadOnlyList<GameAnnouncement>> GetAllAsync()
    {
        const string sql = @"
            SELECT announcement_id, title, body, is_published, is_startup, published_at, created_at, updated_at
            FROM game_announcement ORDER BY is_startup DESC, updated_at DESC";
        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var announcements = new List<GameAnnouncement>();
        while (await reader.ReadAsync()) announcements.Add(Read(reader));
        return announcements;
    }

    public async Task<GameAnnouncement?> GetStartupAsync()
    {
        const string sql = @"
            SELECT announcement_id, title, body, is_published, is_startup, published_at, created_at, updated_at
            FROM game_announcement
            WHERE is_published = TRUE AND is_startup = TRUE
            ORDER BY updated_at DESC LIMIT 1";
        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<GameAnnouncement>> GetPublishedAsync(int offset, int limit)
    {
        const string sql = @"
            SELECT announcement_id, title, body, is_published, is_startup, published_at, created_at, updated_at
            FROM game_announcement
            WHERE is_published = TRUE
            ORDER BY published_at DESC, announcement_id DESC
            LIMIT @limit OFFSET @offset";
        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@offset", offset);
        cmd.Parameters.AddWithValue("@limit", limit);
        await using var reader = await cmd.ExecuteReaderAsync();
        var announcements = new List<GameAnnouncement>();
        while (await reader.ReadAsync()) announcements.Add(Read(reader));
        return announcements;
    }

    public async Task<GameAnnouncement> CreateAsync(string title, string body, bool isPublished, bool isStartup)
    {
        await using var conn = await _db.CreateConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        if (isStartup) await ClearStartupAsync(conn, tx);
        await using var cmd = new MySqlCommand(@"
            INSERT INTO game_announcement (title, body, is_published, is_startup, published_at)
            VALUES (@title, @body, @published, @startup, IF(@published, CURRENT_TIMESTAMP(3), NULL));
            SELECT LAST_INSERT_ID();", conn, tx);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@body", body);
        cmd.Parameters.AddWithValue("@published", isPublished);
        cmd.Parameters.AddWithValue("@startup", isStartup);
        var id = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        await tx.CommitAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<GameAnnouncement?> UpdateAsync(long announcementId, string title, string body, bool isPublished, bool isStartup)
    {
        await using var conn = await _db.CreateConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        if (isStartup) await ClearStartupAsync(conn, tx);
        await using var cmd = new MySqlCommand(@"
            UPDATE game_announcement
            SET title = @title, body = @body, is_published = @published, is_startup = @startup,
                published_at = IF(@published AND published_at IS NULL, CURRENT_TIMESTAMP(3), published_at),
                updated_at = CURRENT_TIMESTAMP(3)
            WHERE announcement_id = @id", conn, tx);
        cmd.Parameters.AddWithValue("@id", announcementId);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@body", body);
        cmd.Parameters.AddWithValue("@published", isPublished);
        cmd.Parameters.AddWithValue("@startup", isStartup);
        if (await cmd.ExecuteNonQueryAsync() == 0)
        {
            await tx.RollbackAsync();
            return null;
        }
        await tx.CommitAsync();
        return await GetByIdAsync(announcementId);
    }

    public async Task<bool> DeleteAsync(long announcementId)
    {
        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = new MySqlCommand("DELETE FROM game_announcement WHERE announcement_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", announcementId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private async Task<GameAnnouncement?> GetByIdAsync(long announcementId)
    {
        const string sql = @"
            SELECT announcement_id, title, body, is_published, is_startup, published_at, created_at, updated_at
            FROM game_announcement WHERE announcement_id = @id";
        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", announcementId);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    private static async Task ClearStartupAsync(MySqlConnection conn, MySqlTransaction tx)
    {
        await using var cmd = new MySqlCommand(
            "UPDATE game_announcement SET is_startup = FALSE, updated_at = CURRENT_TIMESTAMP(3) WHERE is_startup = TRUE", conn, tx);
        await cmd.ExecuteNonQueryAsync();
    }

    private static GameAnnouncement Read(MySqlDataReader reader)
    {
        var publishedAt = reader.GetOrdinal("published_at");
        return new GameAnnouncement(
            reader.GetInt64("announcement_id"), reader.GetString("title"), reader.GetString("body"),
            reader.GetBoolean("is_published"), reader.GetBoolean("is_startup"),
            reader.IsDBNull(publishedAt) ? null : reader.GetDateTime(publishedAt),
            reader.GetDateTime("created_at"), reader.GetDateTime("updated_at"));
    }
}