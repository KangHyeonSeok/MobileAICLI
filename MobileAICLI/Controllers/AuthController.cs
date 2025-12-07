using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;
using MobileAICLI.Services;
using System.Security.Claims;

namespace MobileAICLI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, IOptions<MobileAICLISettings> settings, ILogger<AuthController> logger)
    {
        _authService = authService;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (success, errorMessage) = await _authService.ValidatePasswordAsync(request.Password, ipAddress);

        if (success)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "Administrator"),
                new Claim("AuthenticatedAt", DateTime.UtcNow.ToString("O"))
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(_settings.SessionTimeoutMinutes),
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return Ok(new { success = true });
        }

        return Ok(new { success = false, error = errorMessage });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true });
    }
}

public class LoginRequest
{
    public string Password { get; set; } = string.Empty;
}
