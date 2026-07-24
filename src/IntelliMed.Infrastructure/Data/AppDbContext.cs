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
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ClientAddress> ClientAddresses => Set<ClientAddress>();
    public DbSet<ClientReferral> ClientReferrals => Set<ClientReferral>();
    public DbSet<ClientCompensationClaim> ClientCompensationClaims => Set<ClientCompensationClaim>();
    public DbSet<ClientOccupation> ClientOccupations => Set<ClientOccupation>();
    public DbSet<ClientFamilyRelationship> ClientFamilyRelationships => Set<ClientFamilyRelationship>();
    public DbSet<UserDefinedFieldType> UserDefinedFieldTypes => Set<UserDefinedFieldType>();
    public DbSet<ClientUserDefinedFieldValue> ClientUserDefinedFieldValues => Set<ClientUserDefinedFieldValue>();
    public DbSet<HealthFund> HealthFunds => Set<HealthFund>();
    public DbSet<ProviderGroup> ProviderGroups => Set<ProviderGroup>();
    public DbSet<ClinicSettings> ClinicSettings => Set<ClinicSettings>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<UserClinic> UserClinics => Set<UserClinic>();

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
            entity.HasIndex(e => e.MedicareNumber);
            entity.HasIndex(e => new { e.LastName, e.FirstName });

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

            entity.HasData(
                new ClinicSettings { Id = 1, PracticeName = "IntelliMed Clinic" }
            );
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
        });

        // ClientReferral configuration
        modelBuilder.Entity<ClientReferral>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReferralPeriod).HasMaxLength(2);
            entity.Property(e => e.ReferringProviderName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ReferringProviderNumber).HasMaxLength(20);
            entity.Property(e => e.RequestTypeCde).HasMaxLength(1);
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
        });

        // ClientCompensationClaim configuration
        modelBuilder.Entity<ClientCompensationClaim>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClaimNum).IsRequired().HasMaxLength(30);
            entity.Property(e => e.EmployerName).HasMaxLength(200);
            entity.Property(e => e.CaseManagerName).HasMaxLength(200);
            entity.Property(e => e.PayerName).HasMaxLength(200);
            entity.Property(e => e.PublicNote).HasMaxLength(500);
            entity.Property(e => e.PrivateNote).HasMaxLength(500);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
        });

        // ClientOccupation configuration
        modelBuilder.Entity<ClientOccupation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Occupation).HasMaxLength(255);
            entity.Property(e => e.Employer).HasMaxLength(255);
            entity.Property(e => e.Comment).HasMaxLength(255);
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ClientId);
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
            entity.HasIndex(e => e.AppointmentDate);
            entity.HasIndex(e => new { e.PractitionerId, e.AppointmentDate });
            entity.HasIndex(e => e.ClinicId);
        });

        // Invoice configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Appointment)
                .WithMany()
                .HasForeignKey(e => e.AppointmentId)
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
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }
}
