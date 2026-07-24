using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
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
    private readonly IProviderGroupRepository _providerGroupRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<IdentityCore.ApplicationUser> userManager,
        SignInManager<IdentityCore.ApplicationUser> signInManager,
        IProviderGroupRepository providerGroupRepository,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _providerGroupRepository = providerGroupRepository;
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

    /// <summary>
    /// Get the current authenticated user's own editable profile.
    /// </summary>
    [HttpGet("me/profile")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> GetMyProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var group = user.GroupId.HasValue ? await _providerGroupRepository.GetByIdAsync(user.GroupId.Value) : null;

        return Ok(new UserProfileDto
        {
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles.ToList(),
            Title = user.Title,
            MiddleName = user.MiddleName,
            MobilePhone = user.MobilePhone,
            BusinessHoursPhone = user.BusinessHoursPhone,
            Fax = user.Fax,
            Qualifications = user.Qualifications,
            Specialty = user.Specialty,
            ProviderNumber = user.ProviderNumber,
            AhpraNumber = user.AhpraNumber,
            HpiiNumber = user.HpiiNumber,
            Note = user.Note,
            VocationallyRegistered = user.VocationallyRegistered,
            InternalProvider = user.InternalProvider,
            EPrescribingEnabled = user.EPrescribingEnabled,
            GroupId = user.GroupId,
            GroupName = group?.Name,
            ResidentialAddress = user.ResidentialAddress,
            ResidentialSuburb = user.ResidentialSuburb,
            ResidentialPostcode = user.ResidentialPostcode,
            ResidentialState = user.ResidentialState,
            PostalSameAsResidential = user.PostalSameAsResidential,
            PostalAddress = user.PostalAddress,
            PostalSuburb = user.PostalSuburb,
            PostalPostcode = user.PostalPostcode,
            PostalState = user.PostalState
        });
    }

    /// <summary>
    /// Update the current authenticated user's own profile.
    /// </summary>
    [HttpPut("me/profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Title = request.Title;
        user.MiddleName = request.MiddleName;
        user.MobilePhone = request.MobilePhone;
        user.BusinessHoursPhone = request.BusinessHoursPhone;
        user.Fax = request.Fax;
        user.Qualifications = request.Qualifications;
        user.Specialty = request.Specialty;
        user.ProviderNumber = request.ProviderNumber;
        user.AhpraNumber = request.AhpraNumber;
        user.HpiiNumber = request.HpiiNumber;
        user.Note = request.Note;
        user.VocationallyRegistered = request.VocationallyRegistered;
        user.InternalProvider = request.InternalProvider;
        user.EPrescribingEnabled = request.EPrescribingEnabled;
        user.GroupId = request.GroupId;
        user.ResidentialAddress = request.ResidentialAddress;
        user.ResidentialSuburb = request.ResidentialSuburb;
        user.ResidentialPostcode = request.ResidentialPostcode;
        user.ResidentialState = request.ResidentialState;
        user.PostalSameAsResidential = request.PostalSameAsResidential;
        user.PostalAddress = request.PostalSameAsResidential ? request.ResidentialAddress : request.PostalAddress;
        user.PostalSuburb = request.PostalSameAsResidential ? request.ResidentialSuburb : request.PostalSuburb;
        user.PostalPostcode = request.PostalSameAsResidential ? request.ResidentialPostcode : request.PostalPostcode;
        user.PostalState = request.PostalSameAsResidential ? request.ResidentialState : request.PostalState;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return BadRequest(ModelState);
        }

        return NoContent();
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