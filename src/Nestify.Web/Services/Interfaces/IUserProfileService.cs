using Nestify.Shared.Dtos.Profile;

namespace Nestify.Web.Services.Interfaces;

public sealed record VerificationUpload(Stream Content, string FileName, string ContentType);

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

    /// <summary>The fee the bKash portal shows before the application is sent.</summary>
    Task<decimal> GetVerificationFeeAsync();

    /// <summary>The fake bKash payment. Throws with the server's message when the number or PIN is rejected.</summary>
    Task<VerificationPaymentDto> PayVerificationFeeAsync(string bkashNumber, string pin);

    /// <summary>
    /// Sends the identity document (NID or birth certificate) and, for students
    /// and job holders, the student or employee ID, together with the payment id.
    /// </summary>
    Task<UserProfileDto?> SubmitVerificationAsync(string identityDocumentType, VerificationUpload identity,
        VerificationUpload? occupation, string paymentId);

    Task<UserProfileDto?> CancelVerificationAsync();
}
