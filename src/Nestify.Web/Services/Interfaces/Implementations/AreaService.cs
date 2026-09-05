// src/Nestify.Web/Services/Interfaces/Implementations/AreaService.cs
// Reads the seeded administrative tables through the API. The lists never change
// while the app is running, so each one is fetched once and kept.
using System.Net.Http.Json;
using Nestify.Shared.Dtos.Area;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class AreaService : IAreaService
{
    private readonly HttpClient _httpClient;

    private IReadOnlyList<DivisionDto>? _divisions;
    private readonly Dictionary<int, IReadOnlyList<DistrictDto>> _districts = new();
    private readonly Dictionary<int, IReadOnlyList<UpazilaDto>> _upazilas = new();

    public AreaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<DivisionDto>> GetDivisionsAsync()
    {
        _divisions ??= await GetAsync<DivisionDto>("api/v1/areas/divisions");
        return _divisions;
    }

    public async Task<IReadOnlyList<DistrictDto>> GetDistrictsAsync(int divisionId)
    {
        if (!_districts.TryGetValue(divisionId, out var districts))
        {
            districts = await GetAsync<DistrictDto>($"api/v1/areas/divisions/{divisionId}/districts");
            _districts[divisionId] = districts;
        }

        return districts;
    }

    public async Task<IReadOnlyList<UpazilaDto>> GetUpazilasAsync(int districtId)
    {
        if (!_upazilas.TryGetValue(districtId, out var upazilas))
        {
            upazilas = await GetAsync<UpazilaDto>($"api/v1/areas/districts/{districtId}/upazilas");
            _upazilas[districtId] = upazilas;
        }

        return upazilas;
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(string url)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<T>>(url) ?? new List<T>();
        }
        catch (HttpRequestException)
        {
            // The API being down should leave the dropdown empty, not break the page.
            return Array.Empty<T>();
        }
    }
}
