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

    [HttpPost("me/verification")]
    [RequestSizeLimit(MaxPictureBytes + 1024)]
    public async Task<ActionResult<UserProfileDto>> SubmitVerification([FromForm] string documentType, IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Choose a document photo first." });
        if (file.Length > MaxPictureBytes) return BadRequest(new { message = "The document photo must be 5 MB or smaller." });
        if (file.ContentType is null || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Upload a clear image of your document." });
        using var stream = file.OpenReadStream();
        var (url, uploadError) = await _uploader.UploadAsync(stream, file.FileName, file.ContentType);
        if (url is null) return BadRequest(new { message = uploadError });
        var error = await _verifications.SubmitAsync(RequireUserId(), documentType, url, file.FileName);
        if (error is not null) return BadRequest(new { message = error });
        return Ok(await _profiles.GetAsync(RequireUserId()));
    }

    [HttpDelete("me/verification")]
    public async Task<ActionResult<UserProfileDto>> CancelVerification()
    {
        var error = await _verifications.CancelAsync(RequireUserId());
        if (error is not null) return BadRequest(new { message = error });
        return Ok(await _profiles.GetAsync(RequireUserId()));
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
