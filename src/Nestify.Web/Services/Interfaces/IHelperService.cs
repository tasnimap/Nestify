using Microsoft.AspNetCore.Components.Forms;
using Nestify.Shared.Dtos.Helpers;
using Nestify.Shared.Dtos.Profile;

namespace Nestify.Web.Services.Interfaces;

// Domestic help, backed by api/v1/helpers (Domestic_Help.sql). Calls that can
// fail with a message from the API throw ApplicationException with that text.
public interface IHelperService
{
    // ---- browsing and booking (the bachelor's side) ----
    Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(HelperFilterDto filter);
    Task<HelperDetailDto?> GetHelperAsync(string id);
    Task<HelperPageDto<ReviewDto>> GetReviewsAsync(string helperId, int page = 1, int pageSize = 5);
    Task<List<EngagementDto>> GetMyEngagementsAsync();
    Task<EngagementDto> RequestEngagementAsync(string helperId, EngagementRequestDto request);
    Task CancelRequestAsync(string engagementId);
    Task MarkCompleteAsync(string engagementId);
    Task SubmitReviewAsync(string engagementId, int rating, string comment);

    // ---- the helper's own profile ----
    Task<HelperProfileDto?> GetMyProfileAsync();
    Task<HelperNavDto?> GetNavAsync();
    Task<HelperProfileDto> UpdateProfileAsync(HelperProfileFormDto form);
    Task<HelperProfileDto> UploadPhotoAsync(IBrowserFile file);

    // ---- verification ----
    Task<HelperVerificationStatusDto?> GetVerificationStatusAsync();
    Task<decimal> GetVerificationFeeAsync();
    Task<VerificationPaymentDto> PayVerificationFeeAsync(string bkashNumber, string pin);
    Task SubmitVerificationAsync(IBrowserFile photo, IBrowserFile nid, string paymentId);
    Task CancelVerificationAsync();

    // ---- workspace ----
    Task<HelperWorkspaceDashboardDto?> GetWorkspaceDashboardAsync();
    Task<HelperAvailabilityDto?> GetAvailabilityAsync();
    Task SaveAvailabilityAsync(HelperAvailabilityDto availability);
    Task<HelperWorkspaceScheduleDto?> GetWorkspaceScheduleAsync(DateTime weekStart);
    Task<HelperWorkspaceEngagementsDto?> GetWorkspaceEngagementsAsync();
    Task AcceptWorkspaceEngagementAsync(string id);
    Task DeclineWorkspaceEngagementAsync(string id, string? reason);
    Task<HelperReviewsDto?> GetMyReviewsAsync();
    Task ReplyToReviewAsync(string reviewId, string reply);
}
