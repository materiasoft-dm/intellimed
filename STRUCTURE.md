# IntelliMed Project Structure

> **Reference:** See `handoff.md` for detailed legacy Pracnet system documentation (architecture, database, billing, Medicare claiming, integrations, etc.)

## Solution Structure
```
IntelliMed.sln
├── src/
│   ├── IntelliMed.Api/              # ASP.NET Core Web API (Backend)
│   ├── IntelliMed.Core/             # Domain Entities, DTOs, Repository Interfaces
│   ├── IntelliMed.Infrastructure/   # EF Core, Repositories, Mappers (SQLite)
│   ├── IntelliMed.Infrastructure.SqlServer/  # SQL Server implementation (future)
│   ├── IntelliMed.Infrastructure.MongoDB/    # MongoDB implementation (future)
│   ├── IntelliMed.Shared/           # Shared DTOs and Request/Response models
│   ├── IntelliMed.Client/           # React web client (Vite + TypeScript) — served by the Api
│   └── IntelliMed.Maui/             # .NET MAUI Blazor Hybrid App (Frontend)
```

## Web client (src/IntelliMed.Client)
```
IntelliMed.Client/
├── index.html                   # shell + startup splash
├── vite.config.ts               # dev server :5173, proxies /api → :5284
├── src/
│   ├── main.tsx / router.tsx    # entry point; every route (same URLs the Blazor app had), lazy-loaded
│   ├── session.tsx              # signed-in user, roles, page permissions (`can()`), clinics
│   ├── api/                     # typed fetch wrappers per area (clients, billing, clinical, ...)
│   │   └── types.gen.ts         # GENERATED from Core DTOs/entities: scripts/generate-client-types.py
│   ├── layout/MainLayout.tsx    # auth guard, sidebar, top bar, clinic switcher
│   ├── components/              # DataGrid, Modal, Drawer, pickers, inputs, States (loading/empty), ...
│   ├── lib/                     # dates (.NET format patterns), validation, hooks, infinite scroll
│   ├── pages/                   # one file per screen; pages/client/ holds the client-record tabs
│   └── styles/                  # app.css (inherited look) + polish.css (tokens, motion, focus)
```
DTOs reach the client as camelCase JSON with numeric enums; each generated interface has a
`defaultX()` factory mirroring the C# initialisers — spread it when building a request body so new
DTO fields don't silently go missing. `npm run build` type-checks (`tsc -b`) before bundling.

## Data Layer Architecture

### Core Project (Domain)
```
IntelliMed.Core/
├── Entities/                # Plain POCOs (no EF attributes)
│   ├── Patient.cs
│   ├── Appointment.cs
│   └── Invoice.cs
├── DTOs/                    # Request/Response models
│   ├── PatientDto.cs
│   ├── CreatePatientDto.cs
│   └── PatientSearchDto.cs
├── Interfaces/               # Repository contracts
│   ├── IRepository.cs       # Generic base
│   ├── IPatientRepository.cs
│   └── IAppointmentRepository.cs
└── Services/                # Domain services (optional)
```

### Infrastructure Project (Data Access)
```
IntelliMed.Infrastructure/
├── Data/
│   ├── AppDbContext.cs      # EF Core context
│   └── Migrations/          # EF migrations
├── Repositories/
│   ├── PatientRepository.cs  # Implements IPatientRepository
│   └── AppointmentRepository.cs
├── Mappers/
│   └── EntityMapper.cs      # Entity ↔ DTO conversion
└── Services/
    └── PatientService.cs    # Business logic (if needed)
```

### Repository Interface Example
```csharp
// Core/Interfaces/IPatientRepository.cs
public interface IPatientRepository
{
    Task<PatientDto?> GetByIdAsync(int id);
    Task<IEnumerable<PatientDto>> SearchAsync(string query, int page = 1, int pageSize = 20);
    Task<int> CreateAsync(CreatePatientDto dto);
    Task UpdateAsync(int id, UpdatePatientDto dto);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
```

