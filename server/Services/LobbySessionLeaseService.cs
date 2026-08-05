using System.Collections.Concurrent;
using System.Text.Json;
using MajakServer.Infrastructure;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MajakServer.Services;

public enum LobbySessionLeaseStatus
{
    Acquired,
    ReplacedSameTab,
    Denied,
}

public sealed record LobbySessionLeaseAttempt(
    LobbySessionLeaseStatus Status,
    LobbySessionLeaseHandle? Lease = null);

public sealed class LobbySessionLeaseHandle : IAsyncDisposable
{
    private LobbySessionLeaseService? _owner;
    private readonly string _connectionId;
    private readonly bool _newlyAcquired;
    private bool _committed;

    internal LobbySessionLeaseHandle(
        LobbySessionLeaseService owner,
        string connectionId,
        bool newlyAcquired)
    {
        _owner = owner;
        _connectionId = connectionId;
        _newlyAcquired = newlyAcquired;
    }

    public void Commit() => _committed = true;

    public async ValueTask DisposeAsync()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        if (owner != null && _newlyAcquired && !_committed)
            await owner.ReleaseAsync(_connectionId);
    }
}

/// <summary>
/// Redis-backed global lobby connection lease.
/// A lease is acquired with SET NX and can only be renewed or deleted by its exact token value.
/// </summary>
public class LobbySessionLeaseService
{
    private sealed record LeaseValue(
        string ServerId,
        string ConnectionId,
        string TabId,
        string LeaseToken);

    private sealed record LocalLease(
        string MemberNo,
        string ConnectionId,
        string TabId,
        string SerializedValue);

    private sealed record FallbackLease(
        string SerializedValue,
        DateTimeOffset ExpiresAt);

    private const string KeyPrefix = "player:lobby-session:";

    private const string RenewScript = """
        local current = redis.call('GET', KEYS[1])
        if current == ARGV[1] then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            return 1
        end
        if not current then
            local claimed = redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2], 'NX')
            if claimed then return 1 end
        end
        return 0
        """;

