// src/Nestify.Web/Components/Helper/HelperScheduleView.cs
// Builds the 7 day x 18 hour availability board shown on a helper's profile.
namespace Nestify.Web.Components.Helper;

public enum SlotState
{
    /// <summary>Inside the helper's working window and free to book.</summary>
    Free,

    /// <summary>Inside the working window but already taken by another client.</summary>
    Booked,

    /// <summary>Outside the hours the helper works at all.</summary>
    Off
}

public sealed record ScheduleSlot(int Hour, SlotState State);

public sealed record ScheduleDay(DateTime Date, IReadOnlyList<ScheduleSlot> Slots);

public static class HelperScheduleView
{
    // The board runs 6:00 AM to 12:00 AM — eighteen one hour slots per day.
    public const int StartHour = 6;
    public const int EndHour = 24;
    public const int SlotsPerDay = EndHour - StartHour;
    public const int Days = 7;

    private static readonly string[] DayNames =
    {
        "sat", "sun", "mon", "tue", "wed", "thu", "fri"
    };

    /// <summary>
    /// The frontend phase has no booking table yet, so the board is derived from the
    /// helper's availability window and a stable hash of the helper id. The same helper
    /// always shows the same week; swap this for the API once bookings are stored.
    /// </summary>
    public static IReadOnlyList<ScheduleDay> Build(string helperId, string? availabilityWindow, DateTime startDate)
    {
        var workingDays = ParseDays(availabilityWindow);
        var (openHour, closeHour) = ParseHours(availabilityWindow);

        var days = new List<ScheduleDay>(Days);

        for (var d = 0; d < Days; d++)
        {
            var date = startDate.Date.AddDays(d);
            var works = workingDays.Contains(WeekIndex(date.DayOfWeek));
            var slots = new List<ScheduleSlot>(SlotsPerDay);

            for (var hour = StartHour; hour < EndHour; hour++)
            {
                var open = works && hour >= openHour && hour < closeHour;
                var state = !open
                    ? SlotState.Off
                    : IsBooked(helperId, date, hour) ? SlotState.Booked : SlotState.Free;

                slots.Add(new ScheduleSlot(hour, state));
            }

            days.Add(new ScheduleDay(date, slots));
        }

        return days;
    }

    public static string HourLabel(int hour) => hour switch
    {
        0 or 24 => "12 AM",
        12 => "12 PM",
        < 12 => $"{hour} AM",
        _ => $"{hour - 12} PM"
    };

    /// <summary>"9:00 PM - 10:00 PM" style label for a slot's tooltip.</summary>
    public static string RangeLabel(int hour) => $"{HourLabel(hour)} - {HourLabel(hour + 1)}";

    public static string StateLabel(SlotState state) => state switch
    {
        SlotState.Free => "Available",
        SlotState.Booked => "Booked",
        _ => "Not available"
    };

    // The week is listed the way it is read here: Saturday first.
    private static int WeekIndex(DayOfWeek day) => day switch
    {
        DayOfWeek.Saturday => 0,
        DayOfWeek.Sunday => 1,
        DayOfWeek.Monday => 2,
        DayOfWeek.Tuesday => 3,
        DayOfWeek.Wednesday => 4,
        DayOfWeek.Thursday => 5,
        _ => 6
    };

    // "Sat-Thu, 8am-2pm" -> Saturday through Thursday, wrapping around the week.
    private static HashSet<int> ParseDays(string? window)
    {
        var all = new HashSet<int> { 0, 1, 2, 3, 4, 5, 6 };
        if (string.IsNullOrWhiteSpace(window))
        {
            return all;
        }

        var text = window.ToLowerInvariant();
        var dash = text.IndexOf('-');
        if (dash <= 0)
        {
            return all;
        }

        var from = FindDay(text[..dash]);
        var to = FindDay(text[(dash + 1)..]);
        if (from < 0 || to < 0)
        {
            return all;
        }

        var days = new HashSet<int>();
        var i = from;
        while (true)
        {
            days.Add(i);
            if (i == to) break;
            i = (i + 1) % 7;
        }

        return days;
    }

    private static int FindDay(string text)
    {
        for (var i = 0; i < DayNames.Length; i++)
        {
            if (text.Contains(DayNames[i], StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    // Reads either an explicit "8am-2pm" or one of the loose words the profiles use.
    private static (int Open, int Close) ParseHours(string? window)
    {
        if (string.IsNullOrWhiteSpace(window))
        {
            return (8, 18);
        }

        var text = window.ToLowerInvariant();
        var comma = text.IndexOf(',');
        var timePart = comma >= 0 ? text[(comma + 1)..] : text;

        var hours = ReadHourPair(timePart);
        if (hours is { } pair)
        {
            return pair;
        }

        if (timePart.Contains("full day")) return (StartHour, EndHour);
        if (timePart.Contains("morning")) return (6, 12);
        if (timePart.Contains("afternoon")) return (12, 17);
        if (timePart.Contains("evening")) return (17, 22);
        if (timePart.Contains("night")) return (20, EndHour);

        return (8, 18);
    }

    private static (int Open, int Close)? ReadHourPair(string text)
    {
        var parts = text.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var open = ReadHour(parts[0]);
        var close = ReadHour(parts[1]);
        if (open is null || close is null || close <= open)
        {
            return null;
        }

        return (open.Value, close.Value);
    }

    private static int? ReadHour(string text)
    {
        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length == 0 || !int.TryParse(digits, out var hour) || hour > 12)
        {
            return null;
        }

        var pm = text.Contains("pm", StringComparison.Ordinal);
        if (pm && hour < 12) hour += 12;
        if (!pm && hour == 12) hour = 0;

        return hour;
    }

    // Same helper, same day, same hour -> same answer on every render.
    private static bool IsBooked(string helperId, DateTime date, int hour)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in helperId)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            hash ^= (uint)date.DayOfYear;
            hash *= 16777619;
            hash ^= (uint)hour;
            hash *= 16777619;

            return hash % 100 < 32;
        }
    }
}
