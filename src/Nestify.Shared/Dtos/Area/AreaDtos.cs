namespace Nestify.Shared.Dtos.Area;

public sealed record DivisionDto(int Id, string Name, string BnName);

public sealed record DistrictDto(int Id, int DivisionId, string Name, string BnName);

public sealed record UpazilaDto(int Id, int DistrictId, string Name, string BnName);

/// <summary>
/// The three-level selection AreaCascade binds to. Pages read the ids they need
/// (division/district/upazila) into their own filter or create DTOs.
/// </summary>
public sealed class AreaSelectionDto
{
    public int? DivisionId { get; set; }
    public int? DistrictId { get; set; }
    public int? UpazilaId { get; set; }
}