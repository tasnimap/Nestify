// TEMPORARY — stands in for Obonti's houses/mine (M3) until that lands on main.
// Backs only the house selector on /housing/new. Delete this file, its Mock
// implementation, and HouseOptionDto once the real IHouseService/HouseSummaryDto
// ships, and point the selector at that instead.
using Nestify.Shared.Dtos.Housing;

namespace Nestify.Web.Services.Interfaces;

public interface IHouseLookupService
{
    /// <summary>Houses the current user can post under, i.e. where they are Manager or CoManager.</summary>
    Task<IReadOnlyList<HouseOptionDto>> GetManageableHousesAsync();
}