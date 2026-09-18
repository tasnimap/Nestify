using Nestify.Shared.Dtos.Settlement;

namespace Nestify.Web.Services.Interfaces;

public interface ISettlementService
{
    Task<IReadOnlyList<SettlementBookDto>?> GetBooksAsync();
    Task<IReadOnlyList<MonthlyMemberMealCostDto>?> GetMealCostHistoryAsync();
    Task<SettlementWorkspaceDto?> GetAsync(int year, int month);
    Task<(bool Ok, string Message)> OpenBookAsync(int year, int month);
    Task<(bool Ok, string Message)> AddMemberAsync(int year, int month, long userId);
    Task<(SettlementBillDto? Data, string? Error)> AddBillAsync(int year, int month, CreateSettlementBillRequest request);
    Task<(bool Ok, string Message)> UpdateBillAsync(int year, int month, long billId, decimal amount);
    Task<(bool Ok, string Message)> DeleteBillAsync(int year, int month, long billId);
    Task<(SettlementPaymentDto? Data, string? Error)> AddPaymentAsync(int year, int month, CreateSettlementPaymentRequest request);
    Task<(bool Ok, string Message)> DeletePaymentAsync(int year, int month, long paymentId);
    Task<(bool Ok, string Message)> SaveMealsAsync(int year, int month, SaveSettlementMealsRequest request);
    Task<(bool Ok, string Message)> FinalizeAsync(int year, int month);
}