    private const string AcquireScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then
            local claimed = redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[3], 'NX')
            if claimed then return 1 end
            current = redis.call('GET', KEYS[1])
        end
        if current then
            local previous = cjson.decode(current)
            if previous.TabId and previous.TabId == ARGV[2] then
                redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[3])
                return 2
            end
        end
        return 0
        """;

    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private readonly RedisService _redis;
    private readonly string _serverId;
    private readonly TimeSpan _ttl;
    private readonly ILogger<LobbySessionLeaseService> _logger;
    private readonly ConcurrentDictionary<string, LocalLease> _leasesByConnection = new();
    private readonly Dictionary<string, FallbackLease> _fallbackLeases = new(StringComparer.Ordinal);
    private readonly object _fallbackSync = new();
    private int _redisFallbackWarningLogged;

    public LobbySessionLeaseService(
        RedisService redis,
        IOptions<ChannelServerSettings> settings,
        ILogger<LobbySessionLeaseService> logger)
    {
        _redis = redis;
        _serverId = settings.Value.ServerUrl;
        _ttl = TimeSpan.FromSeconds(Math.Max(30, settings.Value.LobbySessionLeaseSeconds));
        _logger = logger;
    }

    public async Task<LobbySessionLeaseAttempt> TryAcquireAsync(
        string memberNo,
        string connectionId,
        string tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId))
            return new LobbySessionLeaseAttempt(LobbySessionLeaseStatus.Denied);

        if (_leasesByConnection.TryGetValue(connectionId, out var current)
            && current.MemberNo == memberNo
            && current.TabId == tabId
            && await RenewAsync(current))
        {
            return new LobbySessionLeaseAttempt(
                LobbySessionLeaseStatus.Acquired,
                new LobbySessionLeaseHandle(this, connectionId, newlyAcquired: false));
        }

        _leasesByConnection.TryRemove(connectionId, out _);

        var value = new LeaseValue(
            _serverId,
            connectionId,
            tabId,
            Convert.ToHexString(Guid.NewGuid().ToByteArray()));
        string serializedValue = JsonSerializer.Serialize(value);
        var lease = new LocalLease(memberNo, connectionId, tabId, serializedValue);

        var status = await TryAcquireValueAsync(lease);
        if (status == LobbySessionLeaseStatus.Denied)
            return new LobbySessionLeaseAttempt(LobbySessionLeaseStatus.Denied);

        if (status == LobbySessionLeaseStatus.ReplacedSameTab)
        {
            foreach (var priorLease in _leasesByConnection.Values.Where(existing =>
                existing.MemberNo == memberNo && existing.TabId == tabId))
            {
                _leasesByConnection.TryRemove(
                    new KeyValuePair<string, LocalLease>(priorLease.ConnectionId, priorLease));
            }
        }
        _leasesByConnection[connectionId] = lease;
        return new LobbySessionLeaseAttempt(
            status,
            new LobbySessionLeaseHandle(this, connectionId, newlyAcquired: true));
    }

    public async Task ReleaseAsync(string connectionId)
    {
        if (!_leasesByConnection.TryRemove(connectionId, out var lease)) return;

        if (_redis.IsAvailable)
        {
            try
            {
                await _redis.Db!.ScriptEvaluateAsync(
                    ReleaseScript,
                    new RedisKey[] { Key(lease.MemberNo) },
                    new RedisValue[] { lease.SerializedValue });
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Lobby session lease release failed in Redis. memberNo={MemberNo} connectionId={ConnectionId}",
                    lease.MemberNo, connectionId);
            }
        }

        ReleaseFallback(lease);
    }

    public async Task<IReadOnlyList<string>> RefreshAllAsync()
    {
        var lostConnections = new List<string>();
        foreach (var lease in _leasesByConnection.Values)
        {
            if (await RenewAsync(lease)) continue;
            if (_leasesByConnection.TryRemove(
                    new KeyValuePair<string, LocalLease>(lease.ConnectionId, lease)))
            {
                lostConnections.Add(lease.ConnectionId);
            }
        }
        return lostConnections;
    }

    public async Task ReleaseAllAsync()
    {
        foreach (string connectionId in _leasesByConnection.Keys.ToArray())
            await ReleaseAsync(connectionId);
    }

    private async Task<LobbySessionLeaseStatus> TryAcquireValueAsync(LocalLease lease)
    {
        if (_redis.IsAvailable)
        {
            try
            {
                long result = (long)await _redis.Db!.ScriptEvaluateAsync(
                    AcquireScript,
                    new RedisKey[] { Key(lease.MemberNo) },
                    new RedisValue[]
                    {
                        lease.SerializedValue,
                        lease.TabId,
                        (long)_ttl.TotalMilliseconds,
                    });
                return result switch
                {
                    1 => LobbySessionLeaseStatus.Acquired,
                    2 => LobbySessionLeaseStatus.ReplacedSameTab,
                    _ => LobbySessionLeaseStatus.Denied,
                };
            }
            catch (Exception ex)
            {
                LogRedisFallback(ex);
            }
        }
        else
        {
            LogRedisFallback();
        }

        lock (_fallbackSync)
        {
            RemoveExpiredFallbackLease(lease.MemberNo);
            if (_fallbackLeases.TryGetValue(lease.MemberNo, out var current))
            {
                var previous = JsonSerializer.Deserialize<LeaseValue>(current.SerializedValue);
                if (previous?.TabId != lease.TabId)
                    return LobbySessionLeaseStatus.Denied;
                _fallbackLeases[lease.MemberNo] = new FallbackLease(
                    lease.SerializedValue,
                    DateTimeOffset.UtcNow.Add(_ttl));
                return LobbySessionLeaseStatus.ReplacedSameTab;
            }
            _fallbackLeases[lease.MemberNo] = new FallbackLease(
                lease.SerializedValue,
                DateTimeOffset.UtcNow.Add(_ttl));
            return LobbySessionLeaseStatus.Acquired;
        }
    }

    private async Task<bool> RenewAsync(LocalLease lease)
    {
        if (_redis.IsAvailable)
        {
            try
            {
                long renewed = (long)await _redis.Db!.ScriptEvaluateAsync(
                    RenewScript,
                    new RedisKey[] { Key(lease.MemberNo) },
                    new RedisValue[] { lease.SerializedValue, (long)_ttl.TotalMilliseconds });
                return renewed == 1;
            }
            catch (Exception ex)
            {
                LogRedisFallback(ex);
            }
        }
        else
        {
            LogRedisFallback();
        }

        lock (_fallbackSync)
        {
            RemoveExpiredFallbackLease(lease.MemberNo);
            if (_fallbackLeases.TryGetValue(lease.MemberNo, out var current)
                && current.SerializedValue != lease.SerializedValue)
            {
                return false;
            }
            _fallbackLeases[lease.MemberNo] = new FallbackLease(
                lease.SerializedValue,
                DateTimeOffset.UtcNow.Add(_ttl));
            return true;
        }
    }

    private void ReleaseFallback(LocalLease lease)
    {
        lock (_fallbackSync)
        {
            if (_fallbackLeases.TryGetValue(lease.MemberNo, out var current)
                && current.SerializedValue == lease.SerializedValue)
            {
                _fallbackLeases.Remove(lease.MemberNo);
            }
        }
    }

    private void RemoveExpiredFallbackLease(string memberNo)
    {
        if (_fallbackLeases.TryGetValue(memberNo, out var current)
            && current.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _fallbackLeases.Remove(memberNo);
        }
    }

    private void LogRedisFallback(Exception? exception = null)
    {
        if (Interlocked.Exchange(ref _redisFallbackWarningLogged, 1) != 0) return;
        _logger.LogWarning(exception,
            "Redis is unavailable; lobby session uniqueness is limited to this server instance.");
    }

    private static string Key(string memberNo) => $"{KeyPrefix}{memberNo}";
}
