using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

/// <summary>
/// Launches the real, already-built IntelliMed.Api.dll as a subprocess on a freshly-allocated
/// free TCP port (backed by an isolated temp SQLite database) and drives it with a headless
/// Chromium browser via Playwright. UI tests exercise the actual running app end-to-end rather
/// than mocked components.
///
/// A dynamic port (rather than a fixed one) is deliberate: an earlier version used a fixed port,
/// and when a test run got interrupted (e.g. cancelled mid-run) before DisposeAsync could kill
/// the subprocess, the next run's readiness check happily connected to that stale zombie server
/// instead of its own fresh instance — same port, wrong process, wrong (stale) database. A
/// dynamic port makes that class of bug structurally impossible: a new run can never collide
/// with an old one's port.
/// </summary>
public class PlaywrightServerFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private string? _dbPath;

    public IPlaywright PlaywrightInstance { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"intellimed-ui-tests-{Guid.NewGuid():N}.db");
        BaseUrl = $"http://127.0.0.1:{GetFreeTcpPort()}";

        var apiDllPath = FindApiDll();

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{apiDllPath}\"",
            WorkingDirectory = Path.GetDirectoryName(apiDllPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = BaseUrl;
        startInfo.EnvironmentVariables["ConnectionStrings__DefaultConnection"] = $"Data Source={_dbPath}";

        _serverProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start IntelliMed.Api test process.");

        await WaitUntilReadyAsync(TimeSpan.FromSeconds(60));

        // Headed by default so the browser is always visible. Set HEADLESS=1 to run without a
        // visible window (e.g. in CI).
        var headless = Environment.GetEnvironmentVariable("HEADLESS") == "1";

        PlaywrightInstance = await Playwright.CreateAsync();
        Browser = await PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = headless ? 0 : 250
        });
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindApiDll()
    {
        // The test assembly's own output directory has a sibling reference to the Api project's
        // build output via the ProjectReference — walk up to find IntelliMed.Api.dll next to us.
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "IntelliMed.Api.dll");
        if (File.Exists(candidate)) return candidate;

        throw new FileNotFoundException(
            $"Could not find IntelliMed.Api.dll near test output directory '{baseDir}'. " +
            "Ensure IntelliMed.Api is referenced by the test project so its output is copied alongside.");
    }

    private async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        using var httpClient = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            if (_serverProcess is { HasExited: true })
            {
                var stderr = await _serverProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException(
                    $"Test server process exited early (code {_serverProcess.ExitCode}). Stderr: {stderr}");
            }

            try
            {
                var response = await httpClient.GetAsync(BaseUrl);
                if ((int)response.StatusCode < 500) return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Test server did not become ready within {timeout}.", lastError);
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl
        });
        return await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        PlaywrightInstance?.Dispose();

        if (_serverProcess is { HasExited: false })
        {
            _serverProcess.Kill(entireProcessTree: true);
            _serverProcess.WaitForExit(5000);
        }
        _serverProcess?.Dispose();

        if (_dbPath is not null)
        {
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                var path = _dbPath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}

[CollectionDefinition("Playwright UI")]
public class PlaywrightUiCollection : ICollectionFixture<PlaywrightServerFixture>
{
}
