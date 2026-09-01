using System.ComponentModel.DataAnnotations;
using Nestify.Shared.Dtos.Housing;

namespace Nestify.Web.Components.Housing;

/// <summary>
/// Edit model shared by the create and edit pages. Client-side validation here is
/// UX only — the server re-checks everything (§11.3.6, §11.5.3).
/// </summary>
public sealed class HousingPostFormModel
{
    [Required(ErrorMessage = "Give the post a title.")]
    [StringLength(150, MinimumLength = 4, ErrorMessage = "Title should be 4–150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe the place so seekers know what they're getting.")]
    [StringLength(4000, MinimumLength = 20, ErrorMessage = "Description should be at least 20 characters.")]
    public string Description { get; set; } = string.Empty;

    public ListingType ListingType { get; set; } = ListingType.SingleSeat;

    [Range(1, 20, ErrorMessage = "Enter a seat count between 1 and 20.")]
    public int SeatsAvailable { get; set; } = 1;

    [Range(0, 1_000_000, ErrorMessage = "Enter a rent between ৳0 and ৳10,00,000.")]
    public decimal MonthlyRent { get; set; }

    public EligibilityDto Eligibility { get; set; } = new();

    public CreateHousingPostRequestDto ToCreateDto(string houseId) => new()
    {
        HouseId = houseId,
        Title = Title.Trim(),
        Description = Description.Trim(),
        ListingType = ListingType,
        SeatsAvailable = SeatsAvailable,
        MonthlyRent = MonthlyRent,
        Eligibility = Eligibility
    };

    public UpdateHousingPostRequestDto ToUpdateDto() => new()
    {
        Title = Title.Trim(),
        Description = Description.Trim(),
        ListingType = ListingType,
        SeatsAvailable = SeatsAvailable,
        MonthlyRent = MonthlyRent,
        Eligibility = Eligibility
    };

    public static HousingPostFormModel FromDetail(HousingPostDetailDto d) => new()
    {
        Title = d.Title,
        Description = d.Description,
        ListingType = d.ListingType,
        SeatsAvailable = d.SeatsAvailable,
        MonthlyRent = d.MonthlyRent,
        Eligibility = d.Eligibility
    };
}