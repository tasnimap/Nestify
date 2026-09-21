using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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

    [HttpPost("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto request)
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(sub, out var userId))
        {
            return Unauthorized();
        }

        var error = await _auth.ChangePasswordAsync(userId, request);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    private string? CallerIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
