namespace Nestify.Web.Admin;

public enum ApplicationState { Pending, Approved, Declined }

public enum ApplicantKind { User, DomesticHelper }

public sealed class VerificationApplication
{
    public string Id { get; set; } = "";
    public string Applicant { get; set; } = "";
    public ApplicantKind Kind { get; set; }
    public string DocumentType { get; set; } = "";
    public string DocumentNumber { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Area { get; set; } = "";
    public int FeePaid { get; set; }
    public DateTime SubmittedOn { get; set; }
    public ApplicationState State { get; set; } = ApplicationState.Pending;
    public string? Decision { get; set; }
}

public sealed class RevenuePoint
{
    public string Month { get; set; } = "";
    public int Housing { get; set; }
    public int Marketplace { get; set; }
    public int Verification { get; set; }
    public int Total => Housing + Marketplace + Verification;
}

public sealed class GrowthPoint
{
    public string Month { get; set; } = "";
    public int Users { get; set; }
    public int Helpers { get; set; }
}

public sealed class PlatformStats
{
    public int TotalUsers { get; set; }
    public int TotalHelpers { get; set; }
    public int ActiveThisMonth { get; set; }
    public int HomesFound { get; set; }
    public int HelpersHired { get; set; }
    public int ItemsSold { get; set; }
    public int ItemsListed { get; set; }
    public int VerifiedAccounts { get; set; }
}

public sealed class ChartSeries
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6d28d9";
    public IReadOnlyList<int> Values { get; set; } = Array.Empty<int>();
}

public sealed class DonutSlice
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public string Color { get; set; } = "#6d28d9";
}
