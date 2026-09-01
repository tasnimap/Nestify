using Nestify.Shared.Dtos.Housing;

namespace Nestify.Web.Services.Interfaces;

public interface IHousingService
{
    Task<HousingPageDto<HousingPostSummaryDto>> BrowseAsync(HousingPostFilterDto filter);

    /// <summary>
    /// Returns null for a non-existent id AND for a post the viewer is not eligible for (§5.3) —
    /// the two cases are indistinguishable by design, so the detail page must render the same
    /// "not available" state for both. Never render a 403-style "you're not eligible" message.
    /// </summary>
    Task<HousingPostDetailDto?> GetPostAsync(string id);

    /// <summary>Creates a post under the given house. Returns the new post's id.</summary>
    Task<string> CreateAsync(CreateHousingPostRequestDto request);

    /// <summary>Owner-only. Returns null if the post doesn't exist or isn't the caller's.</summary>
    Task<HousingPostDetailDto?> GetPostForEditAsync(string id);

    /// <summary>Owner-only. Returns false if the post doesn't exist or isn't the caller's.</summary>
    Task<bool> UpdateAsync(string id, UpdateHousingPostRequestDto request);
}