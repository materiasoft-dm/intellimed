using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class InvoiceRepositoryTests : IDisposable
{
    private readonly InvoiceRepository _repository;
    private readonly AppDbContext _context;

    public InvoiceRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new InvoiceRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewInvoiceId()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Test",
            LastName = "Client",
            Email = "test@example.com",
            IsActive = true
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var dto = new CreateInvoiceDto
        {
            ClientId = client.Id,
            DueDate = DateTime.Today.AddDays(30),
            Notes = "Test invoice",
            Items = new List<CreateInvoiceItemDto>
            {
                new() { Description = "Consultation", Quantity = 1, UnitPrice = 150.00m },
                new() { Description = "Procedure", Quantity = 2, UnitPrice = 75.00m }
            }
        };

        // Act
        var result = await _repository.CreateAsync(dto);

        // Assert
        result.Should().BeGreaterThan(0);
        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == result);
        invoice.Should().NotBeNull();
        invoice!.ClientId.Should().Be(client.Id);
        invoice.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingInvoice_ReturnsInvoiceDto()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Test",
            LastName = "Client",
            Email = "test@example.com",
            IsActive = true
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClientId = client.Id,
            InvoiceNumber = "INV-001",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Draft,
            TotalAmount = 200.00m
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(invoice.Id);

        // Assert
        result.Should().NotBeNull();
        result!.InvoiceNumber.Should().Be("INV-001");
        result.TotalAmount.Should().Be(200.00m);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingInvoice_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_WithClientIdFilter_ReturnsMatchingInvoices()
    {
        // Arrange
        var client1 = new Client { FirstName = "Client", LastName = "One", Email = "p1@example.com", IsActive = true };
        var client2 = new Client { FirstName = "Client", LastName = "Two", Email = "p2@example.com", IsActive = true };
        _context.Clients.AddRange(client1, client2);
        await _context.SaveChangesAsync();

        var invoices = new[]
        {
            new Invoice { ClientId = client1.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Draft, TotalAmount = 100.00m },
            new Invoice { ClientId = client2.Id, InvoiceNumber = "INV-002", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Draft, TotalAmount = 200.00m },
            new Invoice { ClientId = client1.Id, InvoiceNumber = "INV-003", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Draft, TotalAmount = 150.00m }
        };
        _context.Invoices.AddRange(invoices);
        await _context.SaveChangesAsync();

        var search = new InvoiceSearchDto { ClientId = client1.Id };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.ClientId == client1.Id);
    }

    [Fact]
    public async Task SearchAsync_WithStatusFilter_ReturnsMatchingInvoices()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoices = new[]
        {
            new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Draft, TotalAmount = 100.00m },
            new Invoice { ClientId = client.Id, InvoiceNumber = "INV-002", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Paid, TotalAmount = 200.00m },
            new Invoice { ClientId = client.Id, InvoiceNumber = "INV-003", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Overdue, TotalAmount = 150.00m }
        };
        _context.Invoices.AddRange(invoices);
        await _context.SaveChangesAsync();

        var search = new InvoiceSearchDto { Status = InvoiceStatus.Paid };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task SearchAsync_WithDateRange_ReturnsInvoicesInRange()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoices = new[]
        {
            new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today.AddDays(-10), DueDate = DateTime.Today.AddDays(20), Status = InvoiceStatus.Draft, TotalAmount = 100.00m },
            new Invoice { ClientId = client.Id, InvoiceNumber = "INV-002", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Draft, TotalAmount = 200.00m },
            new Invoice { ClientId = client.Id, InvoiceNumber = "INV-003", InvoiceDate = DateTime.Today.AddDays(10), DueDate = DateTime.Today.AddDays(40), Status = InvoiceStatus.Draft, TotalAmount = 150.00m }
        };
        _context.Invoices.AddRange(invoices);
        await _context.SaveChangesAsync();

        var search = new InvoiceSearchDto
        {
            FromDate = DateTime.Today.AddDays(-5),
            ToDate = DateTime.Today.AddDays(5)
        };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].InvoiceNumber.Should().Be("INV-002");
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        for (int i = 1; i <= 25; i++)
        {
            _context.Invoices.Add(new Invoice
            {
                ClientId = client.Id,
                InvoiceNumber = $"INV-{i:D3}",
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                Status = InvoiceStatus.Draft,
                TotalAmount = 100.00m * i
            });
        }
        await _context.SaveChangesAsync();

        var search = new InvoiceSearchDto { Page = 2, PageSize = 10 };

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(search);

        // Assert
        totalCount.Should().Be(25);
        items.Should().HaveCount(10);
    }

    [Fact]
    public async Task AddPaymentAsync_UpdatesInvoiceAmount()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClientId = client.Id,
            InvoiceNumber = "INV-001",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Draft,
            TotalAmount = 200.00m,
            AmountPaid = 0
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var paymentDto = new CreatePaymentDto
        {
            InvoiceId = invoice.Id,
            Amount = 100.00m,
            Method = PaymentMethod.CreditCard,
            PaymentDate = DateTime.Today
        };

        // Act
        await _repository.AddPaymentAsync(invoice.Id, paymentDto);

        // Assert
        var updatedInvoice = await _context.Invoices.FindAsync(invoice.Id);
        updatedInvoice!.AmountPaid.Should().Be(100.00m);
        updatedInvoice.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task AddPaymentAsync_FullPayment_UpdatesStatusToPaid()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClientId = client.Id,
            InvoiceNumber = "INV-001",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Draft,
            TotalAmount = 200.00m,
            AmountPaid = 0
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var paymentDto = new CreatePaymentDto
        {
            InvoiceId = invoice.Id,
            Amount = 200.00m,
            Method = PaymentMethod.CreditCard,
            PaymentDate = DateTime.Today
        };

        // Act
        await _repository.AddPaymentAsync(invoice.Id, paymentDto);

        // Assert
        var updatedInvoice = await _context.Invoices.FindAsync(invoice.Id);
        updatedInvoice!.AmountPaid.Should().Be(200.00m);
        updatedInvoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task DeleteAsync_RemovesInvoice()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClientId = client.Id,
            InvoiceNumber = "INV-001",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Draft,
            TotalAmount = 100.00m
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        var invoiceId = invoice.Id;

        // Act
        await _repository.DeleteAsync(invoiceId);

        // Assert
        var deleted = await _context.Invoices.FindAsync(invoiceId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingInvoice_ReturnsTrue()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClientId = client.Id,
            InvoiceNumber = "INV-001",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Draft,
            TotalAmount = 100.00m
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsAsync(invoice.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingInvoice_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(999);

        // Assert
        result.Should().BeFalse();
    }
}