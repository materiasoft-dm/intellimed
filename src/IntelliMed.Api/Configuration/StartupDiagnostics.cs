using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace IntelliMed.Api.Configuration;

/// <summary>
/// Opt-in "detailed errors" for a test/development host.
///
/// When the app cannot start under IIS, the only thing a browser ever sees is the generic
/// "HTTP Error 500.30 - ASP.NET Core app failed to start": the process dies before it can answer a
/// request, so the reason lives only in a log file on the server. With <c>ASPNETCORE_DETAILEDERRORS</c>
/// set to <c>true</c> (the publish script writes it into web.config for a test host), a fatal startup
/// failure is caught and a small fallback app is served instead, whose page says what failed and checks
/// the usual suspects (settings file, signing key present, database file and folder writable).
///
/// Off by default, and meant to stay off for anything real: the page shows exception text and paths.
/// It never prints a secret — the signing key is reported only as present or absent.
/// </summary>
public static class StartupDiagnostics
{
    public const string EnvironmentVariable = "ASPNETCORE_DETAILEDERRORS";

    public static bool Enabled => IsTruthy(Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static bool IsTruthy(string? value) =>
        value != null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    /// <summary>
    /// Serves the failure report in place of the app that could not start. If even that fallback cannot
    /// start (for example a malformed appsettings.json breaks every host), the ORIGINAL failure is rethrown
    /// so the process still dies the normal way and the stdout log still names the real cause.
    /// </summary>
    public static void Serve(string[] args, Exception failure)
    {
        // Always leave the cause on stdout too — that is what IIS's stdout log captures.
        Console.Error.WriteLine("[IntelliMed] Startup failed:");
        Console.Error.WriteLine(failure);

        string report;
        try
        {
            report = Describe(failure);
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(report);
            });
            app.Run();
        }
        catch (Exception fallbackFailure)
        {
            Console.Error.WriteLine("[IntelliMed] The detailed-errors fallback could not start either:");
            Console.Error.WriteLine(fallbackFailure);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <summary>The page body: what failed, where, and the environment checks. Plain text, never throws.</summary>
    public static string Describe(Exception failure)
    {
        var sb = new StringBuilder();
        sb.AppendLine("IntelliMed failed to start");
        sb.AppendLine("(detailed errors are ON for this host - set ASPNETCORE_DETAILEDERRORS=false to turn this off)");
        sb.AppendLine();
        sb.AppendLine("WHAT FAILED");
        sb.AppendLine(failure.ToString());
        sb.AppendLine();
        sb.AppendLine("ENVIRONMENT");
        Append(sb, "time (UTC)", () => DateTime.UtcNow.ToString("u"));
        Append(sb, "environment name", () => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "(not set; defaults to Production)");
        Append(sb, "os", () => RuntimeInformation.OSDescription);
        Append(sb, "process", () => $"{RuntimeInformation.ProcessArchitecture}, {(Environment.Is64BitProcess ? "64" : "32")}-bit");
        Append(sb, "runtime", () => RuntimeInformation.FrameworkDescription);
        Append(sb, "user", () => Environment.UserName);
        Append(sb, "iis app pool", () => Environment.GetEnvironmentVariable("APP_POOL_ID") ?? "(not under IIS)");
        Append(sb, "app directory", () => AppContext.BaseDirectory);
        Append(sb, "working directory", Directory.GetCurrentDirectory);
        sb.AppendLine();
        sb.AppendLine("CHECKS");
        Append(sb, "appsettings.json present", () => File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).ToString());
        Append(sb, "appsettings.Production.json present", () => File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json")).ToString());
        Append(sb, "signing key (Jwt:Key) configured", () => HasSigningKey().ToString());
        Append(sb, "wwwroot/index.html present", () => File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html")).ToString());
        Append(sb, "wwwroot/_framework present", () => Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "_framework")).ToString());
        Append(sb, "database file", DescribeDatabase);
        Append(sb, "app folder writable", () => IsWritable(AppContext.BaseDirectory).ToString());
        sb.AppendLine();
        sb.AppendLine("The same text is written to the stdout log (logs/stdout_*.log) when stdout logging is enabled.");
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string label, Func<string> value)
    {
        string text;
        try { text = value(); }
        catch (Exception ex) { text = $"(could not check: {ex.GetType().Name}: {ex.Message})"; }
        sb.Append("  ").Append(label.PadRight(38)).Append(text).AppendLine();
    }

    /// <summary>True when a non-empty Jwt:Key is available from the environment or the settings files. The
    /// value itself is never read into the report.</summary>
    private static bool HasSigningKey()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Jwt__Key"))) return true;
        foreach (var name in new[] { "appsettings.json", "appsettings.Production.json" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(path)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (doc.RootElement.TryGetProperty("Jwt", out var jwt)
                && jwt.TryGetProperty("Key", out var key)
                && key.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(key.GetString()))
            {
                return true;
            }
        }
        return false;
    }

    private static string DescribeDatabase()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
        {
            var settings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(settings))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(settings), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) && cs.TryGetProperty("DefaultConnection", out var dc))
                    connection = dc.GetString();
            }
        }

        const string marker = "Data Source=";
        var start = connection?.IndexOf(marker, StringComparison.OrdinalIgnoreCase) ?? -1;
        if (connection == null || start < 0) return "(not a SQLite 'Data Source=' connection string)";

        var file = connection[(start + marker.Length)..].Split(';')[0].Trim();
        var full = Path.IsPathRooted(file) ? file : Path.GetFullPath(file, Directory.GetCurrentDirectory());
        var info = new FileInfo(full);
        var dir = info.DirectoryName ?? string.Empty;
        return info.Exists
            ? $"{full} ({info.Length / 1024.0 / 1024.0:F1} MB); folder writable: {IsWritable(dir)}"
            : $"{full} (does not exist yet - it will be created on first start); folder writable: {IsWritable(dir)}";
    }

    private static bool IsWritable(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return false;
        var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
