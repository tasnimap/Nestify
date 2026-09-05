// src/Nestify.Web/Services/Interfaces/IHomeService.cs
// The "home" (shared house) a user belongs to. A user is in at most one home.
// Frontend phase: MockHomeService keeps everything in memory. When the API lands,
// write a HomeService against it and keep this interface + HomeRules as they are —
// the page only talks to this interface.
namespace Nestify.Web.Services.Interfaces;

public enum HomeRole
{
    Member = 1,
    CoManager = 2,
    Manager = 3
}

public sealed class HomeMemberView
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public HomeRole Role { get; set; }
    public DateTime JoinedOnUtc { get; set; }
    public bool IsMe { get; set; }
}

public sealed class HomeView
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<HomeMemberView> Members { get; set; } = new();

    public HomeRole MyRole => Members.FirstOrDefault(m => m.IsMe)?.Role ?? HomeRole.Member;
}

// Same fields whether the house is being created or edited later.
public sealed class HomeDetailsRequest
{
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public string Division { get; set; } = "Dhaka";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed record HomeActionResult(bool Ok, string Message);

/// <summary>
/// Who may do what inside a home. The service enforces these too — the page uses
/// them only to decide which buttons to show.
/// </summary>
public static class HomeRules
{
    public static bool CanAddMembers(HomeRole actor) => actor != HomeRole.Member;

    public static bool CanEditDetails(HomeRole actor) => actor != HomeRole.Member;

    // Manager and co-manager both promote members up to co-manager. Nobody is
    // promoted straight to manager; that only happens through a transfer.
    public static bool CanPromote(HomeRole actor, HomeMemberView target) =>
        actor != HomeRole.Member && target.Role == HomeRole.Member && !target.IsMe;

    // Only the manager takes a co-manager back down to plain member.
    public static bool CanDemote(HomeRole actor, HomeMemberView target) =>
        actor == HomeRole.Manager && target.Role == HomeRole.CoManager && !target.IsMe;

    // Manager removes anyone but themselves; co-manager removes plain members only.
    public static bool CanRemove(HomeRole actor, HomeMemberView target) => actor switch
    {
        HomeRole.Manager => !target.IsMe,
        HomeRole.CoManager => target.Role == HomeRole.Member && !target.IsMe,
        _ => false
    };

    public static bool CanTransferManager(HomeRole actor, HomeMemberView target) =>
        actor == HomeRole.Manager && !target.IsMe;

    // The manager is stuck until they hand the role over, so a home is never left
    // without one. Everyone else leaves freely.
    public static bool CanLeave(HomeRole actor) => actor != HomeRole.Manager;

    public static string RoleLabel(HomeRole role) => role switch
    {
        HomeRole.Manager => "Manager",
        HomeRole.CoManager => "Co-manager",
        _ => "Member"
    };
}

public interface IHomeService
{
    /// <summary>The caller's home, or null when they are not in one yet.</summary>
    Task<HomeView?> GetMyHomeAsync();

    /// <summary>Creates a home; the caller becomes its manager.</summary>
    Task<HomeView> CreateHomeAsync(HomeDetailsRequest request, string myName, string myEmail);

    /// <summary>Joins by code; the caller becomes a member. Fails on an unknown code.</summary>
    Task<HomeActionResult> JoinHomeAsync(string joinCode, string myName, string myEmail);

    /// <summary>Edits name, address and coordinates. Manager and co-manager only.</summary>
    Task<HomeActionResult> UpdateHomeDetailsAsync(HomeDetailsRequest request);

    Task<HomeActionResult> AddMemberAsync(string name, string email);
    Task<HomeActionResult> PromoteToCoManagerAsync(string memberId);
    Task<HomeActionResult> DemoteToMemberAsync(string memberId);
    Task<HomeActionResult> RemoveMemberAsync(string memberId);
    Task<HomeActionResult> TransferManagerAsync(string memberId);
    Task<HomeActionResult> LeaveHomeAsync();
}
