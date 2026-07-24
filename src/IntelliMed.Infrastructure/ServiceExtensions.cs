using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntelliMed.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Add DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        // Register repositories
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IPractitionerRepository, PractitionerRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPatientAddressRepository, PatientAddressRepository>();
        services.AddScoped<IPatientReferralRepository, PatientReferralRepository>();
        services.AddScoped<IPatientCompensationClaimRepository, PatientCompensationClaimRepository>();
        services.AddScoped<IPatientOccupationRepository, PatientOccupationRepository>();
        services.AddScoped<IPatientFamilyRelationshipRepository, PatientFamilyRelationshipRepository>();
        services.AddScoped<IUserDefinedFieldTypeRepository, UserDefinedFieldTypeRepository>();
        services.AddScoped<IPatientUserDefinedFieldValueRepository, PatientUserDefinedFieldValueRepository>();
        services.AddScoped<IHealthFundRepository, HealthFundRepository>();
        services.AddScoped<IProviderGroupRepository, ProviderGroupRepository>();
        services.AddScoped<IClinicSettingsRepository, ClinicSettingsRepository>();
        services.AddScoped<IClinicRepository, ClinicRepository>();

        return services;
    }
}