using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using IdentityCore = IntelliMed.Core.Entities;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Admin-only controller for user management, role assignment, and system configuration.
/// All endpoints require the SuperAdmin or Admin role.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<IdentityCore.ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IClinicRepository _clinicRepository;
    private readonly IEmailTemplateRepository _emailTemplateRepository;
    private readonly IEmailSender _emailSender;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<IdentityCore.ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IClinicRepository clinicRepository,
        IEmailTemplateRepository emailTemplateRepository,
        IEmailSender emailSender,
        INotificationRepository notificationRepository,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _clinicRepository = clinicRepository;
        _emailTemplateRepository = emailTemplateRepository;
        _emailSender = emailSender;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    private async Task<UserDto> ToUserDtoAsync(IdentityCore.ApplicationUser user, IList<string> roles)
    {
        var clinics = (await _clinicRepository.GetMyClinicsAsync(user.Id)).ToList();
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Roles = roles,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            InviteSentAt = user.InviteSentAt,
            ClinicIds = clinics.Select(c => c.Id).ToList(),
            ClinicNames = clinics.Select(c => c.Name).ToList()
        };
    }

    // =========================================================================
    // USER MANAGEMENT ENDPOINTS
    // =========================================================================

    /// <summary>
    /// Get all users with their roles.
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        var users = _userManager.Users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(await ToUserDtoAsync(user, roles.ToList()));
        }

        return Ok(userDtos);
    }

    /// <summary>
    /// Get a single user by ID.
    /// </summary>
    [HttpGet("users/{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new UserManagementResponse { Success = false, Message = "User not found." });

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(await ToUserDtoAsync(user, roles.ToList()));
    }

    /// <summary>
    /// Create a new user with optional role assignments.
    /// </summary>
    [HttpPost("users")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserManagementResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        // Validate roles exist
        foreach (var role in request.Roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                return BadRequest(new UserManagementResponse
                {
                    Success = false,
                    Message = $"Role '{role}' does not exist."
                });
            }
        }

        var user = new IdentityCore.ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var password = string.IsNullOrWhiteSpace(request.Password) ? PasswordGenerator.Generate() : request.Password;
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Failed to create user {Email}: {Errors}", request.Email, errors);
            return BadRequest(new UserManagementResponse
            {
                Success = false,
                Message = $"Failed to create user: {errors}"
            });
        }

        // Assign roles
        if (request.Roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, request.Roles);
        }

        // Assign clinics
        if (request.ClinicIds.Count > 0)
        {
            await _clinicRepository.SetUserClinicsAsync(user.Id, request.ClinicIds);
        }

        var assignedRoles = await _userManager.GetRolesAsync(user);

        _logger.LogInformation("User {Email} created successfully with roles: {Roles}",
            request.Email, string.Join(", ", assignedRoles));

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new UserManagementResponse
        {
            Success = true,
            Message = "User created successfully.",
            User = await ToUserDtoAsync(user, assignedRoles.ToList())
        });
    }

    /// <summary>
    /// Update a user's profile, status, and roles.
    /// </summary>
    [HttpPut("users/{id}")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserManagementResponse>> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new UserManagementResponse { Success = false, Message = "User not found." });

        // Update profile
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.IsActive = request.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return BadRequest(new UserManagementResponse
            {
                Success = false,
                Message = $"Failed to update user: {errors}"
            });
        }

        // Update roles if provided
        if (request.Roles != null)
        {
            // Validate all requested roles exist
            foreach (var role in request.Roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    return BadRequest(new UserManagementResponse
                    {
                        Success = false,
                        Message = $"Role '{role}' does not exist."
                    });
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Except(request.Roles).ToList();
            var rolesToAdd = request.Roles.Except(currentRoles).ToList();

            if (rolesToRemove.Count > 0)
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

            if (rolesToAdd.Count > 0)
                await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        // Update clinic assignments if provided
        if (request.ClinicIds != null)
        {
            await _clinicRepository.SetUserClinicsAsync(user.Id, request.ClinicIds);
        }

        var finalRoles = await _userManager.GetRolesAsync(user);

        _logger.LogInformation("User {Email} updated. Active: {Active}, Roles: {Roles}",
            user.Email, user.IsActive, string.Join(", ", finalRoles));

        return Ok(new UserManagementResponse
        {
            Success = true,
            Message = "User updated successfully.",
            User = await ToUserDtoAsync(user, finalRoles.ToList())
        });
    }

    /// <summary>
    /// Delete (deactivate) a user. SuperAdmin users cannot be deleted.
    /// </summary>
    [HttpDelete("users/{id}")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserManagementResponse>> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new UserManagementResponse { Success = false, Message = "User not found." });

        // Prevent deleting SuperAdmin users
        if (await _userManager.IsInRoleAsync(user, "SuperAdmin"))
        {
            return BadRequest(new UserManagementResponse
            {
                Success = false,
                Message = "SuperAdmin users cannot be deleted."
            });
        }

        // Soft delete: deactivate instead of removing
        user.IsActive = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {Email} deactivated", user.Email);

        return Ok(new UserManagementResponse
        {
            Success = true,
            Message = "User deactivated successfully."
        });
    }

    /// <summary>
    /// Reset a user's password (admin-initiated).
    /// </summary>
    [HttpPost("users/{id}/reset-password")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserManagementResponse>> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new UserManagementResponse { Success = false, Message = "User not found." });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(new UserManagementResponse
            {
                Success = false,
                Message = $"Password reset failed: {errors}"
            });
        }

        _logger.LogInformation("Password reset for user {Email}", user.Email);

        return Ok(new UserManagementResponse
        {
            Success = true,
            Message = "Password reset successfully."
        });
    }

    /// <summary>
    /// Emails the user a sign-in invite: generates a fresh temporary password (setting it via Identity's
    /// token-based reset, which is what actually authorizes the credential change) and a single-use link
    /// to /set-password that lets them keep it or choose their own. Doubles as "resend invite".
    /// </summary>
    [HttpPost("users/{id}/send-invite")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserManagementResponse>> SendInvite(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new UserManagementResponse { Success = false, Message = "User not found." });

        if (string.IsNullOrWhiteSpace(user.Email))
            return BadRequest(new UserManagementResponse { Success = false, Message = "User has no email address." });

        var template = await _emailTemplateRepository.GetActiveByEventKeyAsync(clinicId: 1, EmailEventKeys.InviteEmail);
        if (template == null)
        {
            return BadRequest(new UserManagementResponse
            {
                Success = false,
                Message = "No active email template is assigned to the 'Invite Email' event. Configure one under Email Templates first."
            });
        }

        // Set a fresh temporary password now (authorized by a reset token) so the account is usable
        // immediately, then mint a second token for the link — resetting invalidates the first one.
        var temporaryPassword = PasswordGenerator.Generate();
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, temporaryPassword);
        if (!resetResult.Succeeded)
        {
            var errors = string.Join("; ", resetResult.Errors.Select(e => e.Description));
            return BadRequest(new UserManagementResponse { Success = false, Message = $"Failed to prepare invite: {errors}" });
        }

        var setPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        var clinics = (await _clinicRepository.GetMyClinicsAsync(user.Id)).ToList();
        var roleNames = string.Join(", ", roles);
        var clinicNames = string.Join(", ", clinics.Select(c => c.Name));

        var inviteLink = $"{Request.Scheme}://{Request.Host}/set-password" +
            $"?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(setPasswordToken)}&pwd={Uri.EscapeDataString(temporaryPassword)}";

        var mergeTokens = new Dictionary<string, string>
        {
            ["FirstName"] = user.FirstName,
            ["LastName"] = user.LastName,
            ["Email"] = user.Email,
            ["RoleNames"] = roleNames,
            ["ClinicNames"] = clinicNames,
            ["InviteLink"] = inviteLink,
            ["GeneratedPassword"] = temporaryPassword
        };

        var (subject, body) = EmailTemplateRenderer.Render(template.Subject, template.BodyHtml, mergeTokens);
        var (success, error) = await _emailSender.SendAsync(user.Email, subject, body);
        if (!success)
            return BadRequest(new UserManagementResponse { Success = false, Message = $"Failed to send invite email: {error}" });

        user.InviteSentAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Invite sent to {Email} as {Roles} for {Clinics}", user.Email, roleNames, clinicNames);

        return Ok(new UserManagementResponse
        {
            Success = true,
            Message = $"Invite sent to {user.Email} as {(string.IsNullOrEmpty(roleNames) ? "(no role)" : roleNames)} to {(string.IsNullOrEmpty(clinicNames) ? "(no clinic)" : clinicNames)}.",
            User = await ToUserDtoAsync(user, roles)
        });
    }

    // =========================================================================
    // ROLE MANAGEMENT ENDPOINTS
    // =========================================================================

    /// <summary>
    /// Get all roles with their descriptions and permissions.
    /// </summary>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<RoleDto>> GetRoles()
    {
        var roles = new List<RoleDto>
        {
            new()
            {
                Name = "SuperAdmin",
                Description = "Full system access. Can manage users, roles, and all system settings.",
                Permissions = new List<string>
                {
                    "users.read", "users.create", "users.edit", "users.delete",
                    "roles.read", "roles.assign",
                    "patients.read", "patients.create", "patients.edit", "patients.delete",
                    "appointments.read", "appointments.create", "appointments.edit", "appointments.delete",
                    "invoices.read", "invoices.create", "invoices.edit", "invoices.delete",
                    "practitioners.read", "practitioners.create", "practitioners.edit", "practitioners.delete",
                    "reports.read", "settings.read", "settings.edit"
                }
            },
            new()
            {
                Name = "Admin",
                Description = "Administrative access. Can manage users and access all clinical/billing features.",
                Permissions = new List<string>
                {
                    "users.read", "users.create", "users.edit",
                    "roles.read", "roles.assign",
                    "patients.read", "patients.create", "patients.edit", "patients.delete",
                    "appointments.read", "appointments.create", "appointments.edit", "appointments.delete",
                    "invoices.read", "invoices.create", "invoices.edit", "invoices.delete",
                    "practitioners.read", "practitioners.create", "practitioners.edit",
                    "reports.read"
                }
            },
            new()
            {
                Name = "Doctor",
                Description = "Clinical access. Can manage patients, appointments, and view invoices.",
                Permissions = new List<string>
                {
                    "patients.read", "patients.create", "patients.edit",
                    "appointments.read", "appointments.create", "appointments.edit",
                    "invoices.read",
                    "practitioners.read"
                }
            },
            new()
            {
                Name = "Nurse",
                Description = "Clinical support access. Can view and update patients and appointments.",
                Permissions = new List<string>
                {
                    "patients.read", "patients.edit",
                    "appointments.read", "appointments.edit"
                }
            },
            new()
            {
                Name = "Receptionist",
                Description = "Front-desk access. Can manage patients, appointments, and invoices.",
                Permissions = new List<string>
                {
                    "patients.read", "patients.create", "patients.edit",
                    "appointments.read", "appointments.create", "appointments.edit",
                    "invoices.read", "invoices.create", "invoices.edit"
                }
            }
        };

        return Ok(roles);
    }

    /// <summary>
    /// Assign roles to a user (replaces all existing roles).
    /// </summary>
    [HttpPut("users/{id}/roles")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserManagementResponse>> AssignRoles(string id, [FromBody] AssignRolesRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(new UserManagementResponse { Success = false, Message = "User not found." });

        // Validate all roles exist
        foreach (var role in request.Roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                return BadRequest(new UserManagementResponse
                {
                    Success = false,
                    Message = $"Role '{role}' does not exist."
                });
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(request.Roles).ToList();
        var rolesToAdd = request.Roles.Except(currentRoles).ToList();

        if (rolesToRemove.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

        if (rolesToAdd.Count > 0)
            await _userManager.AddToRolesAsync(user, rolesToAdd);

        var finalRoles = await _userManager.GetRolesAsync(user);

        _logger.LogInformation("Roles updated for user {Email}: {Roles}",
            user.Email, string.Join(", ", finalRoles));

        if (rolesToRemove.Count > 0 || rolesToAdd.Count > 0)
        {
            await _notificationRepository.CreateAsync(new CreateNotificationDto
            {
                RecipientUserId = user.Id,
                Type = IdentityCore.NotificationType.RoleAssignmentChanged,
                Message = $"Your roles were updated: {string.Join(", ", finalRoles)}",
                LinkUrl = "/profile"
            });
        }

        return Ok(new UserManagementResponse
        {
            Success = true,
            Message = "Roles assigned successfully.",
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Roles = finalRoles.ToList(),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            }
        });
    }
}