using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ReceiptRepositoryTests : IDisposable
{
    private readonly ReceiptRepository _repository;
    private readonly AppDbContext _context;

    public ReceiptRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ReceiptRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<Client> SeedClientAsync(int? payerClientId = null)
    {
        var client = new Client
        {
            ClinicId = 1,
            FirstName = "Test",
            LastName = "Client",
            Email = "test@example.com",
            DateOfBirth = new DateTime(1990, 1, 1),
            IsActive = true,
            PayerClientId = payerClientId
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return client;
    }

    private async Task<Invoice> SeedInvoiceAsync(int clientId, decimal totalAmount, string invoiceNumber = "INV-001")
    {
        var invoice = new Invoice
        {
            ClinicId = 1,
            ClientId = clientId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Draft,
            TotalAmount = totalAmount
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task CreateAsync_SingleInvoiceSingleTender_UpdatesInvoiceAmountPaid()
    {
        var client = await SeedClientAsync();
        var invoice = await SeedInvoiceAsync(client.Id, 200m);

        var dto = new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 100m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoice.Id, Amount = 100m } }
        };

        await _repository.CreateAsync(dto);

        var updated = await _context.Invoices.FindAsync(invoice.Id);
        updated!.AmountPaid.Should().Be(100m);
        updated.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task CreateAsync_FullPayment_SetsInvoiceStatusPaid()
    {
        var client = await SeedClientAsync();
        var invoice = await SeedInvoiceAsync(client.Id, 200m);

        var dto = new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 200m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoice.Id, Amount = 200m } }
        };

        await _repository.CreateAsync(dto);

        var updated = await _context.Invoices.FindAsync(invoice.Id);
        updated!.Status.Should().Be(InvoiceStatus.Paid);
        updated.AmountOwing.Should().Be(0m);
    }

    [Fact]
    public async Task CreateAsync_MultipleInvoicesSamePayer_AllocatesAcrossBoth()
    {
        var client = await SeedClientAsync();
        var invoiceA = await SeedInvoiceAsync(client.Id, 100m, "INV-A");
        var invoiceB = await SeedInvoiceAsync(client.Id, 50m, "INV-B");

        var dto = new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 150m, Method = PaymentMethod.Eftpos } },
            Allocations =
            {
                new CreateReceiptAllocationDto { InvoiceId = invoiceA.Id, Amount = 100m },
                new CreateReceiptAllocationDto { InvoiceId = invoiceB.Id, Amount = 50m }
            }
        };

        var receiptId = await _repository.CreateAsync(dto);

        (await _context.Invoices.FindAsync(invoiceA.Id))!.Status.Should().Be(InvoiceStatus.Paid);
        (await _context.Invoices.FindAsync(invoiceB.Id))!.Status.Should().Be(InvoiceStatus.Paid);
        (await _context.ReceiptAllocations.CountAsync(a => a.ReceiptId == receiptId)).Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_SplitTender_CreatesTwoReceiptPaymentsOneAllocation()
    {
        var client = await SeedClientAsync();
        var invoice = await SeedInvoiceAsync(client.Id, 100m);

        var dto = new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments =
            {
                new CreateReceiptTenderDto { Amount = 60m, Method = PaymentMethod.Cash },
                new CreateReceiptTenderDto { Amount = 40m, Method = PaymentMethod.CreditCard }
            },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoice.Id, Amount = 100m } }
        };

        var receiptId = await _repository.CreateAsync(dto);

        (await _context.ReceiptPayments.CountAsync(p => p.ReceiptId == receiptId)).Should().Be(2);
        (await _context.ReceiptAllocations.CountAsync(a => a.ReceiptId == receiptId)).Should().Be(1);
        (await _context.Invoices.FindAsync(invoice.Id))!.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task CreateAsync_Overpayment_KeepAsCreditTrue_CreatesCreditAllocationWithNullInvoiceAndItem()
    {
        var client = await SeedClientAsync();
        var invoice = await SeedInvoiceAsync(client.Id, 100m);

        var dto = new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 120m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoice.Id, Amount = 100m } },
            KeepOverpaymentAsCredit = true
        };

        var receiptId = await _repository.CreateAsync(dto);

        var creditAllocation = await _context.ReceiptAllocations.SingleAsync(a => a.ReceiptId == receiptId && a.AllocationType == AllocationType.Credit);
        creditAllocation.Amount.Should().Be(20m);
        creditAllocation.InvoiceId.Should().BeNull();
        creditAllocation.InvoiceItemId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Overpayment_KeepAsCreditFalse_Throws()
    {
        var client = await SeedClientAsync();
        var invoice = await SeedInvoiceAsync(client.Id, 100m);

        var dto = new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 120m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoice.Id, Amount = 100m } },
            KeepOverpaymentAsCredit = false
        };

        var act = async () => await _repository.CreateAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_TenderedLessThanAllocated_Throws()
    {
        var client = await SeedClientAsync();
        var invoice = await SeedInvoiceAsync(client.Id, 100m);

        var dto = new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 50m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoice.Id, Amount = 100m } }
        };

        var act = async () => await _repository.CreateAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_ItemLevelAllocation_PersistsInvoiceItemId()
    {
        var client = await SeedClientAsync();
        var invoice = await SeedInvoiceAsync(client.Id, 100m);
        var item = new InvoiceItem { InvoiceId = invoice.Id, Description = "Consultation", Quantity = 1, UnitPrice = 100m };
        _context.InvoiceItems.Add(item);
        await _context.SaveChangesAsync();

        var dto = new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 100m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoice.Id, InvoiceItemId = item.Id, Amount = 100m } }
        };

        var receiptId = await _repository.CreateAsync(dto);

        var allocation = await _context.ReceiptAllocations.SingleAsync(a => a.ReceiptId == receiptId);
        allocation.InvoiceItemId.Should().Be(item.Id);
    }

    [Fact]
    public async Task GetOutstandingInvoicesForPayerAsync_ReturnsDependentsInvoicesForThePayer()
    {
        var payer = await SeedClientAsync();
        var dependent = await SeedClientAsync(payerClientId: payer.Id);
        var invoice = await SeedInvoiceAsync(dependent.Id, 100m);

        var result = await _repository.GetOutstandingInvoicesForPayerAsync(1, payer.Id);

        result.Should().ContainSingle(i => i.InvoiceId == invoice.Id);
    }

    [Fact]
    public async Task GetOutstandingInvoicesForPayerAsync_ExcludesCancelledFullyPaidAndExcludedInvoice()
    {
        var client = await SeedClientAsync();
        var outstanding = await SeedInvoiceAsync(client.Id, 100m, "INV-OUTSTANDING");
        var cancelled = await SeedInvoiceAsync(client.Id, 100m, "INV-CANCELLED");
        cancelled.Status = InvoiceStatus.Cancelled;
        var fullyPaid = await SeedInvoiceAsync(client.Id, 100m, "INV-PAID");
        fullyPaid.AmountPaid = 100m;
        var excluded = await SeedInvoiceAsync(client.Id, 100m, "INV-EXCLUDED");
        await _context.SaveChangesAsync();

        var result = await _repository.GetOutstandingInvoicesForPayerAsync(1, client.Id, excludeInvoiceId: excluded.Id);

        result.Should().ContainSingle(i => i.InvoiceId == outstanding.Id);
    }
}
