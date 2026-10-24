using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Data only: moves the free-text referrers and employers onto real records. Kept apart from
    /// AddAddressBookAndClinicType because SQLite applies that migration's table rebuilds (e.g.
    /// Clients.DateOfBirth becoming nullable) at its end — SQL in the same migration would still see
    /// the old NOT NULL column. The old text columns are dropped by RemoveReferralAndEmployerFreeText.
    /// </summary>
    public partial class MoveReferralAndEmployerDataToRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            // An organisation has no date of birth — clear the placeholder every one was forced to carry.
            migrationBuilder.Sql("UPDATE Clients SET DateOfBirth = NULL WHERE Type = 1;");

            // Referring providers → address-book people: one per distinct name + provider number
            // (case-insensitive name). The whole free-text name goes into LastName; staff can split it.
            migrationBuilder.Sql(@"
                INSERT INTO AddressBookContacts (LastName, ProviderNumber, Notes, IsArchived, CreatedAt)
                SELECT MIN(TRIM(ReferringProviderName)),
                       NULLIF(TRIM(IFNULL(ReferringProviderNumber, '')), ''),
                       'Imported from referral records', 0, datetime('now')
                FROM ClientReferrals
                GROUP BY LOWER(TRIM(ReferringProviderName)), NULLIF(TRIM(IFNULL(ReferringProviderNumber, '')), '');");

            migrationBuilder.Sql(@"
                UPDATE ClientReferrals SET ReferringContactId = (
                    SELECT c.Id FROM AddressBookContacts c
                    WHERE LOWER(c.LastName) = LOWER(TRIM(ClientReferrals.ReferringProviderName))
                      AND IFNULL(c.ProviderNumber, '') = TRIM(IFNULL(ClientReferrals.ReferringProviderNumber, ''))
                    ORDER BY c.Id LIMIT 1);");

            // Employers → organisation clients, per clinic. Reuse an existing organisation with the same
            // name (case-insensitive) in that clinic; otherwise create one with just its name.
            migrationBuilder.Sql(@"
                INSERT INTO Clients (
                    ClinicId, Type, FirstName, LastName, DateOfBirth, DobAccuracy, InterpreterRequired,
                    MedicareNumber, AtsiStatus, MedicareIncentiveEligible, CtgCoPaymentRelief,
                    Address, Suburb, State, Postcode, Email, Phone,
                    AcceptSms, AcceptEmail, AcceptOnlineAppointments, AcceptSmsMarketing, SameAsNextOfKin,
                    AccountType, UseMedicareRegisteredBankAccount, Deceased, IsActive, CreatedAt,
                    VisibleToAllProviders, Notes)
                SELECT e.ClinicId, 1, '', e.Name, NULL, 0, 0,
                       '', 0, 0, 0,
                       '', '', '', '', '', '',
                       0, 0, 0, 0, 0,
                       0, 0, 0, 1, datetime('now'),
                       1, 'Created from employer details on patient records'
                FROM (
                    SELECT ClinicId, MIN(Name) AS Name FROM (
                        SELECT p.ClinicId AS ClinicId, TRIM(o.Employer) AS Name
                        FROM ClientOccupations o JOIN Clients p ON p.Id = o.ClientId
                        WHERE TRIM(IFNULL(o.Employer, '')) <> ''
                        UNION ALL
                        SELECT p.ClinicId, TRIM(w.EmployerName)
                        FROM ClientCompensationClaims w JOIN Clients p ON p.Id = w.ClientId
                        WHERE TRIM(IFNULL(w.EmployerName, '')) <> ''
                    ) GROUP BY ClinicId, LOWER(Name)
                ) e
                WHERE NOT EXISTS (
                    SELECT 1 FROM Clients x
                    WHERE x.Type = 1 AND x.ClinicId = e.ClinicId AND LOWER(x.LastName) = LOWER(e.Name));");

            migrationBuilder.Sql(@"
                UPDATE ClientOccupations SET EmployerClientId = (
                    SELECT x.Id FROM Clients x JOIN Clients p ON p.Id = ClientOccupations.ClientId
                    WHERE x.Type = 1 AND x.ClinicId = p.ClinicId AND LOWER(x.LastName) = LOWER(TRIM(ClientOccupations.Employer))
                    ORDER BY x.Id LIMIT 1)
                WHERE TRIM(IFNULL(Employer, '')) <> '';");

            migrationBuilder.Sql(@"
                UPDATE ClientCompensationClaims SET EmployerClientId = (
                    SELECT x.Id FROM Clients x JOIN Clients p ON p.Id = ClientCompensationClaims.ClientId
                    WHERE x.Type = 1 AND x.ClinicId = p.ClinicId AND LOWER(x.LastName) = LOWER(TRIM(ClientCompensationClaims.EmployerName))
                    ORDER BY x.Id LIMIT 1)
                WHERE TRIM(IFNULL(EmployerName, '')) <> '';");

            // Address Book page: every role by default (front desk adds referrers while booking), plus a
            // command-palette entry. Skips anything already present.
            migrationBuilder.Sql(@"
                INSERT INTO RolePermissions (RoleName, PageKey, Category)
                SELECT r.RoleName, 'address-book', 'Clinical'
                FROM (SELECT 'SuperAdmin' AS RoleName UNION ALL SELECT 'Admin' UNION ALL SELECT 'Doctor'
                      UNION ALL SELECT 'Nurse' UNION ALL SELECT 'Receptionist') r
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions p WHERE p.RoleName = r.RoleName AND p.PageKey = 'address-book');");

            migrationBuilder.Sql(@"
                INSERT INTO SearchActions (Title, Keywords, Description, Category, ActionType, Target, PageKey, SortOrder, IsActive)
                SELECT 'Address Book', 'contacts doctors specialists referrers gp provider',
                       'GPs, specialists and other providers', 'Clinical', 0, '/address-book', 'address-book', 15, 1
                WHERE NOT EXISTS (SELECT 1 FROM SearchActions WHERE Target = '/address-book');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Undo the links (the old text columns still hold the original values — Up never touched
            // them). Clearing them first also lets AddAddressBookAndClinicType's Down drop the
            // AddressBookContacts table without tripping the referrals' foreign key. Organisation
            // clients created by Up stay: they're valid organisations either way.
            migrationBuilder.Sql("UPDATE ClientReferrals SET ReferringContactId = NULL;");
            migrationBuilder.Sql("UPDATE ClientOccupations SET EmployerClientId = NULL;");
            migrationBuilder.Sql("UPDATE ClientCompensationClaims SET EmployerClientId = NULL;");
            migrationBuilder.Sql("DELETE FROM AddressBookContacts;");
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PageKey = 'address-book';");
            migrationBuilder.Sql("DELETE FROM SearchActions WHERE Target = '/address-book';");
        }
    }
}
