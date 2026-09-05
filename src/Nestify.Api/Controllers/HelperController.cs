using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Helpers;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Api.Controllers;

[ApiController]
[Route("api/v1/helpers")]
public sealed class HelperController : ControllerBase
{
    private readonly HelperService _helpers;

    public HelperController(HelperService helpers)
    {
        _helpers = helpers;
    }

    [HttpGet]
    public async Task<ActionResult<HelperPageDto<HelperSummaryDto>>> Browse([FromQuery] HelperFilterDto filter)
        => Ok(await _helpers.BrowseAsync(filter));

    [HttpGet("{id}")]
    public async Task<ActionResult<HelperDetailDto>> Get(string id)
    {
        var helper = await _helpers.GetHelperAsync(id, CurrentUserId());
        return helper is null ? NotFound() : Ok(helper);
    }

    [HttpGet("{id}/reviews")]
    public async Task<ActionResult<HelperPageDto<ReviewDto>>> GetReviews(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 5)
        => Ok(await _helpers.GetReviewsAsync(id, page, pageSize));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<HelperDetailDto>> GetMyProfile()
    {
        var profile = await _helpers.GetMyProfileAsync(RequireUserId());
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("me")]
    [Authorize]
    public async Task<ActionResult<HelperDetailDto>> Register(HelperRegistrationDto dto)
    {
        var (data, error) = await _helpers.RegisterAsync(RequireUserId(), dto);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<HelperDetailDto>> Update(HelperRegistrationDto dto)
    {
        var (data, error) = await _helpers.UpdateProfileAsync(RequireUserId(), dto);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpGet("engagements")]
    [Authorize]
    public async Task<ActionResult<List<EngagementDto>>> GetMyEngagements()
        => Ok(await _helpers.GetMyEngagementsAsync(RequireUserId()));

    [HttpPost("{id}/engagements")]
    [Authorize]
    public async Task<ActionResult<EngagementDto>> RequestEngagement(string id)
    {
        var (data, error) = await _helpers.RequestEngagementAsync(RequireUserId(), id);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("engagements/{id}/confirm")]
    [Authorize]
    public async Task<ActionResult<EngagementDto>> Confirm(string id)
    {
        var (data, error) = await _helpers.ConfirmEngagementAsync(RequireUserId(), id);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("engagements/{id}/complete")]
    [Authorize]
    public async Task<ActionResult<EngagementDto>> MarkComplete(string id)
    {
        var (data, error) = await _helpers.MarkCompleteAsync(RequireUserId(), id);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("engagements/{id}/review")]
    [Authorize]
    public async Task<IActionResult> SubmitReview(string id, [FromBody] SubmitReviewRequest request)
    {
        var error = await _helpers.SubmitReviewAsync(RequireUserId(), id, request.Rating, request.Comment);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    private long? CurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id) ? id : null;
    }

    private long RequireUserId() => CurrentUserId()
        ?? throw new UnauthorizedAccessException("Missing user id claim.");
}

public sealed class SubmitReviewRequest
{
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}