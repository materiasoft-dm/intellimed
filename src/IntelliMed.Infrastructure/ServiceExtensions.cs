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
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IPractitionerRepository, PractitionerRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IClientAddressRepository, ClientAddressRepository>();
        services.AddScoped<IClientReferralRepository, ClientReferralRepository>();
        services.AddScoped<IClientCompensationClaimRepository, ClientCompensationClaimRepository>();
        services.AddScoped<IClientOccupationRepository, ClientOccupationRepository>();
        services.AddScoped<IClientFamilyRelationshipRepository, ClientFamilyRelationshipRepository>();
        services.AddScoped<IUserDefinedFieldTypeRepository, UserDefinedFieldTypeRepository>();
        services.AddScoped<IClientUserDefinedFieldValueRepository, ClientUserDefinedFieldValueRepository>();
        services.AddScoped<IHealthFundRepository, HealthFundRepository>();
        services.AddScoped<IProviderGroupRepository, ProviderGroupRepository>();
        services.AddScoped<IClinicSettingsRepository, ClinicSettingsRepository>();
        services.AddScoped<IClinicRepository, ClinicRepository>();

        return services;
    }
}