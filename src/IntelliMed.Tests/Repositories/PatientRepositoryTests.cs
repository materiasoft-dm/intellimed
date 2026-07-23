using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PatientRepositoryTests : IDisposable
{
    private readonly PatientRepository _repository;
    private readonly AppDbContext _context;

    public PatientRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PatientRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewPatientId()
    {
        // Arrange
        var dto = new CreatePatientDto
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
        var patient = await _context.Patients.FindAsync(result);
        patient.Should().NotBeNull();
        patient!.FirstName.Should().Be("John");
        patient.LastName.Should().Be("Doe");
        patient.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingPatient_ReturnsPatientDto()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Phone = "0498765432",
            DateOfBirth = new DateTime(1985, 3, 20),
            IsActive = true
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(patient.Id);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.Email.Should().Be("jane.smith@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingPatient_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithExistingPatient_UpdatesPatient()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Original",
            LastName = "Name",
            Email = "original@example.com",
            Phone = "0411111111",
            IsActive = true
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var updateDto = new UpdatePatientDto
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Phone = "0422222222"
        };

        // Act
        await _repository.UpdateAsync(patient.Id, updateDto);

        // Assert
        var updatedPatient = await _context.Patients.FindAsync(patient.Id);
        updatedPatient!.FirstName.Should().Be("Updated");
        updatedPatient.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingPatient_ThrowsException()
    {
        // Arrange
        var updateDto = new UpdatePatientDto
        {
            FirstName = "Test",
            LastName = "User"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.UpdateAsync(999, updateDto));
    }

    [Fact]
    public async Task SearchAsync_WithNameQuery_ReturnsMatchingPatients()
    {
        // Arrange
        var patients = new[]
        {
            new Patient { FirstName = "John", LastName = "Doe", Email = "john@example.com", IsActive = true },
            new Patient { FirstName = "John", LastName = "Smith", Email = "john.smith@example.com", IsActive = true },
            new Patient { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", IsActive = true }
        };
        _context.Patients.AddRange(patients);
        await _context.SaveChangesAsync();

        var search = new PatientSearchDto { Query = "John" };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.LastName == "Doe");
        result.Should().Contain(p => p.LastName == "Smith");
    }

    [Fact]
    public async Task SearchAsync_WithEmailQuery_ReturnsMatchingPatients()
    {
        // Arrange
        var patients = new[]
        {
            new Patient { FirstName = "Test", LastName = "User1", Email = "test1@example.com", IsActive = true },
            new Patient { FirstName = "Test", LastName = "User2", Email = "test2@example.com", IsActive = true },
            new Patient { FirstName = "Other", LastName = "Person", Email = "other@example.com", IsActive = true }
        };
        _context.Patients.AddRange(patients);
        await _context.SaveChangesAsync();

        var search = new PatientSearchDto { Query = "test1@example.com" };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Email.Should().Be("test1@example.com");
    }

    [Fact]
    public async Task SearchAsync_WithActiveFilter_ReturnsOnlyActivePatients()
    {
        // Arrange
        var patients = new[]
        {
            new Patient { FirstName = "Active", LastName = "Patient", Email = "active@example.com", IsActive = true },
            new Patient { FirstName = "Inactive", LastName = "Patient", Email = "inactive@example.com", IsActive = false }
        };
        _context.Patients.AddRange(patients);
        await _context.SaveChangesAsync();

        var search = new PatientSearchDto { IsActive = true };

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
            _context.Patients.Add(new Patient
            {
                FirstName = $"Patient{i:D2}",
                LastName = "Test",
                Email = $"patient{i}@example.com",
                IsActive = true
            });
        }
        await _context.SaveChangesAsync();

        var search = new PatientSearchDto { Page = 2, PageSize = 10 };

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(search);

        // Assert
        totalCount.Should().Be(25);
        items.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPatients()
    {
        // Arrange
        var patients = new[]
        {
            new Patient { FirstName = "Patient1", LastName = "Test", Email = "p1@example.com", IsActive = true },
            new Patient { FirstName = "Patient2", LastName = "Test", Email = "p2@example.com", IsActive = true }
        };
        _context.Patients.AddRange(patients);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPatient()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "ToDelete",
            LastName = "Patient",
            Email = "delete@example.com",
            IsActive = true
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();
        var patientId = patient.Id;

        // Act
        await _repository.DeleteAsync(patientId);

        // Assert
        var deletedPatient = await _context.Patients.FindAsync(patientId);
        deletedPatient.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingPatient_ReturnsTrue()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Exists",
            LastName = "Patient",
            Email = "exists@example.com",
            IsActive = true
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsAsync(patient.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingPatient_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    #region Comprehensive Patient Creation Tests

    [Fact]
    public async Task CreateAsync_WithMinimalRequiredFields_ReturnsCreatedPatient()
    {
        // Arrange - Only required fields
        var dto = new CreatePatientDto
        {
            FirstName = "Minimal",
            LastName = "Patient",
            DateOfBirth = new DateTime(1990, 1, 1),
            DobAccuracy = DobAccuracyEnum.Day,
            MedicareNumber = "1234567890",
            Address = "123 Test St",
            Suburb = "Testville",
            State = "VIC",
            Postcode = "3000"
        };

        // Act
        var patientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Patients.FindAsync(patientId);
        created.Should().NotBeNull();
        created!.FirstName.Should().Be("Minimal");
        created.LastName.Should().Be("Patient");
        created.IsActive.Should().BeTrue();
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_WithAllAccountTypes_ReturnsCorrectPatients()
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
            var dto = CreateMinimalPatientDto();
            dto.FirstName = $"Patient{accountType}";
            dto.AccountType = accountType;

            var patientId = await _repository.CreateAsync(dto);
            var created = await _context.Patients.FindAsync(patientId);
            created!.AccountType.Should().Be(accountType, $"Account type {accountType} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithAllGenders_ReturnsCorrectPatients()
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
            var dto = CreateMinimalPatientDto();
            dto.FirstName = $"Patient{gender}";
            dto.Gender = gender;

            var patientId = await _repository.CreateAsync(dto);
            var created = await _context.Patients.FindAsync(patientId);
            created!.Gender.Should().Be(gender, $"Gender {gender} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithAllAtsiStatuses_ReturnsCorrectPatients()
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
            var dto = CreateMinimalPatientDto();
            dto.FirstName = $"Patient{status}";
            dto.AtsiStatus = status;

            var patientId = await _repository.CreateAsync(dto);
            var created = await _context.Patients.FindAsync(patientId);
            created!.AtsiStatus.Should().Be(status, $"ATSI status {status} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithAllMaritalStatuses_ReturnsCorrectPatients()
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
            var dto = CreateMinimalPatientDto();
            dto.FirstName = $"Patient{status}";
            dto.MaritalStatus = status;

            var patientId = await _repository.CreateAsync(dto);
            var created = await _context.Patients.FindAsync(patientId);
            created!.MaritalStatus.Should().Be(status, $"Marital status {status} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithAllDobAccuracies_ReturnsCorrectPatients()
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
            var dto = CreateMinimalPatientDto();
            dto.FirstName = $"Patient{accuracy}";
            dto.DobAccuracy = accuracy;

            var patientId = await _repository.CreateAsync(dto);
            var created = await _context.Patients.FindAsync(patientId);
            created!.DobAccuracy.Should().Be(accuracy, $"DOB accuracy {accuracy} should be saved correctly");
        }
    }

    [Fact]
    public async Task CreateAsync_WithMedicareDetails_ReturnsPatientWithMedicare()
    {
        // Arrange
        var dto = CreateMinimalPatientDto();
        dto.MedicareNumber = "9876543210";
        dto.MedicarePosition = "1";
        dto.MedicareExpiryDate = new DateTime(2027, 12, 31);
        dto.MedicareIncentiveEligible = true;
        dto.CtgCoPaymentRelief = true;

        // Act
        var patientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Patients.FindAsync(patientId);
        created!.MedicareNumber.Should().Be("9876543210");
        created.MedicarePosition.Should().Be("1");
        created.MedicareExpiryDate.Should().Be(new DateTime(2027, 12, 31));
        created.MedicareIncentiveEligible.Should().BeTrue();
        created.CtgCoPaymentRelief.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithDvaDetails_ReturnsPatientWithDva()
    {
        // Arrange
        var dto = CreateMinimalPatientDto();
        dto.DvaNumber = "DVA123456";
        dto.DvaExpiryDate = new DateTime(2028, 6, 30);

        // Act
        var patientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Patients.FindAsync(patientId);
        created!.DvaNumber.Should().Be("DVA123456");
        created.DvaExpiryDate.Should().Be(new DateTime(2028, 6, 30));
    }

    [Fact]
    public async Task CreateAsync_WithPensionDetails_ReturnsPatientWithPension()
    {
        // Arrange
        var dto = CreateMinimalPatientDto();
        dto.PensionNumber = "PEN123456";
        dto.PensionExpiryDate = new DateTime(2027, 3, 31);
        dto.AccountType = AccountTypeEnum.Pensioner;

        // Act
        var patientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Patients.FindAsync(patientId);
        created!.PensionNumber.Should().Be("PEN123456");
        created.PensionExpiryDate.Should().Be(new DateTime(2027, 3, 31));
        created.AccountType.Should().Be(AccountTypeEnum.Pensioner);
    }

    [Fact]
    public async Task CreateAsync_WithWorkCover_ReturnsWorkCoverPatient()
    {
        // Arrange
        var dto = CreateMinimalPatientDto();
        dto.AccountType = AccountTypeEnum.WorkCover;
        dto.Notes = "WorkCover claim in progress";

        // Act
        var patientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Patients.FindAsync(patientId);
        created!.AccountType.Should().Be(AccountTypeEnum.WorkCover);
        created.Notes.Should().Be("WorkCover claim in progress");
    }

    [Fact]
    public async Task CreateAsync_WithTac_ReturnsTacPatient()
    {
        // Arrange
        var dto = CreateMinimalPatientDto();
        dto.AccountType = AccountTypeEnum.Tac;
        dto.Notes = "Motor vehicle accident - TAC claim";

        // Act
        var patientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _context.Patients.FindAsync(patientId);
        created!.AccountType.Should().Be(AccountTypeEnum.Tac);
        created.Notes.Should().Be("Motor vehicle accident - TAC claim");
    }

    [Fact]
    public async Task CreateAsync_WithFullDetails_ReturnsCompletePatient()
    {
        // Arrange
        var dto = CreateFullPatientDto();

        // Act
        var patientId = await _repository.CreateAsync(dto);

        // Assert
        var created = await _repository.GetByIdAsync(patientId);
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

    #region Comprehensive Patient Retrieval Tests

    [Fact]
    public async Task GetByIdAsync_AfterMultipleCreates_ReturnsCorrectPatient()
    {
        // Arrange
        var dto1 = CreateMinimalPatientDto();
        dto1.FirstName = "First";
        dto1.LastName = "Patient";

        var dto2 = CreateMinimalPatientDto();
        dto2.FirstName = "Second";
        dto2.LastName = "Patient";

        var dto3 = CreateMinimalPatientDto();
        dto3.FirstName = "Third";
        dto3.LastName = "Patient";

        var id1 = await _repository.CreateAsync(dto1);
        var id2 = await _repository.CreateAsync(dto2);
        var id3 = await _repository.CreateAsync(dto3);

        // Act
        var patient1 = await _repository.GetByIdAsync(id1);
        var patient2 = await _repository.GetByIdAsync(id2);
        var patient3 = await _repository.GetByIdAsync(id3);

        // Assert
        patient1!.FirstName.Should().Be("First");
        patient2!.FirstName.Should().Be("Second");
        patient3!.FirstName.Should().Be("Third");
    }

    [Fact]
    public async Task GetByIdAsync_WithFullPatient_ReturnsAllFields()
    {
        // Arrange
        var dto = CreateFullPatientDto();
        var patientId = await _repository.CreateAsync(dto);

        // Act
        var patient = await _repository.GetByIdAsync(patientId);

        // Assert
        patient.Should().NotBeNull();
        patient!.FirstName.Should().Be("John");
        patient.MiddleName.Should().Be("Michael");
        patient.PreferredName.Should().Be("Johnny");
        patient.InterpreterRequired.Should().BeTrue();
        patient.InterpreterLanguage.Should().Be("Italian");
        patient.HealthFundCode.Should().Be("BUP");
        patient.HealthFundNumber.Should().Be("BUPA123456");
        patient.IhiNumber.Should().Be("IHI123456789");
    }

    [Fact]
    public async Task GetByIdAsync_WithArchivedPatient_ReturnsPatient()
    {
        // Arrange
        var dto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(dto);
        await _repository.ArchiveAsync(patientId); // Archive instead of delete

        // Act
        var patient = await _repository.GetByIdAsync(patientId);

        // Assert - GetById should still return the patient even if archived
        patient.Should().NotBeNull();
        patient!.IsActive.Should().BeFalse();
    }

    #endregion

    #region Comprehensive Patient Update Tests

    [Fact]
    public async Task UpdateAsync_UpdatesBasicFields()
    {
        // Arrange
        var createDto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdatePatientDto
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
        await _repository.UpdateAsync(patientId, updateDto);

        // Assert
        var updated = await _context.Patients.FindAsync(patientId);
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
        var createDto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdatePatientDto
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
        await _repository.UpdateAsync(patientId, updateDto);

        // Assert
        var updated = await _context.Patients.FindAsync(patientId);
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
        var createDto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdatePatientDto
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
        await _repository.UpdateAsync(patientId, updateDto);

        // Assert
        var updated = await _context.Patients.FindAsync(patientId);
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
        var createDto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdatePatientDto
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
        await _repository.UpdateAsync(patientId, updateDto);

        // Assert
        var updated = await _repository.GetByIdAsync(patientId);
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
        var createDto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdatePatientDto
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
        await _repository.UpdateAsync(patientId, updateDto);

        // Assert
        var updated = await _context.Patients.FindAsync(patientId);
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
        var createDto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdatePatientDto
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
        await _repository.UpdateAsync(patientId, updateDto);

        // Assert
        var updated = await _context.Patients.FindAsync(patientId);
        updated!.FileNumber.Should().Be("FILE999");
        updated.UrNumber.Should().Be("UR123456");
        updated.LastSeenDate.Should().Be(new DateTime(2026, 7, 20));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEhealthDetails()
    {
        // Arrange
        var createDto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(createDto);

        var updateDto = new UpdatePatientDto
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
        await _repository.UpdateAsync(patientId, updateDto);

        // Assert
        var updated = await _context.Patients.FindAsync(patientId);
        updated!.IhiNumber.Should().Be("IHI999888777");
        updated.IhiRecordStatus.Should().Be("Verified");
        updated.IhiAssignedDate.Should().Be(new DateTime(2026, 1, 15));
        updated.IhiNumberStatus.Should().Be("Active");
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAtTimestamp()
    {
        // Arrange
        var createDto = CreateMinimalPatientDto();
        var patientId = await _repository.CreateAsync(createDto);
        var created = await _context.Patients.FindAsync(patientId);
        var originalCreatedAt = created!.CreatedAt;

        await Task.Delay(10); // Small delay to ensure different timestamp

        var updateDto = new UpdatePatientDto
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
        await _repository.UpdateAsync(patientId, updateDto);

        // Assert
        var updated = await _context.Patients.FindAsync(patientId);
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
            var dto = CreateMinimalPatientDto();
            var patientId = await _repository.CreateAsync(dto);

            var updateDto = new UpdatePatientDto
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

            await _repository.UpdateAsync(patientId, updateDto);

            var updated = await _context.Patients.FindAsync(patientId);
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
            var dto = CreateMinimalPatientDto();
            var patientId = await _repository.CreateAsync(dto);

            var updateDto = new UpdatePatientDto
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

            await _repository.UpdateAsync(patientId, updateDto);

            var updated = await _context.Patients.FindAsync(patientId);
            updated!.Gender.Should().Be(gender, $"Gender {gender} should be updated correctly");
        }
    }

    #endregion

    #region Helper Methods

    private static CreatePatientDto CreateMinimalPatientDto()
    {
        return new CreatePatientDto
        {
            FirstName = "Test",
            LastName = "Patient",
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

    private static CreatePatientDto CreateFullPatientDto()
    {
        return new CreatePatientDto
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
            Notes = "Regular patient",
            Warnings = null,
            NextOfKinPatientId = null,
            NextOfKinName = "Mary Doe",
            NextOfKinPhone = "0412345679",
            EmergencyContactPatientId = null,
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
            PayerPatientId = null,
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