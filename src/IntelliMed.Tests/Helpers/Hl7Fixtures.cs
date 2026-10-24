namespace IntelliMed.Tests.Helpers;

/// <summary>Synthetic HL7 v2 ORU^R01 messages. No real patient data — names, numbers and DOBs are made up.</summary>
public static class Hl7Fixtures
{
    private const string Cr = "\r";

    /// <summary>Builds a segment with each value placed at its HL7 field number, so the fixtures
    /// can't drift by an off-by-one pipe count.</summary>
    private static string Seg(string name, params (int Field, string Value)[] fields)
    {
        var parts = new string[fields.Max(f => f.Field) + 1];
        Array.Fill(parts, string.Empty);
        parts[0] = name;
        foreach (var (field, value) in fields) parts[field] = value;
        return string.Join("|", parts);
    }

    /// <summary>A Full Blood Count with one low and one normal value, a comment and an embedded PDF.</summary>
    public static string FullBloodCount(
        string controlId = "MSG0001",
        string family = "Citizen", string given = "Jane",
        string dob = "19800315", string medicare = "2123456701", string labMrn = "LAB12345",
        string orderingNumber = "2345678A", string orderingFamily = "Chen", string orderingGiven = "Sarah",
        string sender = "ACME PATHOLOGY") =>
        string.Join(Cr, new[]
        {
            $"MSH|^~\\&|LIS|{sender}|IntelliMed|CLINIC|20260310101500||ORU^R01|{controlId}|P|2.3.1",
            $"PID|1||{labMrn}^^^ACME^MR~{medicare}^^^AUSHIC^MC||{family}^{given}||{dob}|F",
            Seg("OBR", (1, "1"), (2, "ORD555"), (3, "ACC9001"), (4, "FBC^Full Blood Count"), (7, "20260309083000"),
                (16, $"{orderingNumber}^{orderingFamily}^{orderingGiven}^^^Dr"), (22, "20260310093000"), (25, "F")),
            "OBX|1|NM|HB^Haemoglobin||108|g/L|115-165|L|||F",
            "NTE|1||Mild anaemia - suggest iron studies.",
            "OBX|2|NM|WCC^White Cell Count||6.4|x10\\S\\9/L|4.0-11.0|N|||F",
            "OBX|3|ED|PDF^Report||^application^pdf^Base64^JVBERi0xLjQKJSVFT0YK||||||F",
            ""
        });

    /// <summary>A message with no patient segment.</summary>
    public static string NoPatient() =>
        string.Join(Cr, new[]
        {
            "MSH|^~\\&|LIS|ACME|IntelliMed|CLINIC|20260310101500||ORU^R01|NOPID1|P|2.3.1",
            "OBR|1|||FBC^Full Blood Count",
            "OBX|1|NM|HB^Haemoglobin||108|g/L|115-165|L|||F",
            ""
        });

    /// <summary>A valid HL7 message that is not a result (an admission).</summary>
    public static string AdtMessage() =>
        string.Join(Cr, new[]
        {
            "MSH|^~\\&|HIS|HOSP|IntelliMed|CLINIC|20260310101500||ADT^A01|ADT1|P|2.3.1",
            "PID|1||123^^^HOSP^MR||Citizen^Jane||19800315|F",
            ""
        });

    /// <summary>A radiology report: narrative text with an HL7 line break escape.</summary>
    public static string ChestXray() =>
        string.Join(Cr, new[]
        {
            "MSH|^~\\&|RIS|CITY RADIOLOGY|IntelliMed|CLINIC|20260311120000||ORU^R01|RAD0001|P|2.3.1",
            "PID|1||RAD777^^^CITYRAD^MR||Citizen^Jane||19800315|F",
            Seg("OBR", (1, "1"), (2, "ORD600"), (3, "RACC1"), (4, "CXR^Chest X-ray"), (7, "20260311110000"), (24, "RAD"), (25, "F")),
            "OBX|1|TX|CXR^Chest X-ray||Lungs clear.\\.br\\No acute abnormality.||||||F",
            ""
        });
}
