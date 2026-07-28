using Microsoft.EntityFrameworkCore;

namespace MajakServer.Repositories.MySQL;

internal sealed record TransactionCodeMetadata(
    string EventTitle,
    string GameId,
    bool IsHistoryEnabled);

internal static class TransactionCodeMetadataResolver
{
    public static async Task<TransactionCodeMetadata?> ResolveAsync(
        GameDataContextFactory gameDb,
        string eventCode)
    {
        if (string.IsNullOrWhiteSpace(eventCode)) return null;

        await using var db = await gameDb.CreateAsync();
        var values = await db.TransactionCodeMasters
            .AsNoTracking()
            .Where(code => code.TransactionCode == eventCode)
            .Select(code => new
            {
                code.CodeTitle,
                code.GameId,
                code.IsHistoryEnabled,
            })
            .SingleOrDefaultAsync();

        return values is null
            ? null
            : CreateMetadata(eventCode, values.CodeTitle, values.GameId, values.IsHistoryEnabled);
    }

    internal static TransactionCodeMetadata CreateMetadata(
        string eventCode,
        string? codeTitle,
        string? gameId,
        bool isHistoryEnabled)
        => new(
            string.IsNullOrWhiteSpace(codeTitle) ? eventCode : codeTitle,
            string.IsNullOrWhiteSpace(gameId) ? Models.Protocol.GameConst.ServiceId : gameId,
            isHistoryEnabled);
}
