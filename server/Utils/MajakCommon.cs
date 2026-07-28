using System.Globalization;

namespace MajakServer.Utils;

/// <summary>
/// Common date/string helpers migrated from HMajCommon.cpp.
/// </summary>
public static class MajakCommon
{
    public const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";

    public static bool TryParseDateTime(string? value, out DateTime dateTime)
        => DateTime.TryParseExact(
            value,
            DateTimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out dateTime);

    public static string FormatDateTime(DateTime dateTime)
        => dateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static bool TryAddMinutes(DateTime baseDateTime, int minutes, out DateTime result)
    {
        if (baseDateTime == default || minutes <= 0)
        {
            result = default;
            return false;
        }

        result = baseDateTime.AddMinutes(minutes);
        return true;
    }

    public static bool TrySubtractMinutes(DateTime baseDateTime, int minutes, out DateTime result)
    {
        if (baseDateTime == default || minutes <= 0)
        {
            result = default;
            return false;
        }

        result = baseDateTime.AddMinutes(-minutes);
        return true;
    }

    public static DateTime GetStartOfWeek(DateTime dateTime)
    {
        int daysFromMonday = dateTime.DayOfWeek == DayOfWeek.Sunday
            ? 6 : (int)dateTime.DayOfWeek - (int)DayOfWeek.Monday;
        return dateTime.Date.AddDays(-daysFromMonday);
    }

    public static DateTime GetStartOfDay(DateTime dateTime)
        => dateTime.Date;

    public static int Compare(DateTime left, DateTime right)
        => DateTime.Compare(left, right);

    public static bool IsSameDay(DateTime left, DateTime right)
        => left.Date == right.Date;

    public static bool IsSameWeek(DateTime left, DateTime right)
        => GetStartOfWeek(left) == GetStartOfWeek(right);

    public static List<string> SplitSchedule(string? value)
        => string.IsNullOrEmpty(value)
            ? []
            : value.Split('|', StringSplitOptions.RemoveEmptyEntries).Take(10).ToList();

    public static List<string> SplitSeparatedValues(string? value, int maxSize)
        => string.IsNullOrEmpty(value) || maxSize > 100
            ? []
            : value.Split('|', StringSplitOptions.RemoveEmptyEntries).Take(100).ToList();
}