using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Notifications;
using Nestify.Shared.Dtos.Notifications;

namespace Nestify.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly NotificationService _notifications;

    public NotificationsController(NotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationFeedDto>> Get()
        => Ok(await _notifications.GetForUserAsync(RequireUserId()));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
        => await _notifications.MarkReadAsync(RequireUserId(), id) ? NoContent() : NotFound();

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(RequireUserId());
        return NoContent();
    }

    private long RequireUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
