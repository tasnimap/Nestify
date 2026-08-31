// src/Nestify.Web/Components/Marketplace/MarketplaceItemFormModel.cs
using System.ComponentModel.DataAnnotations;
using Nestify.Shared.Dtos.Marketplace;

namespace Nestify.Web.Components.Marketplace;

/// <summary>
/// Edit model shared by the sell and edit pages. Client-side validation here is
/// UX only — the server re-checks everything (§11.3.6, §11.5.3).
/// </summary>
public sealed class MarketplaceItemFormModel
{
    [Required(ErrorMessage = "Give the item a title.")]
    [StringLength(80, MinimumLength = 4, ErrorMessage = "Title should be 4–80 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Add a description so buyers know what they're getting.")]
    [StringLength(1200, MinimumLength = 20, ErrorMessage = "Description should be at least 20 characters.")]
    public string Description { get; set; } = string.Empty;

    public MarketplaceCategory Category { get; set; } = MarketplaceCategory.Furniture;

    public ItemCondition Condition { get; set; } = ItemCondition.Good;

    [Range(1, 1_000_000, ErrorMessage = "Enter a price between ৳1 and ৳10,00,000.")]
    public decimal PriceBdt { get; set; }

    [Required(ErrorMessage = "Pick a division.")]
    public string Division { get; set; } = string.Empty;

    [Required(ErrorMessage = "Add the area where the buyer would collect it.")]
    [StringLength(80, ErrorMessage = "Keep the area under 80 characters.")]
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
