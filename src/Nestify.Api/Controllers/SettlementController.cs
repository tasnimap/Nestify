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

    [HttpGet("mine/books")]
    public async Task<ActionResult<List<SettlementBookDto>>> GetBooks()
    {
        var books = await _settlements.GetBooksAsync(RequireUserId());
        return books is null ? NotFound(new { message = "You are not in a home." }) : Ok(books);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<SettlementWorkspaceDto>> GetMine([FromQuery] int year, [FromQuery] int month)
    {
        var workspace = await _settlements.GetAsync(RequireUserId(), year, month);
        return workspace is null ? NotFound(new { message = "You are not in a home." }) : Ok(workspace);
    }

    [HttpPost("mine/open")]
    public async Task<IActionResult> OpenBook([FromQuery] int year, [FromQuery] int month) =>
        Result(await _settlements.OpenBookAsync(RequireUserId(), year, month));

    [HttpPost("mine/members")]
    public async Task<IActionResult> AddMember(
        [FromQuery] int year, [FromQuery] int month, AddSettlementMemberRequest request) =>
        Result(await _settlements.AddMemberAsync(RequireUserId(), year, month, request));

    [HttpPost("mine/bills")]
    public async Task<ActionResult<SettlementBillDto>> AddBill(
        [FromQuery] int year, [FromQuery] int month, CreateSettlementBillRequest request)
    {
        var (data, error) = await _settlements.AddBillAsync(RequireUserId(), year, month, request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPut("mine/bills/{id:long}")]
    public async Task<IActionResult> UpdateBill(
        long id, [FromQuery] int year, [FromQuery] int month, UpdateSettlementBillRequest request) =>
        Result(await _settlements.UpdateBillAsync(RequireUserId(), year, month, id, request));

    [HttpDelete("mine/bills/{id:long}")]
    public async Task<IActionResult> DeleteBill(long id, [FromQuery] int year, [FromQuery] int month) =>
        Result(await _settlements.DeleteBillAsync(RequireUserId(), year, month, id));

    [HttpPost("mine/payments")]
    public async Task<ActionResult<SettlementPaymentDto>> AddPayment(
        [FromQuery] int year, [FromQuery] int month, CreateSettlementPaymentRequest request)
    {
        var (data, error) = await _settlements.AddPaymentAsync(RequireUserId(), year, month, request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpDelete("mine/payments/{id:long}")]
    public async Task<IActionResult> DeletePayment(long id, [FromQuery] int year, [FromQuery] int month) =>
        Result(await _settlements.DeletePaymentAsync(RequireUserId(), year, month, id));

    [HttpPut("mine/meals")]
    public async Task<IActionResult> SaveMeals(
        [FromQuery] int year, [FromQuery] int month, SaveSettlementMealsRequest request) =>
        Result(await _settlements.SaveMealsAsync(RequireUserId(), year, month, request));

    [HttpPost("mine/finalize")]
    public async Task<ActionResult<SettlementResultDto>> Finalize([FromQuery] int year, [FromQuery] int month)
    {
        var (result, error) = await _settlements.FinalizeAsync(RequireUserId(), year, month);
        return result is null ? Conflict(new { message = error }) : Created(string.Empty, result);
    }

    private IActionResult Result((bool Ok, string Message) result) =>
        result.Ok ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });

    private long RequireUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
