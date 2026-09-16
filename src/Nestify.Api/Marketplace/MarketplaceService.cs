using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Api.Profiles;
using Nestify.Shared.Dtos.Marketplace;

namespace Nestify.Api.Marketplace;

// Everything the marketplace pages do, against the tables in Marketplace.sql.
// Category and condition ids are the enum values in Nestify.Shared; listing and
// interest statuses are 1-based in the database and 0-based in the enums, so
// the two small converters at the bottom are the only place that knows that.
public sealed class MarketplaceService
{
    private const short ListingActive = 1;
    private const short ListingSold = 2;
    private const short ListingRemoved = 3;

    private const short InterestPending = 1;
    private const short InterestAccepted = 2;
    private const short InterestDeclined = 3;
    private const short InterestWithdrawn = 4;
    private const short InterestFulfilled = 5;
    private const short InterestClosed = 6;

    private const int MaxImages = 6;

    private readonly DbConnectionFactory _db;
    private readonly CloudinaryUploader _uploader;

    public MarketplaceService(DbConnectionFactory db, CloudinaryUploader uploader)
    {
        _db = db;
        _uploader = uploader;
    }

    // ------------------------------------------------------------ browse

    // Own listings never show in the grid; the seller sees those on "my listings".
    public async Task<MarketplacePageDto<MarketplaceItemSummaryDto>> BrowseAsync(long userId, MarketplaceItemFilterDto filter)
    {
        var where = new List<string> { "l.status = @active", "l.seller_user_id <> @userId" };
        var args = new DynamicParameters();
        args.Add("active", ListingActive);
        args.Add("userId", userId);

        if (filter.Category is { } category)
        {
            where.Add("l.category_id = @category");
            args.Add("category", (short)category);
        }

        if (filter.Condition is { } condition)
        {
            where.Add("l.condition_id = @condition");
            args.Add("condition", (short)condition);
        }

        if (filter.MinPrice is { } min)
        {
            where.Add("l.price_bdt >= @min");
            args.Add("min", min);
        }

        if (filter.MaxPrice is { } max)
        {
            where.Add("l.price_bdt <= @max");
            args.Add("max", max);
        }

        if (!string.IsNullOrWhiteSpace(filter.Division))
        {
            where.Add("lower(dv.name) = lower(@division)");
            args.Add("division", filter.Division.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.District))
        {
            where.Add("lower(ds.name) = lower(@district)");
            args.Add("district", filter.District.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Upazila))
        {
            where.Add("lower(up.name) = lower(@upazila)");
            args.Add("upazila", filter.Upazila.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            where.Add("(l.title ILIKE @search OR l.description ILIKE @search OR l.area_name ILIKE @search)");
            args.Add("search", "%" + filter.Search.Trim() + "%");
        }

        var orderBy = filter.Sort switch
        {
            MarketplaceSort.PriceLowToHigh => "l.price_bdt ASC, l.posted_at_utc DESC",
            MarketplaceSort.PriceHighToLow => "l.price_bdt DESC, l.posted_at_utc DESC",
            _ => "l.posted_at_utc DESC"
        };

        var page = Math.Max(1, filter.Page);
        var size = filter.PageSize <= 0 ? 12 : Math.Min(filter.PageSize, 48);
        args.Add("take", size);
        args.Add("skip", (page - 1) * size);

        var whereSql = string.Join(" AND ", where);

        using var connection = await _db.OpenAsync();

        var total = await connection.ExecuteScalarAsync<int>(
            $"SELECT count(*)::int FROM marketplace_listings l {LocationJoins} WHERE {whereSql}", args);

        var rows = (await connection.QueryAsync<ListingRow>(
            $@"{SelectListing}
               WHERE {whereSql}
               ORDER BY {orderBy}
               LIMIT @take OFFSET @skip", args)).ToList();

        var images = await LoadImagesAsync(connection, rows.Select(r => r.Id));

        return new MarketplacePageDto<MarketplaceItemSummaryDto>
        {
            Items = rows.Select(r => ToSummary(r, images)).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    // Opening a listing counts as a view, except when the seller opens their own.
    public async Task<MarketplaceItemDetailDto?> GetItemAsync(long userId, long id, bool countView)
    {
        using var connection = await _db.OpenAsync();
        var row = await connection.QuerySingleOrDefaultAsync<ListingRow>(
            $"{SelectListing} WHERE l.id = @id", new { id });
        if (row is null)
        {
            return null;
        }

        if (countView && row.SellerUserId != userId)
        {
            await connection.ExecuteAsync(
                "INSERT INTO marketplace_listing_views (listing_id, viewer_user_id) VALUES (@id, @userId)",
                new { id, userId });
        }

        var images = await LoadImagesAsync(connection, new[] { id });
        var hasInterest = await connection.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (SELECT 1 FROM marketplace_buy_interests
                             WHERE listing_id = @id AND buyer_user_id = @userId
                               AND status IN (@pending, @accepted))",
            new { id, userId, pending = InterestPending, accepted = InterestAccepted });

        return new MarketplaceItemDetailDto
        {
            Id = row.Id.ToString(),
            Title = row.Title,
            Description = row.Description,
            PriceBdt = row.PriceBdt,
            Category = (MarketplaceCategory)row.CategoryId,
            Condition = (ItemCondition)row.ConditionId,
            AreaName = row.AreaName,
            Division = row.DivisionName,
            PostedAtUtc = row.PostedAtUtc,
            Status = ToListingStatus(row.Status),
            Images = images.TryGetValue(row.Id, out var list) ? list : new List<string>(),
            SellerId = row.SellerUserId.ToString(),
            SellerDisplayName = row.SellerName,
            SellerVerified = row.SellerVerified,
            SellerJoinedUtc = row.SellerJoinedUtc,
            IsMine = row.SellerUserId == userId,
            HasActiveInterest = hasInterest
        };
    }

    // ------------------------------------------------------ create / edit

    public async Task<(long? Id, string? Error)> CreateAsync(long userId, CreateMarketplaceItemDto dto)
    {
        var check = Validate(dto.Title, dto.Description, dto.PriceBdt, dto.AreaName);
        if (check is not null)
        {
            return (null, check);
        }

        using var connection = await _db.OpenAsync();

        var location = await ResolveLocationAsync(connection, dto.Division, dto.AreaName);
        if (location is null)
        {
            return (null, "Pick a division, district and upazila.");
        }

        var (urls, uploadError) = await StoreImagesAsync(dto.Images);
        if (uploadError is not null)
        {
            return (null, uploadError);
        }

        using var transaction = connection.BeginTransaction();

        var id = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO marketplace_listings
                  (seller_user_id, title, description, category_id, condition_id, price_bdt,
                   division_id, district_id, upazila_id, area_name)
              VALUES (@userId, @title, @description, @category, @condition, @price,
                      @divisionId, @districtId, @upazilaId, @areaName)
              RETURNING id",
            new
            {
                userId,
                title = dto.Title.Trim(),
                description = dto.Description.Trim(),
                category = (short)dto.Category,
                condition = (short)dto.Condition,
                price = dto.PriceBdt,
                divisionId = location.Value.DivisionId,
                districtId = location.Value.DistrictId,
                upazilaId = location.Value.UpazilaId,
                areaName = dto.AreaName.Trim()
            },
            transaction);

        await SaveImagesAsync(connection, transaction, id, urls);
        transaction.Commit();
        return (id, null);
    }

    public async Task<MarketplaceItemDetailDto?> GetForEditAsync(long userId, long id)
    {
        var item = await GetItemAsync(userId, id, countView: false);
        return item is { IsMine: true } ? item : null;
    }

    public async Task<(bool Ok, string Message)> UpdateAsync(long userId, long id, UpdateMarketplaceItemDto dto)
    {
        var check = Validate(dto.Title, dto.Description, dto.PriceBdt, dto.AreaName);
        if (check is not null)
        {
            return (false, check);
        }

        using var connection = await _db.OpenAsync();

        var owned = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM marketplace_listings WHERE id = @id AND seller_user_id = @userId AND status <> @removed)",
            new { id, userId, removed = ListingRemoved });
        if (!owned)
        {
            return (false, "That listing is not yours.");
        }

        var location = await ResolveLocationAsync(connection, dto.Division, dto.AreaName);
        if (location is null)
        {
            return (false, "Pick a division, district and upazila.");
        }

        var (urls, uploadError) = await StoreImagesAsync(dto.Images);
        if (uploadError is not null)
        {
            return (false, uploadError);
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            @"UPDATE marketplace_listings
                 SET title = @title, description = @description, category_id = @category,
                     condition_id = @condition, price_bdt = @price, division_id = @divisionId,
                     district_id = @districtId, upazila_id = @upazilaId, area_name = @areaName,
                     updated_at_utc = now()
               WHERE id = @id",
            new
            {
                id,
                title = dto.Title.Trim(),
                description = dto.Description.Trim(),
                category = (short)dto.Category,
                condition = (short)dto.Condition,
                price = dto.PriceBdt,
                divisionId = location.Value.DivisionId,
                districtId = location.Value.DistrictId,
                upazilaId = location.Value.UpazilaId,
                areaName = dto.AreaName.Trim()
            },
            transaction);

        // The form always sends the full photo list, so replace rather than merge.
        await connection.ExecuteAsync(
            "DELETE FROM marketplace_listing_images WHERE listing_id = @id", new { id }, transaction);
        await SaveImagesAsync(connection, transaction, id, urls);

        transaction.Commit();
        return (true, "Listing updated.");
    }

    public async Task<IReadOnlyList<MyListingDto>> GetMyListingsAsync(long userId)
    {
        using var connection = await _db.OpenAsync();

        var rows = (await connection.QueryAsync<MyListingRow>(
            $@"{ListingColumns},
                   (SELECT count(*)::int FROM marketplace_buy_interests i
                     WHERE i.listing_id = l.id AND i.status <> @withdrawn)      AS interest_count,
                   (SELECT count(*)::int FROM marketplace_buy_interests i
                     WHERE i.listing_id = l.id AND i.status = @pending)         AS pending_interest_count,
                   (SELECT count(*)::int FROM marketplace_listing_views v
                     WHERE v.listing_id = l.id)                                 AS view_count
               {ListingFrom}
               WHERE l.seller_user_id = @userId AND l.status <> @removed
               ORDER BY l.posted_at_utc DESC",
            new { userId, withdrawn = InterestWithdrawn, pending = InterestPending, removed = ListingRemoved })).ToList();

        var images = await LoadImagesAsync(connection, rows.Select(r => r.Id));

        return rows.Select(r => new MyListingDto
        {
            Item = ToSummary(r, images),
            InterestCount = r.InterestCount,
            PendingInterestCount = r.PendingInterestCount,
            ViewCount = r.ViewCount
        }).ToList();
    }

    // The seller picks the request that got the item (or none, if it was sold
    // outside Nestify). That request becomes Fulfilled, every other open one
    // becomes Closed, and nobody can withdraw after this.
    public async Task<(bool Ok, string Message)> MarkSoldAsync(long userId, long id, long? buyerInterestId)
    {
        using var connection = await _db.OpenAsync();

        var active = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM marketplace_listings WHERE id = @id AND seller_user_id = @userId AND status = @active)",
            new { id, userId, active = ListingActive });
        if (!active)
        {
            return (false, "Only an active listing of yours can be marked sold.");
        }

