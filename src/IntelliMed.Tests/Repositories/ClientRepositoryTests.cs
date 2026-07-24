using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientRepositoryTests : IDisposable
{
    private readonly ClientRepository _repository;
    private readonly AppDbContext _context;

    public ClientRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewClientId()
    {
        // Arrange
        var dto = new CreateClientDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "0412345678",
            DateOfBirth = new DateTime(1990, 5, 15),
            Address = "123 Main Street",
            MedicareNumber = "1234567890"
        };

        // Act
        var result = await _repository.CreateAsync(dto);

        // Assert
        result.Should().BeGreaterThan(0);
        var client = await _context.Clients.FindAsync(result);
        client.Should().NotBeNull();
        client!.FirstName.Should().Be("John");
        client.LastName.Should().Be("Doe");
        client.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingClient_ReturnsClientDto()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Phone = "0498765432",
            DateOfBirth = new DateTime(1985, 3, 20),
            IsActive = true
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(client.Id);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.Email.Should().Be("jane.smith@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingClient_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithExistingClient_UpdatesClient()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Original",
            LastName = "Name",
            Email = "original@example.com",
            Phone = "0411111111",
            IsActive = true
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var updateDto = new UpdateClientDto
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Phone = "0422222222"
        };

        // Act
        await _repository.UpdateAsync(client.Id, updateDto);

        // Assert
        var updatedClient = await _context.Clients.FindAsync(client.Id);
        updatedClient!.FirstName.Should().Be("Updated");
        updatedClient.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingClient_ThrowsException()
    {
        // Arrange
        var updateDto = new UpdateClientDto
        {
            FirstName = "Test",
            LastName = "User"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.UpdateAsync(999, updateDto));
    }

    [Fact]
    public async Task SearchAsync_WithNameQuery_ReturnsMatchingClients()
    {
        // Arrange
        var clients = new[]
        {
            new Client { FirstName = "John", LastName = "Doe", Email = "john@example.com", IsActive = true },
            new Client { FirstName = "John", LastName = "Smith", Email = "john.smith@example.com", IsActive = true },
            new Client { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", IsActive = true }
        };
        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync();

        var search = new ClientSearchDto { Query = "John" };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.LastName == "Doe");
        result.Should().Contain(p => p.LastName == "Smith");
    }

    [Fact]
    public async Task SearchAsync_WithEmailQuery_ReturnsMatchingClients()
    {
        // Arrange
        var clients = new[]
        {
            new Client { FirstName = "Test", LastName = "User1", Email = "test1@example.com", IsActive = true },
            new Client { FirstName = "Test", LastName = "User2", Email = "test2@example.com", IsActive = true },
            new Client { FirstName = "Other", LastName = "Person", Email = "other@example.com", IsActive = true }
        };
        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync();

        var search = new ClientSearchDto { Query = "test1@example.com" };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Email.Should().Be("test1@example.com");
    }

    [Fact]
    public async Task SearchAsync_WithActiveFilter_ReturnsOnlyActiveClients()
    {
        // Arrange
        var clients = new[]
        {
            new Client { FirstName = "Active", LastName = "Client", Email = "active@example.com", IsActive = true },
            new Client { FirstName = "Inactive", LastName = "Client", Email = "inactive@example.com", IsActive = false }
        };
        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync();

        var search = new ClientSearchDto { IsActive = true };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].FirstName.Should().Be("Active");
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 25; i++)
        {
            _context.Clients.Add(new Client
            {
                FirstName = $"Client{i:D2}",
                LastName = "Test",
                Email = $"client{i}@example.com",
                IsActive = true
            });
        }
        await _context.SaveChangesAsync();

        var search = new ClientSearchDto { Page = 2, PageSize = 10 };

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(search);

        // Assert
        totalCount.Should().Be(25);
        items.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllClients()
    {
        // Arrange
        var clients = new[]
        {
            new Client { FirstName = "Client1", LastName = "Test", Email = "p1@example.com", IsActive = true },
            new Client { FirstName = "Client2", LastName = "Test", Email = "p2@example.com", IsActive = true }
        };
        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesClient()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "ToDelete",
            LastName = "Client",
            Email = "delete@example.com",
            IsActive = true
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        var clientId = client.Id;

        // Act
        await _repository.DeleteAsync(clientId);

        // Assert
        var deletedClient = await _context.Clients.FindAsync(clientId);
        deletedClient.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingClient_ReturnsTrue()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Exists",
            LastName = "Client",
            Email = "exists@example.com",
            IsActive = true
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsAsync(client.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingClient_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    #region Comprehensive Client Creation Tests

    [Fact]
    public async Task CreateAsync_WithMinimalRequiredFields_ReturnsCreatedClient()
    {
        // Arrange - Only required fields
        var dto = new CreateClientDto
        {
            FirstName = "Minimal",
            LastName = "Client",
            DateOfBirth = new DateTime(1990, 1, 1),
            DobAccuracy = DobAccuracyEnum.Day,
            MedicareNumber = "1234567890",
            Address = "123 Test St",
            Suburb = "Testville",
            State = "VIC",
            Postcode = "3000"
        };

        // Act
        var clientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Clients.FindAsync(clientId);
        created.Should().NotBeNull();
        created!.FirstName.Should().Be("Minimal");
        created.LastName.Should().Be("Client");
        created.IsActive.Should().BeTrue();
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_WithAllAccountTypes_ReturnsCorrectClients()
    {
        // Test all account type values
        var accountTypes = new[]
        {
            AccountTypeEnum.PrivatePatient,
            AccountTypeEnum.Concession,
            AccountTypeEnum.Pensioner,
            AccountTypeEnum.Veteran,
            AccountTypeEnum.WorkCover,
            AccountTypeEnum.Tac,
            AccountTypeEnum.BulkBill,
            AccountTypeEnum.Other
        };

        foreach (var accountType in accountTypes)
        {
            var dto = CreateMinimalClientDto();
            dto.FirstName = $"Client{accountType}";
            dto.AccountType = accountType;

            var clientId = await _repository.CreateAsync(dto);
            var created = await _context.Clients.FindAsync(clientId);
            created!.AccountType.Should().Be(accountType, $"Account type {accountType} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithAllGenders_ReturnsCorrectClients()
    {
        // Test all gender values
        var genders = new[]
        {
            GenderEnum.Unspecified,
            GenderEnum.Male,
            GenderEnum.Female,
            GenderEnum.Other
        };

        foreach (var gender in genders)
        {
            var dto = CreateMinimalClientDto();
            dto.FirstName = $"Client{gender}";
            dto.Gender = gender;

            var clientId = await _repository.CreateAsync(dto);
            var created = await _context.Clients.FindAsync(clientId);
            created!.Gender.Should().Be(gender, $"Gender {gender} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithAllAtsiStatuses_ReturnsCorrectClients()
    {
        // Test all ATSI status values
        var statuses = new[]
        {
            AtsiStatusEnum.NotAsked,
            AtsiStatusEnum.AboriginalOnly,
            AtsiStatusEnum.TorresStraitIslanderOnly,
            AtsiStatusEnum.Both,
            AtsiStatusEnum.NeitherAboriginalNorTorresStraitIslander
        };

        foreach (var status in statuses)
        {
            var dto = CreateMinimalClientDto();
            dto.FirstName = $"Client{status}";
            dto.AtsiStatus = status;

            var clientId = await _repository.CreateAsync(dto);
            var created = await _context.Clients.FindAsync(clientId);
            created!.AtsiStatus.Should().Be(status, $"ATSI status {status} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithAllMaritalStatuses_ReturnsCorrectClients()
    {
        // Test all marital status values
        var statuses = new[]
        {
            MaritalStatusEnum.Unknown,
            MaritalStatusEnum.Single,
            MaritalStatusEnum.Married,
            MaritalStatusEnum.DeFacto,
            MaritalStatusEnum.Divorced,
            MaritalStatusEnum.Widowed,
            MaritalStatusEnum.Separated
        };

        foreach (var status in statuses)
        {
            var dto = CreateMinimalClientDto();
            dto.FirstName = $"Client{status}";
            dto.MaritalStatus = status;

            var clientId = await _repository.CreateAsync(dto);
            var created = await _context.Clients.FindAsync(clientId);
            created!.MaritalStatus.Should().Be(status, $"Marital status {status} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithAllDobAccuracies_ReturnsCorrectClients()
    {
        // Test all DOB accuracy values
        var accuracies = new[]
        {
            DobAccuracyEnum.Day,
            DobAccuracyEnum.Month,
            DobAccuracyEnum.Year,
            DobAccuracyEnum.Estimated
        };

        foreach (var accuracy in accuracies)
        {
            var dto = CreateMinimalClientDto();
            dto.FirstName = $"Client{accuracy}";
            dto.DobAccuracy = accuracy;

            var clientId = await _repository.CreateAsync(dto);
            var created = await _context.Clients.FindAsync(clientId);
            created!.DobAccuracy.Should().Be(accuracy, $"DOB accuracy {accuracy} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithMedicareDetails_ReturnsClientWithMedicare()
    {
        // Arrange
        var dto = CreateMinimalClientDto();
        dto.MedicareNumber = "9876543210";
        dto.MedicarePosition = "1";
        dto.MedicareExpiryDate = new DateTime(2027, 12, 31);
        dto.MedicareIncentiveEligible = true;
        dto.CtgCoPaymentRelief = true;

        // Act
        var clientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Clients.FindAsync(clientId);
        created!.MedicareNumber.Should().Be("9876543210");
        created.MedicarePosition.Should().Be("1");
        created.MedicareExpiryDate.Should().Be(new DateTime(2027, 12, 31));
        created.MedicareIncentiveEligible.Should().BeTrue();
        created.CtgCoPaymentRelief.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithDvaDetails_ReturnsClientWithDva()
    {
        // Arrange
        var dto = CreateMinimalClientDto();
        dto.DvaNumber = "DVA123456";
        dto.DvaExpiryDate = new DateTime(2028, 6, 30);

        // Act
        var clientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Clients.FindAsync(clientId);
        created!.DvaNumber.Should().Be("DVA123456");
        created.DvaExpiryDate.Should().Be(new DateTime(2028, 6, 30));
    }

    [Fact]
    public async Task CreateAsync_WithPensionDetails_ReturnsClientWithPension()
    {
        // Arrange
        var dto = CreateMinimalClientDto();
        dto.PensionNumber = "PEN123456";
        dto.PensionExpiryDate = new DateTime(2027, 3, 31);
        dto.AccountType = AccountTypeEnum.Pensioner;

        // Act
        var clientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Clients.FindAsync(clientId);
        created!.PensionNumber.Should().Be("PEN123456");
        created.PensionExpiryDate.Should().Be(new DateTime(2027, 3, 31));
        created.AccountType.Should().Be(AccountTypeEnum.Pensioner);
    }

    [Fact]
    public async Task CreateAsync_WithWorkCover_ReturnsWorkCoverClient()
    {
        // Arrange
        var dto = CreateMinimalClientDto();
        dto.AccountType = AccountTypeEnum.WorkCover;
        dto.Notes = "WorkCover claim in progress";

        // Act
        var clientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Clients.FindAsync(clientId);
        created!.AccountType.Should().Be(AccountTypeEnum.WorkCover);
        created.Notes.Should().Be("WorkCover claim in progress");
    }

    [Fact]
    public async Task CreateAsync_WithTac_ReturnsTacClient()
    {
        // Arrange
        var dto = CreateMinimalClientDto();
        dto.AccountType = AccountTypeEnum.Tac;
        dto.Notes = "Motor vehicle accident - TAC claim";

        // Act
        var clientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Clients.FindAsync(clientId);
        created!.AccountType.Should().Be(AccountTypeEnum.Tac);
        created.Notes.Should().Be("Motor vehicle accident - TAC claim");
    }

    [Fact]
    public async Task CreateAsync_WithFullDetails_ReturnsCompleteClient()
    {
        // Arrange
        var dto = CreateFullClientDto();

        // Act
        var clientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _repository.GetByIdAsync(clientId);
        created.Should().NotBeNull();
        created!.FirstName.Should().Be("John");
        created.LastName.Should().Be("Doe");
        created.MiddleName.Should().Be("Michael");
        created.PreferredName.Should().Be("Johnny");
        created.Gender.Should().Be(GenderEnum.Male);
        created.DateOfBirth.Should().Be(new DateTime(1985, 6, 15));
        created.MedicareNumber.Should().Be("1234567890");
        created.Address.Should().Be("123 Main Street");
        created.Suburb.Should().Be("Melbourne");
        created.State.Should().Be("VIC");
        created.Postcode.Should().Be("3000");
        created.Email.Should().Be("john.doe@example.com");
        created.Phone.Should().Be("0398765432");
        created.MobilePhone.Should().Be("0412345678");
        created.InterpreterRequired.Should().BeTrue();
        created.InterpreterLanguage.Should().Be("Italian");
        created.MaritalStatus.Should().Be(MaritalStatusEnum.Married);
        created.AccountType.Should().Be(AccountTypeEnum.PrivatePatient);
        created.HealthFundCode.Should().Be("BUP");
        created.HealthFundName.Should().Be("Bupa");
        created.FileNumber.Should().Be("FILE001");
        created.IhiNumber.Should().Be("IHI123456789");
    }

    #endregion

    #region Comprehensive Client Retrieval Tests

    [Fact]
    public async Task GetByIdAsync_AfterMultipleCreates_ReturnsCorrectClient()
    {
        // Arrange
        var dto1 = CreateMinimalClientDto();
        dto1.FirstName = "First";
        dto1.LastName = "Client";

        var dto2 = CreateMinimalClientDto();
        dto2.FirstName = "Second";
        dto2.LastName = "Client";

        var dto3 = CreateMinimalClientDto();
        dto3.FirstName = "Third";
        dto3.LastName = "Client";

        var id1 = await _repository.CreateAsync(dto1);
        var id2 = await _repository.CreateAsync(dto2);
        var id3 = await _repository.CreateAsync(dto3);

        // Act
        var client1 = await _repository.GetByIdAsync(id1);
        var client2 = await _repository.GetByIdAsync(id2);
        var client3 = await _repository.GetByIdAsync(id3);

        // Assert
        client1!.FirstName.Should().Be("First");
        client2!.FirstName.Should().Be("Second");
        client3!.FirstName.Should().Be("Third");
    }

    [Fact]
    public async Task GetByIdAsync_WithFullClient_ReturnsAllFields()
    {
        // Arrange
        var dto = CreateFullClientDto();
        var clientId = await _repository.CreateAsync(dto);

        // Act
        var client = await _repository.GetByIdAsync(clientId);

        // Assert
        client.Should().NotBeNull();
        client!.FirstName.Should().Be("John");
        client.MiddleName.Should().Be("Michael");
        client.PreferredName.Should().Be("Johnny");
        client.InterpreterRequired.Should().BeTrue();
        client.InterpreterLanguage.Should().Be("Italian");
        client.HealthFundCode.Should().Be("BUP");
        client.HealthFundNumber.Should().Be("BUPA123456");
        client.IhiNumber.Should().Be("IHI123456789");
    }

    [Fact]
    public async Task GetByIdAsync_WithArchivedClient_ReturnsClient()
    {
        // Arrange
        var dto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(dto);
        await _repository.ArchiveAsync(clientId); // Archive instead of delete

        // Act
        var client = await _repository.GetByIdAsync(clientId);

        // Assert - GetById should still return the client even if archived
        client.Should().NotBeNull();
        client!.IsActive.Should().BeFalse();
    }

    #endregion

    #region Comprehensive Client Update Tests

    [Fact]
    public async Task UpdateAsync_UpdatesBasicFields()
    {
        // Arrange
        var createDto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdateClientDto
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            DateOfBirth = new DateTime(1990, 1, 1),
            DobAccuracy = DobAccuracyEnum.Day,
            MedicareNumber = "1111111111",
            Address = "456 Updated Street",
            Suburb = "UpdatedSuburb",
            State = "NSW",
            Postcode = "2000",
            Email = "updated@example.com",
            Phone = "0299999999",
            Gender = GenderEnum.Female,
            AccountType = AccountTypeEnum.BulkBill
        };

        // Act
        await _repository.UpdateAsync(clientId, updateDto);

        // Assert
        var updated = await _context.Clients.FindAsync(clientId);
        updated!.FirstName.Should().Be("UpdatedFirst");
        updated.LastName.Should().Be("UpdatedLast");
        updated.Gender.Should().Be(GenderEnum.Female);
        updated.Address.Should().Be("456 Updated Street");
        updated.Suburb.Should().Be("UpdatedSuburb");
        updated.State.Should().Be("NSW");
        updated.Postcode.Should().Be("2000");
        updated.Email.Should().Be("updated@example.com");
        updated.AccountType.Should().Be(AccountTypeEnum.BulkBill);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContactDetails()
    {
        // Arrange
        var createDto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdateClientDto
        {
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            DateOfBirth = createDto.DateOfBirth,
            DobAccuracy = createDto.DobAccuracy,
            MedicareNumber = createDto.MedicareNumber,
            Address = createDto.Address,
            Suburb = createDto.Suburb,
            State = createDto.State,
            Postcode = createDto.Postcode,
            Email = "new.email@example.com",
            Phone = "0399998888",
            MobilePhone = "0499888777",
            BusinessHoursPhone = "0388886666",
            FaxNumber = "0388885555",
            AcceptSms = true,
            AcceptEmail = false
        };

        // Act
        await _repository.UpdateAsync(clientId, updateDto);

        // Assert
        var updated = await _context.Clients.FindAsync(clientId);
        updated!.Email.Should().Be("new.email@example.com");
        updated.MobilePhone.Should().Be("0499888777");
        updated.BusinessHoursPhone.Should().Be("0388886666");
        updated.FaxNumber.Should().Be("0388885555");
        updated.AcceptSms.Should().BeTrue();
        updated.AcceptEmail.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEntitlementDetails()
    {
        // Arrange
        var createDto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdateClientDto
        {
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            DateOfBirth = createDto.DateOfBirth,
            DobAccuracy = createDto.DobAccuracy,
            MedicareNumber = "2222222222",
            MedicarePosition = "2",
            MedicareExpiryDate = new DateTime(2028, 6, 30),
            DvaNumber = "DVA999999",
            DvaExpiryDate = new DateTime(2029, 12, 31),
            PensionNumber = "PEN888888",
            PensionExpiryDate = new DateTime(2028, 9, 30),
            MedicareIncentiveEligible = true,
            CtgCoPaymentRelief = true,
            Address = createDto.Address,
            Suburb = createDto.Suburb,
            State = createDto.State,
            Postcode = createDto.Postcode
        };

        // Act
        await _repository.UpdateAsync(clientId, updateDto);

        // Assert
        var updated = await _context.Clients.FindAsync(clientId);
        updated!.MedicareNumber.Should().Be("2222222222");
        updated.MedicarePosition.Should().Be("2");
        updated.MedicareExpiryDate.Should().Be(new DateTime(2028, 6, 30));
        updated.DvaNumber.Should().Be("DVA999999");
        updated.PensionNumber.Should().Be("PEN888888");
        updated.MedicareIncentiveEligible.Should().BeTrue();
        updated.CtgCoPaymentRelief.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesHealthFundDetails()
    {
        // Arrange
        var createDto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdateClientDto
        {
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            DateOfBirth = createDto.DateOfBirth,
            DobAccuracy = createDto.DobAccuracy,
            MedicareNumber = createDto.MedicareNumber,
            Address = createDto.Address,
            Suburb = createDto.Suburb,
            State = createDto.State,
            Postcode = createDto.Postcode,
            HealthFundId = 4, // nib (seeded)
            HealthFundNumber = "NIB123456",
            HealthFundRef = "REF001",
            HealthFundAliasFamily = "Smith Family",
            HealthFundAliasFirst = "John"
        };

        // Act
        await _repository.UpdateAsync(clientId, updateDto);

        // Assert
        var updated = await _repository.GetByIdAsync(clientId);
        updated!.HealthFundCode.Should().Be("NIB");
        updated.HealthFundName.Should().Be("nib");
        updated.HealthFundNumber.Should().Be("NIB123456");
        updated.HealthFundRef.Should().Be("REF001");
        updated.HealthFundAliasFamily.Should().Be("Smith Family");
        updated.HealthFundAliasFirst.Should().Be("John");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAccountDetails()
    {
        // Arrange
        var createDto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdateClientDto
        {
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            DateOfBirth = createDto.DateOfBirth,
            DobAccuracy = createDto.DobAccuracy,
            MedicareNumber = createDto.MedicareNumber,
            Address = createDto.Address,
            Suburb = createDto.Suburb,
            State = createDto.State,
            Postcode = createDto.Postcode,
            AccountType = AccountTypeEnum.Veteran,
            FeeRateCode = "VET001",
            AccountName = "Veteran Account",
            AccountBsb = "123456",
            AccountNumber = "12345678",
            UseMedicareRegisteredBankAccount = false
        };

        // Act
        await _repository.UpdateAsync(clientId, updateDto);

        // Assert
        var updated = await _context.Clients.FindAsync(clientId);
        updated!.AccountType.Should().Be(AccountTypeEnum.Veteran);
        updated.FeeRateCode.Should().Be("VET001");
        updated.AccountName.Should().Be("Veteran Account");
        updated.AccountBsb.Should().Be("123456");
        updated.AccountNumber.Should().Be("12345678");
        updated.UseMedicareRegisteredBankAccount.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFileDetails()
    {
        // Arrange
        var createDto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdateClientDto
        {
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            DateOfBirth = createDto.DateOfBirth,
            DobAccuracy = createDto.DobAccuracy,
            MedicareNumber = createDto.MedicareNumber,
            Address = createDto.Address,
            Suburb = createDto.Suburb,
            State = createDto.State,
            Postcode = createDto.Postcode,
            FileNumber = "FILE999",
            UrNumber = "UR123456",
            LastSeenDate = new DateTime(2026, 7, 20)
        };

        // Act
        await _repository.UpdateAsync(clientId, updateDto);

        // Assert
        var updated = await _context.Clients.FindAsync(clientId);
        updated!.FileNumber.Should().Be("FILE999");
        updated.UrNumber.Should().Be("UR123456");
        updated.LastSeenDate.Should().Be(new DateTime(2026, 7, 20));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEhealthDetails()
    {
        // Arrange
        var createDto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdateClientDto
        {
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            DateOfBirth = createDto.DateOfBirth,
            DobAccuracy = createDto.DobAccuracy,
            MedicareNumber = createDto.MedicareNumber,
            Address = createDto.Address,
            Suburb = createDto.Suburb,
            State = createDto.State,
            Postcode = createDto.Postcode,
            IhiNumber = "IHI999888777",
            IhiRecordStatus = "Verified",
            IhiAssignedDate = new DateTime(2026, 1, 15),
            IhiNumberStatus = "Active"
        };

        // Act
        await _repository.UpdateAsync(clientId, updateDto);

        // Assert
        var updated = await _context.Clients.FindAsync(clientId);
        updated!.IhiNumber.Should().Be("IHI999888777");
        updated.IhiRecordStatus.Should().Be("Verified");
        updated.IhiAssignedDate.Should().Be(new DateTime(2026, 1, 15));
        updated.IhiNumberStatus.Should().Be("Active");
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAtTimestamp()
    {
        // Arrange
        var createDto = CreateMinimalClientDto();
        var clientId = await _repository.CreateAsync(createDto);
        var created = await _context.Clients.FindAsync(clientId);
        var originalCreatedAt = created!.CreatedAt;

        await Task.Delay(10); // Small delay to ensure different timestamp

        var updateDto = new UpdateClientDto
        {
            FirstName = "Updated",
            LastName = createDto.LastName,
            DateOfBirth = createDto.DateOfBirth,
            DobAccuracy = createDto.DobAccuracy,
            MedicareNumber = createDto.MedicareNumber,
            Address = createDto.Address,
            Suburb = createDto.Suburb,
            State = createDto.State,
            Postcode = createDto.Postcode
        };

        // Act
        await _repository.UpdateAsync(clientId, updateDto);

        // Assert
        var updated = await _context.Clients.FindAsync(clientId);
        updated!.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt.Should().BeOnOrAfter(originalCreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAllAccountTypes()
    {
        // Test updating all account type values
        var accountTypes = new[]
        {
            AccountTypeEnum.PrivatePatient,
            AccountTypeEnum.Concession,
            AccountTypeEnum.Pensioner,
            AccountTypeEnum.Veteran,
            AccountTypeEnum.WorkCover,
            AccountTypeEnum.Tac,
            AccountTypeEnum.BulkBill,
            AccountTypeEnum.Other
        };

        foreach (var accountType in accountTypes)
        {
            var dto = CreateMinimalClientDto();
            var clientId = await _repository.CreateAsync(dto);

            var updateDto = new UpdateClientDto
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
                DobAccuracy = dto.DobAccuracy,
                MedicareNumber = dto.MedicareNumber,
                Address = dto.Address,
                Suburb = dto.Suburb,
                State = dto.State,
                Postcode = dto.Postcode,
                AccountType = accountType
            };

            await _repository.UpdateAsync(clientId, updateDto);

            var updated = await _context.Clients.FindAsync(clientId);
            updated!.AccountType.Should().Be(accountType, $"Account type {accountType} should be updated correctly");
        }
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAllGenders()
    {
        // Test updating all gender values
        var genders = new[]
        {
            GenderEnum.Unspecified,
            GenderEnum.Male,
            GenderEnum.Female,
            GenderEnum.Other
        };

        foreach (var gender in genders)
        {
            var dto = CreateMinimalClientDto();
            var clientId = await _repository.CreateAsync(dto);

            var updateDto = new UpdateClientDto
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
                DobAccuracy = dto.DobAccuracy,
                MedicareNumber = dto.MedicareNumber,
                Address = dto.Address,
                Suburb = dto.Suburb,
                State = dto.State,
                Postcode = dto.Postcode,
                Gender = gender
            };

            await _repository.UpdateAsync(clientId, updateDto);

            var updated = await _context.Clients.FindAsync(clientId);
            updated!.Gender.Should().Be(gender, $"Gender {gender} should be updated correctly");
        }
    }

    #endregion

    #region Helper Methods

    private static CreateClientDto CreateMinimalClientDto()
    {
        return new CreateClientDto
        {
            FirstName = "Test",
            LastName = "Client",
            DateOfBirth = new DateTime(1990, 1, 1),
            DobAccuracy = DobAccuracyEnum.Day,
            MedicareNumber = "1234567890",
            Address = "123 Test Street",
            Suburb = "Testville",
            State = "VIC",
            Postcode = "3000",
            Email = "test@example.com",
            Phone = "0398765432"
        };
    }

    private static CreateClientDto CreateFullClientDto()
    {
        return new CreateClientDto
        {
            // Personal
            FirstName = "John",
            LastName = "Doe",
            MiddleName = "Michael",
            PreferredName = "Johnny",
            MaidenName = null,
            Title = "Mr",
            Gender = GenderEnum.Male,
            DateOfBirth = new DateTime(1985, 6, 15),
            DobAccuracy = DobAccuracyEnum.Day,
            PlaceOfBirth = "Melbourne",
            InterpreterRequired = true,
            InterpreterLanguage = "Italian",
            MaritalStatus = MaritalStatusEnum.Married,
            Ethnicity = "Australian",

            // Entitlement
            MedicareNumber = "1234567890",
            MedicarePosition = "1",
            MedicareExpiryDate = new DateTime(2027, 6, 30),
            DvaNumber = null,
            DvaExpiryDate = null,
            PensionNumber = null,
            PensionExpiryDate = null,
            EntitlementStatus = null,
            SafetyNetNumber = "SN123456",
            AtsiStatus = AtsiStatusEnum.NotAsked,
            MedicareIncentiveEligible = true,
            CtgCoPaymentRelief = true,

            // Residential address
            Address = "123 Main Street",
            Suburb = "Melbourne",
            State = "VIC",
            Postcode = "3000",

            // Contact Details
            Email = "john.doe@example.com",
            Phone = "0398765432",
            BusinessHoursPhone = "0398765433",
            MobilePhone = "0412345678",
            FaxNumber = "0398765434",
            AcceptSms = true,
            AcceptEmail = true,
            AcceptOnlineAppointments = true,
            AcceptSmsMarketing = false,
            Notes = "Regular client",
            Warnings = null,
            NextOfKinClientId = null,
            NextOfKinName = "Mary Doe",
            NextOfKinPhone = "0412345679",
            EmergencyContactClientId = null,
            EmergencyContactName = "Bob Doe",
            EmergencyContactPhone = "0412345680",
            SameAsNextOfKin = false,

            // Health Fund
            HealthFundId = 2, // Bupa (seeded)
            HealthFundNumber = "BUPA123456",
            HealthFundRef = "REF001",
            HealthFundAliasFamily = "Doe Family",
            HealthFundAliasFirst = "John",

            // Account
            AccountType = AccountTypeEnum.PrivatePatient,
            FeeRateCode = "STD",
            PayerClientId = null,
            PayerName = null,
            AccountName = null,
            AccountBsb = null,
            AccountNumber = null,
            UseMedicareRegisteredBankAccount = true,

            // File
            FileNumber = "FILE001",
            UrNumber = "UR001",
            Deceased = false,
            ProviderId = null,
            LastSeenDate = new DateTime(2026, 7, 15),

            // eHealth
            IhiNumber = "IHI123456789",
            IhiRecordStatus = "Verified",
            IhiAssignedDate = new DateTime(2026, 1, 1),
            IhiNumberStatus = "Active",
            IhiUnresolvedDate = null,

            // Lifecard
            LifeCardNum = "LC123456"
        };
    }

    #endregion
}