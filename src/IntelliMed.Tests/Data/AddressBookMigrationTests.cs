using FluentAssertions;
using IntelliMed.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace IntelliMed.Tests.Data;

/// <summary>
/// Runs the real migrations against a real SQLite file: migrates to the schema just before the
/// address-book change, inserts rows the old way (free-text referrers and employers), migrates
/// forward, and checks every row was linked to the right record before the text columns were
/// dropped. Then migrates back and checks the text is rebuilt from the links.
/// </summary>
public class AddressBookMigrationTests : IDisposable
{
    private const string BeforeAddressBook = "20260818121125_AddPbsPrescribingLimits";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"intellimed-migration-test-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            if (File.Exists(_dbPath + suffix)) File.Delete(_dbPath + suffix);
        }
    }

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={_dbPath};Pooling=false").Options);

    private static Task MigrateToAsync(AppDbContext context, string? target) =>
        context.GetService<IMigrator>().MigrateAsync(target);

    private static async Task<long> ScalarAsync(AppDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt64(value);
    }

    private static async Task<string?> TextAsync(AppDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : value.ToString();
    }

    /// <summary>A client row with every NOT NULL column of the pre-change schema filled in.</summary>
    private static string InsertClient(int id, int clinicId, int type, string lastName) => $@"
        INSERT INTO Clients (Id, ClinicId, Type, FirstName, LastName, DateOfBirth, DobAccuracy, InterpreterRequired,
            MedicareNumber, AtsiStatus, MedicareIncentiveEligible, CtgCoPaymentRelief, Address, Suburb, State, Postcode,
            Email, Phone, AcceptSms, AcceptEmail, AcceptOnlineAppointments, AcceptSmsMarketing, SameAsNextOfKin,
            AccountType, UseMedicareRegisteredBankAccount, Deceased, IsActive, CreatedAt, VisibleToAllProviders)
        VALUES ({id}, {clinicId}, {type}, 'Pat', '{lastName}', '1980-05-01 00:00:00', 0, 0,
            '', 0, 0, 0, '', '', '', '', '', '', 0, 0, 0, 0, 0, 0, 0, 0, 1, '2026-01-01 00:00:00', 1);";

    private async Task SeedOldStyleDataAsync()
    {
        await using var context = NewContext();
        await MigrateToAsync(context, BeforeAddressBook);

        await context.Database.ExecuteSqlRawAsync("INSERT INTO Clinics (Id, Name, IsActive) VALUES (2, 'Second Clinic', 1);");
        await context.Database.ExecuteSqlRawAsync(InsertClient(100, 1, 0, "PatientOne"));
        await context.Database.ExecuteSqlRawAsync(InsertClient(101, 1, 0, "PatientTwo"));
        await context.Database.ExecuteSqlRawAsync(InsertClient(102, 2, 0, "PatientThree"));
        // An organisation that already exists in clinic 1 — employer text matching it must reuse it.
        await context.Database.ExecuteSqlRawAsync(InsertClient(200, 1, 1, "Acme Pty Ltd"));

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ClientReferrals (ClientId, ReferralDate, IsDefault, IsGP, ReferringProviderName, ReferringProviderNumber, IsArchived, CreatedAt) VALUES
                (100, '2026-01-01 00:00:00', 0, 0, 'Dr Jane Smith', '2345678A', 0, '2026-01-01 00:00:00'),
                (101, '2026-01-01 00:00:00', 0, 0, 'dr jane smith ', '2345678A', 0, '2026-01-01 00:00:00'),
                (101, '2026-01-01 00:00:00', 0, 1, 'Dr Bob Lee', NULL, 0, '2026-01-01 00:00:00');");

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ClientOccupations (ClientId, Occupation, Employer, HasAsbestos, HasDust, HasRadiation, HasAnimals, IsArchived, CreatedAt) VALUES
                (100, 'Welder', 'ACME PTY LTD', 0, 0, 0, 0, 0, '2026-01-01 00:00:00'),
                (101, 'Driver', 'Fast Freight', 0, 0, 0, 0, 0, '2026-01-01 00:00:00'),
                (102, 'Driver', 'Fast Freight', 0, 0, 0, 0, 0, '2026-01-01 00:00:00'),
                (101, 'Student', NULL, 0, 0, 0, 0, 0, '2026-01-01 00:00:00');");

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ClientCompensationClaims (ClientId, ClaimNum, EmployerName, IsDefault, IsArchived, CreatedAt) VALUES
                (101, 'WC-1', 'fast freight', 0, 0, '2026-01-01 00:00:00');");
    }

    [Fact]
    public async Task MigratingForward_LinksReferrersAndEmployers_ThenDropsTheFreeText()
    {
        await SeedOldStyleDataAsync();
        await using var context = NewContext();
        await MigrateToAsync(context, null);

        // Referrers: "Dr Jane Smith" and "dr jane smith " (same provider number) collapse into one person.
        (await ScalarAsync(context, "SELECT COUNT(*) FROM AddressBookContacts")).Should().Be(2);
        (await ScalarAsync(context, "SELECT COUNT(*) FROM ClientReferrals WHERE ReferringContactId IS NULL")).Should().Be(0);
        (await ScalarAsync(context, "SELECT COUNT(DISTINCT ReferringContactId) FROM ClientReferrals WHERE ClientId IN (100, 101) AND IsGP = 0")).Should().Be(1);
        (await TextAsync(context, @"SELECT c.ProviderNumber FROM ClientReferrals r JOIN AddressBookContacts c ON c.Id = r.ReferringContactId
                                    WHERE r.ClientId = 100")).Should().Be("2345678A");

        // Employers: "ACME PTY LTD" reuses the existing organisation; "Fast Freight" is created once per clinic
        // (clinics 1 and 2), and the claim's "fast freight" links to clinic 1's.
        (await ScalarAsync(context, "SELECT EmployerClientId FROM ClientOccupations WHERE ClientId = 100")).Should().Be(200);
        (await ScalarAsync(context, "SELECT COUNT(*) FROM Clients WHERE Type = 1 AND LOWER(LastName) = 'fast freight'")).Should().Be(2);
        var clinicOneFreight = await ScalarAsync(context, "SELECT Id FROM Clients WHERE Type = 1 AND LastName = 'Fast Freight' AND ClinicId = 1");
        (await ScalarAsync(context, "SELECT EmployerClientId FROM ClientOccupations WHERE ClientId = 101 AND Occupation = 'Driver'")).Should().Be(clinicOneFreight);
        (await ScalarAsync(context, "SELECT EmployerClientId FROM ClientCompensationClaims WHERE ClaimNum = 'WC-1'")).Should().Be(clinicOneFreight);
        (await ScalarAsync(context, "SELECT COUNT(*) FROM ClientOccupations WHERE Occupation = 'Student' AND EmployerClientId IS NULL")).Should().Be(1);

        // Organisations carry no date of birth — new ones and the pre-existing one alike. People keep theirs.
        (await ScalarAsync(context, "SELECT COUNT(*) FROM Clients WHERE Type = 1 AND DateOfBirth IS NOT NULL")).Should().Be(0);
        (await ScalarAsync(context, "SELECT COUNT(*) FROM Clients WHERE Type = 0 AND DateOfBirth IS NULL")).Should().Be(0);

        // The free-text columns are gone.
        (await ScalarAsync(context, "SELECT COUNT(*) FROM pragma_table_info('ClientReferrals') WHERE name IN ('ReferringProviderName', 'ReferringProviderNumber')")).Should().Be(0);
        (await ScalarAsync(context, "SELECT COUNT(*) FROM pragma_table_info('ClientOccupations') WHERE name = 'Employer'")).Should().Be(0);
        (await ScalarAsync(context, "SELECT COUNT(*) FROM pragma_table_info('ClientCompensationClaims') WHERE name = 'EmployerName'")).Should().Be(0);

        // Every role can open the Address Book, and it's in the command palette.
        (await ScalarAsync(context, "SELECT COUNT(*) FROM RolePermissions WHERE PageKey = 'address-book'")).Should().Be(5);
        (await ScalarAsync(context, "SELECT COUNT(*) FROM SearchActions WHERE Target = '/address-book'")).Should().Be(1);
    }

    [Fact]
    public async Task MigratingBack_RebuildsTheFreeTextFromTheLinks()
    {
        await SeedOldStyleDataAsync();
        await using (var forward = NewContext())
        {
            await MigrateToAsync(forward, null);
        }

        await using var context = NewContext();
        await MigrateToAsync(context, BeforeAddressBook);

        (await TextAsync(context, "SELECT ReferringProviderName FROM ClientReferrals WHERE ClientId = 100")).Should().Be("Dr Jane Smith");
        (await TextAsync(context, "SELECT ReferringProviderNumber FROM ClientReferrals WHERE ClientId = 100")).Should().Be("2345678A");
        (await TextAsync(context, "SELECT Employer FROM ClientOccupations WHERE ClientId = 100")).Should().Be("Acme Pty Ltd");
        (await TextAsync(context, "SELECT EmployerName FROM ClientCompensationClaims WHERE ClaimNum = 'WC-1'")).Should().Be("Fast Freight");
        (await ScalarAsync(context, "SELECT COUNT(*) FROM Clients WHERE DateOfBirth IS NULL")).Should().Be(0);
    }
}
