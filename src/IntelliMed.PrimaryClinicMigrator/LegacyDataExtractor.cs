using Microsoft.Data.SqlClient;

namespace IntelliMed.PrimaryClinicMigrator;

/// <summary>
/// Reads legacy Pracnet data directly from a SQL Server instance using the same queries
/// the existing PrimaryClinicMigratorService expects. Each method returns a list of
/// Dictionary rows keyed by column name — ready to be serialized as JSON and POSTed
/// to the IntelliMed API.
/// </summary>
public class LegacyDataExtractor
{
    private readonly string _connectionString;

    public LegacyDataExtractor(string server, string user, string password, string database)
    {
        _connectionString = $"Server={server};User Id={user};Password={password};Database={database};TrustServerCertificate=True;";
    }

    public async Task<List<Dictionary<string, object>>> ExtractClientsAsync()
    {
        const string sql = """
            SELECT
                c.GUID AS ContactGuid, c.FirstName, c.Surname, c.MiddleName,
                c.PreferredName, c.Title, c.Gender, c.DOB,
                c.Address1, c.Address2, c.Suburb, c.State, c.Postcode,
                c.Email, c.HomePhone, c.WorkPhone, c.Mobile,
                c.Fax, c.CanSMS, c.CanEmail, c.AcceptSMSMarketing, c.Note,
                c.IsAboriginal, c.IsTSI,
                p.WarningMessage, p.FileNum, at.Name AS AccountTypeName,
                p.MedicareNum, p.MedicareRefNum, p.MedicareExpiryDate,
                p.VeteranNum, p.VeteranExpiryDate,
                p.PensionCode, p.PensionExpireDate, p.SafetyNetNo,
                p.FundNumber, p.FundRef, p.AliasFirstName, p.AliasSurname,
                p.IsAcceptOnlineAppointment, p.DateDeceased, p.LifeCardNum,
                p.IHINumber, p.IHIRecordStatus, p.IHIAssignedDate,
                p.IHINumberStatus, p.IHIUnresolvedDate,
                p.InterpreterRequired, p.PreferredLanguage,
                c.DOBAccuracyCode, p.MaritalStatusId,
                p.BankAccountName, p.BankAccountNum, p.BSBCode,
                p.FundId, p.CultureBackground
            FROM Patients p
            INNER JOIN Contacts c ON c.GUID = p.ContactGUID
            LEFT JOIN AccountTypes at ON at.GUID = p.AccountTypeGUID
            WHERE c.DeletedDate IS NULL
              AND c.FirstName IS NOT NULL AND c.Surname IS NOT NULL
              AND c.DOB IS NOT NULL
            ORDER BY c.CreatedDate DESC
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractAddressesAsync()
    {
        const string sql = """
            SELECT
                ca.GUID, ca.ContactGUID, ca.StreetAddress,
                ca.Postcode, ca.Suburb, ca.State, ca.Type
            FROM ContactAddresses ca
            INNER JOIN Contacts c ON c.GUID = ca.ContactGUID
            WHERE c.DeletedDate IS NULL AND ca.DeletedDate IS NULL
            ORDER BY ca.GUID
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractReferralsAsync()
    {
        // ReferralRequests.PatientGUID is itself a Contacts.GUID value (Patients has no GUID
        // column of its own — its key is ContactGUID) so we join Contacts directly.
        const string sql = """
            SELECT
                rr.GUID, rr.PatientGUID, rr.ReferralDate, rr.ReferralPeriod,
                rr.IsDefault, rr.IsGP, rr.RequestTypeCde, rr.FirstVisitDate,
                rr.Note,
                rc.FirstName AS ProviderFirstName, rc.Surname AS ProviderSurname
            FROM ReferralRequests rr
            INNER JOIN Contacts pc ON pc.GUID = rr.PatientGUID AND pc.DeletedDate IS NULL
            LEFT JOIN Contacts rc ON rc.GUID = rr.ReferringProviderGUID
            WHERE rr.DeletedDate IS NULL
            ORDER BY rr.GUID
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractOccupationsAsync()
    {
        const string sql = """
            SELECT
                po.GUID, po.PatientGUID, po.Occupation, po.Employer,
                po.StartedYear, po.StoppedYear,
                po.HasAsbestos, po.HasDust, po.HasRadiation, po.HasAnimals,
                po.Comment
            FROM PreOccupations po
            INNER JOIN Contacts c ON c.GUID = po.PatientGUID AND c.DeletedDate IS NULL
            WHERE po.DeletedDate IS NULL
            ORDER BY po.GUID
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractFamilyRelationshipsAsync()
    {
        const string sql = """
            SELECT
                fr.GUID, fr.PatientGUID, fr.RelativeGUID, fr.Relationship
            FROM FamilyRelationships fr
            INNER JOIN Contacts c ON c.GUID = fr.PatientGUID AND c.DeletedDate IS NULL
            WHERE fr.DeletedDate IS NULL
            ORDER BY fr.GUID
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractCompensationClaimsAsync()
    {
        // WorkcoverTACClaims has only 5 columns (ClaimGUID, DateOfInjury, PatientGUID, EmployerGUID,
        // CaseManagerGUID) and no DeletedDate column at all.
        const string sql = """
            SELECT
                wc.ClaimGUID, wc.PatientGUID, wc.DateOfInjury,
                ec.FirstName AS EmployerFirstName, ec.Surname AS EmployerSurname,
                cc.FirstName AS CaseManagerFirstName, cc.Surname AS CaseManagerSurname
            FROM WorkcoverTACClaims wc
            INNER JOIN Contacts pc ON pc.GUID = wc.PatientGUID AND pc.DeletedDate IS NULL
            LEFT JOIN Contacts ec ON ec.GUID = wc.EmployerGUID
            LEFT JOIN Contacts cc ON cc.GUID = wc.CaseManagerGUID
            ORDER BY wc.ClaimGUID
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractInvoicesAsync()
    {
        const string sql = """
            SELECT
                i.GUID, i.PatientGUID, i.InvoiceNum, i.IssueDate, i.DueDate,
                i.PrivateNotes, at.Name AS AccountTypeName
            FROM Invoices i
            INNER JOIN Contacts c ON c.GUID = i.PatientGUID AND c.DeletedDate IS NULL
            LEFT JOIN AccountTypes at ON at.GUID = i.AccountTypeGUID
            WHERE i.DeletedDate IS NULL
            ORDER BY i.GUID
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractInvoiceItemsAsync()
    {
        const string sql = """
            SELECT
                ii.GUID, ii.InvoiceGUID, ii.ItemNum, ii.MBSItemNum,
                ii.Description, ii.ServiceDate, ii.Quantity, ii.Fee,
                ii.Discount, ii.GST, ii.GSTAmount, ii.IsFeeGSTInclusive,
                ii.Rebate, ii.NoOfPatientsSeen, ii.HospitalInd
            FROM InvoiceItems ii
            INNER JOIN Invoices i ON i.GUID = ii.InvoiceGUID AND i.DeletedDate IS NULL
            INNER JOIN Contacts c ON c.GUID = i.PatientGUID AND c.DeletedDate IS NULL
            WHERE ii.DeletedDate IS NULL
            ORDER BY ii.GUID
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractPaymentsAsync()
    {
        // ReceiptPayments has no IssueDate or PaymentTypeGUID column — IssueDate lives on Receipts,
        // and the payment-type relationship is ReceiptPayments.ItemType = PaymentTypes.Code (both
        // tinyint), confirmed by querying real sample data. Receipts itself has no PatientGUID
        // column (only PayerGUID), so there's no patient filter to join here — the API resolves
        // each payment to a client indirectly via the invoice its allocations point to.
        const string sql = """
            SELECT
                rp.GUID, rp.ReceiptGUID, rp.Amount, r.IssueDate,
                pt.Name AS PaymentTypeName
            FROM ReceiptPayments rp
            INNER JOIN Receipts r ON r.GUID = rp.ReceiptGUID AND r.DeletedDate IS NULL
            LEFT JOIN PaymentTypes pt ON pt.Code = rp.ItemType
            ORDER BY rp.GUID
            """;
        return await QueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object>>> ExtractPaymentAllocationsAsync()
    {
        // BillingAllocations has no InvoiceGUID column — only InvoiceItemGUID — so the real invoice
        // has to be resolved via a join through InvoiceItems. It also has no dollar-amount column at
        // all (confirmed against live sample data: only GST, AllocTypeCde, and audit/GUID columns) —
        // so there's no per-allocation amount to sum. We use COUNT(*) as a weight instead: in every
        // real receipt in this legacy database a single receipt pays off exactly one invoice, so the
        // weight value never actually matters for the (overwhelmingly common) single-invoice case —
        // the API takes the full tender amount whenever a receipt resolves to one invoice. For the
        // rare multi-invoice receipt, this gives an equal-per-item split rather than a dollar-accurate
        // one, which is the best available approximation given the schema.
        const string sql = """
            SELECT
                ba.ReceiptGUID, ii.InvoiceGUID,
                COUNT(*) AS AllocatedAmount
            FROM BillingAllocations ba
            INNER JOIN InvoiceItems ii ON ii.GUID = ba.InvoiceItemGUID
            INNER JOIN Receipts r ON r.GUID = ba.ReceiptGUID AND r.DeletedDate IS NULL
            WHERE ba.DeletedDate IS NULL
            GROUP BY ba.ReceiptGUID, ii.InvoiceGUID
            ORDER BY ba.ReceiptGUID, ii.InvoiceGUID
            """;
        return await QueryAsync(sql);
    }

    private async Task<List<Dictionary<string, object>>> QueryAsync(string sql)
    {
        var results = new List<Dictionary<string, object>>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 300; // 5 minutes for large datasets
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[reader.GetName(i)] = value is DBNull ? null! : value;
            }
            results.Add(row);
        }

        return results;
    }
}
