using Nestify.Shared.Dtos.Housing;

namespace Nestify.Web.Components.Housing;

public static class HousingView
{
    public static string Label(ListingType type) => type switch
    {
        ListingType.SingleSeat => "Single seat",
        ListingType.MultipleSeats => "Multiple seats",
        ListingType.EntireHouse => "Entire house",
        _ => type.ToString()
    };

    public static string ShortLabel(ListingType type) => type switch
    {
        ListingType.SingleSeat => "Seat",
        ListingType.MultipleSeats => "Seats",
        ListingType.EntireHouse => "Whole house",
        _ => type.ToString()
    };

    public static string Label(PostStatus status) => status switch
    {
        PostStatus.Active => "Active",
        PostStatus.Closed => "Closed",
        _ => status.ToString()
    };

    public static string StatusModifier(PostStatus status) =>
        status == PostStatus.Active ? "status-active" : "status-closed";

    /// <summary>
    /// One chip of text per non-null requirement. Rendered as plain text, never markup —
    /// this is descriptive copy about the post, not a client-side eligibility check.
    /// </summary>
    public static IReadOnlyList<string> EligibilityChips(EligibilityDto e)
    {
        var chips = new List<string>();

        if (e.Gender is { } gender)
        {
            chips.Add(gender == Gender.Male ? "Male only" : "Female only");
        }
        if (e.Occupation is { } occupation)
        {
            chips.Add(occupation switch
            {
                Occupation.Student => "Students only",
                Occupation.Working => "Working professionals only",
                _ => "Students or working professionals"
            });
        }
        if (e.MaritalStatus is { } marital)
        {
            chips.Add(marital == MaritalStatus.Single ? "Single applicants only" : "Married applicants only");
        }
        if (e.MinAge is { } minAge && e.MaxAge is { } maxAge)
        {
            chips.Add($"Age {minAge}–{maxAge}");
        }
        else if (e.MinAge is { } onlyMin)
        {
            chips.Add($"Age {onlyMin}+");
        }
        else if (e.MaxAge is { } onlyMax)
        {
            chips.Add($"Age up to {onlyMax}");
        }
        if (e.VerifiedOnly)
        {
            chips.Add("Verified accounts only");
        }
        if (e.StudentOnly)
        {
            chips.Add("Student ID required");
        }

        return chips;
    }

    public static string RelativeTime(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta < TimeSpan.FromMinutes(1)) return "just now";
        if (delta < TimeSpan.FromHours(1)) return $"{(int)delta.TotalMinutes}m ago";
        if (delta < TimeSpan.FromDays(1)) return $"{(int)delta.TotalHours}h ago";
        if (delta < TimeSpan.FromDays(7)) return $"{(int)delta.TotalDays}d ago";
        if (delta < TimeSpan.FromDays(30)) return $"{(int)(delta.TotalDays / 7)}w ago";
        return $"{(int)(delta.TotalDays / 30)}mo ago";
    }
}