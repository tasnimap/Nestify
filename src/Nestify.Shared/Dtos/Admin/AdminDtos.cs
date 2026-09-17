namespace Nestify.Shared.Dtos.Admin;

// Contracts for the admin console (api/v1/admin, Admin.sql) and the
// verification queue (api/v1/admin/verifications).

public enum VerificationStatus { Pending, Approved, Rejected }

public sealed class VerificationRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;   // "User" or "Domestic Helper"
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentUrl { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public VerificationStatus Status { get; set; }
}

public enum ModerationScope { Housing = 1, Marketplace = 2 }

public enum ModeratedPostState { Live, Removed }

public enum ModerationReportState { Open, Resolved, Dismissed }

// Counts the shell badges and dashboard tiles read.
public sealed class AdminSummaryDto
{
    public int OpenHousingReports { get; set; }
    public int OpenMarketReports { get; set; }
    public int LiveHousingPosts { get; set; }
    public int LiveMarketItems { get; set; }
    public int PostsTakenDown { get; set; }
    public int ActiveAdmins { get; set; }
}

public sealed class AdminHousingPostDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public int Seats { get; set; }
    public string Area { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public bool OwnerVerified { get; set; }
    public string Eligibility { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public List<string> Photos { get; set; } = new();
    public DateTime PostedAtUtc { get; set; }
    public int ReportCount { get; set; }
    public ModeratedPostState State { get; set; }
    public string? RemovalReason { get; set; }

    public string FullArea => string.IsNullOrWhiteSpace(Area) ? Division : $"{Area}, {Division}";
    public string Cover => Photos.Count > 0 ? Photos[0] : string.Empty;
}

public sealed class AdminMarketItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Seller { get; set; } = string.Empty;
    public bool SellerVerified { get; set; }
    public string Area { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public List<string> Photos { get; set; } = new();
    public DateTime PostedAtUtc { get; set; }
    public int ReportCount { get; set; }
    public ModeratedPostState State { get; set; }
    public string? RemovalReason { get; set; }

    public string Cover => Photos.Count > 0 ? Photos[0] : string.Empty;
}

public sealed class AdminReportDto
{
    public string Id { get; set; } = string.Empty;
    public ModerationScope Scope { get; set; }
    public string PostId { get; set; } = string.Empty;
    public string PostTitle { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public DateTime RaisedAtUtc { get; set; }
    public ModerationReportState State { get; set; }
}

public sealed class TakedownRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public string? ReportId { get; set; }   // the report that led here, resolved together with the strike
}

public sealed class AdminFeeDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class SaveFeeDto
{
    public decimal Amount { get; set; }
}

public sealed class AdminPlanDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ModerationScope Scope { get; set; }
    public int Posts { get; set; }
    public decimal Price { get; set; }
    public int ValidDays { get; set; }
    public bool IsActive { get; set; } = true;
    public int Subscribers { get; set; }
}

public sealed class SavePlanDto
{
    public string Name { get; set; } = string.Empty;
    public ModerationScope Scope { get; set; }
    public int Posts { get; set; }
    public decimal Price { get; set; }
    public int ValidDays { get; set; }
}

public sealed class AdminAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastActiveUtc { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateAdminDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Scope { get; set; } = "Moderation";
    public string Password { get; set; } = string.Empty;
}

public sealed class AdminAuditEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Admin { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Kind { get; set; } = "neutral";
    public DateTime AtUtc { get; set; }
}
