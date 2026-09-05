// src/Nestify.Web/Services/Implementations/MockMarketplaceService.cs
// In-memory fixtures for the frontend phase. Deliberately covers the awkward
// cases: an empty search result, a single-result search, a full page, a
// not-found id, a sold listing, an already-answered interest.
using Nestify.Shared.Dtos.Marketplace;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MockMarketplaceService : IMarketplaceService
{
    // The signed-in user, as far as the mock is concerned.
    private const string MeId = "seller-you";
    private const string MeName = "You";

    private sealed class Item
    {
        public required string Id { get; init; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public decimal PriceBdt { get; set; }
        public MarketplaceCategory Category { get; set; }
        public ItemCondition Condition { get; set; }
        public required string Division { get; set; }
        public required string AreaName { get; set; }
        public DateTime PostedAtUtc { get; set; }
        public ListingStatus Status { get; set; } = ListingStatus.Active;
        public required string SellerId { get; init; }
        public required string SellerName { get; init; }
        public bool SellerVerified { get; init; }
        public DateTime SellerJoinedUtc { get; init; }
        public List<string> Images { get; set; } = new();
        public int ViewCount { get; set; }
    }

    private sealed class Interest
    {
        public required string Id { get; init; }
        public required string ItemId { get; init; }
        public required string BuyerId { get; init; }
        public required string BuyerName { get; init; }
        public bool BuyerVerified { get; init; }
        public required string Message { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public BuyInterestStatus Status { get; set; } = BuyInterestStatus.Pending;
        public string? BuyerPhone { get; init; }
        public string? SellerPhone { get; init; }
        public string? HandoverArea { get; init; }
    }

    private readonly List<Item> _items;
    private readonly List<Interest> _interests;
    private int _idSeed = 100;

    public MockMarketplaceService()
    {
        var now = DateTime.UtcNow;

        _items = new List<Item>
        {
            new()
            {
                Id = "itm-studytable", Title = "Solid wood study table with drawer",
                Description = "Bought two years ago from Hatil. One deep drawer, cable slot at the back, "
                    + "no wobble. Selling because I am moving to a smaller room. Minor scuff on the left leg, "
                    + "shown in the last photo.",
                PriceBdt = 3200m, Category = MarketplaceCategory.Furniture, Condition = ItemCondition.Good,
                Division = "Dhaka", AreaName = "New Market, Dhaka", PostedAtUtc = now.AddHours(-6),
                SellerId = "seller-arif", SellerName = "Arif Mahmud", SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-14),
                Images = { "tile:studytable-a", "tile:studytable-b", "tile:studytable-c" }, ViewCount = 41
            },
            new()
            {
                Id = "itm-cycle", Title = "Duranta single-speed cycle, 26 inch",
                Description = "Daily commuter for one year. New tube and brake pads last month. "
                    + "Lock and light included. Ride it away from Fuller Road.",
                PriceBdt = 4500m, Category = MarketplaceCategory.Other, Condition = ItemCondition.Good,
                Division = "Dhaka", AreaName = "Shahbag, Dhaka", PostedAtUtc = now.AddDays(-1).AddHours(-2),
                SellerId = "seller-tuhin", SellerName = "Tuhin Rahman", SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-8),
                Images = { "tile:cycle-a", "tile:cycle-b" }, ViewCount = 88
            },
            new()
            {
                Id = "itm-riceco", Title = "Miyako rice cooker 1.8L",
                Description = "Cooks for four. Non-stick pot intact, spatula and measuring cup included. "
                    + "Works perfectly, just upgraded to a bigger one.",
                PriceBdt = 1400m, Category = MarketplaceCategory.Appliances, Condition = ItemCondition.LikeNew,
                Division = "Dhaka", AreaName = "Chackbazar, Dhaka", PostedAtUtc = now.AddDays(-2),
                SellerId = "seller-nabila", SellerName = "Nabila Haque", SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-3),
                Images = { "tile:ricecooker-a" }, ViewCount = 23
            },
            new()
            {
                Id = "itm-books-cse", Title = "CSE first-year bundle: Deitel, Rosen, Thomas",
                Description = "Three hardcovers. Rosen has some highlighting in the first four chapters, "
                    + "the rest are clean. No torn pages. Price is for all three together.",
                PriceBdt = 1800m, Category = MarketplaceCategory.Books, Condition = ItemCondition.Fair,
                Division = "Dhaka", AreaName = "Shahbag, Dhaka", PostedAtUtc = now.AddDays(-3).AddHours(-5),
                SellerId = "seller-arif", SellerName = "Arif Mahmud", SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-14),
                Images = { "tile:books-a", "tile:books-b" }, ViewCount = 52
            },
            new()
            {
                Id = "itm-monitor", Title = "Dell 22 inch IPS monitor",
                Description = "1080p, HDMI and VGA. No dead pixels, no backlight bleed worth mentioning. "
                    + "Stand and power cable included, HDMI cable not included.",
                PriceBdt = 7200m, Category = MarketplaceCategory.Electronics, Condition = ItemCondition.Good,
                Division = "Chattogram", AreaName = "Chawkbazar, Chattogram", PostedAtUtc = now.AddDays(-4),
                SellerId = "seller-farhan", SellerName = "Farhan Kabir", SellerVerified = false,
                SellerJoinedUtc = now.AddMonths(-1),
                Images = { "tile:monitor-a", "tile:monitor-b" }, ViewCount = 64
            },
            new()
            {
                Id = "itm-mattress", Title = "Single foam mattress 3ft, 6 months used",
                Description = "Clean, no stains, kept with a cover from day one. Firm side still firm. "
                    + "Selling with the cover.",
                PriceBdt = 2600m, Category = MarketplaceCategory.Bedding, Condition = ItemCondition.LikeNew,
                Division = "Dhaka", AreaName = "Lalbagh, Dhaka", PostedAtUtc = now.AddDays(-5),
                SellerId = "seller-tuhin", SellerName = "Tuhin Rahman", SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-8),
                Images = { "tile:mattress-a" }, ViewCount = 30
            },
            new()
            {
                Id = "itm-kettle", Title = "Electric kettle 1.5L, stainless",
                Description = "Auto cut-off works. A bit of scale at the bottom, comes off with vinegar. "
                    + "Two months old.",
                PriceBdt = 650m, Category = MarketplaceCategory.Kitchen, Condition = ItemCondition.Good,
                Division = "Dhaka", AreaName = "Lalbagh, Dhaka", PostedAtUtc = now.AddDays(-6).AddHours(-3),
                SellerId = "seller-nabila", SellerName = "Nabila Haque", SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-3),
                Images = { "tile:kettle-a" }, ViewCount = 12
            },
            new()
            {
                Id = "itm-almirah", Title = "Steel almirah, three shelves + locker",
                Description = "Heavy, solid, lock and key present. Some paint chipping on top which you "
                    + "will never see. You arrange the pickup van.",
                PriceBdt = 5500m, Category = MarketplaceCategory.Furniture, Condition = ItemCondition.Fair,
                Division = "Dhaka", AreaName = "Kamrangirchar, Dhaka", PostedAtUtc = now.AddDays(-8),
                SellerId = "seller-farhan", SellerName = "Farhan Kabir", SellerVerified = false,
                SellerJoinedUtc = now.AddMonths(-1),
                Images = { "tile:almirah-a", "tile:almirah-b" }, ViewCount = 19
            },
            new()
            {
                Id = "itm-fan", Title = "Vision ceiling fan 56 inch",
                Description = "Runs quiet, no wobble, full speed. Regulator included. Uninstalled and ready.",
                PriceBdt = 1900m, Category = MarketplaceCategory.Appliances, Condition = ItemCondition.Good,
                Division = "Dhaka", AreaName = "Chackbazar, Dhaka", PostedAtUtc = now.AddDays(-9),
                SellerId = "seller-arif", SellerName = "Arif Mahmud", SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-14),
                Images = { "tile:fan-a" }, ViewCount = 27
            },
            new()
            {
                Id = "itm-guitar", Title = "Acoustic guitar with soft case",
                Description = "Beginner guitar, Yamaha-copy. Stays in tune, no fret buzz. Soft case has a "
                    + "broken zipper on the front pocket only.",
                PriceBdt = 3800m, Category = MarketplaceCategory.Other, Condition = ItemCondition.Good,
                Division = "Sylhet", AreaName = "Kotwali Model, Sylhet", PostedAtUtc = now.AddDays(-11),
                SellerId = "seller-farhan", SellerName = "Farhan Kabir", SellerVerified = false,
                SellerJoinedUtc = now.AddMonths(-1),
                Images = { "tile:guitar-a", "tile:guitar-b" }, ViewCount = 45
            },

            // ---- Sold listing: still reachable by URL, absent from the default grid ----
            new()
            {
                Id = "itm-heater", Title = "Room heater 2000W (SOLD)",
                Description = "Two heat settings, tip-over cut-off. Sold to a buyer from Azimpur.",
                PriceBdt = 2200m, Category = MarketplaceCategory.Appliances, Condition = ItemCondition.Good,
                Division = "Dhaka", AreaName = "Lalbagh, Dhaka", PostedAtUtc = now.AddDays(-15),
                Status = ListingStatus.Sold,
                SellerId = "seller-tuhin", SellerName = "Tuhin Rahman", SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-8),
                Images = { "tile:heater-a" }, ViewCount = 120
            },

            // ---- Two listings owned by the signed-in user ----
            new()
            {
                Id = "itm-mydesk", Title = "IKEA-style folding desk, white",
                Description = "Folds flat against the wall. Two years old, one small ink mark. "
                    + "Screws and allen key included.",
                PriceBdt = 2400m, Category = MarketplaceCategory.Furniture, Condition = ItemCondition.Good,
                Division = "Dhaka", AreaName = "Bangshal, Dhaka", PostedAtUtc = now.AddDays(-2).AddHours(-6),
                SellerId = MeId, SellerName = MeName, SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-6),
                Images = { "tile:mydesk-a", "tile:mydesk-b" }, ViewCount = 58
            },
            new()
            {
                Id = "itm-myprinter", Title = "HP DeskJet 2130 all-in-one",
                Description = "Prints, scans, copies. Comes with a half-full black cartridge and a new "
                    + "colour one still sealed.",
                PriceBdt = 3100m, Category = MarketplaceCategory.Electronics, Condition = ItemCondition.LikeNew,
                Division = "Dhaka", AreaName = "Bangshal, Dhaka", PostedAtUtc = now.AddDays(-10),
                SellerId = MeId, SellerName = MeName, SellerVerified = true,
                SellerJoinedUtc = now.AddMonths(-6),
                Images = { "tile:myprinter-a" }, ViewCount = 33
            }
        };

        _interests = new List<Interest>
        {
            new()
            {
                Id = "int-1", ItemId = "itm-mydesk", BuyerId = "buyer-sami", BuyerName = "Samiul Islam",
                BuyerVerified = true, Message = "Is the ink mark on the top surface or the side? "
                    + "I can pick up this weekend from Nazira Bazar.",
                CreatedAtUtc = now.AddDays(-1).AddHours(-4), Status = BuyInterestStatus.Pending,
                BuyerPhone = "+8801711-000111"
            },
            new()
            {
                Id = "int-2", ItemId = "itm-mydesk", BuyerId = "buyer-rima", BuyerName = "Rima Sultana",
                BuyerVerified = false, Message = "Can you hold it till Thursday? Price is fine.",
                CreatedAtUtc = now.AddDays(-1), Status = BuyInterestStatus.Pending,
                BuyerPhone = "+8801822-222333"
            },
            new()
            {
                Id = "int-3", ItemId = "itm-mydesk", BuyerId = "buyer-jony", BuyerName = "Jony Ahsan",
                BuyerVerified = true, Message = "Taking it. Sharing my number for handover.",
                CreatedAtUtc = now.AddDays(-2), Status = BuyInterestStatus.Accepted,
                BuyerPhone = "+8801933-444555", SellerPhone = "+8801700-999888",
                HandoverArea = "Nazira Bazar mor, near the pharmacy"
            },
            new()
            {
                Id = "int-4", ItemId = "itm-myprinter", BuyerId = "buyer-tania", BuyerName = "Tania Noor",
                BuyerVerified = true, Message = "Does the sealed colour cartridge have an expiry printed on it?",
                CreatedAtUtc = now.AddDays(-3), Status = BuyInterestStatus.Declined,
                BuyerPhone = "+8801611-777666"
            },

            // ---- The signed-in user's own outgoing interests (for /buy-interests/mine) ----
            new()
            {
                Id = "int-mine-1", ItemId = "itm-studytable", BuyerId = MeId, BuyerName = MeName,
                BuyerVerified = true, Message = "Interested. Is the scuff structural or just cosmetic?",
                CreatedAtUtc = now.AddHours(-3), Status = BuyInterestStatus.Pending
            },
            new()
            {
                Id = "int-mine-2", ItemId = "itm-monitor", BuyerId = MeId, BuyerName = MeName,
                BuyerVerified = true, Message = "Can meet at Chawkbazar tomorrow evening. Holding at asking price?",
                CreatedAtUtc = now.AddDays(-1).AddHours(-1), Status = BuyInterestStatus.Accepted,
                SellerPhone = "+8801555-121212", HandoverArea = "Chawkbazar, in front of the mosque"
            },
            new()
            {
                Id = "int-mine-3", ItemId = "itm-cycle", BuyerId = MeId, BuyerName = MeName,
                BuyerVerified = true, Message = "Was asking about the frame size — never mind, found one closer.",
                CreatedAtUtc = now.AddDays(-2), Status = BuyInterestStatus.Withdrawn
            }
        };
    }

    // ---------------------------------------------------------------- browse

    public Task<MarketplacePageDto<MarketplaceItemSummaryDto>> BrowseAsync(MarketplaceItemFilterDto filter)
    {
        IEnumerable<Item> query = _items.Where(i => i.Status == ListingStatus.Active && i.SellerId != MeId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(i =>
                i.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.AreaName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Category is { } category)
            query = query.Where(i => i.Category == category);

        if (filter.Condition is { } condition)
            query = query.Where(i => i.Condition == condition);

        if (filter.MinPrice is { } min)
            query = query.Where(i => i.PriceBdt >= min);

        if (filter.MaxPrice is { } max)
            query = query.Where(i => i.PriceBdt <= max);

        if (!string.IsNullOrWhiteSpace(filter.Division))
            query = query.Where(i => string.Equals(i.Division, filter.Division, StringComparison.OrdinalIgnoreCase));

        // An item's AreaName is written "upazila, district", so the two narrower
        // filters read the halves of it. The real API will filter on upazila_id.
        if (!string.IsNullOrWhiteSpace(filter.District))
            query = query.Where(i => string.Equals(DistrictOf(i.AreaName), filter.District, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter.Upazila))
            query = query.Where(i => string.Equals(UpazilaOf(i.AreaName), filter.Upazila, StringComparison.OrdinalIgnoreCase));

        query = filter.Sort switch
        {
            MarketplaceSort.PriceLowToHigh => query.OrderBy(i => i.PriceBdt).ThenByDescending(i => i.PostedAtUtc),
            MarketplaceSort.PriceHighToLow => query.OrderByDescending(i => i.PriceBdt).ThenByDescending(i => i.PostedAtUtc),
            _ => query.OrderByDescending(i => i.PostedAtUtc)
        };

        var all = query.ToList();
        var page = Math.Max(1, filter.Page);
        var size = filter.PageSize <= 0 ? 12 : filter.PageSize;

        var slice = all
            .Skip((page - 1) * size)
            .Take(size)
            .Select(ToSummary)
            .ToList();

        var result = new MarketplacePageDto<MarketplaceItemSummaryDto>
        {
            Items = slice,
            Page = page,
            PageSize = size,
            TotalCount = all.Count
        };

        return Task.FromResult(result);
    }

    public Task<MarketplaceItemDetailDto?> GetItemAsync(string id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null)
            return Task.FromResult<MarketplaceItemDetailDto?>(null);

        item.ViewCount++;

        var dto = new MarketplaceItemDetailDto
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            PriceBdt = item.PriceBdt,
            Category = item.Category,
            Condition = item.Condition,
            AreaName = item.AreaName,
            Division = item.Division,
            PostedAtUtc = item.PostedAtUtc,
            Status = item.Status,
            Images = item.Images.ToList(),
            SellerId = item.SellerId,
            SellerDisplayName = item.SellerName,
            SellerVerified = item.SellerVerified,
            SellerJoinedUtc = item.SellerJoinedUtc,
            IsMine = item.SellerId == MeId,
            HasActiveInterest = _interests.Any(x =>
                x.ItemId == item.Id && x.BuyerId == MeId &&
                x.Status is BuyInterestStatus.Pending or BuyInterestStatus.Accepted)
        };

        return Task.FromResult<MarketplaceItemDetailDto?>(dto);
    }

    // ---------------------------------------------------------- create / edit

    public Task<string> CreateItemAsync(CreateMarketplaceItemDto dto)
    {
        var id = $"itm-new-{_idSeed++}";
        _items.Insert(0, new Item
        {
            Id = id,
            Title = dto.Title,
            Description = dto.Description,
            PriceBdt = dto.PriceBdt,
            Category = dto.Category,
            Condition = dto.Condition,
            Division = dto.Division,
            AreaName = dto.AreaName,
            PostedAtUtc = DateTime.UtcNow,
            Status = ListingStatus.Active,
            SellerId = MeId,
            SellerName = MeName,
            SellerVerified = true,
            SellerJoinedUtc = DateTime.UtcNow.AddMonths(-6),
            Images = dto.Images.Count > 0 ? dto.Images.ToList() : new List<string> { "tile:new-listing" }
        });
        return Task.FromResult(id);
    }

    public Task<MarketplaceItemDetailDto?> GetItemForEditAsync(string id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id && i.SellerId == MeId);
        return item is null
            ? Task.FromResult<MarketplaceItemDetailDto?>(null)
            : GetItemAsync(id);
    }

    public Task<bool> UpdateItemAsync(string id, UpdateMarketplaceItemDto dto)
    {
        var item = _items.FirstOrDefault(i => i.Id == id && i.SellerId == MeId);
        if (item is null) return Task.FromResult(false);

        item.Title = dto.Title;
        item.Description = dto.Description;
        item.PriceBdt = dto.PriceBdt;
        item.Category = dto.Category;
        item.Condition = dto.Condition;
        item.Division = dto.Division;
        item.AreaName = dto.AreaName;
        if (dto.Images.Count > 0) item.Images = dto.Images.ToList();
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<MyListingDto>> GetMyListingsAsync()
    {
        var rows = _items
            .Where(i => i.SellerId == MeId)
            .OrderByDescending(i => i.PostedAtUtc)
            .Select(i => new MyListingDto
            {
                Item = ToSummary(i),
                InterestCount = _interests.Count(x => x.ItemId == i.Id && x.Status != BuyInterestStatus.Withdrawn),
                PendingInterestCount = _interests.Count(x => x.ItemId == i.Id && x.Status == BuyInterestStatus.Pending),
                ViewCount = i.ViewCount
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<MyListingDto>>(rows);
    }

    public Task<bool> MarkSoldAsync(string id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id && i.SellerId == MeId);
        if (item is null || item.Status != ListingStatus.Active) return Task.FromResult(false);
        item.Status = ListingStatus.Sold;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteItemAsync(string id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id && i.SellerId == MeId);
        if (item is null) return Task.FromResult(false);
        item.Status = ListingStatus.Removed;
        return Task.FromResult(true);
    }

    public Task<PriceSuggestionDto?> GetPriceSuggestionAsync(MarketplaceCategory category, ItemCondition condition)
    {
        var comparables = _items
            .Where(i => i.Category == category)
            .Select(i => i.PriceBdt)
            .OrderBy(p => p)
            .ToList();

        if (comparables.Count == 0)
            return Task.FromResult<PriceSuggestionDto?>(null);

        var mid = comparables[comparables.Count / 2];
        var factor = condition switch
        {
            ItemCondition.New => 1.15m,
            ItemCondition.LikeNew => 1.0m,
            ItemCondition.Good => 0.85m,
            _ => 0.7m
        };
        var point = decimal.Round(mid * factor / 50m) * 50m;

        return Task.FromResult<PriceSuggestionDto?>(new PriceSuggestionDto
        {
            SuggestedLow = decimal.Round(point * 0.85m / 50m) * 50m,
            SuggestedHigh = decimal.Round(point * 1.18m / 50m) * 50m,
            SuggestedPoint = point,
            Basis = $"{comparables.Count} recent {category} listing(s), adjusted for {Humanize(condition)} condition",
            ComparableCount = comparables.Count
        });
    }

    // ------------------------------------------------------- buy interests

    public Task<bool> ExpressInterestAsync(string itemId, string message)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null || item.Status != ListingStatus.Active || item.SellerId == MeId)
            return Task.FromResult(false);

        var existing = _interests.FirstOrDefault(x => x.ItemId == itemId && x.BuyerId == MeId);
        if (existing is not null && existing.Status is BuyInterestStatus.Pending or BuyInterestStatus.Accepted)
            return Task.FromResult(false);

        _interests.Add(new Interest
        {
            Id = $"int-new-{_idSeed++}",
            ItemId = itemId,
            BuyerId = MeId,
            BuyerName = MeName,
            BuyerVerified = true,
            Message = message,
            CreatedAtUtc = DateTime.UtcNow,
            Status = BuyInterestStatus.Pending
        });
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<BuyInterestDto>> GetItemInterestsAsync(string itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId && i.SellerId == MeId);
        if (item is null)
            return Task.FromResult<IReadOnlyList<BuyInterestDto>>(Array.Empty<BuyInterestDto>());

        var rows = _interests
            .Where(x => x.ItemId == itemId && x.Status != BuyInterestStatus.Withdrawn)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new BuyInterestDto
            {
                Id = x.Id,
                BuyerId = x.BuyerId,
                BuyerDisplayName = x.BuyerName,
                BuyerVerified = x.BuyerVerified,
                Message = x.Message,
                CreatedAtUtc = x.CreatedAtUtc,
                Status = x.Status,
                // Contact block exists ONLY on the accepted branch (§11.4).
                Contact = x.Status == BuyInterestStatus.Accepted
                    ? new MarketplaceContactDto
                    {
                        DisplayName = x.BuyerName,
                        Phone = x.BuyerPhone ?? "+8801XXX-XXXXXX",
                        PreferredHandoverArea = x.HandoverArea
                    }
                    : null
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<BuyInterestDto>>(rows);
    }

    public Task<bool> RespondToInterestAsync(string interestId, bool accept)
    {
        var interest = _interests.FirstOrDefault(x => x.Id == interestId);
        if (interest is null || interest.Status != BuyInterestStatus.Pending)
            return Task.FromResult(false);

        var item = _items.FirstOrDefault(i => i.Id == interest.ItemId && i.SellerId == MeId);
        if (item is null) return Task.FromResult(false);

        interest.Status = accept ? BuyInterestStatus.Accepted : BuyInterestStatus.Declined;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<MyBuyInterestDto>> GetMyBuyInterestsAsync()
    {
        var rows = _interests
            .Where(x => x.BuyerId == MeId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x =>
            {
                var item = _items.FirstOrDefault(i => i.Id == x.ItemId);
                return new MyBuyInterestDto
                {
                    Id = x.Id,
                    ItemId = x.ItemId,
                    ItemTitle = item?.Title ?? "Listing removed",
                    ItemPriceBdt = item?.PriceBdt ?? 0m,
                    ItemCoverImage = item?.Images.FirstOrDefault() ?? string.Empty,
                    SellerDisplayName = item?.SellerName ?? "—",
                    Message = x.Message,
                    CreatedAtUtc = x.CreatedAtUtc,
                    Status = x.Status,
                    Contact = x.Status == BuyInterestStatus.Accepted
                        ? new MarketplaceContactDto
                        {
                            DisplayName = item?.SellerName ?? "Seller",
                            Phone = x.SellerPhone ?? "+8801XXX-XXXXXX",
                            PreferredHandoverArea = x.HandoverArea
                        }
                        : null
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<MyBuyInterestDto>>(rows);
    }

    public Task<bool> WithdrawInterestAsync(string interestId)
    {
        var interest = _interests.FirstOrDefault(x => x.Id == interestId && x.BuyerId == MeId);
        if (interest is null || interest.Status is BuyInterestStatus.Withdrawn or BuyInterestStatus.Declined)
            return Task.FromResult(false);
        interest.Status = BuyInterestStatus.Withdrawn;
        return Task.FromResult(true);
    }

    // ----------------------------------------------------------------- helpers

    private static MarketplaceItemSummaryDto ToSummary(Item i) => new()
    {
        Id = i.Id,
        Title = i.Title,
        PriceBdt = i.PriceBdt,
        Category = i.Category,
        Condition = i.Condition,
        AreaName = i.AreaName,
        SellerDisplayName = i.SellerName,
        SellerVerified = i.SellerVerified,
        PostedAtUtc = i.PostedAtUtc,
        Status = i.Status,
        CoverImage = i.Images.FirstOrDefault() ?? string.Empty
    };

    private static string Humanize(ItemCondition condition) => condition switch
    {
        ItemCondition.LikeNew => "like-new",
        _ => condition.ToString().ToLowerInvariant()
    };

    private static string UpazilaOf(string areaName)
    {
        var parts = areaName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static string DistrictOf(string areaName)
    {
        var parts = areaName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }
}
