using System.Globalization;
using System.Xml.Linq;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Imports the Australian MBS (Medicare Benefits Schedule) item catalog from the free, official
/// XML feed published quarterly at mbsonline.gov.au. Triggered on demand from Clinic Settings
/// rather than seeded via migration, since the feed URL and contents change every quarter.
/// </summary>
public class BillingItemSyncService : IBillingItemSyncService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public BillingItemSyncService(HttpClient httpClient, AppDbContext context, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _context = context;
        _configuration = configuration;
    }

    public async Task<BillingItemSyncResultDto> SyncAsync()
    {
        var url = _configuration["Mbs:XmlUrl"]
            ?? throw new InvalidOperationException("Mbs:XmlUrl is not configured.");

        await using var stream = await _httpClient.GetStreamAsync(url);
        var document = await Task.Run(() => XDocument.Load(stream));

        // Keyed by ItemNumber so any duplicate <Data> rows in the feed (e.g. sub-item variants)
        // collapse to the last-seen record instead of colliding on the unique index at save time.
        var parsedItems = new Dictionary<string, BillingItem>();
        foreach (var data in document.Root?.Elements("Data") ?? Enumerable.Empty<XElement>())
        {
            var itemNumber = (string?)data.Element("ItemNum");
            if (string.IsNullOrWhiteSpace(itemNumber)) continue;

            parsedItems[itemNumber] = new BillingItem
            {
                ItemNumber = itemNumber,
                Category = (string?)data.Element("Category"),
                Group = (string?)data.Element("Group"),
                SubGroup = (string?)data.Element("SubGroup"),
                Description = (string?)data.Element("Description") ?? string.Empty,
                ScheduleFee = ParseDecimal((string?)data.Element("ScheduleFee")),
                Benefit100 = ParseNullableDecimal((string?)data.Element("Benefit100")),
                ItemStartDate = ParseDate((string?)data.Element("ItemStartDate")),
                ItemEndDate = ParseDate((string?)data.Element("ItemEndDate")),
                IsActive = ParseDate((string?)data.Element("ItemEndDate")) is null
            };
        }

        var existing = await _context.BillingItems.ToDictionaryAsync(b => b.ItemNumber);
        var now = DateTime.UtcNow;
        int added = 0, updated = 0, deactivated = 0;

        foreach (var (itemNumber, parsed) in parsedItems)
        {
            if (existing.TryGetValue(itemNumber, out var current))
            {
                if (current.Description != parsed.Description ||
                    current.ScheduleFee != parsed.ScheduleFee ||
                    current.Benefit100 != parsed.Benefit100 ||
                    current.Category != parsed.Category ||
                    current.Group != parsed.Group ||
                    current.SubGroup != parsed.SubGroup ||
                    current.ItemEndDate != parsed.ItemEndDate ||
                    current.IsActive != parsed.IsActive)
                {
                    current.Category = parsed.Category;
                    current.Group = parsed.Group;
                    current.SubGroup = parsed.SubGroup;
                    current.Description = parsed.Description;
                    current.ScheduleFee = parsed.ScheduleFee;
                    current.Benefit100 = parsed.Benefit100;
                    current.ItemStartDate = parsed.ItemStartDate;
                    current.ItemEndDate = parsed.ItemEndDate;
                    current.IsActive = parsed.IsActive;
                    updated++;
                }
                current.LastSyncedAt = now;
            }
            else
            {
                parsed.LastSyncedAt = now;
                _context.BillingItems.Add(parsed);
                added++;
            }
        }

        // Items that existed before but are no longer present in this quarter's feed are retired.
        foreach (var (itemNumber, current) in existing)
        {
            if (!parsedItems.ContainsKey(itemNumber) && current.IsActive)
            {
                current.IsActive = false;
                current.LastSyncedAt = now;
                deactivated++;
            }
        }

        await _context.SaveChangesAsync();

        return new BillingItemSyncResultDto
        {
            Added = added,
            Updated = updated,
            Deactivated = deactivated,
            SyncedAt = now
        };
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;

    private static decimal? ParseNullableDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : null;
}
