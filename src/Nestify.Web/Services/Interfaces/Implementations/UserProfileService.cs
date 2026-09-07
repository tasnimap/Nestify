// src/Nestify.Web/Services/Interfaces/Implementations/UserProfileService.cs
using System.Net;
using System.Net.Http.Json;
using Nestify.Shared.Dtos.Profile;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class UserProfileService : IUserProfileService
{
    private readonly HttpClient _httpClient;

    public UserProfileService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public UserProfileDto? Current { get; private set; }

    public event Action? Changed;

    public void Clear()
    {
        Current = null;
        Changed?.Invoke();
    }

    public async Task<UserProfileDto?> GetMyProfileAsync()
    {
        var response = await _httpClient.GetAsync("api/v1/profile/me");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return Remember(await response.Content.ReadFromJsonAsync<UserProfileDto>());
    }

    public async Task<UserProfileDto?> UpdateMyProfileAsync(UpdateUserProfileDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync("api/v1/profile/me", dto);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not save your profile.");
        }

        return Remember(await response.Content.ReadFromJsonAsync<UserProfileDto>());
    }

    public async Task<UserProfileDto?> UploadPictureAsync(Stream content, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var filePart = new StreamContent(content);
        filePart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(filePart, "file", fileName);

        var response = await _httpClient.PostAsync("api/v1/profile/me/picture", form);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not upload the picture.");
        }

        return Remember(await response.Content.ReadFromJsonAsync<UserProfileDto>());
    }

    private UserProfileDto? Remember(UserProfileDto? profile)
    {
        if (profile is not null)
        {
            Current = profile;
            Changed?.Invoke();
        }

        return profile;
    }

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
