using Nestify.Shared.Dtos.Admin;

namespace Nestify.Web.Services.Interfaces;

public interface IAdminService
{
    Task<AdminOverviewDto> GetOverviewAsync();

    Task<List<VerificationRequestDto>> GetVerificationsAsync();
    Task DecideVerificationAsync(string id, bool approve);

    Task<List<ModerationReportDto>> GetReportsAsync();
    Task ResolveReportAsync(string id, bool actionTaken);

    Task<List<AdminUserDto>> GetUsersAsync();
    Task SetBanAsync(string id, bool banned);

    Task<List<AuditEntryDto>> GetAuditLogAsync();
}
