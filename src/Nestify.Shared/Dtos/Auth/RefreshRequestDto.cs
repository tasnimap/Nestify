// src/Nestify.Shared/Dtos/Auth/RefreshRequestDto.cs
namespace Nestify.Shared.Dtos.Auth;

public sealed class RefreshRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
