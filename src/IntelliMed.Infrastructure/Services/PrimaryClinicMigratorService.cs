using System.Text.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Upserts legacy Pracnet rows (already read from the legacy SQL Server database and shipped here as
/// plain JSON by the IntelliMed.Migrator.WinForms tool — this API never talks to SQL Server directly)
/// into IntelliMed's own model, matching by LegacyGuid so re-running the same rows updates in place
/// instead of creating duplicates. Ground-truthed against the real PracnetSample_Rosie legacy database
/// (not guessed) — every column name referenced below was confirmed via direct queries against it.
/// </summary>
public class PrimaryClinicMigratorService : IPrimaryClinicMigratorService
{
    private readonly AppDbContext _context;

    public PrimaryClinicMigratorService(AppDbContext context)
    {
        _context = context;
    }

    // =========================================================================
    // CLIENTS (Contacts + Patients + AccountTypes)
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportClientsAsync(List<Dictionary<string, JsonElement>> rows, int clinicId)
    {
        var result = new PrimaryClinicMigratorResultDto();

        foreach (var row in rows)
        {
            var legacyGuid = GetGuid(row, "ContactGuid");
            var existing = await _context.Clients.FirstOrDefaultAsync(c => c.LegacyGuid == legacyGuid);
            var client = existing ?? new Client { ClinicId = clinicId, LegacyGuid = legacyGuid };

            client.FirstName = GetString(row, "FirstName") ?? client.FirstName;
            client.LastName = GetString(row, "Surname") ?? client.LastName;
            client.MiddleName = GetString(row, "MiddleName");
            client.PreferredName = GetString(row, "PreferredName");
            client.Title = GetString(row, "Title");
            client.DateOfBirth = GetDateTime(row, "DOB") ?? client.DateOfBirth;
            client.DobAccuracy = PrimaryClinicLegacyMapper.MapDobAccuracy(GetString(row, "DOBAccuracyCode"));
            client.Gender = PrimaryClinicLegacyMapper.MapGender(GetString(row, "Gender"));
            client.Phone = GetString(row, "HomePhone") ?? string.Empty;
            client.BusinessHoursPhone = GetString(row, "WorkPhone");
            client.MobilePhone = GetString(row, "Mobile");
            client.Email = GetString(row, "Email") ?? string.Empty;
            client.FaxNumber = GetString(row, "Fax");
            client.AcceptSms = GetBool(row, "CanSMS");
            client.AcceptEmail = GetBool(row, "CanEmail");
            client.AcceptSmsMarketing = GetBool(row, "AcceptSMSMarketing");
            client.Address = string.Join(" ", new[] { GetString(row, "Address1"), GetString(row, "Address2") }.Where(s => !string.IsNullOrWhiteSpace(s)));
            client.Postcode = GetString(row, "Postcode") ?? string.Empty;
            client.Suburb = GetString(row, "Suburb") ?? string.Empty;
            client.State = GetString(row, "State") ?? string.Empty;
            client.Notes = GetString(row, "Note");
            client.AtsiStatus = PrimaryClinicLegacyMapper.MapAtsiStatus(GetBool(row, "IsAboriginal"), GetBool(row, "IsTSI"));

            client.FileNumber = GetString(row, "FileNum");
            client.Warnings = GetString(row, "WarningMessage");
            client.AccountType = PrimaryClinicLegacyMapper.MapAccountType(GetString(row, "AccountTypeName"));
            client.IhiNumber = GetString(row, "IHINumber");
            client.IhiRecordStatus = GetString(row, "IHIRecordStatus");
            client.IhiAssignedDate = GetDateTime(row, "IHIAssignedDate");
            client.IhiNumberStatus = GetString(row, "IHINumberStatus");
            client.IhiUnresolvedDate = GetDateTime(row, "IHIUnresolvedDate");
            client.MedicareNumber = GetString(row, "MedicareNum") ?? string.Empty;
            client.MedicarePosition = GetString(row, "MedicareRefNum");
            client.MedicareExpiryDate = GetDateTime(row, "MedicareExpiryDate");
            client.DvaNumber = GetString(row, "VeteranNum");
            client.DvaExpiryDate = GetDateTime(row, "VeteranExpiryDate");
            // Legacy has no distinct "pension number" column — PensionCode is the closest identifying value.
            client.PensionNumber = GetString(row, "PensionCode");
            client.PensionExpiryDate = GetDateTime(row, "PensionExpireDate");
            client.SafetyNetNumber = GetString(row, "SafetyNetNo");
            client.InterpreterRequired = GetBool(row, "InterpreterRequired");
            client.InterpreterLanguage = GetString(row, "PreferredLanguage");
            client.LifeCardNum = GetString(row, "LifeCardNum");
            client.AcceptOnlineAppointments = GetBool(row, "IsAcceptOnlineAppointment");
            client.Deceased = GetDateTime(row, "DateDeceased").HasValue;
            client.Ethnicity = GetString(row, "CultureBackground");
            client.AccountName = GetString(row, "BankAccountName");
            client.AccountNumber = GetString(row, "BankAccountNum");
            client.AccountBsb = GetString(row, "BSBCode");
            // Legacy's MaritalStatusId has no lookup table in the DB (it's an application-level enum) —
            // best-effort mapping only; not independently confirmed against the legacy source.
            client.MaritalStatus = PrimaryClinicLegacyMapper.MapMaritalStatus(GetInt(row, "MaritalStatusId"));

            var fundCode = GetString(row, "FundId");
            if (!string.IsNullOrWhiteSpace(fundCode))
            {
                var fund = await _context.HealthFunds.FirstOrDefaultAsync(f => f.Code == fundCode);
                if (fund == null)
                {
                    fund = new HealthFund { Code = fundCode, Name = fundCode };
                    _context.HealthFunds.Add(fund);
                    await _context.SaveChangesAsync();
                    result.HealthFundsCreated++;
                }
                client.HealthFundId = fund.Id;
            }
            client.HealthFundNumber = GetString(row, "FundNumber");
            client.HealthFundRef = GetString(row, "FundRef");
            client.HealthFundAliasFirst = GetString(row, "AliasFirstName");
            client.HealthFundAliasFamily = GetString(row, "AliasSurname");

            client.UpdatedAt = DateTime.UtcNow;

            if (existing == null)
            {
                _context.Clients.Add(client);
                result.ClientsCreated++;
            }
            else
            {
                result.ClientsUpdated++;
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // ADDRESSES (ContactAddresses)
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportAddressesAsync(List<Dictionary<string, JsonElement>> rows)
    {
        var result = new PrimaryClinicMigratorResultDto();

        foreach (var row in rows)
        {
            var contactGuid = GetGuid(row, "ContactGUID");
            var clientId = await _context.Clients.Where(c => c.LegacyGuid == contactGuid).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (clientId == null) continue; // parent client wasn't imported (e.g. soft-deleted) — skip rather than orphan the row

            var legacyGuid = GetGuid(row, "GUID");
            var existing = await _context.ClientAddresses.FirstOrDefaultAsync(a => a.LegacyGuid == legacyGuid);
            var address = existing ?? new ClientAddress { ClientId = clientId.Value, LegacyGuid = legacyGuid };

            address.AddressLine1 = GetString(row, "StreetAddress") ?? string.Empty;
            address.Postcode = GetString(row, "Postcode") ?? string.Empty;
            address.Suburb = GetString(row, "Suburb") ?? string.Empty;
            address.State = GetString(row, "State") ?? string.Empty;
            address.AddressSubType = GetString(row, "Type") ?? "Not Specified";
            address.AddressType = (GetString(row, "Type") ?? string.Empty).Contains("Postal", StringComparison.OrdinalIgnoreCase)
                ? ClientAddressType.Postal
                : ClientAddressType.Other;

            if (existing == null)
            {
                _context.ClientAddresses.Add(address);
                result.AddressesCreated++;
            }
            else
            {
                result.AddressesUpdated++;
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // REFERRALS (ReferralRequests, joined back to Contacts for the referring provider's name)
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportReferralsAsync(List<Dictionary<string, JsonElement>> rows)
    {
        var result = new PrimaryClinicMigratorResultDto();

        foreach (var row in rows)
        {
            var patientGuid = GetGuid(row, "PatientGUID");
            var clientId = await _context.Clients.Where(c => c.LegacyGuid == patientGuid).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (clientId == null) continue;

            var legacyGuid = GetGuid(row, "GUID");
            var existing = await _context.ClientReferrals.FirstOrDefaultAsync(r => r.LegacyGuid == legacyGuid);
            var referral = existing ?? new ClientReferral { ClientId = clientId.Value, LegacyGuid = legacyGuid };

            referral.ReferralDate = GetDateTime(row, "ReferralDate") ?? referral.ReferralDate;
            referral.ReferralPeriod = GetString(row, "ReferralPeriod");
            referral.IsDefault = GetBool(row, "IsDefault");
            referral.IsGP = GetBool(row, "IsGP");
            referral.RequestTypeCde = GetString(row, "RequestTypeCde");
            referral.FirstVisitDate = GetDateTime(row, "FirstVisitDate");
            referral.Note = GetString(row, "Note");
            var providerName = string.Join(" ", new[] { GetString(row, "ProviderFirstName"), GetString(row, "ProviderSurname") }.Where(s => !string.IsNullOrWhiteSpace(s)));
            referral.ReferringProviderName = string.IsNullOrWhiteSpace(providerName) ? referral.ReferringProviderName : providerName;
            referral.UpdatedAt = DateTime.UtcNow;

            if (existing == null)
            {
                _context.ClientReferrals.Add(referral);
                result.ReferralsCreated++;
            }
            else
            {
                result.ReferralsUpdated++;
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // OCCUPATIONS (PreOccupations)
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportOccupationsAsync(List<Dictionary<string, JsonElement>> rows)
    {
        var result = new PrimaryClinicMigratorResultDto();

        foreach (var row in rows)
        {
            var patientGuid = GetGuid(row, "PatientGUID");
            var clientId = await _context.Clients.Where(c => c.LegacyGuid == patientGuid).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (clientId == null) continue;

            var legacyGuid = GetGuid(row, "GUID");
            var existing = await _context.ClientOccupations.FirstOrDefaultAsync(o => o.LegacyGuid == legacyGuid);
            var occupation = existing ?? new ClientOccupation { ClientId = clientId.Value, LegacyGuid = legacyGuid };

            occupation.Occupation = GetString(row, "Occupation");
            occupation.Employer = GetString(row, "Employer");
            occupation.StartedYear = GetDateTime(row, "StartedYear")?.Year;
            occupation.StoppedYear = GetDateTime(row, "StoppedYear")?.Year;
            occupation.HasAsbestos = GetBool(row, "HasAsbestos");
            occupation.HasDust = GetBool(row, "HasDust");
            occupation.HasRadiation = GetBool(row, "HasRadiation");
            occupation.HasAnimals = GetBool(row, "HasAnimals");
            occupation.Comment = GetString(row, "Comment");
            occupation.UpdatedAt = DateTime.UtcNow;

            if (existing == null)
            {
                _context.ClientOccupations.Add(occupation);
                result.OccupationsCreated++;
            }
            else
            {
                result.OccupationsUpdated++;
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // FAMILY RELATIONSHIPS (FamilyRelationships) — requires the relative to already be imported
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportFamilyRelationshipsAsync(List<Dictionary<string, JsonElement>> rows)
    {
        var result = new PrimaryClinicMigratorResultDto();

        foreach (var row in rows)
        {
            var patientGuid = GetGuid(row, "PatientGUID");
            var relativeGuid = GetGuid(row, "RelativeGUID");
            var clientId = await _context.Clients.Where(c => c.LegacyGuid == patientGuid).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            var relativeClientId = await _context.Clients.Where(c => c.LegacyGuid == relativeGuid).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (clientId == null || relativeClientId == null) continue; // both sides must exist in this run

            var legacyGuid = GetGuid(row, "GUID");
            var existing = await _context.ClientFamilyRelationships.FirstOrDefaultAsync(r => r.LegacyGuid == legacyGuid);
            var relationship = existing ?? new ClientFamilyRelationship { ClientId = clientId.Value, RelativeClientId = relativeClientId.Value, LegacyGuid = legacyGuid };

            relationship.RelationshipType = GetString(row, "Relationship");

            if (existing == null)
            {
                _context.ClientFamilyRelationships.Add(relationship);
                result.FamilyRelationshipsCreated++;
            }
            else
            {
                result.FamilyRelationshipsUpdated++;
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // COMPENSATION CLAIMS (WorkcoverTACClaims, joined back to Contacts for employer/case manager names)
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportCompensationClaimsAsync(List<Dictionary<string, JsonElement>> rows)
    {
        var result = new PrimaryClinicMigratorResultDto();

        foreach (var row in rows)
        {
            var patientGuid = GetGuid(row, "PatientGUID");
            var clientId = await _context.Clients.Where(c => c.LegacyGuid == patientGuid).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (clientId == null) continue;

            var legacyGuid = GetGuid(row, "ClaimGUID");
            var existing = await _context.ClientCompensationClaims.FirstOrDefaultAsync(c => c.LegacyGuid == legacyGuid);
            var claim = existing ?? new ClientCompensationClaim { ClientId = clientId.Value, LegacyGuid = legacyGuid, ClaimNum = legacyGuid[..8] };

            claim.DateOfInjury = GetDateTime(row, "DateOfInjury");
            var employerName = string.Join(" ", new[] { GetString(row, "EmployerFirstName"), GetString(row, "EmployerSurname") }.Where(s => !string.IsNullOrWhiteSpace(s)));
            claim.EmployerName = string.IsNullOrWhiteSpace(employerName) ? null : employerName;
            var caseManagerName = string.Join(" ", new[] { GetString(row, "CaseManagerFirstName"), GetString(row, "CaseManagerSurname") }.Where(s => !string.IsNullOrWhiteSpace(s)));
            claim.CaseManagerName = string.IsNullOrWhiteSpace(caseManagerName) ? null : caseManagerName;
            claim.UpdatedAt = DateTime.UtcNow;

            if (existing == null)
            {
                _context.ClientCompensationClaims.Add(claim);
                result.CompensationClaimsCreated++;
            }
            else
            {
                result.CompensationClaimsUpdated++;
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // INVOICES (Invoices + AccountTypes)
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportInvoicesAsync(List<Dictionary<string, JsonElement>> rows)
    {
        var result = new PrimaryClinicMigratorResultDto();

        foreach (var row in rows)
        {
            var patientGuid = GetGuid(row, "PatientGUID");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.LegacyGuid == patientGuid);
            if (client == null) continue; // patient wasn't imported (e.g. soft-deleted) — skip rather than orphan the invoice

            var legacyGuid = GetGuid(row, "GUID");
            var existing = await _context.Invoices.FirstOrDefaultAsync(i => i.LegacyGuid == legacyGuid);
            var invoiceNum = GetInt(row, "InvoiceNum");
            var invoice = existing ?? new Invoice
            {
                ClientId = client.Id,
                ClinicId = client.ClinicId,
                LegacyGuid = legacyGuid,
                InvoiceNumber = invoiceNum.HasValue ? $"LEGACY-{invoiceNum.Value:D6}" : $"LEGACY-{legacyGuid[..8]}"
            };

            invoice.InvoiceDate = GetDateTime(row, "IssueDate") ?? invoice.InvoiceDate;
            invoice.DueDate = GetDateTime(row, "DueDate") ?? invoice.InvoiceDate;
            invoice.Notes = GetString(row, "PrivateNotes");
            invoice.AccountType = PrimaryClinicLegacyMapper.MapAccountType(GetString(row, "AccountTypeName"));
            invoice.UpdatedAt = DateTime.UtcNow;

            if (existing == null)
            {
                _context.Invoices.Add(invoice);
                result.InvoicesCreated++;
            }
            else
            {
                result.InvoicesUpdated++;
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // INVOICE ITEMS (InvoiceItems) — also recomputes each touched invoice's TotalAmount/PlaceOfService
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportInvoiceItemsAsync(List<Dictionary<string, JsonElement>> rows)
    {
        var result = new PrimaryClinicMigratorResultDto();
        var touchedInvoiceIds = new HashSet<int>();
        var hospitalInvoiceIds = new HashSet<int>();

        foreach (var row in rows)
        {
            var invoiceGuid = GetGuid(row, "InvoiceGUID");
            var invoiceId = await _context.Invoices.Where(i => i.LegacyGuid == invoiceGuid).Select(i => (int?)i.Id).FirstOrDefaultAsync();
            if (invoiceId == null) continue; // parent invoice wasn't imported (e.g. its patient was soft-deleted) — skip

            var legacyGuid = GetGuid(row, "GUID");
            var existing = await _context.InvoiceItems.FirstOrDefaultAsync(ii => ii.LegacyGuid == legacyGuid);
            var item = existing ?? new InvoiceItem { InvoiceId = invoiceId.Value, LegacyGuid = legacyGuid };

            var itemNum = GetString(row, "ItemNum");
            var mbsItemNum = GetString(row, "MBSItemNum");
            var description = GetString(row, "Description");
            item.Description = !string.IsNullOrWhiteSpace(description) ? description : (mbsItemNum ?? itemNum ?? "Legacy item");
            item.ServiceDate = GetDateTime(row, "ServiceDate");

            var quantity = GetDecimal(row, "Quantity") ?? 1m;
            item.Quantity = Math.Max(1, (int)Math.Round(quantity, MidpointRounding.AwayFromZero));

            item.UnitPrice = GetDecimal(row, "Fee") ?? 0m;
            item.Discount = GetDecimal(row, "Discount") ?? 0m;
            item.PercentGst = GetDecimal(row, "GST") ?? 0m;
            item.GstAmount = GetDecimal(row, "GSTAmount") ?? 0m;
            item.FeeIncludeGst = GetBool(row, "IsFeeGSTInclusive");

            // Legacy stores Rebate as the line's total rebate, not per-unit — normalise to fit our
            // RebatePerUnit-times-Quantity model (best-effort; not independently confirmed against
            // the legacy source, since both interpretations are plausible from the column name alone).
            var totalRebate = GetDecimal(row, "Rebate") ?? 0m;
            item.RebatePerUnit = item.Quantity > 0 ? totalRebate / item.Quantity : totalRebate;

            item.DerivedQuantity = GetInt(row, "NoOfPatientsSeen");

            // Try the standardised MBS code first, falling back to the free-text ItemNum.
            item.BillingItemId =
                await _context.BillingItems.Where(b => b.ItemNumber == (mbsItemNum ?? string.Empty)).Select(b => (int?)b.Id).FirstOrDefaultAsync() ??
                await _context.BillingItems.Where(b => b.ItemNumber == (itemNum ?? string.Empty)).Select(b => (int?)b.Id).FirstOrDefaultAsync();

            if (existing == null)
            {
                _context.InvoiceItems.Add(item);
                result.InvoiceItemsCreated++;
            }
            else
            {
                result.InvoiceItemsUpdated++;
            }

            touchedInvoiceIds.Add(invoiceId.Value);
            if (GetBool(row, "HospitalInd"))
                hospitalInvoiceIds.Add(invoiceId.Value);
        }

        await _context.SaveChangesAsync();

        // Now that every item exists, recompute each touched invoice's TotalAmount/PlaceOfService —
        // both are derived from the item set, matching how BillingCalculator computes them for
        // invoices created natively in IntelliMed (see InvoiceRepository).
        foreach (var invoiceId in touchedInvoiceIds)
        {
            var invoice = await _context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == invoiceId);
            invoice.TotalAmount = Math.Round(invoice.Items.Sum(i => i.TotalPrice), 2);
            invoice.PlaceOfService = PrimaryClinicLegacyMapper.MapPlaceOfService(hospitalInvoiceIds.Contains(invoiceId));
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // PAYMENTS (Receipts + ReceiptPayments + BillingAllocations)
    // =========================================================================

    public async Task<PrimaryClinicMigratorResultDto> ImportPaymentsAsync(
        List<Dictionary<string, JsonElement>> allocationRows,
        List<Dictionary<string, JsonElement>> paymentRows)
    {
        var result = new PrimaryClinicMigratorResultDto();

        // allocationRows is pre-grouped by (ReceiptGUID, InvoiceGUID) with a summed AllocatedAmount —
        // this tells us which invoice(s) each receipt applies to and how much. In practice a receipt
        // almost always covers a single invoice, but multi-invoice receipts are handled by pro-rating
        // each tender line's amount across the invoices it touched.
        var invoicesByReceipt = new Dictionary<string, List<(string InvoiceGuid, decimal Amount)>>();
        foreach (var row in allocationRows)
        {
            var receiptGuid = GetGuid(row, "ReceiptGUID");
            var invoiceGuid = GetGuid(row, "InvoiceGUID");
            var amount = GetDecimal(row, "AllocatedAmount") ?? 0m;
            if (!invoicesByReceipt.TryGetValue(receiptGuid, out var list))
                invoicesByReceipt[receiptGuid] = list = new List<(string, decimal)>();
            list.Add((invoiceGuid, amount));
        }

        var touchedInvoiceIds = new HashSet<int>();

        foreach (var row in paymentRows)
        {
            var receiptPaymentGuid = GetGuid(row, "GUID");
            var receiptGuid = GetGuid(row, "ReceiptGUID");
            var amount = GetDecimal(row, "Amount") ?? 0m;
            var method = PrimaryClinicLegacyMapper.MapPaymentMethod(GetString(row, "PaymentTypeName"));

            if (!invoicesByReceipt.TryGetValue(receiptGuid, out var invoiceAllocations) || invoiceAllocations.Count == 0)
                continue; // this receipt's allocations don't resolve to any invoice item — nothing to attribute the payment to

            var totalAllocated = invoiceAllocations.Sum(a => a.Amount);

            foreach (var (invoiceGuid, allocatedAmount) in invoiceAllocations)
            {
                var invoiceId = await _context.Invoices.Where(i => i.LegacyGuid == invoiceGuid).Select(i => (int?)i.Id).FirstOrDefaultAsync();
                if (invoiceId == null) continue; // invoice wasn't imported — skip rather than orphan the payment

                // Single-invoice receipts (the overwhelming common case) get the full tender amount;
                // multi-invoice receipts pro-rate by each invoice's share of the total allocated.
                var paymentAmount = invoiceAllocations.Count == 1 || totalAllocated == 0
                    ? amount
                    : Math.Round(amount * (allocatedAmount / totalAllocated), 2);

                var legacyGuid = $"{receiptPaymentGuid}:{invoiceGuid}";
                var existing = await _context.Payments.FirstOrDefaultAsync(p => p.LegacyGuid == legacyGuid);
                var payment = existing ?? new Payment { InvoiceId = invoiceId.Value, LegacyGuid = legacyGuid };

                payment.Amount = paymentAmount;
                payment.Method = method;
                payment.PaymentDate = GetDateTime(row, "IssueDate") ?? payment.PaymentDate;

                if (existing == null)
                {
                    _context.Payments.Add(payment);
                    result.PaymentsCreated++;
                }
                else
                {
                    result.PaymentsUpdated++;
                }

                touchedInvoiceIds.Add(invoiceId.Value);
            }
        }

        await _context.SaveChangesAsync();

        // Recompute each touched invoice's AmountPaid/Status now that its payments exist, matching
        // how InvoiceRepository.RecordPaymentAsync derives Status from AmountPaid vs TotalAmount.
        foreach (var invoiceId in touchedInvoiceIds)
        {
            var invoice = await _context.Invoices.Include(i => i.Payments).FirstAsync(i => i.Id == invoiceId);
            invoice.AmountPaid = Math.Round(invoice.Payments.Sum(p => p.Amount), 2);
            invoice.Status = invoice.TotalAmount > 0 && invoice.AmountPaid >= invoice.TotalAmount
                ? InvoiceStatus.Paid
                : invoice.AmountPaid > 0
                    ? InvoiceStatus.PartiallyPaid
                    : InvoiceStatus.Sent;
        }

        await _context.SaveChangesAsync();
        return result;
    }

    // =========================================================================
    // ROW-READING HELPERS
    // =========================================================================

    private static string? GetString(Dictionary<string, JsonElement> row, string key)
    {
        return row.TryGetValue(key, out var v) && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? v.GetString() : null;
    }

    private static bool GetBool(Dictionary<string, JsonElement> row, string key)
    {
        return row.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.True;
    }

    private static DateTime? GetDateTime(Dictionary<string, JsonElement> row, string key)
    {
        return row.TryGetValue(key, out var v) && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? v.GetDateTime() : null;
    }

    private static int? GetInt(Dictionary<string, JsonElement> row, string key)
    {
        return row.TryGetValue(key, out var v) && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? v.GetInt32() : null;
    }

    private static decimal? GetDecimal(Dictionary<string, JsonElement> row, string key)
    {
        return row.TryGetValue(key, out var v) && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? v.GetDecimal() : null;
    }

    /// <summary>Reads a non-nullable GUID column and returns it as a string — the direct form every
    /// LegacyGuid comparison in this class needs.</summary>
    private static string GetGuid(Dictionary<string, JsonElement> row, string key)
    {
        return row[key].GetGuid().ToString();
    }
}
