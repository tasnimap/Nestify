using Nestify.Shared.Dtos.Helpers;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MockHelperService : IHelperService
{
    private sealed class Helper
    {
        public required string Id { get; init; }
        public required string Name { get; set; }
        public List<ServiceType> Services { get; set; } = new();
        public decimal MonthlyRate { get; set; }
        public string AvailabilityWindow { get; set; } = string.Empty;
        public required string AreaName { get; set; }
        public DistanceBand? Distance { get; set; }
        public bool IsMine { get; init; }
        public List<ReviewDto> Reviews { get; set; } = new();

        public double RatingAverage => Reviews.Count == 0 ? 0 : Reviews.Average(r => r.Rating);
        public int RatingCount => Reviews.Count;
    }

    private sealed class Engagement
    {
        public required string Id { get; init; }
        public required string HelperId { get; init; }
        public required string HelperName { get; init; }
        public required string ClientName { get; init; }
        public EngagementRole MyRole { get; set; }
        public EngagementStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; init; }
        public bool ClientMarkedComplete { get; set; }
        public bool HelperMarkedComplete { get; set; }
        public bool Reviewed { get; set; }
    }

    private readonly List<Helper> _helpers;
    private readonly List<Engagement> _engagements;

    public MockHelperService()
    {
        var now = DateTime.UtcNow;

        _helpers = new List<Helper>
        {
            new()
            {
                Id = "helper-rina", Name = "Rina Begum",
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
                Id = "helper-shirin", Name = "Shirin Akter",
                Services = new() { ServiceType.Babysitting, ServiceType.ElderCare },
                MonthlyRate = 6000m, AvailabilityWindow = "Sun-Fri, full day",
                AreaName = "Mirpur", Distance = DistanceBand.Within2Km,
                Reviews = new()
            },
            new()
            {
                Id = "helper-jasim", Name = "Jasim Uddin",
                Services = new() { ServiceType.Laundry, ServiceType.General },
                MonthlyRate = 3000m, AvailabilityWindow = "Sat-Thu, evenings",
                AreaName = "Mohammadpur", Distance = DistanceBand.Within5Km,
                Reviews = new()
                {
                    new ReviewDto { ReviewerName = "Fahim", Rating = 3, Comment = "Okay, needed reminders.", CreatedAtUtc = now.AddDays(-5) }
                }
            },
            new()
            {
                Id = "helper-me", Name = "Prapty (My profile)",
                Services = new() { ServiceType.Cooking },
                MonthlyRate = 4000m, AvailabilityWindow = "Sat-Wed, 9am-1pm",
                AreaName = "Uttara", Distance = null,
                IsMine = true, Reviews = new()
            }
        };

        _engagements = new List<Engagement>
        {
            new()
            {
                Id = "eng-1", HelperId = "helper-rina", HelperName = "Rina Begum",
                ClientName = "You", MyRole = EngagementRole.Client,
                Status = EngagementStatus.Requested, CreatedAtUtc = now.AddDays(-1)
            },
            new()
            {
                Id = "eng-2", HelperId = "helper-shirin", HelperName = "Shirin Akter",
                ClientName = "You", MyRole = EngagementRole.Client,
                Status = EngagementStatus.Active, CreatedAtUtc = now.AddDays(-15),
                ClientMarkedComplete = true, HelperMarkedComplete = false
            },
            new()
            {
                Id = "eng-3", HelperId = "helper-jasim", HelperName = "Jasim Uddin",
                ClientName = "You", MyRole = EngagementRole.Client,
                Status = EngagementStatus.Completed, CreatedAtUtc = now.AddDays(-30),
                ClientMarkedComplete = true, HelperMarkedComplete = true
            },
            new()
            {
                Id = "eng-4", HelperId = "helper-me", HelperName = "Prapty (My profile)",
                ClientName = "Nadia", MyRole = EngagementRole.Helper,
                Status = EngagementStatus.HelperConfirmed, CreatedAtUtc = now.AddDays(-3)
            }
        };
    }

    public Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(HelperFilterDto filter)
    {
        var query = _helpers.Where(h => !h.IsMine).AsEnumerable();

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
            .Select(ToSummary)
            .ToList();

        return Task.FromResult(new HelperPageDto<HelperSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public Task<HelperDetailDto?> GetHelperAsync(string id)
    {
        var helper = _helpers.FirstOrDefault(h => h.Id == id);
        return Task.FromResult(helper is null ? null : ToDetail(helper));
    }

    public Task<HelperDetailDto?> GetMyProfileAsync()
    {
        var helper = _helpers.FirstOrDefault(h => h.IsMine);
        return Task.FromResult(helper is null ? null : ToDetail(helper));
    }

    public Task<HelperDetailDto> RegisterAsync(HelperRegistrationDto dto)
    {
        var helper = new Helper
        {
            Id = "helper-me",
            Name = "Prapty (My profile)",
            Services = dto.Services,
            MonthlyRate = dto.MonthlyRate,
            AvailabilityWindow = dto.AvailabilityWindow,
            AreaName = "Uttara",
            IsMine = true
        };

        _helpers.RemoveAll(h => h.IsMine);
        _helpers.Add(helper);

        return Task.FromResult(ToDetail(helper));
    }

    public Task<HelperDetailDto> UpdateProfileAsync(HelperRegistrationDto dto)
    {
        var helper = _helpers.First(h => h.IsMine);
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
        var items = _engagements.Select(ToEngagementDto).ToList();
        return Task.FromResult(items);
    }

    public Task<EngagementDto> RequestEngagementAsync(string helperId)
    {
        var helper = _helpers.First(h => h.Id == helperId);
        var engagement = new Engagement
        {
            Id = Guid.NewGuid().ToString(),
            HelperId = helper.Id,
            HelperName = helper.Name,
            ClientName = "You",
            MyRole = EngagementRole.Client,
            Status = EngagementStatus.Requested,
            CreatedAtUtc = DateTime.UtcNow
        };
        _engagements.Add(engagement);

        return Task.FromResult(ToEngagementDto(engagement));
    }

    public Task<EngagementDto> ConfirmEngagementAsync(string engagementId)
    {
        var engagement = _engagements.First(e => e.Id == engagementId);
        engagement.Status = EngagementStatus.HelperConfirmed;

        return Task.FromResult(ToEngagementDto(engagement));
    }

    public Task<EngagementDto> MarkCompleteAsync(string engagementId)
    {
        var engagement = _engagements.First(e => e.Id == engagementId);

        if (engagement.MyRole == EngagementRole.Client)
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

        return Task.FromResult(ToEngagementDto(engagement));
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

    private static HelperSummaryDto ToSummary(Helper h) => new()
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

    private static HelperDetailDto ToDetail(Helper h) => new()
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
        IsMine = h.IsMine
    };

    private static EngagementDto ToEngagementDto(Engagement e) => new()
    {
        Id = e.Id,
        HelperId = e.HelperId,
        HelperName = e.HelperName,
        ClientName = e.ClientName,
        MyRole = e.MyRole,
        Status = e.Status,
        CreatedAtUtc = e.CreatedAtUtc,
        ClientMarkedComplete = e.ClientMarkedComplete,
        HelperMarkedComplete = e.HelperMarkedComplete,
        CanReview = e.Status == EngagementStatus.Completed && e.MyRole == EngagementRole.Client && !e.Reviewed
    };
}