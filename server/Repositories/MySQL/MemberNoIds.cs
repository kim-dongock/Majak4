using System.Globalization;

namespace MajakServer.Repositories.MySQL;

internal static class MemberNoIds
{
    public static ulong Parse(string memberNo)
        => ulong.Parse(memberNo, NumberStyles.None, CultureInfo.InvariantCulture);

    public static bool TryParse(string memberNo, out ulong value)
        => ulong.TryParse(memberNo, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    public static string Format(ulong memberNo)
        => memberNo.ToString(CultureInfo.InvariantCulture);

    public static string Format(ulong? memberNo)
        => memberNo.HasValue ? Format(memberNo.Value) : string.Empty;
}