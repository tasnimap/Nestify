using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Helpers;
using Nestify.Api.Profiles;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Api.Controllers;

// Everything the signed-in helper does with her own profile and workspace.
[ApiController]
[Route("api/v1/helpers/me")]
[Authorize(Roles = "DomesticHelper")]
public sealed class HelperWorkspaceController : ControllerBase
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    private readonly HelperWorkspaceService _workspace;
    private readonly CloudinaryUploader _uploader;

    public HelperWorkspaceController(HelperWorkspaceService workspace, CloudinaryUploader uploader)
    {
        _workspace = workspace;
        _uploader = uploader;
    }

    // ---- profile ----

    [HttpGet]
    public async Task<ActionResult<HelperProfileDto>> GetProfile()
    {
        var profile = await _workspace.GetProfileAsync(RequireUserId());
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("nav")]
    public async Task<ActionResult<HelperNavDto>> GetNav() => Ok(await _workspace.GetNavAsync(RequireUserId()));

    [HttpPost]
    public async Task<ActionResult<HelperProfileDto>> Register(HelperProfileFormDto form)
    {
        var (data, error) = await _workspace.RegisterAsync(RequireUserId(), form);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPut]
    public async Task<ActionResult<HelperProfileDto>> Update(HelperProfileFormDto form)
    {
        var (data, error) = await _workspace.UpdateProfileAsync(RequireUserId(), form);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("photo")]
    [RequestSizeLimit(MaxPhotoBytes + 1024)]
    public async Task<ActionResult<HelperProfileDto>> UploadPhoto(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a picture first." });
        }
        if (file.Length > MaxPhotoBytes)
        {
            return BadRequest(new { message = "The picture must be 5 MB or smaller." });
        }
        if (file.ContentType is null || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only image files can be used as a profile photo." });
        }

        await using var stream = file.OpenReadStream();
        var (url, error) = await _uploader.UploadAsync(stream, file.FileName, file.ContentType);
        if (url is null)
        {
            return BadRequest(new { message = error });
        }

        var profile = await _workspace.SetPhotoAsync(RequireUserId(), url);
        return profile is null ? NotFound() : Ok(profile);
    }

    // ---- workspace ----

    [HttpGet("workspace/dashboard")]
    public async Task<ActionResult<HelperWorkspaceDashboardDto>> Dashboard()
    {
        var data = await _workspace.GetDashboardAsync(RequireUserId());
        return data is null ? NotFound() : Ok(data);
    }

    [HttpGet("workspace/availability")]
    public async Task<ActionResult<HelperAvailabilityDto>> Availability()
    {
        var data = await _workspace.GetAvailabilityAsync(RequireUserId());
        return data is null ? NotFound() : Ok(data);
    }

    [HttpPut("workspace/availability")]
    public async Task<IActionResult> SaveAvailability(HelperAvailabilityDto dto)
    {
        var error = await _workspace.SaveAvailabilityAsync(RequireUserId(), dto);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpGet("workspace/schedule")]
    public async Task<ActionResult<HelperWorkspaceScheduleDto>> Schedule([FromQuery] DateTime? weekStart)
    {
        var data = await _workspace.GetScheduleAsync(RequireUserId(), weekStart ?? DateTime.Today);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpGet("workspace/engagements")]
    public async Task<ActionResult<HelperWorkspaceEngagementsDto>> Engagements()
    {
        var data = await _workspace.GetEngagementsAsync(RequireUserId());
        return data is null ? NotFound() : Ok(data);
    }

    [HttpPost("workspace/engagements/{id}/accept")]
    public async Task<IActionResult> Accept(string id)
    {
        var error = await _workspace.DecideAsync(RequireUserId(), id, true, null);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpPost("workspace/engagements/{id}/decline")]
    public async Task<IActionResult> Decline(string id, [FromBody] DeclineEngagementDto request)
    {
        var error = await _workspace.DecideAsync(RequireUserId(), id, false, request.Reason);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpGet("workspace/reviews")]
    public async Task<ActionResult<HelperReviewsDto>> Reviews()
    {
        var data = await _workspace.GetReviewsAsync(RequireUserId());
        return data is null ? NotFound() : Ok(data);
    }

    [HttpPost("workspace/reviews/{id}/reply")]
    public async Task<IActionResult> Reply(string id, [FromBody] ReviewReplyDto request)
    {
        var error = await _workspace.ReplyAsync(RequireUserId(), id, request.Reply);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    private long RequireUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
