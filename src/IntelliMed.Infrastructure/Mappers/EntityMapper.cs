using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Mappers;

public static class EntityMapper
{
    // Patient mappings
    public static PatientDto ToDto(Patient entity) => new()
    {
        Id = entity.Id,
        Type = entity.Type,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        MiddleName = entity.MiddleName,
        PreferredName = entity.PreferredName,
        MaidenName = entity.MaidenName,
        Title = entity.Title,
        Gender = entity.Gender,
        DateOfBirth = entity.DateOfBirth,
        DobAccuracy = entity.DobAccuracy,
        PlaceOfBirth = entity.PlaceOfBirth,
        InterpreterRequired = entity.InterpreterRequired,
        InterpreterLanguage = entity.InterpreterLanguage,
        MaritalStatus = entity.MaritalStatus,
        Ethnicity = entity.Ethnicity,
        MedicareNumber = entity.MedicareNumber,
        MedicarePosition = entity.MedicarePosition,
        MedicareExpiryDate = entity.MedicareExpiryDate,
        DvaNumber = entity.DvaNumber,
        DvaExpiryDate = entity.DvaExpiryDate,
        PensionNumber = entity.PensionNumber,
        PensionExpiryDate = entity.PensionExpiryDate,
        EntitlementStatus = entity.EntitlementStatus,
        SafetyNetNumber = entity.SafetyNetNumber,
        AtsiStatus = entity.AtsiStatus,
        MedicareIncentiveEligible = entity.MedicareIncentiveEligible,
        CtgCoPaymentRelief = entity.CtgCoPaymentRelief,
        Address = entity.Address,
        Suburb = entity.Suburb,
        State = entity.State,
        Postcode = entity.Postcode,
        Email = entity.Email,
        Phone = entity.Phone,
        BusinessHoursPhone = entity.BusinessHoursPhone,
        MobilePhone = entity.MobilePhone,
        FaxNumber = entity.FaxNumber,
        AcceptSms = entity.AcceptSms,
        AcceptEmail = entity.AcceptEmail,
        AcceptOnlineAppointments = entity.AcceptOnlineAppointments,
        AcceptSmsMarketing = entity.AcceptSmsMarketing,
        Notes = entity.Notes,
        Warnings = entity.Warnings,
        NextOfKinPatientId = entity.NextOfKinPatientId,
        NextOfKinName = entity.NextOfKinName,
        NextOfKinPhone = entity.NextOfKinPhone,
        EmergencyContactPatientId = entity.EmergencyContactPatientId,
        EmergencyContactName = entity.EmergencyContactName,
        EmergencyContactPhone = entity.EmergencyContactPhone,
        SameAsNextOfKin = entity.SameAsNextOfKin,
        HealthFundId = entity.HealthFundId,
        HealthFundCode = entity.HealthFund?.Code,
        HealthFundName = entity.HealthFund?.Name,
        HealthFundNumber = entity.HealthFundNumber,
        HealthFundRef = entity.HealthFundRef,
        HealthFundAliasFamily = entity.HealthFundAliasFamily,
        HealthFundAliasFirst = entity.HealthFundAliasFirst,
        HealthFundJoinDate = entity.HealthFundJoinDate,
        AccountType = entity.AccountType,
        FeeRateCode = entity.FeeRateCode,
        PayerPatientId = entity.PayerPatientId,
        PayerName = entity.PayerName,
        AccountName = entity.AccountName,
        AccountBsb = entity.AccountBsb,
        AccountNumber = entity.AccountNumber,
        UseMedicareRegisteredBankAccount = entity.UseMedicareRegisteredBankAccount,
        FileNumber = entity.FileNumber,
        UrNumber = entity.UrNumber,
        Deceased = entity.Deceased,
        ProviderId = entity.ProviderId,
        LastSeenDate = entity.LastSeenDate,
        IhiNumber = entity.IhiNumber,
        IhiRecordStatus = entity.IhiRecordStatus,
        IhiAssignedDate = entity.IhiAssignedDate,
        IhiNumberStatus = entity.IhiNumberStatus,
        IhiUnresolvedDate = entity.IhiUnresolvedDate,
        LifeCardNum = entity.LifeCardNum,
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    public static Patient ToEntity(CreatePatientDto dto) => new()
    {
        Type = dto.Type,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        MiddleName = dto.MiddleName,
        PreferredName = dto.PreferredName,
        MaidenName = dto.MaidenName,
        Title = dto.Title,
        Gender = dto.Gender,
        DateOfBirth = dto.DateOfBirth,
        DobAccuracy = dto.DobAccuracy,
        PlaceOfBirth = dto.PlaceOfBirth,
        InterpreterRequired = dto.InterpreterRequired,
        InterpreterLanguage = dto.InterpreterLanguage,
        MaritalStatus = dto.MaritalStatus,
        Ethnicity = dto.Ethnicity,
        MedicareNumber = dto.MedicareNumber,
        MedicarePosition = dto.MedicarePosition,
        MedicareExpiryDate = dto.MedicareExpiryDate,
        DvaNumber = dto.DvaNumber,
        DvaExpiryDate = dto.DvaExpiryDate,
        PensionNumber = dto.PensionNumber,
        PensionExpiryDate = dto.PensionExpiryDate,
        EntitlementStatus = dto.EntitlementStatus,
        SafetyNetNumber = dto.SafetyNetNumber,
        AtsiStatus = dto.AtsiStatus,
        MedicareIncentiveEligible = dto.MedicareIncentiveEligible,
        CtgCoPaymentRelief = dto.CtgCoPaymentRelief,
        Address = dto.Address,
        Suburb = dto.Suburb,
        State = dto.State,
        Postcode = dto.Postcode,
        Email = dto.Email,
        Phone = dto.Phone,
        BusinessHoursPhone = dto.BusinessHoursPhone,
        MobilePhone = dto.MobilePhone,
        FaxNumber = dto.FaxNumber,
        AcceptSms = dto.AcceptSms,
        AcceptEmail = dto.AcceptEmail,
        AcceptOnlineAppointments = dto.AcceptOnlineAppointments,
        AcceptSmsMarketing = dto.AcceptSmsMarketing,
        Notes = dto.Notes,
        Warnings = dto.Warnings,
        NextOfKinPatientId = dto.NextOfKinPatientId,
        NextOfKinName = dto.NextOfKinName,
        NextOfKinPhone = dto.NextOfKinPhone,
        EmergencyContactPatientId = dto.EmergencyContactPatientId,
        EmergencyContactName = dto.EmergencyContactName,
        EmergencyContactPhone = dto.EmergencyContactPhone,
        SameAsNextOfKin = dto.SameAsNextOfKin,
        HealthFundId = dto.HealthFundId,
        HealthFundNumber = dto.HealthFundNumber,
        HealthFundRef = dto.HealthFundRef,
        HealthFundAliasFamily = dto.HealthFundAliasFamily,
        HealthFundAliasFirst = dto.HealthFundAliasFirst,
        HealthFundJoinDate = dto.HealthFundJoinDate,
        AccountType = dto.AccountType,
        FeeRateCode = dto.FeeRateCode,
        PayerPatientId = dto.PayerPatientId,
        PayerName = dto.PayerName,
        AccountName = dto.AccountName,
        AccountBsb = dto.AccountBsb,
        AccountNumber = dto.AccountNumber,
        UseMedicareRegisteredBankAccount = dto.UseMedicareRegisteredBankAccount,
        FileNumber = dto.FileNumber,
        UrNumber = dto.UrNumber,
        Deceased = dto.Deceased,
        ProviderId = dto.ProviderId,
        LastSeenDate = dto.LastSeenDate,
        IhiNumber = dto.IhiNumber,
        IhiRecordStatus = dto.IhiRecordStatus,
        IhiAssignedDate = dto.IhiAssignedDate,
        IhiNumberStatus = dto.IhiNumberStatus,
        IhiUnresolvedDate = dto.IhiUnresolvedDate,
        LifeCardNum = dto.LifeCardNum,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    public static void UpdateEntity(Patient entity, UpdatePatientDto dto)
    {
        entity.Type = dto.Type;
        entity.FirstName = dto.FirstName;
        entity.LastName = dto.LastName;
        entity.MiddleName = dto.MiddleName;
        entity.PreferredName = dto.PreferredName;
        entity.MaidenName = dto.MaidenName;
        entity.Title = dto.Title;
        entity.Gender = dto.Gender;
        entity.DateOfBirth = dto.DateOfBirth;
        entity.DobAccuracy = dto.DobAccuracy;
        entity.PlaceOfBirth = dto.PlaceOfBirth;
        entity.InterpreterRequired = dto.InterpreterRequired;
        entity.InterpreterLanguage = dto.InterpreterLanguage;
        entity.MaritalStatus = dto.MaritalStatus;
        entity.Ethnicity = dto.Ethnicity;
        entity.MedicareNumber = dto.MedicareNumber;
        entity.MedicarePosition = dto.MedicarePosition;
        entity.MedicareExpiryDate = dto.MedicareExpiryDate;
        entity.DvaNumber = dto.DvaNumber;
        entity.DvaExpiryDate = dto.DvaExpiryDate;
        entity.PensionNumber = dto.PensionNumber;
        entity.PensionExpiryDate = dto.PensionExpiryDate;
        entity.EntitlementStatus = dto.EntitlementStatus;
        entity.SafetyNetNumber = dto.SafetyNetNumber;
        entity.AtsiStatus = dto.AtsiStatus;
        entity.MedicareIncentiveEligible = dto.MedicareIncentiveEligible;
        entity.CtgCoPaymentRelief = dto.CtgCoPaymentRelief;
        entity.Address = dto.Address;
        entity.Suburb = dto.Suburb;
        entity.State = dto.State;
        entity.Postcode = dto.Postcode;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.BusinessHoursPhone = dto.BusinessHoursPhone;
        entity.MobilePhone = dto.MobilePhone;
        entity.FaxNumber = dto.FaxNumber;
        entity.AcceptSms = dto.AcceptSms;
        entity.AcceptEmail = dto.AcceptEmail;
        entity.AcceptOnlineAppointments = dto.AcceptOnlineAppointments;
        entity.AcceptSmsMarketing = dto.AcceptSmsMarketing;
        entity.Notes = dto.Notes;
        entity.Warnings = dto.Warnings;
        entity.NextOfKinPatientId = dto.NextOfKinPatientId;
        entity.NextOfKinName = dto.NextOfKinName;
        entity.NextOfKinPhone = dto.NextOfKinPhone;
        entity.EmergencyContactPatientId = dto.EmergencyContactPatientId;
        entity.EmergencyContactName = dto.EmergencyContactName;
        entity.EmergencyContactPhone = dto.EmergencyContactPhone;
        entity.SameAsNextOfKin = dto.SameAsNextOfKin;
        entity.HealthFundId = dto.HealthFundId;
        entity.HealthFundNumber = dto.HealthFundNumber;
        entity.HealthFundRef = dto.HealthFundRef;
        entity.HealthFundAliasFamily = dto.HealthFundAliasFamily;
        entity.HealthFundAliasFirst = dto.HealthFundAliasFirst;
        entity.HealthFundJoinDate = dto.HealthFundJoinDate;
        entity.AccountType = dto.AccountType;
        entity.FeeRateCode = dto.FeeRateCode;
        entity.PayerPatientId = dto.PayerPatientId;
        entity.PayerName = dto.PayerName;
        entity.AccountName = dto.AccountName;
        entity.AccountBsb = dto.AccountBsb;
        entity.AccountNumber = dto.AccountNumber;
        entity.UseMedicareRegisteredBankAccount = dto.UseMedicareRegisteredBankAccount;
        entity.FileNumber = dto.FileNumber;
        entity.UrNumber = dto.UrNumber;
        entity.Deceased = dto.Deceased;
        entity.ProviderId = dto.ProviderId;
        entity.LastSeenDate = dto.LastSeenDate;
        entity.IhiNumber = dto.IhiNumber;
        entity.IhiRecordStatus = dto.IhiRecordStatus;
        entity.IhiAssignedDate = dto.IhiAssignedDate;
        entity.IhiNumberStatus = dto.IhiNumberStatus;
        entity.IhiUnresolvedDate = dto.IhiUnresolvedDate;
        entity.LifeCardNum = dto.LifeCardNum;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    // PatientReferral mappings
    public static PatientReferralDto ToDto(PatientReferral entity) => new()
    {
        Id = entity.Id,
        PatientId = entity.PatientId,
        ReferralDate = entity.ReferralDate,
        ReferralPeriod = entity.ReferralPeriod,
        IsDefault = entity.IsDefault,
        IsGP = entity.IsGP,
        ReferringProviderName = entity.ReferringProviderName,
        ReferringProviderNumber = entity.ReferringProviderNumber,
        RequestTypeCde = entity.RequestTypeCde,
        FirstVisitDate = entity.FirstVisitDate,
        Note = entity.Note,
        IsArchived = entity.IsArchived,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    public static PatientReferral ToEntity(CreatePatientReferralDto dto) => new()
    {
        PatientId = dto.PatientId,
        ReferralDate = dto.ReferralDate,
        ReferralPeriod = dto.ReferralPeriod,
        IsDefault = dto.IsDefault,
        IsGP = dto.IsGP,
        ReferringProviderName = dto.ReferringProviderName,
        ReferringProviderNumber = dto.ReferringProviderNumber,
        RequestTypeCde = dto.RequestTypeCde,
        FirstVisitDate = dto.FirstVisitDate,
        Note = dto.Note,
        CreatedAt = DateTime.UtcNow
    };

    public static void UpdateEntity(PatientReferral entity, UpdatePatientReferralDto dto)
    {
        entity.ReferralDate = dto.ReferralDate;
        entity.ReferralPeriod = dto.ReferralPeriod;
        entity.IsDefault = dto.IsDefault;
        entity.IsGP = dto.IsGP;
        entity.ReferringProviderName = dto.ReferringProviderName;
        entity.ReferringProviderNumber = dto.ReferringProviderNumber;
        entity.RequestTypeCde = dto.RequestTypeCde;
        entity.FirstVisitDate = dto.FirstVisitDate;
        entity.Note = dto.Note;
        entity.IsArchived = dto.IsArchived;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    // PatientAddress mappings
    public static PatientAddressDto ToDto(PatientAddress entity) => new()
    {
        Id = entity.Id,
        PatientId = entity.PatientId,
        AddressType = entity.AddressType,
        AddressLine1 = entity.AddressLine1,
        AddressLine2 = entity.AddressLine2,
        Suburb = entity.Suburb,
        Postcode = entity.Postcode,
        State = entity.State,
        AddressSubType = entity.AddressSubType,
        Community = entity.Community,
        SendToMedicare = entity.SendToMedicare
    };

    public static PatientAddress ToEntity(CreatePatientAddressDto dto) => new()
    {
        PatientId = dto.PatientId,
        AddressType = dto.AddressType,
        AddressLine1 = dto.AddressLine1,
        AddressLine2 = dto.AddressLine2,
        Suburb = dto.Suburb,
        Postcode = dto.Postcode,
        State = dto.State,
        AddressSubType = dto.AddressSubType,
        Community = dto.Community,
        SendToMedicare = dto.SendToMedicare
    };

    public static void UpdateEntity(PatientAddress entity, UpdatePatientAddressDto dto)
    {
        entity.AddressType = dto.AddressType;
        entity.AddressLine1 = dto.AddressLine1;
        entity.AddressLine2 = dto.AddressLine2;
        entity.Suburb = dto.Suburb;
        entity.Postcode = dto.Postcode;
        entity.State = dto.State;
        entity.AddressSubType = dto.AddressSubType;
        entity.Community = dto.Community;
        entity.SendToMedicare = dto.SendToMedicare;
    }

    // PatientCompensationClaim mappings
    public static PatientCompensationClaimDto ToDto(PatientCompensationClaim entity) => new()
    {
        Id = entity.Id,
        PatientId = entity.PatientId,
        ClaimNum = entity.ClaimNum,
        DateOfInjury = entity.DateOfInjury,
        EmployerName = entity.EmployerName,
        CaseManagerName = entity.CaseManagerName,
        PayerName = entity.PayerName,
        IsDefault = entity.IsDefault,
        PublicNote = entity.PublicNote,
        PrivateNote = entity.PrivateNote,
        IsArchived = entity.IsArchived,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    public static PatientCompensationClaim ToEntity(CreatePatientCompensationClaimDto dto) => new()
    {
        PatientId = dto.PatientId,
        ClaimNum = dto.ClaimNum,
        DateOfInjury = dto.DateOfInjury,
        EmployerName = dto.EmployerName,
        CaseManagerName = dto.CaseManagerName,
        PayerName = dto.PayerName,
        IsDefault = dto.IsDefault,
        PublicNote = dto.PublicNote,
        PrivateNote = dto.PrivateNote,
        CreatedAt = DateTime.UtcNow
    };

    public static void UpdateEntity(PatientCompensationClaim entity, UpdatePatientCompensationClaimDto dto)
    {
        entity.ClaimNum = dto.ClaimNum;
        entity.DateOfInjury = dto.DateOfInjury;
        entity.EmployerName = dto.EmployerName;
        entity.CaseManagerName = dto.CaseManagerName;
        entity.PayerName = dto.PayerName;
        entity.IsDefault = dto.IsDefault;
        entity.PublicNote = dto.PublicNote;
        entity.PrivateNote = dto.PrivateNote;
        entity.IsArchived = dto.IsArchived;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    // PatientOccupation mappings
    public static PatientOccupationDto ToDto(PatientOccupation entity) => new()
    {
        Id = entity.Id,
        PatientId = entity.PatientId,
        Occupation = entity.Occupation,
        Employer = entity.Employer,
        StartedYear = entity.StartedYear,
        StoppedYear = entity.StoppedYear,
        HasAsbestos = entity.HasAsbestos,
        HasDust = entity.HasDust,
        HasRadiation = entity.HasRadiation,
        HasAnimals = entity.HasAnimals,
        Comment = entity.Comment,
        IsArchived = entity.IsArchived,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    public static PatientOccupation ToEntity(CreatePatientOccupationDto dto) => new()
    {
        PatientId = dto.PatientId,
        Occupation = dto.Occupation,
        Employer = dto.Employer,
        StartedYear = dto.StartedYear,
        StoppedYear = dto.StoppedYear,
        HasAsbestos = dto.HasAsbestos,
        HasDust = dto.HasDust,
        HasRadiation = dto.HasRadiation,
        HasAnimals = dto.HasAnimals,
        Comment = dto.Comment,
        CreatedAt = DateTime.UtcNow
    };

    public static void UpdateEntity(PatientOccupation entity, UpdatePatientOccupationDto dto)
    {
        entity.Occupation = dto.Occupation;
        entity.Employer = dto.Employer;
        entity.StartedYear = dto.StartedYear;
        entity.StoppedYear = dto.StoppedYear;
        entity.HasAsbestos = dto.HasAsbestos;
        entity.HasDust = dto.HasDust;
        entity.HasRadiation = dto.HasRadiation;
        entity.HasAnimals = dto.HasAnimals;
        entity.Comment = dto.Comment;
        entity.IsArchived = dto.IsArchived;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    // UserDefinedFieldType mappings
    public static UserDefinedFieldTypeDto ToDto(UserDefinedFieldType entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        FieldType = entity.FieldType,
        DefaultValue = entity.DefaultValue,
        DisplayOrder = entity.DisplayOrder,
        IsActive = entity.IsActive
    };

    public static UserDefinedFieldType ToEntity(CreateUserDefinedFieldTypeDto dto) => new()
    {
        Name = dto.Name,
        FieldType = dto.FieldType,
        DefaultValue = dto.DefaultValue,
        DisplayOrder = dto.DisplayOrder,
        IsActive = true
    };

    public static void UpdateEntity(UserDefinedFieldType entity, UpdateUserDefinedFieldTypeDto dto)
    {
        entity.Name = dto.Name;
        entity.FieldType = dto.FieldType;
        entity.DefaultValue = dto.DefaultValue;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.IsActive = dto.IsActive;
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