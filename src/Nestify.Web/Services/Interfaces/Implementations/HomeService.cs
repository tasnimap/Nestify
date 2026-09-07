// src/Nestify.Web/Services/Interfaces/Implementations/HomeService.cs
// The real IHomeService, talking to api/v1/homes (User_Home.sql). The page still
// only sees HomeView / HomeRules, so nothing in MyHome.razor changes.
//
// Roles are numbered the other way round on the two sides: the database uses
// 1 Manager, 2 Co-manager, 3 Member, while HomeRole is Member = 1 .. Manager = 3.
// ToRole is the only place that knows that.
using System.Net;
using System.Net.Http.Json;
using Nestify.Shared.Dtos.Home;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class HomeService : IHomeService
{
    private const short DbManager = 1;
    private const short DbCoManager = 2;

    private readonly HttpClient _httpClient;

    public HomeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Only 204 / 404 mean "in no home yet". Anything else has to surface: a failed
    // call used to look exactly like having no home, which dropped a user who does
    // have one back onto the create/join screen.
    public async Task<HomeView?> GetMyHomeAsync()
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync("api/v1/homes/mine");
        }
        catch (HttpRequestException)
        {
            throw new ApplicationException("Could not reach the server.");
        }

        if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not load your home.");
        }

        return ToView(await response.Content.ReadFromJsonAsync<HomeDto>());
    }

    // The name and email come from the token on the API side, so both are ignored here.
    public async Task<HomeView> CreateHomeAsync(HomeDetailsRequest request, string myName, string myEmail)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/homes", ToDetails(request));
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not create the home.");
        }

        return ToView(await response.Content.ReadFromJsonAsync<HomeDto>())
               ?? throw new ApplicationException("Could not create the home.");
    }

    // Files a request rather than joining; the house decides.
    public Task<HomeActionResult> JoinHomeAsync(string joinCode, string myName, string myEmail) =>
        SendAsync(HttpMethod.Post, "api/v1/homes/join", new JoinHomeDto { JoinCode = joinCode });

    public async Task<MyJoinRequestView?> GetMyJoinRequestAsync()
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync("api/v1/homes/my-request");
        }
        catch (HttpRequestException)
        {
            throw new ApplicationException("Could not reach the server.");
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not load your join request.");
        }

        var dto = await response.Content.ReadFromJsonAsync<MyJoinRequestDto>();
        return dto is null ? null : new MyJoinRequestView
        {
            Id = dto.Id,
            HomeName = dto.HomeName,
            RequestedAtUtc = dto.RequestedAtUtc
        };
    }

    public Task<HomeActionResult> CancelMyJoinRequestAsync() =>
        SendAsync(HttpMethod.Post, "api/v1/homes/my-request/cancel");

    public Task<HomeActionResult> ApproveJoinRequestAsync(string requestId) =>
        SendAsync(HttpMethod.Post, $"api/v1/homes/mine/requests/{requestId}/approve");

    public Task<HomeActionResult> RejectJoinRequestAsync(string requestId) =>
        SendAsync(HttpMethod.Post, $"api/v1/homes/mine/requests/{requestId}/reject");

    public Task<HomeActionResult> UpdateHomeDetailsAsync(HomeDetailsRequest request) =>
        SendAsync(HttpMethod.Put, "api/v1/homes/mine", ToDetails(request));

    // The API looks the person up by the email they registered with; the typed
    // name is only what the form asked for and is not sent.
    public Task<HomeActionResult> AddMemberAsync(string name, string email) =>
        SendAsync(HttpMethod.Post, "api/v1/homes/mine/members", new AddHomeMemberDto { Email = email });

    public Task<HomeActionResult> PromoteToCoManagerAsync(string memberId) =>
        SendAsync(HttpMethod.Post, $"api/v1/homes/mine/members/{memberId}/promote");

    public Task<HomeActionResult> DemoteToMemberAsync(string memberId) =>
        SendAsync(HttpMethod.Post, $"api/v1/homes/mine/members/{memberId}/demote");

    public Task<HomeActionResult> RemoveMemberAsync(string memberId) =>
        SendAsync(HttpMethod.Delete, $"api/v1/homes/mine/members/{memberId}");

    public Task<HomeActionResult> TransferManagerAsync(string memberId) =>
        SendAsync(HttpMethod.Post, $"api/v1/homes/mine/members/{memberId}/transfer-manager");

    public Task<HomeActionResult> LeaveHomeAsync() =>
        SendAsync(HttpMethod.Post, "api/v1/homes/mine/leave");

    private async Task<HomeActionResult> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        try
        {
            var response = await _httpClient.SendAsync(request);
            var message = await ReadMessageAsync(response);
            return new HomeActionResult(response.IsSuccessStatusCode, message ?? "Done.");
        }
        catch (HttpRequestException)
        {
            return new HomeActionResult(false, "Could not reach the server.");
        }
    }

    private static HomeDetailsDto ToDetails(HomeDetailsRequest request) => new()
    {
        Name = request.Name,
        AddressLine = request.AddressLine,
        AreaName = request.AreaName,
        Division = request.Division,
        Latitude = request.Latitude,
        Longitude = request.Longitude
    };

    private static HomeView? ToView(HomeDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        return new HomeView
        {
            Id = dto.Id,
            Name = dto.Name,
            AddressLine = dto.AddressLine,
            AreaName = dto.AreaName,
            Division = dto.Division,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            JoinCode = dto.JoinCode,
            CreatedAtUtc = dto.CreatedAtUtc,
            PendingRequests = dto.PendingRequests.Select(r => new HomeJoinRequestView
            {
                Id = r.Id,
                Name = r.Name,
                Email = r.Email,
                RequestedAtUtc = r.RequestedAtUtc
            }).ToList(),
            Members = dto.Members.Select(m => new HomeMemberView
            {
                Id = m.Id,
                Name = m.Name,
                Email = m.Email,
                Role = ToRole(m.Role),
                JoinedOnUtc = m.JoinedAtUtc,
                IsMe = m.IsMe
            }).ToList()
        };
    }

    private static HomeRole ToRole(short dbRole) => dbRole switch
    {
        DbManager => HomeRole.Manager,
        DbCoManager => HomeRole.CoManager,
        _ => HomeRole.Member
    };

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<MessageBody>();
            return body?.Message;
        }
        catch
        {
            return null;
        }
    }

    private sealed class MessageBody
    {
        public string? Message { get; set; }
    }
}
