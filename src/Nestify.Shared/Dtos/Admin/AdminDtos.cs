namespace Nestify.Shared.Dtos.Admin;

public sealed class AdminOverviewDto
{
    public int PendingVerifications { get; set; }
    public int OpenReports { get; set; }
    public int ActiveUsers { get; set; }
    public int BannedUsers { get; set; }
    public List<AdminActivityDto> RecentActivity { get; set; } = new();
}

public sealed class AdminActivityDto
{
    public string Action { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime WhenUtc { get; set; }
}

public enum VerificationStatus { Pending, Approved, Rejected }

public sealed class VerificationRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;   // "User" or "Domestic Helper"
    public string DocumentType { get; set; } = string.Empty;
    public DateTime SubmittedUtc { get; set; }
    public VerificationStatus Status { get; set; }
}

public enum ReportStatus { Open, Resolved, Dismissed }

public sealed class ModerationReportDto
{
    public string Id { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetLabel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public ReportStatus Status { get; set; }
}

public sealed class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;   // "User" / "Domestic Helper" / "Admin"
    public bool IsVerified { get; set; }
    public bool IsBanned { get; set; }
    public DateTime JoinedUtc { get; set; }
}

public sealed class AuditEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime OccurredUtc { get; set; }
}
