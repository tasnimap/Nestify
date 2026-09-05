using System.Security.Claims;

namespace Nestify.Web.Auth;

// One place that knows which interface a role belongs to and where its home is.
public static class RoleRoutes
{
    public const string UserHome = "housing";
    public const string HelperHome = "helpers/dashboard";
    public const string AdminHome = "admin";

    public static bool IsAdmin(string? role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

    public static bool IsHelper(string? role) =>
        string.Equals(role, "DomesticHelper", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "DomesticHelp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "Maid", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "Helper", StringComparison.OrdinalIgnoreCase);

    public static string HomeFor(string? role)
    {
        if (IsAdmin(role)) return AdminHome;
        if (IsHelper(role)) return HelperHome;
        return UserHome;
    }

    public static string HomeFor(ClaimsPrincipal user) => HomeFor(RoleOf(user));

    // The role claim can arrive as ClaimTypes.Role or the raw "role" key depending on the token.
    public static string? RoleOf(ClaimsPrincipal user) =>
        user.Claims.FirstOrDefault(c =>
            string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase))?.Value;
}
