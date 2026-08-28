using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class SystemSettingsRepositoryTests : IDisposable
{
    private readonly SystemSettingsRepository _repository;
    private readonly AppDbContext _context;

    public SystemSettingsRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new SystemSettingsRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    private static UpdateSystemSettingsDto ValidDto() => new()
    {
        PasswordMinLength = 10,
        PasswordRequireDigit = true,
        PasswordRequireLowercase = true,
        PasswordRequireUppercase = false,
        PasswordRequireNonAlphanumeric = false,
        LockoutMaxFailedAttempts = 3,
        LockoutDurationMinutes = 30,
        SessionExpirationHours = 12,
        DefaultAppointmentSlotMinutes = 20,
        NewUserShowCommandPalette = false,
        BackupRetentionDays = 90
    };

    [Fact]
    public async Task GetAsync_ReturnsTheSeededDefaults()
    {
        var settings = await _repository.GetAsync();

        // These must match the values previously hardcoded in Program.cs and the backup service —
        // seeding is meant to be behaviour-neutral.
        settings.PasswordMinLength.Should().Be(8);
        settings.PasswordRequireDigit.Should().BeTrue();
        settings.PasswordRequireLowercase.Should().BeTrue();
        settings.PasswordRequireUppercase.Should().BeTrue();
        settings.PasswordRequireNonAlphanumeric.Should().BeTrue();
        settings.LockoutMaxFailedAttempts.Should().Be(5);
        settings.LockoutDurationMinutes.Should().Be(15);
        settings.SessionExpirationHours.Should().Be(8);
        settings.BackupRetentionDays.Should().Be(60);
    }

    [Fact]
    public async Task UpdateAsync_RoundTripsEveryField()
    {
        await _repository.UpdateAsync(ValidDto());

        var settings = await _repository.GetAsync();
        settings.PasswordMinLength.Should().Be(10);
        settings.PasswordRequireUppercase.Should().BeFalse();
        settings.LockoutMaxFailedAttempts.Should().Be(3);
        settings.LockoutDurationMinutes.Should().Be(30);
        settings.SessionExpirationHours.Should().Be(12);
        settings.DefaultAppointmentSlotMinutes.Should().Be(20);
        settings.NewUserShowCommandPalette.Should().BeFalse();
        settings.BackupRetentionDays.Should().Be(90);
        settings.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(UpdateSystemSettingsDto.PasswordMinLength), 5)]
    [InlineData(nameof(UpdateSystemSettingsDto.PasswordMinLength), 129)]
    [InlineData(nameof(UpdateSystemSettingsDto.LockoutMaxFailedAttempts), 0)]
    [InlineData(nameof(UpdateSystemSettingsDto.LockoutDurationMinutes), 0)]
    [InlineData(nameof(UpdateSystemSettingsDto.SessionExpirationHours), 0)]
    [InlineData(nameof(UpdateSystemSettingsDto.DefaultAppointmentSlotMinutes), 4)]
    [InlineData(nameof(UpdateSystemSettingsDto.BackupRetentionDays), 0)]
    public async Task UpdateAsync_RejectsOutOfRangeValues(string field, int badValue)
    {
        var dto = ValidDto();
        typeof(UpdateSystemSettingsDto).GetProperty(field)!.SetValue(dto, badValue);

        var act = async () => await _repository.UpdateAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "an out-of-range policy value is rejected outright rather than silently clamped");
    }

    [Fact]
    public async Task UpdateAsync_RejectingAValue_LeavesTheStoredSettingsUntouched()
    {
        var dto = ValidDto();
        dto.BackupRetentionDays = 0;

        var act = async () => await _repository.UpdateAsync(dto);
        await act.Should().ThrowAsync<InvalidOperationException>();

        var settings = await _repository.GetAsync();
        settings.BackupRetentionDays.Should().Be(60, "the rejected save must not partially apply");
        settings.PasswordMinLength.Should().Be(8);
    }

    [Fact]
    public async Task UpdateAsync_AcceptsTheBoundaryValues()
    {
        var dto = ValidDto();
        dto.PasswordMinLength = 6;
        dto.LockoutMaxFailedAttempts = 1;
        dto.BackupRetentionDays = 3650;

        var act = async () => await _repository.UpdateAsync(dto);

        await act.Should().NotThrowAsync();
    }
}
