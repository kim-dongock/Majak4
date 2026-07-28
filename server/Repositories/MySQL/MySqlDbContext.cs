using MySqlConnector;
using MajakServer.Services;
using MajakServer.Utils;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// MySQL 接続ファクトリー共通実装。
/// ゲーム DB とログ DB は資格情報・接続プールを共有しない。
/// </summary>
public abstract class MySqlDbContextBase
{
    private readonly IParameterStoreService _parameterStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly MySqlDatabaseRole _role;
    private string? _cachedConnectionString;
    private readonly SemaphoreSlim _connectionStringLock = new(1, 1);

    protected MySqlDbContextBase(
        IParameterStoreService parameterStore,
        IConfiguration configuration,
        ILogger logger,
        MySqlDatabaseRole role)
    {
        _parameterStore = parameterStore;
        _configuration = configuration;
        _logger = logger;
        _role = role;
    }

    public MySqlConnection CreateConnection()
        => CreateConnectionAsync().GetAwaiter().GetResult();

    public async Task<MySqlConnection> CreateConnectionAsync()
    {
        var connStr = await GetConnectionStringAsync();
        var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<string> GetConnectionStringAsync()
    {
        if (_cachedConnectionString is not null)
            return _cachedConnectionString;

        await _connectionStringLock.WaitAsync();
        try
        {
            _cachedConnectionString ??= await BuildConnectionStringAsync();
            return _cachedConnectionString;
        }
        finally
        {
            _connectionStringLock.Release();
        }
    }

    private async Task<string> BuildConnectionStringAsync()
    {
        bool parameterStoreEnabled = _configuration.GetValue<bool>("AWS:ParameterStore:Enabled");
        if (!parameterStoreEnabled)
        {
            string connectionName = _role == MySqlDatabaseRole.Game ? "GameDatabase" : "LogDatabase";
            string? localConnectionString = _configuration.GetConnectionString(connectionName);
            if (!string.IsNullOrWhiteSpace(localConnectionString))
                return localConnectionString;

            throw new InvalidOperationException(
                $"MySQL {_role} database connection is not configured. " +
                $"Set ConnectionStrings:{connectionName} when AWS:ParameterStore:Enabled is false.");
        }

        var config = await _parameterStore.GetParameterConfigAsync();
        var values = _role == MySqlDatabaseRole.Game
            ? (config.mysql_game_host, config.mysql_game_port, config.mysql_game_db,
               config.mysql_game_user, config.mysql_game_password)
            : (First(config.mysql_log_host, config.mysql_host),
               First(config.mysql_log_port, config.mysql_port),
               First(config.mysql_log_db, config.mysql_db),
               First(config.mysql_log_user, config.mysql_user),
               First(config.mysql_log_password, config.mysql_password));

        var host = Decrypt(values.Item1);
        var port = Decrypt(values.Item2);
        var database = Decrypt(values.Item3);
        var user = Decrypt(values.Item4);
        var password = Decrypt(values.Item5);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            string connectionName = _role == MySqlDatabaseRole.Game ? "GameDatabase" : "LogDatabase";
            string? localConnectionString = _configuration.GetConnectionString(connectionName);
            if (!string.IsNullOrWhiteSpace(localConnectionString))
                return localConnectionString;

            throw new InvalidOperationException(
                $"MySQL {_role} database connection is not configured. " +
                $"Set mysql_{_role.ToString().ToLowerInvariant()}_* in Parameter Store or ConnectionStrings:{connectionName}.");
        }

        if (string.IsNullOrWhiteSpace(port)) port = "3306";

        _logger.LogInformation(
            "MySQL {DatabaseRole} database connection configured. HostLength={HostLength}, DatabaseLength={DatabaseLength}",
            _role, host.Length, database.Length);

        int minimumPoolSize = _role == MySqlDatabaseRole.Game ? 5 : 3;
        int maximumPoolSize = _role == MySqlDatabaseRole.Game ? 100 : 50;
        return $"Server={host};Port={port};Database={database};Uid={user};Pwd={password};CharSet=utf8mb4;" +
               $"MinimumPoolSize={minimumPoolSize};MaximumPoolSize={maximumPoolSize};ConnectionTimeout=30;";
    }

    private static string Decrypt(string? value)
        => CryptoHelper.DecryptParameterStoreValue(value ?? "");

    private static string First(string? preferred, string? fallback)
        => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback ?? "";
}

public enum MySqlDatabaseRole
{
    Game,
    Log,
}

/// <summary>ゲーム進行・ユーザーデータ用 MySQL 接続。</summary>
public sealed class GameDbContext : MySqlDbContextBase
{
    public GameDbContext(
        IParameterStoreService parameterStore,
        IConfiguration configuration,
        ILogger<GameDbContext> logger)
        : base(parameterStore, configuration, logger, MySqlDatabaseRole.Game)
    {
    }
}

/// <summary>追記型履歴・監査ログ用 MySQL 接続。</summary>
public class LogDbContext : MySqlDbContextBase
{
    public LogDbContext(
        IParameterStoreService parameterStore,
        IConfiguration configuration,
        ILogger<LogDbContext> logger)
        : base(parameterStore, configuration, logger, MySqlDatabaseRole.Log)
    {
    }

    protected LogDbContext(
        IParameterStoreService parameterStore,
        IConfiguration configuration,
        ILogger logger)
        : base(parameterStore, configuration, logger, MySqlDatabaseRole.Log)
    {
    }
}

/// <summary>旧テストと段階移行コード向け。新規コードは LogDbContext を使用する。</summary>
[Obsolete("Use GameDbContext or LogDbContext explicitly.")]
public sealed class MySqlDbContext : LogDbContext
{
    public MySqlDbContext(
        IParameterStoreService parameterStore,
        IConfiguration configuration,
        ILogger<MySqlDbContext> logger)
        : base(parameterStore, configuration, logger)
    {
    }
}
