using Common.Auth.Jwt;
using Common.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly JwtTokenGenerator _tokenGenerator;

    public AuthController(IUserService userService, JwtTokenGenerator tokenGenerator)
    {
        _userService = userService;
        _tokenGenerator = tokenGenerator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthRequest request)
    {
        var isValid = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
        if (!isValid) return Unauthorized("Invalid username or password.");

        var user = await _userService.GetByUsernameAsync(request.Username);
        var token = _tokenGenerator.GenerateToken(new AppUser
        {
            Id = user!.Id,
            Username = user!.Username,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role
        });

        return Ok(new AuthResponse { Token = token, Role = user.Role });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AuthRequest request)
    {
        var existing = await _userService.GetByUsernameAsync(request.Username);
        if (existing != null)
            return BadRequest("Username already exists");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = $"{request.Username}@demo.com",
            FullName = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "User"
        };

        await _userService.CreateAsync(user); // ⬅️ add this method in IUserService
        return Ok("User created");
    }

    [HttpGet("me")]
    [Authorize] // anyone with valid token
    public IActionResult Me()
    {
        var username = User.Identity?.Name; // Comes from JwtRegisteredClaimNames.Sub
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;        
        var fullname = User.FindFirst("fullname")?.Value;          

        var expUnix = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        DateTime? expiresAt = null;
        if (long.TryParse(expUnix, out var expSeconds))
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
        }

        return Ok(new
        {
            Username = username,
            FullName = fullname,
            Email = email,
            Role = role,
            ExpiresAtUtc = expiresAt
        });
    }

    [HttpGet("/admin/users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userService.GetAllAsync();

        var response = users.Select(u => new
        {
            u.Id,
            u.Username,
            u.Email,
            u.FullName,
            u.Role,
            u.CreatedAt
        });

        return Ok(response);
    }
}
