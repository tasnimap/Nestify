// src/Nestify.Shared/Dtos/Profile/UserProfileDtos.cs
namespace Nestify.Shared.Dtos.Profile;

public enum VerificationState { NotApplied, Pending, Verified, Rejected }
public enum ProfileGender { Male, Female }

/// <summary>
/// The user_additional_profile_info row, plus the few users columns the profile
/// page shows next to it.
/// </summary>
public sealed class UserProfileDto
{
    public const string DefaultPictureUrl =
        "https://res.cloudinary.com/dait0sacc/image/upload/v1774704629/k7ygnoel72ychr8ico6n.png";

    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public string ProfilePictureUrl { get; set; } = DefaultPictureUrl;
    public string? Occupation { get; set; }
    public ProfileGender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool IsSmoker { get; set; }
    public bool IsDrinker { get; set; }
    public string? OrganizationName { get; set; }
    public VerificationState VerificationState { get; set; }
    public string? Address { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? InstagramUrl { get; set; }
}

/// <summary>
/// What the "Edit profile" dialog sends back. The picture is not here - it is
/// uploaded to Cloudinary through its own endpoint.
/// </summary>
public sealed class UpdateUserProfileDto
{
    public string? Occupation { get; set; }
    public ProfileGender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool IsSmoker { get; set; }
    public bool IsDrinker { get; set; }
    public string? OrganizationName { get; set; }
    public string? Address { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? InstagramUrl { get; set; }
}

/// <summary>What the bKash portal asks for. The PIN is checked and thrown away.</summary>
public sealed class BkashPaymentDto
{
    public string BkashNumber { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
}

/// <summary>The receipt the user sees after the fake bKash payment goes through.</summary>
public sealed class VerificationPaymentDto
{
    public string PaymentId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public decimal AmountBdt { get; set; }
    public string BkashNumber { get; set; } = string.Empty;
    public DateTime PaidAtUtc { get; set; }
}

public sealed class VerificationFeeDto
{
    public decimal AmountBdt { get; set; }
}
