using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientAddressRepositoryTests : IDisposable
{
    private readonly ClientAddressRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;

    public ClientAddressRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientAddressRepository(_context);

        var client = new Client { FirstName = "Addr", LastName = "Test" };
        _context.Clients.Add(client);
        _context.SaveChanges();
        _clientId = client.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewAddressId()
    {
        var dto = new CreateClientAddressDto
        {
            ClientId = _clientId,
            AddressType = ClientAddressType.Postal,
            AddressLine1 = "PO Box 1",
            Suburb = "Sometown",
            Postcode = "3000",
            State = "VIC"
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var address = await _context.ClientAddresses.FindAsync(result);
        address.Should().NotBeNull();
        address!.AddressType.Should().Be(ClientAddressType.Postal);
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsOnlyThatClientsAddresses()
    {
        _context.ClientAddresses.Add(new ClientAddress
        {
            ClientId = _clientId,
            AddressType = ClientAddressType.Other,
            AddressLine1 = "1 Other St",
            Suburb = "X",
            Postcode = "1000",
            State = "NSW"
        });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().HaveCount(1);
        result[0].AddressType.Should().Be(ClientAddressType.Other);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingAddress_ThrowsException()
    {
        var update = new UpdateClientAddressDto { AddressLine1 = "x", Suburb = "x", Postcode = "x", State = "x" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateAsync(999, update));
    }
}
