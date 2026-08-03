using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// A self-contained "SSMS for SQLite" admin tool, gated by one dedicated username/password that is
/// deliberately isolated from ASP.NET Identity/RBAC — no ApplicationUser row backs it, it's not in
/// the RolePermissions catalog, and no staff role (not even SuperAdmin) can reach it. It reuses the
/// app's existing JwtBearer scheme (see Program.cs) rather than a parallel auth system: Login issues
/// a JWT carrying a "scope"="dbadmin" claim that a regular staff token never has, stored in an
/// HttpOnly cookie (this is a plain server-rendered mini-app, not the Blazor SPA, so there's no
/// client-side code to attach an Authorization header). Deliberately renders raw HTML directly —
/// no Razor views, nothing shipped in the public Blazor WASM bundle — so this tool's existence and
/// feature set aren't discoverable by inspecting the public app at all.
/// </summary>
[Route("dbadmin")]
public class DbAdminController : ControllerBase
{
    private const string CookieName = "dbadmin_token";
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);

    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly IDatabaseBackupService _backupService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DbAdminController> _logger;

    public DbAdminController(
        IConfiguration configuration,
        AppDbContext context,
        IDatabaseBackupService backupService,
        IMemoryCache cache,
        ILogger<DbAdminController> logger)
    {
        _configuration = configuration;
        _context = context;
        _backupService = backupService;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult LoginForm(string? error = null) =>
        Content(RenderLoginPage(error), "text/html");

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
    {
        var ip = ClientIp();
        var lockoutKey = $"dbadmin_lockout_{ip}";

        if (_cache.TryGetValue<int>(lockoutKey, out var failures) && failures >= MaxFailedAttempts)
        {
            _logger.LogWarning("DB admin login attempt while locked out, IP {Ip}", ip);
            return Content(RenderLoginPage("Too many failed attempts. Try again in 15 minutes."), "text/html");
        }

        if (!VerifyCredentials(username, password))
        {
            _cache.Set(lockoutKey, failures + 1, LockoutDuration);
            await LogAsync(DbAdminEventType.LoginFailure, $"Username: {username}", ip);
            return Content(RenderLoginPage("Invalid credentials."), "text/html");
        }

        _cache.Remove(lockoutKey);
        await LogAsync(DbAdminEventType.LoginSuccess, null, ip);

        Response.Cookies.Append(CookieName, GenerateToken(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.Add(TokenLifetime)
        });

        return Redirect("/dbadmin");
    }

    [HttpGet("logout")]
    [Authorize(Policy = "DbAdminOnly")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName);
        return Redirect("/dbadmin/login");
    }

    [HttpGet("")]
    [Authorize(Policy = "DbAdminOnly")]
    public async Task<IActionResult> Dashboard()
    {
        var tables = await GetTablesAsync();
        return Content(RenderDashboard(tables), "text/html");
    }

    [HttpGet("table/{name}")]
    [Authorize(Policy = "DbAdminOnly")]
    public async Task<IActionResult> BrowseTable(string name, int page = 1)
    {
        const int pageSize = 50;
        if (page < 1) page = 1;

        await using var connection = new SqliteConnection(ConnectionString());
        await connection.OpenAsync();

        if (!await TableExistsAsync(connection, name))
            return Content(RenderError($"Table '{WebUtility.HtmlEncode(name)}' not found."), "text/html");

        long totalRows;
        await using (var countCmd = connection.CreateCommand())
        {
            // The table name here only ever comes from sqlite_master (verified above, never
            // free-typed user input reaching this point), so this interpolation isn't an
            // injection surface.
            countCmd.CommandText = $"SELECT COUNT(*) FROM \"{name}\"";
            totalRows = Convert.ToInt64(await countCmd.ExecuteScalarAsync() ?? 0L);
        }

        List<string> columns;
        List<List<string?>> rows;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM \"{name}\" LIMIT $take OFFSET $skip";
            cmd.Parameters.AddWithValue("$take", pageSize);
            cmd.Parameters.AddWithValue("$skip", (page - 1) * pageSize);
            (columns, rows) = await ReadResultsAsync(cmd);
        }

        return Content(RenderTableBrowser(name, columns, rows, page, pageSize, totalRows), "text/html");
    }

    [HttpPost("query")]
    [Authorize(Policy = "DbAdminOnly")]
    public async Task<IActionResult> RunQuery([FromForm] string sql)
    {
        var ip = ClientIp();
        sql = (sql ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(sql))
            return Content(RenderDashboard(await GetTablesAsync(), sql, null, "Enter a SQL statement."), "text/html");

        var trimmed = sql.TrimStart();
        var isReadOnly = trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("pragma", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("explain", StringComparison.OrdinalIgnoreCase);

        // Safety net: anything that might mutate data gets a real, verified SQLite backup first,
        // via the same online-backup mechanism the scheduled backups already use — so there's
        // always a known-good rollback point before a destructive statement runs.
        string? backupFileName = null;
        if (!isReadOnly)
        {
            var backup = await _backupService.PerformBackupAsync(DatabaseBackupTrigger.Manual);
            backupFileName = backup.FileName;
        }

        await using var connection = new SqliteConnection(ConnectionString());
        await connection.OpenAsync();

        string resultsHtml;
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            if (isReadOnly)
            {
                var (columns, rows) = await ReadResultsAsync(cmd);
                await LogAsync(DbAdminEventType.QueryExecuted, sql, ip);
                resultsHtml = BuildResultsFragment(columns, rows, backupFileName);
            }
            else
            {
                var affected = await cmd.ExecuteNonQueryAsync();
                await LogAsync(DbAdminEventType.QueryExecuted, sql, ip);
                resultsHtml = BuildAffectedFragment(affected, backupFileName);
            }
        }
        catch (Exception ex)
        {
            await LogAsync(DbAdminEventType.QueryExecuted, $"FAILED: {sql} — {ex.Message}", ip);
            resultsHtml = BuildErrorFragment($"Query failed: {WebUtility.HtmlEncode(ex.Message)}", backupFileName);
        }

        // Re-fetch the table list too — a CREATE/DROP/ALTER just now would otherwise leave the
        // left column showing a stale schema until the next full page load.
        return Content(RenderDashboard(await GetTablesAsync(), sql, resultsHtml), "text/html");
    }

    // ------------------------------------------------------------------
    // Auth helpers
    // ------------------------------------------------------------------

    private bool VerifyCredentials(string? username, string? password)
    {
        var configuredUsername = _configuration["DbAdmin:Username"];
        var configuredHash = _configuration["DbAdmin:PasswordHash"];

        if (string.IsNullOrEmpty(configuredUsername) || string.IsNullOrEmpty(configuredHash))
        {
            _logger.LogError("DbAdmin:Username/PasswordHash are not configured — /dbadmin login is disabled.");
            return false;
        }

        if (!string.Equals(username, configuredUsername, StringComparison.Ordinal))
            return false;

        // A standalone PasswordHasher instance — same PBKDF2 algorithm ASP.NET Identity uses for
        // staff passwords, but with no ApplicationUser/AspNetUsers row backing this credential.
        var hasher = new PasswordHasher<object>();
        var result = hasher.VerifyHashedPassword(new object(), configuredHash, password ?? string.Empty);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    private string GenerateToken()
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "IntelliMed_SuperSecretKey_AtLeast32Characters!";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "IntelliMed";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "IntelliMed.Client";

        var claims = new List<Claim>
        {
            // The one claim regular staff JWTs never carry — see Program.cs's "DbAdminOnly" policy.
            new("scope", "dbadmin"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(TokenLifetime),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private async Task LogAsync(DbAdminEventType type, string? detail, string ip)
    {
        _context.DbAdminAuditLogs.Add(new DbAdminAuditLog { EventType = type, Detail = detail, IpAddress = ip });
        await _context.SaveChangesAsync();
    }

    // ------------------------------------------------------------------
    // Raw SQL helpers
    // ------------------------------------------------------------------

    private string ConnectionString() =>
        _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=intellimed.db";

    private async Task<List<(string Name, long RowCount)>> GetTablesAsync()
    {
        var tables = new List<(string, long)>();

        await using var connection = new SqliteConnection(ConnectionString());
        await connection.OpenAsync();

        var tableNames = new List<string>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tableNames.Add(reader.GetString(0));
        }

        foreach (var name in tableNames)
        {
            await using var countCmd = connection.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM \"{name}\"";
            var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync() ?? 0L);
            tables.Add((name, count));
        }

        return tables;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string name)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = $name";
        cmd.Parameters.AddWithValue("$name", name);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
    }

    private static async Task<(List<string> Columns, List<List<string?>> Rows)> ReadResultsAsync(SqliteCommand cmd)
    {
        await using var reader = await cmd.ExecuteReaderAsync();
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var rows = new List<List<string?>>();

        while (await reader.ReadAsync())
        {
            var row = new List<string?>(columns.Count);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i)) { row.Add(null); continue; }
                var value = reader.GetValue(i);
                row.Add(value is byte[] blob ? $"<{blob.Length} bytes>" : value.ToString());
            }
            rows.Add(row);
        }

        return (columns, rows);
    }

    // ------------------------------------------------------------------
    // HTML rendering — deliberately raw string templates, not Razor views, so nothing about this
    // tool is compiled into the app's public-facing assemblies/bundle.
    // ------------------------------------------------------------------

    private static string Layout(string title, string body) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <title>{{WebUtility.HtmlEncode(title)}} — DB Admin</title>
        <style>
          body { font-family: -apple-system, Segoe UI, sans-serif; background: #1e1e1e; color: #ddd; margin: 0; padding: 0; }
          header { background: #111; padding: 10px 20px; display: flex; justify-content: space-between; align-items: center; border-bottom: 2px solid #c0392b; }
          header a { color: #ddd; text-decoration: none; margin-left: 16px; }
          header a:hover { color: #fff; }
          main { padding: 20px; max-width: 1200px; margin: 0 auto; }
          h1, h2 { color: #fff; }
          table { border-collapse: collapse; width: 100%; margin-bottom: 16px; font-size: 13px; }
          th, td { border: 1px solid #444; padding: 4px 8px; text-align: left; }
          th { background: #2a2a2a; }
          tr:nth-child(even) { background: #252525; }
          textarea { width: 100%; min-height: 80px; resize: vertical; font-family: Consolas, monospace; background: #111; color: #ddd; border: 1px solid #444; padding: 8px; box-sizing: border-box; }
          button { background: #c0392b; color: #fff; border: none; padding: 8px 16px; cursor: pointer; margin-top: 8px; font-size: 14px; }
          button:hover { background: #a93226; }
          .error { color: #e74c3c; font-weight: bold; }
          .warn { color: #f39c12; }
          .muted { color: #888; font-size: 12px; }
          a.tbl { color: #5dade2; }
          .scroll-wrap { overflow-x: auto; }
          .scroll-wrap table { width: auto; min-width: 100%; }
          .dashboard-grid { display: flex; }
          .dashboard-grid .col-tables { flex: 0 0 auto; width: 320px; overflow: auto; }
          .dashboard-grid .col-resizer { flex: 0 0 6px; width: 6px; margin: 0 10px; border-radius: 3px; background: #333; cursor: col-resize; }
          .dashboard-grid .col-resizer:hover, .dashboard-grid .col-resizer.active { background: #c0392b; }
          .dashboard-grid .col-console { flex: 1 1 auto; min-width: 0; }
          @media (max-width: 720px) {
            .dashboard-grid { flex-direction: column; }
            .dashboard-grid .col-resizer { display: none; }
            .dashboard-grid .col-tables { width: 100% !important; flex: none; }
          }
        </style>
        </head>
        <body>
        <header>
          <strong>⚠ DB Admin</strong>
          <div><a href="/dbadmin">Dashboard</a><a href="/dbadmin/logout">Logout</a></div>
        </header>
        <main>
        {{body}}
        </main>
        </body>
        </html>
        """;

    private static string RenderLoginPage(string? error)
    {
        var errorHtml = error != null ? $"<div class=\"error\">{WebUtility.HtmlEncode(error)}</div>" : "";

        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <title>DB Admin Login</title>
        <style>
          body { font-family: -apple-system, Segoe UI, sans-serif; background: #1e1e1e; color: #ddd; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }
          .card { background: #252525; border: 1px solid #444; padding: 24px; width: 320px; }
          h1 { font-size: 18px; color: #fff; margin-top: 0; }
          input { background: #111; color: #ddd; border: 1px solid #444; padding: 8px; width: 100%; box-sizing: border-box; margin-bottom: 10px; font-size: 14px; }
          button { background: #c0392b; color: #fff; border: none; padding: 10px; width: 100%; cursor: pointer; font-size: 14px; }
          .error { color: #e74c3c; margin-bottom: 10px; }
        </style>
        </head>
        <body>
          <form class="card" method="post" action="/dbadmin/login">
            <h1>⚠ DB Admin</h1>
            {{errorHtml}}
            <input type="text" name="username" placeholder="Username" autofocus required />
            <input type="password" name="password" placeholder="Password" required />
            <button type="submit">Sign In</button>
          </form>
        </body>
        </html>
        """;
    }

    private string RenderDashboard(List<(string Name, long RowCount)> tables, string? sqlValue = null, string? resultsHtml = null, string? queryError = null)
    {
        var rowsHtml = string.Join("", tables.Select(t =>
            $"<tr><td><a class=\"tbl\" href=\"/dbadmin/table/{Uri.EscapeDataString(t.Name)}\">{WebUtility.HtmlEncode(t.Name)}</a></td><td>{t.RowCount:N0}</td></tr>"));
        var errorHtml = queryError != null ? $"<div class=\"error\">{WebUtility.HtmlEncode(queryError)}</div>" : "";
        var sqlEncoded = WebUtility.HtmlEncode(sqlValue ?? "");

        var body = $$"""
            <h1>Dashboard</h1>
            <div class="dashboard-grid">
              <div class="col-tables" id="colTables">
                <h2>Tables</h2>
                <table>
                  <thead><tr><th>Name</th><th>Rows</th></tr></thead>
                  <tbody>{{rowsHtml}}</tbody>
                </table>
              </div>
              <div class="col-resizer" id="colResizer"></div>
              <div class="col-console">
                <h2>SQL Console</h2>
                {{errorHtml}}
                <form method="post" action="/dbadmin/query">
                  <textarea name="sql" placeholder="SELECT * FROM Clients LIMIT 10;">{{sqlEncoded}}</textarea>
                  <button type="submit">Run</button>
                </form>
                <p class="muted">Non-SELECT statements trigger an automatic backup first. Every query is logged to DbAdminAuditLogs.</p>
                {{resultsHtml}}
              </div>
            </div>
            <script>
              (function () {
                var resizer = document.getElementById('colResizer');
                var tables = document.getElementById('colTables');
                if (!resizer || !tables) return;

                var saved = localStorage.getItem('dbadmin_tables_col_width');
                if (saved) tables.style.width = saved + 'px';

                resizer.addEventListener('mousedown', function (e) {
                  e.preventDefault();
                  var startX = e.clientX;
                  var startWidth = tables.offsetWidth;
                  resizer.classList.add('active');
                  document.body.style.userSelect = 'none';

                  function onMove(ev) {
                    var next = Math.max(180, Math.min(700, startWidth + (ev.clientX - startX)));
                    tables.style.width = next + 'px';
                  }
                  function onUp() {
                    document.removeEventListener('mousemove', onMove);
                    document.removeEventListener('mouseup', onUp);
                    resizer.classList.remove('active');
                    document.body.style.userSelect = '';
                    localStorage.setItem('dbadmin_tables_col_width', tables.offsetWidth);
                  }
                  document.addEventListener('mousemove', onMove);
                  document.addEventListener('mouseup', onUp);
                });
              })();
            </script>
            """;

        return Layout("Dashboard", body);
    }

    private static string RenderTableBrowser(string tableName, List<string> columns, List<List<string?>> rows, int page, int pageSize, long totalRows)
    {
        var headerHtml = string.Join("", columns.Select(c => $"<th>{WebUtility.HtmlEncode(c)}</th>"));
        var rowsHtml = string.Join("", rows.Select(r =>
            "<tr>" + string.Join("", r.Select(v => $"<td>{(v == null ? "<span class=\"muted\">NULL</span>" : WebUtility.HtmlEncode(v))}</td>")) + "</tr>"));

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)pageSize));
        var encodedName = Uri.EscapeDataString(tableName);
        var pagerLinks = new List<string>();
        if (page > 1) pagerLinks.Add($"<a class=\"tbl\" href=\"/dbadmin/table/{encodedName}?page={page - 1}\">&laquo; Prev</a>");
        if (page < totalPages) pagerLinks.Add($"<a class=\"tbl\" href=\"/dbadmin/table/{encodedName}?page={page + 1}\">Next &raquo;</a>");

        var body = $"""
            <h1>{WebUtility.HtmlEncode(tableName)}</h1>
            <p class="muted">Page {page} of {totalPages} — {totalRows:N0} rows total. {string.Join(" &middot; ", pagerLinks)}</p>
            <div class="scroll-wrap">
            <table>
              <thead><tr>{headerHtml}</tr></thead>
              <tbody>{rowsHtml}</tbody>
            </table>
            </div>
            """;

        return Layout(tableName, body);
    }

    // These render just the fragment slotted below the SQL Console form on the dashboard itself
    // (see RenderDashboard/RunQuery) — results stay next to the query that produced them instead
    // of navigating to a separate page.
    private static string BuildResultsFragment(List<string> columns, List<List<string?>> rows, string? backupFileName)
    {
        var headerHtml = string.Join("", columns.Select(c => $"<th>{WebUtility.HtmlEncode(c)}</th>"));
        var rowsHtml = string.Join("", rows.Select(r =>
            "<tr>" + string.Join("", r.Select(v => $"<td>{(v == null ? "<span class=\"muted\">NULL</span>" : WebUtility.HtmlEncode(v))}</td>")) + "</tr>"));
        var backupHtml = backupFileName != null ? $"<p class=\"warn\">Backup created before running: {WebUtility.HtmlEncode(backupFileName)}</p>" : "";

        return $"""
            <h3>Results</h3>
            {backupHtml}
            <p class="muted">{rows.Count:N0} row(s) returned.</p>
            <div class="scroll-wrap">
            <table>
              <thead><tr>{headerHtml}</tr></thead>
              <tbody>{rowsHtml}</tbody>
            </table>
            </div>
            """;
    }

    private static string BuildAffectedFragment(int affected, string? backupFileName)
    {
        var backupHtml = backupFileName != null ? $"<p class=\"warn\">Backup created before running: {WebUtility.HtmlEncode(backupFileName)}</p>" : "";

        return $"""
            <h3>Query Executed</h3>
            {backupHtml}
            <p>{affected:N0} row(s) affected.</p>
            """;
    }

    private static string BuildErrorFragment(string safeHtmlMessage, string? backupFileName)
    {
        var backupHtml = backupFileName != null ? $"<p class=\"warn\">A backup was still created before this failed: {WebUtility.HtmlEncode(backupFileName)}</p>" : "";

        return $"""
            <h3 class="error">Error</h3>
            <p>{safeHtmlMessage}</p>
            {backupHtml}
            """;
    }

    private static string RenderError(string safeHtmlMessage, string? backupFileName = null)
    {
        var backupHtml = backupFileName != null ? $"<p class=\"warn\">A backup was still created before this failed: {WebUtility.HtmlEncode(backupFileName)}</p>" : "";

        var body = $"""
            <h1 class="error">Error</h1>
            <p>{safeHtmlMessage}</p>
            {backupHtml}
            <p><a class="tbl" href="/dbadmin">&laquo; Back to dashboard</a></p>
            """;

        return Layout("Error", body);
    }
}
