using Nestify.Shared.Dtos.Area;

namespace Nestify.Web.Services.Interfaces;

public interface IAreaService
{
    Task<IReadOnlyList<DivisionDto>> GetDivisionsAsync();
    Task<IReadOnlyList<DistrictDto>> GetDistrictsAsync(int divisionId);
    Task<IReadOnlyList<UpazilaDto>> GetUpazilasAsync(int districtId);
}