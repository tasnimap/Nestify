using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nestify.Api.Assistant;
using Nestify.Api.Homes;
using Nestify.Shared.Dtos.Assistant;

namespace Nestify.Api.Controllers;

[ApiController]
[Route("api/v1/assistant")]
[Authorize]
[EnableRateLimiting("assistant-chat")]
public sealed class AssistantController : ControllerBase
{
    private readonly GeminiAssistantService _assistant;
    private readonly HomeService _homes;

    public AssistantController(GeminiAssistantService assistant, HomeService homes)
    {
        _assistant = assistant;
        _homes = homes;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AssistantChatResponseDto>> Chat(AssistantChatRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var home = await _homes.GetMyHomeAsync(RequireUserId());
            var reply = await _assistant.ReplyAsync(request, home, cancellationToken);
            return Ok(new AssistantChatResponseDto { Reply = reply });
        }
        catch (AssistantRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (AssistantUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
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
