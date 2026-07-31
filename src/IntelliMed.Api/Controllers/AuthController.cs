using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using IdentityCore = IntelliMed.Core.Entities;

namespace IntelliMed.Api.Controllers;

// Must stay reachable without a token — Login/Logout are the entry point before one exists, and
// the /me* endpoints already do their own User.Identity?.IsAuthenticated checks internally.
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityCore.ApplicationUser> _userManager;
    private readonly SignInManager<IdentityCore.ApplicationUser> _signInManager;
    private readonly IProviderGroupRepository _providerGroupRepository;
    private readonly IProviderScheduleRepository _providerScheduleRepository;
    private readonly IEmailTemplateRepository _emailTemplateRepository;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<IdentityCore.ApplicationUser> userManager,
        SignInManager<IdentityCore.ApplicationUser> signInManager,
        IProviderGroupRepository providerGroupRepository,
        IProviderScheduleRepository providerScheduleRepository,
        IEmailTemplateRepository emailTemplateRepository,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _providerGroupRepository = providerGroupRepository;
        _providerScheduleRepository = providerScheduleRepository;
        _emailTemplateRepository = emailTemplateRepository;
        _emailSender = emailSender;
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
            DefaultAppointmentTimeslotMinutes = user.DefaultAppointmentTimeslotMinutes,
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
        user.DefaultAppointmentTimeslotMinutes = request.DefaultAppointmentTimeslotMinutes;
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

    /// <summary>
    /// Get the current user's self-service weekly working hours (Profile Settings &gt; Weekly Schedule).
    /// Always returns all 7 days — days never configured come back with IsActive = false.
    /// </summary>
    [HttpGet("me/schedule")]
    [ProducesResponseType(typeof(List<ProviderScheduleDayDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySchedule()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var schedule = await _providerScheduleRepository.GetByUserIdAsync(user.Id);
        return Ok(schedule);
    }

    /// <summary>
    /// Bulk-replace the current user's weekly working hours.
    /// </summary>
    [HttpPut("me/schedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetMySchedule([FromBody] SetProviderScheduleRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        await _providerScheduleRepository.SetScheduleAsync(user.Id, request);
        return NoContent();
    }

    /// <summary>
    /// Requests a password reset email. Always returns success regardless of whether the account
    /// exists, to avoid leaking which email addresses are registered.
    /// </summary>
    [HttpPost("me/forgot-password")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserManagementResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user != null && user.IsActive)
        {
            var template = await _emailTemplateRepository.GetActiveByEventKeyAsync(clinicId: 1, EmailEventKeys.ForgotPassword);
            if (template != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetLink = $"{Request.Scheme}://{Request.Host}/set-password" +
                    $"?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

                var mergeTokens = new Dictionary<string, string>
                {
                    ["FirstName"] = user.FirstName,
                    ["LastName"] = user.LastName,
                    ["Email"] = user.Email ?? string.Empty,
                    ["ResetLink"] = resetLink
                };

                var (subject, body) = EmailTemplateRenderer.Render(template.Subject, template.BodyHtml, mergeTokens);
                var (success, error) = await _emailSender.SendAsync(user.Email!, subject, body);
                if (!success)
                    _logger.LogWarning("Forgot-password email to {Email} failed: {Error}", user.Email, error);
                else
                    _logger.LogInformation("Forgot-password email sent to {Email}", user.Email);
            }
            else
            {
                _logger.LogWarning("Forgot-password requested but no active 'ForgotPassword' email template is configured.");
            }
        }

        return Ok(new UserManagementResponse
        {
            Success = true,
            Message = "If that account exists, a password reset email has been sent."
        });
    }

    /// <summary>
    /// Completes an invite or forgot-password flow: sets the user's password using the token emailed to them.
    /// </summary>
    [HttpPost("me/set-password")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserManagementResponse>> SetPassword([FromBody] SetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            return BadRequest(new UserManagementResponse { Success = false, Message = "This link is invalid or has expired." });

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(new UserManagementResponse { Success = false, Message = $"Failed to set password: {errors}" });
        }

        _logger.LogInformation("Password set via invite/reset link for user {Email}", user.Email);

        return Ok(new UserManagementResponse { Success = true, Message = "Password set successfully. You can now log in." });
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