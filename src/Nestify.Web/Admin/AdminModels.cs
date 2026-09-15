namespace Nestify.Web.Admin;

public enum PostState { Live, Removed }

public enum ReportState { Open, Resolved, Dismissed }

public enum ApplicationState { Pending, Approved, Declined }

public enum PlanScope { Housing, Marketplace }

public enum ApplicantKind { User, DomesticHelper }

public sealed class HousingPost
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ListingType { get; set; } = "";
    public int Seats { get; set; } = 1;
    public string Area { get; set; } = "";
    public string District { get; set; } = "";
    public string Division { get; set; } = "";
    public string Owner { get; set; } = "";
    public bool OwnerVerified { get; set; }
    public string Eligibility { get; set; } = "";
    public int Rent { get; set; }
    public List<string> Photos { get; set; } = new();
    public DateTime PostedOn { get; set; }
    public int ReportCount { get; set; }
    public PostState State { get; set; } = PostState.Live;
    public string? RemovalReason { get; set; }

    public string FullArea => $"{Area}, {District}";

    public string Cover => Photos.Count > 0 ? Photos[0] : "";
}

public sealed class MarketItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Condition { get; set; } = "";
    public string Seller { get; set; } = "";
    public bool SellerVerified { get; set; }
    public string Area { get; set; } = "";
    public int Price { get; set; }
    public List<string> Photos { get; set; } = new();
    public DateTime PostedOn { get; set; }
    public int ReportCount { get; set; }
    public PostState State { get; set; } = PostState.Live;
    public string? RemovalReason { get; set; }

    public string Cover => Photos.Count > 0 ? Photos[0] : "";
}

public sealed class PostReport
{
    public string Id { get; set; } = "";
    public PlanScope Scope { get; set; }
    public string PostId { get; set; } = "";
    public string PostTitle { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Details { get; set; } = "";
    public string ReportedBy { get; set; } = "";
    public DateTime RaisedOn { get; set; }
    public ReportState State { get; set; } = ReportState.Open;
}

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

public sealed class FeeSetting
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public int Amount { get; set; }
}

public sealed class PostPlan
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public PlanScope Scope { get; set; }
    public int Posts { get; set; }
    public int Price { get; set; }
    public int ValidDays { get; set; }
    public bool IsActive { get; set; } = true;
    public int Subscribers { get; set; }
}

public sealed class AuditEntry
{
    public string Id { get; set; } = "";
    public string Admin { get; set; } = "";
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Note { get; set; }
    public string Kind { get; set; } = "neutral";
    public DateTime At { get; set; }
}

public sealed class AdminAccount
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public DateTime LastActive { get; set; }
    public bool IsActive { get; set; } = true;
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
    public int TotalAdmins { get; set; }
    public int ActiveThisMonth { get; set; }
    public int HomesFound { get; set; }
    public int HelpersHired { get; set; }
    public int ItemsSold { get; set; }
    public int ItemsListed { get; set; }
    public int FakePostsRemoved { get; set; }
    public int VerifiedAccounts { get; set; }
    public int LiveHousingPosts { get; set; }
    public int LiveMarketPosts { get; set; }
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
