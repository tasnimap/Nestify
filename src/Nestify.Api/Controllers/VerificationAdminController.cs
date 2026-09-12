using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Profiles;
using Nestify.Shared.Dtos.Admin;

namespace Nestify.Api.Controllers;

[ApiController]
[Route("api/v1/admin/verifications")]
[Authorize(Roles = "Admin")]
public sealed class VerificationAdminController : ControllerBase
{
    private readonly VerificationService _verifications;
    public VerificationAdminController(VerificationService verifications) => _verifications = verifications;
    [HttpGet] public Task<List<VerificationRequestDto>> GetQueue() => _verifications.GetQueueAsync();
    [HttpPost("{id:long}/decision")]
    public async Task<IActionResult> Decide(long id, [FromBody] VerificationDecisionDto decision)
    {
        var error = await _verifications.DecideAsync(id, RequireAdminId(), decision.Approve);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }
    private long RequireAdminId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Missing user id claim.");
    }
    public sealed class VerificationDecisionDto { public bool Approve { get; set; } }
}
