namespace Nestify.Shared.Dtos.Assistant;

/// <summary>A single safe, user-visible turn supplied to the Nestify assistant.</summary>
public sealed class AssistantChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class AssistantChatRequestDto
{
    public string Message { get; set; } = string.Empty;

    // The browser retains only a short, user-visible history. The API validates
    // and caps it again before it is sent to the model.
    public List<AssistantChatMessageDto> History { get; set; } = new();
}

public sealed class AssistantChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
}
