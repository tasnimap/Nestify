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

    public async Task<decimal> GetVerificationFeeAsync()
    {
        var fee = await _httpClient.GetFromJsonAsync<VerificationFeeDto>("api/v1/profile/me/verification/fee");
        return fee?.AmountBdt ?? 0;
    }

    public async Task<VerificationPaymentDto> PayVerificationFeeAsync(string bkashNumber, string pin)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/profile/me/verification/payment",
            new BkashPaymentDto { BkashNumber = bkashNumber, Pin = pin });
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "The bKash payment did not go through.");
        }

        return await response.Content.ReadFromJsonAsync<VerificationPaymentDto>()
               ?? throw new ApplicationException("The bKash payment did not go through.");
    }

    public async Task<UserProfileDto?> SubmitVerificationAsync(string identityDocumentType, VerificationUpload identity,
        VerificationUpload? occupation, string paymentId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(identityDocumentType), "identityDocumentType");
        form.Add(new StringContent(paymentId), "paymentId");
        form.Add(FilePart(identity), "identityFile", identity.FileName);
        if (occupation is not null)
        {
            form.Add(FilePart(occupation), "occupationFile", occupation.FileName);
        }

        var response = await _httpClient.PostAsync("api/v1/profile/me/verification", form);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not submit your verification.");
        }

        return Remember(await response.Content.ReadFromJsonAsync<UserProfileDto>());
    }

    private static StreamContent FilePart(VerificationUpload upload)
    {
        var part = new StreamContent(upload.Content);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(upload.ContentType);
        return part;
    }

    public async Task<UserProfileDto?> CancelVerificationAsync()
    {
        var response = await _httpClient.DeleteAsync("api/v1/profile/me/verification");
        if (!response.IsSuccessStatusCode)
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not cancel your verification application.");
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
