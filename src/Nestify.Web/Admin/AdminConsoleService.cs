using Nestify.Shared.Dtos.Admin;

namespace Nestify.Web.Admin;

// What is left of the front-end only console state: the verification queue
// and the dashboard's revenue and growth charts, still sample data until their
// API exists. Moderation, fees, plans, admin accounts and the audit log come
// from IAdminService.
public sealed class AdminConsoleService
{
    private readonly List<VerificationApplication> _applications = new();

    private int _counter = 1000;

    public AdminConsoleService()
    {
        SeedApplications();
    }

    // Set by the admin shell once the signed-in name is known.
    public string CurrentAdmin { get; set; } = "Admin";

    public event Action? Changed;

    public IReadOnlyList<VerificationApplication> Applications => _applications;

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

    public int PendingApplications => _applications.Count(a => a.State == ApplicationState.Pending);

    // ---------- verification ----------

    public void DecideApplication(string id, bool approve, string note)
    {
        var application = _applications.FirstOrDefault(a => a.Id == id);
        if (application is null) return;

        application.State = approve ? ApplicationState.Approved : ApplicationState.Declined;
        application.Decision = note;
        Changed?.Invoke();
    }

    // ---------- helpers ----------

    public static string KindLabel(ApplicantKind kind) =>
        kind == ApplicantKind.DomesticHelper ? "Domestic helper" : "User";

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

    private string NextId(string prefix) => $"{prefix}-{++_counter}";

    // ---------- sample data ----------

    private void SeedApplications()
    {
        _applications.AddRange(new[]
        {
            New("Ruhul Amin", ApplicantKind.User, "NID", "1994738201", "01711-224466", "Dhanmondi, Dhaka", 100, 2),
            New("Shefali Begum", ApplicantKind.DomesticHelper, "NID", "1988420113", "01822-119933", "Mirpur 12, Dhaka", 150, 5),
            New("Tahmid Rahman", ApplicantKind.User, "Student ID", "CSE-19-0418", "01933-887711", "Palashi, Dhaka", 100, 9),
            New("Morjina Khatun", ApplicantKind.DomesticHelper, "Birth certificate", "20019134772", "01644-556677", "Badda, Dhaka", 150, 14),
            New("Sazzad Hossain", ApplicantKind.User, "Passport", "BX0442719", "01577-334455", "Agrabad, Chattogram", 100, 20),
            New("Rahima Akter", ApplicantKind.DomesticHelper, "NID", "1979330085", "01399-778822", "Uttara, Dhaka", 150, 27)
        });

        VerificationApplication New(string name, ApplicantKind kind, string docType, string docNo, string phone, string area, int fee, int hoursAgo) => new()
        {
            Id = NextId("VER"),
            Applicant = name,
            Kind = kind,
            DocumentType = docType,
            DocumentNumber = docNo,
            Phone = phone,
            Area = area,
            FeePaid = fee,
            SubmittedOn = DateTime.Now.AddHours(-hoursAgo)
        };
    }
}
