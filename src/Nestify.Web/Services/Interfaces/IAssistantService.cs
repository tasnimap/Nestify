namespace Nestify.Web.Services.Interfaces;

public sealed record AssistantChatMessage(string Role, string Text);

public interface IAssistantService
{
    Task<string> AskAsync(string message, IReadOnlyCollection<AssistantChatMessage> history);
}
