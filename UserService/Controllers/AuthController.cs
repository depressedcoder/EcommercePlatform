using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserService.DTO;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IKeycloakService _keycloakService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IKeycloakService keycloakService,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _keycloakService = keycloakService;
        _logger = logger;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Sync user from Keycloak to our database
        await _keycloakService.SyncUserFromKeycloakAsync(userId);

        var user = await _userService.GetByUsernameAsync(User.Identity?.Name ?? string.Empty);
        if (user == null)
            return NotFound();

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Roles,
            user.Attributes,
            user.IsActive,
            user.LastLoginAt
        });
    }

    [HttpGet("users")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users.Select(u => new
        {
            u.Id,
            u.Username,
            u.Email,
            u.FullName,
            u.Roles,
            u.IsActive,
            u.CreatedAt,
            u.LastLoginAt
        }));
    }

    [HttpGet("users/{userId}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> GetUser(string userId)
    {
        var user = await _keycloakService.GetUserFromKeycloakAsync(userId);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpPost("users")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var keycloakUserId = await _keycloakService.CreateUserInKeycloakAsync(dto);

        await _keycloakService.SyncUserFromKeycloakAsync(keycloakUserId);

        var user = await _userService.GetByUsernameAsync(dto.Username);
        return CreatedAtAction(nameof(GetUser), new { userId = keycloakUserId }, user);
    }

    [HttpPut("users/{userId}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserDto dto)
    {
        await _keycloakService.UpdateUserInKeycloakAsync(userId, dto);

        await _keycloakService.SyncUserFromKeycloakAsync(userId);

        var user = await _userService.GetByUsernameAsync(dto.Username);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpDelete("users/{userId}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var existing = await _keycloakService.GetUserFromKeycloakAsync(userId);
        if (existing == null)
            return NotFound();

        await _keycloakService.DeleteUserFromKeycloakAsync(userId);
        await _userService.DeleteUserAsync(existing.Username);
        return NoContent();
    }

    [HttpGet("users/{userId}/roles")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> GetUserRoles(string userId)
    {
        var roles = await _keycloakService.GetUserRolesAsync(userId);
        return Ok(roles);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegistrationResponseDto>> Register([FromBody] UserRegistrationDto dto)
    {
        try
        {
            var response = await _keycloakService.RegisterUserAsync(dto);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponseDto>> Login([FromBody] LoginDto dto)
    {
        try
        {
            var tokenResponse = await _keycloakService.LoginUserAsync(dto);
            return Ok(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login");
            return Unauthorized(new { message = "Invalid username or password." });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutDto dto)
    {
        var username = User.Identity?.Name;
        var result = await _keycloakService.LogoutAsync(dto.RefreshToken, username);
        if (result)
            return Ok(new { message = "Logout successful." });
        else
            return BadRequest(new { message = "Logout failed. Please try again." });
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponseDto>> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        try
        {
            var tokenResponse = await _keycloakService.RefreshTokenAsync(dto.RefreshToken);
            return Ok(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return Unauthorized(new { message = "Invalid refresh token." });
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            var result = await _keycloakService.SendPasswordResetEmailAsync(dto.Email);
            if (result)
            {
                return Ok(new { message = "Password reset instructions have been sent to your email." });
            }
            return NotFound(new { message = "No account found with this email address." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset email");
            return BadRequest(new { message = "Failed to process password reset request." });
        }
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<ActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
    {
        try
        {
            var result = await _keycloakService.VerifyEmailAsync(dto.UserId, dto.Token);
            if (result)
            {
                return Ok(new { message = "Email verified successfully." });
            }
            return BadRequest(new { message = "Email verification failed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email");
            return BadRequest(new { message = "Failed to verify email." });
        }
    }
}