### Entity Example (Plain POCO)
```csharp
// Core/Entities/Patient.cs
public class Patient
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string MedicareNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### DTO Example
```csharp
// Core/DTOs/PatientDto.cs
public class PatientDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public DateTime DateOfBirth { get; set; }
    public string MedicareNumber { get; set; } = string.Empty;
    public int Age => CalculateAge(DateOfBirth);
}

public class CreatePatientDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string MedicareNumber { get; set; } = string.Empty;
}
```

### Repository Implementation Example
```csharp
// Infrastructure/Repositories/PatientRepository.cs
public class PatientRepository : IPatientRepository
{
    private readonly AppDbContext _context;

    public PatientRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PatientDto?> GetByIdAsync(int id)
    {
        var patient = await _context.Patients.FindAsync(id);
        return patient == null ? null : MapToDto(patient);
    }

    public async Task<IEnumerable<PatientDto>> SearchAsync(string query, int page = 1, int pageSize = 20)
    {
        var patients = await _context.Patients
            .Where(p => p.IsActive &&
                (p.FirstName.Contains(query) || p.LastName.Contains(query)))
            .OrderBy(p => p.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return patients.Select(MapToDto);
    }

    private static PatientDto MapToDto(Patient entity) => new()
    {
        Id = entity.Id,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        DateOfBirth = entity.DateOfBirth,
        MedicareNumber = entity.MedicareNumber
    };
}
```

### DI Registration
```csharp
// Api/Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
```

### Switching Databases
To switch from SQLite to SQL Server:
1. Change `UseSqlite()` to `UseSqlServer()` in DI registration
2. Update connection string in `appsettings.json`
3. Run `dotnet ef migrations add MigrationName` to generate new migration
4. No code changes needed in repositories or services

## Web UI conventions

### Grid and list states
Every grid or list shows one of three things, in this order — never a bare table:

1. **Loading** — `<LoadingState text="Loading prescriptions..." />` (`components/States.tsx`) while a
   request is in flight (or skeleton rows inside a table). Search pickers use `text="Searching..."`.
2. **Empty** — `<EmptyState message="No prescriptions recorded." icon="bi-capsule" />`
   (`components/States.tsx`) once loading has finished with no rows. When rows are only
   hidden by an "include archived" filter, say so (`AddClient.NoneMessage`).
3. **The grid itself.**

`DataGrid` takes `loading` + `emptyText` and does this itself. Keep the loading flag *true by default* on
pages that fetch on mount, otherwise the empty message flashes before the data arrives. Searches use
`useLatestSearch` (`lib/hooks.ts`) or a sequence ref: the latest request wins, so a slow earlier response cannot overwrite newer results. `EmptyAndLoadingStateTests`
holds API responses back with a Playwright route to prove each state.

## IntelliMed.Maui Project Structure (Standard Template)
```
IntelliMed.Maui/
├── App.xaml                     # Application resources
├── App.xaml.cs                  # Application entry point (InitializeComponent)
├── MauiProgram.cs               # Service registration, returns MauiApp
├── AppShell.xaml                # Root navigation shell
├── AppShell.xaml.cs
├── Platforms/
│   ├── Windows/
│   │   └── (Platform-specific files)
│   ├── Android/
│   └── iOS/
├── Resources/
│   ├── Styles/
│   │   ├── Colors.xaml
│   │   └── Styles.xaml
│   ├── AppIcon.appicon.svg
│   └── Splash/splash.svg
└── wwwroot/
    └── index.html               # For BlazorWebView
```

## Key Technical Notes

### SDK Configuration
- Use `Microsoft.NET.Sdk` (NOT `Microsoft.NET.Sdk.Razor`)
- Do NOT add custom `Program.cs` - SDK generates entry point

### MauiProgram.cs Pattern
```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .Services.AddMauiBlazorWebView();
    return builder.Build();  // Return MauiApp, not MauiAppBuilder
}
```

### App.xaml.cs Pattern
```csharp
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
```

### Dependencies
- Microsoft.AspNetCore.Components.WebView.Maui
- MudBlazor (UI components)
- Microsoft.Extensions.Configuration (config)