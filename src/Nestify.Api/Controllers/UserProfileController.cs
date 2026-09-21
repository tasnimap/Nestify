using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Profiles;
using Nestify.Shared.Dtos.Profile;

namespace Nestify.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
[Authorize]
public sealed class UserProfileController : ControllerBase
{
    // Cloudinary is free-tier, so keep uploads small.
    private const long MaxPictureBytes = 5 * 1024 * 1024;

    private readonly UserProfileService _profiles;
    private readonly CloudinaryUploader _uploader;
    private readonly VerificationService _verifications;

    public UserProfileController(UserProfileService profiles, CloudinaryUploader uploader, VerificationService verifications)
    {
        _profiles = profiles;
        _uploader = uploader;
        _verifications = verifications;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetMyProfile()
    {
        var profile = await _profiles.GetAsync(RequireUserId());
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserProfileDto>> UpdateMyProfile(UpdateUserProfileDto dto)
    {
        var (data, error) = await _profiles.UpdateAsync(RequireUserId(), dto);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("me/picture")]
    [RequestSizeLimit(MaxPictureBytes + 1024)]
    public async Task<ActionResult<UserProfileDto>> UploadPicture(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a picture first." });
        }

        if (file.Length > MaxPictureBytes)
        {
            return BadRequest(new { message = "The picture must be 5 MB or smaller." });
        }

        if (file.ContentType is null || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only image files can be used as a profile picture." });
        }

        using var stream = file.OpenReadStream();
        var (url, error) = await _uploader.UploadAsync(stream, file.FileName, file.ContentType);
        if (url is null)
        {
            return BadRequest(new { message = error });
        }

        var profile = await _profiles.SetPictureAsync(RequireUserId(), url);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("me/verification/fee")]
    public async Task<ActionResult<VerificationFeeDto>> GetVerificationFee()
        => Ok(new VerificationFeeDto { AmountBdt = await _verifications.GetUserFeeAsync() });

    // The fake bKash portal: any Bangladeshi number and any 4-5 digit PIN pay the fee.
    [HttpPost("me/verification/payment")]
    public async Task<ActionResult<VerificationPaymentDto>> PayVerificationFee(BkashPaymentDto dto)
    {
        var (data, error) = await _verifications.PayUserFeeAsync(RequireUserId(), dto);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    // identityFile is the NID or birth certificate; occupationFile is the
    // student or employee ID and is only sent when the profile needs one.
    [HttpPost("me/verification")]
    [RequestSizeLimit(2 * MaxPictureBytes + 1024)]
    public async Task<ActionResult<UserProfileDto>> SubmitVerification(
        [FromForm] string identityDocumentType, [FromForm] long paymentId, IFormFile identityFile, IFormFile? occupationFile)
    {
        if (!IsUsableImage(identityFile, out var identityError)) return BadRequest(new { message = identityError });
        if (occupationFile is not null && !IsUsableImage(occupationFile, out var occupationError)) return BadRequest(new { message = occupationError });

        await using var identityStream = identityFile.OpenReadStream();
        await using var occupationStream = occupationFile?.OpenReadStream();

        var error = await _verifications.SubmitUserAsync(RequireUserId(), identityDocumentType,
            new VerificationService.UploadedFile { Content = identityStream, FileName = identityFile.FileName, ContentType = identityFile.ContentType },
            occupationFile is null ? null : new VerificationService.UploadedFile { Content = occupationStream!, FileName = occupationFile.FileName, ContentType = occupationFile.ContentType },
            paymentId);
        if (error is not null) return BadRequest(new { message = error });
        return Ok(await _profiles.GetAsync(RequireUserId()));
    }

    [HttpDelete("me/verification")]
    public async Task<ActionResult<UserProfileDto>> CancelVerification()
    {
        var error = await _verifications.CancelUserAsync(RequireUserId());
        if (error is not null) return BadRequest(new { message = error });
        return Ok(await _profiles.GetAsync(RequireUserId()));
    }

    private static bool IsUsableImage(IFormFile? file, out string? error)
    {
        error = null;
        if (file is null || file.Length == 0) error = "Choose a document photo first.";
        else if (file.Length > MaxPictureBytes) error = "Each document photo must be 5 MB or smaller.";
        else if (file.ContentType is null || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) error = "Upload a clear image of your document.";
        return error is null;
    }

    private long RequireUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
