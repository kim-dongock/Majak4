namespace MajakServer.Repositories.MySQL.Entities;

public sealed class PaifuArchiveLogEntity
{
    public ulong PaifuArchiveId { get; set; }
    public DateTime PlayedAt { get; set; }
    public string ChannelId { get; set; } = "";
    public uint RoomId { get; set; }
    public string RoomName { get; set; } = "";
    public string RoomOption { get; set; } = "";
    public string ResultText { get; set; } = "";
    public string MembersJson { get; set; } = "[]";
    public int PacketCount { get; set; }
    public string S3ObjectKey { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}