using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntelliMed.Tests.Services;

public class ArtgEnrichmentServiceTests : IDisposable
{
    private readonly ArtgEnrichmentService _service;
    private readonly AppDbContext _context;

    public ArtgEnrichmentServiceTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _service = new ArtgEnrichmentService(_context, NullLogger<ArtgEnrichmentService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    /// <summary>Mirrors the real export's shape: a filter banner, a blank line, then the header —
    /// so the parser has to find the header rather than assume row 1.</summary>
    private static StringReader Csv(params string[] dataRows)
    {
        var header = "ARTG ID,Product Name,Sponsor Name,Active Ingredients,Poison Schedule,Dosage Form,Route of Administration";
        var body = string.Join("\n", dataRows);
        return new StringReader($"Applied filters: Indication is All Medicines\n\n{header}\n{body}\n");
    }

    private Medicine AddMedicine(string name, MedicineSchedule? schedule = null, string? form = null, string? route = null)
    {
        var medicine = new Medicine { Name = name, IsActive = true, Schedule = schedule, Form = form, Route = route };
        _context.Medicines.Add(medicine);
        _context.SaveChanges();
        return medicine;
    }

    [Fact]
    public async Task EnrichAsync_FillsScheduleFromMatchingBrand()
    {
        var medicine = AddMedicine("GABAPENTIN-WGR");

        var result = await _service.EnrichAsync(Csv(
            "525450,GABAPENTIN-WGR gabapentin 600 mg film coated tablets blister pack,Micro Labs,gabapentin,(S4) Prescription Only Medicine,\"Tablet, film coated\",Oral"));

        result.Matched.Should().Be(1);
        result.ScheduleFilled.Should().Be(1);
        _context.Medicines.Find(medicine.Id)!.Schedule.Should().Be(MedicineSchedule.S4);
    }

    [Fact]
    public async Task EnrichAsync_FillsArtgIdRouteAndForm()
    {
        var medicine = AddMedicine("GABAPENTIN-WGR");

        await _service.EnrichAsync(Csv(
            "525450,GABAPENTIN-WGR gabapentin 600 mg tablets,Micro Labs,gabapentin,(S4) Prescription Only Medicine,\"Tablet, film coated\",Oral"));

        var updated = _context.Medicines.Find(medicine.Id)!;
        updated.ArtgId.Should().Be("525450");
        updated.Route.Should().Be("Oral");
        updated.Form.Should().Be("Tablet, film coated");
    }

    [Fact]
    public async Task EnrichAsync_NeverOverwritesAnExistingValue()
    {
        // A curated schedule, or one from a more authoritative source, outranks a brand-level match.
        var medicine = AddMedicine("ENDONE", schedule: MedicineSchedule.S8, form: "Curated form", route: "Curated route");

        var result = await _service.EnrichAsync(Csv(
            "12345,ENDONE oxycodone 5 mg tablet,Aspen,oxycodone,(S4) Prescription Only Medicine,Tablet,Sublingual"));

        var updated = _context.Medicines.Find(medicine.Id)!;
        updated.Schedule.Should().Be(MedicineSchedule.S8);
        updated.Form.Should().Be("Curated form");
        updated.Route.Should().Be("Curated route");
        result.ScheduleFilled.Should().Be(0);
    }

    [Fact]
    public async Task EnrichAsync_AcrossManyRegistrationsOfABrand_TakesTheStrictestSchedule()
    {
        // Different strengths of one brand can be scheduled differently. Over-prompting for a state
        // authority code is far cheaper than missing an S8 presentation.
        var medicine = AddMedicine("PANADEINE");

        await _service.EnrichAsync(Csv(
            "1,PANADEINE paracetamol 500 mg tablet,X,paracetamol,(S2) Pharmacy Medicine,Tablet,Oral",
            "2,PANADEINE FORTE codeine 30 mg tablet,X,codeine,(S8) Controlled Drug,Tablet,Oral",
            "3,PANADEINE codeine 15 mg tablet,X,codeine,(S4) Prescription Only Medicine,Tablet,Oral"));

        _context.Medicines.Find(medicine.Id)!.Schedule.Should().Be(MedicineSchedule.S8);
    }

    [Fact]
    public async Task EnrichAsync_WithinOneCellNamingSeveralSchedules_TakesTheStrictest()
    {
        var medicine = AddMedicine("MIXEDSCHED");

        await _service.EnrichAsync(Csv(
            "9,MIXEDSCHED something 10 mg,X,thing,\"(S2) Pharmacy Medicine, (S3) Pharmacist Only Medicine\",Tablet,Oral"));

        _context.Medicines.Find(medicine.Id)!.Schedule.Should().Be(MedicineSchedule.S3);
    }

    [Fact]
    public async Task EnrichAsync_UnmatchedBrandsAreCountedNotGuessed()
    {
        AddMedicine("acetylleucine");   // generic-name row from a non-PBS source — no brand to match

        var result = await _service.EnrichAsync(Csv(
            "1,GABAPENTIN-WGR gabapentin 600 mg,X,gabapentin,(S4) Prescription Only Medicine,Tablet,Oral"));

        result.Unmatched.Should().Be(1);
        result.Matched.Should().Be(0);
        _context.Medicines.Single().Schedule.Should().BeNull();
    }

    [Fact]
    public async Task EnrichAsync_HandlesQuotedFieldsContainingCommasAndNewlines()
    {
        // Indications and Warnings routinely contain both. A line-based parser mis-aligns every
        // column after one of these.
        var medicine = AddMedicine("TRICKY");
        var csv = new StringReader(
            "Applied filters: x\n\n" +
            "ARTG ID,Product Name,Indications,Poison Schedule,Dosage Form,Route of Administration\n" +
            "77,TRICKY thing 5 mg,\"Line one, with comma\nand a newline\",(S8) Controlled Drug,Capsule,Oral\n");

        var result = await _service.EnrichAsync(csv);

        result.ArtgRows.Should().Be(1);
        _context.Medicines.Find(medicine.Id)!.Schedule.Should().Be(MedicineSchedule.S8);
    }

    [Fact]
    public async Task EnrichAsync_ReportsScheduleBreakdownForSanityChecking()
    {
        AddMedicine("ALPHA");
        AddMedicine("BETA");

        var result = await _service.EnrichAsync(Csv(
            "1,ALPHA a 1 mg,X,a,(S8) Controlled Drug,Tablet,Oral",
            "2,BETA b 1 mg,X,b,(S4) Prescription Only Medicine,Tablet,Oral"));

        result.ScheduleBreakdown.Should().ContainKey("S8").WhoseValue.Should().Be(1);
        result.ScheduleBreakdown.Should().ContainKey("S4").WhoseValue.Should().Be(1);
    }

    [Theory]
    [InlineData("(S8) Controlled Drug", MedicineSchedule.S8)]
    [InlineData("(S4) Prescription Only Medicine", MedicineSchedule.S4)]
    [InlineData("(S3) Pharmacist Only Medicine", MedicineSchedule.S3)]
    [InlineData("(S2) Pharmacy Medicine", MedicineSchedule.S2)]
    [InlineData("Not scheduled. Not considered by committee", MedicineSchedule.Unscheduled)]
    [InlineData("Not Scheduled after consideration by Committee", MedicineSchedule.Unscheduled)]
    public void MapSchedule_MapsTheExportsVocabulary(string raw, MedicineSchedule expected)
    {
        ArtgCsvParser.MapSchedule(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    // S1 is intentionally blank in the Poisons Standard — it carries no scheduling meaning.
    [InlineData("(S1) This Schedule is intentionally blank")]
    // S5/S6/S7/S9 cover poisons and prohibited substances, with no MedicineSchedule equivalent —
    // treated as unknown rather than forced into a value.
    [InlineData("(S6) Poison")]
    public void MapSchedule_ReturnsNullForValuesWithNoMedicineMeaning(string? raw)
    {
        ArtgCsvParser.MapSchedule(raw).Should().BeNull();
    }
}
