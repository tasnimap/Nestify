// Auth/AuthorizationMessageHandler.cs
using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace Nestify.Web.Auth;

// Attaches the stored access token to every request the app makes to the API.
public sealed class AuthorizationMessageHandler : DelegatingHandler
{
    private const string TokenStorageKey = "authToken";
    private readonly ILocalStorageService _localStorage;

    public AuthorizationMessageHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            var token = await _localStorage.GetItemAsync<string>(TokenStorageKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
