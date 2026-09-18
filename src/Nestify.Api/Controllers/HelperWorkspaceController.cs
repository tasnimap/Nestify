using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Helpers;
using Nestify.Shared.Dtos.Helpers;
namespace Nestify.Api.Controllers;
[ApiController]
[Route("api/v1/helpers/me/workspace")]
[Authorize(Roles = "DomesticHelper")]
public sealed class HelperWorkspaceController : ControllerBase
{
    private readonly HelperWorkspaceService _workspace;
    public HelperWorkspaceController(HelperWorkspaceService workspace) => _workspace=workspace;
    [HttpGet("dashboard")] public async Task<ActionResult<HelperWorkspaceDashboardDto>> Dashboard(){var data=await _workspace.GetDashboardAsync(UserId());return data is null?NotFound():Ok(data);}
    [HttpGet("availability")] public async Task<ActionResult<HelperAvailabilityDto>> Availability([FromQuery]DateTime? weekStart){var data=await _workspace.GetAvailabilityAsync(UserId(),weekStart??DateTime.UtcNow);return data is null?NotFound():Ok(data);}
    [HttpPut("availability")] public async Task<IActionResult> SaveAvailability(HelperAvailabilityDto dto){var error=await _workspace.SaveAvailabilityAsync(UserId(),dto);return error is null?NoContent():BadRequest(new{message=error});}
    [HttpGet("schedule")] public async Task<ActionResult<HelperWorkspaceScheduleDto>> Schedule([FromQuery]DateTime? weekStart){var data=await _workspace.GetScheduleAsync(UserId(),weekStart??DateTime.UtcNow);return data is null?NotFound():Ok(data);}
    [HttpGet("engagements")] public async Task<ActionResult<HelperWorkspaceEngagementsDto>> Engagements(){var data=await _workspace.GetEngagementsAsync(UserId());return data is null?NotFound():Ok(data);}
    [HttpPost("engagements/{id}/accept")] public async Task<IActionResult> Accept(string id){var error=await _workspace.DecideAsync(UserId(),id,true,null);return error is null?NoContent():BadRequest(new{message=error});}
    [HttpPost("engagements/{id}/decline")] public async Task<IActionResult> Decline(string id,[FromBody]DeclineRequest request){var error=await _workspace.DecideAsync(UserId(),id,false,request.Reason);return error is null?NoContent():BadRequest(new{message=error});}
    private long UserId(){var sub=User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value??User.FindFirst(ClaimTypes.NameIdentifier)?.Value;return long.TryParse(sub,out var id)?id:throw new UnauthorizedAccessException("Missing user id claim.");}
    public sealed class DeclineRequest { public string? Reason {get;set;} }
}
