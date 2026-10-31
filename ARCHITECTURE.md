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

The web client's `MainLayout.tsx` (`src/IntelliMed.Client/src/layout/`) loads the signed-in user, their roles and the
pages their roles may open (`/api/role-permissions/my-pages`), and shows each sidebar link only when `can(pageKey)`
allows it. Pages are also protected at the API (`[RequirePage]` on the controllers) — the menu is a convenience, not
the security boundary.

```tsx
// MainLayout.tsx — permission-gated sidebar links
{can('admin/users') && <NavLink to="/admin/users" className="nav-link mdi-nav-link">User Management</NavLink>}
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

Last reviewed 2026-08-14. Phase 1 (web) is substantially complete — the sections below were badly
out of date and have been rewritten against the actual code rather than the original plan.

### Shipped

**Platform**
- [x] Clean Architecture solution (Core / Infrastructure / Api / Web), EF Core + SQLite, repository
      pattern with DTO mapping via `EntityMapper`
- [x] ASP.NET Identity with `ApplicationUser`, five seeded roles (SuperAdmin, Admin, Doctor, Nurse,
      Receptionist), JWT bearer auth
- [x] Global authorization fallback — every endpoint requires an authenticated user unless it opts
      out with `[AllowAnonymous]`, so a new controller is protected by default
- [x] RolePermissions catalogue (page-key based), editable live at `/admin/roles`, enforced through
      the single shared `RolePermissions.HasAccessAsync` helper (`Api/Authorization/`)
- [x] Multi-clinic support with per-clinic scoping and a blocking clinic selector
- [x] Per-provider patient visibility (`ClientProviderAccess`)

**Clinical**
- [x] Patients with addresses, referrals, family, occupations, compensation claims, user-defined fields
- [x] Contacts: address book of outside practitioners (referrers, specialists) linked to a clinic or
      hospital, and organisation clients as employers and payers
- [x] Server-side permission enforcement: `[RequirePage("key|other-key")]` (any one key suffices;
      `Api/Authorization/RequirePageAttribute.cs`, shared sets in `PageAccess.cs`) on every controller that
      previously checked nothing beyond "signed in". `AuthorizationCoverageTests` fails when a new action
      has no gate or a catalogue key is enforced nowhere.
- [x] Results inbox: HL7 v2 `ORU^R01` ingestion (browser upload, plus `POST /api/results/ingest` with a
      per-clinic API key), conservative patient matching, unmatched queue, acknowledge/hold audit trail.
      Tables: `ResultReports`, `ResultItems`, `ResultAttachments`, `ClientLabIdentifiers`,
      `ResultIngestKeys`. Page key `results` (not granted to Receptionist). Parser:
      `Infrastructure/Hl7/Hl7ResultParser.cs`; matching: `Infrastructure/Services/ResultIngestService.cs`
- [x] Appointments — list, calendar (drag to move/resize), waiting room, appointment types
- [x] Progress notes — SOAP, draft/finalize, append-only addenda, episodes, walk-in auto-appointment
- [x] Health issues (problem list), ported from Medinet's Past History
- [x] Allergies, feeding the prescribing safety check
- [x] Prescribing — seven-step wizard with restriction/schedule gating, issue + print, script history
- [x] Medicines catalogue (PBS + Wikidata sync, CSV import/export) and drug-interaction reference data

**Financial**
- [x] Invoices with lifecycle actions (refund, write-off, cancel, split), printing
- [x] Receipt / allocation payment model, receipt printing
- [x] Fee schedules with price history, derived item configs, account-type mapping

**Operations**
- [x] Scheduled and manual database backups
- [x] `/dbadmin` SQL console on a separate credential, isolated from staff auth/RBAC
- [x] Email templates + SMTP, user invite flow
- [x] Command palette (Ctrl+K) with an admin-editable action catalogue
- [x] Version endpoint (`GET /api/version`) surfaced in the UI, to spot a stale deployment at a glance

### Not built

Electronic prescribing (eRx) transmission — the `IPrescriptionTransmissionService` seam exists with a
not-configured implementation; Medicare/PRODA electronic claiming; My Health Record upload;
pathology/radiology results and HL7 ingestion; immunisations and AIR; referral-letter authoring; care
plans; recalls and reminders. Reporting page keys are seeded but no report screens exist yet.

See `USER_GUIDE.md` for what each shipped feature does, and the Medinet gap analysis for what is
worth porting next.

### Phase 2: Native clients (future)
- [ ] MAUI desktop project reusing the Blazor UI against the same API

### Phase 3: On-premise deployment (future)
- [ ] Windows Service host + Inno Setup installer (server/client modes)

## Configuration and secrets

**`Jwt:Key` is required and is deliberately absent from `appsettings.json`.** It signs both staff
tokens and the `/dbadmin` scope token, so a committed value would hand out full patient-database
access to anyone with repository or artifact access. Supply it as the `Jwt__Key` environment
variable, or in an untracked `appsettings.Production.json` beside the deployed binary.

Behaviour, implemented in `Api/Configuration/JwtSettings.cs`:
- missing key outside Development → **throws at startup** (fails loudly, never silently insecure);
- missing key in Development → generates an ephemeral per-run key and logs a warning;
- the previously-committed default key is rejected in **every** environment.

`DbAdmin:Username` / `DbAdmin:PasswordHash` follow the same rule — when absent, `/dbadmin` login is
disabled entirely rather than falling back to anything.

## File Locations

| Component | Path |
|-----------|------|
| Entities | `src/IntelliMed.Core/Entities/` |
| DTOs | `src/IntelliMed.Core/DTOs/` |
| Interfaces | `src/IntelliMed.Core/Interfaces/` |
| Repositories | `src/IntelliMed.Infrastructure/Repositories/` |
| DbContext | `src/IntelliMed.Infrastructure/Data/AppDbContext.cs` |
| Auth (client) | `src/IntelliMed.Client/src/api/auth.ts`, `src/IntelliMed.Client/src/session.tsx` |
| Login Page | `src/IntelliMed.Client/src/pages/Login.tsx` |
| UI Components | `src/IntelliMed.Client/src/components/` |

> The web client is React (`src/IntelliMed.Client`); it replaced the Blazor WASM client on 2026-10-31.
> Diagrams above that say "Blazor" describe the original plan — the API, roles and data flow are unchanged.

## Next Steps

The original step-by-step auth wiring instructions that lived here described work completed long ago
(Identity, JWT, roles, seeding — all shipped; see Implementation Status above) and have been removed
rather than left to mislead.

Current priorities, in order, from the Medinet feature-gap comparison:

1. **Investigations & results** — *inbox, HL7 v2 ingestion, matching and acknowledge are built.*
   Remaining: pathology/imaging requests with an outstanding-requests list, result-to-note/recall/
   patient-message actions, a Results tab on the patient record, and live lab or messaging-vendor feeds
   (needs a vendor agreement; the ingest endpoint is where they plug in).
2. **Recalls & reminders** — patient safety plus recurring revenue; much cheaper now that the health
   issues problem list exists to drive it.
3. **Referral letters** — outbound letter authoring and printing. Note `ClientReferral` is *inbound*
   referral tracking for Medicare eligibility and is not the same thing.
4. **Care plans (GPMP/TCA)** — directly billable MBS items, also keyed off the problem list.

Financial track, from the Pracnet parity audit (receipting, printing, lifecycle actions and the
invoice data model are all done): financial reporting (day sheet, aged debtors, banking, earnings),
then fee-calculation edge cases, then electronic claiming — deliberately last, since it involves real
government API certification.

Deferred indefinitely: eRx transmission and My Health Record, both of which require conformance
programs rather than just implementation effort.

### Phase 2: Native clients (future)
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