using Nestify.Shared.Dtos.Admin;

namespace Nestify.Web.Services.Interfaces;

// The admin console pages: moderation, fees and plans, admin accounts, audit log.
public interface IAdminService
{
    Task<AdminSummaryDto> GetSummaryAsync();

    Task<List<AdminHousingPostDto>> GetHousingPostsAsync();
    Task<List<AdminReportDto>> GetHousingReportsAsync();
    Task<(bool Ok, string Message)> TakeDownHousingPostAsync(string id, string reason, string? reportId);
    Task<(bool Ok, string Message)> RestoreHousingPostAsync(string id);

    Task<List<AdminMarketItemDto>> GetMarketItemsAsync();
    Task<List<AdminReportDto>> GetMarketReportsAsync();
    Task<(bool Ok, string Message)> TakeDownMarketItemAsync(string id, string reason, string? reportId);
    Task<(bool Ok, string Message)> RestoreMarketItemAsync(string id);

    Task<bool> DismissReportAsync(ModerationScope scope, string id);

    Task<List<AdminFeeDto>> GetFeesAsync();
    Task<(bool Ok, string Message)> SaveFeeAsync(string code, decimal amount);

    Task<List<AdminPlanDto>> GetPlansAsync();
    Task<(bool Ok, string Message)> CreatePlanAsync(SavePlanDto dto);
    Task<(bool Ok, string Message)> UpdatePlanAsync(string id, SavePlanDto dto);
    Task<bool> TogglePlanAsync(string id);
    Task<(bool Ok, string Message)> DeletePlanAsync(string id);

    Task<List<AdminAccountDto>> GetAdminsAsync();
    Task<(bool Ok, string Message)> CreateAdminAsync(CreateAdminDto dto);
    Task<(bool Ok, string Message)> ToggleAdminAsync(string id);

    Task<List<AdminAuditEntryDto>> GetAuditAsync(int take = 200);

    Task<List<VerificationRequestDto>> GetVerificationsAsync();
    Task<(bool Ok, string Message)> DecideVerificationAsync(string id, bool approve, string? reason);
}
