using System.Text;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// DATABASE CONFIGURATION
// ============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=intellimed.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// ============================================================================
// ASP.NET IDENTITY CONFIGURATION
// ============================================================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // User requirements
    options.User.RequireUniqueEmail = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ============================================================================
// JWT AUTHENTICATION CONFIGURATION
// ============================================================================
var jwtKey = builder.Configuration["Jwt:Key"] ?? "IntelliMed_SuperSecretKey_AtLeast32Characters!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "IntelliMed";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "IntelliMed.Client";

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
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

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

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseWebAssemblyDebugging();
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

// Fallback to index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();

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
    // Only seed if the table is empty
    if (await context.RolePermissions.AnyAsync())
        return;

    var permissions = new List<RolePermission>
    {
        // =====================================================================
        // SuperAdmin — ALL pages
        // =====================================================================
        // Clinical
        new() { RoleName = "SuperAdmin", PageKey = "patients", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "patients/create", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "patients/edit", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "patients/delete", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "appointments/delete", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "practitioners", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "practitioners/create", Category = "Clinical" },
        new() { RoleName = "SuperAdmin", PageKey = "practitioners/edit", Category = "Clinical" },
        // Financial
        new() { RoleName = "SuperAdmin", PageKey = "invoices", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/create", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/edit", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "invoices/delete", Category = "Financial" },
        new() { RoleName = "SuperAdmin", PageKey = "payments", Category = "Financial" },
        // Admin
        new() { RoleName = "SuperAdmin", PageKey = "admin/users", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/roles", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/audit", Category = "Admin" },
        new() { RoleName = "SuperAdmin", PageKey = "admin/settings", Category = "Admin" },
        // Reports
        new() { RoleName = "SuperAdmin", PageKey = "reports", Category = "Reports" },
        new() { RoleName = "SuperAdmin", PageKey = "reports/financial", Category = "Reports" },
        new() { RoleName = "SuperAdmin", PageKey = "reports/clinical", Category = "Reports" },

        // =====================================================================
        // Admin — most pages except some SuperAdmin-only
        // =====================================================================
        new() { RoleName = "Admin", PageKey = "patients", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "patients/create", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "patients/edit", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "patients/delete", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "appointments/delete", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "practitioners", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "practitioners/create", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "practitioners/edit", Category = "Clinical" },
        new() { RoleName = "Admin", PageKey = "invoices", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/create", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/edit", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "invoices/delete", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "payments", Category = "Financial" },
        new() { RoleName = "Admin", PageKey = "admin/users", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/roles", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/audit", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "admin/settings", Category = "Admin" },
        new() { RoleName = "Admin", PageKey = "reports", Category = "Reports" },
        new() { RoleName = "Admin", PageKey = "reports/financial", Category = "Reports" },
        new() { RoleName = "Admin", PageKey = "reports/clinical", Category = "Reports" },

        // =====================================================================
        // Doctor — clinical + read-only financial
        // =====================================================================
        new() { RoleName = "Doctor", PageKey = "patients", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "patients/create", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "patients/edit", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "practitioners", Category = "Clinical" },
        new() { RoleName = "Doctor", PageKey = "invoices", Category = "Financial" },
        new() { RoleName = "Doctor", PageKey = "reports", Category = "Reports" },
        new() { RoleName = "Doctor", PageKey = "reports/clinical", Category = "Reports" },

        // =====================================================================
        // Nurse — patients + appointments (no delete)
        // =====================================================================
        new() { RoleName = "Nurse", PageKey = "patients", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "patients/create", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "patients/edit", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "Nurse", PageKey = "practitioners", Category = "Clinical" },

        // =====================================================================
        // Receptionist — patients, appointments, invoices
        // =====================================================================
        new() { RoleName = "Receptionist", PageKey = "patients", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "patients/create", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "patients/edit", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "appointments", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "appointments/create", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "appointments/edit", Category = "Clinical" },
        new() { RoleName = "Receptionist", PageKey = "invoices", Category = "Financial" },
        new() { RoleName = "Receptionist", PageKey = "invoices/create", Category = "Financial" },
        new() { RoleName = "Receptionist", PageKey = "payments", Category = "Financial" },
    };

    await context.RolePermissions.AddRangeAsync(permissions);
    await context.SaveChangesAsync();
}
