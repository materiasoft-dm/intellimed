using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class InvoiceRepositoryTests : IDisposable
{
    private readonly InvoiceRepository _repository;
    private readonly ReceiptRepository _receiptRepository;
    private readonly AppDbContext _context;

    public InvoiceRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _receiptRepository = new ReceiptRepository(_context);
        _repository = new InvoiceRepository(_context, new BillingCalculator(_context), new DerivedFeeCalculator(_context), new MultipleOperationRuleCalculator(_context), _receiptRepository);
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
    public async Task CreateAsync_TwoLinesWithDifferentFeeScheduleOverrides_EachResolvesAgainstItsOwnSchedule()
    {
        // Mirrors the legacy per-line "Schedule" column: one invoice, two lines, each billed
        // against a different fee schedule regardless of the invoice's account-type mapping.
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);

        var itemA = new BillingItem { ItemNumber = "104", Description = "Item A", ScheduleFee = 999m, IsActive = true };
        var itemB = new BillingItem { ItemNumber = "105", Description = "Item B", ScheduleFee = 999m, IsActive = true };
        _context.BillingItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var scheduleBbgp = new FeeSchedule { Code = "BBGP", Description = "Bulk Bill GP", RoundingType = RoundingTypeEnum.Exact };
        var scheduleBbo = new FeeSchedule { Code = "BBO", Description = "Bulk Bill Specialist", RoundingType = RoundingTypeEnum.Exact };
        _context.FeeSchedules.AddRange(scheduleBbgp, scheduleBbo);
        await _context.SaveChangesAsync();

        _context.FeeScheduleItems.AddRange(
            new FeeScheduleItem { FeeScheduleId = scheduleBbgp.Id, BillingItemId = itemA.Id, Fee = 76.80m },
            new FeeScheduleItem { FeeScheduleId = scheduleBbo.Id, BillingItemId = itemB.Id, Fee = 38.60m });
        await _context.SaveChangesAsync();

        // BulkBill is used because its Fee=Rebate resolution is authoritatively recomputed server-side
        // from the schedule in CreateAsync; UnitPrice itself is always trusted from what the client
        // submits (already resolved client-side via the same per-line override through the live
        // /resolve-line preview — see AddInvoice.razor's ResolveLineAsync), so it isn't the field that
        // proves server-side per-line schedule resolution.
        var dto = new CreateInvoiceDto
        {
            ClientId = client.Id,
            AccountType = AccountTypeEnum.BulkBill,
            DueDate = DateTime.Today.AddDays(30),
            Items = new List<CreateInvoiceItemDto>
            {
                new() { BillingItemId = itemA.Id, Description = "Item A", Quantity = 1, UnitPrice = 0, FeeScheduleId = scheduleBbgp.Id },
                new() { BillingItemId = itemB.Id, Description = "Item B", Quantity = 1, UnitPrice = 0, FeeScheduleId = scheduleBbo.Id }
            }
        };

        var invoiceId = await _repository.CreateAsync(dto);

        var invoice = await _context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == invoiceId);
        invoice.Items.Single(i => i.BillingItemId == itemA.Id).RebatePerUnit.Should().Be(76.80m);
        invoice.Items.Single(i => i.BillingItemId == itemB.Id).RebatePerUnit.Should().Be(38.60m);
    }

    [Fact]
    public async Task CreateAsync_WithDerivedItemLine_AppliesDerivedFeeBeforeTotal()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);

        var primaryItem = new BillingItem { ItemNumber = "30571", Description = "Primary surgical procedure", ScheduleFee = 500m, Benefit100 = 500m, IsActive = true };
        var assistantItem = new BillingItem { ItemNumber = "51300", Description = "Assistant at surgery", ScheduleFee = 0m, Benefit100 = 0m, IsActive = true };
        _context.BillingItems.AddRange(primaryItem, assistantItem);
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = assistantItem.Id,
            CalculationType = DerivedCalculationType.PercentageOfAssociatedItem,
            AssociatedBillingItemId = primaryItem.Id,
            Percentage = 20m
        });
        await _context.SaveChangesAsync();

        var dto = new CreateInvoiceDto
        {
            ClientId = client.Id,
            AccountType = AccountTypeEnum.BulkBill,
            DueDate = DateTime.Today.AddDays(30),
            Items = new List<CreateInvoiceItemDto>
            {
                new() { BillingItemId = primaryItem.Id, Description = "Primary", Quantity = 1, UnitPrice = 500m },
                // UnitPrice here is a deliberately wrong placeholder — the derived pass must overwrite it.
                new() { BillingItemId = assistantItem.Id, Description = "Assistant", Quantity = 1, UnitPrice = 999m }
            }
        };

        var invoiceId = await _repository.CreateAsync(dto);

        var invoice = await _context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == invoiceId);
        var assistantLine = invoice.Items.Single(i => i.BillingItemId == assistantItem.Id);

        assistantLine.UnitPrice.Should().Be(100m); // 20% of the primary line's $500
        invoice.TotalAmount.Should().Be(600m); // 500 (primary) + 100 (derived), not 500 + 999
    }

    [Fact]
    public async Task CreateAsync_MultipleOperationRuleAbatesSecondProcedure_BeforeAssistantFeeIsDerived()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);

        // Two Group T8 (Operations) items on the same occasion: the multiple operation rule must
        // abate the lower-fee one to 50% before the assistant fee is derived from it.
        var primaryProcedure = new BillingItem { ItemNumber = "30001", Description = "Primary op", Group = "T8", ScheduleFee = 600m, Benefit100 = 600m, IsActive = true };
        var secondaryProcedure = new BillingItem { ItemNumber = "30002", Description = "Secondary op", Group = "T8", ScheduleFee = 400m, Benefit100 = 400m, IsActive = true };
        var assistantItem = new BillingItem { ItemNumber = "51300", Description = "Assistant at surgery", Group = "T9", ScheduleFee = 0m, Benefit100 = 0m, IsActive = true };
        _context.BillingItems.AddRange(primaryProcedure, secondaryProcedure, assistantItem);
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = assistantItem.Id,
            CalculationType = DerivedCalculationType.PercentageOfAssociatedItem,
            AssociatedBillingItemId = secondaryProcedure.Id,
            Percentage = 20m
        });
        await _context.SaveChangesAsync();

        var dto = new CreateInvoiceDto
        {
            ClientId = client.Id,
            AccountType = AccountTypeEnum.BulkBill,
            DueDate = DateTime.Today.AddDays(30),
            Items = new List<CreateInvoiceItemDto>
            {
                new() { BillingItemId = primaryProcedure.Id, Description = "Primary", Quantity = 1, UnitPrice = 600m },
                new() { BillingItemId = secondaryProcedure.Id, Description = "Secondary", Quantity = 1, UnitPrice = 400m },
                new() { BillingItemId = assistantItem.Id, Description = "Assistant", Quantity = 1, UnitPrice = 999m }
            }
        };

        var invoiceId = await _repository.CreateAsync(dto);

        var invoice = await _context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == invoiceId);
        invoice.Items.Single(i => i.BillingItemId == primaryProcedure.Id).UnitPrice.Should().Be(600m); // 100% — highest fee
        invoice.Items.Single(i => i.BillingItemId == secondaryProcedure.Id).UnitPrice.Should().Be(200m); // 50% of 400
        invoice.Items.Single(i => i.BillingItemId == assistantItem.Id).UnitPrice.Should().Be(40m); // 20% of the ABATED 200, not raw 400
        invoice.TotalAmount.Should().Be(840m); // 600 + 200 + 40
    }

    [Fact]
    public async Task CreateAsync_ThreeOperationAssistantScenario_MatchesWorkedMbsExample()
    {
        // Real-world worked example: a surgeon performs three Group T8 operations on one occasion
        // (Laparotomy $1,200, small bowel resection $800, haemostasis $400) with an assistant
        // claiming item 51303. MBS defines the assistant fee as 20% of the surgeon's TOTAL abated
        // fee across every eligible operation, not just the highest-fee one.
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);

        var laparotomy = new BillingItem { ItemNumber = "30515", Description = "Laparotomy", Group = "T8", ScheduleFee = 1200m, Benefit100 = 1200m, IsActive = true };
        var bowelResection = new BillingItem { ItemNumber = "30375", Description = "Small bowel resection", Group = "T8", ScheduleFee = 800m, Benefit100 = 800m, IsActive = true };
        var haemostasis = new BillingItem { ItemNumber = "30390", Description = "Haemostasis", Group = "T8", ScheduleFee = 400m, Benefit100 = 400m, IsActive = true };
        var assistantItem = new BillingItem { ItemNumber = "51303", Description = "Assistant at surgery", Group = "T9", ScheduleFee = 0m, Benefit100 = 0m, IsActive = true };
        _context.BillingItems.AddRange(laparotomy, bowelResection, haemostasis, assistantItem);
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = assistantItem.Id,
            CalculationType = DerivedCalculationType.PercentageOfAssociatedItem,
            AssociatedGroup = "T8",
            Percentage = 20m
        });
        await _context.SaveChangesAsync();

        var dto = new CreateInvoiceDto
        {
            ClientId = client.Id,
            AccountType = AccountTypeEnum.BulkBill,
            DueDate = DateTime.Today.AddDays(30),
            Items = new List<CreateInvoiceItemDto>
            {
                new() { BillingItemId = laparotomy.Id, Description = "Laparotomy", Quantity = 1, UnitPrice = 1200m },
                new() { BillingItemId = bowelResection.Id, Description = "Bowel resection", Quantity = 1, UnitPrice = 800m },
                new() { BillingItemId = haemostasis.Id, Description = "Haemostasis", Quantity = 1, UnitPrice = 400m },
                new() { BillingItemId = assistantItem.Id, Description = "Assistant", Quantity = 1, UnitPrice = 999m }
            }
        };

        var invoiceId = await _repository.CreateAsync(dto);

        var invoice = await _context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == invoiceId);
        invoice.Items.Single(i => i.BillingItemId == laparotomy.Id).UnitPrice.Should().Be(1200m); // 100% — highest fee
        invoice.Items.Single(i => i.BillingItemId == bowelResection.Id).UnitPrice.Should().Be(400m); // 50% of 800
        invoice.Items.Single(i => i.BillingItemId == haemostasis.Id).UnitPrice.Should().Be(100m); // 25% of 400
        invoice.Items.Single(i => i.BillingItemId == assistantItem.Id).UnitPrice.Should().Be(340m); // 20% of (1200+400+100)=1700
        invoice.TotalAmount.Should().Be(2040m); // 1200 + 400 + 100 + 340
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
    public async Task GetByIdWithDetailsAsync_ReturnsClinicLetterheadFields()
    {
        // Arrange
        var clinic = new Clinic
        {
            Name = "Riverside Clinic",
            Abn = "12 345 678 901",
            Address = "1 River St",
            Suburb = "Springfield",
            State = "NSW",
            Postcode = "2000",
            Phone = "02 1234 5678",
            Email = "info@riverside.example"
        };
        _context.Clinics.Add(clinic);

        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClinicId = clinic.Id,
            ClientId = client.Id,
            InvoiceNumber = "INV-900",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Draft,
            TotalAmount = 100.00m
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdWithDetailsAsync(invoice.Id);

        // Assert
        result.Should().NotBeNull();
        result!.ClinicId.Should().Be(clinic.Id);
        result.ClinicName.Should().Be("Riverside Clinic");
        result.ClinicAbn.Should().Be("12 345 678 901");
        result.ClinicAddress.Should().Be("1 River St");
        result.ClinicSuburb.Should().Be("Springfield");
        result.ClinicState.Should().Be("NSW");
        result.ClinicPostcode.Should().Be("2000");
        result.ClinicPhone.Should().Be("02 1234 5678");
        result.ClinicEmail.Should().Be("info@riverside.example");
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
    public async Task SearchAsync_WithClinicIdFilter_ReturnsOnlyThatClinicsInvoices()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoices = new[]
        {
            new Invoice { ClinicId = 1, ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Draft, TotalAmount = 100.00m },
            new Invoice { ClinicId = 2, ClientId = client.Id, InvoiceNumber = "INV-002", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Draft, TotalAmount = 200.00m }
        };
        _context.Invoices.AddRange(invoices);
        await _context.SaveChangesAsync();

        var search = new InvoiceSearchDto { ClinicId = 1 };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().ContainSingle();
        result[0].InvoiceNumber.Should().Be("INV-001");
    }

    [Fact]
    public async Task CreateAsync_SetsClinicIdFromDto()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var dto = new CreateInvoiceDto
        {
            ClinicId = 3,
            ClientId = client.Id,
            DueDate = DateTime.Today.AddDays(30),
            Items = new List<CreateInvoiceItemDto> { new() { Description = "Consultation", Quantity = 1, UnitPrice = 100m } }
        };

        // Act
        var id = await _repository.CreateAsync(dto);

        // Assert
        var invoice = await _context.Invoices.FindAsync(id);
        invoice!.ClinicId.Should().Be(3);
    }

    [Fact]
    public async Task GetAllPaymentsAsync_ReturnsPaymentsWithClientAndInvoiceNumber()
    {
        // Arrange
        var client = new Client { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClinicId = 1,
            ClientId = client.Id,
            InvoiceNumber = "INV-777",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.PartiallyPaid,
            TotalAmount = 200.00m,
            AmountPaid = 100.00m
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var receiptId = await _receiptRepository.CreateAsync(new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 100.00m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoice.Id, Amount = 100.00m } }
        });

        // Act
        var (items, totalCount) = await _repository.GetAllPaymentsAsync(new PaymentSearchDto());

        // Assert
        totalCount.Should().Be(1);
        var payment = items.Should().ContainSingle().Subject;
        payment.InvoiceNumber.Should().Be("INV-777");
        payment.ClientName.Should().Be("Jane Doe");
        payment.ReceiptId.Should().Be(receiptId);
    }

    [Fact]
    public async Task GetAllPaymentsAsync_WithClinicIdFilter_ReturnsOnlyThatClinicsPayments()
    {
        // Arrange
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoiceClinic1 = new Invoice { ClinicId = 1, ClientId = client.Id, InvoiceNumber = "INV-A", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Paid, TotalAmount = 100m, AmountPaid = 100m };
        var invoiceClinic2 = new Invoice { ClinicId = 2, ClientId = client.Id, InvoiceNumber = "INV-B", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Paid, TotalAmount = 50m, AmountPaid = 50m };
        _context.Invoices.AddRange(invoiceClinic1, invoiceClinic2);
        await _context.SaveChangesAsync();

        await _receiptRepository.CreateAsync(new CreateReceiptDto
        {
            ClinicId = 1,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 100m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoiceClinic1.Id, Amount = 100m } }
        });
        await _receiptRepository.CreateAsync(new CreateReceiptDto
        {
            ClinicId = 2,
            PayerClientId = client.Id,
            Payments = { new CreateReceiptTenderDto { Amount = 50m, Method = PaymentMethod.Cash } },
            Allocations = { new CreateReceiptAllocationDto { InvoiceId = invoiceClinic2.Id, Amount = 50m } }
        });

        // Act
        var (items, totalCount) = await _repository.GetAllPaymentsAsync(new PaymentSearchDto { ClinicId = 1 });

        // Assert
        totalCount.Should().Be(1);
        items.Should().ContainSingle(p => p.InvoiceNumber == "INV-A");
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

    [Fact]
    public async Task WriteOffAsync_ReducesAmountOwingAndPersistsAmountWrittenOff()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 200m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        await _repository.WriteOffAsync(invoice.Id, new CreateWriteOffDto { Amount = 50m, Reason = "Financial hardship" });

        var updated = await _context.Invoices.FindAsync(invoice.Id);
        updated!.AmountWrittenOff.Should().Be(50m);
        updated.AmountOwing.Should().Be(150m);
    }

    [Fact]
    public async Task WriteOffAsync_ExceedingOwingAmount_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 100m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.WriteOffAsync(invoice.Id, new CreateWriteOffDto { Amount = 150m, Reason = "Too much" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteOffAsync_EmptyReason_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 100m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.WriteOffAsync(invoice.Id, new CreateWriteOffDto { Amount = 50m, Reason = "" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteOffAsync_CancelledInvoice_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Cancelled, TotalAmount = 100m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.WriteOffAsync(invoice.Id, new CreateWriteOffDto { Amount = 50m, Reason = "Nope" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteOffAsync_MultiplePartialWriteOffs_Accumulate()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 200m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        await _repository.WriteOffAsync(invoice.Id, new CreateWriteOffDto { Amount = 50m, Reason = "First" });
        await _repository.WriteOffAsync(invoice.Id, new CreateWriteOffDto { Amount = 30m, Reason = "Second" });

        var updated = await _context.Invoices.FindAsync(invoice.Id);
        updated!.AmountWrittenOff.Should().Be(80m);
        updated.AmountOwing.Should().Be(120m);
        (await _context.InvoiceWriteOffs.CountAsync(w => w.InvoiceId == invoice.Id)).Should().Be(2);
    }

    [Fact]
    public async Task CancelAsync_UnpaidInvoice_SetsStatusAndReason()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 100m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        await _repository.CancelAsync(invoice.Id, new CancelInvoiceDto { Reason = "Booked in error" });

        var updated = await _context.Invoices.FindAsync(invoice.Id);
        updated!.Status.Should().Be(InvoiceStatus.Cancelled);
        updated.CancelReason.Should().Be("Booked in error");
    }

    [Fact]
    public async Task CancelAsync_PaidInvoice_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.PartiallyPaid, TotalAmount = 100m, AmountPaid = 50m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.CancelAsync(invoice.Id, new CancelInvoiceDto { Reason = "Should fail" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Cancelled, TotalAmount = 100m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.CancelAsync(invoice.Id, new CancelInvoiceDto { Reason = "Again?" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SplitAsync_MovesItemsAndRecomputesSourceTotal()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 300m };
        _context.Invoices.Add(invoice);
        var itemA = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item A", Quantity = 1, UnitPrice = 100m };
        var itemB = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item B", Quantity = 1, UnitPrice = 200m };
        _context.InvoiceItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var result = await _repository.SplitAsync(invoice.Id, new SplitInvoiceDto
        {
            Reason = "Splitting for separate payers",
            NewInvoices = { new SplitInvoiceGroupDto { InvoiceItemIds = { itemB.Id } } }
        });

        result.NewInvoiceIds.Should().HaveCount(1);
        var source = await _context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == invoice.Id);
        source.Items.Should().ContainSingle(i => i.Id == itemA.Id);
        source.TotalAmount.Should().Be(100m);

        var sibling = await _context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == result.NewInvoiceIds[0]);
        sibling.Items.Should().ContainSingle(i => i.Description == "Item B");
        sibling.TotalAmount.Should().Be(200m);
    }

    [Fact]
    public async Task SplitAsync_ClonedItemsGetFreshIdsAndNullLegacyGuid()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 300m };
        _context.Invoices.Add(invoice);
        var itemA = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item A", Quantity = 1, UnitPrice = 100m, LegacyGuid = "legacy-guid-a" };
        var itemB = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item B", Quantity = 1, UnitPrice = 200m };
        _context.InvoiceItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var result = await _repository.SplitAsync(invoice.Id, new SplitInvoiceDto
        {
            Reason = "Splitting",
            NewInvoices = { new SplitInvoiceGroupDto { InvoiceItemIds = { itemA.Id } } }
        });

        var sibling = await _context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == result.NewInvoiceIds[0]);
        var clonedItem = sibling.Items.Single();
        clonedItem.Id.Should().NotBe(itemA.Id);
        clonedItem.LegacyGuid.Should().BeNull();
    }

    [Fact]
    public async Task SplitAsync_ItemClaimedByTwoGroups_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 300m };
        _context.Invoices.Add(invoice);
        var itemA = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item A", Quantity = 1, UnitPrice = 100m };
        var itemB = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item B", Quantity = 1, UnitPrice = 200m };
        _context.InvoiceItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.SplitAsync(invoice.Id, new SplitInvoiceDto
        {
            Reason = "Bad split",
            NewInvoices =
            {
                new SplitInvoiceGroupDto { InvoiceItemIds = { itemA.Id } },
                new SplitInvoiceGroupDto { InvoiceItemIds = { itemA.Id, itemB.Id } }
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SplitAsync_PaidSource_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.PartiallyPaid, TotalAmount = 300m, AmountPaid = 100m };
        _context.Invoices.Add(invoice);
        var itemA = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item A", Quantity = 1, UnitPrice = 100m };
        var itemB = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item B", Quantity = 1, UnitPrice = 200m };
        _context.InvoiceItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.SplitAsync(invoice.Id, new SplitInvoiceDto
        {
            Reason = "Should fail",
            NewInvoices = { new SplitInvoiceGroupDto { InvoiceItemIds = { itemB.Id } } }
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SplitAsync_WrittenOffSource_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 300m, AmountWrittenOff = 50m };
        _context.Invoices.Add(invoice);
        var itemA = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item A", Quantity = 1, UnitPrice = 100m };
        var itemB = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item B", Quantity = 1, UnitPrice = 200m };
        _context.InvoiceItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.SplitAsync(invoice.Id, new SplitInvoiceDto
        {
            Reason = "Should fail",
            NewInvoices = { new SplitInvoiceGroupDto { InvoiceItemIds = { itemB.Id } } }
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SplitAsync_MovingEveryItem_Throws()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 300m };
        _context.Invoices.Add(invoice);
        var itemA = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item A", Quantity = 1, UnitPrice = 100m };
        var itemB = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item B", Quantity = 1, UnitPrice = 200m };
        _context.InvoiceItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var act = async () => await _repository.SplitAsync(invoice.Id, new SplitInvoiceDto
        {
            Reason = "Should fail",
            NewInvoices = { new SplitInvoiceGroupDto { InvoiceItemIds = { itemA.Id, itemB.Id } } }
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SplitAsync_TwoSiblings_NoInvoiceNumberCollision()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Sent, TotalAmount = 300m };
        _context.Invoices.Add(invoice);
        var itemA = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item A", Quantity = 1, UnitPrice = 100m };
        var itemB = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item B", Quantity = 1, UnitPrice = 100m };
        var itemC = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item C", Quantity = 1, UnitPrice = 100m };
        _context.InvoiceItems.AddRange(itemA, itemB, itemC);
        await _context.SaveChangesAsync();

        // Directly exercises the per-sibling-save-before-next-number fix: if two numbers were ever
        // generated before either sibling saved, both would collide on the same next number.
        var result = await _repository.SplitAsync(invoice.Id, new SplitInvoiceDto
        {
            Reason = "Two new invoices",
            NewInvoices =
            {
                new SplitInvoiceGroupDto { InvoiceItemIds = { itemB.Id } },
                new SplitInvoiceGroupDto { InvoiceItemIds = { itemC.Id } }
            }
        });

        result.NewInvoiceIds.Should().HaveCount(2);
        var siblingNumbers = await _context.Invoices
            .Where(i => result.NewInvoiceIds.Contains(i.Id))
            .Select(i => i.InvoiceNumber)
            .ToListAsync();
        siblingNumbers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreateAsync_PersistsConsentFlagsAndPayeePractitioner()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        var payee = new Practitioner { FirstName = "Payee", LastName = "Doctor", Title = "Dr", IsActive = true };
        _context.Practitioners.Add(payee);
        await _context.SaveChangesAsync();

        var dto = new CreateInvoiceDto
        {
            ClientId = client.Id,
            DueDate = DateTime.Today.AddDays(30),
            PayeePractitionerId = payee.Id,
            ClaimSubmissionAuthorised = true,
            FinancialInterestDisclosed = true,
            CompensationClaim = true,
            SubmissionAuthorityReceived = true,
            BenefitAssignmentRequested = true,
            Items = new List<CreateInvoiceItemDto> { new() { Description = "Consultation", Quantity = 1, UnitPrice = 100m } }
        };

        var id = await _repository.CreateAsync(dto);

        var invoice = await _context.Invoices.FindAsync(id);
        invoice!.PayeePractitionerId.Should().Be(payee.Id);
        invoice.ClaimSubmissionAuthorised.Should().BeTrue();
        invoice.FinancialInterestDisclosed.Should().BeTrue();
        invoice.CompensationClaim.Should().BeTrue();
        invoice.SubmissionAuthorityReceived.Should().BeTrue();
        invoice.BenefitAssignmentRequested.Should().BeTrue();
        invoice.ClaimStatus.Should().Be(ClaimStatus.NotSubmitted);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsPayerClientIdAndPayerNameFromClient()
    {
        // Regression test for a dead-mapping bug: InvoiceDto.PayerClientId/PayerName were declared
        // but never assigned in EntityMapper.ToDto(Invoice, Clinic?), so a dependent client's
        // invoices always resolved the payer as themselves instead of the real payer.
        var payer = new Client { FirstName = "Parent", LastName = "Payer", Email = "parent@example.com", IsActive = true };
        _context.Clients.Add(payer);
        await _context.SaveChangesAsync();

        var dependent = new Client
        {
            FirstName = "Dependent",
            LastName = "Child",
            Email = "child@example.com",
            IsActive = true,
            PayerClientId = payer.Id,
            PayerName = "Parent Payer"
        };
        _context.Clients.Add(dependent);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClientId = dependent.Id,
            InvoiceNumber = "INV-PAYER-TEST",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Draft,
            TotalAmount = 100m
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdWithDetailsAsync(invoice.Id);

        result.Should().NotBeNull();
        result!.PayerClientId.Should().Be(payer.Id);
        result.PayerName.Should().Be("Parent Payer");
    }

    [Fact]
    public async Task UpdateClaimStatusAsync_UpdatesClaimStatus()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoice = new Invoice { ClientId = client.Id, InvoiceNumber = "INV-001", InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Status = InvoiceStatus.Draft, TotalAmount = 100m };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        await _repository.UpdateClaimStatusAsync(invoice.Id, ClaimStatus.Submitted);

        var updated = await _context.Invoices.FindAsync(invoice.Id);
        updated!.ClaimStatus.Should().Be(ClaimStatus.Submitted);
    }

    [Fact]
    public async Task SplitAsync_CarriesForwardConsentFlagsAndPayeePractitioner_ButResetsClaimStatusToNotSubmitted()
    {
        var client = new Client { FirstName = "Test", LastName = "Client", Email = "test@example.com", IsActive = true };
        _context.Clients.Add(client);
        var payee = new Practitioner { FirstName = "Payee", LastName = "Doctor", Title = "Dr", IsActive = true };
        _context.Practitioners.Add(payee);
        await _context.SaveChangesAsync();

        var invoice = new Invoice
        {
            ClientId = client.Id,
            InvoiceNumber = "INV-001",
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Status = InvoiceStatus.Sent,
            TotalAmount = 300m,
            PayeePractitionerId = payee.Id,
            ClaimSubmissionAuthorised = true,
            FinancialInterestDisclosed = true,
            CompensationClaim = true,
            SubmissionAuthorityReceived = true,
            BenefitAssignmentRequested = true,
            ClaimStatus = ClaimStatus.Submitted
        };
        _context.Invoices.Add(invoice);
        var itemA = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item A", Quantity = 1, UnitPrice = 100m };
        var itemB = new InvoiceItem { InvoiceId = invoice.Id, Description = "Item B", Quantity = 1, UnitPrice = 200m };
        _context.InvoiceItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var result = await _repository.SplitAsync(invoice.Id, new SplitInvoiceDto
        {
            Reason = "Testing field carry-forward",
            NewInvoices = { new SplitInvoiceGroupDto { InvoiceItemIds = { itemB.Id } } }
        });

        var sibling = await _context.Invoices.FirstAsync(i => i.Id == result.NewInvoiceIds[0]);
        sibling.PayeePractitionerId.Should().Be(payee.Id);
        sibling.ClaimSubmissionAuthorised.Should().BeTrue();
        sibling.FinancialInterestDisclosed.Should().BeTrue();
        sibling.CompensationClaim.Should().BeTrue();
        sibling.SubmissionAuthorityReceived.Should().BeTrue();
        sibling.BenefitAssignmentRequested.Should().BeTrue();
        sibling.ClaimStatus.Should().Be(ClaimStatus.NotSubmitted);
    }
}