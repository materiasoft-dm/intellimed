using System.Text;
using IntelliMed.Api.Configuration;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

// A fatal startup failure normally shows only IIS's bare "500.30". With ASPNETCORE_DETAILEDERRORS=true
// (a test host) the reason is served instead - see StartupDiagnostics.
try
{
    var builder = WebApplication.CreateBuilder(args);

    // ============================================================================
    // DATABASE CONFIGURATION
    // ============================================================================
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=intellimed.db";

    builder.Services.AddInfrastructure(connectionString, builder.Configuration);

    // ============================================================================
    // ASP.NET IDENTITY CONFIGURATION
    // ============================================================================
    // The password/lockout policy is stored in SystemSettings and read here, straight from its own
    // short-lived DbContext rather than through DI.
    //
    // Deliberately NOT an IConfigureOptions<IdentityOptions>: that resolves lazily while the host is
    // being built, and reading the database from inside it (sync-over-async) deadlocked — `dotnet ef`
    // hung indefinitely on any command that builds the host. Reading here, before AddIdentity, keeps it
    // a plain sequential call with nothing to deadlock against.
    //
    // Consequence: a policy change takes effect on the next restart, which the System Settings screen
    // states plainly. On a brand-new database the table doesn't exist yet, so failures fall back to the
    // same defaults that used to be hardcoded — the policy is never silently weakened.
    var identityPolicy = SystemSettingsBootstrap.Read(connectionString,
        LoggerFactory.Create(c => c.AddConsole()).CreateLogger("SystemSettingsBootstrap"));

    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = identityPolicy.PasswordRequireDigit;
        options.Password.RequireLowercase = identityPolicy.PasswordRequireLowercase;
        options.Password.RequireUppercase = identityPolicy.PasswordRequireUppercase;
        options.Password.RequireNonAlphanumeric = identityPolicy.PasswordRequireNonAlphanumeric;
        options.Password.RequiredLength = identityPolicy.PasswordMinLength;

        // User requirements
        options.User.RequireUniqueEmail = true;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identityPolicy.LockoutDurationMinutes);
        options.Lockout.MaxFailedAccessAttempts = identityPolicy.LockoutMaxFailedAttempts;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

    // ============================================================================
    // JWT AUTHENTICATION CONFIGURATION
    // ============================================================================
    // Resolved once and shared — see JwtSettings for why there is no hardcoded fallback key.
    var jwtSettings = JwtSettings.Resolve(
        builder.Configuration,
        builder.Environment,
        LoggerFactory.Create(c => c.AddConsole()).CreateLogger<JwtSettings>());
    builder.Services.AddSingleton(jwtSettings);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            // context.Token is NOT populated from the Authorization header by this point — the header
            // is only read as a fallback AFTER this event returns, and only if it's still empty. So we
            // must check the header ourselves rather than context.Token, or this cookie would silently
            // win over a real Bearer header on every request in the app. Scoped to /dbadmin so a stray
            // dbadmin_token cookie can never affect authentication on any other route, even in theory.
            OnMessageReceived = context =>
            {
                var isDbAdminPath = context.Request.Path.StartsWithSegments("/dbadmin");
                if (isDbAdminPath && !context.Request.Headers.ContainsKey("Authorization")
                    && context.Request.Cookies.TryGetValue("dbadmin_token", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        // Every endpoint requires an authenticated user by default — new controllers are protected
        // automatically instead of relying on remembering to stamp [Authorize] on each one.
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        // Satisfied only by the separate token DbAdminController.Login issues, never by a regular
        // staff JWT (even SuperAdmin) — that token type is never given this claim at all.
        options.AddPolicy("DbAdminOnly", policy => policy.RequireClaim("scope", "dbadmin"));
    });

    // Backs the /dbadmin login IP lockout — deliberately not using ASP.NET Identity's own lockout,
    // which is tied to an ApplicationUser row that this separate credential doesn't have.
    builder.Services.AddMemoryCache();

    // ============================================================================
    // CONTROLLERS & API CONFIGURATION
    // ============================================================================
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Swagger with JWT support
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "IntelliMed API",
            Version = "v1",
            Description = "Medical Practice Management API"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // ============================================================================
    // DATABASE INITIALIZATION & SEEDING
    // ============================================================================
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            // Apply pending EF Core migrations
            await context.Database.MigrateAsync();

            // Seed roles
            await SeedRolesAsync(roleManager);

            // Seed default admin user
            await SeedDefaultUserAsync(userManager);

            // Seed default role permissions
            await SeedRolePermissionsAsync(context);
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while initializing the database.");
        }
    }

    // ============================================================================
    // HTTP REQUEST PIPELINE CONFIGURATION
    // ============================================================================
    // Test hosts only: ASPNETCORE_DETAILEDERRORS=true shows exception details in the browser.
    // (Development already does this by default.)
    if (StartupDiagnostics.Enabled && !app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseWebAssemblyDebugging();

        // In Development mode, the project wwwroot doesn't have _framework files.
        // They're in bin/Debug/net10.0/wwwroot from the CopyBlazorWasmOutput target.
        // Add a static file provider that serves from the build output directory
        // so _framework/* files resolve correctly (fixes SRI integrity failures).
        var buildOutputWwwRoot = Path.Combine(app.Environment.ContentRootPath, "bin", "Debug", "net10.0", "wwwroot");
        if (Directory.Exists(buildOutputWwwRoot))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(buildOutputWwwRoot),
                // Serve before the default static files so _framework takes priority
            });
        }
    }
    else
    {
        app.UseHttpsRedirection();
    }

    // Serve Blazor WASM static files from the IntelliMed.Web project
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Fallback to index.html for client-side routing — must stay anonymous: every client-side route
    // (including /login itself) is served through this, so the global auth fallback policy would
    // otherwise 401 the SPA shell before the Blazor router ever gets a chance to redirect.
    app.MapFallbackToFile("index.html").AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (StartupDiagnostics.Enabled)
{
    StartupDiagnostics.Serve(args, ex);
}

