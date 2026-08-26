// Services/Interfaces/IAuthService.cs
using Nestify.Shared.Dtos.Auth;

namespace Nestify.Web.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);
    Task LogoutAsync();
}