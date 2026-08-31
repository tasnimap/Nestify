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
        public required string AreaName { get; set; }
        public required string Division { get; set; }
        public PostStatus Status { get; set; } = PostStatus.Active;
        public DateTime CreatedAtUtc { get; set; }
        public EligibilityDto Eligibility { get; set; } = new();
        public bool IsMine { get; init; }
    }

    private readonly List<Post> _posts;

    public MockHousingService()
    {
        var now = DateTime.UtcNow;

        _posts = new List<Post>
        {
            new()
            {
                Id = "post-dhanmondi-seat", Title = "One seat in a 3-bed flat, Dhanmondi",
                Description = "Quiet mess near Dhanmondi 27. Two current tenants are AUST/DU students. "
                    + "Wifi, gas, and a part-time cleaner included in rent. Attached bath for the vacant seat.",
                ListingType = ListingType.SingleSeat, SeatsAvailable = 1, MonthlyRent = 6500m,
                AreaName = "Dhanmondi", Division = "Dhaka", CreatedAtUtc = now.AddHours(-5),
                Eligibility = new EligibilityDto { Gender = Gender.Male, StudentOnly = true, MaxAge = 27 }
            },
            new()
            {
                Id = "post-mirpur-twoSeats", Title = "Two seats, newly furnished flat in Mirpur-10",
                Description = "Third floor, lift access, two seats open in a 4-seat flat. Owner lives in the "
                    + "same building. Cooking gas metered separately; everything else shared equally.",
                ListingType = ListingType.MultipleSeats, SeatsAvailable = 2, MonthlyRent = 5200m,
                AreaName = "Mirpur", Division = "Dhaka", CreatedAtUtc = now.AddDays(-1).AddHours(-3),
                Eligibility = new EligibilityDto { VerifiedOnly = true }
            },
            new()
            {
                Id = "post-mohammadpur-house", Title = "Entire 2-bed house available, Mohammadpur",
                Description = "Full house handover — current tenants are relocating end of month. "
                    + "Two bedrooms, one common room, small rooftop access. Ideal for a group moving in together.",
                ListingType = ListingType.EntireHouse, SeatsAvailable = 4, MonthlyRent = 18000m,
                AreaName = "Mohammadpur", Division = "Dhaka", CreatedAtUtc = now.AddDays(-2),
                Eligibility = new EligibilityDto()
            },
            new()
            {
                Id = "post-tongi-seat-mine", Title = "Seat near Tongi Bus Stand",
                Description = "My own listing, kept here so the owner view (My posts, later) has something "
                    + "to show once that page exists.",
                ListingType = ListingType.SingleSeat, SeatsAvailable = 1, MonthlyRent = 4000m,
                AreaName = "Tongi", Division = "Dhaka", CreatedAtUtc = now.AddDays(-3),
                Eligibility = new EligibilityDto { Occupation = Occupation.Student },
                IsMine = true
            },
            new()
            {
                Id = "post-chattogram-closed", Title = "Seat in Panchlaish (currently closed)",
                Description = "Was open last month, now closed since the seat was filled. Kept in the "
                    + "fixture set to exercise the Closed-post rendering path.",
                ListingType = ListingType.SingleSeat, SeatsAvailable = 1, MonthlyRent = 5000m,
                AreaName = "Panchlaish", Division = "Chattogram", CreatedAtUtc = now.AddDays(-10),
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
                AreaName = "Sylhet Sadar", Division = "Sylhet", CreatedAtUtc = now.AddDays(-4),
                Eligibility = new EligibilityDto
                {
                    Gender = Gender.Male, MaritalStatus = MaritalStatus.Single, MinAge = 18, MaxAge = 30
                }
            },
        };
    }

    public Task<HousingPageDto<HousingPostSummaryDto>> BrowseAsync(HousingPostFilterDto filter)
    {
        var query = _posts.Where(p => p.Status == PostStatus.Active || p.IsMine).AsEnumerable();

        if (filter.ListingType is { } lt)
        {
            query = query.Where(p => p.ListingType == lt);
        }
        if (filter.MaxRent is { } maxRent)
        {
            query = query.Where(p => p.MonthlyRent <= maxRent);
        }
        if (filter.DivisionId is not null)
        {
            // Mock has no real division/district/upazila ids wired to posts yet — once AreaCascade's
            // ids are threaded through post creation (F2), filter on UpazilaId here instead of Division name.
            var divisionName = filter.DivisionId switch { 1 => "Dhaka", 2 => "Chattogram", 3 => "Sylhet", _ => null };
            if (divisionName is not null)
            {
                query = query.Where(p => p.Division == divisionName);
            }
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

        return Task.FromResult(new HousingPageDto<HousingPostSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public Task<HousingPostDetailDto?> GetPostAsync(string id)
    {
        var post = _posts.FirstOrDefault(p => p.Id == id);

        // A missing id and an ineligible id are indistinguishable by design (§5.3) — both return null,
        // and the page renders the same "not available" state either way.
        if (post is null)
        {
            return Task.FromResult<HousingPostDetailDto?>(null);
        }

        return Task.FromResult<HousingPostDetailDto?>(new HousingPostDetailDto
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
            IsMine = post.IsMine
        });
    }

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
        CreatedAtUtc = p.CreatedAtUtc
    };
}