        long? buyerUserId = null;
        if (buyerInterestId is not null)
        {
            buyerUserId = await connection.ExecuteScalarAsync<long?>(
                @"SELECT buyer_user_id FROM marketplace_buy_interests
                   WHERE id = @buyerInterestId AND listing_id = @id AND status = @accepted",
                new { buyerInterestId, id, accepted = InterestAccepted });
            if (buyerUserId is null)
            {
                return (false, "Pick one of the accepted buy requests.");
            }
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            @"UPDATE marketplace_listings
                 SET status = @sold, sold_at_utc = now(), sold_to_user_id = @buyerUserId, updated_at_utc = now()
               WHERE id = @id",
            new { id, sold = ListingSold, buyerUserId },
            transaction);

        if (buyerInterestId is not null)
        {
            await connection.ExecuteAsync(
                "UPDATE marketplace_buy_interests SET status = @fulfilled, responded_at_utc = now() WHERE id = @buyerInterestId",
                new { buyerInterestId, fulfilled = InterestFulfilled },
                transaction);
        }

        await connection.ExecuteAsync(
            @"UPDATE marketplace_buy_interests
                 SET status = @closed, responded_at_utc = now()
               WHERE listing_id = @id AND status IN (@pending, @accepted)",
            new { id, closed = InterestClosed, pending = InterestPending, accepted = InterestAccepted },
            transaction);

