// Services/Interfaces/IAuthService.cs
using Nestify.Shared.Dtos.Auth;

namespace Nestify.Web.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);
    Task LogoutAsync();

    /// <summary>Throws ApplicationException with the API's message when it is refused.</summary>
    Task ChangePasswordAsync(string currentPassword, string newPassword);
}