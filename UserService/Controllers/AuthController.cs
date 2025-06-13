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

    public AuthController(IUserService userService, IKeycloakService keycloakService)
    {
        _userService = userService;
        _keycloakService = keycloakService;
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
}
