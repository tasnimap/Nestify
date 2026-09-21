using Nestify.Shared.Dtos.Admin;

namespace Nestify.Web.Admin;

// What is left of the front-end only console state: the dashboard's revenue
// and growth charts, still sample data until their API exists. Moderation,
// verification, fees, plans, admin accounts and the audit log come from
// IAdminService.
public sealed class AdminConsoleService
{
    // Set by the admin shell once the signed-in name is known.
    public string CurrentAdmin { get; set; } = "Admin";

    public IReadOnlyList<RevenuePoint> Revenue { get; } = new List<RevenuePoint>
    {
        new() { Month = "Oct", Housing = 18400, Marketplace = 9200,  Verification = 6300 },
        new() { Month = "Nov", Housing = 21100, Marketplace = 10400, Verification = 7100 },
        new() { Month = "Dec", Housing = 19600, Marketplace = 12800, Verification = 6800 },
        new() { Month = "Jan", Housing = 26300, Marketplace = 14100, Verification = 9400 },
        new() { Month = "Feb", Housing = 24800, Marketplace = 13500, Verification = 8700 },
        new() { Month = "Mar", Housing = 29700, Marketplace = 15900, Verification = 10200 },
        new() { Month = "Apr", Housing = 33200, Marketplace = 17300, Verification = 11800 },
        new() { Month = "May", Housing = 31500, Marketplace = 16400, Verification = 11100 },
        new() { Month = "Jun", Housing = 37900, Marketplace = 19800, Verification = 13600 },
        new() { Month = "Jul", Housing = 41300, Marketplace = 21200, Verification = 14900 },
        new() { Month = "Aug", Housing = 39600, Marketplace = 22700, Verification = 14100 },
        new() { Month = "Sep", Housing = 46800, Marketplace = 25400, Verification = 16700 }
    };

    public IReadOnlyList<GrowthPoint> Growth { get; } = new List<GrowthPoint>
    {
        new() { Month = "Apr", Users = 1180, Helpers = 190 },
        new() { Month = "May", Users = 1420, Helpers = 232 },
        new() { Month = "Jun", Users = 1755, Helpers = 281 },
        new() { Month = "Jul", Users = 2090, Helpers = 336 },
        new() { Month = "Aug", Users = 2468, Helpers = 392 },
        new() { Month = "Sep", Users = 2914, Helpers = 451 }
    };

    public PlatformStats Stats { get; } = new()
    {
        TotalUsers = 2914,
        TotalHelpers = 451,
        ActiveThisMonth = 1863,
        HomesFound = 612,
        HelpersHired = 287,
        ItemsSold = 1044,
        ItemsListed = 1732,
        VerifiedAccounts = 1208
    };

    public int MonthRevenue => Revenue[^1].Total;

    public int LastMonthRevenue => Revenue[^2].Total;

    public int YearRevenue => Revenue.Sum(r => r.Total);

    // ---------- helpers ----------

    public static string ScopeLabel(ModerationScope scope) =>
        scope == ModerationScope.Housing ? "Housing" : "Marketplace";

    public static string Taka(decimal amount) => "৳" + amount.ToString("N0");

    public static string Ago(DateTime when)
    {
        var local = when.Kind == DateTimeKind.Utc ? when.ToLocalTime() : when;
        var span = DateTime.Now - local;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} d ago";
        return local.ToString("d MMM yyyy");
    }

    public static string Ago(DateTime? when) => when is null ? "never" : Ago(when.Value);
}
