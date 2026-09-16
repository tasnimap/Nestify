namespace Nestify.Shared.Dtos.Admin;

// The signed-in admin's own row, read straight from the users table.
public sealed class AdminProfileDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public short AccountType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? RoleName { get; set; }
    public string? RoleDescription { get; set; }
    public DateTime? RoleGrantedAtUtc { get; set; }
    public int ActiveSessions { get; set; }
    public DateTime? LastSignInUtc { get; set; }
    public int AdminCount { get; set; }
    public int UserCount { get; set; }
}
