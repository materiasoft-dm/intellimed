# IntelliMed Rebuild Project Plan

> **Reference:** See `handoff.md` for detailed legacy Pracnet system documentation (architecture, database, billing, Medicare claiming, integrations, etc.)

## 1. Project Goal
Modernize the IntelliMed practice-management system from legacy architecture to .NET MAUI Blazor Hybrid.

## 2. Modernization Stack
- **Backend:** ASP.NET Core Web API (.NET 8/9)
- **Frontend:** .NET MAUI Blazor Hybrid
    - **Web:** Blazor WebAssembly (WASM)
    - **Windows/Android/iOS:** .NET MAUI wrappers for native capabilities.
- **Database:** SQLite (initially), designed for SQL Server, MySQL, LiteDB, MongoDB
- **UI Framework:** MudBlazor
- **Architecture:** Clean Architecture with Repository Pattern and DTO-first data layer

## 3. Data Layer Architecture

### Design Principles
1. **DTOs only in data layer** - Entities are plain POCOs, not tracked by EF
2. **Repository Pattern** - All data access through interfaces
3. **Easy database switching** - Swap Infrastructure implementation, no business logic changes
4. **Testability** - Mock repositories for unit testing

### Repository Pattern
```
Core (Domain)
├── Entities (plain POCOs, no EF attributes)
├── DTOs (request/response models)
└── Interfaces (IRepository<T>, IPatientRepository, etc.)

Infrastructure (Data)
├── Repositories (implements interfaces)
├── Mappers (Entity ↔ DTO conversion)
└── DbContext (SQLite for now, swappable)

Api
├── Controllers (use DTOs, inject repositories)
└── Services (optional business logic layer)
```

### Example Interface
```csharp
// In Core
public interface IPatientRepository
{
    Task<PatientDto?> GetByIdAsync(int id);
    Task<IEnumerable<PatientDto>> SearchAsync(string query);
    Task<int> CreateAsync(CreatePatientDto dto);
    Task UpdateAsync(int id, UpdatePatientDto dto);
    Task DeleteAsync(int id);
}
```

### Adding New Database Providers
To add a new database (e.g., MongoDB):
1. Create new project `IntelliMed.Infrastructure.MongoDB`
2. Implement same repository interfaces
3. Register in DI container
4. No changes needed in Core, Api, or Maui

### Database Support Strategy
| Database | Status | Notes |
|----------|--------|-------|
| SQLite | ✅ Initial | Easy dev, file-based |
| SQL Server | Planned | Production deployment |
| MySQL | Planned | Alternative RDBMS |
| LiteDB | Planned | Embedded, no server |
| MongoDB | Planned | Document database |

## 5. Technical Findings (Lessons Learned)

### .NET MAUI Project Setup Requirements
1. **SDK:** Must use `Microsoft.NET.Sdk` (NOT `Microsoft.NET.Sdk.Razor`)
2. **Entry Point:** Do NOT create a custom `Program.cs` - the SDK generates it automatically
3. **MauiProgram.cs:** Return `MauiApp` (call `.Build()`), NOT `MauiAppBuilder`
4. **App Pattern:** Use `App.xaml` + `App.xaml.cs` with `InitializeComponent()` in constructor
5. **Window Creation:** Override `CreateWindow(IActivationState? activationState)` returning `new Window(new AppShell())`
6. **Resources:** Include `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml`
7. **AppShell:** Create `AppShell.xaml` + `AppShell.xaml.cs` as the root navigation shell

### Common Build Errors & Fixes
- **CS5001 "No static Main method":** Don't add Program.cs - SDK provides it
- **"MauiApp does not contain Application":** Return `MauiApp` from MauiProgram, not `MauiAppBuilder`
- **"StaticWebAssetsPrepareForRun target does not exist":** Wrong SDK or missing Maui workload
- **"MauiAppBuilder has no Build()":** Return type should be `MauiApp`, call `.Build()` before returning

## 6. Project Structure
```
src/
├── IntelliMed.Api/              # ASP.NET Core Web API (Backend)
├── IntelliMed.Core/             # Domain Entities, DTOs, Repository Interfaces
├── IntelliMed.Infrastructure/   # EF Core, Repositories, Mappers (SQLite)
├── IntelliMed.Infrastructure.SqlServer/  # SQL Server implementation (future)
├── IntelliMed.Infrastructure.MongoDB/    # MongoDB implementation (future)
├── IntelliMed.Shared/           # Shared DTOs and Request/Response models
└── IntelliMed.Maui/             # .NET MAUI Blazor Hybrid App (Frontend)
```

## 7. Development Notes
- Use `dotnet new maui` template as baseline - do not hand-craft project structure
- When adding BlazorWebView, use `Microsoft.AspNetCore.Components.WebView.Maui` package
- For MudBlazor integration, add services in MauiProgram.cs: `builder.Services.AddMauiBlazorWebView();`
- Windows target: `net9.0-windows10.0.19041.0`

## 8. Deployment Environments

### Staging
- **URL:** https://intellimed-staging.onrender.com
- **Provider:** Render.com
- **Method:** Docker container (see `Dockerfile`)
- **Purpose:** Pre-production testing and validation
- **Database:** SQLite (file-based, `/app/data/intellimed.db`)
- **Notes:**
  - Auto-deploys from `main` branch
  - Build command: `dotnet publish -c Release`
  - Run command: `dotnet IntelliMed.Api.dll`
  - Port: 80 (HTTP)
  - See `Dockerfile` for full multi-stage build configuration

### Local Development
- **URL:** http://localhost:5284 (API)
- **Database:** SQLite (`intellimed.db` in project root)
- **Notes:**
  - Run via `dotnet run` in `IntelliMed.Api` project
  - Blazor WASM client available at `/`
  - Database migrations run automatically on startup

### Production (Future)
- **Provider:** TBD
- **Notes:** SQL Server recommended for production scale
