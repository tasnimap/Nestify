using Microsoft.AspNetCore.Components.Forms;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Web.Services.Interfaces;

public interface IHelperService
{
    Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(
        HelperFilterDto filter);

    Task<HelperDetailDto?> GetHelperAsync(string id);

    Task<HelperDetailDto?> GetMyProfileAsync();

    Task<HelperDetailDto> RegisterAsync(
        HelperRegistrationDto dto);

    Task<HelperDetailDto> UpdateProfileAsync(
        HelperRegistrationDto dto);

    Task<HelperPageDto<ReviewDto>> GetReviewsAsync(
        string helperId,
        int page = 1,
        int pageSize = 5);

    Task<List<EngagementDto>> GetMyEngagementsAsync();

    /// <summary>
    /// Slots are the hours selected on the availability board.
    /// </summary>
    Task<EngagementDto> RequestEngagementAsync(
        string helperId,
        IReadOnlyList<EngagementSlotDto>? slots = null);

    Task<EngagementDto> ConfirmEngagementAsync(
        string engagementId);

    Task<EngagementDto> MarkCompleteAsync(
        string engagementId);

    Task SubmitReviewAsync(
        string engagementId,
        int rating,
        string comment);

    Task SubmitVerificationAsync(
        string documentType,
        IBrowserFile file);
}
