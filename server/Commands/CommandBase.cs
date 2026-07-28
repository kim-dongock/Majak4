using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using MajakServer.Models.Player;

namespace MajakServer.Commands;

/// <summary>
/// すべてのコマンドが実装すべきコンテキスト + インターフェース
/// </summary>
public class CommandContext
{
    public string           ConnectionId { get; init; } = "";
    public MajakPlayer?     Player       { get; init; }
    public IClientProxy     Caller       { get; init; } = null!;
    public IHubCallerClients Clients     { get; init; } = null!;
    public IGroupManager    Groups       { get; init; } = null!;
    public string           RemoteIpAddress { get; init; } = "";
    public string           AuthMemberNo { get; init; } = "";
    public string           AuthPix { get; init; } = "";
    public Action           AbortConnection { get; init; } = () => { };
    public Action<string>    AbortConnectionWithReason { get; init; } = _ => { };
    public Dictionary<string, object?> Payload { get; init; } = new();

    public T Get<T>(string key, T defaultValue = default!)
    {
        if (Payload.TryGetValue(key, out var v) && v is T typed) return typed;
        if (v is System.Text.Json.JsonElement je)
        {
            try { return (T)(object)je.Deserialize(typeof(T))!; }
            catch { }
        }
        return defaultValue;
    }

    public string GetString(string key) => Get<string>(key, "");
    public int    GetInt(string key)    => Get<int>(key, 0);
    public int    GetInt(string key, int defaultValue) => Get<int>(key, defaultValue);
    public long   GetLong(string key)   => Get<long>(key, 0L);
    public bool   GetBool(string key)   => Get<bool>(key, false);

    public int[]? GetIntArray(string key)
    {
        if (Payload.TryGetValue(key, out var v))
        {
            if (v is int[] arr) return arr;
            if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
                return je.Deserialize<int[]>();
        }
        return null;
    }

    /// <summary>ユーザースコア配列などの動的オブジェクト配列を取得</summary>
    public dynamic[]? GetObjectArray(string key)
    {
        if (Payload.TryGetValue(key, out var v))
        {
            if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                return je.EnumerateArray()
                    .Select(e => (dynamic)e)
                    .ToArray();
            }
        }
        return null;
    }
}

public interface ICommand
{
    Task ExecuteAsync(CommandContext ctx);
}
