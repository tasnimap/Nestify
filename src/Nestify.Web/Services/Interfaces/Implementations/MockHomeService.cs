// src/Nestify.Web/Services/Interfaces/Implementations/MockHomeService.cs
// In-memory home state for the frontend phase. Registered as a Singleton so the
// home survives page navigation. Starts empty: the user is in no home until they
// create one or join with a code. Both flows drop dummy housemates in so the
// members list, the role badges and every action button are visible right away.
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MockHomeService : IHomeService
{
    private HomeView? _home;
    private int _nextId = 1;

    // Named codes land you in a specific house. Any other code falls back to the
    // first one, so a tester always gets somewhere to look at.
    private static readonly Dictionary<string, (string Name, string Address, string Area, double Lat, double Lng)> JoinableHomes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["NST-4821"] = ("Mirpur Boys Nest", "House 24, Road 7, Mirpur DOHS", "Mirpur", 23.8223, 90.3654),
            ["NST-1907"] = ("Dhanmondi Mess House", "House 9, Road 11A, Dhanmondi", "Dhanmondi", 23.7461, 90.3742)
        };

    public Task<HomeView?> GetMyHomeAsync() => Task.FromResult(_home);

    public Task<HomeView> CreateHomeAsync(HomeDetailsRequest request, string myName, string myEmail)
    {
        _home = new HomeView
        {
            Id = "home-" + Guid.NewGuid().ToString("N")[..6],
            Name = request.Name,
            AddressLine = request.AddressLine,
            AreaName = request.AreaName,
            Division = request.Division,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            JoinCode = "NST-" + Random.Shared.Next(1000, 9999),
            CreatedAtUtc = DateTime.UtcNow,
            Members =
            {
                new HomeMemberView
                {
                    Id = NextId(),
                    Name = myName,
                    Email = myEmail,
                    Role = HomeRole.Manager,
                    JoinedOnUtc = DateTime.UtcNow,
                    IsMe = true
                },
                new HomeMemberView { Id = NextId(), Name = "Sabbir Rahman", Email = "sabbir@example.com", Role = HomeRole.CoManager, JoinedOnUtc = DateTime.UtcNow },
                new HomeMemberView { Id = NextId(), Name = "Nafis Iqbal", Email = "nafis@example.com", Role = HomeRole.Member, JoinedOnUtc = DateTime.UtcNow },
                new HomeMemberView { Id = NextId(), Name = "Arif Chowdhury", Email = "arif@example.com", Role = HomeRole.Member, JoinedOnUtc = DateTime.UtcNow }
            }
        };

        return Task.FromResult(_home);
    }

    public Task<HomeActionResult> JoinHomeAsync(string joinCode, string myName, string myEmail)
    {
        if (_home is not null)
        {
            return Fail("You are already in a home.");
        }

        var code = string.IsNullOrWhiteSpace(joinCode) ? "NST-4821" : joinCode.Trim();
        if (!JoinableHomes.TryGetValue(code, out var seed))
        {
            seed = JoinableHomes["NST-4821"];
        }

        _home = new HomeView
        {
            Id = "home-" + Guid.NewGuid().ToString("N")[..6],
            Name = seed.Name,
            AddressLine = seed.Address,
            AreaName = seed.Area,
            Division = "Dhaka",
            Latitude = seed.Lat,
            Longitude = seed.Lng,
            JoinCode = code.ToUpperInvariant(),
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-7),
            Members =
            {
                new HomeMemberView { Id = NextId(), Name = "Tanvir Ahmed", Email = "tanvir@example.com", Role = HomeRole.Manager, JoinedOnUtc = DateTime.UtcNow.AddMonths(-7) },
                new HomeMemberView { Id = NextId(), Name = "Sabbir Rahman", Email = "sabbir@example.com", Role = HomeRole.CoManager, JoinedOnUtc = DateTime.UtcNow.AddMonths(-5) },
                new HomeMemberView { Id = NextId(), Name = "Nafis Iqbal", Email = "nafis@example.com", Role = HomeRole.Member, JoinedOnUtc = DateTime.UtcNow.AddMonths(-2) },
                new HomeMemberView { Id = NextId(), Name = myName, Email = myEmail, Role = HomeRole.Member, JoinedOnUtc = DateTime.UtcNow, IsMe = true }
            }
        };

        return Ok($"Joined {_home.Name}.");
    }

    public Task<HomeActionResult> UpdateHomeDetailsAsync(HomeDetailsRequest request)
    {
        if (_home is null)
        {
            return Fail("You are not in a home.");
        }

        if (!HomeRules.CanEditDetails(_home.MyRole))
        {
            return Fail("Only the manager or a co-manager can edit house details.");
        }

        _home.Name = request.Name;
        _home.AddressLine = request.AddressLine;
        _home.AreaName = request.AreaName;
        _home.Division = request.Division;
        _home.Latitude = request.Latitude;
        _home.Longitude = request.Longitude;
        return Ok("House details updated.");
    }

    public Task<HomeActionResult> AddMemberAsync(string name, string email)
    {
        if (_home is null)
        {
            return Fail("You are not in a home.");
        }

        if (!HomeRules.CanAddMembers(_home.MyRole))
        {
            return Fail("Only the manager or a co-manager can add members.");
        }

        if (_home.Members.Any(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return Fail("That person is already a member.");
        }

        _home.Members.Add(new HomeMemberView
        {
            Id = NextId(),
            Name = name,
            Email = email,
            Role = HomeRole.Member,
            JoinedOnUtc = DateTime.UtcNow
        });

        return Ok($"{name} was added.");
    }

    public Task<HomeActionResult> PromoteToCoManagerAsync(string memberId)
    {
        if (_home is null || Find(memberId) is not { } target)
        {
            return Fail("Member not found.");
        }

        if (!HomeRules.CanPromote(_home.MyRole, target))
        {
            return Fail("You cannot promote this member.");
        }

        target.Role = HomeRole.CoManager;
        return Ok($"{target.Name} is now a co-manager.");
    }

    public Task<HomeActionResult> DemoteToMemberAsync(string memberId)
    {
        if (_home is null || Find(memberId) is not { } target)
        {
            return Fail("Member not found.");
        }

        if (!HomeRules.CanDemote(_home.MyRole, target))
        {
            return Fail("You cannot demote this member.");
        }

        target.Role = HomeRole.Member;
        return Ok($"{target.Name} is now a member.");
    }

    public Task<HomeActionResult> RemoveMemberAsync(string memberId)
    {
        if (_home is null || Find(memberId) is not { } target)
        {
            return Fail("Member not found.");
        }

        if (!HomeRules.CanRemove(_home.MyRole, target))
        {
            return Fail("You cannot remove this member.");
        }

        _home.Members.Remove(target);
        return Ok($"{target.Name} was removed.");
    }

    public Task<HomeActionResult> TransferManagerAsync(string memberId)
    {
        if (_home is null || Find(memberId) is not { } target)
        {
            return Fail("Member not found.");
        }

        if (!HomeRules.CanTransferManager(_home.MyRole, target))
        {
            return Fail("Only the manager can transfer the manager role.");
        }

        var me = _home.Members.First(m => m.IsMe);
        target.Role = HomeRole.Manager;
        me.Role = HomeRole.CoManager;
        return Ok($"{target.Name} is now the manager.");
    }

    public Task<HomeActionResult> LeaveHomeAsync()
    {
        if (_home is null)
        {
            return Fail("You are not in a home.");
        }

        if (!HomeRules.CanLeave(_home.MyRole))
        {
            return Fail("Transfer the manager role before leaving.");
        }

        _home = null;
        return Ok("You left the home.");
    }

    private HomeMemberView? Find(string memberId) =>
        _home?.Members.FirstOrDefault(m => m.Id == memberId);

    private string NextId() => (_nextId++).ToString();

    private static Task<HomeActionResult> Ok(string message) =>
        Task.FromResult(new HomeActionResult(true, message));

    private static Task<HomeActionResult> Fail(string message) =>
        Task.FromResult(new HomeActionResult(false, message));
}
