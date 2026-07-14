# IntelliMed Architecture

## Why ASP.NET Identity? ✅

**Decision:** Use ASP.NET Identity for authentication (not simple JWT or OAuth2)

**Rationale:**
| Requirement | ASP.NET Identity | Simple JWT | OAuth2 |
|-------------|-------------------|------------|--------|
| User registration | ✅ Built-in | ❌ Manual | ✅ Built-in |
| Password hashing | ✅ Built-in | ❌ Manual | ✅ Built-in |
| Password reset | ✅ Built-in | ❌ Manual | ✅ Built-in |
| Account lockout | ✅ Built-in | ❌ Manual | ✅ Built-in |
| Role-based access (RBAC) | ✅ Built-in | ❌ Manual | ⚠️ Partial |
| User management UI | ✅ Built-in | ❌ Manual | ⚠️ Partial |
| Web scalability (100s users) | ✅ Built-in | ⚠️ Manual | ✅ Built-in |
| TopShelf Windows Service | ✅ Works | ✅ Works | ✅ Works |
| Complexity | Medium | Low | High |

**User Requirements Met:**
- ✅ Web app deployment (future)
- ✅ Hundreds of concurrent users
- ✅ Role-based access (Admin, Doctor, Nurse, Receptionist)
- ✅ Windows Service hosting (TopShelf)
- ✅ JWT for API authentication

**Stack:** ASP.NET Identity + JWT tokens + TopShelf Windows Service = ✅ Fully Compatible

### Model 1: Client-Server (REST API) - RECOMMENDED
```
┌─────────────────┐         ┌─────────────────────────┐
│  IntelliMed     │         │     IntelliMed API       │
│  Desktop App    │◀───────▶│     (ASP.NET Core)       │
│  (MAUI/WPF)     │  REST   │                         │
│                 │         │  ┌───────────────────┐  │
│                 │         │  │  Controllers      │  │
│                 │         │  │  Auth Service     │  │
│                 │         │  └─────────┬─────────┘  │
│                 │         │            │            │
│                 │         │  ┌─────────▼─────────┐  │
│                 │         │  │  Repositories      │  │
│                 │         │  │  (EF Core)         │  │
│                 │         │  └─────────┬─────────┘  │
└─────────────────┘         │            │            │
                            │  ┌─────────▼─────────┐  │
                            │  │  Database          │  │
                            │  │  (SQL Server/      │  │
                            │  │   SQLite/Azure)    │  │
                            │  └───────────────────┘  │
                            └─────────────────────────┘
```

**Characteristics:**
- MAUI/WPF app is a thin client
- All business logic runs on the API server
- Database is on the server (can be local or cloud)
- App requires network connection to API
- Single deployment, easy to update

