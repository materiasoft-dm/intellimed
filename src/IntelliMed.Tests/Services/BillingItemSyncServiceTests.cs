using System.Net;
using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;

namespace IntelliMed.Tests.Services;

public class BillingItemSyncServiceTests : IDisposable
{
    private readonly AppDbContext _context;

    public BillingItemSyncServiceTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
    }

    public void Dispose() => _context.Dispose();

    private static BillingItemSyncService BuildService(AppDbContext context, string xml)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(xml)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var configData = new Dictionary<string, string?> { ["Mbs:XmlUrl"] = "https://example.test/mbs.xml" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        return new BillingItemSyncService(httpClient, context, configuration);
    }

    private static string BuildFeedXml(params (string ItemNum, string Description, decimal Fee, string? EndDate)[] items)
    {
        var dataElements = items.Select(i => $"""
            <Data><ItemNum>{i.ItemNum}</ItemNum><Category>1</Category><Group>A1</Group>
            <Description>{i.Description}</Description><ScheduleFee>{i.Fee}</ScheduleFee><Benefit100>{i.Fee}</Benefit100>
            <ItemStartDate>01.01.2020</ItemStartDate><ItemEndDate>{i.EndDate}</ItemEndDate></Data>
            """);
        return $"<MBS_XML>{string.Join("", dataElements)}</MBS_XML>";
    }

    [Fact]
    public async Task SyncAsync_WithNoExistingItems_AddsAllItemsFromFeed()
    {
        var xml = BuildFeedXml(("23", "Standard consultation", 41.40m, null), ("36", "Long consultation", 79.70m, null));
        var service = BuildService(_context, xml);

        var result = await service.SyncAsync();

        result.Added.Should().Be(2);
        result.Updated.Should().Be(0);
        result.Deactivated.Should().Be(0);
        _context.BillingItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncAsync_WithChangedFee_UpdatesExistingItem()
    {
        _context.BillingItems.Add(new BillingItem { ItemNumber = "23", Description = "Standard consultation", ScheduleFee = 40.00m, IsActive = true });
        await _context.SaveChangesAsync();

        var xml = BuildFeedXml(("23", "Standard consultation", 41.40m, null));
        var service = BuildService(_context, xml);

        var result = await service.SyncAsync();

        result.Added.Should().Be(0);
        result.Updated.Should().Be(1);
        result.Deactivated.Should().Be(0);
        (await _context.BillingItems.FindAsync(_context.BillingItems.First().Id))!.ScheduleFee.Should().Be(41.40m);
    }

    [Fact]
    public async Task SyncAsync_WithItemMissingFromFeed_DeactivatesIt()
    {
        _context.BillingItems.Add(new BillingItem { ItemNumber = "999", Description = "Retired item", ScheduleFee = 10.00m, IsActive = true });
        await _context.SaveChangesAsync();

        var xml = BuildFeedXml(("23", "Standard consultation", 41.40m, null));
        var service = BuildService(_context, xml);

        var result = await service.SyncAsync();

        result.Added.Should().Be(1);
        result.Deactivated.Should().Be(1);
        var retired = _context.BillingItems.Single(b => b.ItemNumber == "999");
        retired.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SyncAsync_ItemWithPastEndDate_IsMarkedInactive()
    {
        var xml = BuildFeedXml(("23", "Discontinued item", 41.40m, "01.01.2021"));
        var service = BuildService(_context, xml);

        await service.SyncAsync();

        var item = _context.BillingItems.Single(b => b.ItemNumber == "23");
        item.IsActive.Should().BeFalse();
    }
}
