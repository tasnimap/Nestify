using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Profiles;
using Nestify.Shared.Dtos.Helpers;
using Nestify.Shared.Dtos.Profile;

namespace Nestify.Api.Controllers;

// Helper verification: pay the fee through the fake bKash portal, then send a
// photo of herself and her NID for an admin to check.
[ApiController]
[Route("api/v1/helpers/me/verification")]
[Authorize(Roles = "DomesticHelper")]
public sealed class VerificationController : ControllerBase
{
    private const long MaxDocumentBytes = 10 * 1024 * 1024;

    private readonly VerificationService _verifications;

    public VerificationController(VerificationService verifications)
    {
        _verifications = verifications;
    }

    [HttpGet]
    public async Task<ActionResult<HelperVerificationStatusDto>> GetStatus()
        => Ok(await _verifications.GetHelperStatusAsync(RequireUserId()));

    [HttpGet("fee")]
    public async Task<ActionResult<VerificationFeeDto>> GetFee()
        => Ok(new VerificationFeeDto { AmountBdt = await _verifications.GetHelperFeeAsync(RequireUserId()) });

    [HttpPost("payment")]
    public async Task<ActionResult<VerificationPaymentDto>> Pay(BkashPaymentDto dto)
    {
        var (data, error) = await _verifications.PayHelperFeeAsync(RequireUserId(), dto);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost]
    [RequestSizeLimit(MaxDocumentBytes * 2 + 1024)]
    public async Task<IActionResult> Submit([FromForm] string paymentId, IFormFile photoFile, IFormFile nidFile)
    {
        if (!long.TryParse(paymentId, out var paymentIdValue))
        {
            return BadRequest(new { message = "Pay the verification fee first." });
        }

        var problem = CheckImage(photoFile, "a clear photo of yourself") ?? CheckImage(nidFile, "a clear photo of your NID");
        if (problem is not null)
        {
            return BadRequest(new { message = problem });
        }

        await using var photoStream = photoFile.OpenReadStream();
        await using var nidStream = nidFile.OpenReadStream();

        var error = await _verifications.SubmitHelperAsync(RequireUserId(),
            new VerificationService.UploadedFile { Content = photoStream, FileName = photoFile.FileName, ContentType = photoFile.ContentType },
            new VerificationService.UploadedFile { Content = nidStream, FileName = nidFile.FileName, ContentType = nidFile.ContentType },
            paymentIdValue);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpDelete]
    public async Task<IActionResult> Cancel()
    {
        var error = await _verifications.CancelHelperAsync(RequireUserId());
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    private static string? CheckImage(IFormFile? file, string what)
    {
        if (file is null || file.Length == 0)
        {
            return $"Upload {what}.";
        }
        if (file.Length > MaxDocumentBytes)
        {
            return "Each photo must be 10 MB or smaller.";
        }
        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return $"Upload {what} as an image.";
        }
        return null;
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
