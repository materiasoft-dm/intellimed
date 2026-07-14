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
│   └── IntelliMed.Maui/             # .NET MAUI Blazor Hybrid App (Frontend)
```

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