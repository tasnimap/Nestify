// src/Nestify.Web/Services/Interfaces/Implementations/MockHousingService.cs
// In-memory fixtures for the frontend phase. Covers the awkward cases: an empty
// filter result, a single-result filter, a full page, and a not-found id.
using Nestify.Shared.Dtos.Housing;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MockHousingService : IHousingService
{
    private sealed class Post
    {
        public required string Id { get; init; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public ListingType ListingType { get; set; }
        public int SeatsAvailable { get; set; }
        public decimal MonthlyRent { get; set; }
        // AreaName is the upazila or metropolitan thana, spelled as the seeded
        // administrative tables spell it, so the browse filters can match on it.
        public required string AreaName { get; set; }
        public required string District { get; set; }
        public required string Division { get; set; }
        public PostStatus Status { get; set; } = PostStatus.Active;
        public DateTime CreatedAtUtc { get; set; }
        public EligibilityDto Eligibility { get; set; } = new();
        public bool IsMine { get; init; }
        public List<string> ImageUrls { get; set; } = new();
    }

    private sealed class Booking
    {
        public required string Id { get; init; }
        public required string PostId { get; init; }
        public required string RequesterName { get; init; }
        public required string RequesterEmail { get; init; }
        public required string RequesterPhone { get; init; }
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public string? Message { get; set; }
        public DateTime RequestedAtUtc { get; set; }
    }

    private readonly IHouseLookupService _houseLookup;
    private readonly IAreaService _areas;
    private readonly List<Post> _posts;
    private readonly List<Booking> _bookings;

    public MockHousingService(IHouseLookupService houseLookup, IAreaService areas)
    {
        _houseLookup = houseLookup;
        _areas = areas;
        _bookings = new List<Booking>();
        var now = DateTime.UtcNow;

        _posts = new List<Post>
        {
            new()
            {
                Id = "post-dhanmondi-seat", Title = "One seat in a 3-bed flat, Dhanmondi",
                Description = "Quiet mess near Dhanmondi 27. Two current tenants are AUST/DU students. "
                    + "Wifi, gas, and a part-time cleaner included in rent. Attached bath for the vacant seat.",
                ListingType = ListingType.SingleSeat, SeatsAvailable = 1, MonthlyRent = 6500m,
                AreaName = "Dhanmondi", District = "Dhaka", Division = "Dhaka", CreatedAtUtc = now.AddHours(-5),
                Eligibility = new EligibilityDto { Gender = Gender.Male, StudentOnly = true, MaxAge = 27 },
                ImageUrls = new List<string> { "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&q=80&w=800", "https://images.unsplash.com/photo-1502672260266-1c1c2b4418f8?auto=format&fit=crop&q=80&w=800" }
            },
            new()
            {
                Id = "post-mirpur-twoSeats", Title = "Two seats, newly furnished flat in Mirpur-10",
                Description = "Third floor, lift access, two seats open in a 4-seat flat. Owner lives in the "
                    + "same building. Cooking gas metered separately; everything else shared equally.",
                ListingType = ListingType.MultipleSeats, SeatsAvailable = 2, MonthlyRent = 5200m,
                AreaName = "Mirpur Model", District = "Dhaka", Division = "Dhaka", CreatedAtUtc = now.AddDays(-1).AddHours(-3),
                Eligibility = new EligibilityDto { VerifiedOnly = true },
                ImageUrls = new List<string> { "https://images.unsplash.com/photo-1501183638710-841dd1904471?auto=format&fit=crop&q=80&w=800" }
            },
            new()
            {
                Id = "post-mohammadpur-house", Title = "Entire 2-bed house available, Mohammadpur",
                Description = "Full house handover — current tenants are relocating end of month. "
                    + "Two bedrooms, one common room, small rooftop access. Ideal for a group moving in together.",
                ListingType = ListingType.EntireHouse, SeatsAvailable = 4, MonthlyRent = 18000m,
                AreaName = "Mohammadpur", District = "Dhaka", Division = "Dhaka", CreatedAtUtc = now.AddDays(-2),
                Eligibility = new EligibilityDto(),
                ImageUrls = new List<string> { "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&q=80&w=800", "https://images.unsplash.com/photo-1560185007-cde436f6a4d0?auto=format&fit=crop&q=80&w=800", "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&q=80&w=800" }
            },
            new()
            {
                Id = "post-tongi-seat-mine", Title = "Seat near Tongi Bus Stand",
                Description = "My own listing, kept here so the owner view (My posts, later) has something "
                    + "to show once that page exists.",
                ListingType = ListingType.SingleSeat, SeatsAvailable = 1, MonthlyRent = 4000m,
                AreaName = "Tongi East", District = "Gazipur", Division = "Dhaka", CreatedAtUtc = now.AddDays(-3),
                Eligibility = new EligibilityDto { Occupation = Occupation.Student },
                IsMine = true
            },
            new()
            {
                Id = "post-chattogram-closed", Title = "Seat in Panchlaish (currently closed)",
                Description = "Was open last month, now closed since the seat was filled. Kept in the "
                    + "fixture set to exercise the Closed-post rendering path.",
                ListingType = ListingType.SingleSeat, SeatsAvailable = 1, MonthlyRent = 5000m,
                AreaName = "Panchlaish", District = "Chattogram", Division = "Chattogram", CreatedAtUtc = now.AddDays(-10),
                Status = PostStatus.Closed,
                Eligibility = new EligibilityDto { Gender = Gender.Female }
            },
            new()
            {
                Id = "post-sylhet-seat", Title = "Single seat, walking distance to Shahjalal University",
                Description = "Small, quiet flat with two students already in. Good for someone who "
                    + "wants a calm study environment. Married applicants cannot be accommodated — one bathroom, "
                    + "shared bedroom arrangement.",
                ListingType = ListingType.SingleSeat, SeatsAvailable = 1, MonthlyRent = 3800m,
                AreaName = "Sylhet Sadar", District = "Sylhet", Division = "Sylhet", CreatedAtUtc = now.AddDays(-4),
                Eligibility = new EligibilityDto
                {
                    Gender = Gender.Male, MaritalStatus = MaritalStatus.Single, MinAge = 18, MaxAge = 30
                }
            },
        };
    }

    public async Task<HousingPageDto<HousingPostSummaryDto>> BrowseAsync(HousingPostFilterDto filter)
    {
        // NOTE — mock only: real eligibility (§5.3) is enforced server-side via VisibleTo(viewer),
        // never client-side. This mock simply excludes Closed posts the way an ineligible/closed
        // post would be excluded for a seeker, so the Browse page has a realistic set to render.
        // Only show active posts to everyone - closed posts are managed in MyPosts page only.
        var query = _posts.Where(p => p.Status == PostStatus.Active).AsEnumerable();

        if (filter.ListingType is { } lt)
        {
            query = query.Where(p => p.ListingType == lt);
        }
        if (filter.MaxRent is { } maxRent)
        {
            query = query.Where(p => p.MonthlyRent <= maxRent);
        }
        // The cascade gives ids; posts carry names until post creation stores
        // upazila_id (F2), so the ids are turned back into names here.
        var area = await AreaNames.ResolveAsync(_areas, filter.DivisionId, filter.DistrictId, filter.UpazilaId);

        if (area.Division is not null)
        {
            query = query.Where(p => string.Equals(p.Division, area.Division, StringComparison.OrdinalIgnoreCase));
        }
        if (area.District is not null)
        {
            query = query.Where(p => string.Equals(p.District, area.District, StringComparison.OrdinalIgnoreCase));
        }
        if (area.Upazila is not null)
        {
            query = query.Where(p => string.Equals(p.AreaName, area.Upazila, StringComparison.OrdinalIgnoreCase));
        }

        var all = query.OrderByDescending(p => p.CreatedAtUtc).ToList();
        var total = all.Count;
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Max(filter.PageSize, 1);

        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToSummary)
            .ToList();

        return new HousingPageDto<HousingPostSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public Task<HousingPostDetailDto?> GetPostAsync(string id)
    {
        var post = _posts.FirstOrDefault(p => p.Id == id);

        // A missing id and an ineligible id are indistinguishable by design (§5.3) — both return null,
        // and the page renders the same "not available" state either way.
        return Task.FromResult(post is null ? null : ToDetail(post));
    }

    public async Task<string> CreateAsync(CreateHousingPostRequestDto request)
    {
        var houses = await _houseLookup.GetManageableHousesAsync();
        var house = houses.FirstOrDefault(h => h.Id == request.HouseId);

        var post = new Post
        {
            Id = $"post-{Guid.NewGuid():N}".Substring(0, 13),
            Title = request.Title,
            Description = request.Description,
            ListingType = request.ListingType,
            SeatsAvailable = request.SeatsAvailable,
            MonthlyRent = request.MonthlyRent,
            AreaName = house?.AreaName ?? "Unknown area",
            // The house lookup carries no district yet, so a post created here is
            // reachable by division and upazila but not by the district filter.
            District = string.Empty,
            Division = house?.Division ?? "Unknown division",
            CreatedAtUtc = DateTime.UtcNow,
            Eligibility = request.Eligibility,
            IsMine = true
        };

        _posts.Add(post);
        return post.Id;
    }

    public Task<HousingPostDetailDto?> GetPostForEditAsync(string id)
    {
        var post = _posts.FirstOrDefault(p => p.Id == id && p.IsMine);
        return Task.FromResult(post is null ? null : ToDetail(post));
    }

    public Task<bool> UpdateAsync(string id, UpdateHousingPostRequestDto request)
    {
        var post = _posts.FirstOrDefault(p => p.Id == id && p.IsMine);
        if (post is null)
        {
            return Task.FromResult(false);
        }

        post.Title = request.Title;
        post.Description = request.Description;
        post.ListingType = request.ListingType;
        post.SeatsAvailable = request.SeatsAvailable;
        post.MonthlyRent = request.MonthlyRent;
        post.Eligibility = request.Eligibility;

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<MyHousingPostDto>> GetMineAsync()
    {
        var rows = _posts
            .Where(p => p.IsMine)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p =>
            {
                var bookings = _bookings.Where(b => b.PostId == p.Id).ToList();
                return new MyHousingPostDto
                {
                    Post = ToSummary(p),
                    BookingRequestCount = bookings.Count,
                    PendingBookingRequestCount = bookings.Count(b => b.Status == BookingStatus.Pending)
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<MyHousingPostDto>>(rows);
    }

    public Task<bool> CloseAsync(string id)
    {
        var post = _posts.FirstOrDefault(p => p.Id == id && p.IsMine);
        if (post is null || post.Status != PostStatus.Active)
        {
            return Task.FromResult(false);
        }
        post.Status = PostStatus.Closed;
        return Task.FromResult(true);
    }

    public Task<bool> ReopenAsync(string id)
    {
        var post = _posts.FirstOrDefault(p => p.Id == id && p.IsMine);
        if (post is null || post.Status != PostStatus.Closed)
        {
            return Task.FromResult(false);
        }
        post.Status = PostStatus.Active;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var post = _posts.FirstOrDefault(p => p.Id == id && p.IsMine);
        if (post is null)
        {
            return Task.FromResult(false);
        }
        // Real delete (§3.6), not a soft status — matches the API's hard DELETE + cascade.
        _posts.Remove(post);
        _bookings.RemoveAll(booking => booking.PostId == id);
        return Task.FromResult(true);
    }

    public Task<bool> RequestBookingAsync(string postId, string? message)
    {
        var post = _posts.FirstOrDefault(p => p.Id == postId);
        
        // Cannot book if post doesn't exist, is closed, or is the caller's own
        if (post is null || post.Status != PostStatus.Active || post.IsMine)
        {
            return Task.FromResult(false);
        }

        // Check if a Pending or Accepted request already exists (mock assumes current user is "RequesterName")
        if (_bookings.Any(b => b.PostId == postId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Accepted)))
        {
            return Task.FromResult(false);
        }

        var booking = new Booking
        {
            Id = $"booking-{Guid.NewGuid():N}".Substring(0, 17),
            PostId = postId,
            RequesterName = "Current User",
            RequesterEmail = "user@example.com",
            RequesterPhone = "+8801700000000",
            Status = BookingStatus.Pending,
            Message = message,
            RequestedAtUtc = DateTime.UtcNow
        };

        _bookings.Add(booking);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<BookingRequesterDto>> GetRequestersAsync(string postId)
    {
        var post = _posts.FirstOrDefault(p => p.Id == postId && p.IsMine);
        if (post is null)
        {
            return Task.FromResult<IReadOnlyList<BookingRequesterDto>>(new List<BookingRequesterDto>());
        }

        var requesters = _bookings
            .Where(b => b.PostId == postId)
            .Select(b => new BookingRequesterDto
            {
                BookingId = b.Id,
                RequesterName = b.RequesterName,
                RequestedAtUtc = b.RequestedAtUtc,
                Status = b.Status,
                Message = b.Message
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<BookingRequesterDto>>(requesters);
    }

    public Task<bool> AcceptBookingAsync(string bookingId)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            return Task.FromResult(false);
        }

        // Verify the post belongs to the current user
        var post = _posts.FirstOrDefault(p => p.Id == booking.PostId && p.IsMine);
        if (post is null)
        {
            return Task.FromResult(false);
        }

        booking.Status = BookingStatus.Accepted;
        return Task.FromResult(true);
    }

    public Task<bool> RejectBookingAsync(string bookingId, RejectBookingRequestDto request)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            return Task.FromResult(false);
        }

        // Verify the post belongs to the current user
        var post = _posts.FirstOrDefault(p => p.Id == booking.PostId && p.IsMine);
        if (post is null)
        {
            return Task.FromResult(false);
        }

        booking.Status = BookingStatus.Rejected;
        if (request.Message is not null)
        {
            booking.Message = request.Message;
        }
        return Task.FromResult(true);
    }

    public Task<ContactDisclosureDto?> GetBookingContactAsync(string bookingId)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
        
        // Return contact only if booking is Accepted
        if (booking is null || booking.Status != BookingStatus.Accepted)
        {
            return Task.FromResult<ContactDisclosureDto?>(null);
        }

        var post = _posts.FirstOrDefault(p => p.Id == booking.PostId);
        if (post is null)
        {
            return Task.FromResult<ContactDisclosureDto?>(null);
        }

        // Return contact only if caller is a party to it (owner or requester)
        // In mock, we always allow it if accepted
        var contact = new ContactDisclosureDto
        {
            Name = booking.RequesterName,
            Email = booking.RequesterEmail,
            Phone = booking.RequesterPhone
        };

        return Task.FromResult<ContactDisclosureDto?>(contact);
    }

    public Task<IReadOnlyList<MyBookingDto>> GetMyBookingsAsync()
    {
        // Mock assumes current user is "Current User" (the requester)
        var bookings = _bookings
            .Where(b => b.RequesterName == "Current User")
            .OrderByDescending(b => b.RequestedAtUtc)
            .Select(b =>
            {
                var post = _posts.FirstOrDefault(p => p.Id == b.PostId);
                return new MyBookingDto
                {
                    BookingId = b.Id,
                    Post = post is not null ? ToSummary(post) : new HousingPostSummaryDto(),
                    Status = b.Status,
                    RequestedAtUtc = b.RequestedAtUtc,
                    Message = b.Message,
                    ManagerName = b.Status == BookingStatus.Accepted ? "Manager Name" : null
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<MyBookingDto>>(bookings);
    }

    private static HousingPostDetailDto ToDetail(Post post) => new()
    {
        Id = post.Id,
        Title = post.Title,
        Description = post.Description,
        ListingType = post.ListingType,
        SeatsAvailable = post.SeatsAvailable,
        MonthlyRent = post.MonthlyRent,
        AreaName = post.AreaName,
        Division = post.Division,
        Status = post.Status,
        CreatedAtUtc = post.CreatedAtUtc,
        Eligibility = post.Eligibility,
        IsMine = post.IsMine,
        ImageUrls = post.ImageUrls
    };

    private static HousingPostSummaryDto ToSummary(Post p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        ListingType = p.ListingType,
        SeatsAvailable = p.SeatsAvailable,
        MonthlyRent = p.MonthlyRent,
        AreaName = p.AreaName,
        Division = p.Division,
        Status = p.Status,
        CreatedAtUtc = p.CreatedAtUtc,
        ImageUrls = p.ImageUrls
    };
}