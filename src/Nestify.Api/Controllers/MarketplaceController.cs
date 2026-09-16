using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Marketplace;
using Nestify.Shared.Dtos.Marketplace;

namespace Nestify.Api.Controllers;

// Second-hand marketplace: listings, buy interests and reports (Marketplace.sql).
[ApiController]
[Route("api/v1/marketplace")]
[Authorize]
public sealed class MarketplaceController : ControllerBase
{
    private readonly MarketplaceService _marketplace;

    public MarketplaceController(MarketplaceService marketplace)
    {
        _marketplace = marketplace;
    }

    // ---- Browse + detail ----

    [HttpGet("items")]
    public async Task<ActionResult<MarketplacePageDto<MarketplaceItemSummaryDto>>> Browse([FromQuery] MarketplaceItemFilterDto filter) =>
        Ok(await _marketplace.BrowseAsync(RequireUserId(), filter));

    [HttpGet("items/{id:long}")]
    public async Task<ActionResult<MarketplaceItemDetailDto>> GetItem(long id)
    {
        var item = await _marketplace.GetItemAsync(RequireUserId(), id, countView: true);
        return item is null ? NotFound() : Ok(item);
    }

    // ---- Create + edit + mine ----

    [HttpPost("items")]
    public async Task<IActionResult> Create(CreateMarketplaceItemDto dto)
    {
        var (id, error) = await _marketplace.CreateAsync(RequireUserId(), dto);
        return id is null ? BadRequest(new { message = error }) : Ok(new { id = id.Value.ToString() });
    }

    [HttpGet("items/{id:long}/edit")]
    public async Task<ActionResult<MarketplaceItemDetailDto>> GetForEdit(long id)
    {
        var item = await _marketplace.GetForEditAsync(RequireUserId(), id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("items/{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateMarketplaceItemDto dto) =>
        Result(await _marketplace.UpdateAsync(RequireUserId(), id, dto));

    [HttpGet("items/mine")]
    public async Task<ActionResult<IReadOnlyList<MyListingDto>>> MyListings() =>
        Ok(await _marketplace.GetMyListingsAsync(RequireUserId()));

    // The seller picks which request got the item; the rest are closed.
    [HttpPost("items/{id:long}/sold")]
    public async Task<IActionResult> MarkSold(long id, MarkSoldDto dto) =>
        Result(await _marketplace.MarkSoldAsync(RequireUserId(), id,
            long.TryParse(dto.BuyerInterestId, out var interestId) ? interestId : null));

    [HttpDelete("items/{id:long}")]
    public async Task<IActionResult> Delete(long id) =>
        Result(await _marketplace.DeleteAsync(RequireUserId(), id), "Listing removed.", "That listing is not yours.");

    // 204 when there is nothing to compare against yet.
    [HttpGet("price-suggestion")]
    public async Task<ActionResult<PriceSuggestionDto>> PriceSuggestion([FromQuery] MarketplaceCategory category, [FromQuery] ItemCondition condition)
    {
        var suggestion = await _marketplace.GetPriceSuggestionAsync(category, condition);
        return suggestion is null ? NoContent() : Ok(suggestion);
    }

    // ---- Buy interests ----

    [HttpPost("items/{id:long}/interests")]
    public async Task<IActionResult> ExpressInterest(long id, ExpressInterestDto dto) =>
        Result(await _marketplace.ExpressInterestAsync(RequireUserId(), id, dto.Message));

    [HttpGet("items/{id:long}/interests")]
    public async Task<ActionResult<IReadOnlyList<BuyInterestDto>>> ListingInterests(long id) =>
        Ok(await _marketplace.GetListingInterestsAsync(RequireUserId(), id));

    [HttpPost("interests/{interestId:long}/accept")]
    public async Task<IActionResult> Accept(long interestId) =>
        Result(await _marketplace.RespondAsync(RequireUserId(), interestId, accept: true), "Request accepted.", "That request is no longer pending.");

    [HttpPost("interests/{interestId:long}/decline")]
    public async Task<IActionResult> Decline(long interestId) =>
        Result(await _marketplace.RespondAsync(RequireUserId(), interestId, accept: false), "Request declined.", "That request is no longer pending.");

    [HttpGet("interests/mine")]
    public async Task<ActionResult<IReadOnlyList<MyBuyInterestDto>>> MyInterests() =>
        Ok(await _marketplace.GetMyInterestsAsync(RequireUserId()));

    [HttpPost("interests/{interestId:long}/withdraw")]
    public async Task<IActionResult> Withdraw(long interestId) =>
        Result(await _marketplace.WithdrawAsync(RequireUserId(), interestId), "Request withdrawn.", "That request cannot be withdrawn.");

    // ---- Reports ----

    [HttpPost("items/{id:long}/report")]
    public async Task<IActionResult> Report(long id, ReportListingDto dto) =>
        Result(await _marketplace.ReportAsync(RequireUserId(), id, dto));

    private IActionResult Result((bool Ok, string Message) outcome) =>
        outcome.Ok ? Ok(new { message = outcome.Message }) : BadRequest(new { message = outcome.Message });

    private IActionResult Result(bool ok, string success, string failure) =>
        ok ? Ok(new { message = success }) : BadRequest(new { message = failure });

    private long RequireUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
