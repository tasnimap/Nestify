// src/Nestify.Web/Services/DateFormatterService.cs
namespace Nestify.Web.Services;

/// <summary>
/// Formats DateTime values to Asia/Dhaka timezone.
/// Per §0.3 assumption: All timestamps are stored in UTC; display converts to Asia/Dhaka (UTC+6).
/// Why: a settlement month boundary computed in the wrong zone silently moves expenses between months.
/// </summary>
public sealed class DateFormatterService
{
    private static readonly TimeZoneInfo DhakaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Bangladesh Standard Time");

    /// <summary>
    /// Converts UTC DateTime to Dhaka timezone.
    /// Assumes input is in UTC (with Kind = Utc or unspecified UTC).
    /// </summary>
    private static DateTime ConvertToDhaka(DateTime utcTime)
    {
        var unspecifiedUtc = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTime(unspecifiedUtc, DhakaTimeZone);
    }

    /// <summary>
    /// Formats a UTC DateTime as a Dhaka date string.
    /// Format: "DD Mon" (e.g., "15 Sep")
    /// </summary>
    public string FormatDate(DateTime utcTime)
    {
        var dhakaTime = ConvertToDhaka(utcTime);
        return dhakaTime.ToString("dd MMM", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a UTC DateTime with time in Dhaka timezone.
    /// Format: "DD Mon · HH:mm" (e.g., "15 Sep · 14:30")
    /// </summary>
    public string FormatDateTime(DateTime utcTime)
    {
        var dhakaTime = ConvertToDhaka(utcTime);
        return dhakaTime.ToString("dd MMM · HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a UTC DateTime with full date and time.
    /// Format: "DD MMMM YYYY, HH:mm:ss" (e.g., "15 September 2026, 14:30:45")
    /// </summary>
    public string FormatFullDateTime(DateTime utcTime)
    {
        var dhakaTime = ConvertToDhaka(utcTime);
        return dhakaTime.ToString("dd MMMM yyyy, HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a UTC DateTime as ISO 8601 in Dhaka timezone (for logging/storage).
    /// Format: "YYYY-MM-DDTHH:mm:ss" (e.g., "2026-09-15T14:30:45")
    /// </summary>
    public string FormatIso(DateTime utcTime)
    {
        var dhakaTime = ConvertToDhaka(utcTime);
        return dhakaTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a UTC DateTime relative to now (e.g., "2 hours ago", "tomorrow at 14:30").
    /// Returns Dhaka timezone-aware relative time.
    /// </summary>
    public string FormatRelative(DateTime utcTime)
    {
        var dhakaTime = ConvertToDhaka(utcTime);
        var now = ConvertToDhaka(DateTime.UtcNow);
        var diff = now - dhakaTime;

        if (diff.TotalSeconds < 60)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays}d ago";

        return FormatDate(utcTime);
    }

    /// <summary>
    /// Gets the current date in Dhaka timezone.
    /// Useful for filtering by date ranges or month boundaries.
    /// </summary>
    public DateOnly GetTodayInDhaka()
    {
        var dhakaTime = ConvertToDhaka(DateTime.UtcNow);
        return DateOnly.FromDateTime(dhakaTime);
    }

    /// <summary>
    /// Gets the start of a Dhaka calendar month (UTC).
    /// Example: for September 2026, returns 2026-09-01T00:00:00 in Dhaka, converted back to UTC.
    /// </summary>
    public DateTime GetMonthStartUtc(int year, int month)
    {
        var dhakaDate = new DateTime(year, month, 1, 0, 0, 0);
        var utcDate = TimeZoneInfo.ConvertTime(dhakaDate, DhakaTimeZone, TimeZoneInfo.Utc);
        return utcDate;
    }

    /// <summary>
    /// Gets the end of a Dhaka calendar month (UTC).
    /// Example: for September 2026, returns 2026-09-30T23:59:59 in Dhaka, converted back to UTC.
    /// </summary>
    public DateTime GetMonthEndUtc(int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var dhakaDate = new DateTime(year, month, daysInMonth, 23, 59, 59);
        var utcDate = TimeZoneInfo.ConvertTime(dhakaDate, DhakaTimeZone, TimeZoneInfo.Utc);
        return utcDate;
    }
}
