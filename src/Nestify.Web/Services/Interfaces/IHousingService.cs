using Nestify.Shared.Dtos.Housing;

namespace Nestify.Web.Services.Interfaces;

/// <summary>
/// The contract M1's pages depend on. Mirrors §3.6/§3.7 of the implementation plan —
/// same operations, same DTOs, same return shapes — so the mock and the real
/// HttpClient implementation are swappable with one line in Program.cs.
///
/// F1 + F2 + F3-core scope. <c>bookings/mine</c> and withdraw are still to come —
/// don't add them here ahead of that page landing.
/// </summary>
public interface IHousingService
{
    Task<HousingPageDto<HousingPostSummaryDto>> BrowseAsync(HousingPostFilterDto filter);

    /// <summary>
    /// Returns null for a non-existent id AND for a post the viewer is not eligible for (§5.3) —
    /// the two cases are indistinguishable by design, so the detail page must render the same
    /// "not available" state for both. Never render a 403-style "you're not eligible" message.
    /// </summary>
    Task<HousingPostDetailDto?> GetPostAsync(string id);

    /// <summary>Creates a post under the given house. Returns the new post's id.</summary>
    Task<string> CreateAsync(CreateHousingPostRequestDto request);

    /// <summary>Owner-only. Returns null if the post doesn't exist or isn't the caller's.</summary>
    Task<HousingPostDetailDto?> GetPostForEditAsync(string id);

    /// <summary>Owner-only. Returns false if the post doesn't exist or isn't the caller's.</summary>
    Task<bool> UpdateAsync(string id, UpdateHousingPostRequestDto request);

    /// <summary>
    /// The caller's own posts, including closed ones — deliberately bypasses eligibility
    /// filtering (§3.6) so an owner always sees their own listing.
    /// </summary>
    Task<IReadOnlyList<MyHousingPostDto>> GetMineAsync();

    /// <summary>Owner-only. Sets Status to Closed; the post drops out of Browse for everyone else.</summary>
    Task<bool> CloseAsync(string id);

    /// <summary>Owner-only. Sets Status back to Active.</summary>
    Task<bool> ReopenAsync(string id);

    /// <summary>
    /// Owner-only. A real, permanent delete (§3.6) — not a soft "Removed" status like M4's
    /// items — and cascades to that post's booking requests.
    /// </summary>
    Task<bool> DeleteAsync(string id);

    // ---- Two-party booking flow (§3.7, §11.4) -----------------------------

    /// <summary>
    /// Seeker requests to book a post. False if the post is closed, it's the caller's own
    /// post, or a Pending/Accepted request already exists from this caller (§3.7, 409 on the
    /// DB's partial unique index).
    /// </summary>
    Task<bool> RequestBookingAsync(string postId, string? message);

    /// <summary>
    /// Manager/CoManager view of everyone who's requested this post (§11.4.4). Rows never
    /// carry contact — see <see cref="GetBookingContactAsync"/>.
    /// </summary>
    Task<IReadOnlyList<BookingRequesterDto>> GetRequestersAsync(string postId);

    /// <summary>Manager accepts — the only transition that unlocks mutual disclosure (§11.4.2).</summary>
    Task<bool> AcceptBookingAsync(string bookingId);

    /// <summary>Manager rejects.</summary>
    Task<bool> RejectBookingAsync(string bookingId, RejectBookingRequestDto request);

    /// <summary>
    /// Returns contact only when the booking is Accepted and the caller is a party to it —
    /// the requester or the post's manager (§11.4.2). The single gate for the whole module's
    /// PII disclosure; every other DTO here structurally lacks a contact property (§11.4.3).
    /// </summary>
    Task<ContactDisclosureDto?> GetBookingContactAsync(string bookingId);

    /// <summary>
    /// Seeker's view of their own booking requests. Rows never carry manager contact —
    /// use GetBookingContactAsync for PII disclosure (§11.4.2).
    /// </summary>
    Task<IReadOnlyList<MyBookingDto>> GetMyBookingsAsync();
}