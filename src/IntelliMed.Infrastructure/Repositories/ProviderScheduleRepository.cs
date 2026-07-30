using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ProviderScheduleRepository : IProviderScheduleRepository
{
    private readonly AppDbContext _context;

    public ProviderScheduleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProviderScheduleDayDto>> GetByUserIdAsync(string applicationUserId)
    {
        var days = await _context.ProviderSchedules
            .Where(s => s.ApplicationUserId == applicationUserId)
            .ToDictionaryAsync(s => s.DayOfWeek);

        return Enum.GetValues<DayOfWeek>().Select(day => days.TryGetValue(day, out var s)
            ? new ProviderScheduleDayDto { DayOfWeek = day, IsActive = s.IsActive, StartTime = s.StartTime, EndTime = s.EndTime }
            : new ProviderScheduleDayDto { DayOfWeek = day, IsActive = false, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17) }
        ).ToList();
    }

    public async Task SetScheduleAsync(string applicationUserId, SetProviderScheduleRequest request)
    {
        var existing = await _context.ProviderSchedules
            .Where(s => s.ApplicationUserId == applicationUserId)
            .ToDictionaryAsync(s => s.DayOfWeek);

        foreach (var day in request.Days)
        {
            if (existing.TryGetValue(day.DayOfWeek, out var row))
            {
                row.IsActive = day.IsActive;
                row.StartTime = day.StartTime;
                row.EndTime = day.EndTime;
            }
            else
            {
                _context.ProviderSchedules.Add(new ProviderSchedule
                {
                    ApplicationUserId = applicationUserId,
                    DayOfWeek = day.DayOfWeek,
                    IsActive = day.IsActive,
                    StartTime = day.StartTime,
                    EndTime = day.EndTime
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<ProviderScheduleDayDto?> GetByPractitionerEmailAsync(string practitionerEmail, DayOfWeek dayOfWeek)
    {
        var userId = await _context.Users
            .Where(u => u.Email == practitionerEmail)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userId == null) return null;

        var schedule = await _context.ProviderSchedules
            .FirstOrDefaultAsync(s => s.ApplicationUserId == userId && s.DayOfWeek == dayOfWeek);

        return schedule == null ? null : new ProviderScheduleDayDto
        {
            DayOfWeek = schedule.DayOfWeek,
            IsActive = schedule.IsActive,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime
        };
    }
}