// ============================================================================
// SEEDING METHODS
// ============================================================================
static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
{
    string[] roles = ["SuperAdmin", "Admin", "Doctor", "Nurse", "Receptionist"];

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

static async Task SeedDefaultUserAsync(UserManager<ApplicationUser> userManager)
{
    const string adminEmail = "admin@clinic.com";
    const string adminPassword = "IntelliMed2024!";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "System",
            LastName = "Administrator",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    else
    {
        // Ensure existing admin has SuperAdmin role
        if (!await userManager.IsInRoleAsync(adminUser, "SuperAdmin"))
        {
            await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
        }
    }
}

static async Task SeedRolePermissionsAsync(AppDbContext context)
{
    // Seed once per database. "Table is empty" is the wrong test: MigrateAsync runs first, and several
    // migrations (e.g. AddAppointmentSchedulingFeatures) insert permission rows of their own, so a fresh
    // database was never empty here and silently skipped this whole seed. Instead, treat the seed as done
    // once any role holds "clients" — only this seed ever grants it (no migration does), and SuperAdmin
    // always has it — so permissions an admin later removes are never put back on restart.
    if (await context.RolePermissions.AnyAsync(p => p.PageKey == "clients"))
        return;

    var permissions = new List<RolePermission>
    {
        // =====================================================================
        // SuperAdmin — ALL pages
        // =====================================================================
        // Clinical
        new() { RoleName = "SuperAdmin", PageKey = "clients", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "clients/create", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "clients/edit", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "address-book", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "results", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "clients/delete", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments/delete", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments/waiting-room", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "prescriptions", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "prescriptions/create", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "prescriptions/edit", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "progress-notes", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "progress-notes/create", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "progress-notes/edit", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "health-issues", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "health-issues/create", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "health-issues/edit", Category = "Clinical" },
        // Financial
        new() { RoleName = "SuperAdmin", PageKey = "invoices", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/create", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/edit", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/delete", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/refund", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/write-off", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/void", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/split", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "payments", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "fee-schedules", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "derived-item-configs", Category = "Financial" },
        // Admin
        new() { RoleName = "SuperAdmin", PageKey = "admin/users", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/roles", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/settings", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/appointment-types", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/email-templates", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/search-actions", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/database-backups", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/medicines", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/drug-interactions", Category = "Admin" },
        // Reports
        new() { RoleName = "SuperAdmin", PageKey = "reports", Category = "Reports" },
        new() { RoleName = "SuperAdmin", PageKey = "reports/financial", Category = "Reports" },
        new() { RoleName = "SuperAdmin", PageKey = "reports/clinical", Category = "Reports" },

        // =====================================================================
        // Admin — most pages except some SuperAdmin-only
        // =====================================================================
        new() { RoleName = "Admin", PageKey = "clients", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "clients/create", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "clients/edit", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "address-book", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "results", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "clients/delete", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments/delete", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments/waiting-room", Category = "Clinical" },
        // View-only for prescriptions — Admin reads as a practice-manager/IT role here, and letting a
        // non-clinician issue prescriptions is a different risk than letting them configure fee
        // schedules. Adjust at /admin/roles if this default doesn't fit.
        new() { RoleName = "Admin", PageKey = "prescriptions", Category = "Clinical" },
        // Same reasoning — view-only, not a clinical author.
        new() { RoleName = "Admin", PageKey = "progress-notes", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "health-issues", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "invoices", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/create", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/edit", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/delete", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/refund", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/write-off", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/void", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/split", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "payments", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "fee-schedules", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "derived-item-configs", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "admin/users", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/roles", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/settings", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/appointment-types", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/email-templates", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/search-actions", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/database-backups", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/medicines", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/drug-interactions", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "reports", Category = "Reports" },
        new() { RoleName = "Admin", PageKey = "reports/financial", Category = "Reports" },
        new() { RoleName = "Admin", PageKey = "reports/clinical", Category = "Reports" },

        // =====================================================================
        // Doctor — clinical + read-only financial
        // =====================================================================
        new() { RoleName = "Doctor", PageKey = "clients", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "clients/create", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "clients/edit", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "address-book", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "results", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "appointments/waiting-room", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "prescriptions", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "prescriptions/create", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "prescriptions/edit", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "progress-notes", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "progress-notes/create", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "progress-notes/edit", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "health-issues", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "health-issues/create", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "health-issues/edit", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "invoices", Category = "Financial" },
        new() { RoleName = "Doctor", PageKey = "reports", Category = "Reports" },
        new() { RoleName = "Doctor", PageKey = "reports/clinical", Category = "Reports" },

        // =====================================================================
        // Nurse — clients + appointments (no delete)
        // =====================================================================
        new() { RoleName = "Nurse", PageKey = "clients", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "clients/create", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "clients/edit", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "address-book", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "results", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "appointments/waiting-room", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "prescriptions", Category = "Clinical" },
        // Full access, unlike prescriptions above (view-only) — nurses legitimately author clinical
        // documentation (vitals, triage, wound care notes) in a way they don't prescribe. A deliberate
        // departure from the prescriptions default, not an inconsistency. Adjust at /admin/roles if this
        // default doesn't fit.
        new() { RoleName = "Nurse", PageKey = "progress-notes", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "progress-notes/create", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "progress-notes/edit", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "health-issues", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "health-issues/create", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "health-issues/edit", Category = "Clinical" },

        // =====================================================================
        // Receptionist — clients, appointments, invoices
        // =====================================================================
        new() { RoleName = "Receptionist", PageKey = "clients", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "clients/create", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "clients/edit", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "address-book", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "appointments/waiting-room", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "invoices", Category = "Financial" },
        new() { RoleName = "Receptionist", PageKey = "invoices/create", Category = "Financial" },
        new() { RoleName = "Receptionist", PageKey = "payments", Category = "Financial" },
    };

    // Skip rows a migration already inserted — (RoleName, PageKey) is unique.
    var existing = (await context.RolePermissions.Select(p => new { p.RoleName, p.PageKey }).ToListAsync())
        .Select(p => (p.RoleName, p.PageKey))
        .ToHashSet();

    await context.RolePermissions.AddRangeAsync(permissions.Where(p => !existing.Contains((p.RoleName, p.PageKey))));
    await context.SaveChangesAsync();
}

// Exposes the top-level Program for WebApplicationFactory<Program> in integration/UI tests.
public partial class Program { }
