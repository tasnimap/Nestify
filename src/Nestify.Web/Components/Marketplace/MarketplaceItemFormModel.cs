// src/Nestify.Web/Components/Marketplace/MarketplaceItemFormModel.cs
using System.ComponentModel.DataAnnotations;
using Nestify.Web;
using Nestify.Shared.Dtos.Marketplace;

namespace Nestify.Web.Components.Marketplace;

/// <summary>
/// Edit model shared by the sell and edit pages. Client-side validation here is
/// UX only — the server re-checks everything (§11.3.6, §11.5.3).
/// </summary>
public sealed class MarketplaceItemFormModel
{
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.MarketplaceItemTitleRequired))]
    [StringLength(80, MinimumLength = 4, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.MarketplaceItemTitleLength))]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.MarketplaceItemDescriptionRequired))]
    [StringLength(1200, MinimumLength = 20, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.MarketplaceItemDescriptionLength))]
    public string Description { get; set; } = string.Empty;

    public MarketplaceCategory Category { get; set; } = MarketplaceCategory.Furniture;

    public ItemCondition Condition { get; set; } = ItemCondition.Good;

    [Range(1, 1_000_000, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.MarketplaceItemPriceRange))]
    public decimal PriceBdt { get; set; }

    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.MarketplaceItemDivisionRequired))]
    public string Division { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.MarketplaceItemAreaRequired))]
    [StringLength(80, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.MarketplaceItemAreaLength))]
    public string AreaName { get; set; } = string.Empty;

    public List<string> Images { get; set; } = new();

    public CreateMarketplaceItemDto ToCreateDto() => new()
    {
        Title = Title.Trim(),
        Description = Description.Trim(),
        Category = Category,
        Condition = Condition,
        PriceBdt = PriceBdt,
        Division = Division,
        AreaName = AreaName.Trim(),
        Images = Images.ToList()
    };

    public UpdateMarketplaceItemDto ToUpdateDto() => new()
    {
        Title = Title.Trim(),
        Description = Description.Trim(),
        Category = Category,
        Condition = Condition,
        PriceBdt = PriceBdt,
        Division = Division,
        AreaName = AreaName.Trim(),
        Images = Images.ToList()
    };

    public static MarketplaceItemFormModel FromDetail(MarketplaceItemDetailDto d) => new()
    {
        Title = d.Title,
        Description = d.Description,
        Category = d.Category,
        Condition = d.Condition,
        PriceBdt = d.PriceBdt,
        Division = d.Division,
        AreaName = d.AreaName,
        Images = d.Images.ToList()
    };
}
