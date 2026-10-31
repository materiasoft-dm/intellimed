using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using IntelliMed.Api.Configuration;
using Xunit;

namespace IntelliMed.Tests.Services;

/// <summary>
/// "Detailed errors" for a test host. A real start failure under IIS only ever shows the bare
/// "HTTP Error 500.30"; with ASPNETCORE_DETAILEDERRORS=true the app instead serves the reason. These tests
/// start the real API with a deliberately fatal problem (Production with no JWT signing key — the exact
/// failure that produced 500.30 on the first deploy) and check what a browser would get.
/// </summary>
public class StartupDiagnosticsTests
{
    // ---- the report itself ----

    [Fact]
    public void Describe_NamesTheFailure_AndTheEnvironmentItRanIn()
    {
        var report = StartupDiagnostics.Describe(new InvalidOperationException("Jwt:Key is not configured."));

        report.Should().Contain("IntelliMed failed to start")
            .And.Contain("Jwt:Key is not configured.")
            .And.Contain("InvalidOperationException")
            .And.Contain("app directory")
            .And.Contain("database file")
            .And.Contain("app folder writable");
    }

    [Fact]
    public void Describe_NeverPrintsTheSigningKey_OnlyWhetherItIsSet()
    {
        const string secret = "TOP-SECRET-SIGNING-KEY-value-0123456789-abcdefghijklmnop";
        var before = Environment.GetEnvironmentVariable("Jwt__Key");
        Environment.SetEnvironmentVariable("Jwt__Key", secret);
        try
        {
            var report = StartupDiagnostics.Describe(new Exception("boom"));

            report.Should().NotContain(secret);
            report.Should().MatchRegex(@"signing key \(Jwt:Key\) configured\s+True");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Jwt__Key", before);
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("yes please", false)]
    public void TheOptIn_OnlyTurnsOn_ForTrueOr1(string? value, bool expected)
    {
        StartupDiagnostics.IsTruthy(value).Should().Be(expected);
    }

    // ---- the real app, started the way IIS would start it ----

    private static string ApiDll()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "IntelliMed.Api.dll");
        File.Exists(dll).Should().BeTrue("the API project is referenced by the test project, so its dll sits beside the tests");
        return dll;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Starts the API in Production with NO signing key — a fatal startup failure.</summary>
    private static Process StartWithoutSigningKey(int port, bool detailedErrors, string logPath)
    {
        var psi = new ProcessStartInfo("dotnet", $"\"{ApiDll()}\"")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var key in psi.Environment.Keys.Where(k => k.StartsWith("ASPNETCORE", StringComparison.OrdinalIgnoreCase)
                                                             || k.StartsWith("Jwt", StringComparison.OrdinalIgnoreCase)).ToList())
            psi.Environment.Remove(key);

        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["ASPNETCORE_URLS"] = $"http://localhost:{port}";
        // Keep this throwaway process off the shared development database.
        psi.Environment["ConnectionStrings__DefaultConnection"] = $"Data Source={Path.Combine(Path.GetTempPath(), $"startup-diag-{Guid.NewGuid():N}.db")}";
        if (detailedErrors) psi.Environment[StartupDiagnostics.EnvironmentVariable] = "true";

        var process = Process.Start(psi)!;
        // Drain output so a full pipe can never stall the child.
        process.OutputDataReceived += (_, e) => { if (e.Data != null) File.AppendAllText(logPath, e.Data + Environment.NewLine); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) File.AppendAllText(logPath, e.Data + Environment.NewLine); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    [Fact]
    public async Task WithDetailedErrorsOn_AFatalStartupFailure_IsShownInTheBrowser_NotJustA500Page()
    {
        var port = FreePort();
        var log = Path.Combine(Path.GetTempPath(), $"startup-diag-{Guid.NewGuid():N}.log");
        using var process = StartWithoutSigningKey(port, detailedErrors: true, log);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            string? body = null;
            HttpStatusCode? status = null;
            for (var i = 0; i < 60 && body == null; i++)
            {
                if (process.HasExited) break;
                try
                {
                    var response = await http.GetAsync($"http://localhost:{port}/");
                    status = response.StatusCode;
                    body = await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(500);
                }
            }

            process.HasExited.Should().BeFalse("the fallback app keeps the process alive so the page can be served; log: " + (File.Exists(log) ? File.ReadAllText(log) : ""));
            status.Should().Be(HttpStatusCode.InternalServerError);
            body.Should().Contain("IntelliMed failed to start")
                .And.Contain("Jwt:Key is not configured")
                .And.Contain("signing key (Jwt:Key) configured")
                .And.Contain("False");

            // any path serves the report, not just "/"
            var other = await http.GetAsync($"http://localhost:{port}/api/clients/search");
            other.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await other.Content.ReadAsStringAsync()).Should().Contain("Jwt:Key is not configured");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            File.Delete(log);
        }
    }

    [Fact]
    public async Task WithDetailedErrorsOff_AFatalStartupFailure_StillKillsTheProcess_AndServesNothing()
    {
        var port = FreePort();
        var log = Path.Combine(Path.GetTempPath(), $"startup-diag-{Guid.NewGuid():N}.log");
        using var process = StartWithoutSigningKey(port, detailedErrors: false, log);
        try
        {
            var exited = process.WaitForExit(TimeSpan.FromSeconds(60));
            exited.Should().BeTrue("without the opt-in the app must fail exactly as before, so IIS reports 500.30");
            process.ExitCode.Should().NotBe(0);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var act = async () => await http.GetAsync($"http://localhost:{port}/");
            await act.Should().ThrowAsync<Exception>("nothing is listening (refused or timed out): no fallback page leaks details by default");

            // The cause is still on stdout for the IIS stdout log.
            await Task.Delay(300);
            File.ReadAllText(log).Should().Contain("Jwt:Key is not configured");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            File.Delete(log);
        }
    }
}
