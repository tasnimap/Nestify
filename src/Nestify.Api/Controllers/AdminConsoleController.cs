using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Admin;
using Nestify.Shared.Dtos.Admin;

namespace Nestify.Api.Controllers;

// Moderation, fees and plans, admin accounts and the audit log (Admin.sql).
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminConsoleController : ControllerBase
{
    private readonly AdminConsoleService _admin;

    public AdminConsoleController(AdminConsoleService admin) => _admin = admin;

    [HttpGet("summary")]
    public async Task<ActionResult<AdminSummaryDto>> Summary() => Ok(await _admin.GetSummaryAsync());

    // ---- Housing ----

    [HttpGet("housing/posts")]
    public async Task<ActionResult<List<AdminHousingPostDto>>> HousingPosts() => Ok(await _admin.GetHousingPostsAsync());

    [HttpGet("housing/reports")]
    public async Task<ActionResult<List<AdminReportDto>>> HousingReports() => Ok(await _admin.GetHousingReportsAsync());

    [HttpPost("housing/posts/{id:long}/takedown")]
    public async Task<IActionResult> TakeDownHousing(long id, TakedownRequestDto dto) =>
        Result(await _admin.TakeDownHousingPostAsync(RequireAdminId(), id, dto));

    [HttpPost("housing/posts/{id:long}/restore")]
    public async Task<IActionResult> RestoreHousing(long id) =>
        Result(await _admin.RestoreHousingPostAsync(RequireAdminId(), id));

    // ---- Marketplace ----

    [HttpGet("marketplace/items")]
    public async Task<ActionResult<List<AdminMarketItemDto>>> MarketItems() => Ok(await _admin.GetMarketItemsAsync());

    [HttpGet("marketplace/reports")]
    public async Task<ActionResult<List<AdminReportDto>>> MarketReports() => Ok(await _admin.GetMarketReportsAsync());

    [HttpPost("marketplace/items/{id:long}/takedown")]
    public async Task<IActionResult> TakeDownMarket(long id, TakedownRequestDto dto) =>
        Result(await _admin.TakeDownMarketItemAsync(RequireAdminId(), id, dto));

    [HttpPost("marketplace/items/{id:long}/restore")]
    public async Task<IActionResult> RestoreMarket(long id) =>
        Result(await _admin.RestoreMarketItemAsync(RequireAdminId(), id));

    // ---- Reports ----

    [HttpPost("reports/{scope}/{id:long}/dismiss")]
    public async Task<IActionResult> DismissReport(ModerationScope scope, long id) =>
        await _admin.DismissReportAsync(RequireAdminId(), scope, id)
            ? Ok(new { message = "Report dismissed." })
            : BadRequest(new { message = "That report is no longer open." });

    // ---- Fees and plans ----

    [HttpGet("fees")]
    public async Task<ActionResult<List<AdminFeeDto>>> Fees() => Ok(await _admin.GetFeesAsync());

    [HttpPut("fees/{code}")]
    public async Task<IActionResult> SaveFee(string code, SaveFeeDto dto) =>
        Result(await _admin.SaveFeeAsync(RequireAdminId(), code, dto.Amount));

    [HttpGet("plans")]
    public async Task<ActionResult<List<AdminPlanDto>>> Plans() => Ok(await _admin.GetPlansAsync());

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan(SavePlanDto dto)
    {
        var (id, error) = await _admin.CreatePlanAsync(RequireAdminId(), dto);
        return id is null ? BadRequest(new { message = error }) : Ok(new { id = id.Value.ToString() });
    }

    [HttpPut("plans/{id:long}")]
    public async Task<IActionResult> UpdatePlan(long id, SavePlanDto dto) =>
        Result(await _admin.UpdatePlanAsync(RequireAdminId(), id, dto));

    [HttpPost("plans/{id:long}/toggle")]
    public async Task<IActionResult> TogglePlan(long id) =>
        await _admin.TogglePlanAsync(RequireAdminId(), id)
            ? Ok(new { message = "Plan updated." })
            : BadRequest(new { message = "That plan no longer exists." });

    [HttpDelete("plans/{id:long}")]
    public async Task<IActionResult> DeletePlan(long id) =>
        Result(await _admin.DeletePlanAsync(RequireAdminId(), id));

    // ---- Admin accounts ----

    [HttpGet("accounts")]
    public async Task<ActionResult<List<AdminAccountDto>>> Accounts() => Ok(await _admin.GetAdminsAsync());

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount(CreateAdminDto dto)
    {
        var (id, error) = await _admin.CreateAdminAsync(RequireAdminId(), dto);
        return id is null ? BadRequest(new { message = error }) : Ok(new { id = id.Value.ToString() });
    }

    [HttpPost("accounts/{id:long}/toggle")]
    public async Task<IActionResult> ToggleAccount(long id) =>
        Result(await _admin.ToggleAdminAsync(RequireAdminId(), id));

    // ---- Audit ----

    [HttpGet("audit")]
    public async Task<ActionResult<List<AdminAuditEntryDto>>> Audit([FromQuery] int take = 200) =>
        Ok(await _admin.GetAuditAsync(take));

    private IActionResult Result((bool Ok, string Message) outcome) =>
        outcome.Ok ? Ok(new { message = outcome.Message }) : BadRequest(new { message = outcome.Message });

    private long RequireAdminId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
