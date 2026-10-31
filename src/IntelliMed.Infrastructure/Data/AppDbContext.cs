using IntelliMed.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Data;

/// <summary>
/// Application database context extending IdentityDbContext for ASP.NET Identity support.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptPayment> ReceiptPayments => Set<ReceiptPayment>();
    public DbSet<ReceiptAllocation> ReceiptAllocations => Set<ReceiptAllocation>();
    public DbSet<InvoiceWriteOff> InvoiceWriteOffs => Set<InvoiceWriteOff>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ClientAddress> ClientAddresses => Set<ClientAddress>();
    public DbSet<ClientReferral> ClientReferrals => Set<ClientReferral>();
    public DbSet<AddressBookContact> AddressBookContacts => Set<AddressBookContact>();
    public DbSet<ResultReport> ResultReports => Set<ResultReport>();
    public DbSet<ResultItem> ResultItems => Set<ResultItem>();
    public DbSet<ResultAttachment> ResultAttachments => Set<ResultAttachment>();
    public DbSet<ClientLabIdentifier> ClientLabIdentifiers => Set<ClientLabIdentifier>();
    public DbSet<ResultIngestKey> ResultIngestKeys => Set<ResultIngestKey>();
    public DbSet<ClientCompensationClaim> ClientCompensationClaims => Set<ClientCompensationClaim>();
    public DbSet<ClientOccupation> ClientOccupations => Set<ClientOccupation>();
    public DbSet<ClientFamilyRelationship> ClientFamilyRelationships => Set<ClientFamilyRelationship>();
    public DbSet<UserDefinedFieldType> UserDefinedFieldTypes => Set<UserDefinedFieldType>();
    public DbSet<ClientUserDefinedFieldValue> ClientUserDefinedFieldValues => Set<ClientUserDefinedFieldValue>();
    public DbSet<ClientProviderAccess> ClientProviderAccesses => Set<ClientProviderAccess>();
    public DbSet<HealthFund> HealthFunds => Set<HealthFund>();
    public DbSet<ProviderGroup> ProviderGroups => Set<ProviderGroup>();
    public DbSet<ClinicSettings> ClinicSettings => Set<ClinicSettings>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<UserClinic> UserClinics => Set<UserClinic>();
    public DbSet<BillingItem> BillingItems => Set<BillingItem>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<ActiveIngredient> ActiveIngredients => Set<ActiveIngredient>();
    public DbSet<MedicineIngredient> MedicineIngredients => Set<MedicineIngredient>();
    public DbSet<ClientAllergy> ClientAllergies => Set<ClientAllergy>();
    public DbSet<ClientHealthIssue> ClientHealthIssues => Set<ClientHealthIssue>();
    public DbSet<DrugInteraction> DrugInteractions => Set<DrugInteraction>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionIssue> PrescriptionIssues => Set<PrescriptionIssue>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<ProgressNote> ProgressNotes => Set<ProgressNote>();
    public DbSet<ProgressNoteAddendum> ProgressNoteAddenda => Set<ProgressNoteAddendum>();
    public DbSet<FeeSchedule> FeeSchedules => Set<FeeSchedule>();
    public DbSet<FeeScheduleItem> FeeScheduleItems => Set<FeeScheduleItem>();
    public DbSet<FeeScheduleItemPriceHistory> FeeScheduleItemPriceHistories => Set<FeeScheduleItemPriceHistory>();
    public DbSet<AccountTypeFeeScheduleMapping> AccountTypeFeeScheduleMappings => Set<AccountTypeFeeScheduleMapping>();
    public DbSet<DerivedItemConfig> DerivedItemConfigs => Set<DerivedItemConfig>();
    public DbSet<DerivedItemRateCalculated> DerivedItemRateCalculateds => Set<DerivedItemRateCalculated>();
    public DbSet<AppointmentTypeSetting> AppointmentTypeSettings => Set<AppointmentTypeSetting>();
    public DbSet<ProviderSchedule> ProviderSchedules => Set<ProviderSchedule>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<SearchAction> SearchActions => Set<SearchAction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DatabaseBackupSettings> DatabaseBackupSettings => Set<DatabaseBackupSettings>();
    public DbSet<DbAdminAuditLog> DbAdminAuditLogs => Set<DbAdminAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ApplicationUser configuration
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(20);
            entity.Property(e => e.MiddleName).HasMaxLength(100);
            entity.Property(e => e.MobilePhone).HasMaxLength(20);
            entity.Property(e => e.BusinessHoursPhone).HasMaxLength(20);
            entity.Property(e => e.Fax).HasMaxLength(20);
            entity.Property(e => e.Qualifications).HasMaxLength(200);
            entity.Property(e => e.Specialty).HasMaxLength(150);
            entity.Property(e => e.ProviderNumber).HasMaxLength(20);
            entity.Property(e => e.AhpraNumber).HasMaxLength(20);
            entity.Property(e => e.HpiiNumber).HasMaxLength(20);
            entity.Property(e => e.Note).HasMaxLength(4000);
            entity.Property(e => e.ResidentialAddress).HasMaxLength(255);
            entity.Property(e => e.ResidentialSuburb).HasMaxLength(100);
            entity.Property(e => e.ResidentialPostcode).HasMaxLength(10);
            entity.Property(e => e.ResidentialState).HasMaxLength(10);
            entity.Property(e => e.PostalAddress).HasMaxLength(255);
            entity.Property(e => e.PostalSuburb).HasMaxLength(100);
            entity.Property(e => e.PostalPostcode).HasMaxLength(10);
            entity.Property(e => e.PostalState).HasMaxLength(10);
            entity.HasIndex(e => e.Email).IsUnique();

            entity.HasOne(e => e.Group)
                .WithMany()
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Client configuration
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.MedicareNumber).HasMaxLength(20);
            entity.Property(e => e.MiddleName).HasMaxLength(100);
            entity.Property(e => e.PreferredName).HasMaxLength(100);
            entity.Property(e => e.MaidenName).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(20);
            entity.Property(e => e.PlaceOfBirth).HasMaxLength(255);
            entity.Property(e => e.InterpreterLanguage).HasMaxLength(100);
            entity.Property(e => e.Ethnicity).HasMaxLength(100);
            entity.Property(e => e.EntitlementStatus).HasMaxLength(50);
            entity.Property(e => e.SafetyNetNumber).HasMaxLength(50);
            entity.Property(e => e.HealthFundRef).HasMaxLength(50);
            entity.Property(e => e.HealthFundAliasFamily).HasMaxLength(100);
            entity.Property(e => e.HealthFundAliasFirst).HasMaxLength(100);
            entity.Property(e => e.FeeRateCode).HasMaxLength(20);
            entity.Property(e => e.PayerName).HasMaxLength(200);
            entity.Property(e => e.AccountName).HasMaxLength(200);
            entity.Property(e => e.AccountBsb).HasMaxLength(10);
            entity.Property(e => e.AccountNumber).HasMaxLength(30);
            entity.Property(e => e.FileNumber).HasMaxLength(50);
            entity.Property(e => e.UrNumber).HasMaxLength(50);
            entity.Property(e => e.BusinessHoursPhone).HasMaxLength(20);
            entity.Property(e => e.MobilePhone).HasMaxLength(20);
            entity.Property(e => e.FaxNumber).HasMaxLength(20);
            entity.Property(e => e.NextOfKinName).HasMaxLength(200);
            entity.Property(e => e.NextOfKinPhone).HasMaxLength(20);
            entity.Property(e => e.EmergencyContactName).HasMaxLength(200);
            entity.Property(e => e.EmergencyContactPhone).HasMaxLength(20);
            entity.Property(e => e.IhiNumber).HasMaxLength(16);
            entity.Property(e => e.IhiRecordStatus).HasMaxLength(50);
            entity.Property(e => e.IhiNumberStatus).HasMaxLength(50);
            entity.Property(e => e.LifeCardNum).HasMaxLength(50);
            entity.Property(e => e.DvaCardType).HasMaxLength(20);
            entity.Property(e => e.ConcessionCardType).HasMaxLength(20);
            entity.Property(e => e.SafetyNetCardType).HasMaxLength(20);
            entity.Property(e => e.PbsEntitlementNumber).HasMaxLength(20);
            entity.Property(e => e.PbsEntitlementCardType).HasMaxLength(20);
            entity.Property(e => e.AtsiAccreditation).HasMaxLength(50);
            entity.Property(e => e.HealthFundCoverLevel).HasMaxLength(50);
            entity.Property(e => e.CountryOfBirth).HasMaxLength(100);
            entity.Property(e => e.CulturalBackground).HasMaxLength(100);
            entity.Property(e => e.PreferredLanguage).HasMaxLength(100);
            entity.Property(e => e.Salutation).HasMaxLength(100);
            entity.Property(e => e.NextOfKinRelationship).HasMaxLength(50);
            entity.Property(e => e.EmergencyContactRelationship).HasMaxLength(50);
            entity.Property(e => e.ResidentialAddressType).HasMaxLength(50);
            entity.Property(e => e.HowDidYouHear).HasMaxLength(200);
            entity.Property(e => e.MinorFileNumber).HasMaxLength(20);
            entity.Property(e => e.ExternalId).HasMaxLength(100);
            entity.Property(e => e.PatientPortalId).HasMaxLength(50);
            entity.Property(e => e.PreferredPharmacyCode1).HasMaxLength(20);
            entity.Property(e => e.PreferredPharmacyCode2).HasMaxLength(20);
            entity.HasIndex(e => e.MedicareNumber);
            entity.HasIndex(e => new { e.LastName, e.FirstName });
            // Explicit DB-level default (not just the C# initializer) so the migration backfills
            // every existing patient as visible-to-all — no one loses access when this ships.
            entity.Property(e => e.VisibleToAllProviders).HasDefaultValue(true);

            entity.HasOne(e => e.NextOfKinClient)
                .WithMany()
                .HasForeignKey(e => e.NextOfKinClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EmergencyContactClient)
                .WithMany()
                .HasForeignKey(e => e.EmergencyContactClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PayerClient)
                .WithMany()
                .HasForeignKey(e => e.PayerClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByPractitioner)
                .WithMany()
                .HasForeignKey(e => e.CreatedByPractitionerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.HealthFund)
                .WithMany()
                .HasForeignKey(e => e.HealthFundId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Clinic)
                .WithMany()
                .HasForeignKey(e => e.ClinicId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClinicId);
        });

        // HealthFund configuration
        modelBuilder.Entity<HealthFund>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.HasIndex(e => e.Code).IsUnique();

            entity.HasData(
                new HealthFund { Id = 1, Code = "MED", Name = "Medibank Private" },
                new HealthFund { Id = 2, Code = "BUP", Name = "Bupa" },
                new HealthFund { Id = 3, Code = "HCF", Name = "HCF" },
                new HealthFund { Id = 4, Code = "NIB", Name = "nib" },
                new HealthFund { Id = 5, Code = "GMH", Name = "GMHBA" },
                new HealthFund { Id = 6, Code = "AU", Name = "Australian Unity" },
                new HealthFund { Id = 7, Code = "HBF", Name = "HBF Health" },
                new HealthFund { Id = 8, Code = "TUH", Name = "Teachers Health" },
                new HealthFund { Id = 9, Code = "DHF", Name = "Doctors' Health Fund" },
                new HealthFund { Id = 10, Code = "FRK", Name = "Frank Health Insurance" }
            );
        });

        // ProviderGroup configuration
        modelBuilder.Entity<ProviderGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasData(
                new ProviderGroup { Id = 1, Name = "General Practitioners" },
                new ProviderGroup { Id = 2, Name = "Specialists" },
                new ProviderGroup { Id = 3, Name = "Physiotherapists" },
                new ProviderGroup { Id = 4, Name = "Dentists" },
                new ProviderGroup { Id = 5, Name = "Nurse" },
                new ProviderGroup { Id = 6, Name = "Allied Health Professional" }
            );
        });

        // ClinicSettings configuration (single-row settings table)
        // SystemSettings — application-wide, single row. Seeded so the row always exists; every
        // default matches the value that used to be hardcoded, so seeding is behaviour-neutral.
        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasData(new SystemSettings { Id = 1 });
        });

        modelBuilder.Entity<ClinicSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PracticeName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Abn).HasMaxLength(20);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Fax).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Website).HasMaxLength(255);
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Suburb).HasMaxLength(100);
            entity.Property(e => e.Postcode).HasMaxLength(10);
            entity.Property(e => e.State).HasMaxLength(10);
            entity.Property(e => e.SmtpHost).HasMaxLength(255);
            entity.Property(e => e.SmtpUsername).HasMaxLength(255);
            entity.Property(e => e.SmtpPassword).HasMaxLength(255);
            entity.Property(e => e.SmtpFromEmail).HasMaxLength(255);
            entity.Property(e => e.SmtpFromName).HasMaxLength(200);

            entity.HasData(
                new ClinicSettings { Id = 1, PracticeName = "IntelliMed Clinic" }
            );
        });

        // EmailTemplate configuration
        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(255);
            entity.Property(e => e.EventKey).HasMaxLength(50);
            entity.HasIndex(e => new { e.ClinicId, e.EventKey });
        });

        // SearchAction configuration (global command palette catalogue)
        modelBuilder.Entity<SearchAction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Keywords).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Target).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PageKey).HasMaxLength(200);
            entity.HasIndex(e => e.IsActive);
        });

        // Clinic configuration
        modelBuilder.Entity<Clinic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Abn).HasMaxLength(20);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Fax).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Suburb).HasMaxLength(100);
            entity.Property(e => e.Postcode).HasMaxLength(10);
            entity.Property(e => e.State).HasMaxLength(10);

            entity.HasData(
                new Clinic { Id = 1, Name = "Main Clinic" }
            );
        });

        // UserClinic configuration (many-to-many join)
        modelBuilder.Entity<UserClinic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ApplicationUserId, e.ClinicId }).IsUnique();

            entity.HasOne(e => e.ApplicationUser)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Clinic)
                .WithMany(c => c.UserClinics)
                .HasForeignKey(e => e.ClinicId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ClientAddress configuration
        modelBuilder.Entity<ClientAddress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AddressLine1).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AddressLine2).HasMaxLength(255);
            entity.Property(e => e.Suburb).HasMaxLength(100);
            entity.Property(e => e.Postcode).HasMaxLength(10);
            entity.Property(e => e.State).HasMaxLength(10);
            entity.Property(e => e.AddressSubType).HasMaxLength(50);
            entity.Property(e => e.Community).HasMaxLength(100);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ClientId, e.AddressType });
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
        });

        // ClientReferral configuration
        modelBuilder.Entity<ClientReferral>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReferralPeriod).HasMaxLength(2);
            entity.Property(e => e.RequestTypeCde).HasMaxLength(1);
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReferringContact)
                .WithMany()
                .HasForeignKey(e => e.ReferringContactId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
        });

        // AddressBookContact configuration — people only; organisations are Clients, workplaces are Clinics
        modelBuilder.Entity<AddressBookContact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(20);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Profession).HasMaxLength(100);
            entity.Property(e => e.Speciality).HasMaxLength(100);
            entity.Property(e => e.ProviderNumber).HasMaxLength(20);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Mobile).HasMaxLength(50);
            entity.Property(e => e.Fax).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.Suburb).HasMaxLength(100);
            entity.Property(e => e.Postcode).HasMaxLength(10);
            entity.Property(e => e.State).HasMaxLength(10);
            entity.Property(e => e.PreferredName).HasMaxLength(100);
            entity.Property(e => e.Hpii).HasMaxLength(20);
            entity.Property(e => e.ProviderTypeCode).HasMaxLength(20);
            entity.Property(e => e.Abn).HasMaxLength(20);
            entity.Property(e => e.Acn).HasMaxLength(20);
            entity.Property(e => e.AddressLine2).HasMaxLength(200);
            entity.Property(e => e.Attention).HasMaxLength(200);
            entity.Property(e => e.MessagingId).HasMaxLength(100);
            entity.Property(e => e.MessagingNetwork).HasMaxLength(100);
            entity.Property(e => e.ExternalId).HasMaxLength(100);
            entity.HasOne(e => e.Clinic)
                .WithMany()
                .HasForeignKey(e => e.ClinicId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.LastName);
            entity.HasIndex(e => e.ProviderNumber);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
        });

        // Results inbox — pathology / imaging reports received from labs
        modelBuilder.Entity<ResultReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SenderName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MessageControlId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PatientIdentifier).HasMaxLength(100);
            entity.Property(e => e.PatientFamilyName).HasMaxLength(200);
            entity.Property(e => e.PatientGivenName).HasMaxLength(200);
            entity.Property(e => e.PatientSex).HasMaxLength(10);
            entity.Property(e => e.PatientMedicareNumber).HasMaxLength(20);
            entity.Property(e => e.OrderingProviderName).HasMaxLength(200);
            entity.Property(e => e.OrderingProviderNumber).HasMaxLength(20);
            entity.Property(e => e.PlacerOrderNumber).HasMaxLength(100);
            entity.Property(e => e.FillerOrderNumber).HasMaxLength(100);
            entity.Property(e => e.ReportName).HasMaxLength(300);
            entity.Property(e => e.ReviewComment).HasMaxLength(1000);
            entity.Property(e => e.HoldNote).HasMaxLength(500);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedPractitioner)
                .WithMany()
                .HasForeignKey(e => e.AssignedPractitionerId)
                .OnDelete(DeleteBehavior.Restrict);
            // A re-sent message must not create a second report.
            entity.HasIndex(e => new { e.ClinicId, e.SenderName, e.MessageControlId }).IsUnique();
            entity.HasIndex(e => new { e.ClinicId, e.Status });
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.PlacerOrderNumber);
        });

        modelBuilder.Entity<ResultItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Units).HasMaxLength(100);
            entity.Property(e => e.ReferenceRange).HasMaxLength(200);
            entity.Property(e => e.AbnormalFlag).HasMaxLength(10);
            entity.Property(e => e.ResultStatus).HasMaxLength(5);
            entity.HasOne(e => e.ResultReport)
                .WithMany(r => r.Items)
                .HasForeignKey(e => e.ResultReportId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ResultReportId);
        });

        modelBuilder.Entity<ResultAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.ResultReport)
                .WithMany(r => r.Attachments)
                .HasForeignKey(e => e.ResultReportId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ResultReportId);
        });

        modelBuilder.Entity<ClientLabIdentifier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SenderName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Identifier).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.SenderName, e.Identifier });
            entity.HasIndex(e => new { e.ClientId, e.SenderName, e.Identifier }).IsUnique();
        });

        modelBuilder.Entity<ResultIngestKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.Property(e => e.KeyPrefix).IsRequired().HasMaxLength(16);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(64);
            entity.HasOne(e => e.Clinic)
                .WithMany()
                .HasForeignKey(e => e.ClinicId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.KeyHash).IsUnique();
        });

        // ClientCompensationClaim configuration
        modelBuilder.Entity<ClientCompensationClaim>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClaimNum).IsRequired().HasMaxLength(30);
            entity.Property(e => e.CaseManagerName).HasMaxLength(200);
            entity.Property(e => e.PayerName).HasMaxLength(200);
            entity.Property(e => e.PublicNote).HasMaxLength(500);
            entity.Property(e => e.PrivateNote).HasMaxLength(500);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EmployerClient)
                .WithMany()
                .HasForeignKey(e => e.EmployerClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
        });

        // ClientOccupation configuration
        modelBuilder.Entity<ClientOccupation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Occupation).HasMaxLength(255);
            entity.Property(e => e.Comment).HasMaxLength(255);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EmployerClient)
                .WithMany()
                .HasForeignKey(e => e.EmployerClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
        });

        // ClientFamilyRelationship configuration
        modelBuilder.Entity<ClientFamilyRelationship>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RelationshipType).HasMaxLength(50);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RelativeClient)
                .WithMany()
                .HasForeignKey(e => e.RelativeClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
        });

        // UserDefinedFieldType configuration
        modelBuilder.Entity<UserDefinedFieldType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DefaultValue).HasMaxLength(255);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // ClientUserDefinedFieldValue configuration
        modelBuilder.Entity<ClientUserDefinedFieldValue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Value).HasMaxLength(255);
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UserDefinedFieldType)
                .WithMany()
                .HasForeignKey(e => e.UserDefinedFieldTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
        });

        // Practitioner configuration
        modelBuilder.Entity<Practitioner>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(20);
            entity.Property(e => e.Profession).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ProviderNumber).HasMaxLength(20);
            entity.HasIndex(e => e.ProviderNumber);
            entity.HasIndex(e => e.ApplicationUserId);
        });

        // ClientProviderAccess configuration — explicit per-practitioner visibility grants beyond
        // the always-implicit creator (Client.CreatedByPractitionerId). See ClientRepository.
        modelBuilder.Entity<ClientProviderAccess>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ClientId, e.PractitionerId }).IsUnique();

            entity.HasOne(e => e.Client)
                .WithMany(c => c.ProviderAccess)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Practitioner)
                .WithMany()
                .HasForeignKey(e => e.PractitionerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Appointment configuration
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Practitioner)
                .WithMany(p => p.Appointments)
                .HasForeignKey(e => e.PractitionerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Clinic>()
                .WithMany()
                .HasForeignKey(e => e.ClinicId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AppointmentTypeSetting)
                .WithMany()
                .HasForeignKey(e => e.AppointmentTypeSettingId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.AppointmentDate);
            entity.HasIndex(e => new { e.PractitionerId, e.AppointmentDate });
            entity.HasIndex(e => e.ClinicId);
            entity.HasIndex(e => e.RecurrenceSeriesId);
        });

        // AppointmentTypeSetting configuration
        modelBuilder.Entity<AppointmentTypeSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ColorHex).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.ClinicId);
        });

        // ProviderSchedule configuration (self-service weekly hours, keyed on ApplicationUserId)
        modelBuilder.Entity<ProviderSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.ApplicationUser)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.ApplicationUserId, e.DayOfWeek }).IsUnique();
        });

        // Invoice configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Appointment)
                .WithMany()
                .HasForeignKey(e => e.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Practitioner)
                .WithMany()
                .HasForeignKey(e => e.PractitionerId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.PayeePractitioner)
                .WithMany()
                .HasForeignKey(e => e.PayeePractitionerId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Clinic>()
                .WithMany()
                .HasForeignKey(e => e.ClinicId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClinicId);
        });

        // InvoiceItem configuration
        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.BillingItem)
                .WithMany()
                .HasForeignKey(e => e.BillingItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FeeSchedule)
                .WithMany()
                .HasForeignKey(e => e.FeeScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // BillingItem configuration
        modelBuilder.Entity<BillingItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Category).HasMaxLength(10);
            entity.Property(e => e.Group).HasMaxLength(10);
            entity.Property(e => e.Description).IsRequired();
            entity.HasIndex(e => e.ItemNumber).IsUnique();
        });

        // Medicine configuration — reference data, never hard-deleted (see BillingItem)
        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.GenericName).HasMaxLength(255);
            entity.Property(e => e.Strength).HasMaxLength(100);
            entity.Property(e => e.Form).HasMaxLength(100);
            entity.Property(e => e.Manufacturer).HasMaxLength(200);
            entity.Property(e => e.ArtgId).HasMaxLength(50);
            entity.Property(e => e.AmtConceptId).HasMaxLength(50);
            entity.Property(e => e.PbsItemCode).HasMaxLength(20);
            entity.Property(e => e.Route).HasMaxLength(100);
            entity.Property(e => e.TherapeuticGroupId).HasMaxLength(20);
            entity.Property(e => e.TherapeuticGroupTitle).HasMaxLength(255);
            // Duplicate-therapy checking looks medicines up by group, so this is a real query path.
            entity.HasIndex(e => e.TherapeuticGroupId);
            entity.Property(e => e.AtcCode).HasMaxLength(20);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.AmtConceptId).IsUnique();
        });

        // ActiveIngredient configuration
        modelBuilder.Entity<ActiveIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AmtSubstanceId).HasMaxLength(50);
            entity.Property(e => e.AtcCode).HasMaxLength(20);
            entity.Property(e => e.WikidataId).HasMaxLength(20);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // MedicineIngredient configuration — bridge, modeled on ClientProviderAccess
        modelBuilder.Entity<MedicineIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MedicineId, e.ActiveIngredientId }).IsUnique();

            entity.HasOne(e => e.Medicine)
                .WithMany(m => m.Ingredients)
                .HasForeignKey(e => e.MedicineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ActiveIngredient)
                .WithMany()
                .HasForeignKey(e => e.ActiveIngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ClientHealthIssue configuration — Restrict on Client, matching every other clinical
        // client-child record: an orphaned problem-list row is worse than a blocked delete.
        modelBuilder.Entity<ClientHealthIssue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Condition).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CodingSystem).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
        });

        // ClientAllergy configuration
        modelBuilder.Entity<ClientAllergy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Substance).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Reaction).HasMaxLength(255);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ActiveIngredient)
                .WithMany()
                .HasForeignKey(e => e.ActiveIngredientId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.ClientId);
        });

        // DrugInteraction configuration — ships empty, admin CSV import populates it (see
        // DrugInteractionImportService). Canonical A<B ingredient ordering is enforced by whoever
        // writes a row (DrugInteractionRepository/DrugInteractionImportService), not by the DB itself.
        modelBuilder.Entity<DrugInteraction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SourceDataset).HasMaxLength(100);
            entity.HasIndex(e => new { e.IngredientAId, e.IngredientBId }).IsUnique();
            entity.HasOne(e => e.IngredientA)
                .WithMany()
                .HasForeignKey(e => e.IngredientAId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.IngredientB)
                .WithMany()
                .HasForeignKey(e => e.IngredientBId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Prescription configuration — no client-scoped delete route exists (see
        // PrescriptionRepository), only Cease/Cancel status transitions, so ClientId/MedicineId/
        // PrescribedByPractitionerId are all Restrict rather than Cascade: a legal prescription record
        // should never silently vanish because something it references was removed. AppointmentId is
        // SetNull (matches Invoice.AppointmentId exactly) since losing the visit link is acceptable,
        // losing the script itself is not.
        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MedicineName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ScriptNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Dose).HasMaxLength(100);
            entity.Property(e => e.Frequency).HasMaxLength(100);
            entity.Property(e => e.Instruction).HasMaxLength(255);
            entity.Property(e => e.Route).HasMaxLength(50);
            entity.Property(e => e.Quantity).HasMaxLength(100);
            entity.Property(e => e.AuthorityApprovalNumber).HasMaxLength(50);
            entity.Property(e => e.StateAuthorityCode).HasMaxLength(50);
            entity.Property(e => e.PrescribingNotes).HasMaxLength(500);
            entity.Property(e => e.NotesForPharmacy).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.SafetyOverrideNote).HasMaxLength(500);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.ScriptNumber).IsUnique();

            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Medicine)
                .WithMany()
                .HasForeignKey(e => e.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Appointment)
                .WithMany()
                .HasForeignKey(e => e.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.PrescribedByPractitioner)
                .WithMany()
                .HasForeignKey(e => e.PrescribedByPractitionerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Episode)
                .WithMany()
                .HasForeignKey(e => e.EpisodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PrescriptionIssue configuration — an immutable audit record (see PrescriptionIssue.cs), so
        // its own FK back to Prescription is Restrict too, belt-and-suspenders alongside there being
        // no delete route for either entity at all.
        modelBuilder.Entity<PrescriptionIssue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Dose).HasMaxLength(100);
            entity.Property(e => e.Frequency).HasMaxLength(100);
            entity.Property(e => e.Route).HasMaxLength(50);
            entity.Property(e => e.Quantity).HasMaxLength(100);
            entity.Property(e => e.PrescribingNotes).HasMaxLength(500);
            entity.Property(e => e.NotesForPharmacy).HasMaxLength(500);
            entity.Property(e => e.PrescriberName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PrescriberProviderNumber).HasMaxLength(20);
            entity.Property(e => e.PrescriberAhpraNumber).HasMaxLength(20);
            entity.Property(e => e.PrescriberHpiiNumber).HasMaxLength(20);
            entity.Property(e => e.PrescriberQualifications).HasMaxLength(255);
            entity.HasIndex(e => e.PrescriptionId);

            entity.HasOne(e => e.Prescription)
                .WithMany(p => p.Issues)
                .HasForeignKey(e => e.PrescriptionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.IssuedByPractitioner)
                .WithMany()
                .HasForeignKey(e => e.IssuedByPractitionerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Episode configuration — an organizational label grouping ProgressNotes, not itself a legal
        // document (see Episode.cs), so Restrict here is just belt-and-suspenders: there's no delete
        // route at all, only Archive.
        modelBuilder.Entity<Episode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.ClientId);

            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProgressNote configuration — Restrict on Client/Practitioner/Episode (an orphaned reference on
        // a clinical record is worse than a SetNull would allow, same reasoning as Prescription's own
        // Client/PrescribedByPractitioner FKs); SetNull on Appointment matches
        // Invoice.AppointmentId/Prescription.AppointmentId exactly — this "encounter" link is optional.
        modelBuilder.Entity<ProgressNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReasonForVisit).HasMaxLength(255);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.AppointmentId);

            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Appointment)
                .WithMany()
                .HasForeignKey(e => e.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Practitioner)
                .WithMany()
                .HasForeignKey(e => e.PractitionerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Episode)
                .WithMany(ep => ep.ProgressNotes)
                .HasForeignKey(e => e.EpisodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProgressNoteAddendum configuration — an immutable audit record (see ProgressNoteAddendum.cs),
        // so its own FK back to ProgressNote is Restrict too, belt-and-suspenders alongside there being
        // no delete route for either entity once Finalized.
        modelBuilder.Entity<ProgressNoteAddendum>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.SourceReferenceType).HasMaxLength(50);
            entity.HasIndex(e => e.ProgressNoteId);

            entity.HasOne(e => e.ProgressNote)
                .WithMany(n => n.Addenda)
                .HasForeignKey(e => e.ProgressNoteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByPractitioner)
                .WithMany()
                .HasForeignKey(e => e.CreatedByPractitionerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // FeeSchedule configuration
        modelBuilder.Entity<FeeSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.SourceUrl).HasMaxLength(1000);
            entity.Property(e => e.LastFetchResult).HasMaxLength(500);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasOne(e => e.HealthFund)
                .WithMany()
                .HasForeignKey(e => e.HealthFundId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FeeTable)
                .WithMany()
                .HasForeignKey(e => e.FeeTableId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // FeeScheduleItem configuration
        modelBuilder.Entity<FeeScheduleItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FeeScheduleId, e.BillingItemId }).IsUnique();
            entity.HasOne(e => e.FeeSchedule)
                .WithMany()
                .HasForeignKey(e => e.FeeScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BillingItem)
                .WithMany()
                .HasForeignKey(e => e.BillingItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // FeeScheduleItemPriceHistory configuration
        modelBuilder.Entity<FeeScheduleItemPriceHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FeeScheduleItemId, e.ChangedAt });
            entity.HasOne(e => e.FeeScheduleItem)
                .WithMany()
                .HasForeignKey(e => e.FeeScheduleItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DerivedItemConfig configuration
        modelBuilder.Entity<DerivedItemConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.AssociatedGroup).HasMaxLength(10);
            entity.HasIndex(e => e.BillingItemId).IsUnique();
            entity.HasOne(e => e.BillingItem)
                .WithMany()
                .HasForeignKey(e => e.BillingItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssociatedBillingItem)
                .WithMany()
                .HasForeignKey(e => e.AssociatedBillingItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // DerivedItemRateCalculated configuration
        modelBuilder.Entity<DerivedItemRateCalculated>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FeeScheduleItemId, e.OrderNum }).IsUnique();
            entity.HasOne(e => e.FeeScheduleItem)
                .WithMany(f => f.DerivedItemRateCalculateds)
                .HasForeignKey(e => e.FeeScheduleItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AccountTypeFeeScheduleMapping configuration
        modelBuilder.Entity<AccountTypeFeeScheduleMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ClinicId, e.AccountType }).IsUnique();
            entity.HasOne(e => e.FeeSchedule)
                .WithMany()
                .HasForeignKey(e => e.FeeScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Receipt configuration
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
            entity.HasIndex(e => e.ClinicId);
            entity.HasIndex(e => e.PayerClientId);
            entity.HasOne(e => e.PayerClient)
                .WithMany()
                .HasForeignKey(e => e.PayerClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Clinic>()
                .WithMany()
                .HasForeignKey(e => e.ClinicId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ReceiptPayment configuration (a tender within a Receipt)
        modelBuilder.Entity<ReceiptPayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
            entity.HasOne(e => e.Receipt)
                .WithMany(r => r.Payments)
                .HasForeignKey(e => e.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // InvoiceWriteOff configuration (invoice-level debt forgiveness, no money movement)
        modelBuilder.Entity<InvoiceWriteOff>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.InvoiceId);
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.WriteOffs)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ReceiptAllocation configuration (a settlement within a Receipt)
        modelBuilder.Entity<ReceiptAllocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LegacyGuid).IsUnique();
            entity.HasIndex(e => e.InvoiceId);
            entity.HasOne(e => e.Receipt)
                .WithMany(r => r.Allocations)
                .HasForeignKey(e => e.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.ReceiptAllocations)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InvoiceItem)
                .WithMany()
                .HasForeignKey(e => e.InvoiceItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RolePermission configuration
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoleName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PageKey).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.HasIndex(e => new { e.RoleName, e.PageKey }).IsUnique();
        });

        // Notification configuration
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecipientUserId).IsRequired();
            entity.Property(e => e.Message).IsRequired().HasMaxLength(500);
            entity.Property(e => e.LinkUrl).HasMaxLength(300);
            entity.HasOne(e => e.RecipientUser)
                .WithMany()
                .HasForeignKey(e => e.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.RecipientUserId, e.IsRead, e.CreatedAt });
        });

        // DbAdminAuditLog configuration (the /dbadmin tool's only audit trail)
        modelBuilder.Entity<DbAdminAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
        });

        // DatabaseBackupSettings configuration (single-row settings table)
        modelBuilder.Entity<DatabaseBackupSettings>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasData(
                new DatabaseBackupSettings { Id = 1, IntervalValue = 1, IntervalUnit = BackupIntervalUnit.Days }
            );
        });
    }
}
