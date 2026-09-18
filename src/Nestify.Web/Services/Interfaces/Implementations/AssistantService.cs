using System.Net;
using System.Net.Http.Json;
using Nestify.Shared.Dtos.Assistant;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class AssistantService : IAssistantService
{
    private readonly HttpClient _httpClient;

    public AssistantService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> AskAsync(string message, IReadOnlyCollection<AssistantChatMessage> history)
    {
        var request = new AssistantChatRequestDto
        {
            Message = message,
            History = history.TakeLast(8).Select(m => new AssistantChatMessageDto
            {
                Role = m.Role,
                Text = m.Text
            }).ToList()
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("api/v1/assistant/chat", request);
        }
        catch (HttpRequestException)
        {
            return "I can't reach the Nestify server right now. Please try again shortly.";
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadMessageAsync(response);
            return response.StatusCode == HttpStatusCode.Unauthorized
                ? "Your session has ended. Please sign in again."
                : error ?? "I couldn't answer that right now. Please try again.";
        }

        var result = await response.Content.ReadFromJsonAsync<AssistantChatResponseDto>();
        return string.IsNullOrWhiteSpace(result?.Reply)
            ? "I couldn't produce a response for that. Please try again."
            : result.Reply;
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<MessageBody>())?.Message;
        }
        catch
        {
            return null;
        }
    }

    private sealed class MessageBody { public string? Message { get; set; } }
}
