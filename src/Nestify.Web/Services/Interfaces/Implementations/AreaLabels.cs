// src/Nestify.Web/Services/Interfaces/Implementations/AreaLabels.cs
// Picks the label shown in an area dropdown. Values and filters keep using the
// English name / id; only the visible text follows the selected language.
using System.Globalization;
using Nestify.Shared.Dtos.Area;

namespace Nestify.Web.Services.Implementations;

public static class AreaLabels
{
    private static bool IsBangla =>
        CultureInfo.CurrentUICulture.Name.StartsWith("bn", StringComparison.OrdinalIgnoreCase);

    private static string Pick(string name, string? bnName) =>
        IsBangla && !string.IsNullOrWhiteSpace(bnName) ? bnName : name;

    public static string Label(this DivisionDto d) => Pick(d.Name, d.BnName);
    public static string Label(this DistrictDto d) => Pick(d.Name, d.BnName);
    public static string Label(this UpazilaDto u) => Pick(u.Name, u.BnName);
}
