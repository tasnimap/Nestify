using Dapper;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Area;

namespace Nestify.Api.Controllers;

// Reference data from Bangladesh_Administrative_Structure.sql. Read only, and the
// same for everyone, so it needs no token.
[ApiController]
[Route("api/v1/areas")]
public sealed class AreasController : ControllerBase
{
    private readonly DbConnectionFactory _db;

    public AreasController(DbConnectionFactory db)
    {
        _db = db;
    }

    [HttpGet("divisions")]
    public async Task<ActionResult<IReadOnlyList<DivisionDto>>> GetDivisions()
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<DivisionDto>(
            @"SELECT d.id, d.name, COALESCE(t.name_bn, d.bn_name, d.name) AS BnName
              FROM divisions d
              LEFT JOIN area_names_bn t ON t.area_type = 'division' AND t.area_id = d.id
              ORDER BY d.name");
        return Ok(rows.ToList());
    }

    [HttpGet("divisions/{divisionId:int}/districts")]
    public async Task<ActionResult<IReadOnlyList<DistrictDto>>> GetDistricts(int divisionId)
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<DistrictDto>(
            @"SELECT d.id, d.division_id AS DivisionId, d.name,
                     COALESCE(t.name_bn, d.bn_name, d.name) AS BnName
              FROM districts d
              LEFT JOIN area_names_bn t ON t.area_type = 'district' AND t.area_id = d.id
              WHERE d.division_id = @divisionId
              ORDER BY d.name",
            new { divisionId });
        return Ok(rows.ToList());
    }

    // Metropolitan thanas come back in the same list as the rural upazilas of the
    // district; they are the same level of the hierarchy.
    [HttpGet("districts/{districtId:int}/upazilas")]
    public async Task<ActionResult<IReadOnlyList<UpazilaDto>>> GetUpazilas(int districtId)
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<UpazilaDto>(
            @"SELECT u.id, u.district_id AS DistrictId, u.name,
                     COALESCE(t.name_bn, u.bn_name, u.name) AS BnName
              FROM upazilas u
              LEFT JOIN area_names_bn t ON t.area_type = 'upazila' AND t.area_id = u.id
              WHERE u.district_id = @districtId
              ORDER BY u.is_metropolitan_thana DESC, u.name",
            new { districtId });
        return Ok(rows.ToList());
    }
}
