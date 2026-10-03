namespace Nestify.Web;

/// <summary>Marker used to locate the shared Web UI localization resources.</summary>
public sealed class SharedResource
{
    private static readonly System.Resources.ResourceManager Resources =
        new("Nestify.Web.SharedResource", typeof(SharedResource).Assembly);

    private static string Get(string key) =>
        Resources.GetString(key, System.Globalization.CultureInfo.CurrentUICulture) ?? key;

    public static string RegisterNameRequired => Get("Enter your full name.");
    public static string RegisterNameLength => Get("Name must be 2 to 120 characters.");
    public static string RegisterEmailRequired => Get("Enter your email address.");
    public static string RegisterEmailFormat => Get("Enter a valid email address, e.g. name@example.com.");
    public static string RegisterPhoneRequired => Get("Enter your phone number.");
    public static string RegisterPhoneFormat => Get("Enter an 11-digit number starting with 01.");
    public static string RegisterPasswordRequired => Get("Choose a password.");
    public static string RegisterPasswordLength => Get("Password must be at least 8 characters.");
    public static string RegisterConfirmRequired => Get("Re-enter your password.");
    public static string RegisterConfirmMatch => Get("The two passwords do not match.");
    public static string RegisterTermsRequired => Get("You need to accept the terms to continue.");

    public static string SettlementBillNameRequired => Get("Give the bill a name.");
    public static string SettlementAmountNonNegative => Get("Amount cannot be negative.");
    public static string SettlementPayerRequired => Get("Pick who paid.");
    public static string SettlementPaymentAmountRequired => Get("Amount must be greater than ৳0.00.");
    public static string SettlementPaymentNoteRequired => Get("Add a short note so the house knows what this was.");

    public static string HousingPostTitleRequired => Get("Give the post a title.");
    public static string HousingPostTitleLength => Get("Title should be 4–150 characters.");
    public static string HousingPostDescriptionRequired => Get("Describe the place so seekers know what they're getting.");
    public static string HousingPostDescriptionLength => Get("Description should be at least 20 characters.");
    public static string HousingPostRentRange => Get("Enter a rent between ৳0 and ৳10,00,000.");

    public static string MarketplaceItemTitleRequired => Get("Give the item a title.");
    public static string MarketplaceItemTitleLength => Get("Title should be 4–80 characters.");
    public static string MarketplaceItemDescriptionRequired => Get("Add a description so buyers know what they're getting.");
    public static string MarketplaceItemDescriptionLength => Get("Description should be at least 20 characters.");
    public static string MarketplaceItemPriceRange => Get("Enter a price between ৳1 and ৳10,00,000.");
    public static string MarketplaceItemDivisionRequired => Get("Pick a division.");
    public static string MarketplaceItemAreaRequired => Get("Add the area where the buyer would collect it.");
    public static string MarketplaceItemAreaLength => Get("Keep the area under 80 characters.");
}
