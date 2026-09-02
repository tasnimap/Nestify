using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Auth;
using Nestify.Shared.Dtos.Auth;

namespace Nestify.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterRequestDto request)
    {
        var (data, error) = await _auth.RegisterAsync(request, CallerIp());
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginRequestDto request)
    {
        var (data, error) = await _auth.LoginAsync(request, CallerIp());
        return data is null ? Unauthorized(new { message = error }) : Ok(data);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshRequestDto request)
    {
        var (data, error) = await _auth.RefreshAsync(request.RefreshToken, CallerIp());
        return data is null ? Unauthorized(new { message = error }) : Ok(data);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequestDto request)
    {
        await _auth.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    private string? CallerIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
