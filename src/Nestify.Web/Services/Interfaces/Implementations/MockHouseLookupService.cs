// src/Nestify.Web/Services/Interfaces/Implementations/MockHouseLookupService.cs
// TEMPORARY fixture — see IHouseLookupService for the removal plan.
using Nestify.Shared.Dtos.Housing;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MockHouseLookupService : IHouseLookupService
{
    private readonly List<HouseOptionDto> _houses = new()
    {
        new() { Id = "house-dhanmondi", Name = "Dhanmondi Mess House", AreaName = "Dhanmondi", Division = "Dhaka" },
        new() { Id = "house-mirpur", Name = "Mirpur Flat 4B", AreaName = "Mirpur", Division = "Dhaka" },
    };

    public Task<IReadOnlyList<HouseOptionDto>> GetManageableHousesAsync()
        => Task.FromResult<IReadOnlyList<HouseOptionDto>>(_houses);
}