using Nestify.Shared.Dtos.Area;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MockAreaService : IAreaService
{
    private readonly List<DivisionDto> _divisions = new()
    {
        new(1, "Dhaka", "ঢাকা"),
        new(2, "Chattogram", "চট্টগ্রাম"),
        new(3, "Sylhet", "সিলেট"),
    };

    private readonly List<DistrictDto> _districts = new()
    {
        new(101, 1, "Dhaka", "ঢাকা"),
        new(102, 1, "Gazipur", "গাজীপুর"),
        new(103, 1, "Narayanganj", "নারায়ণগঞ্জ"),
        new(201, 2, "Chattogram", "চট্টগ্রাম"),
        new(202, 2, "Cox's Bazar", "কক্সবাজার"),
        new(301, 3, "Sylhet", "সিলেট"),
        new(302, 3, "Moulvibazar", "মৌলভীবাজার"),
    };

    private readonly List<UpazilaDto> _upazilas = new()
    {
        new(10101, 101, "Dhanmondi", "ধানমন্ডি"),
        new(10102, 101, "Mirpur", "মিরপুর"),
        new(10103, 101, "Mohammadpur", "মোহাম্মদপুর"),
        new(10201, 102, "Tongi", "টঙ্গী"),
        new(10202, 102, "Sreepur", "শ্রীপুর"),
        new(10301, 103, "Siddhirganj", "সিদ্ধিরগঞ্জ"),
        new(20101, 201, "Panchlaish", "পাঁচলাইশ"),
        new(20102, 201, "Kotwali", "কোতোয়ালী"),
        new(20201, 202, "Cox's Bazar Sadar", "কক্সবাজার সদর"),
        new(30101, 301, "Sylhet Sadar", "সিলেট সদর"),
        new(30201, 302, "Sreemangal", "শ্রীমঙ্গল"),
    };

    public Task<IReadOnlyList<DivisionDto>> GetDivisionsAsync()
        => Task.FromResult<IReadOnlyList<DivisionDto>>(_divisions);

    public Task<IReadOnlyList<DistrictDto>> GetDistrictsAsync(int divisionId)
        => Task.FromResult<IReadOnlyList<DistrictDto>>(
            _districts.Where(d => d.DivisionId == divisionId).ToList());

    public Task<IReadOnlyList<UpazilaDto>> GetUpazilasAsync(int districtId)
        => Task.FromResult<IReadOnlyList<UpazilaDto>>(
            _upazilas.Where(u => u.DistrictId == districtId).ToList());
}