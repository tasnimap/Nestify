// src/Nestify.Web/Services/Interfaces/IHousingService.cs
using Nestify.Shared.Dtos.Housing;

namespace Nestify.Web.Services.Interfaces;

/// <summary>
/// The contract M1's pages depend on. Mirrors §3.6 of the implementation plan —
/// same operations, same DTOs, same return shapes — so the mock and the real
/// HttpClient implementation are swappable with one line in Program.cs.
///
/// F1 scope only (Browse + Detail). Create/edit/mine/bookings are added in F2/F3 —
/// do not add them here ahead of those pages landing.
/// </summary>
public interface IHousingService
{
    Task<HousingPageDto<HousingPostSummaryDto>> BrowseAsync(HousingPostFilterDto filter);

    /// <summary>
    /// Returns null for a non-existent id AND for a post the viewer is not eligible for (§5.3) —
    /// the two cases are indistinguishable by design, so the detail page must render the same
    /// "not available" state for both. Never render a 403-style "you're not eligible" message.
    /// </summary>
    Task<HousingPostDetailDto?> GetPostAsync(string id);
}