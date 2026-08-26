// src/Nestify.Shared/Dtos/Auth/LoginRequestDto.cs
namespace Nestify.Shared.Dtos.Auth;

public sealed class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}