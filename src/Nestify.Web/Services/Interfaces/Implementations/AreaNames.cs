// src/Nestify.Web/Services/Interfaces/Implementations/AreaNames.cs
// The area cascade selects ids, while the mock listings and helper profiles still
// carry place names. This turns one set into the other so the browse filters can
// compare them. It goes away once the modules store upazila_id on their rows.
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public static class AreaNames
{
    public sealed record Selection(string? Division, string? District, string? Upazila);

    public static async Task<Selection> ResolveAsync(
        IAreaService areas, int? divisionId, int? districtId, int? upazilaId)
    {
        if (divisionId is not int division)
        {
            return new Selection(null, null, null);
        }

        var divisionName = (await areas.GetDivisionsAsync())
            .FirstOrDefault(d => d.Id == division)?.Name;

        if (districtId is not int district)
        {
            return new Selection(divisionName, null, null);
        }

        var districtName = (await areas.GetDistrictsAsync(division))
            .FirstOrDefault(d => d.Id == district)?.Name;

        if (upazilaId is not int upazila)
        {
            return new Selection(divisionName, districtName, null);
        }

        var upazilaName = (await areas.GetUpazilasAsync(district))
            .FirstOrDefault(u => u.Id == upazila)?.Name;

        return new Selection(divisionName, districtName, upazilaName);
    }
}
