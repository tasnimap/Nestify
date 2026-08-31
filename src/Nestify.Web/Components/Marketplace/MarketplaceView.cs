// src/Nestify.Web/Components/Marketplace/MarketplaceView.cs
// Presentation helpers shared by the M4 components and pages: enum labels,
// relative time, and the deterministic look of the generated image tiles.
using System.Globalization;
using Nestify.Shared.Dtos.Marketplace;

namespace Nestify.Web.Components.Marketplace;

public static class MarketplaceView
{
    public static string Label(MarketplaceCategory category) => category.ToString();

    public static string Label(ItemCondition condition) => condition switch
    {
        ItemCondition.New => "New",
        ItemCondition.LikeNew => "Like new",
        ItemCondition.Good => "Good",
        ItemCondition.Fair => "Fair",
        _ => condition.ToString()
    };

    public static string ConditionModifier(ItemCondition condition) => condition switch
    {
        ItemCondition.New => "new",
        ItemCondition.LikeNew => "likenew",
        ItemCondition.Good => "good",
        _ => "fair"
    };

    public static string Label(MarketplaceSort sort) => sort switch
    {
        MarketplaceSort.Newest => "Newest",
        MarketplaceSort.PriceLowToHigh => "Price: low to high",
        MarketplaceSort.PriceHighToLow => "Price: high to low",
        _ => sort.ToString()
    };

    public static string ShortLabel(MarketplaceSort sort) => sort switch
    {
        MarketplaceSort.Newest => "Newest",
        MarketplaceSort.PriceLowToHigh => "Price ↑",
        MarketplaceSort.PriceHighToLow => "Price ↓",
        _ => sort.ToString()
    };

    public static string Label(ListingStatus status) => status switch
    {
        ListingStatus.Active => "Active",
        ListingStatus.Sold => "Sold",
        ListingStatus.Removed => "Removed",
        _ => status.ToString()
    };

    public static string StatusModifier(ListingStatus status) => status switch
    {
        ListingStatus.Active => "status-active",
        ListingStatus.Sold => "status-sold",
        _ => "status-removed"
    };

    public static string Label(BuyInterestStatus status) => status switch
    {
        BuyInterestStatus.Pending => "Awaiting seller",
        BuyInterestStatus.Accepted => "Accepted",
        BuyInterestStatus.Declined => "Declined",
        BuyInterestStatus.Withdrawn => "Withdrawn",
        _ => status.ToString()
    };

    public static string InterestModifier(BuyInterestStatus status) => status switch
    {
        BuyInterestStatus.Pending => "pending",
        BuyInterestStatus.Accepted => "accepted",
        BuyInterestStatus.Declined => "declined",
        _ => "withdrawn"
    };

    /// <summary>Short uppercase tag shown on the corner of a generated tile.</summary>
    public static string CategoryTag(MarketplaceCategory category) => category switch
    {
        MarketplaceCategory.Furniture => "Furniture",
        MarketplaceCategory.Electronics => "Electronics",
        MarketplaceCategory.Appliances => "Appliance",
        MarketplaceCategory.Kitchen => "Kitchen",
        MarketplaceCategory.Books => "Books",
        MarketplaceCategory.Bedding => "Bedding",
        _ => "Other"
    };

    /// <summary>True when the string is a real image reference rather than a tile seed.</summary>
    public static bool IsPhoto(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith('/'));

    /// <summary>First letter of the title, uppercased. Falls back to N (Nestify).</summary>
    public static string Monogram(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "N";
        foreach (var ch in title)
        {
            if (char.IsLetter(ch)) return char.ToUpper(ch, CultureInfo.InvariantCulture).ToString();
            if (char.IsDigit(ch)) return ch.ToString();
        }
        return "N";
    }

    /// <summary>Initials for a person's display name, up to two letters.</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "?";
        var first = parts[0][0];
        var second = parts.Length > 1 ? parts[^1][0] : (parts[0].Length > 1 ? parts[0][1] : ' ');
        return (char.ToUpper(first) + (char.IsLetter(second) ? char.ToUpper(second).ToString() : "")).Trim();
    }

    /// <summary>Stable hue (0-359) derived from a seed string, so a tile always looks the same.</summary>
    public static int Hue(string? seed)
    {
        if (string.IsNullOrEmpty(seed)) return 265;
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in seed)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            // Bias toward the purple→blue→teal arc so tiles feel on-brand.
            return 200 + (int)(hash % 140);
        }
    }

    public static string RelativeTime(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta < TimeSpan.FromMinutes(1)) return "just now";
        if (delta < TimeSpan.FromHours(1)) return $"{(int)delta.TotalMinutes}m ago";
        if (delta < TimeSpan.FromDays(1)) return $"{(int)delta.TotalHours}h ago";
        if (delta < TimeSpan.FromDays(7)) return $"{(int)delta.TotalDays}d ago";
        if (delta < TimeSpan.FromDays(30)) return $"{(int)(delta.TotalDays / 7)}w ago";
        if (delta < TimeSpan.FromDays(365)) return $"{(int)(delta.TotalDays / 30)}mo ago";
        return $"{(int)(delta.TotalDays / 365)}y ago";
    }

    public static string MemberSince(DateTime utc)
    {
        var months = (int)Math.Max(1, Math.Round((DateTime.UtcNow - utc).TotalDays / 30));
        if (months < 12) return $"Member for {months} mo";
        var years = months / 12;
        return $"Member for {years} yr{(years > 1 ? "s" : "")}";
    }
}
