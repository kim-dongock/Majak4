namespace MajakServer.Services;

public static class AvatarCatalog
{
    public const string LocalBase =
        "/assets/images/characters";

    private static readonly HashSet<string> ValidThumbnailFiles =
        Enumerable.Range(1, 16)
            .SelectMany(index => new[]
            {
                $"thumbnail_{index:00}m.png",
                $"thumbnail_{index:00}f.png",
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetAvatars(string sexCode)
        => Enumerable.Range(1, 16)
            .Select(index => $"{LocalBase}/thumbnail_{index:00}{GetSuffix(sexCode)}.png")
            .ToArray();

    public static bool IsValid(string? sexCode, string? avatarUrl)
    {
        var normalizedSex = sexCode?.ToUpperInvariant();
        if (normalizedSex is not ("M" or "F") || string.IsNullOrWhiteSpace(avatarUrl))
            return false;

        var fileName = ExtractThumbnailFileName(avatarUrl);
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        if (!ValidThumbnailFiles.Contains(fileName)) return false;

        return fileName.EndsWith(GetSuffix(normalizedSex) + ".png", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidThumbnailFileName(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName)
           && ValidThumbnailFiles.Contains(fileName);

    private static string? ExtractThumbnailFileName(string avatarUrl)
    {
        var noQuery = avatarUrl.Split('?', '#')[0];
        var fileName = noQuery.Split('/').LastOrDefault();
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    private static char GetSuffix(string sexCode)
        => sexCode.ToUpperInvariant() switch
        {
            "M" => 'm',
            "F" => 'f',
            _ => throw new ArgumentOutOfRangeException(nameof(sexCode)),
        };
}