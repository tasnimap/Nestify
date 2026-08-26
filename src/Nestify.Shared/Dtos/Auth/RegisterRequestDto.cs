// src/Nestify.Shared/Dtos/Auth/RegisterRequestDto.cs
namespace Nestify.Shared.Dtos.Auth;

public sealed class RegisterRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
}