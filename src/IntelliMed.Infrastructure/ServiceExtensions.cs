using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Infrastructure.Services;
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
        services.AddScoped<IBillingItemRepository, BillingItemRepository>();
        services.AddHttpClient<IBillingItemSyncService, BillingItemSyncService>();
        services.AddScoped<IFeeScheduleRepository, FeeScheduleRepository>();
        services.AddScoped<IAccountTypeFeeScheduleMappingRepository, AccountTypeFeeScheduleMappingRepository>();
        services.AddScoped<IBillingCalculator, BillingCalculator>();
        services.AddScoped<IDerivedItemConfigRepository, DerivedItemConfigRepository>();
        services.AddScoped<IDerivedFeeCalculator, DerivedFeeCalculator>();
        services.AddScoped<IMultipleOperationRuleCalculator, MultipleOperationRuleCalculator>();
        services.AddScoped<IPrimaryClinicMigratorService, PrimaryClinicMigratorService>();
        services.AddScoped<IAppointmentTypeSettingRepository, AppointmentTypeSettingRepository>();
        services.AddScoped<IProviderScheduleRepository, ProviderScheduleRepository>();
        services.AddScoped<IAppointmentReminderService, NoOpAppointmentReminderService>();
        // A default-less HttpClient (no User-Agent/Accept) gets served an empty body by some CDNs
        // that front provider-portal fee schedule downloads (e.g. AHSA's Zendesk-hosted attachments)
        // — set browser-like headers so those direct-download links actually return their content.
        services.AddHttpClient<IFeeScheduleImportService, FeeScheduleImportService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        });
        services.AddHostedService<FeeScheduleAutoImportBackgroundService>();

        return services;
    }
}