using System.Net.Http.Headers;
using System.Text.Json;

namespace Nestify.Api.Profiles;

/// <summary>
/// The cloud name out of CLOUDINARY_URL (cloudinary://key:secret@cloud_name) and
/// the unsigned upload preset named by UPLOAD_PICTURE.
/// </summary>
public sealed class CloudinarySettings
{
    public string CloudName { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;
    public string UploadPreset { get; init; } = "Nestify";

    public static CloudinarySettings Parse(string cloudinaryUrl, string? uploadPreset)
    {
        var uri = new Uri(cloudinaryUrl);
        var parts = uri.UserInfo.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("CLOUDINARY_URL must look like cloudinary://api_key:api_secret@cloud_name.");
        }

        return new CloudinarySettings
        {
            CloudName = uri.Host,
            ApiKey = Uri.UnescapeDataString(parts[0]),
            ApiSecret = Uri.UnescapeDataString(parts[1]),
            UploadPreset = string.IsNullOrWhiteSpace(uploadPreset) ? "Nestify" : uploadPreset.Trim()
        };
    }
}

/// <summary>
/// Uploads a picture to Cloudinary with the unsigned preset and hands back the
/// hosted link. The upload goes through the API so the browser never has to know
/// the Cloudinary account details.
/// </summary>
public sealed class CloudinaryUploader
{
    private readonly HttpClient _httpClient;
    private readonly CloudinarySettings _settings;

    public CloudinaryUploader(HttpClient httpClient, CloudinarySettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<(string? Url, string? Error)> UploadAsync(Stream file, string fileName, string? contentType)
    {
        using var content = new MultipartFormDataContent();
        AddField(content, "upload_preset", _settings.UploadPreset);

        var filePart = new StreamContent(file);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            filePart.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        filePart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"file\"",
            FileName = "\"" + Path.GetFileName(fileName) + "\""
        };
        content.Add(filePart);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(
                $"https://api.cloudinary.com/v1_1/{_settings.CloudName}/image/upload", content);
        }
        catch (HttpRequestException)
        {
            return (null, "Could not reach Cloudinary. Try again in a moment.");
        }

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return (null, ReadError(body) ?? "Cloudinary rejected the upload.");
        }

        using var json = JsonDocument.Parse(body);
        var url = json.RootElement.TryGetProperty("secure_url", out var secure)
            ? secure.GetString()
            : null;

        return string.IsNullOrWhiteSpace(url)
            ? (null, "Cloudinary did not return a picture link.")
            : (url, null);
    }

    // Cloudinary only reads the fields when their names are quoted, which the
    // MultipartFormDataContent.Add overloads do not do.
    private static void AddField(MultipartFormDataContent content, string name, string value)
    {
        var part = new StringContent(value);
        part.Headers.ContentType = null;
        part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"" + name + "\""
        };
        content.Add(part);
    }

    private static string? ReadError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("error", out var error)
                   && error.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