        transaction.Commit();
        return (true, "Marked as sold.");
    }

    // A deleted listing is kept as Removed so old interests still have something to point at.
    public async Task<bool> DeleteAsync(long userId, long id)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync(
            @"UPDATE marketplace_listings
                 SET status = @removed, removed_at_utc = now(), removed_by_user_id = @userId, updated_at_utc = now()
               WHERE id = @id AND seller_user_id = @userId AND status <> @removed",
            new { id, userId, removed = ListingRemoved });
        return changed == 1;
    }

    // Median of the category's listings, nudged by condition and rounded to 50 taka.
    public async Task<PriceSuggestionDto?> GetPriceSuggestionAsync(MarketplaceCategory category, ItemCondition condition)
    {
        using var connection = await _db.OpenAsync();
        var prices = (await connection.QueryAsync<decimal>(
            @"SELECT price_bdt FROM marketplace_listings
               WHERE category_id = @category AND status IN (@active, @sold)
               ORDER BY price_bdt",
            new { category = (short)category, active = ListingActive, sold = ListingSold })).ToList();

        if (prices.Count == 0)
        {
            return null;
        }

        var mid = prices[prices.Count / 2];
        var factor = condition switch
        {
            ItemCondition.New => 1.15m,
            ItemCondition.LikeNew => 1.0m,
            ItemCondition.Good => 0.85m,
            _ => 0.7m
        };
        var point = decimal.Round(mid * factor / 50m) * 50m;
        var label = condition == ItemCondition.LikeNew ? "like-new" : condition.ToString().ToLowerInvariant();

        return new PriceSuggestionDto
        {
            SuggestedLow = decimal.Round(point * 0.85m / 50m) * 50m,
            SuggestedHigh = decimal.Round(point * 1.18m / 50m) * 50m,
            SuggestedPoint = point,
            Basis = $"{prices.Count} recent {category} listing(s), adjusted for {label} condition",
            ComparableCount = prices.Count
        };
    }

    // ------------------------------------------------------ buy interests

    public async Task<(bool Ok, string Message)> ExpressInterestAsync(long userId, long listingId, string message)
    {
        message = (message ?? string.Empty).Trim();
        if (message.Length == 0 || message.Length > 500)
        {
            return (false, "Write a short message to the seller.");
        }

        using var connection = await _db.OpenAsync();

        var listing = await connection.QuerySingleOrDefaultAsync<(long SellerUserId, short Status)>(
            "SELECT seller_user_id, status FROM marketplace_listings WHERE id = @listingId", new { listingId });
        if (listing == default || listing.Status != ListingActive)
        {
            return (false, "That listing is no longer available.");
        }

        if (listing.SellerUserId == userId)
        {
            return (false, "You cannot buy your own listing.");
        }

        var open = await connection.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (SELECT 1 FROM marketplace_buy_interests
                             WHERE listing_id = @listingId AND buyer_user_id = @userId
                               AND status IN (@pending, @accepted))",
            new { listingId, userId, pending = InterestPending, accepted = InterestAccepted });
        if (open)
        {
            return (false, "You already have a request open on this listing.");
        }

        await connection.ExecuteAsync(
            "INSERT INTO marketplace_buy_interests (listing_id, buyer_user_id, message) VALUES (@listingId, @userId, @message)",
            new { listingId, userId, message });
        return (true, "Request sent.");
    }

    // Seller side. The buyer's phone only leaves the database once the seller accepted.
    public async Task<IReadOnlyList<BuyInterestDto>> GetListingInterestsAsync(long userId, long listingId)
    {
        using var connection = await _db.OpenAsync();

        var rows = await connection.QueryAsync<InterestRow>(
            @"SELECT i.id, i.listing_id, i.buyer_user_id, i.message, i.status,
                     i.preferred_handover_area, i.created_at_utc,
                     b.full_name AS buyer_name, b.phone_number AS buyer_phone,
                     coalesce(bp.is_verified, false) AS buyer_verified
                FROM marketplace_buy_interests i
                JOIN marketplace_listings l ON l.id = i.listing_id
                JOIN users b ON b.id = i.buyer_user_id
                LEFT JOIN user_additional_profile_info bp ON bp.user_id = b.id
               WHERE i.listing_id = @listingId AND l.seller_user_id = @userId AND i.status <> @withdrawn
               ORDER BY i.created_at_utc DESC",
            new { listingId, userId, withdrawn = InterestWithdrawn });

        return rows.Select(r => new BuyInterestDto
        {
            Id = r.Id.ToString(),
            BuyerId = r.BuyerUserId.ToString(),
            BuyerDisplayName = r.BuyerName,
            BuyerVerified = r.BuyerVerified,
            Message = r.Message,
            CreatedAtUtc = r.CreatedAtUtc,
            Status = ToInterestStatus(r.Status),
            Contact = r.Status is InterestAccepted or InterestFulfilled
                ? new MarketplaceContactDto
                {
                    DisplayName = r.BuyerName,
                    Phone = r.BuyerPhone,
                    PreferredHandoverArea = r.PreferredHandoverArea
                }
                : null
        }).ToList();
    }

    public async Task<bool> RespondAsync(long userId, long interestId, bool accept)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync(
            @"UPDATE marketplace_buy_interests i
                 SET status = @next, responded_at_utc = now()
                FROM marketplace_listings l
               WHERE i.id = @interestId AND l.id = i.listing_id
                 AND l.seller_user_id = @userId AND i.status = @pending",
            new { interestId, userId, pending = InterestPending, next = accept ? InterestAccepted : InterestDeclined });
        return changed == 1;
    }

    // Buyer side. The seller's phone only leaves the database once they accepted.
    public async Task<IReadOnlyList<MyBuyInterestDto>> GetMyInterestsAsync(long userId)
    {
        using var connection = await _db.OpenAsync();

        var rows = (await connection.QueryAsync<MyInterestRow>(
            @"SELECT i.id, i.listing_id, i.message, i.status, i.preferred_handover_area, i.created_at_utc,
                     l.title AS item_title, l.price_bdt AS item_price_bdt, l.status AS listing_status,
                     s.full_name AS seller_name, s.phone_number AS seller_phone,
                     (SELECT image_url FROM marketplace_listing_images im
                       WHERE im.listing_id = l.id ORDER BY im.sort_order LIMIT 1) AS cover_image
                FROM marketplace_buy_interests i
                JOIN marketplace_listings l ON l.id = i.listing_id
                JOIN users s ON s.id = l.seller_user_id
               WHERE i.buyer_user_id = @userId
               ORDER BY i.created_at_utc DESC",
            new { userId })).ToList();

        return rows.Select(r => new MyBuyInterestDto
        {
            Id = r.Id.ToString(),
            ItemId = r.ListingId.ToString(),
            ItemTitle = r.ListingStatus == ListingRemoved ? "Listing removed" : r.ItemTitle,
            ItemPriceBdt = r.ItemPriceBdt,
            ItemCoverImage = r.CoverImage ?? string.Empty,
            SellerDisplayName = r.SellerName,
            Message = r.Message,
            CreatedAtUtc = r.CreatedAtUtc,
            Status = ToInterestStatus(r.Status),
            Contact = r.Status is InterestAccepted or InterestFulfilled
                ? new MarketplaceContactDto
                {
                    DisplayName = r.SellerName,
                    Phone = r.SellerPhone,
                    PreferredHandoverArea = r.PreferredHandoverArea
                }
                : null
        }).ToList();
    }

    public async Task<bool> WithdrawAsync(long userId, long interestId)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync(
            @"UPDATE marketplace_buy_interests i
                 SET status = @withdrawn, withdrawn_at_utc = now()
                FROM marketplace_listings l
               WHERE i.id = @interestId AND i.buyer_user_id = @userId AND i.status IN (@pending, @accepted)
                 AND l.id = i.listing_id AND l.status = @active",
            new { interestId, userId, withdrawn = InterestWithdrawn, pending = InterestPending, accepted = InterestAccepted, active = ListingActive });
        return changed == 1;
    }

    // ------------------------------------------------------------ reports

    public async Task<(bool Ok, string Message)> ReportAsync(long userId, long listingId, ReportListingDto dto)
    {
        using var connection = await _db.OpenAsync();

        var reasonId = await connection.ExecuteScalarAsync<short?>(
            "SELECT id FROM marketplace_report_reasons WHERE lower(name) = lower(@reason)",
            new { reason = (dto.Reason ?? string.Empty).Trim() });
        if (reasonId is null)
        {
            return (false, "Pick a reason for the report.");
        }

        var listing = await connection.QuerySingleOrDefaultAsync<(long SellerUserId, short Status)>(
            "SELECT seller_user_id, status FROM marketplace_listings WHERE id = @listingId", new { listingId });
        if (listing == default || listing.Status == ListingRemoved)
        {
            return (false, "That listing is no longer available.");
        }

        if (listing.SellerUserId == userId)
        {
            return (false, "You cannot report your own listing.");
        }

        var alreadyOpen = await connection.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (SELECT 1 FROM marketplace_reports
                             WHERE listing_id = @listingId AND reported_by_user_id = @userId AND state = 1)",
            new { listingId, userId });
        if (alreadyOpen)
        {
            return (false, "You already reported this listing.");
        }

        var details = (dto.Details ?? string.Empty).Trim();
        await connection.ExecuteAsync(
            @"INSERT INTO marketplace_reports (listing_id, reported_by_user_id, reason_id, details)
              VALUES (@listingId, @userId, @reasonId, @details)",
            new { listingId, userId, reasonId, details = details.Length == 0 ? null : details[..Math.Min(300, details.Length)] });
        return (true, "Report submitted.");
    }

    // ------------------------------------------------------------ helpers

    private const string LocationJoins =
        @"JOIN divisions dv ON dv.id = l.division_id
          LEFT JOIN districts ds ON ds.id = l.district_id
          LEFT JOIN upazilas up ON up.id = l.upazila_id";

    private const string ListingColumns =
        @"SELECT l.id, l.seller_user_id, l.title, l.description, l.category_id, l.condition_id,
                 l.price_bdt, l.area_name, l.status, l.posted_at_utc,
                 dv.name AS division_name,
                 s.full_name AS seller_name, s.created_at_utc AS seller_joined_utc,
                 coalesce(sp.is_verified, false) AS seller_verified";

    private const string ListingFrom =
        @"FROM marketplace_listings l
            JOIN users s ON s.id = l.seller_user_id
            LEFT JOIN user_additional_profile_info sp ON sp.user_id = s.id
            " + LocationJoins;

    private const string SelectListing = ListingColumns + " " + ListingFrom;

    private static string? Validate(string title, string description, decimal price, string areaName)
    {
        title = (title ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();
        if (title.Length < 4 || title.Length > 80)
        {
            return "Title should be 4–80 characters.";
        }

        if (description.Length < 20 || description.Length > 1200)
        {
            return "Description should be 20–1200 characters.";
        }

        if (price < 1 || price > 1_000_000)
        {
            return "Enter a price between ৳1 and ৳10,00,000.";
        }

        if (string.IsNullOrWhiteSpace(areaName))
        {
            return "Add the area where the buyer would collect it.";
        }

        return null;
    }

    // The form sends the division name and "upazila, district"; turn those back
    // into the ids of the administrative tables.
    private static async Task<(int DivisionId, int? DistrictId, int? UpazilaId)?> ResolveLocationAsync(
        IDbConnection connection, string division, string areaName)
    {
        var divisionId = await connection.ExecuteScalarAsync<int?>(
            "SELECT id FROM divisions WHERE lower(name) = lower(@name)",
            new { name = (division ?? string.Empty).Trim() });
        if (divisionId is null)
        {
            return null;
        }

        var parts = (areaName ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int? districtId = null;
        int? upazilaId = null;

        if (parts.Length >= 2)
        {
            districtId = await connection.ExecuteScalarAsync<int?>(
                "SELECT id FROM districts WHERE division_id = @divisionId AND lower(name) = lower(@name)",
                new { divisionId, name = parts[1] });

            if (districtId is not null)
            {
                upazilaId = await connection.ExecuteScalarAsync<int?>(
                    "SELECT id FROM upazilas WHERE district_id = @districtId AND lower(name) = lower(@name)",
                    new { districtId, name = parts[0] });
            }
        }

        return (divisionId.Value, districtId, upazilaId);
    }

    // Pictures arrive either as links already on Cloudinary (kept as they are) or
    // as data URLs picked in the browser, which are uploaded here. Anything else
    // (the generated placeholder tiles) is dropped.
    private async Task<(List<string> Urls, string? Error)> StoreImagesAsync(IReadOnlyList<string> images)
    {
        var urls = new List<string>();
        foreach (var image in images.Take(MaxImages))
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                continue;
            }

            if (image.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                image.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(image);
                continue;
            }

            if (!image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var comma = image.IndexOf(',');
            if (comma < 0)
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(image[(comma + 1)..]);
            }
            catch (FormatException)
            {
                continue;
            }

            var contentType = image[5..comma].Split(';')[0];
            using var stream = new MemoryStream(bytes);
            var (url, error) = await _uploader.UploadAsync(stream, "listing.jpg", contentType);
            if (url is null)
            {
                return (urls, error ?? "Could not upload a photo.");
            }

            urls.Add(url);
        }

        return (urls, null);
    }

    private static async Task SaveImagesAsync(IDbConnection connection, IDbTransaction transaction, long listingId, List<string> urls)
    {
        for (var i = 0; i < urls.Count; i++)
        {
            await connection.ExecuteAsync(
                "INSERT INTO marketplace_listing_images (listing_id, image_url, sort_order) VALUES (@listingId, @url, @order)",
                new { listingId, url = urls[i], order = (short)i },
                transaction);
        }
    }

    private static async Task<Dictionary<long, List<string>>> LoadImagesAsync(IDbConnection connection, IEnumerable<long> listingIds)
    {
        var ids = listingIds.Distinct().ToArray();
        var result = new Dictionary<long, List<string>>();
        if (ids.Length == 0)
        {
            return result;
        }

        var rows = await connection.QueryAsync<(long ListingId, string ImageUrl)>(
            "SELECT listing_id, image_url FROM marketplace_listing_images WHERE listing_id = ANY(@ids) ORDER BY listing_id, sort_order",
            new { ids });

        foreach (var (listingId, url) in rows)
        {
            if (!result.TryGetValue(listingId, out var list))
            {
                result[listingId] = list = new List<string>();
            }

            list.Add(url);
        }

        return result;
    }

    private static MarketplaceItemSummaryDto ToSummary(ListingRow r, Dictionary<long, List<string>> images)
    {
        var list = images.TryGetValue(r.Id, out var found) ? found : new List<string>();
        return new MarketplaceItemSummaryDto
        {
            Id = r.Id.ToString(),
            Title = r.Title,
            PriceBdt = r.PriceBdt,
            Category = (MarketplaceCategory)r.CategoryId,
            Condition = (ItemCondition)r.ConditionId,
            AreaName = r.AreaName,
            SellerDisplayName = r.SellerName,
            SellerVerified = r.SellerVerified,
            PostedAtUtc = r.PostedAtUtc,
            Status = ToListingStatus(r.Status),
            CoverImage = list.FirstOrDefault() ?? string.Empty,
            Images = list
        };
    }

    private static ListingStatus ToListingStatus(short status) => status switch
    {
        ListingSold => ListingStatus.Sold,
        ListingRemoved => ListingStatus.Removed,
        _ => ListingStatus.Active
    };

    private static BuyInterestStatus ToInterestStatus(short status) => status switch
    {
        InterestAccepted => BuyInterestStatus.Accepted,
        InterestDeclined => BuyInterestStatus.Declined,
        InterestWithdrawn => BuyInterestStatus.Withdrawn,
        InterestFulfilled => BuyInterestStatus.Fulfilled,
        InterestClosed => BuyInterestStatus.Closed,
        _ => BuyInterestStatus.Pending
    };

    private class ListingRow
    {
        public long Id { get; set; }
        public long SellerUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public short CategoryId { get; set; }
        public short ConditionId { get; set; }
        public decimal PriceBdt { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public short Status { get; set; }
        public DateTime PostedAtUtc { get; set; }
        public string DivisionName { get; set; } = string.Empty;
        public string SellerName { get; set; } = string.Empty;
        public DateTime SellerJoinedUtc { get; set; }
        public bool SellerVerified { get; set; }
    }

    private sealed class MyListingRow : ListingRow
    {
        public int InterestCount { get; set; }
        public int PendingInterestCount { get; set; }
        public int ViewCount { get; set; }
    }

    private sealed class InterestRow
    {
        public long Id { get; set; }
        public long ListingId { get; set; }
        public long BuyerUserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public short Status { get; set; }
        public string? PreferredHandoverArea { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string BuyerName { get; set; } = string.Empty;
        public string BuyerPhone { get; set; } = string.Empty;
        public bool BuyerVerified { get; set; }
    }

    private sealed class MyInterestRow
    {
        public long Id { get; set; }
        public long ListingId { get; set; }
        public string Message { get; set; } = string.Empty;
        public short Status { get; set; }
        public string? PreferredHandoverArea { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string ItemTitle { get; set; } = string.Empty;
        public decimal ItemPriceBdt { get; set; }
        public short ListingStatus { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public string SellerPhone { get; set; } = string.Empty;
        public string? CoverImage { get; set; }
    }
}