### Model 2: Embedded API (Offline-First)
```
┌─────────────────────────────────────────────────────────────┐
│                    IntelliMed Desktop App                    │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                    MAUI Shell (Win)                     │ │
│  │  ┌───────────────────────────────────────────────────┐  │ │
│  │  │              Blazor WebView                        │  │ │
│  │  │  ┌─────────────────────────────────────────────┐   │  │ │
│  │  │  │         IntelliMed.UI (Blazor)             │   │  │ │
│  │  │  │   (Same UI code as web, no changes needed) │   │  │ │
│  │  │  └─────────────────────────────────────────────┘   │  │ │
│  │  └───────────────────────────────────────────────────┘  │ │
│  │                                                          │  │
│  │  ┌───────────────────────────────────────────────────┐  │ │
│  │  │         Embedded ASP.NET Core API                  │  │ │
│  │  │  ┌─────────────┐  ┌─────────────────────────────┐  │  │ │
│  │  │  │ Auth Service│  │   Repository Layer          │  │  │ │
│  │  │  │ (ASP.NET   │  │   (Practitioner, Patient,   │  │  │ │
│  │  │  │  Identity) │  │    Appointment, Invoice)    │  │  │ │
│  │  │  └─────────────┘  └─────────────────────────────┘  │  │ │
│  │  │  ┌─────────────────────────────────────────────┐   │  │ │
│  │  │  │         SQLite Database (Local)              │   │  │ │
│  │  │  │   - Patients, Practitioners, Appointments    │   │  │ │
│  │  │  │   - Invoices, Payments                       │   │  │ │
│  │  │  └─────────────────────────────────────────────┘   │  │ │
│  │  └───────────────────────────────────────────────────┘  │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**Characteristics:**
- Single installer, fully self-contained
- No separate server needed
- Fully offline capable
- Database is local SQLite
- Harder to sync data across multiple machines

---

## Server Hosting Options

### Option 1: Windows Service (RECOMMENDED) ✅
**Best for:** Small to medium clinics (1-20 users)

**Technology:** TopShelf - A library that makes creating Windows Services easy.

**Why TopShelf?**
```csharp
// Write a console app...
public class Program
{
    public static void Main()
    {
        Host.CreateDefaultBuilder()
            .ConfigureServices(services => {
                services.AddHostedService<MyApiService>();
            })
            .RunAsService();  // ← One line makes it a Windows Service!
}
```

**Benefits over traditional Windows Services:**
| Feature | Without TopShelf | With TopShelf |
|---------|------------------|---------------|
| Debug | Attach debugger, painful | Just run as console app! |
| Install | `sc create` or custom installer | `MyApp.exe install` |
| Uninstall | `sc delete` | `MyApp.exe uninstall` |
| Config | XML in obscure location | Simple app.config |

**TopShelf Commands:**
```powershell
MyApp.exe install      # Install as Windows Service
MyApp.exe uninstall    # Remove the service
MyApp.exe start        # Start the service
MyApp.exe stop         # Stop the service
MyApp.exe              # Run as console app (for debugging)
```

**Setup:**
```powershell
# Install as Windows Service
IntelliMed.Server.exe install
IntelliMed.Server.exe start

# Or via installer (recommended)
IntelliMed-Setup.exe  # Choose "Server" installation
```

### Option 2: IIS (Enterprise)
**Best for:** Large clinics with IT staff

**Pros:**
- ✅ Built-in monitoring
- ✅ Easy SSL/HTTPS
- ✅ Auto-restart on failure
- ✅ Load balancing support

**Cons:**
- ❌ Needs Windows Server or Windows Pro
- ❌ More complex setup
- ❌ Requires IIS knowledge

### Option 3: Azure App Service (Cloud)
**Best for:** No on-prem server, cloud-first

**Pros:**
- ✅ No server management
- ✅ Auto-scaling
- ✅ Built-in SSL, monitoring

**Cons:**
- ❌ Monthly cost
- ❌ Internet dependency
- ❌ Data residency concerns (medical data)

```
┌─────────────────────────────────────────────────────────────┐
│                    IntelliMed Desktop App                    │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                    MAUI Shell (Win)                     │ │
│  │  ┌───────────────────────────────────────────────────┐  │ │
│  │  │              Blazor WebView                        │  │ │
│  │  │  ┌─────────────────────────────────────────────┐   │  │ │
│  │  │  │         IntelliMed.UI (Blazor)             │   │  │ │
│  │  │  │   (Same UI code as web, no changes needed) │   │  │ │
│  │  │  └─────────────────────────────────────────────┘   │  │ │
│  │  └───────────────────────────────────────────────────┘  │ │
│  │                                                          │  │
│  │  ┌───────────────────────────────────────────────────┐  │ │
│  │  │         Embedded ASP.NET Core API                  │  │ │
│  │  │  ┌─────────────┐  ┌─────────────────────────────┐  │  │ │
│  │  │  │ Auth Service│  │   Repository Layer          │  │  │ │
│  │  │  │ (ASP.NET   │  │   (Practitioner, Patient,   │  │  │ │
│  │  │  │  Identity) │  │    Appointment, Invoice)    │  │  │ │
│  │  │  └─────────────┘  └─────────────────────────────┘  │  │ │
│  │  │  ┌─────────────────────────────────────────────┐   │  │ │
│  │  │  │         SQLite Database (Local)              │   │  │ │
│  │  │  │   - Patients, Practitioners, Appointments    │   │  │ │
│  │  │  │   - Invoices, Payments                       │   │  │ │
│  │  │  └─────────────────────────────────────────────┘   │  │ │
│  │  └───────────────────────────────────────────────────┘  │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Key Architecture Decisions

### 1. Deployment Model
| Aspect | Decision | Rationale |
|--------|----------|-----------|
| **App Type** | MAUI Blazor Hybrid | Native Windows app with shared Blazor UI |
| **Backend** | REST API (ASP.NET Core) | Standard client-server communication |
| **Database** | SQL Server (server) / SQLite (local dev) | Flexible deployment options |
| **Installer** | Single MSI | Easy deployment, auto-updates possible |
| **Connectivity** | Requires API connection | All data operations via HTTP |

### 2. Communication Flow
```
┌──────────────┐     HTTP/REST      ┌──────────────┐     EF Core     ┌──────────────┐
│   MAUI UI   │ ──────────────────▶│  API Server  │ ───────────────▶│  Database    │
│  (Blazor)   │◀────────────────── │  (ASP.NET)   │◀─────────────── │  (SQL/Local) │
└──────────────┘    JSON Response   └──────────────┘   Data Access    └──────────────┘
```

1. User interacts with Blazor UI in MAUI
2. UI calls API endpoints via `HttpClient`
3. API controller processes request
4. Repository queries database via EF Core
5. Response returns as JSON to client

### 3. Authentication
- **Provider:** ASP.NET Identity + JWT tokens
- **User Store:** ASP.NET Identity tables (Users, Roles, UserRoles)
- **Token:** JWT stored in MAUI secure storage
- **Login Flow:** 
  1. Client sends credentials to `/api/auth/login`
  2. API validates against ASP.NET Identity
  3. API returns JWT token with user roles
  4. Client stores token and includes in subsequent requests

### 3.1 User Roles (RBAC)
| Role | Access Level |
|------|--------------|
| **Admin** | Full system access, user management, settings |
| **Doctor** | Patient records, appointments, clinical notes |
| **Nurse** | Patient records, vitals, basic appointments |
| **Receptionist** | Appointments, billing, patient check-in/out |

```csharp
// Example: Role-based authorization
[Authorize(Roles = "Admin")]
[HttpDelete("api/users/{id}")]
public async Task<IActionResult> DeleteUser(string id) { ... }

[Authorize(Roles = "Doctor,Nurse")]
[HttpGet("api/patients")]
public async Task<IActionResult> GetPatients() { ... }
```

### 3.2 ASP.NET Identity + TopShelf Compatibility ✅
ASP.NET Identity runs perfectly inside a TopShelf Windows Service:

```
┌─────────────────────────────────────────────────────────────┐
│         TopShelf Windows Service                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │  IntelliMed.Api (ASP.NET Core)                       │  │
│   │  ├── ASP.NET Identity (User management, Roles)        │  │
│   │  ├── JWT tokens (API authentication)                │  │
│   │  ├── Controllers (REST endpoints)                    │  │
│   │  └── Entity Framework Core (SQL Server)              │  │
│   └─────────────────────────────────────────────────────┘  │
│                          ↓                                   │
│   Install: MyApp.exe install                                │
│   Start: MyApp.exe start                                    │
│   Auto-start on boot ✅                                      │
└─────────────────────────────────────────────────────────────┘
```

**Technology Stack (All Compatible):**
| Layer | Technology | Purpose |
|-------|------------|---------|
| **Hosting** | TopShelf | Runs API as Windows Service |
| **Auth Framework** | ASP.NET Identity | User management, roles, passwords |
| **API Security** | JWT | Token-based authentication |
| **Database** | SQL Server | Stores users, roles, app data |

**Built-in ASP.NET Identity Features:**
| Feature | ASP.NET Identity | TopShelf |
|---------|------------------|----------|
| User registration | ✅ | - |
| Password hashing | ✅ | - |
| Password reset | ✅ | - |
| Account lockout | ✅ | - |
| Role management | ✅ | - |
| Login/logout | ✅ | - |
| Run as service | - | ✅ |
| Auto-start on boot | - | ✅ |
| Console mode for debug | - | ✅ |

### 4. Data Layer
- **Pattern:** Repository Pattern with interfaces
- **ORM:** Entity Framework Core
- **Database:** SQL Server (production), SQLite (development)
- **Entities:** Patient, Practitioner, Appointment, Invoice, Payment

### 5. API Layer
- **Framework:** ASP.NET Core Web API
- **Controllers:** RESTful endpoints for each entity
- **Versioning:** URL-based (`/api/v1/...`)
- **Documentation:** Swagger/OpenAPI

### 6. UI Layer
- **Framework:** Blazor (same code for web and desktop)
- **HTTP Client:** `HttpClient` service injected via DI
- **Components:** Reusable across web and MAUI
- **Theming:** Light/Dark mode support

## Project Dependencies

### Client-Side (MAUI)
```
IntelliMed.Maui (MAUI Blazor Hybrid)
    ├── IntelliMed.UI (Blazor UI - shared)
    │   └── HttpClient (for API calls)
    └── Microsoft.AspNetCore.Components.WebView.Maui
```

### Server-Side (API)
```
IntelliMed.Api (ASP.NET Core Web API)
    ├── Controllers (REST endpoints)
    ├── Services (Auth, Business Logic)
    ├── IntelliMed.Core (Entities, DTOs, Interfaces)
    └── IntelliMed.Infrastructure (EF Core, Repositories)
        └── Database (SQL Server / SQLite)
```

### Communication
- MAUI app uses `HttpClient` to call API endpoints
- JSON serialization/deserialization for data transfer
- JWT tokens for authentication header

## Implementation Status

### Completed
- [x] Project structure with Clean Architecture
- [x] Entity Framework Core setup with SQLite
- [x] Repository pattern implementation
- [x] Basic Blazor UI with navigation
- [x] User dropdown menu with logout
- [x] API project structure (IntelliMed.Api)
- [x] `PasswordHash` field added to Practitioner entity

### In Progress
- [ ] Add ASP.NET Identity NuGet packages
- [ ] Create `ApplicationUser` class (inherits from `IdentityUser`)
- [ ] Update `AppDbContext` to `IdentityDbContext<ApplicationUser>`
- [ ] Add Email index and seed data to AppDbContext
- [ ] Add role definitions (Admin, Doctor, Nurse, Receptionist)
- [ ] Add `GetByEmailAsync` to IPractitionerRepository
- [ ] Implement `GetByEmailAsync` in PractitionerRepository
- [ ] Create AuthController with login/logout endpoints
- [ ] Add JWT authentication to API
- [ ] Update AuthService to call API instead of local storage
- [ ] Remove hardcoded credentials from Login.razor

### Planned
- [ ] IntelliMed.WindowsService project (TopShelf wrapper)
  - TopShelf NuGet: `TopShelf` package
  - Simple `RunAsService()` configuration
  - Console mode for debugging
  - Commands: `install`, `uninstall`, `start`, `stop`
- [ ] Installer project with Inno Setup
  - Single installer with Client/Server options
  - Server: Install Windows Service + create database
  - Client: Install app + prompt for API URL
  - Auto-start service on boot

## File Locations

| Component | Path |
|-----------|------|
| Entities | `src/IntelliMed.Core/Entities/` |
| DTOs | `src/IntelliMed.Core/DTOs/` |
| Interfaces | `src/IntelliMed.Core/Interfaces/` |
| Repositories | `src/IntelliMed.Infrastructure/Repositories/` |
| DbContext | `src/IntelliMed.Infrastructure/Data/AppDbContext.cs` |
| Auth Service | `src/IntelliMed.Web/Services/AuthService.cs` |
| Login Page | `src/IntelliMed.Web/Pages/Login.razor` |
| UI Components | `src/IntelliMed.UI/` |

## Next Steps

### 1. Complete Auth Wiring (API Side)
- Add ASP.NET Identity NuGet packages:
  ```
  Microsoft.AspNetCore.Identity.EntityFrameworkCore
  Microsoft.IdentityModel.Tokens
  System.IdentityModel.Tokens.Jwt
  ```
- Create `ApplicationUser` class:
  ```csharp
  public class ApplicationUser : IdentityUser
  {
      public string FirstName { get; set; }
      public string LastName { get; set; }
      // Add clinic-specific fields
  }
  ```
- Update `AppDbContext` to inherit from `IdentityDbContext<ApplicationUser>`
- Add role definitions (Admin, Doctor, Nurse, Receptionist)
- Create `AuthController` with `/api/auth/login` endpoint
- Implement JWT token generation with roles
- Add seed data for initial admin user

### 2. Update AuthService (Client Side)
- Replace `IClientStorage` calls with `HttpClient` calls
- Send login request to `/api/auth/login`
- Store JWT token in secure storage
- Include token in all subsequent API requests

### 3. Create Windows Service Project (TopShelf)
- Create `IntelliMed.WindowsService` project
- Add TopShelf NuGet: `TopShelf`
- Configure `Program.cs` with TopShelf host:
  ```csharp
  Host.CreateDefaultBuilder()
      .ConfigureWebHostDefaults(web => web.UseStartup<Startup>())
      .UseUrls("http://0.0.0.0:5000")
      .RunAsService();
  ```
- Test locally: Run as console app for debugging
- Install: `MyApp.exe install` then `MyApp.exe start`
- Uninstall: `MyApp.exe uninstall`

### 4. Create Installer
- Set up WiX or Inno Setup project
- Add installation type selection (Client/Server)
- For Server: Install Windows Service + create database
- For Client: Install app + prompt for API URL
- Add uninstaller that removes service (if server install)

### 5. Configure API Base URL
- Store API URL in app settings
- Allow user to change API URL in settings
- Add connection test on startup

### 6. Build & Package
- Create MSI/EXE installer
- Test both installation types
- Configure auto-update mechanism