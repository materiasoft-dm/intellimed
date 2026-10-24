using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Hl7;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Services;

public class Hl7ResultParserTests
{
    private readonly Hl7ResultParser _parser = new();

    [Fact]
    public void Parse_FullBloodCount_ReadsHeaderAndPatient()
    {
        var r = _parser.Parse(Hl7Fixtures.FullBloodCount());

        r.SenderName.Should().Be("ACME PATHOLOGY");
        r.MessageControlId.Should().Be("MSG0001");
        r.PatientFamilyName.Should().Be("Citizen");
        r.PatientGivenName.Should().Be("Jane");
        r.PatientDateOfBirth.Should().Be(new DateTime(1980, 3, 15));
        r.PatientSex.Should().Be("F");
        r.PatientIdentifier.Should().Be("LAB12345");
        r.PatientMedicareNumber.Should().Be("2123456701");
    }

    [Fact]
    public void Parse_FullBloodCount_ReadsOrderAndProvider()
    {
        var r = _parser.Parse(Hl7Fixtures.FullBloodCount());

        r.PlacerOrderNumber.Should().Be("ORD555");
        r.FillerOrderNumber.Should().Be("ACC9001");
        r.ReportName.Should().Be("Full Blood Count");
        r.Kind.Should().Be(ResultReportKind.Pathology);
        r.CollectedAt.Should().Be(new DateTime(2026, 3, 9, 8, 30, 0));
        r.ReportedAt.Should().Be(new DateTime(2026, 3, 10, 9, 30, 0));
        r.OrderingProviderNumber.Should().Be("2345678A");
        r.OrderingProviderName.Should().Be("Dr Sarah Chen");
    }

    [Fact]
    public void Parse_FullBloodCount_ReadsItemsFlagsAndComment()
    {
        var r = _parser.Parse(Hl7Fixtures.FullBloodCount());

        r.Items.Should().HaveCount(2);
        var hb = r.Items[0];
        hb.Code.Should().Be("HB");
        hb.Name.Should().Be("Haemoglobin");
        hb.Value.Should().Be("108");
        hb.Units.Should().Be("g/L");
        hb.ReferenceRange.Should().Be("115-165");
        hb.AbnormalFlag.Should().Be("L");
        hb.IsAbnormal.Should().BeTrue();
        hb.Comment.Should().Be("Mild anaemia - suggest iron studies.");
        hb.Sequence.Should().Be(1);

        var wcc = r.Items[1];
        wcc.IsAbnormal.Should().BeFalse("N means normal");
        wcc.Comment.Should().BeNull("the note followed the haemoglobin, not this line");
        wcc.Units.Should().Be("x10^9/L", "the \\S\\ escape is the component separator");
    }

    [Fact]
    public void Parse_EmbeddedPdf_BecomesAnAttachment()
    {
        var r = _parser.Parse(Hl7Fixtures.FullBloodCount());

        r.Attachments.Should().ContainSingle();
        r.Attachments[0].ContentType.Should().Be("application/pdf");
        r.Attachments[0].FileName.Should().EndWith(".pdf");
        System.Text.Encoding.ASCII.GetString(r.Attachments[0].Data).Should().StartWith("%PDF-1.4");
    }

    [Fact]
    public void Parse_RadiologyReport_IsImagingAndUnescapesLineBreaks()
    {
        var r = _parser.Parse(Hl7Fixtures.ChestXray());

        r.Kind.Should().Be(ResultReportKind.Imaging);
        r.Items.Should().ContainSingle();
        r.Items[0].Value.Should().Be("Lungs clear.\nNo acute abnormality.");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Parse_ToleratesOtherSegmentTerminators(string terminator)
    {
        var message = Hl7Fixtures.FullBloodCount().Replace("\r", terminator);

        var r = _parser.Parse(message);

        r.Items.Should().HaveCount(2);
        r.PatientFamilyName.Should().Be("Citizen");
    }

    [Fact]
    public void Parse_ToleratesMllpFraming()
    {
        var framed = "\u000B" + Hl7Fixtures.FullBloodCount() + "\u001C\r";

        var r = _parser.Parse(framed);

        r.MessageControlId.Should().Be("MSG0001");
    }

    [Fact]
    public void Parse_CorruptAttachment_KeepsTheStructuredResults()
    {
        var message = Hl7Fixtures.FullBloodCount().Replace("JVBERi0xLjQKJSVFT0YK", "!!!not-base64!!!");

        var r = _parser.Parse(message);

        r.Attachments.Should().BeEmpty();
        r.Items.Should().Contain(i => i.Code == "HB");
    }

    [Fact]
    public void Parse_NonResultMessage_IsRejected()
    {
        var act = () => _parser.Parse(Hl7Fixtures.AdtMessage());

        act.Should().Throw<Hl7ParseException>().WithMessage("*ORU*");
    }

    [Fact]
    public void Parse_MessageWithoutPatient_IsRejected()
    {
        var act = () => _parser.Parse(Hl7Fixtures.NoPatient());

        act.Should().Throw<Hl7ParseException>().WithMessage("*PID*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not hl7")]
    public void Parse_NotHl7_IsRejected(string text)
    {
        var act = () => _parser.Parse(text);

        act.Should().Throw<Hl7ParseException>();
    }

    [Theory]
    [InlineData("20260309", 2026, 3, 9, 0, 0)]
    [InlineData("202603090830", 2026, 3, 9, 8, 30)]
    [InlineData("20260309083015.1234+1100", 2026, 3, 9, 8, 30)]
    public void ParseTimestamp_HandlesHl7Precisions(string value, int y, int mo, int d, int h, int mi)
    {
        Hl7ResultParser.ParseTimestamp(value).Should().Be(new DateTime(y, mo, d, h, mi, value.Length >= 14 ? 15 : 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void ParseTimestamp_ReturnsNullWhenUnparseable(string? value)
    {
        Hl7ResultParser.ParseTimestamp(value).Should().BeNull();
    }
}
