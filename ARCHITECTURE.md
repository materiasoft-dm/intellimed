# IntelliMed Architecture

## Phase 1: Web Development

**Focus:** Build the web application first, then add native clients later.

### Deployment Modes (All Supported)

| Mode | Description | Use Case | Database | Auth |
|------|-------------|----------|----------|------|
| **1. All Web** | Standard web app | Cloud-hosted, multi-clinic | SQL Server/Azure | ASP.NET Identity |
| **2. Web + Native** | Web app + MAUI clients | Clinic with tablets/window | SQL Server (local) | ASP.NET Identity |
| **3. Fully Offline** | Self-contained on-premise | Single clinic, no internet | SQLite | ASP.NET Identity |

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         PHASE 1: WEB DEVELOPMENT                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   MODE 1: All Web                    MODE 2: Web + Native               │
│   ┌─────────────┐                    ┌─────────────┐ ┌─────────────┐    │
│   │   Browser   │                    │   Browser   │ │  MAUI App   │    │
│   │  (Blazor)   │                    │  (Blazor)   │ │  (Blazor)   │    │
│   └──────┬──────┘                    └──────┬──────┘ └──────┬──────┘    │
│          │                                  │               │           │
│          └──────────────┬───────────────────┘───────────────┘           │
│                         │                                                │
│                         ▼                                                │
│                  ┌─────────────┐                                        │
│                  │  IntelliMed  │                                        │
│                  │    API       │                                        │
│                  │ (ASP.NET)    │                                        │
│                  └──────┬──────┘                                        │
│                         │                                                │
│                         ▼                                                │
│                  ┌─────────────┐                                        │
│                  │  SQL Server  │                                        │
│                  │  (Azure/    │                                        │
│                  │   Local)     │                                        │
│                  └─────────────┘                                        │
│                                                                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   MODE 3: Fully Offline (On-Premise)                                    │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                    Single Server/PC                             │   │
│   │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐                  │   │
│   │  │   Browser   │ │  MAUI App  │ │  Windows   │                  │   │
│   │  │  (Blazor)   │ │  (Blazor)  │ │  Service   │                  │   │
│   │  └──────┬──────┘ └──────┬──────┘ └─────┬─────┘                  │   │
│   │         │               │              │                         │   │
│   │         └───────────────┼──────────────┘                         │   │
│   │                         │                                        │   │
│   │                         ▼                                        │   │
│   │                  ┌─────────────┐                                 │   │
│   │                  │  IntelliMed  │                                │   │
│   │                  │    API       │                                │   │
│   │                  │ (ASP.NET)    │                                │   │
│   │                  └──────┬──────┘                                 │   │
│   │                         │                                        │   │
│   │                         ▼                                        │   │
│   │                  ┌─────────────┐                                 │   │
│   │                  │   SQLite    │                                 │   │
│   │                  │  (Local)    │                                 │   │
│   │                  └─────────────┘                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Phase 1 Priorities
1. **Web App** - Blazor Server or Blazor WebAssembly with API
2. **Authentication** - ASP.NET Identity with JWT
3. **Database** - SQL Server (production-ready)
4. **API** - REST endpoints for future native clients

### Future (Phase 2+)
- MAUI desktop apps connecting to same API
- Windows Service for on-premise deployments
- Offline sync capabilities

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

### 3.1 Role-Based Access Control (RBAC)

IntelliMed uses ASP.NET Identity roles for authorization. Each role grants access to specific screens and API endpoints.

#### Role Definitions

| Role | Description | Access Level |
|------|-------------|--------------|
| **SuperAdmin** | System owner / clinic director. Full access to everything including user management. | All screens + User Management + Role assignment |
| **Admin** | Clinic administrator. Manages users and system settings. | All screens + User Management |
| **Doctor** | Medical practitioner. Clinical records, appointments, prescriptions. | Patients, Appointments, Invoices (view) |
| **Nurse** | Nursing staff. Patient vitals, basic appointments, clinical notes. | Patients, Appointments |
| **Receptionist** | Front desk. Patient check-in/out, scheduling, billing. | Patients, Appointments, Invoices |

#### SuperAdmin Concept

The **SuperAdmin** is the highest-privilege role. Key characteristics:

- **Current user as SuperAdmin**: The initial admin user (`admin@clinic.com`) is seeded with both **SuperAdmin** and **Admin** roles.
- **Cannot be deleted**: SuperAdmin users are protected from deletion/deactivation via the Admin API.
- **Full access**: SuperAdmin can access ALL pages including the User Management screen.
- **Role assignment**: Only SuperAdmin and Admin can assign roles to other users.

#### Screen Access Matrix

| Screen / Feature | SuperAdmin | Admin | Doctor | Nurse | Receptionist |
|------------------|:----------:|:-----:|:------:|:-----:|:------------:|
| Dashboard (Home) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Patients | ✅ | ✅ | ✅ | ✅ | ✅ |
| Appointments | ✅ | ✅ | ✅ | ✅ | ✅ |
| Invoices | ✅ | ✅ | ✅ | ❌ | ✅ |
| **User Management** | ✅ | ✅ | ❌ | ❌ | ❌ |
| Role Configuration | ✅ | ✅ | ❌ | ❌ | ❌ |
| Audit Log | ✅ | ✅ | ❌ | ❌ | ❌ |
| System Settings | ✅ | ✅ | ❌ | ❌ | ❌ |

