using Nestify.Shared.Dtos.Admin;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

// In-memory admin data for the console preview. Swap for a real API client later.
public sealed class MockAdminService : IAdminService
{
    private readonly List<VerificationRequestDto> _verifications;
    private readonly List<ModerationReportDto> _reports;
    private readonly List<AdminUserDto> _users;
    private readonly List<AuditEntryDto> _audit;

    public MockAdminService()
    {
        var now = DateTime.UtcNow;

        _verifications = new()
        {
            new() { Id = "v-101", ApplicantName = "Rafiul Karim", SubjectType = "User", DocumentType = "National ID", SubmittedUtc = now.AddHours(-5), Status = VerificationStatus.Pending },
            new() { Id = "v-102", ApplicantName = "Shamima Akter", SubjectType = "Domestic Helper", DocumentType = "National ID", SubmittedUtc = now.AddHours(-9), Status = VerificationStatus.Pending },
            new() { Id = "v-103", ApplicantName = "Tanvir Ahmed", SubjectType = "User", DocumentType = "Student ID", SubmittedUtc = now.AddDays(-1), Status = VerificationStatus.Pending },
            new() { Id = "v-104", ApplicantName = "Rina Begum", SubjectType = "Domestic Helper", DocumentType = "Birth Certificate", SubmittedUtc = now.AddDays(-2), Status = VerificationStatus.Approved },
        };

        _reports = new()
        {
            new() { Id = "r-201", TargetType = "Marketplace item", TargetLabel = "IELTS book set — \"like new\"", Reason = "Misleading condition", ReportedBy = "Nadia H.", CreatedUtc = now.AddHours(-3), Status = ReportStatus.Open },
            new() { Id = "r-202", TargetType = "Housing post", TargetLabel = "Seat in Bakshi Bazar mess", Reason = "Suspected fraud", ReportedBy = "Fahim R.", CreatedUtc = now.AddHours(-20), Status = ReportStatus.Open },
            new() { Id = "r-203", TargetType = "Helper review", TargetLabel = "1-star review on Jasim Uddin", Reason = "Offensive language", ReportedBy = "Jasim U.", CreatedUtc = now.AddDays(-2), Status = ReportStatus.Resolved },
        };

        _users = new()
        {
            new() { Id = "u-1", FullName = "Rafiul Karim", Email = "rafiul@example.com", AccountType = "User", IsVerified = true, JoinedUtc = now.AddDays(-40) },
            new() { Id = "u-2", FullName = "Shamima Akter", Email = "shamima@example.com", AccountType = "Domestic Helper", IsVerified = false, JoinedUtc = now.AddDays(-12) },
            new() { Id = "u-3", FullName = "Tanvir Ahmed", Email = "tanvir@example.com", AccountType = "User", IsVerified = false, JoinedUtc = now.AddDays(-8) },
            new() { Id = "u-4", FullName = "Old Spammer", Email = "spam@example.com", AccountType = "User", IsVerified = false, IsBanned = true, JoinedUtc = now.AddDays(-90) },
            new() { Id = "u-5", FullName = "Rina Begum", Email = "rina@example.com", AccountType = "Domestic Helper", IsVerified = true, JoinedUtc = now.AddDays(-25) },
        };

        _audit = new()
        {
            new() { Id = "a-1", Actor = "admin1", Action = "Approved verification", Target = "Rina Begum", Note = "ID matched", OccurredUtc = now.AddDays(-2) },
            new() { Id = "a-2", Actor = "admin1", Action = "Banned user", Target = "Old Spammer", Note = "Repeated fake listings", OccurredUtc = now.AddDays(-3) },
            new() { Id = "a-3", Actor = "admin1", Action = "Dismissed report", Target = "r-203", Note = "No policy breach", OccurredUtc = now.AddDays(-2) },
        };
    }

    public Task<AdminOverviewDto> GetOverviewAsync() => Task.FromResult(new AdminOverviewDto
    {
        PendingVerifications = _verifications.Count(v => v.Status == VerificationStatus.Pending),
        OpenReports = _reports.Count(r => r.Status == ReportStatus.Open),
        ActiveUsers = _users.Count(u => !u.IsBanned),
        BannedUsers = _users.Count(u => u.IsBanned),
        RecentActivity = _audit
            .OrderByDescending(a => a.OccurredUtc)
            .Take(5)
            .Select(a => new AdminActivityDto { Action = a.Action, Detail = a.Target, WhenUtc = a.OccurredUtc })
            .ToList()
    });

    public Task<List<VerificationRequestDto>> GetVerificationsAsync() =>
        Task.FromResult(_verifications.OrderBy(v => v.Status).ThenBy(v => v.SubmittedUtc).ToList());

    public Task DecideVerificationAsync(string id, bool approve)
    {
        var v = _verifications.FirstOrDefault(x => x.Id == id);
        if (v is not null)
        {
            v.Status = approve ? VerificationStatus.Approved : VerificationStatus.Rejected;
            _audit.Insert(0, new AuditEntryDto
            {
                Id = Guid.NewGuid().ToString(),
                Actor = "admin1",
                Action = approve ? "Approved verification" : "Rejected verification",
                Target = v.ApplicantName,
                OccurredUtc = DateTime.UtcNow
            });
        }
        return Task.CompletedTask;
    }

    public Task<List<ModerationReportDto>> GetReportsAsync() =>
        Task.FromResult(_reports.OrderBy(r => r.Status).ThenByDescending(r => r.CreatedUtc).ToList());

    public Task ResolveReportAsync(string id, bool actionTaken)
    {
        var r = _reports.FirstOrDefault(x => x.Id == id);
        if (r is not null)
        {
            r.Status = actionTaken ? ReportStatus.Resolved : ReportStatus.Dismissed;
            _audit.Insert(0, new AuditEntryDto
            {
                Id = Guid.NewGuid().ToString(),
                Actor = "admin1",
                Action = actionTaken ? "Resolved report" : "Dismissed report",
                Target = r.Id,
                OccurredUtc = DateTime.UtcNow
            });
        }
        return Task.CompletedTask;
    }

    public Task<List<AdminUserDto>> GetUsersAsync() =>
        Task.FromResult(_users.OrderByDescending(u => u.JoinedUtc).ToList());

    public Task SetBanAsync(string id, bool banned)
    {
        var u = _users.FirstOrDefault(x => x.Id == id);
        if (u is not null)
        {
            u.IsBanned = banned;
            _audit.Insert(0, new AuditEntryDto
            {
                Id = Guid.NewGuid().ToString(),
                Actor = "admin1",
                Action = banned ? "Banned user" : "Unbanned user",
                Target = u.FullName,
                OccurredUtc = DateTime.UtcNow
            });
        }
        return Task.CompletedTask;
    }

    public Task<List<AuditEntryDto>> GetAuditLogAsync() =>
        Task.FromResult(_audit.OrderByDescending(a => a.OccurredUtc).ToList());
}
