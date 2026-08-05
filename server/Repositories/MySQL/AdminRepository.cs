using MySqlConnector;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// 管理サイト用 MySQL クエリ集。raw MySqlCommand を使用 (Dapper 不使用)。
/// </summary>
public class AdminRepository
{
    private readonly GameDbContext _db;

    public AdminRepository(GameDbContext db) => _db = db;

    // ─── 管理者アカウント ────────────────────────────────────────────────
    public async Task<AdminAccount?> FindAdminAccountAsync(string email)
    {
        const string sql =
            "SELECT admin_no, email, role, is_active, created_at " +
            "FROM admin_account WHERE email = @email LIMIT 1";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", email.ToLowerInvariant());
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return ReadAdminAccount(reader);
    }

    public async Task<AdminAccount> UpsertAdminAccountAsync(string email, string role)
    {
        const string sql =
            "INSERT INTO admin_account (email, role, is_active) VALUES (@email, @role, TRUE) " +
            "ON DUPLICATE KEY UPDATE role = @role, updated_at = CURRENT_TIMESTAMP(3)";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", email.ToLowerInvariant());
        cmd.Parameters.AddWithValue("@role",  role);
        await cmd.ExecuteNonQueryAsync();
        return (await FindAdminAccountAsync(email))!;
    }

    public async Task<IReadOnlyList<AdminAccount>> GetAdminAccountsAsync()
    {
        const string sql =
            "SELECT admin_no, email, role, is_active, created_at " +
            "FROM admin_account ORDER BY created_at";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<AdminAccount>();
        while (await reader.ReadAsync()) list.Add(ReadAdminAccount(reader));
        return list;
    }

    public async Task SetAdminAccountActiveAsync(string email, bool isActive)
    {
        const string sql =
            "UPDATE admin_account SET is_active = @active, updated_at = CURRENT_TIMESTAMP(3) " +
            "WHERE email = @email";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email",  email.ToLowerInvariant());
        cmd.Parameters.AddWithValue("@active", isActive);
        await cmd.ExecuteNonQueryAsync();
    }

    private static AdminAccount ReadAdminAccount(MySqlDataReader r) =>
        new(r.GetUInt64("admin_no"), r.GetString("email"), r.GetString("role"),
            r.GetBoolean("is_active"), r.GetDateTime("created_at"));

