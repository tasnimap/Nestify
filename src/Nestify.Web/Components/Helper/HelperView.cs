// src/Nestify.Web/Components/Helper/HelperView.cs
// Presentation helpers for the M2 domestic help pages.
namespace Nestify.Web.Components.Helper;

public static class HelperView
{
    // Placeholder portraits for the frontend phase. randomuser.me serves the same
    // headshot for a given number, and the women/men folders keep the picture
    // matching the name, so a khala never shows up with a man's photo. Helpers
    // upload their own photo in a later phase.
    private const int PortraitCount = 90;

    // Bengali names carry the gender in the last part far more often than in the
    // first, so both are checked. Anything unrecognised is treated as a woman:
    // almost every khala or bua on the platform is one.
    private static readonly string[] MaleParts =
    {
        "uddin", "uddun", "mia", "miah", "hossain", "hosen", "rahman", "islam", "ali",
        "khan", "sheikh", "molla", "sarker", "jasim", "kamal", "jamal", "sujon", "rakib",
        "babul", "shohag", "ripon", "abdul", "mohammad", "md", "ishmam", "tanvir", "fahim"
    };

    private static readonly string[] FemaleParts =
    {
        "begum", "akter", "akhter", "khatun", "bibi", "banu", "sultana", "nasrin",
        "parvin", "rina", "momena", "rahima", "shirin", "shamima", "fatema", "salma",
        "ayesha", "jharna", "nadia", "prapty", "tasnim"
    };

    /// <summary>A stable placeholder portrait for a helper, matched to the name's gender.</summary>
    public static string PhotoUrl(string? name)
    {
        var folder = IsMale(name) ? "men" : "women";
        return $"https://randomuser.me/api/portraits/{folder}/{Slot(name)}.jpg";
    }

    private static bool IsMale(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var parts = name.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);

        // A woman's name part wins over a man's: "Momena Ali" is a woman.
        foreach (var part in parts)
        {
            if (Array.IndexOf(FemaleParts, part) >= 0)
            {
                return false;
            }
        }

        foreach (var part in parts)
        {
            if (Array.IndexOf(MaleParts, part) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Same name, same portrait, every time.</summary>
    private static int Slot(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 1;
        }

        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in name)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            return (int)(hash % PortraitCount);
        }
    }
}
