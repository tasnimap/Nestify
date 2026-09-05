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
            "SELECT id, name, bn_name AS BnName FROM divisions ORDER BY name");
        return Ok(rows.ToList());
    }

    [HttpGet("divisions/{divisionId:int}/districts")]
    public async Task<ActionResult<IReadOnlyList<DistrictDto>>> GetDistricts(int divisionId)
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<DistrictDto>(
            @"SELECT id, division_id AS DivisionId, name, bn_name AS BnName
              FROM districts
              WHERE division_id = @divisionId
              ORDER BY name",
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
            @"SELECT id, district_id AS DistrictId, name, COALESCE(bn_name, name) AS BnName
              FROM upazilas
              WHERE district_id = @districtId
              ORDER BY is_metropolitan_thana DESC, name",
            new { districtId });
        return Ok(rows.ToList());
    }
}
