namespace Nestify.Shared.Dtos.Admin;

// Fields an admin may change on their own account.
public sealed class UpdateAdminProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
