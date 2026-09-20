using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Profiles;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Api.Controllers;

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
    public async Task<ActionResult<HelperVerificationStatusDto>> GetPending()
    {
        var (data, error) = await _verifications.GetHelperPendingAsync(RequireUserId());
        return error is null ? Ok(data) : BadRequest(new { message = error });
    }

    [HttpPost]
    [RequestSizeLimit(MaxDocumentBytes + 1024)]
    public async Task<IActionResult> Submit([FromForm] string documentType, IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a document photo first." });
        }

        if (file.Length > MaxDocumentBytes)
        {
            return BadRequest(new { message = "The document photo must be 10 MB or smaller." });
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Upload a clear image of your document." });
        }

        await using var stream = file.OpenReadStream();
        var error = await _verifications.SubmitHelperAsync(
            RequireUserId(), documentType, stream, file.FileName, file.ContentType);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpDelete]
    public async Task<IActionResult> Cancel()
    {
        var error = await _verifications.CancelHelperAsync(RequireUserId());
        return error is null ? NoContent() : BadRequest(new { message = error });
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
