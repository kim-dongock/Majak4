using MajakServer.Repositories.MySQL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// Hangame 認証成功後のゲーム会員登録。
/// 認証情報は保存せず、表示用プロフィールとゲーム初期状態だけを保持する。
/// </summary>
public class GamePlayerRepository
{
    private readonly GameDataContextFactory _db;

    public GamePlayerRepository(GameDataContextFactory db) => _db = db;

    public virtual async Task<GamePlayerAccount?> GetAccountAsync(string memberNo)
    {
        if (!TryParseMemberNo(memberNo, out var memberNoValue)) return null;
        await using var db = await _db.CreateAsync();
        return await db.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.MemberNo == memberNoValue)
            .Select(account => ToAccount(account))
            .SingleOrDefaultAsync();
    }

    /// <summary>利用規約同意を記録する。account_status は変えない (管理者承認待ちのまま)。</summary>
    public virtual async Task AgreeToTermsAsync(string memberNo)
    {
        if (!TryParseMemberNo(memberNo, out var memberNoValue)) return;
        await using var db = await _db.CreateAsync();
        var now = DateTime.UtcNow;
        await db.PlayerAccounts
            .Where(account => account.MemberNo == memberNoValue && account.TermsAgreedAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(account => account.TermsAgreedAt, now)
                .SetProperty(account => account.UpdatedAt, now));
    }

    public virtual async Task RefreshLoginAsync(
        string memberNo,
        string displayName,
        bool isTestEnvironment)
    {
        if (!TryParseMemberNo(memberNo, out var memberNoValue)) return;
        await using var db = await _db.CreateAsync();
        var now = DateTime.UtcNow;
        await db.PlayerAccounts
            .Where(account => account.MemberNo == memberNoValue)
            .ExecuteUpdateAsync(update => update
                .SetProperty(account => account.DisplayName, displayName ?? string.Empty)
                .SetProperty(account => account.SourceEnvironment, isTestEnvironment ? "test" : "production")
                .SetProperty(account => account.LastLoginAt, now)
                .SetProperty(account => account.UpdatedAt, now));
    }

    // ── Google 認証専用メソッド ───────────────────────────────────────

    /// <summary>google_sub でアカウントを検索する。</summary>
    public virtual async Task<GamePlayerAccount?> GetAccountByGoogleSubAsync(string googleSub)
    {
        await using var db = await _db.CreateAsync();
        return await db.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.GoogleSub == googleSub)
            .Select(account => ToAccount(account))
            .SingleOrDefaultAsync();
    }

    /// <summary>display_name (ニックネーム) が使用可能かどうかを確認する。</summary>
    public virtual async Task<bool> IsNicknameAvailableAsync(string displayName)
    {
        await using var db = await _db.CreateAsync();
        return !await db.PlayerAccounts.AnyAsync(account => account.DisplayName == displayName);
    }

    /// <summary>Google 認証で初回登録する (利用規約同意も同時に記録)。</summary>
    public virtual async Task<ulong> RegisterGoogleAsync(
        string googleSub,
        string displayName,
        string sexCode,
        ushort birthYear,
        string avatarId)
    {
        await using var strategyDb = await _db.CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var db = await _db.CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            var memberNo = await InsertGoogleAccountAsync(db, displayName, sexCode, birthYear, avatarId, googleSub);
            AddRelatedPlayerRows(db, memberNo);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return memberNo;
        });
    }

    // ── 旧 Hangame 認証 (互換維持) ────────────────────────────────────

    public virtual async Task RegisterAsync(
        string memberNo,
        string displayName,
        string sexCode,
        string avatarId,
        bool isTestEnvironment)
    {
        if (!TryParseMemberNo(memberNo, out var memberNoValue))
            throw new InvalidOperationException($"member_no must be numeric: {memberNo}");
        await using var db = await _db.CreateAsync();
        AddNewPlayer(
            db,
            memberNoValue,
            displayName,
            sexCode,
            avatarId,
            isTestEnvironment ? "test" : "production",
            googleSub: null,
            email: null,
            termsAgreed: false);
        await db.SaveChangesAsync();
    }

    private static async Task<ulong> InsertGoogleAccountAsync(
        GameDataContext db,
        string displayName,
        string sexCode,
        ushort birthYear,
        string avatarId,
        string googleSub)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var now = DateTime.UtcNow;
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = @"
            INSERT INTO player_account
                (display_name, email, google_sub, sex_code, birth_year, avatar_id,
                 terms_agreed_at, account_status, source_environment,
                 first_login_at, last_login_at, created_at, updated_at)
            VALUES
                (@displayName, @email, @googleSub, @sexCode, @birthYear, @avatarId,
                 @now, 0, 'google', @now, @now, @now, @now)";
        AddParameter(cmd, "@displayName", displayName ?? string.Empty);
        AddParameter(cmd, "@email", null);
        AddParameter(cmd, "@googleSub", googleSub);
        AddParameter(cmd, "@sexCode", sexCode);
        AddParameter(cmd, "@birthYear", birthYear);
        AddParameter(cmd, "@avatarId", avatarId);
        AddParameter(cmd, "@now", now);
        await cmd.ExecuteNonQueryAsync();

        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT LAST_INSERT_ID()";
        var value = await cmd.ExecuteScalarAsync();
        return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
    }

    private static void AddNewPlayer(
        GameDataContext db,
        ulong memberNo,
        string displayName,
        string sexCode,
        string avatarId,
        string sourceEnvironment,
        string? googleSub,
        string? email,
        bool termsAgreed)
    {
        var now = DateTime.UtcNow;
        db.AddRange(
            new PlayerAccountEntity
            {
                MemberNo = memberNo,
                DisplayName = displayName ?? string.Empty,
                Email = email,
                GoogleSub = googleSub,
                SexCode = sexCode,
                AvatarId = avatarId,
                TermsAgreedAt = termsAgreed ? now : null,
                AccountStatus = 0,
                SourceEnvironment = sourceEnvironment,
                FirstLoginAt = now,
                LastLoginAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new PlayerWalletEntity
            {
                MemberNo = memberNo,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new PlayerProfileEntity
            {
                MemberNo = memberNo,
                WeeklyTargetDate = DateOnly.FromDateTime(now),
                JoinedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new PlayerModeStatsEntity
            {
                MemberNo = memberNo,
                ModeCode = "regular",
                CreatedAt = now,
                UpdatedAt = now,
            });
    }

    private static void AddRelatedPlayerRows(GameDataContext db, ulong memberNo)
    {
        var now = DateTime.UtcNow;
        db.AddRange(
            new PlayerWalletEntity
            {
                MemberNo = memberNo,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new PlayerProfileEntity
            {
                MemberNo = memberNo,
                WeeklyTargetDate = DateOnly.FromDateTime(now),
                JoinedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new PlayerModeStatsEntity
            {
                MemberNo = memberNo,
                ModeCode = "regular",
                CreatedAt = now,
                UpdatedAt = now,
            });
    }

    private static GamePlayerAccount ToAccount(PlayerAccountEntity account)
        => new(
            account.DisplayName,
            account.SexCode,
            account.BirthYear,
            account.AvatarId,
            account.AccountStatus,
            account.TermsAgreedAt)
        {
            MemberNoValue = account.MemberNo,
        };

    private static bool TryParseMemberNo(string memberNo, out ulong memberNoValue)
        => ulong.TryParse(memberNo, NumberStyles.None, CultureInfo.InvariantCulture, out memberNoValue);

    private static void AddParameter(
        System.Data.Common.DbCommand cmd,
        string name,
        object? value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(parameter);
    }
}

public sealed record GamePlayerAccount(
    string DisplayName,
    string SexCode,
    ushort? BirthYear,
    string AvatarId,
    int AccountStatus,
    DateTime? TermsAgreedAt)
{
    /// <summary>DB の member_no。</summary>
    public ulong MemberNoValue { get; init; }
    /// <summary>レガシープロトコル互換の文字列表現。</summary>
    public string MemberNo => MemberNoValue.ToString(CultureInfo.InvariantCulture);
    /// <summary>プレイ可能かどうか (管理者承認済み)。</summary>
    public bool IsActive => AccountStatus == 1;
    /// <summary>利用規約に同意済みかどうか。</summary>
    public bool TermsAgreed => TermsAgreedAt.HasValue;
}