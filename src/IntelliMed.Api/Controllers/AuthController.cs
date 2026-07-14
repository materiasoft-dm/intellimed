using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IntelliMed.Core.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using IdentityCore = IntelliMed.Core.Entities;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityCore.ApplicationUser> _userManager;
    private readonly SignInManager<IdentityCore.ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<IdentityCore.ApplicationUser> userManager,
        SignInManager<IdentityCore.ApplicationUser> signInManager,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate user and return JWT token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            // Find user by email
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login attempt failed: User not found for email {Email}", request.Email);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid email or password"
                });
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt failed: User {Email} is deactivated", request.Email);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Account is deactivated. Please contact administrator."
                });
            }

            // Attempt to sign in
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Login attempt failed: User {Email} is locked out", request.Email);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Account is locked. Please try again later or reset your password."
                });
            }

            if (!result.Succeeded)
            {
                _logger.LogWarning("Login attempt failed: Invalid password for user {Email}", request.Email);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid email or password"
                });
            }

            // Update last login time
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Get user roles
            var roles = await _userManager.GetRolesAsync(user);

            // Generate JWT token
            var token = await GenerateJwtTokenAsync(user, roles);

            _logger.LogInformation("User {Email} logged in successfully", request.Email);

            return Ok(new LoginResponse
            {
                Success = true,
                Token = token,
                Email = user.Email,
                FullName = user.FullName,
                Roles = roles.ToList(),
                ExpiresAt = DateTime.UtcNow.AddHours(8)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", request.Email);
            return StatusCode(500, new LoginResponse
            {
                Success = false,
                ErrorMessage = "An error occurred during login. Please try again."
            });
        }
    }

    /// <summary>
    /// Logout current user.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LogoutResponse>> Logout()
    {
        try
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out successfully");
            return Ok(new LogoutResponse
            {
                Success = true,
                Message = "Logged out successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new LogoutResponse
            {
                Success = false,
                Message = "An error occurred during logout"
            });
        }
    }

    /// <summary>
    /// Get current authenticated user info.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized(new CurrentUserResponse { IsAuthenticated = false });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new CurrentUserResponse { IsAuthenticated = false });
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new CurrentUserResponse
        {
            IsAuthenticated = true,
            Email = user.Email,
            FullName = user.FullName,
            Roles = roles.ToList()
        });
    }

    private async Task<string> GenerateJwtTokenAsync(IdentityCore.ApplicationUser user, IList<string> roles)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "IntelliMed_SuperSecretKey_AtLeast32Characters!";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "IntelliMed";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "IntelliMed.Client";
        var expirationHours = int.Parse(_configuration["Jwt:ExpirationHours"] ?? "8");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new("firstName", user.FirstName),
            new("lastName", user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(expirationHours);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}