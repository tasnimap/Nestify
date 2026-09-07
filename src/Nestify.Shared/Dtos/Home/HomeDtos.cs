// src/Nestify.Shared/Dtos/Home/HomeDtos.cs
// The homes / home_members tables from User_Home.sql, shaped the way the /home
// page needs them. Role numbers here are the database numbers: 1 Manager,
// 2 Co-manager, 3 Member.
namespace Nestify.Shared.Dtos.Home;

public sealed class HomeMemberDto
{
    public string Id { get; set; } = string.Empty;          // home_members.id
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public short Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public bool IsMe { get; set; }
}

public sealed class HomeDto
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
    public List<HomeMemberDto> Members { get; set; } = new();

    /// <summary>Pending join requests. Only filled in for a manager or co-manager.</summary>
    public List<HomeJoinRequestDto> PendingRequests { get; set; } = new();
}

/// <summary>A pending request, as the manager and co-managers see it.</summary>
public sealed class HomeJoinRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
}

/// <summary>The request the caller is waiting on, shown while they are in no home.</summary>
public sealed class MyJoinRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string HomeName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
}

/// <summary>Same fields whether the home is being created or edited later.</summary>
public sealed class HomeDetailsDto
{
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class JoinHomeDto
{
    public string JoinCode { get; set; } = string.Empty;
}

/// <summary>A member is added by the email they registered with.</summary>
public sealed class AddHomeMemberDto
{
    public string Email { get; set; } = string.Empty;
}
