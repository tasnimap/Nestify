using Microsoft.AspNetCore.Components.Forms;
using Nestify.Shared.Dtos.Helpers;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MockHelperService : IHelperService
{
    private sealed class Helper
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public required string Name { get; set; }
        public List<ServiceType> Services { get; set; } = new();
        public decimal MonthlyRate { get; set; }
        public string AvailabilityWindow { get; set; } = string.Empty;
        public required string AreaName { get; set; }
        public string District { get; set; } = "Dhaka";
        public string Division { get; set; } = "Dhaka";
        public DistanceBand? Distance { get; set; }
        public bool IsVerified { get; set; }
        public List<ReviewDto> Reviews { get; set; } = new();

        public double RatingAverage =>
            Reviews.Count == 0 ? 0 : Reviews.Average(r => r.Rating);

        public int RatingCount => Reviews.Count;
    }

    private sealed class Engagement
    {
        public required string Id { get; init; }
        public required string HelperId { get; init; }
        public required string HelperUserId { get; init; }
        public required string HelperName { get; init; }
        public required string ClientUserId { get; init; }
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

    public MockHelperService(
        ICurrentUserService currentUser,
        IAreaService areas)
    {
        _currentUser = currentUser;
        _areas = areas;

        var now = DateTime.UtcNow;

        _helpers = new List<Helper>
        {
            new()
            {
                Id = "helper-rina",
                UserId = "user-rina",
                Name = "Rina Begum",
                Services = new()
                {
                    ServiceType.Cooking,
                    ServiceType.Cleaning
                },
                MonthlyRate = 4500m,
                AvailabilityWindow = "Sat-Thu, 8am-2pm",
                AreaName = "Dhanmondi",
                Distance = DistanceBand.Within1Km,
                IsVerified = true,
                Reviews = new()
                {
                    new ReviewDto
                    {
                        ReviewerName = "Tanvir",
                        Rating = 5,
                        Comment = "Very reliable and punctual.",
                        CreatedAtUtc = now.AddDays(-10)
                    },
                    new ReviewDto
                    {
                        ReviewerName = "Nadia",
                        Rating = 4,
                        Comment = "Good cooking, a bit late once.",
                        CreatedAtUtc = now.AddDays(-20)
                    }
                }
            },
            new()
            {
                Id = "helper-shirin",
                UserId = "user-shirin",
                Name = "Shirin Akter",
                Services = new()
                {
                    ServiceType.Babysitting,
                    ServiceType.ElderCare
                },
                MonthlyRate = 6000m,
                AvailabilityWindow = "Sun-Fri, full day",
                AreaName = "Mirpur Model",
                Distance = DistanceBand.Within2Km
            },
            new()
            {
                Id = "helper-jasim",
                UserId = "user-jasim",
                Name = "Jasim Uddin",
                Services = new()
                {
                    ServiceType.Laundry,
                    ServiceType.General
                },
                MonthlyRate = 3000m,
                AvailabilityWindow = "Sat-Thu, evenings",
                AreaName = "Mohammadpur",
                Distance = DistanceBand.Within5Km,
                Reviews = new()
                {
                    new ReviewDto
                    {
                        ReviewerName = "Fahim",
                        Rating = 3,
                        Comment = "Okay, needed reminders.",
                        CreatedAtUtc = now.AddDays(-5)
                    }
                }
            },
            new()
            {
                Id = "helper-prapty",
                UserId = "user-prapty",
                Name = "Prapty",
                Services = new()
                {
                    ServiceType.Cooking
                },
                MonthlyRate = 4000m,
                AvailabilityWindow = "Sat-Wed, 9am-1pm",
                AreaName = "Uttara East",
                Distance = null
            },
            new()
            {
                Id = "helper-shreoshi",
                UserId = "user-shreoshi",
                Name = "Shreoshi",
                Services = new()
                {
                    ServiceType.Cleaning,
                    ServiceType.General
                },
                MonthlyRate = 3500m,
                AvailabilityWindow = "Fri-Wed, mornings",
                AreaName = "Vatara",
                Distance = null
            }
        };

        _engagements = new List<Engagement>
        {
            new()
            {
                Id = "eng-1",
                HelperId = "helper-rina",
                HelperUserId = "user-rina",
                HelperName = "Rina Begum",
                ClientUserId = "user-prapty",
                ClientName = "Prapty",
                Status = EngagementStatus.Requested,
                CreatedAtUtc = now.AddDays(-1)
            },
            new()
            {
                Id = "eng-2",
                HelperId = "helper-shirin",
                HelperUserId = "user-shirin",
                HelperName = "Shirin Akter",
                ClientUserId = "user-prapty",
                ClientName = "Prapty",
                Status = EngagementStatus.Active,
                CreatedAtUtc = now.AddDays(-15),
                ClientMarkedComplete = true
            },
            new()
            {
                Id = "eng-3",
                HelperId = "helper-jasim",
                HelperUserId = "user-jasim",
                HelperName = "Jasim Uddin",
                ClientUserId = "user-prapty",
                ClientName = "Prapty",
                Status = EngagementStatus.Completed,
                CreatedAtUtc = now.AddDays(-30),
                ClientMarkedComplete = true,
                HelperMarkedComplete = true
            },
            new()
            {
                Id = "eng-4",
                HelperId = "helper-prapty",
                HelperUserId = "user-prapty",
                HelperName = "Prapty",
                ClientUserId = "user-nadia",
                ClientName = "Nadia",
                Status = EngagementStatus.HelperConfirmed,
                CreatedAtUtc = now.AddDays(-3)
            },
            new()
            {
                Id = "eng-5",
                HelperId = "helper-shreoshi",
                HelperUserId = "user-shreoshi",
                HelperName = "Shreoshi",
                ClientUserId = "user-tanvir",
                ClientName = "Tanvir",
                Status = EngagementStatus.Requested,
                CreatedAtUtc = now.AddDays(-2)
            }
        };
    }

    public async Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(
        HelperFilterDto filter)
    {
        var query = _helpers
            .Where(h => h.UserId != _currentUser.UserId)
            .AsEnumerable();

        var area = await AreaNames.ResolveAsync(
            _areas,
            filter.DivisionId,
            filter.DistrictId,
            filter.UpazilaId);

        if (area.Division is not null)
        {
            query = query.Where(h =>
                string.Equals(
                    h.Division,
                    area.Division,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (area.District is not null)
        {
            query = query.Where(h =>
                string.Equals(
                    h.District,
                    area.District,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (area.Upazila is not null)
        {
            query = query.Where(h =>
                string.Equals(
                    h.AreaName,
                    area.Upazila,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (filter.ServiceType is { } serviceType)
        {
            query = query.Where(h => h.Services.Contains(serviceType));
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
            HelperSortOption.RateAsc =>
                query.OrderBy(h => h.MonthlyRate),

            HelperSortOption.RateDesc =>
                query.OrderByDescending(h => h.MonthlyRate),

            HelperSortOption.DistanceAsc =>
                query.OrderBy(h => h.Distance),

            _ =>
                query.OrderByDescending(h => h.RatingAverage)
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

        return Task.FromResult(
            helper is null ? null : ToDetail(helper));
    }

    public Task<HelperDetailDto?> GetMyProfileAsync()
    {
        var helper = _helpers.FirstOrDefault(
            h => h.UserId == _currentUser.UserId);

        return Task.FromResult(
            helper is null ? null : ToDetail(helper));
    }

    public Task<HelperDetailDto> RegisterAsync(
        HelperRegistrationDto dto)
    {
        _helpers.RemoveAll(
            h => h.UserId == _currentUser.UserId);

        var helper = new Helper
        {
            Id = $"helper-{_currentUser.UserId}",
            UserId = _currentUser.UserId,
            Name = _currentUser.DisplayName,
            Services = dto.Services,
            MonthlyRate = dto.MonthlyRate,
            AvailabilityWindow = dto.AvailabilityWindow,
            AreaName = "Uttara East",
            IsVerified = false
        };

        _helpers.Add(helper);

        return Task.FromResult(ToDetail(helper));
    }

    public Task<HelperDetailDto> UpdateProfileAsync(
        HelperRegistrationDto dto)
    {
        var helper = _helpers.First(
            h => h.UserId == _currentUser.UserId);

        helper.Services = dto.Services;
        helper.MonthlyRate = dto.MonthlyRate;
        helper.AvailabilityWindow = dto.AvailabilityWindow;

        return Task.FromResult(ToDetail(helper));
    }

    public Task<HelperPageDto<ReviewDto>> GetReviewsAsync(
        string helperId,
        int page = 1,
        int pageSize = 5)
    {
        var helper = _helpers.FirstOrDefault(
            h => h.Id == helperId);

        var all = helper?.Reviews
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList()
            ?? new List<ReviewDto>();

        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new HelperPageDto<ReviewDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = all.Count
        });
    }

    public Task<List<EngagementDto>> GetMyEngagementsAsync()
    {
        var userId = _currentUser.UserId;

        var items = _engagements
            .Where(e =>
                e.ClientUserId == userId ||
                e.HelperUserId == userId)
            .Select(e => ToEngagementDto(e, userId))
            .ToList();

        return Task.FromResult(items);
    }

    public Task<EngagementDto> RequestEngagementAsync(
        string helperId,
        IReadOnlyList<EngagementSlotDto>? slots = null)
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

        return Task.FromResult(
            ToEngagementDto(engagement, _currentUser.UserId));
    }

    public Task<EngagementDto> ConfirmEngagementAsync(
        string engagementId)
    {
        var engagement = _engagements.First(
            e => e.Id == engagementId);

        engagement.Status = EngagementStatus.HelperConfirmed;

        return Task.FromResult(
            ToEngagementDto(engagement, _currentUser.UserId));
    }

    public Task<EngagementDto> MarkCompleteAsync(
        string engagementId)
    {
        var engagement = _engagements.First(
            e => e.Id == engagementId);

        if (_currentUser.UserId == engagement.ClientUserId)
        {
            engagement.ClientMarkedComplete = true;
        }
        else
        {
            engagement.HelperMarkedComplete = true;
        }

        if (engagement.ClientMarkedComplete &&
            engagement.HelperMarkedComplete)
        {
            engagement.Status = EngagementStatus.Completed;
        }
        else if (engagement.Status == EngagementStatus.HelperConfirmed)
        {
            engagement.Status = EngagementStatus.Active;
        }

        return Task.FromResult(
            ToEngagementDto(engagement, _currentUser.UserId));
    }

    public Task SubmitReviewAsync(
        string engagementId,
        int rating,
        string comment)
    {
        var engagement = _engagements.First(
            e => e.Id == engagementId);

        var helper = _helpers.First(
            h => h.Id == engagement.HelperId);

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

    public Task SubmitVerificationAsync(
        string documentType,
        IBrowserFile file)
    {
        // Mock mode does not persist uploaded documents.
        return Task.CompletedTask;
    }

    private HelperSummaryDto ToSummary(Helper helper)
    {
        return new HelperSummaryDto
        {
            Id = helper.Id,
            Name = helper.Name,
            Services = helper.Services,
            MonthlyRate = helper.MonthlyRate,
            RatingAverage = helper.RatingAverage,
            RatingCount = helper.RatingCount,
            AreaName = helper.AreaName,
            Distance = helper.Distance,
            IsVerified = helper.IsVerified
        };
    }

    private HelperDetailDto ToDetail(Helper helper)
    {
        return new HelperDetailDto
        {
            Id = helper.Id,
            Name = helper.Name,
            Services = helper.Services,
            MonthlyRate = helper.MonthlyRate,
            AvailabilityWindow = helper.AvailabilityWindow,
            RatingAverage = helper.RatingAverage,
            RatingCount = helper.RatingCount,
            AreaName = helper.AreaName,
            Distance = helper.Distance,
            IsMine = helper.UserId == _currentUser.UserId,
            IsVerified = helper.IsVerified
        };
    }

    private static EngagementDto ToEngagementDto(
        Engagement engagement,
        string currentUserId)
    {
        return new EngagementDto
        {
            Id = engagement.Id,
            HelperId = engagement.HelperId,
            HelperName = engagement.HelperName,
            ClientName = engagement.ClientName,
            MyRole = currentUserId == engagement.HelperUserId
                ? EngagementRole.Helper
                : EngagementRole.Client,
            Status = engagement.Status,
            CreatedAtUtc = engagement.CreatedAtUtc,
            ClientMarkedComplete = engagement.ClientMarkedComplete,
            HelperMarkedComplete = engagement.HelperMarkedComplete,
            CanReview =
                engagement.Status == EngagementStatus.Completed &&
                currentUserId == engagement.ClientUserId &&
                !engagement.Reviewed
        };
    }
}