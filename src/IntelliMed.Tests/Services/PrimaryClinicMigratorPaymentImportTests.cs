using System.Text.Json;
using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Services;

/// <summary>
/// Direct coverage of PrimaryClinicMigratorService.ImportPaymentsAsync's rewritten behavior: it now
/// groups legacy payment/allocation rows by ReceiptGUID into one Receipt (with its ReceiptPayment
/// tenders and ReceiptAllocation settlements) instead of one flat Payment row per tender-invoice pair.
/// </summary>
public class PrimaryClinicMigratorPaymentImportTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly PrimaryClinicMigratorService _service;

    public PrimaryClinicMigratorPaymentImportTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _service = new PrimaryClinicMigratorService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static Dictionary<string, JsonElement> Row(object values)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(values));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private async Task<(Client Client, Invoice InvoiceA, Invoice InvoiceB)> SeedClientAndTwoInvoicesAsync()
    {
        var client = new Client { ClinicId = 1, FirstName = "Test", LastName = "Payer", DateOfBirth = new DateTime(1980, 1, 1), LegacyGuid = "client-1" };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoiceA = new Invoice { ClinicId = 1, ClientId = client.Id, InvoiceNumber = "LEGACY-A", TotalAmount = 50m, LegacyGuid = "88888888-8888-8888-8888-888888888888" };
        var invoiceB = new Invoice { ClinicId = 1, ClientId = client.Id, InvoiceNumber = "LEGACY-B", TotalAmount = 75m, LegacyGuid = "99999999-9999-9999-9999-999999999999" };
        _context.Invoices.AddRange(invoiceA, invoiceB);
        await _context.SaveChangesAsync();

        return (client, invoiceA, invoiceB);
    }

    [Fact]
    public async Task ImportPaymentsAsync_SingleReceiptAcrossTwoInvoices_CreatesOneReceiptWithTwoAllocations()
    {
        var (client, invoiceA, invoiceB) = await SeedClientAndTwoInvoicesAsync();
        const string receiptGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

        var allocationRows = new List<Dictionary<string, JsonElement>>
        {
            Row(new { ReceiptGUID = receiptGuid, InvoiceGUID = invoiceA.LegacyGuid, AllocatedAmount = 20m }),
            Row(new { ReceiptGUID = receiptGuid, InvoiceGUID = invoiceB.LegacyGuid, AllocatedAmount = 30m })
        };
        var paymentRows = new List<Dictionary<string, JsonElement>>
        {
            Row(new { GUID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", ReceiptGUID = receiptGuid, Amount = 50m, PaymentTypeName = "Cash", IssueDate = new DateTime(2026, 1, 15) })
        };

        var result = await _service.ImportPaymentsAsync(allocationRows, paymentRows);

        result.PaymentsCreated.Should().Be(1);
        (await _context.Receipts.CountAsync()).Should().Be(1);
        (await _context.ReceiptPayments.CountAsync()).Should().Be(1);
        (await _context.ReceiptAllocations.CountAsync()).Should().Be(2);

        var receipt = await _context.Receipts.Include(r => r.Payments).Include(r => r.Allocations).FirstAsync(r => r.LegacyGuid == receiptGuid);
        receipt.PayerClientId.Should().Be(client.Id);
        receipt.ClinicId.Should().Be(1);
        receipt.Payments.Single().Amount.Should().Be(50m);
        receipt.Payments.Single().Method.Should().Be(PaymentMethod.Cash);
        receipt.Allocations.Should().Contain(a => a.InvoiceId == invoiceA.Id && a.Amount == 20m);
        receipt.Allocations.Should().Contain(a => a.InvoiceId == invoiceB.Id && a.Amount == 30m);

        (await _context.Invoices.FindAsync(invoiceA.Id))!.AmountPaid.Should().Be(20m);
        (await _context.Invoices.FindAsync(invoiceB.Id))!.AmountPaid.Should().Be(30m);
        (await _context.Invoices.FindAsync(invoiceA.Id))!.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task ImportPaymentsAsync_ReimportingSameReceipt_UpdatesInPlace_DoesNotDuplicate()
    {
        var (_, invoiceA, invoiceB) = await SeedClientAndTwoInvoicesAsync();
        const string receiptGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

        var allocationRows = new List<Dictionary<string, JsonElement>>
        {
            Row(new { ReceiptGUID = receiptGuid, InvoiceGUID = invoiceA.LegacyGuid, AllocatedAmount = 20m }),
            Row(new { ReceiptGUID = receiptGuid, InvoiceGUID = invoiceB.LegacyGuid, AllocatedAmount = 30m })
        };
        var paymentRows = new List<Dictionary<string, JsonElement>>
        {
            Row(new { GUID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", ReceiptGUID = receiptGuid, Amount = 50m, PaymentTypeName = "Cash", IssueDate = new DateTime(2026, 1, 15) })
        };

        await _service.ImportPaymentsAsync(allocationRows, paymentRows);
        var second = await _service.ImportPaymentsAsync(allocationRows, paymentRows);

        second.PaymentsUpdated.Should().Be(1);
        (await _context.Receipts.CountAsync()).Should().Be(1);
        (await _context.ReceiptPayments.CountAsync()).Should().Be(1);
        (await _context.ReceiptAllocations.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ImportPaymentsAsync_OneInvoiceNotImported_SkipsThatAllocationOnly_KeepsTheOther()
    {
        var (_, invoiceA, _) = await SeedClientAndTwoInvoicesAsync();
        const string receiptGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

        var allocationRows = new List<Dictionary<string, JsonElement>>
        {
            Row(new { ReceiptGUID = receiptGuid, InvoiceGUID = invoiceA.LegacyGuid, AllocatedAmount = 20m }),
            Row(new { ReceiptGUID = receiptGuid, InvoiceGUID = "cccccccc-cccc-cccc-cccc-cccccccccccc", AllocatedAmount = 30m }) // never imported
        };
        var paymentRows = new List<Dictionary<string, JsonElement>>
        {
            Row(new { GUID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", ReceiptGUID = receiptGuid, Amount = 50m, PaymentTypeName = "Cash", IssueDate = new DateTime(2026, 1, 15) })
        };

        await _service.ImportPaymentsAsync(allocationRows, paymentRows);

        (await _context.ReceiptAllocations.CountAsync()).Should().Be(1);
        (await _context.ReceiptAllocations.SingleAsync()).InvoiceId.Should().Be(invoiceA.Id);
    }
}
