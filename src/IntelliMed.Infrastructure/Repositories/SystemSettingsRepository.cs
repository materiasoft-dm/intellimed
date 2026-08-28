using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class SystemSettingsRepository : ISystemSettingsRepository
{
    private const int SingletonId = 1;

    private readonly AppDbContext _context;

    public SystemSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SystemSettingsDto> GetAsync()
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Id == SingletonId)
                       ?? new SystemSettings();
        return ToDto(settings);
    }

    public async Task UpdateAsync(UpdateSystemSettingsDto dto)
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Id == SingletonId);
        if (settings == null)
        {
            settings = new SystemSettings { Id = SingletonId };
            _context.SystemSettings.Add(settings);
        }

        // Rejected rather than clamped: silently "fixing" a password policy to something the admin
        // didn't ask for is worse than telling them the value was invalid.
        Require(dto.PasswordMinLength is >= 6 and <= 128, "Password minimum length must be between 6 and 128.");
        Require(dto.LockoutMaxFailedAttempts is >= 1 and <= 100, "Lockout attempts must be between 1 and 100.");
        Require(dto.LockoutDurationMinutes is >= 1 and <= 1440, "Lockout duration must be between 1 and 1440 minutes.");
        Require(dto.SessionExpirationHours is >= 1 and <= 720, "Session length must be between 1 and 720 hours.");
        Require(dto.DefaultAppointmentSlotMinutes is >= 5 and <= 240, "Default appointment slot must be between 5 and 240 minutes.");
        Require(dto.BackupRetentionDays is >= 1 and <= 3650, "Backup retention must be between 1 and 3650 days.");

        settings.PasswordMinLength = dto.PasswordMinLength;
        settings.PasswordRequireDigit = dto.PasswordRequireDigit;
        settings.PasswordRequireLowercase = dto.PasswordRequireLowercase;
        settings.PasswordRequireUppercase = dto.PasswordRequireUppercase;
        settings.PasswordRequireNonAlphanumeric = dto.PasswordRequireNonAlphanumeric;
        settings.LockoutMaxFailedAttempts = dto.LockoutMaxFailedAttempts;
        settings.LockoutDurationMinutes = dto.LockoutDurationMinutes;
        settings.SessionExpirationHours = dto.SessionExpirationHours;
        settings.DefaultAppointmentSlotMinutes = dto.DefaultAppointmentSlotMinutes;
        settings.NewUserShowCommandPalette = dto.NewUserShowCommandPalette;
        settings.BackupRetentionDays = dto.BackupRetentionDays;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static SystemSettingsDto ToDto(SystemSettings e) => new()
    {
        PasswordMinLength = e.PasswordMinLength,
        PasswordRequireDigit = e.PasswordRequireDigit,
        PasswordRequireLowercase = e.PasswordRequireLowercase,
        PasswordRequireUppercase = e.PasswordRequireUppercase,
        PasswordRequireNonAlphanumeric = e.PasswordRequireNonAlphanumeric,
        LockoutMaxFailedAttempts = e.LockoutMaxFailedAttempts,
        LockoutDurationMinutes = e.LockoutDurationMinutes,
        SessionExpirationHours = e.SessionExpirationHours,
        DefaultAppointmentSlotMinutes = e.DefaultAppointmentSlotMinutes,
        NewUserShowCommandPalette = e.NewUserShowCommandPalette,
        BackupRetentionDays = e.BackupRetentionDays,
        UpdatedAt = e.UpdatedAt
    };
}
