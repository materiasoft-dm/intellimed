using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Services;

/// <summary>
/// Verifies the LegacyGuid-based upsert pattern PrimaryClinicMigratorService uses for every entity
/// (find existing by LegacyGuid, else create) is actually idempotent — re-importing the same legacy
/// row updates the existing IntelliMed record instead of creating a duplicate. The real SQL Server
/// restore/read can't be exercised in a unit test, so this drives the same find-or-create logic
/// directly against an in-memory AppDbContext.
/// </summary>
public class PrimaryClinicMigratorIdempotencyTests : IDisposable
{
    private readonly AppDbContext _context;

    public PrimaryClinicMigratorIdempotencyTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<Client> UpsertClientByLegacyGuidAsync(string legacyGuid, string firstName)
    {
        var existing = await _context.Clients.FirstOrDefaultAsync(c => c.LegacyGuid == legacyGuid);
        var client = existing ?? new Client { ClinicId = 1, LegacyGuid = legacyGuid };
        client.FirstName = firstName;
        client.LastName = "Test";
        client.DateOfBirth = new DateTime(1980, 1, 1);

        if (existing == null)
            _context.Clients.Add(client);

        await _context.SaveChangesAsync();
        return client;
    }

    [Fact]
    public async Task ReimportingSameLegacyGuid_UpdatesExistingRow_DoesNotDuplicate()
    {
        var first = await UpsertClientByLegacyGuidAsync("11111111-1111-1111-1111-111111111111", "Original Name");
        var second = await UpsertClientByLegacyGuidAsync("11111111-1111-1111-1111-111111111111", "Updated Name");

        second.Id.Should().Be(first.Id);
        (await _context.Clients.CountAsync(c => c.LegacyGuid == "11111111-1111-1111-1111-111111111111")).Should().Be(1);
        (await _context.Clients.FindAsync(first.Id))!.FirstName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DifferentLegacyGuids_CreateSeparateRows()
    {
        await UpsertClientByLegacyGuidAsync("22222222-2222-2222-2222-222222222222", "Patient A");
        await UpsertClientByLegacyGuidAsync("33333333-3333-3333-3333-333333333333", "Patient B");

        (await _context.Clients.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task NativelyCreatedClient_WithNullLegacyGuid_IsNeverMatchedByLookup()
    {
        _context.Clients.Add(new Client { ClinicId = 1, FirstName = "Native", LastName = "Client", DateOfBirth = new DateTime(1990, 1, 1), LegacyGuid = null });
        await _context.SaveChangesAsync();

        var matched = await _context.Clients.FirstOrDefaultAsync(c => c.LegacyGuid == "44444444-4444-4444-4444-444444444444");

        matched.Should().BeNull();
    }

    private async Task<Invoice> UpsertInvoiceByLegacyGuidAsync(int clientId, string legacyGuid, decimal totalAmount)
    {
        var existing = await _context.Invoices.FirstOrDefaultAsync(i => i.LegacyGuid == legacyGuid);
        var invoice = existing ?? new Invoice { ClinicId = 1, ClientId = clientId, LegacyGuid = legacyGuid, InvoiceNumber = $"LEGACY-{legacyGuid[..6]}" };
        invoice.TotalAmount = totalAmount;

        if (existing == null)
            _context.Invoices.Add(invoice);

        await _context.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task ReimportingSameInvoiceLegacyGuid_UpdatesExistingRow_DoesNotDuplicate()
    {
        var client = await UpsertClientByLegacyGuidAsync("55555555-5555-5555-5555-555555555555", "Invoice Owner");

        var first = await UpsertInvoiceByLegacyGuidAsync(client.Id, "66666666-6666-6666-6666-666666666666", 100m);
        var second = await UpsertInvoiceByLegacyGuidAsync(client.Id, "66666666-6666-6666-6666-666666666666", 150m);

        second.Id.Should().Be(first.Id);
        (await _context.Invoices.CountAsync(i => i.LegacyGuid == "66666666-6666-6666-6666-666666666666")).Should().Be(1);
        (await _context.Invoices.FindAsync(first.Id))!.TotalAmount.Should().Be(150m);
    }

    [Fact]
    public async Task PaymentLegacyGuid_IsAReceiptInvoiceCompositeKey_NotJustTheReceiptGuid()
    {
        // A single legacy receipt can pay off multiple invoices, so PrimaryClinicMigratorService keys
        // each generated Payment row on "{ReceiptPaymentGuid}:{InvoiceLegacyGuid}", not the receipt
        // payment's own GUID alone — otherwise the second invoice's payment would collide with the first.
        var client = await UpsertClientByLegacyGuidAsync("77777777-7777-7777-7777-777777777777", "Payer");
        var invoiceA = await UpsertInvoiceByLegacyGuidAsync(client.Id, "88888888-8888-8888-8888-888888888888", 50m);
        var invoiceB = await UpsertInvoiceByLegacyGuidAsync(client.Id, "99999999-9999-9999-9999-999999999999", 75m);

        const string receiptPaymentGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        _context.Payments.Add(new Payment { InvoiceId = invoiceA.Id, Amount = 20m, LegacyGuid = $"{receiptPaymentGuid}:88888888-8888-8888-8888-888888888888" });
        _context.Payments.Add(new Payment { InvoiceId = invoiceB.Id, Amount = 30m, LegacyGuid = $"{receiptPaymentGuid}:99999999-9999-9999-9999-999999999999" });
        await _context.SaveChangesAsync();

        (await _context.Payments.CountAsync()).Should().Be(2);
    }
}
