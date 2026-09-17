using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Housing;
using Nestify.Shared.Dtos.Housing;

namespace Nestify.Api.Controllers;

// Housing posts and seat bookings (Housing.sql).
[ApiController]
[Route("api/v1/housing")]
[Authorize]
public sealed class HousingController : ControllerBase
{
    private readonly HousingService _housing;

    public HousingController(HousingService housing)
    {
        _housing = housing;
    }

    // ---- Houses the caller can post under ----

    [HttpGet("houses")]
    public async Task<ActionResult<List<HouseOptionDto>>> GetHouses() =>
        Ok(await _housing.GetManageableHousesAsync(RequireUserId()));

    // ---- Browse + detail ----

    [HttpGet("posts")]
    public async Task<ActionResult<HousingPageDto<HousingPostSummaryDto>>> Browse([FromQuery] HousingPostFilterDto filter) =>
        Ok(await _housing.BrowseAsync(RequireUserId(), filter));

    [HttpGet("posts/{id:long}")]
    public async Task<ActionResult<HousingPostDetailDto>> GetPost(long id)
    {
        var post = await _housing.GetPostAsync(RequireUserId(), id);
        return post is null ? NotFound() : Ok(post);
    }

    // ---- Create + edit + mine ----

    [HttpPost("posts")]
    public async Task<IActionResult> Create(CreateHousingPostRequestDto dto)
    {
        var (id, error) = await _housing.CreateAsync(RequireUserId(), dto);
        return id is null ? BadRequest(new { message = error }) : Ok(new { id = id.Value.ToString() });
    }

    [HttpGet("posts/{id:long}/edit")]
    public async Task<ActionResult<HousingPostDetailDto>> GetPostForEdit(long id)
    {
        var post = await _housing.GetPostForEditAsync(RequireUserId(), id);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpPut("posts/{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateHousingPostRequestDto dto) =>
        Result(await _housing.UpdateAsync(RequireUserId(), id, dto));

    [HttpGet("posts/mine")]
    public async Task<ActionResult<List<MyHousingPostDto>>> GetMine() =>
        Ok(await _housing.GetMineAsync(RequireUserId()));

    [HttpPost("posts/{id:long}/close")]
    public async Task<IActionResult> Close(long id) =>
        await _housing.CloseAsync(RequireUserId(), id) ? NoContent() : NotFound();

    [HttpPost("posts/{id:long}/reopen")]
    public async Task<IActionResult> Reopen(long id) =>
        await _housing.ReopenAsync(RequireUserId(), id) ? NoContent() : NotFound();

    [HttpDelete("posts/{id:long}")]
    public async Task<IActionResult> Delete(long id) =>
        await _housing.DeleteAsync(RequireUserId(), id) ? NoContent() : NotFound();

    // ---- Bookings ----

    [HttpPost("posts/{id:long}/report")]
    public async Task<IActionResult> Report(long id, ReportHousingPostDto dto) =>
        Result(await _housing.ReportAsync(RequireUserId(), id, dto));

    [HttpPost("posts/{id:long}/bookings")]
    public async Task<IActionResult> RequestBooking(long id, BookingRequestBody body) =>
        Result(await _housing.RequestBookingAsync(RequireUserId(), id, body.Message));

    [HttpGet("posts/{id:long}/bookings")]
    public async Task<ActionResult<List<BookingRequesterDto>>> GetRequesters(long id)
    {
        var rows = await _housing.GetRequestersAsync(RequireUserId(), id);
        return rows is null ? NotFound() : Ok(rows);
    }

    [HttpPost("bookings/{id:long}/accept")]
    public async Task<IActionResult> Accept(long id) =>
        Result(await _housing.AcceptBookingAsync(RequireUserId(), id));

    [HttpPost("bookings/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, RejectBookingRequestDto dto) =>
        Result(await _housing.RejectBookingAsync(RequireUserId(), id, dto.Message));

    [HttpPost("bookings/{id:long}/withdraw")]
    public async Task<IActionResult> Withdraw(long id) =>
        Result(await _housing.WithdrawBookingAsync(RequireUserId(), id));

    [HttpGet("bookings/{id:long}/contact")]
    public async Task<ActionResult<ContactDisclosureDto>> GetContact(long id)
    {
        var contact = await _housing.GetBookingContactAsync(RequireUserId(), id);
        return contact is null ? NotFound() : Ok(contact);
    }

    [HttpGet("bookings/mine")]
    public async Task<ActionResult<List<MyBookingDto>>> GetMyBookings() =>
        Ok(await _housing.GetMyBookingsAsync(RequireUserId()));

    public sealed class BookingRequestBody
    {
        public string? Message { get; set; }
    }

    private IActionResult Result((bool Ok, string Message) outcome) =>
        outcome.Ok ? Ok(new { message = outcome.Message }) : BadRequest(new { message = outcome.Message });

    private long RequireUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
