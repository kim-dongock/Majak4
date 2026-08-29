using MajakServer.Repositories.MySQL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// Hangame 認証成功後のゲーム会員登録。
/// 認証情報は保存せず、表示用プロフィールとゲーム初期状態だけを保持する。
/// </summary>
public class GamePlayerRepository
{
    private const string GoogleAuthPrefix = "google:";
    private const string HangeAuthPrefix = "hange:";
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

    /// <summary>利用規約同意を記録する。</summary>
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

    public virtual async Task RefreshHangeLoginAsync(
        string memberNo,
        string displayName,
        string sexCode,
        ushort? birthYear,
        string avatarId,
        bool isTestEnvironment)
    {
        if (!TryParseMemberNo(memberNo, out var memberNoValue)) return;
        await using var db = await _db.CreateAsync();
        var now = DateTime.UtcNow;
        await db.PlayerAccounts
            .Where(account => account.MemberNo == memberNoValue)
            .ExecuteUpdateAsync(update => update
                .SetProperty(account => account.DisplayName, displayName)
                .SetProperty(account => account.SexCode, sexCode)
                .SetProperty(account => account.BirthYear, birthYear)
                .SetProperty(account => account.AvatarId, avatarId)
                .SetProperty(account => account.SourceEnvironment, isTestEnvironment ? "test" : "production")
                .SetProperty(account => account.LastLoginAt, now)
                .SetProperty(account => account.UpdatedAt, now));
    }

    public virtual async Task<bool> UpdateAccountProfileAsync(
        string memberNo,
        ushort birthYear,
        string avatarId)
    {
        if (!TryParseMemberNo(memberNo, out var memberNoValue)) return false;
        await using var db = await _db.CreateAsync();
        var now = DateTime.UtcNow;
        var updated = await db.PlayerAccounts
            .Where(account => account.MemberNo == memberNoValue)
            .ExecuteUpdateAsync(update => update
                .SetProperty(account => account.BirthYear, birthYear)
                .SetProperty(account => account.AvatarId, avatarId)
                .SetProperty(account => account.UpdatedAt, now));
        return updated == 1;
    }

    // ── Google 認証専用メソッド ───────────────────────────────────────

    /// <summary>Google subに対応するexternal_auth_idでアカウントを検索する。</summary>
    public virtual async Task<GamePlayerAccount?> GetAccountByGoogleSubAsync(string googleSub)
    {
        var externalAuthId = GoogleAuthPrefix + googleSub;
        await using var db = await _db.CreateAsync();
        return await db.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.ExternalAuthId == externalAuthId)
            .Select(account => ToAccount(account))
            .SingleOrDefaultAsync();
    }

    public virtual async Task<GamePlayerAccount?> GetAccountByHangeUserNoAsync(string userNo)
    {
        var externalAuthId = HangeAuthPrefix + userNo;
        await using var db = await _db.CreateAsync();
        return await db.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.ExternalAuthId == externalAuthId)
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
        string? email,
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
            var account = CreateGoogleAccount(displayName, sexCode, birthYear, avatarId, GoogleAuthPrefix + googleSub, email);
            db.PlayerAccounts.Add(account);
            await db.SaveChangesAsync();
            AddRelatedPlayerRows(db, account.MemberNo);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return account.MemberNo;
        });
    }

    // ── 旧 Hangame 認証 (互換維持) ────────────────────────────────────

    public virtual async Task<ulong> RegisterHangeAsync(
        string userNo,
        string displayName,
        string sexCode,
        string avatarId,
        bool isTestEnvironment,
        ushort? birthYear = null)
    {
        if (!ulong.TryParse(userNo, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            throw new InvalidOperationException("Hange userno must be numeric.");

        await using var strategyDb = await _db.CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var db = await _db.CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            var account = CreateHangeAccount(
                displayName,
                sexCode,
                birthYear,
                avatarId,
                HangeAuthPrefix + userNo,
                isTestEnvironment);
            db.PlayerAccounts.Add(account);
            await db.SaveChangesAsync();
            AddRelatedPlayerRows(db, account.MemberNo);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return account.MemberNo;
        });
    }

    private static PlayerAccountEntity CreateGoogleAccount(
        string displayName,
        string sexCode,
        ushort birthYear,
        string avatarId,
        string externalAuthId,
        string? email)
    {
        var now = DateTime.UtcNow;
        return new PlayerAccountEntity
        {
            DisplayName = displayName ?? string.Empty,
            Email = email,
            ExternalAuthId = externalAuthId,
            SexCode = sexCode,
            BirthYear = birthYear,
            AvatarId = avatarId,
            TermsAgreedAt = now,
            AccountStatus = 1,
            SourceEnvironment = "google",
            FirstLoginAt = now,
            LastLoginAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static PlayerAccountEntity CreateHangeAccount(
        string displayName,
        string sexCode,
        ushort? birthYear,
        string avatarId,
        string externalAuthId,
        bool isTestEnvironment)
    {
        var now = DateTime.UtcNow;
        return new PlayerAccountEntity
        {
            DisplayName = displayName,
            ExternalAuthId = externalAuthId,
            SexCode = sexCode,
            BirthYear = birthYear,
            AvatarId = avatarId,
            AccountStatus = 1,
            SourceEnvironment = isTestEnvironment ? "test" : "production",
            FirstLoginAt = now,
            LastLoginAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
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