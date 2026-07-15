using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace IntelliMed.Web.Services;

/// <summary>
/// Centralized front-end error logger.
/// Logs errors to the browser console AND sends them to the API backend for persistence.
/// </summary>
public interface ILoggerService
{
    /// <summary>Log an error with full context to console and backend.</summary>
    Task LogErrorAsync(Exception exception, string? context = null);

    /// <summary>Log a warning message.</summary>
    void LogWarning(string message, string? context = null);

    /// <summary>Log an informational message.</summary>
    void LogInfo(string message, string? context = null);
}

public class LoggerService : ILoggerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LoggerService> _logger;

    public LoggerService(HttpClient httpClient, ILogger<LoggerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task LogErrorAsync(Exception exception, string? context = null)
    {
        // 1. Always log to browser console (visible in F12 dev tools)
        var ctx = context is not null ? $"[{context}] " : "";
        _logger.LogError(exception, "{Context}{Message}", ctx, exception.Message);

        // 2. Send to API backend for persistent logging
        try
        {
            var payload = new FrontendErrorLog
            {
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                Source = exception.Source,
                Context = context,
                Timestamp = DateTimeOffset.UtcNow,
                UserAgent = "Blazor WASM", // could be enriched with JS interop
                Url = "" // could be enriched with NavigationManager
            };

            // Fire-and-forget with short timeout — don't block the UI
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _httpClient.PostAsJsonAsync("api/log/frontend-error", payload, cts.Token);
        }
        catch
        {
            // Silently fail — we already logged to console, backend logging is best-effort
            _logger.LogWarning("Failed to send error log to backend API");
        }
    }

    public void LogWarning(string message, string? context = null)
    {
        var ctx = context is not null ? $"[{context}] " : "";
        _logger.LogWarning("{Context}{Message}", ctx, message);
    }

    public void LogInfo(string message, string? context = null)
    {
        var ctx = context is not null ? $"[{context}] " : "";
        _logger.LogInformation("{Context}{Message}", ctx, message);
    }
}

/// <summary>
/// DTO for sending front-end errors to the API backend.
/// </summary>
public class FrontendErrorLog
{
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string? Context { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? UserAgent { get; set; }
    public string? Url { get; set; }
}