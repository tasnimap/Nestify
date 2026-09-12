using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Settlement;
using Nestify.Shared.Dtos.Settlement;

namespace Nestify.Api.Controllers;

[ApiController]
[Route("api/v1/settlement")]
[Authorize]
public sealed class SettlementController : ControllerBase
{
    private readonly SettlementService _settlements;

    public SettlementController(SettlementService settlements)
    {
        _settlements = settlements;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<SettlementWorkspaceDto>> GetMine([FromQuery] int year, [FromQuery] int month)
    {
        var workspace = await _settlements.GetAsync(RequireUserId(), year, month);
        return workspace is null ? NotFound(new { message = "You are not in a home." }) : Ok(workspace);
    }

    [HttpPost("mine/bills")]
    public async Task<ActionResult<SettlementBillDto>> AddBill(
        [FromQuery] int year, [FromQuery] int month, CreateSettlementBillRequest request)
    {
        var (data, error) = await _settlements.AddBillAsync(RequireUserId(), year, month, request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("mine/payments")]
    public async Task<ActionResult<SettlementPaymentDto>> AddPayment(
        [FromQuery] int year, [FromQuery] int month, CreateSettlementPaymentRequest request)
    {
        var (data, error) = await _settlements.AddPaymentAsync(RequireUserId(), year, month, request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPut("mine/meals")]
    public async Task<IActionResult> SaveMeals(
        [FromQuery] int year, [FromQuery] int month, SaveSettlementMealsRequest request)
    {
        var result = await _settlements.SaveMealsAsync(RequireUserId(), year, month, request);
        return result.Ok ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });
    }

    [HttpPost("mine/finalize")]
    public async Task<ActionResult<SettlementResultDto>> Finalize([FromQuery] int year, [FromQuery] int month)
    {
        var (result, error) = await _settlements.FinalizeAsync(RequireUserId(), year, month);
        return result is null ? Conflict(new { message = error }) : Created(string.Empty, result);
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