    // ─── プレイヤー承認 ─────────────────────────────────────────────────────
    /// <summary>承認待ちプレイヤー一覧 (account_status=0 かつ terms_agreed_at IS NOT NULL)</summary>
    public async Task<IReadOnlyList<PendingPlayer>> GetPendingPlayersAsync(int offset, int limit)
    {
        const string sql = @"
            SELECT member_no, display_name, sex_code, avatar_id, terms_agreed_at, created_at
            FROM player_account
            WHERE account_status = 0 AND terms_agreed_at IS NOT NULL
            ORDER BY terms_agreed_at
            LIMIT @limit OFFSET @offset";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@limit",  limit);
        cmd.Parameters.AddWithValue("@offset", offset);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<PendingPlayer>();
        while (await r.ReadAsync())
            list.Add(new PendingPlayer(
                r.GetUInt64("member_no"), r.GetString("display_name"), r.GetString("sex_code"), r.GetString("avatar_id"),
                r.GetDateTime("terms_agreed_at"), r.GetDateTime("created_at")));
        return list;
    }

    public async Task<int> CountPendingPlayersAsync()
    {
        const string sql = @"
            SELECT COUNT(*) FROM player_account
            WHERE account_status = 0 AND terms_agreed_at IS NOT NULL";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>プレイヤーを承認する (account_status 0→1)。</summary>
    public async Task<bool> ApprovePlayerAsync(ulong memberNo, ulong adminNo)
    {
        const string sql = @"
            UPDATE player_account
            SET account_status = 1,
                approved_at    = NOW(3),
                approved_by    = @adminNo,
                updated_at     = NOW(3)
            WHERE member_no = @memberNo AND account_status = 0";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@memberNo",   memberNo);
        cmd.Parameters.AddWithValue("@adminNo", adminNo);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>プレイヤーを停止する (account_status →2)。</summary>
    public async Task<bool> SuspendPlayerAsync(ulong memberNo, ulong adminNo, string reason)
    {
        const string sql = @"
            UPDATE player_account
            SET account_status = 2,
                approved_by    = @adminNo,
                reject_reason  = @reason,
                updated_at     = NOW(3)
            WHERE member_no = @memberNo AND account_status <> 2";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@memberNo",   memberNo);
        cmd.Parameters.AddWithValue("@adminNo", adminNo);
        cmd.Parameters.AddWithValue("@reason",     reason ?? "");
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>停止を解除して再度プレイ可能にする (account_status 2→1)。</summary>
    public async Task<bool> UnsuspendPlayerAsync(ulong memberNo, ulong adminNo)
    {
        const string sql = @"
            UPDATE player_account
            SET account_status = 1,
                approved_by    = @adminNo,
                reject_reason  = NULL,
                updated_at     = NOW(3)
            WHERE member_no = @memberNo AND account_status = 2";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@memberNo",   memberNo);
        cmd.Parameters.AddWithValue("@adminNo", adminNo);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ─── ダッシュボード統計 ────────────────────────────────────────────────
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM player_account)                           AS total_players,
                (SELECT COUNT(*) FROM player_account
                    WHERE last_login_at >= DATE_SUB(NOW(), INTERVAL 24 HOUR))   AS active_today,
                (SELECT COUNT(*) FROM player_account
                    WHERE account_status = 0
                      AND terms_agreed_at IS NOT NULL)                          AS pending_approval,
                (SELECT COALESCE(SUM(cash_amount),0) FROM cash_charge_order
                    WHERE status = 'completed'
                    AND completed_at >= CURDATE())                               AS cash_today,
                (SELECT COALESCE(SUM(price_jpy),0) FROM cash_charge_order
                    WHERE status = 'completed'
                    AND completed_at >= CURDATE())                               AS revenue_today,
                (SELECT COALESCE(SUM(cash_amount),0) FROM cash_charge_order
                    WHERE status = 'completed'
                    AND completed_at >= DATE_FORMAT(NOW(),'%Y-%m-01'))           AS cash_month,
                (SELECT COALESCE(SUM(price_jpy),0) FROM cash_charge_order
                    WHERE status = 'completed'
                    AND completed_at >= DATE_FORMAT(NOW(),'%Y-%m-01'))           AS revenue_month";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        await using var r    = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new DashboardStats(
            r.GetInt64("total_players"),  r.GetInt64("active_today"),
            r.GetInt64("pending_approval"),
            r.GetInt64("cash_today"),     r.GetInt64("revenue_today"),
            r.GetInt64("cash_month"),     r.GetInt64("revenue_month"));
    }

    // ─── プレイヤー検索 ────────────────────────────────────────────────────
    public async Task<IReadOnlyList<PlayerSummary>> SearchPlayersAsync(
        string? keyword, int offset, int limit)
    {
        const string sql = @"
             SELECT a.member_no, a.display_name, a.sex_code, a.avatar_id, a.account_status, a.last_login_at,
                   w.game_money, w.gem_count, w.cash_count, w.paid_cash_count, w.free_cash_count
            FROM player_account a
             JOIN player_wallet w ON w.member_no = a.member_no
            WHERE (@kw IS NULL
                 OR CAST(a.member_no AS CHAR) LIKE CONCAT('%',@kw,'%')
                   OR a.display_name LIKE CONCAT('%',@kw,'%'))
            ORDER BY a.last_login_at DESC
            LIMIT @limit OFFSET @offset";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@kw",     string.IsNullOrWhiteSpace(keyword) ? (object)DBNull.Value : keyword);
        cmd.Parameters.AddWithValue("@limit",  limit);
        cmd.Parameters.AddWithValue("@offset", offset);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<PlayerSummary>();
        while (await r.ReadAsync())
            list.Add(new PlayerSummary(
                r.GetUInt64("member_no"), r.GetString("display_name"), r.GetString("sex_code"), r.GetString("avatar_id"),
                r.GetInt32("account_status"), r.GetDateTime("last_login_at"),
                r.GetInt64("game_money"), r.GetInt32("gem_count"), r.GetInt32("cash_count"),
                r.GetInt32("paid_cash_count"), r.GetInt32("free_cash_count")));
        return list;
    }

    public async Task<PlayerDetail?> GetPlayerDetailAsync(ulong memberNo)
    {
        const string sql = @"
            SELECT a.member_no, a.display_name, a.sex_code, a.avatar_id, a.account_status,
                   a.first_login_at, a.last_login_at,
                   w.game_money, w.gem_count, w.cash_count, w.paid_cash_count, w.free_cash_count,
                   p.common_rating, p.experience, p.weekly_point, p.last_played_at
            FROM player_account a
            JOIN player_wallet  w ON w.member_no = a.member_no
            JOIN player_profile p ON p.member_no = a.member_no
            WHERE a.member_no = @memberNo";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@memberNo", memberNo);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        int lpOrd = r.GetOrdinal("last_played_at");
        return new PlayerDetail(
            r.GetUInt64("member_no"), r.GetString("display_name"), r.GetString("sex_code"), r.GetString("avatar_id"),
            r.GetInt32("account_status"), r.GetDateTime("first_login_at"), r.GetDateTime("last_login_at"),
            r.GetInt64("game_money"), r.GetInt32("gem_count"), r.GetInt32("cash_count"),
            r.GetInt32("paid_cash_count"), r.GetInt32("free_cash_count"),
            r.GetInt32("common_rating"), r.GetInt32("experience"), r.GetInt32("weekly_point"),
            r.IsDBNull(lpOrd) ? null : r.GetDateTime(lpOrd));
    }

    // ─── キャッシュ残高調整 ────────────────────────────────────────────
    /// <summary>
    /// 管理者調整で無償キャッシュを加算、または無償優先で回収する。
    /// </summary>
    public async Task<CashBalanceAdjustment> AdjustCashAsync(ulong memberNo, int amount)
    {
        await using var conn = await _db.CreateConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            await using var selCmd = new MySqlCommand(
                "SELECT cash_count, paid_cash_count, free_cash_count, row_version FROM player_wallet " +
                "WHERE member_no = @m FOR UPDATE", conn, tx);
            selCmd.Parameters.AddWithValue("@m", memberNo);
            await using var r = await selCmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) throw new InvalidOperationException("player_wallet not found");
            int before = r.GetInt32("cash_count");
            int paidBefore = r.GetInt32("paid_cash_count");
            int freeBefore = r.GetInt32("free_cash_count");
            long rowVersion = r.GetInt64("row_version");
            await r.CloseAsync();

            int after = checked(before + amount);
            if (after < 0)
                throw new InvalidOperationException($"Majak Cash balance would go negative: {before} + {amount}");

            int paidAfter = paidBefore;
            int freeAfter = freeBefore;
            if (amount > 0)
            {
                freeAfter = checked(freeBefore + amount);
            }
            else
            {
                int deduction = -amount;
                int freeDeduction = Math.Min(freeBefore, deduction);
                freeAfter -= freeDeduction;
                paidAfter -= deduction - freeDeduction;
            }

            await using var updCmd = new MySqlCommand(
                "UPDATE player_wallet SET cash_count = @after, paid_cash_count = @paidAfter, free_cash_count = @freeAfter, " +
                "row_version = row_version + 1, updated_at = CURRENT_TIMESTAMP(3) " +
                "WHERE member_no = @m AND row_version = @rv", conn, tx);
            updCmd.Parameters.AddWithValue("@after", after);
            updCmd.Parameters.AddWithValue("@paidAfter", paidAfter);
            updCmd.Parameters.AddWithValue("@freeAfter", freeAfter);
            updCmd.Parameters.AddWithValue("@m",     memberNo);
            updCmd.Parameters.AddWithValue("@rv",    rowVersion);
            await updCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return new CashBalanceAdjustment(before, after, paidBefore, paidAfter, freeBefore, freeAfter);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── キャッシュ商品マスター ────────────────────────────────────────
    public async Task<IReadOnlyList<CashProduct>> GetCashProductsAsync()
    {
        const string sql =
            "SELECT product_id, display_name, cash_amount, price_jpy, platform, " +
            "store_product_id, is_active, sort_order " +
            "FROM cash_product_master ORDER BY platform, sort_order";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        await using var r    = await cmd.ExecuteReaderAsync();
        var list = new List<CashProduct>();
        int spOrd = -1;
        while (await r.ReadAsync())
        {
            if (spOrd < 0) spOrd = r.GetOrdinal("store_product_id");
            list.Add(new CashProduct(
                r.GetString("product_id"), r.GetString("display_name"),
                r.GetInt32("cash_amount"), r.GetInt32("price_jpy"),
                r.GetString("platform"),
                r.IsDBNull(spOrd) ? null : r.GetString(spOrd),
                r.GetBoolean("is_active"), r.GetInt32("sort_order")));
        }
        return list;
    }

    public async Task<IReadOnlyList<CashProduct>> GetActiveWebCashProductsAsync()
    {
        const string sql =
            "SELECT product_id, display_name, cash_amount, price_jpy, platform, " +
            "store_product_id, is_active, sort_order " +
            "FROM cash_product_master " +
            "WHERE platform = 'web' AND is_active = TRUE " +
            "ORDER BY sort_order";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<CashProduct>();
        int spOrd = -1;
        while (await r.ReadAsync())
        {
            if (spOrd < 0) spOrd = r.GetOrdinal("store_product_id");
            list.Add(new CashProduct(
                r.GetString("product_id"), r.GetString("display_name"),
                r.GetInt32("cash_amount"), r.GetInt32("price_jpy"),
                r.GetString("platform"),
                r.IsDBNull(spOrd) ? null : r.GetString(spOrd),
                r.GetBoolean("is_active"), r.GetInt32("sort_order")));
        }
        return list;
    }

    public async Task UpdateCashProductAsync(CashProduct p)
    {
        const string sql =
            "UPDATE cash_product_master SET " +
            "display_name=@dn, cash_amount=@ca, price_jpy=@pj, " +
            "store_product_id=@sp, is_active=@ia, sort_order=@so, " +
            "updated_at=CURRENT_TIMESTAMP(3) WHERE product_id=@id";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@dn", p.DisplayName);
        cmd.Parameters.AddWithValue("@ca", p.CashAmount);
        cmd.Parameters.AddWithValue("@pj", p.PriceJpy);
        cmd.Parameters.AddWithValue("@sp", (object?)p.StoreProductId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ia", p.IsActive);
        cmd.Parameters.AddWithValue("@so", p.SortOrder);
        cmd.Parameters.AddWithValue("@id", p.ProductId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── キャッシュ売上統計 ────────────────────────────────────────────
    public async Task<IReadOnlyList<DailyRevenue>> GetDailyRevenueAsync(int days)
    {
        const string sql = @"
            SELECT DATE(completed_at) AS revenue_date, platform,
                   COUNT(*)           AS order_count,
                   SUM(cash_amount)   AS total_cash,
                   SUM(price_jpy)     AS total_jpy
            FROM cash_charge_order
            WHERE status = 'completed'
              AND completed_at >= DATE_SUB(CURDATE(), INTERVAL @days DAY)
            GROUP BY DATE(completed_at), platform
            ORDER BY revenue_date DESC, platform";

        await using var conn = await _db.CreateConnectionAsync();
        await using var cmd  = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@days", days);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<DailyRevenue>();
        while (await r.ReadAsync())
            list.Add(new DailyRevenue(
                DateOnly.FromDateTime(r.GetDateTime("revenue_date")),
                r.GetString("platform"),
                r.GetInt32("order_count"),
                r.GetInt64("total_cash"),
                r.GetInt64("total_jpy")));
        return list;
    }
}

// ─── DTO ──────────────────────────────────────────────────────────────────
public record AdminAccount(ulong AdminNo, string Email, string Role, bool IsActive, DateTime CreatedAt);

public record DashboardStats(
    long TotalPlayers, long ActivePlayersToday,
    long PendingApproval,
    long CashChargedToday, long RevenueJpyToday,
    long CashChargedThisMonth, long RevenueJpyThisMonth);

public record PendingPlayer(
    ulong MemberNo, string DisplayName, string SexCode, string AvatarId,
    DateTime TermsAgreedAt, DateTime RegisteredAt);

public record PlayerSummary(
    ulong MemberNo, string DisplayName, string SexCode, string AvatarId,
    int AccountStatus, DateTime LastLoginAt,
    long GameMoney, int GemCount, int CashCount, int PaidCashCount, int FreeCashCount);

public record PlayerDetail(
    ulong MemberNo, string DisplayName, string SexCode, string AvatarId,
    int AccountStatus, DateTime FirstLoginAt, DateTime LastLoginAt,
    long GameMoney, int GemCount, int CashCount, int PaidCashCount, int FreeCashCount,
    int CommonRating, int Experience, int WeeklyPoint, DateTime? LastPlayedAt);

public record CashBalanceAdjustment(
    int TotalBefore, int TotalAfter,
    int PaidBefore, int PaidAfter,
    int FreeBefore, int FreeAfter);

public record CashProduct(
    string ProductId, string DisplayName, int CashAmount,
    int PriceJpy, string Platform, string? StoreProductId,
    bool IsActive, int SortOrder);

public record DailyRevenue(
    DateOnly RevenueDate, string Platform,
    int OrderCount, long TotalCash, long TotalJpy);
