namespace MajakServer.Models.Protocol;

public sealed class GameDiscardAuditReport
{
    public int RoomId { get; init; }
    public long AuditSeq { get; init; }
    public long ActionSeq { get; init; }
    public int SeatOrder { get; init; }
    public int Action { get; init; }
    public int[] HandCounts { get; init; } = Array.Empty<int>();
    public int[] VisibleDiscardCounts { get; init; } = Array.Empty<int>();
    public int[] ClaimedDiscardCounts { get; init; } = Array.Empty<int>();
    public int[] MeldCounts { get; init; } = Array.Empty<int>();
    public int PendingDiscardCount { get; init; }
}