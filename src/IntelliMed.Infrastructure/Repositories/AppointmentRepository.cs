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
            .Include(a => a.Patient)
            .Include(a => a.Practitioner)
            .FirstOrDefaultAsync(a => a.Id == id);
        return appointment == null ? null : EntityMapper.ToDto(appointment);
    }

    public async Task<IEnumerable<AppointmentDto>> SearchAsync(AppointmentSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var appointments = await query
            .Include(a => a.Patient)
            .Include(a => a.Practitioner)
            .ToListAsync();
        return appointments.Select(EntityMapper.ToDto);
    }

    public async Task<(IEnumerable<AppointmentDto> Items, int TotalCount)> GetPagedAsync(AppointmentSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var totalCount = await query.CountAsync();

        var appointments = await query
            .Include(a => a.Patient)
            .Include(a => a.Practitioner)
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return (appointments.Select(EntityMapper.ToDto), totalCount);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByDateAsync(DateTime date)
    {
        var appointments = await _dbSet
            .Include(a => a.Patient)
            .Include(a => a.Practitioner)
            .Where(a => a.AppointmentDate.Date == date.Date)
            .OrderBy(a => a.StartTime)
            .ToListAsync();
        return appointments.Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByPatientIdAsync(int patientId)
    {
        var appointments = await _dbSet
            .Include(a => a.Patient)
            .Include(a => a.Practitioner)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();
        return appointments.Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByPractitionerIdAsync(int practitionerId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _dbSet
            .Include(a => a.Patient)
            .Include(a => a.Practitioner)
            .Where(a => a.PractitionerId == practitionerId);

        if (fromDate.HasValue)
            query = query.Where(a => a.AppointmentDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(a => a.AppointmentDate <= toDate.Value.Date);

        var appointments = await query
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .ToListAsync();
        return appointments.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateAppointmentDto dto)
    {
        var appointment = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(appointment);
        await _context.SaveChangesAsync();
        return appointment.Id;
    }

    public async Task UpdateAsync(int id, UpdateAppointmentDto dto)
    {
        var appointment = await _dbSet.FindAsync(id);
        if (appointment == null)
            throw new InvalidOperationException($"Appointment with ID {id} not found");

        EntityMapper.UpdateEntity(appointment, dto);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsTimeSlotAvailableAsync(int practitionerId, DateTime date, TimeSpan startTime, TimeSpan endTime, int? excludeAppointmentId = null)
    {
        var query = _dbSet.Where(a =>
            a.PractitionerId == practitionerId &&
            a.AppointmentDate.Date == date.Date &&
            a.StartTime < endTime &&
            a.EndTime > startTime);

        if (excludeAppointmentId.HasValue)
        {
            query = query.Where(a => a.Id != excludeAppointmentId.Value);
        }

        return !await query.AnyAsync();
    }

    private IQueryable<Appointment> BuildSearchQuery(AppointmentSearchDto search)
    {
        var query = _dbSet.AsQueryable();

        if (search.PatientId.HasValue)
            query = query.Where(a => a.PatientId == search.PatientId.Value);

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