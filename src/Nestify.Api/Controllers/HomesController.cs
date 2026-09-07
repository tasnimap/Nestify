using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Homes;
using Nestify.Shared.Dtos.Home;

namespace Nestify.Api.Controllers;

// The shared house a user lives in. Every route works on the caller's own home,
// so none of them takes a home id.
[ApiController]
[Route("api/v1/homes")]
[Authorize]
public sealed class HomesController : ControllerBase
{
    private readonly HomeService _homes;

    public HomesController(HomeService homes)
    {
        _homes = homes;
    }

    // 204 when the user is in no home yet; the page shows the create/join screen.
    [HttpGet("mine")]
    public async Task<ActionResult<HomeDto>> GetMyHome()
    {
        var home = await _homes.GetMyHomeAsync(RequireUserId());
        return home is null ? NoContent() : Ok(home);
    }

    [HttpPost]
    public async Task<ActionResult<HomeDto>> Create(HomeDetailsDto dto)
    {
        var (home, error) = await _homes.CreateAsync(RequireUserId(), dto);
        return home is null ? BadRequest(new { message = error }) : Ok(home);
    }

    // Files a request; a manager or co-manager has to approve it.
    [HttpPost("join")]
    public async Task<IActionResult> Join(JoinHomeDto dto) =>
        Result(await _homes.RequestJoinAsync(RequireUserId(), dto.JoinCode));

    // 204 when the caller has no request waiting.
    [HttpGet("my-request")]
    public async Task<ActionResult<MyJoinRequestDto>> GetMyRequest()
    {
        var request = await _homes.GetMyRequestAsync(RequireUserId());
        return request is null ? NoContent() : Ok(request);
    }

    [HttpPost("my-request/cancel")]
    public async Task<IActionResult> CancelMyRequest() =>
        Result(await _homes.CancelMyRequestAsync(RequireUserId()));

    [HttpPost("mine/requests/{requestId:long}/approve")]
    public async Task<IActionResult> ApproveRequest(long requestId) =>
        Result(await _homes.ApproveRequestAsync(RequireUserId(), requestId));

    [HttpPost("mine/requests/{requestId:long}/reject")]
    public async Task<IActionResult> RejectRequest(long requestId) =>
        Result(await _homes.RejectRequestAsync(RequireUserId(), requestId));

    [HttpPut("mine")]
    public async Task<IActionResult> UpdateDetails(HomeDetailsDto dto) =>
        Result(await _homes.UpdateDetailsAsync(RequireUserId(), dto));

    [HttpPost("mine/members")]
    public async Task<IActionResult> AddMember(AddHomeMemberDto dto) =>
        Result(await _homes.AddMemberAsync(RequireUserId(), dto.Email));

    [HttpPost("mine/members/{memberId:long}/promote")]
    public async Task<IActionResult> Promote(long memberId) =>
        Result(await _homes.PromoteAsync(RequireUserId(), memberId));

    [HttpPost("mine/members/{memberId:long}/demote")]
    public async Task<IActionResult> Demote(long memberId) =>
        Result(await _homes.DemoteAsync(RequireUserId(), memberId));

    [HttpDelete("mine/members/{memberId:long}")]
    public async Task<IActionResult> RemoveMember(long memberId) =>
        Result(await _homes.RemoveMemberAsync(RequireUserId(), memberId));

    [HttpPost("mine/members/{memberId:long}/transfer-manager")]
    public async Task<IActionResult> TransferManager(long memberId) =>
        Result(await _homes.TransferManagerAsync(RequireUserId(), memberId));

    [HttpPost("mine/leave")]
    public async Task<IActionResult> Leave() =>
        Result(await _homes.LeaveAsync(RequireUserId()));

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
