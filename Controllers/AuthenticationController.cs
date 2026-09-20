using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using EquipmentManagementBackend.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace EquipmentManagementBackend.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Authentication")]
public sealed class AuthenticationController(
    IAuthenticationService authentication,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Login with an existing email and password.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        try
        {
            var session = await authentication.LoginAsync(
                request.Email,
                request.Password);

            var user = await authentication.CurrentAsync(session);

            return Ok(new TokenResponse(CreateToken(user)));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials or inactive account."
            });
        }
    }

    /// <summary>
    /// Admin creates a new application user.
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost("users")]
    [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUser(RegisterRequest request)
    {
        var user = await authentication.RegisterAsync(
            request.Name,
            request.Email,
            request.Password,
            request.Role);

        return StatusCode(
            StatusCodes.Status201Created,
            user);
    }

    /// <summary>
    /// Change the authenticated user's password.
    /// </summary>
    [Authorize]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request)
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!long.TryParse(value, out var userId))
        {
            return Unauthorized();
        }

        await authentication.ChangePasswordAsync(
            new Session(userId),
            request.CurrentPassword,
            request.NewPassword);

        return NoContent();
    }

    private string CreateToken(User user)
    {
        var jwt = configuration.GetSection("Jwt");

        var secret = jwt["Secret"]
            ?? throw new InvalidOperationException(
                "Configure Jwt:Secret.");

        var expiryMinutes =
            int.TryParse(
                jwt["ExpiryMinutes"],
                out var configuredExpiry)
                ? configuredExpiry
                : 60;

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new Claim(
                ClaimTypes.Email,
                user.Email),

            new Claim(
                ClaimTypes.Role,
                user.Role)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}

public sealed record RegisterRequest(
    [property: Required, MaxLength(150)]
    string Name,

    [property: Required, EmailAddress, MaxLength(200)]
    string Email,

    [property: Required]
    string Password,

    [property: Required]
    string Role);

public sealed record LoginRequest(
    [property: Required, EmailAddress]
    string Email,

    [property: Required]
    string Password);

public sealed record ResetPasswordRequest(
    [property: Required]
    string CurrentPassword,

    [property: Required]
    string NewPassword);

public sealed record TokenResponse(string Token);