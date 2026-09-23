using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Helpers;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Api.Controllers;

// Browsing helpers and booking them, from the bachelor's side.
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

    [HttpGet("{id:long}")]
    public async Task<ActionResult<HelperDetailDto>> Get(long id)
    {
        var helper = await _helpers.GetHelperAsync(id.ToString(), CurrentUserId());
        return helper is null ? NotFound() : Ok(helper);
    }

    [HttpGet("{id:long}/reviews")]
    public async Task<ActionResult<HelperPageDto<ReviewDto>>> GetReviews(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 5)
        => Ok(await _helpers.GetReviewsAsync(id.ToString(), page, pageSize));

    [HttpGet("engagements")]
    [Authorize]
    public async Task<ActionResult<List<EngagementDto>>> GetMyEngagements()
        => Ok(await _helpers.GetMyEngagementsAsync(RequireUserId()));

    [HttpPost("{id:long}/engagements")]
    [Authorize]
    public async Task<ActionResult<EngagementDto>> RequestEngagement(long id, [FromBody] EngagementRequestDto request)
    {
        var (data, error) = await _helpers.RequestEngagementAsync(RequireUserId(), id.ToString(), request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("engagements/{id}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelRequest(string id)
    {
        var error = await _helpers.CancelRequestAsync(RequireUserId(), id);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpPost("engagements/{id}/complete")]
    [Authorize]
    public async Task<IActionResult> MarkComplete(string id)
    {
        var error = await _helpers.MarkCompleteAsync(RequireUserId(), id);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpPost("engagements/{id}/release")]
    [Authorize]
    public async Task<IActionResult> ReleaseEngagement(string id)
    {
        var error = await _helpers.ReleaseEngagementAsync(RequireUserId(), id);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpPost("engagements/{id}/review")]
    [Authorize]
    public async Task<IActionResult> SubmitReview(string id, [FromBody] SubmitReviewDto request)
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
