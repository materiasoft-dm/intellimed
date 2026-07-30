using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
{
    public AppointmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AppointmentDto?> GetByIdAsync(int id)
    {
        var appointment = await _dbSet
            .Include(a => a.Client)
            .Include(a => a.Practitioner)
            .Include(a => a.AppointmentTypeSetting)
            .FirstOrDefaultAsync(a => a.Id == id);
        return appointment == null ? null : EntityMapper.ToDto(appointment);
    }

    public async Task<IEnumerable<AppointmentDto>> SearchAsync(AppointmentSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var appointments = await query
            .Include(a => a.Client)
            .Include(a => a.Practitioner)
            .Include(a => a.AppointmentTypeSetting)
            .ToListAsync();
        return appointments.Select(EntityMapper.ToDto);
    }

    public async Task<(IEnumerable<AppointmentDto> Items, int TotalCount)> GetPagedAsync(AppointmentSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var totalCount = await query.CountAsync();

        // SQLite can't ORDER BY a TimeSpan column, so the StartTime tie-break has to happen
        // in-memory — safe here since it only reorders within a page already sliced by AppointmentDate.
        var appointments = await query
            .Include(a => a.Client)
            .Include(a => a.Practitioner)
            .Include(a => a.AppointmentTypeSetting)
            .OrderBy(a => a.AppointmentDate)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return (appointments.OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).Select(EntityMapper.ToDto), totalCount);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByDateAsync(DateTime date)
    {
        // SQLite can't ORDER BY a TimeSpan column — sort by StartTime in-memory after fetching.
        var appointments = await _dbSet
            .Include(a => a.Client)
            .Include(a => a.Practitioner)
            .Where(a => a.AppointmentDate.Date == date.Date)
            .ToListAsync();
        return appointments.OrderBy(a => a.StartTime).Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByClientIdAsync(int clientId)
    {
        var appointments = await _dbSet
            .Include(a => a.Client)
            .Include(a => a.Practitioner)
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();
        return appointments.Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByPractitionerIdAsync(int practitionerId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _dbSet
            .Include(a => a.Client)
            .Include(a => a.Practitioner)
            .Where(a => a.PractitionerId == practitionerId);

        if (fromDate.HasValue)
            query = query.Where(a => a.AppointmentDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(a => a.AppointmentDate <= toDate.Value.Date);

        // SQLite can't ORDER BY a TimeSpan column, so the StartTime tie-break happens in-memory.
        var appointments = await query
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();
        return appointments.OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateAppointmentDto dto)
    {
        var appointment = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(appointment);
        await _context.SaveChangesAsync();
        return appointment.Id;
    }

    /// <summary>Materializes one Appointment row per occurrence up front, all sharing a new RecurrenceSeriesId, rather than expanding a recurrence rule at read time.</summary>
    public async Task<IEnumerable<int>> CreateSeriesAsync(CreateAppointmentSeriesDto dto)
    {
        const int maxOccurrences = 104; // safety cap (~2 years weekly)
        var seriesId = Guid.NewGuid();
        var dates = new List<DateTime>();
        var current = dto.AppointmentDate.Date;

        while (dates.Count < maxOccurrences)
        {
            dates.Add(current);

            if (dto.Occurrences.HasValue && dates.Count >= dto.Occurrences.Value)
                break;

            current = dto.Frequency switch
            {
                RecurrenceFrequency.Weekly => current.AddDays(7),
                RecurrenceFrequency.Fortnightly => current.AddDays(14),
                RecurrenceFrequency.Monthly => current.AddMonths(1),
                _ => current.AddDays(7)
            };

            if (!dto.Occurrences.HasValue && dto.UntilDate.HasValue && current.Date > dto.UntilDate.Value.Date)
                break;
        }

        var appointments = dates.Select(date => new Appointment
        {
            ClinicId = dto.ClinicId,
            ClientId = dto.ClientId,
            PractitionerId = dto.PractitionerId,
            AppointmentDate = date,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Type = dto.Type,
            AppointmentTypeSettingId = dto.AppointmentTypeSettingId,
            Notes = dto.Notes,
            IsBulkBill = dto.IsBulkBill,
            Fee = dto.Fee,
            Status = AppointmentStatus.Scheduled,
            RecurrenceSeriesId = seriesId,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _dbSet.AddRangeAsync(appointments);
        await _context.SaveChangesAsync();
        return appointments.Select(a => a.Id);
    }

    public async Task UpdateAsync(int id, UpdateAppointmentDto dto)
    {
        var appointment = await _dbSet.FindAsync(id);
        if (appointment == null)
            throw new InvalidOperationException($"Appointment with ID {id} not found");

        EntityMapper.UpdateEntity(appointment, dto);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, AppointmentStatus status)
    {
        var appointment = await _dbSet.FindAsync(id);
        if (appointment == null)
            throw new InvalidOperationException($"Appointment with ID {id} not found");

        appointment.Status = status;
        var now = DateTime.UtcNow;
        switch (status)
        {
            case AppointmentStatus.Arrived:
                appointment.ArrivedAt ??= now;
                break;
            case AppointmentStatus.InProgress:
                appointment.SeenAt ??= now;
                break;
            case AppointmentStatus.Completed:
                appointment.CompletedAt ??= now;
                break;
        }
        appointment.UpdatedAt = now;
        await _context.SaveChangesAsync();
    }

    public async Task RescheduleAsync(int id, RescheduleAppointmentDto dto)
    {
        var appointment = await _dbSet.FindAsync(id);
        if (appointment == null)
            throw new InvalidOperationException($"Appointment with ID {id} not found");

        var available = await IsTimeSlotAvailableAsync(appointment.PractitionerId, dto.AppointmentDate, dto.StartTime, dto.EndTime, id);
        if (!available)
            throw new InvalidOperationException("The selected time slot overlaps with another appointment for this practitioner.");

        appointment.AppointmentDate = dto.AppointmentDate;
        appointment.StartTime = dto.StartTime;
        appointment.EndTime = dto.EndTime;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsTimeSlotAvailableAsync(int practitionerId, DateTime date, TimeSpan startTime, TimeSpan endTime, int? excludeAppointmentId = null)
    {
        // SQLite can't translate comparisons on a TimeSpan column, so the overlap check itself
        // runs in-memory — the SQL-side filter (practitioner/date/status/id) keeps that set small.
        var query = _dbSet.Where(a =>
            a.PractitionerId == practitionerId &&
            a.AppointmentDate.Date == date.Date &&
            a.Status != AppointmentStatus.Cancelled);

        if (excludeAppointmentId.HasValue)
        {
            query = query.Where(a => a.Id != excludeAppointmentId.Value);
        }

        var candidates = await query.ToListAsync();
        return !candidates.Any(a => a.StartTime < endTime && a.EndTime > startTime);
    }

    public async Task<IEnumerable<AppointmentDto>> GetWaitingRoomAsync(int? clinicId, DateTime date)
    {
        var query = _dbSet
            .Include(a => a.Client)
            .Include(a => a.Practitioner)
            .Where(a => a.AppointmentDate.Date == date.Date &&
                        (a.Status == AppointmentStatus.Arrived || a.Status == AppointmentStatus.InProgress));

        if (clinicId.HasValue)
            query = query.Where(a => a.ClinicId == clinicId.Value);

        var appointments = await query.ToListAsync();
        return appointments
            .OrderBy(a => a.ArrivedAt ?? a.AppointmentDate.Add(a.StartTime))
            .Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<AppointmentDto>> GetBySeriesIdAsync(Guid seriesId)
    {
        var appointments = await _dbSet
            .Include(a => a.Client)
            .Include(a => a.Practitioner)
            .Where(a => a.RecurrenceSeriesId == seriesId)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();
        return appointments.Select(EntityMapper.ToDto);
    }

    public async Task<int> DeleteSeriesAsync(Guid seriesId, DateTime? fromDate = null)
    {
        var query = _dbSet.Where(a => a.RecurrenceSeriesId == seriesId);
        if (fromDate.HasValue)
            query = query.Where(a => a.AppointmentDate.Date >= fromDate.Value.Date);

        var appointments = await query.ToListAsync();
        _dbSet.RemoveRange(appointments);
        await _context.SaveChangesAsync();
        return appointments.Count;
    }

    private IQueryable<Appointment> BuildSearchQuery(AppointmentSearchDto search)
    {
        var query = _dbSet.AsQueryable();

        if (search.ClinicId.HasValue)
            query = query.Where(a => a.ClinicId == search.ClinicId.Value);

        if (search.ClientId.HasValue)
            query = query.Where(a => a.ClientId == search.ClientId.Value);

        if (search.PractitionerId.HasValue)
            query = query.Where(a => a.PractitionerId == search.PractitionerId.Value);

        if (search.FromDate.HasValue)
            query = query.Where(a => a.AppointmentDate >= search.FromDate.Value.Date);

        if (search.ToDate.HasValue)
            query = query.Where(a => a.AppointmentDate <= search.ToDate.Value.Date);

        if (search.Status.HasValue)
            query = query.Where(a => a.Status == search.Status.Value);

        return query;
    }
}
