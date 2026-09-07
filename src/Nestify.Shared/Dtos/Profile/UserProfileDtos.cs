// src/Nestify.Shared/Dtos/Profile/UserProfileDtos.cs
namespace Nestify.Shared.Dtos.Profile;

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
    public string? Address { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? InstagramUrl { get; set; }
}
