using Nestify.Shared.Dtos.Profile;

namespace Nestify.Web.Services.Interfaces;

public interface IUserProfileService
{
    /// <summary>The profile loaded so far, so the navbar can show the picture without a second call.</summary>
    UserProfileDto? Current { get; }

    /// <summary>Raised whenever Current changes, so the navbar can redraw.</summary>
    event Action? Changed;

    /// <summary>Drops the cached profile on logout.</summary>
    void Clear();

    /// <summary>
    /// Loads the signed-in user's profile. The API creates the row with the default
    /// picture if this is the first visit.
    /// </summary>
    Task<UserProfileDto?> GetMyProfileAsync();

    Task<UserProfileDto?> UpdateMyProfileAsync(UpdateUserProfileDto dto);

    /// <summary>
    /// Sends a picture picked from the device to the API, which uploads it to
    /// Cloudinary and stores the link it gets back.
    /// </summary>
    Task<UserProfileDto?> UploadPictureAsync(Stream content, string fileName, string contentType);

    Task<UserProfileDto?> SubmitVerificationAsync(string documentType, Stream content, string fileName, string contentType);
    Task<UserProfileDto?> CancelVerificationAsync();
}