#### API Authorization

```csharp
// SuperAdmin + Admin only
[Authorize(Roles = "SuperAdmin,Admin")]
[HttpGet("api/admin/users")]
public async Task<IActionResult> GetUsers() { ... }

// All clinical staff
[Authorize(Roles = "SuperAdmin,Admin,Doctor,Nurse")]
[HttpGet("api/patients")]
public async Task<IActionResult> GetPatients() { ... }

// Receptionist can access billing
[Authorize(Roles = "SuperAdmin,Admin,Receptionist")]
[HttpGet("api/invoices")]
public async Task<IActionResult> GetInvoices() { ... }
```

#### User Management API Endpoints

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| `GET` | `/api/admin/users` | SuperAdmin, Admin | List all users with roles |
| `GET` | `/api/admin/users/{id}` | SuperAdmin, Admin | Get single user details |
| `POST` | `/api/admin/users` | SuperAdmin, Admin | Create new user with role assignment |
| `PUT` | `/api/admin/users/{id}` | SuperAdmin, Admin | Update user profile, status, roles |
| `DELETE` | `/api/admin/users/{id}` | SuperAdmin, Admin | Soft-delete (deactivate + lockout) |
| `POST` | `/api/admin/users/{id}/reset-password` | SuperAdmin, Admin | Admin password reset |
| `GET` | `/api/admin/roles` | SuperAdmin, Admin | List all roles with descriptions |
| `PUT` | `/api/admin/users/{id}/roles` | SuperAdmin, Admin | Assign roles (replaces all) |

#### Seeded Default Users

| Email | Password | Roles |
|-------|----------|-------|
| `admin@clinic.com` | `IntelliMed2024!` | SuperAdmin, Admin |

#### Client-Side Role Checking

The `MainLayout.razor` loads the current user's roles via `IAuthService.GetCurrentUserAsync()` and conditionally renders the **Admin** menu (visible only to SuperAdmin/Admin roles). The User Management page is at `/admin/users` and is protected both at the UI level (menu visibility) and API level (controller authorization).

```csharp
// MainLayout.razor — role-based menu visibility
@if (_currentUserRoles.Any(r => r == "SuperAdmin" || r == "Admin"))
{
    <MudMenu Icon="@Icons.Material.Filled.AdminPanelSettings" Color="Color.Error" Label="Admin">
        <MudMenuItem OnClick="NavigateToUserManagement">User Management</MudMenuItem>
        ...
    </MudMenu>
}
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

### Phase 1: Web Development (Current Focus)

#### Completed
- [x] Project structure with Clean Architecture
- [x] Entity Framework Core setup with SQLite
- [x] Repository pattern implementation
- [x] Basic Blazor UI with navigation
- [x] User dropdown menu with logout
- [x] API project structure (IntelliMed.Api)
- [x] `PasswordHash` field added to Practitioner entity

#### In Progress (Web Auth)
- [ ] Add ASP.NET Identity NuGet packages
- [ ] Create `ApplicationUser` class (inherits from `IdentityUser`)
- [ ] Update `AppDbContext` to `IdentityDbContext<ApplicationUser>`
- [ ] Add Email index and seed data to AppDbContext
- [ ] Add role definitions (Admin, Doctor, Nurse, Receptionist)
- [ ] Create AuthController with login/logout endpoints
- [ ] Add JWT authentication to API
- [ ] Update AuthService to call API instead of local storage
- [ ] Remove hardcoded credentials from Login.razor

#### Phase 1 Complete (Web App)
- [ ] Patient management pages
- [ ] Appointment scheduling
- [ ] Practitioner management
- [ ] Invoice/billing pages
- [ ] User management (Admin)

### Phase 2: Native Clients (Future)
- [ ] MAUI desktop app project
- [ ] Connect MAUI to existing API
- [ ] Blazor UI reuse from web

### Phase 3: On-Premise Deployment (Future)
- [ ] IntelliMed.WindowsService project (TopShelf wrapper)
  - TopShelf NuGet: `TopShelf` package
  - Simple `RunAsService()` configuration
  - Console mode for debugging
  - Commands: `install`, `uninstall`, `start`, `stop`
- [ ] Installer project with Inno Setup
  - Single installer with deployment mode selection
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

### Phase 1: Complete Auth Wiring (Web)

#### 1. API Side - Add ASP.NET Identity
- Add NuGet packages:
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

#### 2. Client Side - Update AuthService
- Replace `IClientStorage` calls with `HttpClient` calls
- Send login request to `/api/auth/login`
- Store JWT token in secure storage
- Include token in all subsequent API requests

#### 3. UI Side - Remove Hardcoded Credentials
- Update `Login.razor` to call `AuthService.LoginAsync()`
- Remove hardcoded username/password checks
- Add proper error handling for failed login

### Phase 2: Native Clients (Future)
- Create `IntelliMed.Desktop` MAUI project
- Connect MAUI to existing API endpoints
- Reuse Blazor UI components from web

### Phase 3: On-Premise Deployment (Future)
- Create `IntelliMed.WindowsService` project with TopShelf
- Configure TopShelf host:
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