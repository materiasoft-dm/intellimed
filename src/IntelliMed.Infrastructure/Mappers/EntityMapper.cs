using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Mappers;

public static class EntityMapper
{
    // Patient mappings
    public static PatientDto ToDto(Patient entity) => new()
    {
        Id = entity.Id,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        DateOfBirth = entity.DateOfBirth,
        Email = entity.Email,
        Phone = entity.Phone,
        Address = entity.Address,
        Suburb = entity.Suburb,
        State = entity.State,
        Postcode = entity.Postcode,
        MedicareNumber = entity.MedicareNumber,
        MedicarePosition = entity.MedicarePosition,
        PensionNumber = entity.PensionNumber,
        DvaNumber = entity.DvaNumber,
        HealthFundName = entity.HealthFundName,
        HealthFundNumber = entity.HealthFundNumber,
        EmergencyContactName = entity.EmergencyContactName,
        EmergencyContactPhone = entity.EmergencyContactPhone,
        Notes = entity.Notes,
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    public static Patient ToEntity(CreatePatientDto dto) => new()
    {
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        DateOfBirth = dto.DateOfBirth,
        Email = dto.Email,
        Phone = dto.Phone,
        Address = dto.Address,
        Suburb = dto.Suburb,
        State = dto.State,
        Postcode = dto.Postcode,
        MedicareNumber = dto.MedicareNumber,
        MedicarePosition = dto.MedicarePosition,
        PensionNumber = dto.PensionNumber,
        DvaNumber = dto.DvaNumber,
        HealthFundName = dto.HealthFundName,
        HealthFundNumber = dto.HealthFundNumber,
        EmergencyContactName = dto.EmergencyContactName,
        EmergencyContactPhone = dto.EmergencyContactPhone,
        Notes = dto.Notes,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    public static void UpdateEntity(Patient entity, UpdatePatientDto dto)
    {
        entity.FirstName = dto.FirstName;
        entity.LastName = dto.LastName;
        entity.DateOfBirth = dto.DateOfBirth;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.Address = dto.Address;
        entity.Suburb = dto.Suburb;
        entity.State = dto.State;
        entity.Postcode = dto.Postcode;
        entity.MedicareNumber = dto.MedicareNumber;
        entity.MedicarePosition = dto.MedicarePosition;
        entity.PensionNumber = dto.PensionNumber;
        entity.DvaNumber = dto.DvaNumber;
        entity.HealthFundName = dto.HealthFundName;
        entity.HealthFundNumber = dto.HealthFundNumber;
        entity.EmergencyContactName = dto.EmergencyContactName;
        entity.EmergencyContactPhone = dto.EmergencyContactPhone;
        entity.Notes = dto.Notes;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    // Practitioner mappings
    public static PractitionerDto ToDto(Practitioner entity) => new()
    {
        Id = entity.Id,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        Title = entity.Title,
        Profession = entity.Profession,
        ProviderNumber = entity.ProviderNumber,
        AhpraNumber = entity.AhpraNumber,
        Email = entity.Email,
        Phone = entity.Phone,
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    public static Practitioner ToEntity(CreatePractitionerDto dto) => new()
    {
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Title = dto.Title,
        Profession = dto.Profession,
        ProviderNumber = dto.ProviderNumber,
        AhpraNumber = dto.AhpraNumber,
        Email = dto.Email,
        Phone = dto.Phone,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    public static void UpdateEntity(Practitioner entity, UpdatePractitionerDto dto)
    {
        entity.FirstName = dto.FirstName;
        entity.LastName = dto.LastName;
        entity.Title = dto.Title;
        entity.Profession = dto.Profession;
        entity.ProviderNumber = dto.ProviderNumber;
        entity.AhpraNumber = dto.AhpraNumber;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    // Appointment mappings
    public static AppointmentDto ToDto(Appointment entity) => new()
    {
        Id = entity.Id,
        PatientId = entity.PatientId,
        PatientName = entity.Patient != null ? $"{entity.Patient.FirstName} {entity.Patient.LastName}" : string.Empty,
        PractitionerId = entity.PractitionerId,
        PractitionerName = entity.Practitioner != null ? $"{entity.Practitioner.Title} {entity.Practitioner.FirstName} {entity.Practitioner.LastName}".Trim() : string.Empty,
        AppointmentDate = entity.AppointmentDate,
        StartTime = entity.StartTime,
        EndTime = entity.EndTime,
        Status = entity.Status,
        Type = entity.Type,
        Notes = entity.Notes,
        IsBulkBill = entity.IsBulkBill,
        Fee = entity.Fee,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    public static Appointment ToEntity(CreateAppointmentDto dto) => new()
    {
        PatientId = dto.PatientId,
        PractitionerId = dto.PractitionerId,
        AppointmentDate = dto.AppointmentDate,
        StartTime = dto.StartTime,
        EndTime = dto.EndTime,
        Type = dto.Type,
        Notes = dto.Notes,
        IsBulkBill = dto.IsBulkBill,
        Fee = dto.Fee,
        Status = AppointmentStatus.Scheduled,
        CreatedAt = DateTime.UtcNow
    };

    public static void UpdateEntity(Appointment entity, UpdateAppointmentDto dto)
    {
        entity.PatientId = dto.PatientId;
        entity.PractitionerId = dto.PractitionerId;
        entity.AppointmentDate = dto.AppointmentDate;
        entity.StartTime = dto.StartTime;
        entity.EndTime = dto.EndTime;
        entity.Status = dto.Status;
        entity.Type = dto.Type;
        entity.Notes = dto.Notes;
        entity.IsBulkBill = dto.IsBulkBill;
        entity.Fee = dto.Fee;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    // Invoice mappings
    public static InvoiceDto ToDto(Invoice entity) => new()
    {
        Id = entity.Id,
        PatientId = entity.PatientId,
        PatientName = entity.Patient != null ? $"{entity.Patient.FirstName} {entity.Patient.LastName}" : string.Empty,
        AppointmentId = entity.AppointmentId,
        InvoiceNumber = entity.InvoiceNumber,
        InvoiceDate = entity.InvoiceDate,
        DueDate = entity.DueDate,
        Status = entity.Status,
        TotalAmount = entity.TotalAmount,
        AmountPaid = entity.AmountPaid,
        Notes = entity.Notes,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        Items = entity.Items.Select(ToDto).ToList(),
        Payments = entity.Payments.Select(ToDto).ToList()
    };

    public static InvoiceItemDto ToDto(InvoiceItem entity) => new()
    {
        Id = entity.Id,
        InvoiceId = entity.InvoiceId,
        Description = entity.Description,
        Quantity = entity.Quantity,
        UnitPrice = entity.UnitPrice
    };

    public static PaymentDto ToDto(Payment entity) => new()
    {
        Id = entity.Id,
        InvoiceId = entity.InvoiceId,
        Amount = entity.Amount,
        Method = entity.Method,
        Reference = entity.Reference,
        PaymentDate = entity.PaymentDate
    };

    public static Invoice ToEntity(CreateInvoiceDto dto, string invoiceNumber) => new()
    {
        PatientId = dto.PatientId,
        AppointmentId = dto.AppointmentId,
        InvoiceNumber = invoiceNumber,
        InvoiceDate = DateTime.UtcNow,
        DueDate = dto.DueDate,
        Notes = dto.Notes,
        Status = InvoiceStatus.Draft,
        TotalAmount = dto.Items.Sum(i => i.Quantity * i.UnitPrice),
        AmountPaid = 0,
        CreatedAt = DateTime.UtcNow,
        Items = dto.Items.Select(i => new InvoiceItem
        {
            Description = i.Description,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList()
    };

    public static Payment ToEntity(CreatePaymentDto dto) => new()
    {
        InvoiceId = dto.InvoiceId,
        Amount = dto.Amount,
        Method = dto.Method,
        Reference = dto.Reference,
        PaymentDate = dto.PaymentDate,
        CreatedAt = DateTime.UtcNow
    };
}