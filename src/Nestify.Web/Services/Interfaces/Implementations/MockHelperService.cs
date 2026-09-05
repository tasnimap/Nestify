using Nestify.Shared.Dtos.Helpers;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MockHelperService : IHelperService
{
    private sealed class Helper
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }   // <-- replaces IsMine
        public required string Name { get; set; }
        public List<ServiceType> Services { get; set; } = new();
        public decimal MonthlyRate { get; set; }
        public string AvailabilityWindow { get; set; } = string.Empty;
        // AreaName is the upazila or metropolitan thana, spelled as the seeded
        // administrative tables spell it, so the browse filters can match on it.
        public required string AreaName { get; set; }
        public string District { get; set; } = "Dhaka";
        public string Division { get; set; } = "Dhaka";
        public DistanceBand? Distance { get; set; }
        public List<ReviewDto> Reviews { get; set; } = new();

        public double RatingAverage => Reviews.Count == 0 ? 0 : Reviews.Average(r => r.Rating);
        public int RatingCount => Reviews.Count;
    }

    private sealed class Engagement
    {
        public required string Id { get; init; }
        public required string HelperId { get; init; }
        public required string HelperUserId { get; init; }   // who the helper side actually is
        public required string HelperName { get; init; }
        public required string ClientUserId { get; init; }   // who the client side actually is
        public required string ClientName { get; init; }
        public EngagementStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; init; }
        public bool ClientMarkedComplete { get; set; }
        public bool HelperMarkedComplete { get; set; }
        public bool Reviewed { get; set; }
    }

    private readonly ICurrentUserService _currentUser;
    private readonly IAreaService _areas;
    private readonly List<Helper> _helpers;
    private readonly List<Engagement> _engagements;

    public MockHelperService(ICurrentUserService currentUser, IAreaService areas)
    {
        _currentUser = currentUser;
        _areas = areas;
        var now = DateTime.UtcNow;

        _helpers = new List<Helper>
        {
            new()
            {
                Id = "helper-rina", UserId = "user-rina", Name = "Rina Begum",
                Services = new() { ServiceType.Cooking, ServiceType.Cleaning },
                MonthlyRate = 4500m, AvailabilityWindow = "Sat-Thu, 8am-2pm",
                AreaName = "Dhanmondi", Distance = DistanceBand.Within1Km,
                Reviews = new()
                {
                    new ReviewDto { ReviewerName = "Tanvir", Rating = 5, Comment = "Very reliable and punctual.", CreatedAtUtc = now.AddDays(-10) },
                    new ReviewDto { ReviewerName = "Nadia", Rating = 4, Comment = "Good cooking, a bit late once.", CreatedAtUtc = now.AddDays(-20) }
                }
            },
            new()
            {
                Id = "helper-shirin", UserId = "user-shirin", Name = "Shirin Akter",
                Services = new() { ServiceType.Babysitting, ServiceType.ElderCare },
                MonthlyRate = 6000m, AvailabilityWindow = "Sun-Fri, full day",
                AreaName = "Mirpur Model", Distance = DistanceBand.Within2Km,
                Reviews = new()
            },
            new()
            {
                Id = "helper-jasim", UserId = "user-jasim", Name = "Jasim Uddin",
                Services = new() { ServiceType.Laundry, ServiceType.General },
                MonthlyRate = 3000m, AvailabilityWindow = "Sat-Thu, evenings",
                AreaName = "Mohammadpur", Distance = DistanceBand.Within5Km,
                Reviews = new()
                {
                    new ReviewDto { ReviewerName = "Fahim", Rating = 3, Comment = "Okay, needed reminders.", CreatedAtUtc = now.AddDays(-5) }
                }
            },
            // Seed one helper profile per teammate so "SwitchTo" has something to find.
            new()
            {
                Id = "helper-prapty", UserId = "user-prapty", Name = "Prapty",
                Services = new() { ServiceType.Cooking },
                MonthlyRate = 4000m, AvailabilityWindow = "Sat-Wed, 9am-1pm",
                AreaName = "Uttara East", Distance = null,
                Reviews = new()
            },
            new()
            {
                Id = "helper-shreoshi", UserId = "user-shreoshi", Name = "Shreoshi",
                Services = new() { ServiceType.Cleaning, ServiceType.General },
                MonthlyRate = 3500m, AvailabilityWindow = "Fri-Wed, mornings",
                AreaName = "Vatara", Distance = null,
                Reviews = new()
            }
        };

        _engagements = new List<Engagement>
        {
            new()
            {
                Id = "eng-1", HelperId = "helper-rina", HelperUserId = "user-rina", HelperName = "Rina Begum",
                ClientUserId = "user-prapty", ClientName = "Prapty",
                Status = EngagementStatus.Requested, CreatedAtUtc = now.AddDays(-1)
            },
            new()
            {
                Id = "eng-2", HelperId = "helper-shirin", HelperUserId = "user-shirin", HelperName = "Shirin Akter",
                ClientUserId = "user-prapty", ClientName = "Prapty",
                Status = EngagementStatus.Active, CreatedAtUtc = now.AddDays(-15),
                ClientMarkedComplete = true, HelperMarkedComplete = false
            },
            new()
            {
                Id = "eng-3", HelperId = "helper-jasim", HelperUserId = "user-jasim", HelperName = "Jasim Uddin",
                ClientUserId = "user-prapty", ClientName = "Prapty",
                Status = EngagementStatus.Completed, CreatedAtUtc = now.AddDays(-30),
                ClientMarkedComplete = true, HelperMarkedComplete = true
            },
            new()
            {
                Id = "eng-4", HelperId = "helper-prapty", HelperUserId = "user-prapty", HelperName = "Prapty",
                ClientUserId = "user-nadia", ClientName = "Nadia",
                Status = EngagementStatus.HelperConfirmed, CreatedAtUtc = now.AddDays(-3)
            },
            new()
            {
                Id = "eng-5", HelperId = "helper-shreoshi", HelperUserId = "user-shreoshi", HelperName = "Shreoshi",
                ClientUserId = "user-tanvir", ClientName = "Tanvir",
                Status = EngagementStatus.Requested, CreatedAtUtc = now.AddDays(-2)
            }
        };
    }

    public async Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(HelperFilterDto filter)
    {
        var query = _helpers.Where(h => h.UserId != _currentUser.UserId).AsEnumerable();

        // The cascade gives ids; helpers carry names until profiles store
        // upazila_id, so the ids are turned back into names here.
        var area = await AreaNames.ResolveAsync(_areas, filter.DivisionId, filter.DistrictId, filter.UpazilaId);

        if (area.Division is not null)
        {
            query = query.Where(h => string.Equals(h.Division, area.Division, StringComparison.OrdinalIgnoreCase));
        }
        if (area.District is not null)
        {
            query = query.Where(h => string.Equals(h.District, area.District, StringComparison.OrdinalIgnoreCase));
        }
        if (area.Upazila is not null)
        {
            query = query.Where(h => string.Equals(h.AreaName, area.Upazila, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.ServiceType is { } svc)
        {
            query = query.Where(h => h.Services.Contains(svc));
        }
        if (filter.MaxMonthlyRate is { } maxRate)
        {
            query = query.Where(h => h.MonthlyRate <= maxRate);
        }
        if (filter.MinRating is { } minRating)
        {
            query = query.Where(h => h.RatingAverage >= minRating);
        }

        query = filter.Sort switch
        {
            HelperSortOption.RateAsc => query.OrderBy(h => h.MonthlyRate),
            HelperSortOption.RateDesc => query.OrderByDescending(h => h.MonthlyRate),
            HelperSortOption.DistanceAsc => query.OrderBy(h => h.Distance),
            _ => query.OrderByDescending(h => h.RatingAverage)
        };

        var all = query.ToList();
        var total = all.Count;
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Max(filter.PageSize, 1);

        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => ToSummary(h))
            .ToList();

        return new HelperPageDto<HelperSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public Task<HelperDetailDto?> GetHelperAsync(string id)
    {
        var helper = _helpers.FirstOrDefault(h => h.Id == id);
        return Task.FromResult(helper is null ? null : ToDetail(helper));
    }

    public Task<HelperDetailDto?> GetMyProfileAsync()
    {
        var helper = _helpers.FirstOrDefault(h => h.UserId == _currentUser.UserId);
        return Task.FromResult(helper is null ? null : ToDetail(helper));
    }

    public Task<HelperDetailDto> RegisterAsync(HelperRegistrationDto dto)
    {
        // Remove any existing profile for the current user, then add a fresh one.
        _helpers.RemoveAll(h => h.UserId == _currentUser.UserId);

        var helper = new Helper
        {
            Id = $"helper-{_currentUser.UserId}",
            UserId = _currentUser.UserId,
            Name = _currentUser.DisplayName,
            Services = dto.Services,
            MonthlyRate = dto.MonthlyRate,
            AvailabilityWindow = dto.AvailabilityWindow,
            AreaName = "Uttara East"
        };

        _helpers.Add(helper);

        return Task.FromResult(ToDetail(helper));
    }

    public Task<HelperDetailDto> UpdateProfileAsync(HelperRegistrationDto dto)
    {
        var helper = _helpers.First(h => h.UserId == _currentUser.UserId);
        helper.Services = dto.Services;
        helper.MonthlyRate = dto.MonthlyRate;
        helper.AvailabilityWindow = dto.AvailabilityWindow;

        return Task.FromResult(ToDetail(helper));
    }

    public Task<HelperPageDto<ReviewDto>> GetReviewsAsync(string helperId, int page = 1, int pageSize = 5)
    {
        var helper = _helpers.FirstOrDefault(h => h.Id == helperId);
        var all = helper?.Reviews.OrderByDescending(r => r.CreatedAtUtc).ToList() ?? new List<ReviewDto>();
        var total = all.Count;

        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new HelperPageDto<ReviewDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public Task<List<EngagementDto>> GetMyEngagementsAsync()
    {
        var uid = _currentUser.UserId;
        var items = _engagements
            .Where(e => e.ClientUserId == uid || e.HelperUserId == uid)
            .Select(e => ToEngagementDto(e, uid))
            .ToList();

        return Task.FromResult(items);
    }

    public Task<EngagementDto> RequestEngagementAsync(string helperId)
    {
        var helper = _helpers.First(h => h.Id == helperId);
        var engagement = new Engagement
        {
            Id = Guid.NewGuid().ToString(),
            HelperId = helper.Id,
            HelperUserId = helper.UserId,
            HelperName = helper.Name,
            ClientUserId = _currentUser.UserId,
            ClientName = _currentUser.DisplayName,
            Status = EngagementStatus.Requested,
            CreatedAtUtc = DateTime.UtcNow
        };
        _engagements.Add(engagement);

        return Task.FromResult(ToEngagementDto(engagement, _currentUser.UserId));
    }

    public Task<EngagementDto> ConfirmEngagementAsync(string engagementId)
    {
        var engagement = _engagements.First(e => e.Id == engagementId);
        engagement.Status = EngagementStatus.HelperConfirmed;

        return Task.FromResult(ToEngagementDto(engagement, _currentUser.UserId));
    }

    public Task<EngagementDto> MarkCompleteAsync(string engagementId)
    {
        var engagement = _engagements.First(e => e.Id == engagementId);

        if (_currentUser.UserId == engagement.ClientUserId)
        {
            engagement.ClientMarkedComplete = true;
        }
        else
        {
            engagement.HelperMarkedComplete = true;
        }

        if (engagement.ClientMarkedComplete && engagement.HelperMarkedComplete)
        {
            engagement.Status = EngagementStatus.Completed;
        }
        else if (engagement.Status == EngagementStatus.HelperConfirmed)
        {
            engagement.Status = EngagementStatus.Active;
        }

        return Task.FromResult(ToEngagementDto(engagement, _currentUser.UserId));
    }

    public Task SubmitReviewAsync(string engagementId, int rating, string comment)
    {
        var engagement = _engagements.First(e => e.Id == engagementId);
        var helper = _helpers.First(h => h.Id == engagement.HelperId);

        helper.Reviews.Add(new ReviewDto
        {
            ReviewerName = engagement.ClientName,
            Rating = rating,
            Comment = comment,
            CreatedAtUtc = DateTime.UtcNow
        });
        engagement.Reviewed = true;

        return Task.CompletedTask;
    }

    private HelperSummaryDto ToSummary(Helper h) => new()
    {
        Id = h.Id,
        Name = h.Name,
        Services = h.Services,
        MonthlyRate = h.MonthlyRate,
        RatingAverage = h.RatingAverage,
        RatingCount = h.RatingCount,
        AreaName = h.AreaName,
        Distance = h.Distance
    };

    private HelperDetailDto ToDetail(Helper h) => new()
    {
        Id = h.Id,
        Name = h.Name,
        Services = h.Services,
        MonthlyRate = h.MonthlyRate,
        AvailabilityWindow = h.AvailabilityWindow,
        RatingAverage = h.RatingAverage,
        RatingCount = h.RatingCount,
        AreaName = h.AreaName,
        Distance = h.Distance,
        IsMine = h.UserId == _currentUser.UserId
    };

    private static EngagementDto ToEngagementDto(Engagement e, string currentUserId) => new()
    {
        Id = e.Id,
        HelperId = e.HelperId,
        HelperName = e.HelperName,
        ClientName = e.ClientName,
        MyRole = currentUserId == e.HelperUserId ? EngagementRole.Helper : EngagementRole.Client,
        Status = e.Status,
        CreatedAtUtc = e.CreatedAtUtc,
        ClientMarkedComplete = e.ClientMarkedComplete,
        HelperMarkedComplete = e.HelperMarkedComplete,
        CanReview = e.Status == EngagementStatus.Completed
                    && currentUserId == e.ClientUserId
                    && !e.Reviewed
    };
}