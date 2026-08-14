using MajakServer.Services;
using MySqlConnector;

namespace MajakServer.Repositories.MySQL;

public sealed class EconomyPolicyRepository
{
    private readonly GameDbContext _db;

    public EconomyPolicyRepository(GameDbContext db) => _db = db;

    public async Task<GameEconomyPolicy?> GetAsync()
    {
        const string sql = @"
            SELECT initial_gp, free_replenish_target_gp, free_replenish_daily_limit, updated_at
            FROM game_economy_policy
            WHERE policy_id = 1";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new GameEconomyPolicy(
            reader.GetInt64("initial_gp"),
            reader.GetInt64("free_replenish_target_gp"),
            reader.GetInt32("free_replenish_daily_limit"),
            reader.GetDateTime("updated_at"));
    }

    public async Task<GameEconomyPolicy> UpdateAsync(
        long initialGp,
        long freeReplenishTargetGp,
        int freeReplenishDailyLimit)
    {
        const string sql = @"
            INSERT INTO game_economy_policy
                (policy_id, initial_gp, free_replenish_target_gp, free_replenish_daily_limit)
            VALUES (1, @initialGp, @targetGp, @dailyLimit)
            ON DUPLICATE KEY UPDATE
                initial_gp = VALUES(initial_gp),
                free_replenish_target_gp = VALUES(free_replenish_target_gp),
                free_replenish_daily_limit = VALUES(free_replenish_daily_limit),
                updated_at = CURRENT_TIMESTAMP(3)";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@initialGp", initialGp);
        cmd.Parameters.AddWithValue("@targetGp", freeReplenishTargetGp);
        cmd.Parameters.AddWithValue("@dailyLimit", freeReplenishDailyLimit);
        await cmd.ExecuteNonQueryAsync();
        return (await GetAsync())!;
    }
}