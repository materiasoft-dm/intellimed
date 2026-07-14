# Pracnet developer handover

_Technical handover for a developer taking over Pracnet. Generated 08/07/2026 from the current codebase. Repo-relative paths throughout; no credentials or patient data are included._

## Contents

1. [Overview and architecture](#1-overview-and-architecture)
2. [Building, running and releasing](#2-building-running-and-releasing)
3. [Application startup and cross-module messaging](#3-application-startup-and-cross-module-messaging)
4. [User management and security](#4-user-management-and-security)
5. [Database, Entity Framework and data access](#5-database-entity-framework-and-data-access)
6. [Database migrations and the update tool](#6-database-migrations-and-the-update-tool)
7. [Invoices, billing and fee calculation](#7-invoices-billing-and-fee-calculation)
8. [Medicare Online claiming (BBSW, IMC, DVA, OVS, OEC) and PRODA](#8-medicare-online-claiming-bbsw-imc-dva-ovs-oec-and-proda)
9. [Claiming reports (DB4 and DVA forms)](#9-claiming-reports-db4-and-dva-forms)
10. [Tyro EFTPOS integration](#10-tyro-eftpos-integration)
11. [LanternPay and eTAC (TAC) integration](#11-lanternpay-and-etac-tac-integration)
12. [Medinet clinical-system integration](#12-medinet-clinical-system-integration)
13. [AIR and ACIR immunisation](#13-air-and-acir-immunisation)
14. [Printing and Crystal Reports](#14-printing-and-crystal-reports)
15. [Document scanning and shared documents](#15-document-scanning-and-shared-documents)
16. [Conventions, testing and gotchas](#16-conventions-testing-and-gotchas)
17. [Deployment: release notes and version increments](#17-deployment-release-notes-and-version-increments)
18. [Continuous integration and the updater build](#18-continuous-integration-and-the-updater-build)

---

## 1. Overview and architecture

### 1.1 What Pracnet is

Pracnet is a large Australian healthcare practice-management desktop application. It is the billing, appointments, patient-management and claiming engine behind PrimaryClinic (Global Health Limited's practice-management product line). Functionally it covers patient management, billing against Medicare, DVA and private health funds, appointment scheduling, immunisation (ACIR), clinical-system linking (Medinet), document scanning and management, and online claiming to Services Australia.

Technically it is a .NET Framework 4.5, C#, WinForms application. It is not .NET Core or .NET 5+, so everything you write here targets the classic full framework and the classic WinForms message loop. The primary build platform is x86 (32-bit), with a handful of AnyCPU projects. The main executable project is `SourceCode/Pracnet B1/` (assembly `Pracnet.exe`), and the whole thing is stitched together by one very large solution, `SourceCode/Pracnet.sln`, which contains roughly 100 buildable projects plus a set of solution folders (121 `Project(...)` entries in total, some of which are just folders such as `Common`, `Billing`, `Core`, `Security`, `Claiming`).

A new developer should treat this as a mature, layered line-of-business application with a long history: expect Database-First Entity Framework, hand-written singletons, WinForms user controls named `frm<Name>`, and a mix of naming conventions accreted over many years.

### 1.2 The Abaki namespace and layering convention

Almost every project and namespace uses the `Abaki.*` prefix, and the layering is expressed directly in the namespace as `Abaki.<Domain>.<Layer>`. For example `Abaki.Billing.Dal` is the billing data-access layer, `Abaki.Billing.Bll` is the billing business-logic layer, `Abaki.Billing.Gui` is the billing presentation layer. Once you internalise this convention you can usually guess which project a class lives in from its namespace, and vice versa.

The architecture is a strict layered pattern. The five layers, and where each concern lives, are:

| Layer | Representative projects | Responsibility |
|---|---|---|
| Presentation (GUI) | `Pracnet B1`, `Abaki.Core.Gui B1`, `Abaki.Billing.Gui B1`, `AppointmentGUI B1`, `Abaki.SecurityGUI`, `ScanDoc` | WinForms forms and user controls, event handlers, grids |
| Business logic (BLL) | `Abaki.Billing.Bll B1`, `Abaki.Core.Service`, `Abaki.Business.Core` | Presenters, controllers, services, calculation logic |
| Data access (DAL) | `Abaki.Core.Dal`, `Abaki.Billing.Dal B1`, `AppointmentDAL B1` | EF contexts, repositories, Dapper queries, stored-proc calls |
| Domain / data | `Abaki.Data`, `Abaki.Data.DataContext`, `Abaki.Data.DTOs`, `Abaki.Data.SpecialDtoObjects` | EF entities (`.edmx`-derived domain classes), DTOs, projections |
| Common / shared | 16 `Abaki.Common.*` projects plus `Abaki.Common` | Utilities, enums, helpers, extensions, logging, message strings, local settings |

The dependency direction runs top-to-bottom: GUI depends on BLL, BLL depends on DAL, DAL depends on Domain/Data, and every layer may depend on Common/Shared. Common/Shared depends on nothing above it. When you add code, respect this direction. A recurring real-world example: `SubmissionAuthorityIndMapper` and `BenefitAssignmentAuthorisedIndMapper` were deliberately placed in `Abaki.Common.Helpers` (not in the Billing GUI) so that both the Billing GUI and the test project can reference them without creating a circular dependency. When you find yourself wanting a GUI project to reference another GUI project, that mapper-in-Common pattern is usually the correct answer instead.

There are 16 `Abaki.Common.*` shared projects. The ones you will touch most often:

- `Abaki.Common` and `Abaki.Common.Utils` / `Abaki.Common.Helpers` - general utilities, `GeneralUtils`, `DatabaseConnectionHelper`, claim-field mappers.
- `Abaki.Common.Enums` - domain enums such as `eClaimStatus`, `eClaimTypeReport`, `eServiceTypeCde`.
- `Abaki.Common.Logger` - `ErrorLogger` and the claiming logger wrappers over log4net.
- `Abaki.Common.LocalSettings` - `LocalSetting` singleton and per-machine configuration.
- `Abaki.Common.Extensions`, `Abaki.Common.Converter(s)`, `Abaki.Common.AutoFormat`, `Abaki.Common.MessageStrings`, `Abaki.Common.Security`, `Abaki.Common.SMS`, `Abaki.Common.ReportUtils`, `Abaki.Common.FormUtils`, `Abaki.Common.Exceptions`, `Abaki.Common.ApplicationGlobalInformation`.

### 1.3 Key architectural patterns

Four patterns recur throughout the codebase and are worth learning up front.

Presenter / controller pattern in the BLL. Billing screens are driven by presenter and controller classes rather than putting logic in the form. Examples in `Abaki.Billing.Bll B1`: `InvoicePresenter`, `ReceiptPresenter`, `InvItemPresenter`, and `MOClaimController`. The WinForms control holds a reference to its presenter and delegates calculation and persistence to it.

Mediator pattern for cross-module messaging. `CoreMediator` (defined at `SourceCode/Abaki.Core.Dal/CoreMessage/CoreMediator.cs`, with a GUI-layer counterpart under `Abaki.Core.Gui B1/CoreMessage/`) is a singleton derived from `MediatorBase<eCoreMessage>`. Modules register handlers keyed by an `eCoreMessage` enum value and raise notifications via `CoreMediator.Instance.NotifyColleagues(...)`. This is how the billing, receipt, immunisation and appointment modules communicate without direct references. Registration happens at startup in `MainApp.InitilizeMediator()` (note the historical spelling of the method name) for messages like `NewInvoice`, `EditInvoice`, `NewReceipt`, `NewDeposit`, `NewImm`, `PatientArriving` and `OpenClinicalData`. There is also a lighter-weight `GroupMediator` singleton in the same file keyed by string group names.

Singleton helpers hold global state. The application leans heavily on `Instance` singletons rather than dependency injection. The important ones: `DatabaseConnectionHelper.Instance` (holds the EF connection strings and metadata mappings, see below), `LocalSetting.Instance`, `RecallGlobalSetting.Instance` (global practice settings such as `SharedStoragePath`, `ProfileStorageFolder`, `DefaultFeeRate`, `LocationId`), and the Dapper context accessor `.Instance`. Because these are process-wide singletons, be careful about test isolation and thread affinity: several of them are populated at login and assume the WinForms UI thread.

DTOs and projections for cross-layer data transfer. `Abaki.Data.DTOs` and `Abaki.Data.SpecialDtoObjects` carry shaped data between layers, and SQL views are projected into read-only entities (for example `InvoiceItemView` exists as a projection even though `SentClaimStatus` is not a column on the persisted `InvoiceItem` entity). Do not assume a field on a view projection also exists on the backing entity.

### 1.4 Data access and the database

The persistence stack is Entity Framework 6 (Database-First, using `.edmx` models), running against SQL Server, with Dapper used alongside EF for performance-critical queries. Migration scripts live under `Database/PracnetDatabaseFrom0.1.6/` in versioned revision folders (0.1.6 through 0.1.9), and are applied by the separate database-update tooling at release time.

There is not one EF context but several, split by subject area. The context classes live in `SourceCode/Abaki.Data.DataContext/`:

- `CommonContext`, `CoreContext`, `CoreViewContext`
- `BillingContext`, `BillingViewsContext`
- `MedicareContext`, `MedicareOnlineContext`

Each context is backed by its own `.edmx` metadata resource. The mapping between a context and its conceptual/store/mapping resources is centralised in `SourceCode/Abaki.Common.Helpers/DatabaseConnectionHelper.cs`, which declares the `res://*/Domains.*.csdl|...ssdl|...msl` metadata strings for Core, CoreView, Common, NewCommon, TransactionLog, Medicare, Security, ACIR, MedicareOnline, BillingViews and Billing, plus a P2K model used by the migration tooling. `DatabaseConnectionHelper` is a private-constructor singleton that holds the runtime provider connection strings; the connection itself is resolved per profile via `ProfileDB`. When EF throws a "data reader is incompatible" error at runtime, that almost always means a store-model migration has not been applied to the target database rather than a code bug (see the team memory note on `ViewInvoiceSearch` schema drift).

Practical implication for a new developer: if you change a table shape you generally have to update both the relevant `.edmx` in `Abaki.Data` and add a numbered SQL migration under `Database/PracnetDatabaseFrom0.1.6/Revision_*/`.

### 1.5 Application entry point and startup flow

The entry point is `MainApp.Main()` in `SourceCode/Pracnet B1/MainApp.cs`. The startup sequence is:

1. Wire global exception handling: `AppDomain.CurrentDomain.UnhandledException`, `Application.ThreadException`, and `Application.ApplicationExit`. Unhandled exceptions are logged through `ErrorLogger` and surfaced via `PracnetMessageBox`.
2. Enforce single-instance. Unless the process is started with an argument of `1`/`true` (which allows multiple instances), `Main` creates a named `Mutex` (prefixed `Local\Mutex_` plus a GUID derived from the executable path) and a `MemoryMappedFile` shared-memory block. If the mutex already exists, the code reads the existing window handle out of shared memory and brings the running instance to the foreground instead of starting a second copy. See `MainApp.cs:173` onward.
3. `RunApp()` (`MainApp.cs:391`) shows the splash screen (`frmSplash`), sets the culture to Australian English with `Thread.CurrentThread.CurrentCulture = new CultureInfo("en-AU")` (this is why Medicare, MBS, DVA and currency formatting behave as en-AU throughout), then shows the login form `frmLogin`.
4. Authentication runs through `AuthenticateUser` / `MedinetAuthenticate`, supporting both direct Pracnet login and Medinet single-sign-on.
5. On success it constructs the main form `MainForm = new frmMain2()` (`MainApp.cs:561`), caches `frmPracnetMain`, and finally calls `InitilizeMediator()` to register the cross-module message handlers.

Note the About-dialog version quirk: the version string shown in the About/login screen is sourced from `Abaki.SecurityGUI`'s assembly file version, not from `Pracnet.exe`. This matters at release time and is documented in `.claude/rules/assembly-version.md` under "always-bumped projects".

### 1.6 Key third-party dependencies

NuGet packages are restored into `SourceCode/packages/`. The libraries a new developer will encounter most:

- Entity Framework 6 (multiple 6.x versions are present in `packages/`; 6.0.0 is the baseline noted in project docs) - the ORM.
- Dapper (2.0.78) - micro-ORM for hot-path SQL and stored-proc calls alongside EF.
- log4net (1.2.10) - logging. Configured via the `<log4net>` section in `SourceCode/Pracnet B1/App.config` with a `RollingFileAppender`. Application code logs through `ErrorLogger.WriteLog(...)`; claiming code logs through `ClaimingErrorLogger` (writes to `Logs/ClaimingLog_*`).
- AutoMapper (2.2.1 and 3.2.1 both present) - DTO/entity mapping. Two major versions coexist, so check which one a given project references before writing mapping profiles.
- Quartz.NET (2.3.2) with Common.Logging - background job scheduling.
- CefSharp (49.0.0, with 41.x also present) - the embedded Chromium browser used to host web content inside WinForms, with matching `cef.redist.x86`/`x64` native redistributables.
- Infragistics (16.1.2033) and Telerik UI controls - the WinForms grid, docking, list-view and toolbar controls (for example the invoice-item grid and the Infragistics `UltraWinListView` used by the scanner). These live under `SourceCode/Referenced Assemblies/`.
- Crystal Reports (4.0, referenced from `Referenced Assemblies/`) - all printed claim forms (DB4, DVA D-series) and reports, via `Abaki.CrystalReport`.
- Newtonsoft.Json (several versions, 6.0.4 through 11.0.1) - JSON serialisation, notably for the Medicare Online claim payloads.
- Microsoft.Data.SqlClient, Microsoft.Identity.Client and the IdentityModel / JWT stack (5.6.0) plus jose-jwt - used by the PRODA authentication path for online claiming.
- FlaUI (3.2.0) and Interop.UIAutomationClient - UI automation, used by the newer UI test harness (`Test.ClinNet.UiAutomation`).
- LargeAddressAware (1.0.3) - lets the 32-bit `Pracnet.exe` address more than 2 GB, relevant because the primary target is x86.

Because several packages ship in multiple versions side by side, always check a project's `packages.config` / `.csproj` references rather than assuming the newest version is in use.

### 1.7 The other solutions

`Pracnet.sln` is the main build, but the repository contains several sibling solutions and tools. Know they exist so you build the right one:

- `SourceCode/Abaki.AuditLogCodeGeneration.sln` - a code-generation tool that emits audit-log wiring code.
- `SourceCode/Abaki.P2kDataMigration.sln` and `SourceCode/PracnetMigration/PracnetMigration.sln` - P2K (a legacy system) data-migration tooling. `PracnetMigration.sln` pulls in `Abaki.P2kDataMigration`, `Abaki.P2kDataContext` and `Abaki.Data.DTOs`; the P2K EF model is the one referenced by the `P2KMetaData` mapping in `DatabaseConnectionHelper`.
- `SourceCode/PrimaryClinic.ProdaAuthentication/PracNetConfigurationService.sln` - the PRODA (Provider Digital Access) authentication and configuration service used by online claiming. It bundles `PrimaryClinic.ProdaAuthentication`, `ProdaAuthentication`, `SetupCmd` and `PrimaryClinicDatabaseConfiguration`. Because PRODA auth is a separate solution, the online-claiming certificate/JWT flow is built and versioned independently of the main app.

### 1.8 Build and release essentials

Day-to-day, open `SourceCode/Pracnet.sln` in Visual Studio 2022 with `Pracnet B1` as the startup project and run with F5. From the command line, restore with `nuget.exe restore` then build with MSBuild targeting `Configuration=Release`, `Platform=x86`. The full signed-and-packaged release build is `BuildScripts/PracnetBuild.bat`, which restores NuGet, builds `Pracnet.exe` and the database-update tool, copies the DB scripts, signs assemblies with the `globalhealth.pfx` code-signing certificate (the certificate file and its password are provided to the build, not stored here) and produces a self-extracting update package.

Two release rules are worth flagging now because they affect ordinary work:

- Do not bump `AssemblyVersion` / `AssemblyFileVersion` during feature or bug-fix work. Version bumps happen only at release time via the `/MakeRelease` process, for every project changed since the last release. See `.claude/rules/release-process.md` and `.claude/rules/assembly-version.md`.
- Every changed `.cs` file under `SourceCode/` needs at least one passing unit test (MSTest), built and run before pushing. See `.claude/rules/unit-tests.md` for the per-file obligation and the mapping from source area to test project.

### 1.9 A mental map for new developers

If you are trying to find where something lives, start from the domain and the concern:

- Billing, invoicing, fee calculation, rebates, claim payloads - the `Abaki.Billing.*` triplet (`.Gui B1`, `.Bll B1`, `.Dal B1`), with entities in `Abaki.Data` and printed forms in `Abaki.Billing.Report B1` via `Abaki.CrystalReport`.
- Medicare / DVA online claiming - `Abaki.Pracnet.OnlineClaiming`, `Abaki.HICOnline`, `PrimaryClinic.MedicareOnline`, driven from `BillingService_*` classes in `Abaki.Billing.Gui B1`, authenticated via the separate PRODA solution.
- Appointments - `AppointmentGUI B1`, `AppointmentDAL B1`, `AppointmentCore`, `AppointmentBook`.
- Clinical-system linking (Medinet) - the `Abaki.LinkingMedinetAdaptor*` projects and `Abaki.Sync`.
- Document scanning and storage - `ScanDoc` (assembly `Abaki.ScanDoc.dll`) and `DocumentController` in `Abaki.Core.Gui B1`.
- Cross-cutting utilities, enums, logging - the `Abaki.Common.*` family.
- Application shell, startup, single-instance, main window - `Pracnet B1` (`MainApp.cs`, `frmMain2`, `frmLogin`, `frmSplash`).

Two naming gotchas to remember: project folders inconsistently carry a ` B1` suffix (for example `Abaki.Billing.Bll B1`, `Pracnet B1`) while the assemblies and namespaces do not, and form classes follow the `frm<Name>` convention (`frmMain2`, `frmLogin`, `frmSplash`). Some method names carry historical typos that are load-bearing because they are referenced elsewhere (for example `InitilizeMediator`); do not "fix" them casually.

---

## 2. Building, running and releasing

This section covers how to get Pracnet building and running on a developer machine, how the command-line and release builds work, and the assembly-version and release-note rules that govern a cut release. Everything below is grounded in the actual repository: the solution and project files under `SourceCode/`, the release script `BuildScripts/PracnetBuild.bat`, the version rules in `.claude/rules/assembly-version.md` and `.claude/rules/release-process.md`, and the `/MakeRelease` skill.

### 2.1 What you are building

Pracnet is a .NET Framework 4.5 WinForms desktop application. The main executable project is `Pracnet B1` (folder name has a space and a `B1` suffix, which is a repository-wide naming convention). Its project file is `SourceCode/Pracnet B1/Pracnet.csproj` and it declares:

- `<OutputType>WinExe</OutputType>` (a Windows GUI executable, so no console window)
- `<AssemblyName>Pracnet</AssemblyName>` (the built binary is `Pracnet.exe`, not `Pracnet B1.exe`)
- `<TargetFrameworkVersion>v4.5</TargetFrameworkVersion>`
- `<PlatformTarget>x86</PlatformTarget>`

The whole product is x86. This is not optional. Several dependencies are native or 32-bit only (TWAIN scanning DLLs such as `TwainGui.dll` and `pthreadVC.dll`, `FreeImage.dll`, Crystal Reports runtime, the Tyro and Easyclaim adapters). Building or running as AnyCPU or x64 will fail to load these at runtime. Some support projects are AnyCPU, but the executable and the release build are pinned to x86. Always select the `x86` solution platform in Visual Studio, and always pass `/p:Platform="x86"` on the command line.

The solution `SourceCode/Pracnet.sln` contains roughly 100 projects (GUI, BLL, DAL, data, and the many `Abaki.Common.*` shared libraries). The startup project is `Pracnet B1`.

### 2.2 Day-to-day: build and run in Visual Studio

The intended developer loop is Visual Studio, not the command line:

1. Open `SourceCode/Pracnet.sln`.
2. Set the solution configuration to `Debug` and the solution platform to `x86`.
3. Confirm the startup project is `Pracnet B1` (it should be by default).
4. Press F5 to build and debug.

On first run after a fresh clone you will normally need a NuGet restore (Visual Studio does this automatically on build if package restore is enabled, or run it manually as in 2.3). The app runs under the `en-AU` culture and is a single-instance application enforced by a named mutex in `MainApp.Main()` (`SourceCode/Pracnet B1/MainApp.cs`), so if a previous debug session is still alive the new instance will hand off and exit rather than start. `BuildScripts/KillPracnet.bat` exists to force-kill a stuck instance.

Running Pracnet locally needs a reachable SQL Server database and a connection profile (`ProfileDB`, managed through `DatabaseConnectionHelper`). Getting the database provisioned is a prerequisite for the app to progress past the login form; the build itself does not require a database, but F5-to-running does.

Visual Studio version reality: the repository is developed against Visual Studio 2022 (Professional on the current dev machines). The `.claude` rules and test-run commands reference the VS2022 Professional MSBuild and `vstest.console.exe` paths. Be aware of a mismatch: the release script `BuildScripts/PracnetBuild.bat` still hard-codes the Visual Studio 2017 Professional MSBuild path (`...Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\msbuild.exe`). That path will not exist on a VS2022-only machine, so the batch file as-committed will fail the compile step unless VS2017 build tools are installed or the path is edited. For day-to-day work, use the VS2022 IDE or a VS2022 Developer Command Prompt and ignore the hard-coded path.

### 2.3 Command-line build

For a scripted or CI-style build of just the executable, restore packages first then build the `Pracnet B1` project (from the repository root):

```
nuget.exe restore "SourceCode/Pracnet.sln"
msbuild /p:Configuration=Release /p:Platform="x86" /t:REBUILD "SourceCode/Pracnet B1/Pracnet.csproj"
```

Use an MSBuild that ships with a Visual Studio that has the .NET Framework 4.5 targeting pack and the required UI and Crystal Reports assemblies (a VS2022 Developer Command Prompt works). `nuget.exe` is vendored in `BuildScripts/nuget.exe` if it is not otherwise on the path.

### 2.4 Running the tests

Tests are MSTest v1 (`Microsoft.VisualStudio.QualityTools.UnitTestFramework`). The billing test project is `SourceCode/Test.Abaki.Billing.Dal/Test.Abaki.Billing.csproj`, producing `Test.Abaki.Billing.Dal.dll`. Build and run it from a Visual Studio Developer Command Prompt (which puts `MSBuild.exe` and `vstest.console.exe` on the path, so you do not hardcode an install location):

```
MSBuild.exe "SourceCode/Test.Abaki.Billing.Dal/Test.Abaki.Billing.csproj" -p:Configuration=Debug -t:Build -v:quiet -noLogo

vstest.console.exe "SourceCode/Test.Abaki.Billing.Dal/bin/Debug/Test.Abaki.Billing.Dal.dll" "/Tests:<TestClassName>"
```

(The exact `MSBuild.exe` / `vstest.console.exe` location depends on your Visual Studio edition and version, so use the Developer Command Prompt rather than a hardcoded path.)

Per the unit-test rule, when you touch a `.cs` file under `SourceCode/` you must add or update a test and confirm it passes before pushing. Do not push a branch with red tests, even on a non-PR branch. See `.claude/rules/unit-tests.md` for the source-area-to-test-project mapping and the exemptions (UI/WinForms, pure CRUD, generated files, version bumps).

### 2.5 The full release build: `BuildScripts/PracnetBuild.bat`

The release build is a Windows batch script run from the `BuildScripts` directory (paths inside it are relative to that folder). It produces a self-extracting updater executable. The high-level flow, in order:

1. Wipe the previous `Output/` folder (`rmdir /S /Q ..\Output`), then set `packageOutputFolder=PracnetUpdate`.
2. NuGet restore of the whole solution (`nuget.exe restore "..\SourceCode\Pracnet.sln"`).
3. `DeleteLicenseLicxCmd.exe` sweeps `..\SourceCode` to strip `.licx` licence files before the compile (Infragistics and other licensed controls). This exists so the packaged build does not embed design-time licence artefacts.
4. Rebuild the application: MSBuild `Release` / `x86` `REBUILD` of `Pracnet.csproj` with `OutputPath` redirected to `..\..\Output\PracnetUpdate`. This is the step with the hard-coded VS2017 MSBuild path noted in 2.2.
5. Rebuild the database update tool: MSBuild `Release` / `x86` `REBUILD` of `SourceCode/PracnetDatabaseUpdateTool/Abaki.Pracnet.DatabaseUpdateTool.csproj` into `..\..\Output\PracnetUpdate\DatabaseUpdateModule`. This is a second, separate executable (`Abaki.Pracnet.DatabaseUpdateTool.exe`) that applies SQL migrations at install time.
6. Copy database scripts: creates `...\DatabaseUpdateModule\DbScriptsPackage` and `xcopy`s every `*.sql` under `Database/PracnetDatabaseFrom0.1.6/` into it, honouring the exclusion list `BuildScripts/ExcludeSqlFolderName.txt` (which excludes `FullScriptsFromPreviousVersion`, the Medinet/Pracnet link-file processor script folders, and the `1stAvailable`/`HealthEngine` integration folders). These are the ordered revision scripts the DB update tool runs.
7. Delete `.pdb` symbol files from both output folders so debug symbols do not ship.
8. Stamp a build-date marker file `PracnetUpdatedDate.txt` in the package folder with a fixed literal `1.0.0.0` version token plus the current date and time. Note this token is a script literal and is not the product version; the real product version is the assembly version stamped into `Pracnet.exe` (see 2.7).
9. Copy the helper executables `UpdatePackageStoringTool.exe` and `SetupCmd.exe` into the package folder. `SetupCmd.exe` is the entry point the self-extractor runs (see step 12); it orchestrates copying the application and invoking the DB update tool.
10. Code-sign every shipped DLL and EXE. The script calls `signtool.exe sign` with the certificate `BuildScripts/globalhealth.pfx` and a timestamp URL, once per assembly, for both the application package folder and the `DatabaseUpdateModule` folder (this is why the script is very long: it is an explicit per-file signing list, roughly 80 assemblies for the app plus the DB tool set). The PFX password is passed inline as a `signtool` argument in the script; treat both the certificate file and that password as secrets (see 2.8). Signing establishes the Global Health Authenticode signature users see when the updater runs.
11. Zip the package: `7zip\7z a ..\Output\PracnetUpdate.7z ..\Output\PracnetUpdate\*`.
12. Build the self-extracting updater: `copy /b` concatenates the 7-Zip SFX module, the SFX config text `BuildScripts/7zip/PracnetUpdate.txt`, and the `.7z` archive into `..\Output\pracnetupdate.exe`. The config (`PracnetUpdate.txt`) tells the SFX to extract to `%Temp%\PracnetUpdate`, title itself "PrimaryClinic Practice", then run `SetupCmd.exe -i:"<extracted module path>"` and delete the temp folder afterwards. That is how a single downloaded `pracnetupdate.exe` unpacks, applies the DB scripts and copies the new binaries.
13. Build and sign a second self-extractor for the External Setting Tool: zips `Output\PracnetUpdate\ExternalSettingTool\*` into `ExternalSettingTool.7z`, wraps it with `BuildScripts/7zip/ExternalSettingTool.txt` into `Output\ExternalSettingTool.exe` (extract to temp, run `Abaki.Pracnet.ExternalSettingTool.exe`, clean up), then signs it. The source project is `SourceCode/Abaki.Pracnet.ExternalSettingTool`.
14. The trailing `PBuilder` / `GeneratePBDScript` lines are all `REM`-commented out. Paquet Builder is legacy packaging that is no longer part of the active flow; the live output is the 7-Zip self-extractor from step 12.

On any failure the script jumps to `:error`, which pauses, removes the two output folders and exits with the error code. Success prints "Package was built successfully."

Gotchas in the release build:

- VS2017 path (see 2.2) is the first thing that will break on a modern machine.
- SFX module name mismatch: the script's `copy /b` lines reference `.\7zip\7zsd_All.sfx`, but the file actually present in `BuildScripts/7zip/` is `PracnetUpdate.sfx`. If the SFX build step fails with a missing-file error, this naming discrepancy is the likely cause; reconcile the script against what is on disk.
- The signing list is maintained by hand. If you add a new shipped assembly to the product, you must add a matching `signtool` line (in both the app and, if applicable, the DB module block) or the new DLL ships unsigned.

### 2.6 When to bump versions: never per-feature, only at release

This is the single most important release rule and it is easy to get wrong. Per `.claude/rules/release-process.md`:

| Action | Bump versions? |
|---|---|
| Implementing a feature | No |
| Fixing a bug | No |
| Refactoring or test-only changes | No |
| Running `/MakeRelease` | Yes, for every project changed since the last release |

Do not touch `AssemblyVersion` / `AssemblyFileVersion` during feature or bug-fix work. Per-feature bumps create noisy diffs, conflict on every merge and decouple the version from the release that actually shipped. The `/MakeRelease` flow owns the bump.

Release scope covers all merged branches, not just your current branch. A release includes every branch merged to `Development` since the previous release. When determining which projects to bump, diff against the last release reference, not against `Development` HEAD or your feature branch:

```
git log <last-release-ref>..HEAD --first-parent
```

This is what catches, for example, a Letter Writer fix that landed on a separate bugfix branch already merged to `Development`. Bump every project touched across all those merged branches.

### 2.7 How to bump: which digit, which projects

Mechanics are in `.claude/rules/assembly-version.md`. Each project keeps its version in `SourceCode/<ProjectName>/Properties/AssemblyInfo.cs`:

```csharp
[assembly: AssemblyVersion("3.0.0")]
[assembly: AssemblyFileVersion("3.0.0")]
```

Rules:

1. Bump only projects whose source files actually changed since the last release (plus the always-bumped projects below).
2. Increment the patch (4th) number for bug fixes, or the minor (3rd) number for features. Feature: `1.0.0.0` becomes `1.0.1.0`. Bug fix: `1.0.0.0` becomes `1.0.0.1`.
3. Set both `AssemblyVersion` and `AssemblyFileVersion` to the same value.
4. Reference the PRACNET ticket in the commit message for traceability.

Do not bump test projects, and do not treat auto-generated `.Designer.cs` changes as a source change that warrants a bump.

Always-bumped projects (bump these on every release even if their sources did not change):

- `SourceCode/Pracnet B1/Properties/AssemblyInfo.cs`. This stamps `Pracnet.exe`; it is what support and users quote as the build version.
- `SourceCode/Abaki.SecurityGUI/Properties/AssemblyInfo.cs`. The login/About dialog's version line reads its own DLL, not `Pracnet.exe`. `frmLogin.GetPracnetVersion()` (`SourceCode/Abaki.SecurityGUI/frmLogin.cs:720`) calls `Assembly.GetExecutingAssembly()` then `FileVersionInfo.GetVersionInfo(...).ProductVersion` from inside `Abaki.SecurityGUI`, so if you bump `Pracnet B1` alone the About dialog still shows the stale `Abaki.SecurityGUI` version. Keep these two in lockstep.

As of this handover both `Pracnet B1` and `Abaki.SecurityGUI` are at `3.0.0`, matching the top entry in the release notes. The `AssemblyVersion` here uses three parts (`3.0.0`) rather than four; keep the existing arity when you edit these files rather than introducing a fourth segment.

Editing `AssemblyInfo.cs` on Windows: line-precise edits with `sed -i` can strip CRLF line endings and churn the whole file. Use the Edit tool (or a binary-safe edit) for these files, per the recorded memory note on sed CRLF churn.

### 2.8 The `/MakeRelease` flow

`/MakeRelease` is the release skill that prepares a release by setting assembly versions for all modified projects. Conceptually it does the version work described in 2.6 and 2.7 for you: determine the diff since the last release, identify every project whose sources changed, bump those plus the always-bumped pair, and target the new release version. It owns the bump so that individual features and fixes stay version-clean.

After `/MakeRelease` sets versions, the release notes are updated (2.9) and the packaged updater is produced by `BuildScripts/PracnetBuild.bat` (2.5). Version-bumping and packaging are separate steps: `/MakeRelease` handles versions and notes; the batch script handles compile, sign and package.

### 2.9 Release notes: `ReleaseNote.rtf`

Location: `SourceCode/Pracnet B1/Resources/ReleaseNote.rtf`. This RTF is shipped in the executable and shown to users, so its formatting must stay consistent. Newest entries go at the top. The current top entry is version 3.0.0 (Medicare web-services update, Submission Authorised tick, failed-claim visibility on the claim tabs, plus bug fixes).

Format requirements (from `.claude/rules/release-process.md`):

- Font is Calibri, declared in the RTF font table: `{\fonttbl{\f0\fswiss Calibri;}}`.
- Bold title per release: `{\b\fs28 Release Note for version X.Y.Z}\par`.
- Bold section headers: `\par{\b Enhancement:}\par` and `\par{\b Bug fixes:}\par`.
- Bullet items are plain text, each terminated by `\par`.
- A separator line between releases: `{\fs20 ____________________________________________________________________________________}\par`.

Write release notes in customer-facing language, describing behaviour and outcomes rather than internal ticket mechanics (the 3.0.0 entry is a good model: "Failed claims now show on the Bulk Bill, DVA, IMC and OVS claim list tabs", not "PRACNET-3014 TransmitFailed status writes"). Older entries in the file use inconsistent styles (some without bold headers, some with dashed separators); when adding a new block, follow the clean modern format above rather than copying an old one. The rule suggests reading existing content with the `striprtf` Python library and re-emitting cleanly to keep formatting consistent.

### 2.10 Secrets and shared-content hygiene

- The signing certificate `BuildScripts/globalhealth.pfx` and its password (passed inline to `signtool` in `PracnetBuild.bat`) are secrets. Do not reproduce the password in tickets, PRs, commit messages or docs. If signing needs to be reconfigured, describe the certificate's role and where it lives, not its credentials.
- The document-storage ZIP passwords referenced elsewhere in the codebase are likewise secrets; never echo their values.
- When writing anything for a shared platform (Jira, Bitbucket, Confluence, Slack), strip machine-local absolute paths and use repo-relative paths (`SourceCode/...`, `BuildScripts/...`) and file:line citations instead, per `.claude/rules/no-local-paths.md`.

---

## 3. Application startup and cross-module messaging

This section covers how Pracnet boots from a cold process to a running main window, and how the many WinForms modules (billing, appointments, waiting room, claiming, immunisation, clinical data) talk to each other at runtime without holding direct references. There are two distinct concerns here that are wired together in the same file:

1. Process bootstrap and single-instance enforcement in `SourceCode/Pracnet B1/MainApp.cs`.
2. The in-process publish and subscribe bus, `CoreMediator`, that every module uses to notify its colleagues of events such as a new invoice, a receipt, or a patient arriving.

Both live largely in the `Pracnet` executable project (`Pracnet B1`), with the mediator itself defined lower down in `Abaki.Common` and `Abaki.Core.Dal`.

### 3.1 Entry point and the single-instance guard

The managed entry point is `MainApp.Main(string[] args)` at `SourceCode/Pracnet B1/MainApp.cs:173`. It is marked `[STAThread]` because the whole app is WinForms.

The very first thing `Main` does is install the global exception handlers so that anything unhandled ends up in the log rather than crashing silently:

- `AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException` (non-UI-thread faults).
- `Application.ThreadException += Application_ThreadException` (UI-thread faults).
- `Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)`.
- `Application.ApplicationExit += OnProcessExit`.

Both exception handlers unwrap a generic wrapper message (`MessageStrings.MessageBoxGenericExceptionMsg`) to surface the inner exception, show a message box, and log via `ErrorLogger.WriteLog`. `OnProcessExit` (`MainApp.cs:293`) runs `ConstantValues.SqlQuery_UpdateUserOnlineState` against `NewCommonContext` to mark the current user offline and flips `OnlineCheck._isLoggedIn` to false, so the "who is online" feature is kept honest on a clean shutdown.

Single-instance behaviour. By default Pracnet allows only one running instance per install location. There is an escape hatch: if the process is launched with a single argument of `1` or `true`, `isAllowRunWithMultipleInstance` is set and the guard is skipped entirely (`MainApp.cs:180`). Otherwise:

- A per-install identity is derived from the executable path: `AppDomain.CurrentDomain.BaseDirectory` combined with the executing assembly name, lower-cased, then hashed by `GeneralUtils.GenerateGuid(...)` into `shareMemoryName`. This means two Pracnet installs in different folders are treated as different applications and can both run, which is deliberate.
- A named `Mutex` is created as `"Local\\Mutex_" + shareMemoryName`. The `Local\` prefix scopes the mutex to the current Terminal Services or RDP session rather than the global namespace, so separate Windows sessions on the same server each get their own single-instance slot. This matters because Pracnet is commonly run on shared or terminal-server hosts.
- If the mutex is newly created (`newMutexCreated == true`) this is the first instance. It creates a memory-mapped file via `SharedMemory.CreateMMF(shareMemoryName, ReadWrite, IntPtr.Size)` (see `SourceCode/Abaki.Common/SharedMemory.cs`) and calls `RunApp()`.
- If the mutex already exists a previous instance is running. Rather than start a second copy, the second launch reads the first instance's main-window handle back out of the shared memory (`SharedMemory.ReadHandle`), restores and maximises that window (`ShowWindow` with `SW_MAXIMIZE`), and brings it to the foreground via `GeneralUtils.SetForegroundWindow`, then returns. The user experiences "clicking the icon again just focuses the already-open Pracnet."

The creation and reads of the shared memory block are wrapped in `lock (typeof(frmMain2))`. This is a coarse lock keyed on a `Type` object; it is a known anti-pattern but is intentional here as a cheap cross-thread guard around the small shared-memory window. Do not "clean this up" to a private lock object without understanding that the same type is locked on in `frmMain2` too.

How the window handle gets into shared memory. The first instance does not write its handle in `MainApp`; the main form writes its own handle when its native window is created. `frmMain2.OnHandleCreated` (`SourceCode/Pracnet B1/frmMain2.cs:85`) calls `SharedMemory.WriteHandle(MainApp.sharedMemory, this.Handle)` under the same `lock (typeof(frmMain2))`. So the sequence is: first instance creates the MMF sized to `IntPtr.Size`, later builds `frmMain2`, and when that form's handle is created the handle is stamped into the MMF for any future second instance to find.

Note there are two different payloads living in the same `sharedMemory` object at different offsets. `WriteHandle`/`ReadHandle` use offset 0 for the window handle (an `IntPtr`). `SharedMemory.Write`/`Read` with `IntPtr.Size` as the offset store a string (the logged-in user id) used by the Medinet single-sign-on handshake described below. Keep the offset conventions straight when editing `SharedMemory.cs`.

### 3.2 `RunApp`: TLS, culture, splash, login

`RunApp()` (`MainApp.cs:391`) is the real startup body. Order matters here; the sequence is:

1. TLS enforcement. `ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12`. Services Australia decommissions TLS 1.0 and 1.1 in 2026 and requires TLS 1.2, so the app pins TLS 1.2 up front. TLS 1.3 is intentionally not enabled because the app targets .NET Framework 4.5, which lacks `SecurityProtocolType.Tls13`. A `ServerCertificateValidationCallback` is chained on with `+=` (not assigned with `=`) purely to log each handshake destination and the negotiated protocol; it still returns standard validation (`errors == SslPolicyErrors.None`) so it never weakens certificate checking. Because it uses `+=`, downstream handlers such as `ProdaHttpClient.RequestToServer` can add their own logic without clobbering this one. If you touch this callback, preserve the `+=` and never make it return `true` unconditionally.
2. Splash screen. `frmSplash.ShowSplashScreen()` (`SourceCode/Pracnet B1/frmSplash.cs:30`) starts the splash on its own background STA thread running its own `Application.Run`, so the splash keeps painting and stays responsive while the main thread does slow initialisation. The splash is closed later from various points via the static `frmSplash.CloseForm()`, which marshals back onto the splash thread with `Invoke`.
3. Culture. `Thread.CurrentThread.CurrentCulture = new CultureInfo("en-AU")`. This is the single source of truth for Australian date, currency, and number formatting across billing and reports. Do not remove or move this before code that formats money or dates.
4. Local settings. `LocalSetting.IsLazyLoading = true` then reads `LocalSetting.Instance.PracnetLocalSetting` to recover the last-used login name and profile name. Empty login name defaults to `"Admin"`.
5. Login form. A `frmLogin` is constructed and given three things: a `CancelCallback` (which calls `Environment.Exit(0)`), the remembered login and profile via `InitilizeData`, and crucially `f.LoadingHandler = new frmLogin.MainFormLoadingHandler(StartLoadingMainFormCallback)`. That handler delegate has the signature `void (ProfileDB profileDb, ProgressBar progressBar, Form owner)` (`SourceCode/Abaki.SecurityGUI/frmLogin.cs:51`). When the login form authenticates successfully it invokes this callback (`frmLogin.Process`), which is how the executable injects "now build the heavy main form" behaviour into the security DLL without the security DLL referencing `Pracnet B1`. This is an inversion-of-control seam: `frmLogin` owns the progress bar and drives it, `MainApp` owns what actually gets loaded.
6. Profile and auto-update. The selected `ProfileDB` sets `DatabaseConnectionHelper.Instance.ProviderConnectionString` for the Demographic catalog. If a debugger is not attached, `AutoDetectUpdate` compares the local build timestamp file against the copy on the server share and, if they differ, offers to run `PracnetUpdate.exe` from the server. This is skipped under the debugger so developers are never nagged.
7. Authenticate and run. `if (AuthenticateUser(...)) { ...; Application.Run(MainForm); }`. The main message loop only starts once a user is authenticated and `MainForm` (a `frmMain2`) has been built. The profile name is appended to the window title.

### 3.3 Building the main form: `InitializeMainFormData`

`StartLoadingMainFormCallback` (`MainApp.cs:492`) is the target of the login form's `LoadingHandler`; it stashes the chosen profile in `_currentProfile` and calls `InitializeMainFormData(progressBar, owner)`. `InitializeMainFormData` (`MainApp.cs:515`) is where the expensive, order-sensitive initialisation happens, driving the login form's progress bar via `AddMoreProcessedPercentage`:

- Empties the local temp document folder asynchronously (`GeneralUtils.EmptyLocalDocumentFolder` via `BeginInvoke`).
- Database version gate. Compares `DatabaseGlobalSetting.Instance.DatabaseVersion` with `DatabaseGlobalSetting.LegalDatabaseVersion`. On mismatch it shows a message and calls `Application.Exit()`. This is the guard that stops a client binary running against an unmigrated or over-migrated database; if you add a database migration you must also bump the legal version, or every client will refuse to start.
- Document storage path. `InitDocStoragePath` (delegates to `FrmDocStorageSetting.InitDocStoragePath`) ensures a shared storage location is configured; if the user declines, the app exits.
- Logger share paths. Sets `ErrorLoggerToServer`, `MedicareOnlineLoggerToServer`, and `ClaimingLoggerToServer` share folders to `RecallGlobalSetting.Instance.SharedStoragePath`, and initialises `Abaki.DbLogger.Logger` with the Demographic connection string.
- Crystal Reports. `ReportUtility.SetConnectionInfo(...)` seeds the report engine's SQL login from the profile, and `InitilizeCrystalReport()` (`MainApp.cs:876`) pre-loads a throwaway hidden `frmReport` positioned off-screen at (10000, 10000) so the first real report the user opens is fast. This warm-up is a deliberate latency hack.
- Cross-DLL glue. `InitSmsSettings()` (`MainApp.cs:580`) assigns static delegate fields on `frmEmail` and `SMSClass` to methods on `Abaki.Core.Dal.CoreCommon.Instance`. This is the same IoC pattern as the login handler: lower-level UI controls call these static hooks to persist email and SMS without taking a project reference on the DAL. `CommonEvents.SearchRefDoctor = SearchRefDoctor` is another instance of the same pattern (fax form calls back to search for a referring doctor).
- Main form construction. `MainForm = new frmMain2()`, maximised. A cached `frmPracnetMain` (the home dashboard) is pre-built into `frmMain2.CacheFrmPracnetMain`.
- Mediator registration. `InitilizeMediator()` (see below).

Note the spelling: the method is `InitilizeMediator` (with the transposed letters). It is called from two places, once from `InitializeMainFormData` at startup and it is the canonical wiring for the executable's own message subscriptions.

### 3.4 Authentication paths and Medinet single sign-on

`AuthenticateUser` (`MainApp.cs:705`) tries two paths in order:

1. Medinet SSO. It first writes an empty string into the shared memory user-id slot, then calls `MedinetAuthenticate` (`MainApp.cs:598`). Medinet is the clinical system Pracnet integrates with; when both run together Medinet drops the logged-in user id into a shared-memory key (`PracnetLocalSetting.GetClinicalSoftwareShareKey()`). If present, Pracnet reads that user id (or falls back to resolving the current Active Directory domain user via `DomainUserManager.GetADUserByName`), looks up the matching Pracnet user with `DapperContext.Instance.GetUser`, validates the linked employee is not deleted or archived, sets up the `CustomPrincipal`/`CustomIdentity`, populates `GlobalInfo.UserInfo`, builds the main form (`InitializeMainFormData(null, f)`), closes the splash, and returns true. This is the "seamless" launch where Pracnet inherits the Medinet session and never shows its own login dialog.
2. Interactive login. If Medinet SSO does not apply, the code falls through, does some Win32 foreground-focus stealing so the login window reliably takes keyboard focus over whatever launched it, and shows the `frmLogin` dialog modally. `DialogResult.Cancel` aborts startup. On success it reads `GlobalInfo.UserInfo` from the principal, persists the login and profile names back to `LocalSetting` if they changed, and writes the user id into shared memory.

Either successful path ends with `MainForm.InitPPSubscriber()` and a `SharedMemory.Write` of the user id. `InitPPSubscriber` (`frmMain2.cs:541`) wires up the Patient Portal notification subscriber (`PPSubscriber`) only when `RecallGlobalSetting.Instance.UsePPIntegration` is on and the user holds the `PatientPortal.Approve` permission.

Security identity invariant. After authentication, `GlobalInfo.UserInfo` (a `CustomIdentity`) is the app-wide current user, and `System.Threading.Thread.CurrentPrincipal` is a `CustomPrincipal`. Permission checks everywhere go through `PracnetAuthorization.HasPermission(ePracnetFeatures.X, ePracnetPermission.Y)`. Code that runs before this point must not assume a user exists.

### 3.5 `frmMain2` as the main form and MDI-style document host

`frmMain2` (`SourceCode/Pracnet B1/frmMain2.cs`) is the single top-level window and the only form passed to `Application.Run`. It implements `IToolStripHost, IMainForm, ICanExportToExcel, IUpdateControlPermisionsOnForm`. Modules are not separate windows; they are docked documents inside `frmMain2`'s `DockPanel dockManager` (WeifenLuo docking). `frmMain2.GetDocument<T>()` is the standard way to find an already-open module (for example `GetDocument<frmAppointment>()` or `GetDocument<frmImmunSearch>()`), which is how message handlers refresh a module only if it is currently open.

In its constructor `frmMain2` registers `SelectionInfoChange` directly and then calls `RegisterCoreMessage()` (`frmMain2.cs:2669`), wires `dockManager.ActiveDocumentChanged`, connects the real-time `PCPNotificationService` (online-booking and FHIR notifications), and configures the idle auto-lock timer (`ConfigApplicationIdle`, driven by `RecallGlobalSetting.AutoLockScreenAfter` or the per-machine `LocalSetting` value).

### 3.6 The `CoreMediator` bus: how modules communicate

Pracnet modules are decoupled by an in-process mediator (a typed message bus), not by direct references. The base implementation is `MediatorBase<TMessage>` in `SourceCode/Abaki.Common/MediatorBase.cs`. It is a dictionary from a message token to a `List<Action<object>>` of callbacks with three operations:

- `Register(token, callback)` adds a handler, de-duplicating by comparing the delegate's target-object hash and method hash (so registering the same instance method twice is a no-op).
- `Unregister(token, callback)` removes by the same identity comparison.
- `NotifyColleagues(token, args)` synchronously invokes every registered callback for that token in registration order, passing a single `object` payload.

`CoreMediator` (`SourceCode/Abaki.Core.Dal/CoreMessage/CoreMediator.cs`) is a singleton (`CoreMediator.Instance`) deriving from `MediatorBase<eCoreMessage>`. The message vocabulary is the enum `eCoreMessage` in `SourceCode/Abaki.Common.Enums/eCoreMessage.cs` (values include `NewInvoice`, `EditInvoice`, `NewReceipt`, `EditReceipt`, `NewImm`, `NewDeposit`, `PatientArriving`, `NewVisit`, `PatientLeaving`, `PatientLeft`, `ShowHistory`, `OpenClinicalData`, `InvoiceAdded`, `InvoiceChanged`, `InvoiceRefreshed`, the per-claim-type `*ClaimRefreshed` messages, `SelectionInfoChange`, `TerminateSession`, and more).

There is a duplicate, fully commented-out `CoreMediator` and `eCoreMessage` under `SourceCode/Abaki.Core.Gui B1/CoreMessage/`. These are dead files; the live types are the `Abaki.Core.Dal.CoreMessage` ones. Do not resurrect the GUI-project copies.

A parallel `GroupMediator` also lives in `CoreMediator.cs`. It is keyed on a `string` group name rather than the enum and is used for instance-scoped notifications where the payload must reach only handlers for one particular entity. The one live use is referral refresh, where the group key is `eCoreMessage.ReferralRefreshed.ToString() + patientGuid`, so only the grid bound to that patient reacts (see `ReferralDetailDtoGridView.cs` and the `PatientDemographicCtrl*` publishers).

Threading and lifetime gotchas.

- `NotifyColleagues` is synchronous and runs entirely on the caller's thread. If a background thread publishes a message, the handlers execute on that background thread; any handler that touches WinForms controls must marshal with `BeginInvoke` itself. The mediator gives you no thread affinity.
- The mediator holds strong references to handler delegates, which hold their target objects. A control or form that registers must unregister on close or it leaks (and worse, a disposed form's handler can still be invoked). The appointment book does this correctly with paired `RegisterMessageHandlers`/`UnRegisterMessageHandlers` (`SourceCode/AppointmentGUI B1/ApptBookCtrl_Comm.cs`). Several other subscribers register without a matching unregister; when adding new subscribers, always pair them.
- Handlers run in registration order and one throwing handler will abort the rest of that notification, because `NotifyColleagues` has no per-callback try/catch. Keep handlers defensive.
- `frmMain2` and `MainApp` both register lambdas that forward into a `ProcessCoreMessage` method. Because those lambdas are distinct delegate instances, the de-dup logic will not collapse them; the two `ProcessCoreMessage` methods handle disjoint message sets, so there is no double handling in practice. If you add a message, register it in exactly one of the two.

### 3.7 Who publishes and who subscribes (the actual topology)

`MainApp.InitilizeMediator` (`MainApp.cs:858`) registers the executable as the handler for the "create or open a document" family and routes them through `MainApp.ProcessCoreMessage` (`MainApp.cs:914`):

- `NewInvoice` and `NewImm`, payload `WaitingRoomBusinessSearch`, open a new invoice via `BillingService.CreateNewInvoice(...)` or a new immunisation claim via `ACIRServices.CreateNewACIR(...)`.
- `EditInvoice`, `NewReceipt`, `EditReceipt`, `ShowHistory`, payload a `Guid?`, route to the corresponding `BillingService` method.
- `PatientArriving`, payload an `Appointment`, calls `WaitingRoomServices.NewVisit(...)`. If `RecallGlobalSetting.Instance.EnableRadiologyWorkflow` is on it passes a continuation (`ShowInvoiceInRadiologyWorkflow`) that in turn republishes `PatientArrivingInRadiologyWf` with a `WaitingRoomBusinessSearch`. This is an example of one mediator message triggering another.
- `OpenClinicalData` calls `MainForm.ShowClinicalData()`.

`frmMain2.RegisterCoreMessage` (`frmMain2.cs:2669`) registers the form as handler for the "act inside the main window" family, routed through `frmMain2.ProcessCoreMessage` (`frmMain2.cs:2684`): `PatientLeaving` (creates the leaving invoice, with three payload shapes handled: `AppointmentAndWaitingRoomLeaveParam`, `WaitingRoomBusinessSearch`, or `Appointment`), the radiology-workflow messages, `JumpToAppointment`, `WaitRoomQuickInvoice`, `Immunisations` (refreshes an open `frmImmunSearch`), `ShowHistory`, and `TerminateSession` (remote session kill: notifies, then closes all module windows and logs off on a background thread).

The main publishers of these messages are:

- Appointments: `SourceCode/AppointmentGUI B1/ApptBookCtrl_CalendarViewEventHandlers.cs` publishes `PatientArriving` when a patient is marked arrived. The appointment book is also a subscriber to `NewVisit`, `PatientLeft`, `AppointmentAdded/Changed/Refresh`, and `PatientDetailChanged` (it repaints or reloads the affected appointment).
- Waiting room: `SourceCode/Abaki.Core.Gui B1/WaitingRoomServices.cs` publishes `NewVisit` and `PatientLeft`; `frmWaitingRoomCore.cs` publishes `NewReceipt` and `OpenClinicalData` and subscribes to the visit lifecycle messages to reload its list.
- Billing: `InvoicePresenter.cs`, `ReceiptPresenter.cs`, `BillingService.cs`, and the account-history controls publish `InvoiceAdded`, `InvoiceChanged`, `InvoiceRefreshed`, `ReceiptAdded`, `ReceiptAdjusted`, and `RefreshReceiptHistory`. Billing grids and the account-history presenter subscribe to keep views live.
- Online claiming (`Abaki.Billing.Gui B1/HICOnline/*`): after a claim is created or a response is processed, forms publish the per-type `BBClaimRefreshed`, `DvaClaimRefreshed`, `ImcClaimRefreshed`, `OvsClaimRefreshed`, `PPClaimRefreshed`, `eTACRefreshed`, `eWCRefreshed`, `eOthersRefreshed`, and the corresponding claim-management windows subscribe with a `DoRefresh`.
- Immunisation (ACIR): `frmAcirClaim.cs` and friends publish `Immunisations` and `SelectionInfoChange`.

The net effect is a loosely coupled event architecture. A billing action does not call the appointment book directly; it publishes `InvoiceChanged`, and any open appointment or waiting-room view that cares has registered a handler. This is why "open the module first, then it stays in sync" is the observed behaviour: handlers only exist while the module is open and registered.

### 3.8 Practical guidance for a new developer

- To add a new cross-module event, add a value to `eCoreMessage`, publish with `CoreMediator.Instance.NotifyColleagues(eCoreMessage.X, payload)`, and register handlers in the consuming modules. Decide the payload type up front and treat it as the contract; handlers must down-cast the `object` defensively (`obj as T` with a null check), as the existing handlers do.
- Register in exactly one place per module, and unregister on close. Prefer paired `Register`/`Unregister` methods like the appointment book.
- Never assume the notification is on the UI thread. If your handler updates controls and the publisher might be a background thread, marshal with `BeginInvoke`.
- Startup ordering is load-bearing. Culture is set before any money or date formatting, the database version gate runs before the main form is built, and the Crystal Reports warm-up depends on report connection info already being set. If you insert new startup work, place it relative to these in `RunApp` and `InitializeMainFormData` rather than at the top.
- The single-instance identity is path-based. Testing two builds side by side in different folders will let both run; that is expected, not a bug.
- The `lock (typeof(frmMain2))` around shared memory in both `MainApp` and `frmMain2` must stay consistent between the two files; they are guarding the same MMF window.

---

## 4. User management and security

This section covers how Pracnet authenticates users at startup, how the current user is represented for the life of the session, how roles and feature permissions are modelled and edited, how those permissions gate features across the application, and where the underlying data lives. Everything below is grounded in the actual code under `SourceCode/Abaki.SecurityGUI`, `SourceCode/Abaki.Common.Security`, `SourceCode/Abaki.Common.Enums` and `SourceCode/Abaki.Data/Domains/Entities_Security`.

The subsystem originally derives from Martin Cook's "codegator" security sample (the copyright banner survives in `SecurityManager.cs`, `UserManager.cs`, `CustomIdentity.cs` and `CustomPrincipal.cs`), but it has been heavily reworked into an Entity Framework database-first model on SQL Server.

### 4.1 High-level shape

There are three cooperating layers:

- Presentation: the login form and the admin management forms live in `SourceCode/Abaki.SecurityGUI` (assembly `Abaki.Security.GUI`, namespaces `Abaki.Security.GUI` and `AbakiSecurityGUI`).
- Security service and principal: static managers and the custom `IIdentity` / `IPrincipal` implementations live in `SourceCode/Abaki.Common.Security`.
- Domain and data: the EF entities, the `ISecurityData`-family interfaces and their implementations live in `SourceCode/Abaki.Data/Domains/Entities_Security`. The permission flag enums live in `SourceCode/Abaki.Common.Enums/SecurityPermissionEnums.cs`.

The managers (`SecurityManager`, `UserManager`, `RoleManager`, `SecurityFeatureManager`, etc.) are thin static facades. Each has a static constructor that pulls a concrete data object from `DataManager` (for example `UserManager` static ctor does `c_userData = DataManager.UserData;`). This is a hand-rolled service-locator; there is no dependency injection.

### 4.2 Login and authentication

Entry point: `SourceCode/Abaki.SecurityGUI/frmLogin.cs`. The main executable (`Pracnet B1/MainApp.cs`) constructs `frmLogin`, wires callbacks and drives it through `AuthenticateUser` (`MainApp.cs:705`).

There are two authentication modes, selected by the `cbAuthentications` combo on the login form:

1. Direct login (username plus password). This is index 1 in the combo and the default when the machine is not domain-joined.
2. Domain / single sign-on. This is index 0 and only becomes available when the workstation is joined to a Windows domain and a matching Pracnet user has a mapped domain GUID.

#### Direct login flow

`frmLogin.ValidateUser()` (`frmLogin.cs:310`) handles the password branch:

1. Reads the selected database profile (see 4.7) and the typed username / password.
2. Calls `SecurityManager.Authenticate(userName, password)` (`frmLogin.cs:358`).
3. `SecurityManager.Authenticate` (`SourceCode/Abaki.Common.Security/SecurityManager.cs:71`) hashes the plaintext password with `HashData` (SHA1 over the UTF-16 / Unicode bytes of the string, hex-formatted) and delegates to `c_securityData.Authenticate`.
4. `SecurityData.Authenticate` (`SourceCode/Abaki.Data/Domains/Entities_Security/SecurityData.cs:13`) looks up a `SecurityUser` where `UserName == userName && Password == hashedPassword && DeletedDate IS NULL`. On a match it records the login in session storage (`ApplicationSessionStorage.UserLoginId` / `UserLoginUsername`), stamps `LastLogonDate = DateTime.Now`, saves, and returns true.
5. Back in `ValidateUser`, on success it checks the database schema version against `DatabaseGlobalSetting.LegalDatabaseVersion` and aborts with a message if they differ.
6. It then applies extra business gates: a non-Admin user must be mapped to a staff/doctor contact (`ReferrenceDoctorOrStaff != null`) and that contact must not be archived. Failing either shows a message telling the operator to log in as Admin and fix the mapping.
7. On success it calls `LoadRolesUser` to build the principal (see 4.4), then `InitPracnetData()`, then closes with `DialogResult.OK`.

Gotcha: password hashing here is legacy SHA1 with no salt and an unusual `"{0:X}"` hex format that drops leading zeroes per byte. Do not "modernise" the hash algorithm in isolation - the stored `SecurityUsers.Password` column holds hashes produced by exactly this routine, so any change to `SecurityManager.HashData` invalidates every existing password. Treat the format as a fixed on-disk contract.

#### Domain / SSO flow

When the machine is domain-joined and the logged-in Windows user maps to a Pracnet user, `frmLogin.ShowHideUserPassword` (`frmLogin.cs:732`) reveals the authentication combo and pre-selects the domain option, disabling the username and password fields. `DomainUserManager` (`SourceCode/Abaki.Common.Security/DomainUserManager.cs`) does the Active Directory work:

- `IsJoinDomain` returns true when the machine name differs from the user domain name.
- `GetLoginUserID()` resolves the current Windows account (`Environment.UserName`) to an AD object GUID via `System.DirectoryServices.AccountManagement`.
- `GetADUserByName` performs a `PrincipalSearcher` lookup and returns a `DomainUser` with display name, samAccountName, mail and the directory GUID.

In `ValidateUser`, the SSO branch matches the AD GUID against `SecurityUser.DomainUserGuid` (falling back to matching `SecurityUser.UserName == Environment.UserName`), applies the same archived-contact gate, then builds the principal without prompting for a password.

There is also a separate SSO path used when Pracnet is launched from a linked clinical system (Medinet), handled entirely in the executable: `MainApp.MedinetAuthenticate` (`SourceCode/Pracnet B1/MainApp.cs:598`). It reads a user GUID out of a shared-memory block keyed by `GetClinicalSoftwareShareKey()`, or falls back to the domain GUID, then loads the `SecurityUser` through `DapperContext.Instance.GetUser(userGuid, domainUserGuid)` and constructs the principal directly. This is why `frmLogin` depends on `DomainUserManager` but the "launched from Medinet" case never shows the login dialog.

Note on terminology: `Environment.UserDomainName.Equals(Environment.MachineName)` is the not-joined test used in a couple of places; `DomainUserManager.IsJoinDomain` is the inverse. `GetADUsers()` currently returns an empty list unconditionally (there is an early `return lstADUsers;` before the real query), so the domain-user picker in the admin form is effectively empty on most installs - worth knowing before assuming AD enumeration works.

### 4.3 How the About-dialog version is sourced

The About box is `SourceCode/Pracnet B1/frmAbout.cs`. It does not read the version from `Pracnet.exe`. Instead it displays `GlobalInfo.PracnetVerion` (`frmAbout.cs:30`), and that global is populated during login by `frmLogin.InitPracnetData()`:

```
GlobalInfo.PracnetVerion = GetPracnetVersion();   // frmLogin.cs:493
```

`GetPracnetVersion()` (`frmLogin.cs:720`) is:

```
Assembly assembly = Assembly.GetExecutingAssembly();
FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
return fileVersionInfo.ProductVersion;
```

Because this method lives inside the `Abaki.Security.GUI` assembly, `Assembly.GetExecutingAssembly()` returns `Abaki.Security.GUI.dll`, so the "Version X.Y.Z" line in the About dialog reflects that DLL's product / file version, not `Pracnet.exe`. The current value is set in `SourceCode/Abaki.SecurityGUI/Properties/AssemblyInfo.cs` (`AssemblyFileVersion("3.0.0")`).

Practical consequence: `Pracnet B1` and `Abaki.SecurityGUI` must be version-bumped in lockstep at release time, otherwise the About dialog shows a stale number even though `Pracnet.exe` advanced. This is exactly what the release rule in `.claude/rules/assembly-version.md` calls out under "Always-bumped projects". The build date shown below the version (`GlobalInfo.PracnetBuildDate`, formatted `dd/MM/yyyy HH:mm:ss`) and the licence expiry line come from separate sources and are unrelated to the assembly version.

### 4.4 The session identity: CustomIdentity and CustomPrincipal

Once a user is authenticated, the whole application reasons about "who am I and what can I do" through the standard .NET `Thread.CurrentPrincipal` slot, holding Pracnet's own implementations in `SourceCode/Abaki.Common.Security/Principal`.

`CustomIdentity` (`CustomIdentity.cs`) wraps a `SecurityUser`. Its constructor takes a username, calls `UserManager.FindByUserName`, throws `SecurityException` if the user does not exist, and caches the loaded `SecurityUser` in `LogInSecurityUser`. `IsAuthenticated` is simply "name is not empty". It also exposes the user GUID (`UserID`), the linked contact GUID (`RefGUID`, which is `SecurityUser.ContactGUID`) and static helpers `GetCurrentContactGuid()` / `GetCurrentUserGuid()` that read off `Thread.CurrentPrincipal`.

`CustomPrincipal` (`CustomPrincipal.cs`) wraps the identity plus string arrays of role names and "rights". When constructed from a `CustomIdentity`, `LoadRolesRights` populates roles from `UserRoleManager.FindByUser` and rights from `SecurityManager.EffectiveRights`. `IsInRole` and `IsAuthorized` are array lookups. Note that this rights-array machinery is the legacy codegator model and is largely vestigial - the live permission checks go through `PracnetAuthorization` and the feature-permission dictionary, not `IsAuthorized`.

Wiring at startup:

- In the login flow, `frmLogin.LoadRolesUser` (`frmLogin.cs:271`) builds a `CustomPrincipal(new CustomIdentity(userName))` and assigns it to `Thread.CurrentPrincipal` (or sets the AppDomain thread principal on first run).
- Back in `MainApp.AuthenticateUser` (`MainApp.cs:759`) it then calls `PracnetAuthorization.SetUserLoginPrincipal(Thread.CurrentPrincipal as CustomPrincipal)` and caches the identity in `GlobalInfo.UserInfo`. The Medinet SSO path does the same at `MainApp.cs:678`.

So there are two "current user" accessors used throughout the codebase: `Thread.CurrentPrincipal` (framework standard) and `PracnetAuthorization.CurrentUserLoginPrincipal` / `GlobalInfo.UserInfo` (Pracnet's cached copy). They are set from the same object; prefer `PracnetAuthorization` for permission checks and `GlobalInfo.UserInfo.UserID` when you need the acting user's GUID for audit stamping (`CreatedGUID`, `UpdatedGUID`, `DeletedGUID`).

### 4.5 The permission model

Two enums in `SourceCode/Abaki.Common.Enums/SecurityPermissionEnums.cs` define the model.

`ePracnetFeatures` (a plain int enum) is the catalogue of protectable areas of the app. Each value is a stable numeric ID that also matches `SecurityFeature.SecurityFeatureId` in the database. Examples: `PatientDemographics = 1`, `Invoices = 2`, `Clinical = 4`, `Documents = 8`, `Appointmentbook = 10`, `BulkBilkClaim = 15`, `MedicareOnline = 40`, `SystemAudit = 59`, `UserAccountManagement = 76`. Because these IDs are persisted, do not renumber existing members - only append new ones (the file already has gaps where features were retired, for example there is no 14, 25, 28, 49, 51).

`ePracnetPermission` is a `[Flags] enum : long`, so a single feature can carry a bitwise-OR combination of actions. The individual flags are the "Create/Read/Update/Delete/Archive/Export/Print_Email style" actions:

- Core CRUD: `Read = 4`, `Create = 4096`, `Update = 1` (the comment notes Update does not apply to invoice Adjust / Writeoff), `Delete = 2048`, `Archive = 2`.
- Output actions: `Print = 8`, `Print_SMS_Email_Fax = 32`, `Print_Email_Fax = 64`, `Print_Email = 128`, `SMS = 256`, `Export = 512`, `ExportToExcel = 32768`.
- Billing-specific actions: `Refund = 16`, `ClearDrawer = 1024`, `AdjustInvoice = 8192`, `WriteOffInvoice = 16384`, `DeleteRefund`, `PrintRefund`, `DeleteWriteOff`, `PrintWriteOff`, `AmendInvoiceReferral`, `Approve`, `ManualReport`.
- Data-scope and record actions: `Merge`, `BulkArchive`, `Upload`, `Download`, `ViewUsersOwnDataOnly = 4194304`.
- One composite convenience value: `CanOpenManagementForm = Read | Create | Update | AdjustInvoice`.

`PracnetAuthorization.GetPermissionName` (`PracnetAuthorization.cs:111`) maps these flags to the human-readable verbs shown in "you do not have permission" messages ("Edit", "Archive", "View", "Print", "Add", "Delete", "Adjust", "Write off", and so on).

Gotcha: the flag values are not contiguous powers of two (there are deliberate gaps, and `long` was chosen precisely because the bit space is filling up - see the commented-out reserved values at the bottom of the enum). When adding a permission, take the next unused power of two and confirm nothing else uses it; the flags are stored as integers in the database, so collisions silently grant the wrong action.

#### Roles (SecurityGroup) and how effective permissions are computed

Roles are `SecurityGroup` rows. A user is linked to roles through `SecurityUserGroup` join rows. A role grants permissions through two further tables: `SecurityGroupFeature` (role plus feature, with an `IsActive` flag) and `SecurityGroupFeaturePermission` (that feature-grant plus a `SecurityPermission`, again with `IsActive`). Permissions are only ever granted through roles; there is no per-user permission override in the live path (the legacy `SecurityUserPermissions` / `EffectiveRights` machinery still exists but is not what gates the UI).

The effective, flattened permission set for the logged-in user is computed lazily on the `SecurityUser` entity itself, in the partial class `SourceCode/Abaki.Data/Domains/Entities_Security/SecurityUser.cs`:

```
public IDictionary<int, ePracnetPermission> FeaturePermissions { get; }   // SecurityUser.cs:23
```

The getter queries the SQL view `SecurityUserGroupFeaturePermissionViews` for the current user's GUID, then folds every `(SecurityFeatureId, PermisionId)` pair into a dictionary keyed by feature ID, bitwise-OR-ing the permission flags so that permissions from multiple roles union together. It is cached in `_featurePermissions`; `RefreshSecurityGroupFeaturePermission()` clears that cache (plus the cached patient-access lists and the linked employee) and is called after any admin edit.

### 4.6 How permissions gate features across the app

Every gate goes through `SourceCode/Abaki.Common.Security/PracnetAuthorization.cs`. The core check is `HasPermission(ePracnetFeatures eFeature, ePracnetPermission eAction)` (`PracnetAuthorization.cs:233`):

1. It reads `CurrentUserLoginPrincipal`, confirms the identity is authenticated and is a `CustomIdentity` with a loaded `LogInSecurityUser`.
2. Admin short-circuit: if the user's GUID equals `ConstantValues.Contact_Admin` (defined in `SourceCode/Abakia.Common.DefinedMessageStrings/ConstantValues.cs:99`), it returns true for everything. The built-in Admin account bypasses all feature checks.
3. Otherwise it looks up the feature ID in `LogInSecurityUser.FeaturePermissions` and returns `(featurePermission & eAction) > 0` - that is, "does the granted flag set for this feature intersect the requested action".

There is an overload `HasPermission(feature, action, Func<bool> canExecute, Action<string,string> showMessageBoxIfNotHavePermission)` (`PracnetAuthorization.cs:215`) that lets a caller supply a pre-condition and an on-failure message callback (the message uses `GetFeatureName` and `GetPermissionName`). Related helpers: `IsAdminUser()` (GUID equals `Contact_Admin`), `HasRoleAdmin()` (Admin GUID, or membership of the `SecurityUser_RoleAdmin` group `ConstantValues.SecurityUser_RoleAdmin`, `ConstantValues.cs:105`), `RefreshUserFeaturePermissions()` (used after admin edits so the running session sees new grants without re-login), and `ResetLoginPrincipal()` (used on log-off - installs an unauthenticated principal).

Usage pattern is pervasive across all layers, not just UI. Typical call sites:

- Forms enable/disable controls and buttons based on the flags. See `frmUserAccount.UpdateControlPermissions` (`SourceCode/Abaki.SecurityGUI/frmUserAccount.cs:182`) and `frmUserManagement.CanCreate/CanEdit/CanView/CanArchive` (`frmUserManagement.cs:29-45`).
- Business logic gates behaviour, for example `AccountHistoryPresenter` and `BillingCommon` guard invoice operations with `HasPermission(ePracnetFeatures.Invoices, ...)` (see `SourceCode/Abaki.Billing.Dal B1/Controllers/BillingCommon.cs:326`, `:421`, `:449`).

Because Admin is a hard-coded bypass by GUID, testing permission behaviour must be done as a non-Admin user; logging in as Admin will make every feature appear enabled.

`InitPracnetData` warms the permission cache early by making one throwaway call: `PracnetAuthorization.HasPermission(ePracnetFeatures.AppointmentTypes, ePracnetPermission.Create)` on the background load thread (`frmLogin.cs:545`). That forces the `FeaturePermissions` dictionary to populate before the main form starts querying it.

#### Patient-level access scoping

Beyond feature flags, a user can be restricted in which patients they can see. `SecurityUser.AccessPatientsType` (a byte) drives this: 0 = all patients, 1 = only patients who have an invoice with this user, 2 = only an explicitly assigned patient list (`SecurityUserPatients`). `PracnetAuthorization.GetAccessedPatientsQuery` (`PracnetAuthorization.cs:46`) emits a SQL `EXISTS(...)` fragment that callers splice into patient queries:

- Type 1 checks `dbo.InvoiceProviderPatientView` for a row matching the login user's employee GUID.
- Type 2 checks `dbo.SecurityUserPatients` (non-deleted) for the patient plus the login user GUID.

`AccessedPatientGuids` exposes the same restriction as an in-memory GUID list. Type 1 depends on the login user being mapped to an employee contact; if `ContactGUID` is null it returns an empty set, effectively hiding all patients. Security note: this fragment is string-concatenated into SQL. The values interpolated are GUIDs sourced from the authenticated principal (not user text), which is why it is not a classic injection vector, but keep that constraint in mind if you extend it.

### 4.7 The admin UI: managing users and roles

All admin screens are in `SourceCode/Abaki.SecurityGUI` and are themselves gated on `ePracnetFeatures.UserAccountManagement`.

- `frmUserManagement.cs` is the management dock window listing users and roles side by side (`ICanExportToExcel`, `IUpdateControlPermisionsOnForm`). It reads via `UserManager.FindAll()` and `RoleManager.FindAll()` and routes create/edit/delete through the managers with `GlobalInfo.UserInfo.UserID` as the acting-user stamp.
- `frmUserAccount.cs` edits a single `SecurityUser`: username, full name, description, linked doctor/staff contact (`cboReferObject`), optional domain user mapping (`cbDomainUsers`), archived flag, patient-access-type radio buttons and an explicit patient list, plus the set of roles assigned to the user. `SaveUser` (`frmUserAccount.cs:292`) enforces that a non-Admin user must be mapped to a doctor and to at least one role, prevents duplicate usernames, prevents assigning a domain user already mapped to someone else, and (when Medinet linking is on) requires a linked Medinet contact. It writes through `UserManager.SaveNewUser` / `SaveUser` and mirrors changes into Medinet via `CoreService.UpdateMedinetSecurityUser`. After saving it calls `PracnetAuthorization.RefreshUserFeaturePermissions()`.
- `RoleDetailsCtrl.cs` edits a `SecurityGroup` (role): its name, description, archived flag, and a checkbox tree of feature/permission grants (`ultraTreeGroupPermissions`). Ticking a leaf toggles `SecurityGroupFeaturePermission.IsActive`; the "Clone" button copies another role's grants into the tree. It saves through `RoleManager.SaveRole` and then refreshes permissions. Non-Admin editors are prevented from granting the `UserAccountManagement` feature (see the filter in `RoleDetailsCtrl.InitTree` `:236` and `frmUserAccount.btnAdd_Click` `:593`), so a normal user cannot escalate their own access to user administration.
- `ChangePassword.cs` and `ResetPassWord.cs` handle password change / reset. `ChangePassword.btnOK_Click` calls `UserManager.UpdatePassword(identity.UserID, old, new)`, which requires the current thread principal to be authenticated (`UserManager.cs:206`). New/reset passwords are hashed via `SecurityManager.HashData` inside the manager, never stored in plaintext.

Design note: several manager methods contain commented-out `if (!Thread.CurrentPrincipal.IsInRole("Admin")) throw ...` guards. Those role-based server-side guards are disabled; the real enforcement is the UI-level `HasPermission` gating plus the `UserAccountManagement`-feature filter. If you add a new entry point that mutates users or roles, gate it explicitly - do not assume the manager will refuse.

Password display quirk: `frmUserAccount` shows a fixed dummy string (`"0123456789"`) in the password box when a user already has a password, and only actually re-hashes when the operator explicitly clicks Reset (`_isResetPassword`). Do not treat the box contents as the real password.

### 4.8 Where the data lives

Security data is stored in the Pracnet SQL Server database (the "Demographic" catalog of the selected profile), reached through EF via the connection string named `PracnetCore1Entities` (referenced in `frmLogin.InitDatabaseConnection`, `frmLogin.cs:152`). The runtime EF contexts used are `NewCommonContext` (tables) and `NewCommonViewContext` (SQL views); some hot reads use Dapper via `DapperContext` (`SourceCode/Abaki.Data.SpecialDtoObjects/DapperContext/DapperContext.cs`, e.g. `GetUser`).

Key tables and views (entities under `SourceCode/Abaki.Data/Domains/Entities_Security`):

- `SecurityUsers` - the user account. Columns of interest: `GUID`, `UserName`, `Password` (SHA1 hash), `FullName`, `Description`, `ContactGUID` (links to the doctor/staff `Employee` contact), `DomainUserGuid` (AD mapping for SSO), `LastLogonDate`, `AccessPatientsType`, and the audit stamps `CreatedGUID` / `UpdatedGUID` / `DeletedGUID` / `DeletedDate`. `Archived` is a computed property meaning `DeletedDate != null` (soft delete - rows are never physically removed by the admin UI).
- `SecurityGroups` - roles (name, description, soft-delete columns, audit stamps).
- `SecurityUserGroups` - user-to-role join (soft-deletable).
- `SecurityGroupFeatures` - role-to-feature grants with `IsActive`.
- `SecurityGroupFeaturePermissions` - the feature-grant-to-permission leaf with `IsActive`.
- `SecurityFeatures` / `SecurityPermissions` - the catalogue rows mirroring `ePracnetFeatures` (via `SecurityFeatureId`) and `ePracnetPermission`.
- `SecurityUserPatients` - explicit patient assignments for `AccessPatientsType == 2`.
- Views: `SecurityUserGroupFeaturePermissionView` (flattened effective permissions consumed by `SecurityUser.FeaturePermissions`), `SecurityUserPatientView` / `SecurityUserSelectedPatientView` (patient-access), and `InvoiceProviderPatientView` (used by access-type 1).

The built-in Admin account is identified everywhere by the fixed GUID `ConstantValues.Contact_Admin` and the built-in admin role by `ConstantValues.SecurityUser_RoleAdmin` (`SourceCode/Abakia.Common.DefinedMessageStrings/ConstantValues.cs`). These GUIDs are seeded by the database migration scripts and must not change.

### 4.9 Gotchas summary for a new maintainer

- Do not change `SecurityManager.HashData` - it is the on-disk password-hash contract for every existing account.
- Do not renumber `ePracnetFeatures` values or reuse `ePracnetPermission` bit values; both are persisted as integers.
- Admin (by GUID) bypasses all `HasPermission` checks - test permissions as a non-Admin user.
- Bump `Abaki.SecurityGUI` and `Pracnet B1` versions together, or the About dialog lies.
- Manager-level "must be Admin" guards are commented out; enforce access at every new entry point via `PracnetAuthorization.HasPermission`.
- After editing users or roles, call `PracnetAuthorization.RefreshUserFeaturePermissions()` so the running session picks up changes without a re-login.
- Users are soft-deleted (`DeletedDate`), never hard-deleted, and a non-Admin user must always be mapped to a live (non-archived) doctor/staff contact to be able to log in.
- `DomainUserManager.GetADUsers()` returns an empty list by design, so the domain-user picker is usually empty; SSO still works through `GetADUserByName` / `GetLoginUserID` and the per-user `DomainUserGuid` mapping.

---

## 5. Database, Entity Framework and data access

This section is the map of how Pracnet talks to SQL Server. If you are new, read this before you touch any repository, presenter or service, because almost everything above the UI layer eventually resolves to one of the objects described here.

### 5.1 The big picture in one paragraph

Pracnet is a database-first Entity Framework 6 application layered over one SQL Server catalogue. Despite the name "multiple contexts", every context in the running application points at the **same physical database** (the clinic's Demographic catalogue). The contexts differ only in which EF metadata (which .edmx model) they carry, not in which server or catalogue they hit. On top of EF, performance-sensitive or awkward-to-map reads go through Dapper (`DapperContext`). A single raw provider connection string is built once at login from the selected profile and stored on the `DatabaseConnectionHelper` singleton, and every context and every Dapper call reads that one string. Caches (`CacheCommonData`) sit in front of reference data so the UI does not re-query slow-changing tables on every keystroke. DTOs (`Abaki.Data.DTOs`, `Abaki.Data.SpecialDtoObjects`) carry data across layers so the UI does not bind directly to EF entities.

### 5.2 Where the connection string actually comes from

There is one raw ADO.NET (SqlClient) connection string in the whole running process. Its lifecycle:

1. At login, `frmLogin` reads the selected database profile and calls `currentProfile.ConnectionString(eInitalCatalog.Demographic)`. The setter is at `SourceCode/Abaki.SecurityGUI/frmLogin.cs:143`:
   `DatabaseConnectionHelper.Instance.ProviderConnectionString = currentProfile.ConnectionString(eInitalCatalog.Demographic);`
2. `ProfileDB` (`SourceCode/Abaki.Common.LocalSettings/ProfileDB.cs`) is a saved connection profile. It derives from `DatabaseSetting` (`SourceCode/Abaki.Common.LocalSettings/DatabaseSetting.cs`), which is where the actual SqlClient string is assembled in `DatabaseSetting.ConnectionString(eInitalCatalog)` (line 257). That method uses a `SqlConnectionStringBuilder`, chooses catalogue by the `eInitalCatalog` enum, applies either trusted (Windows) or fixed (SQL user) authentication, and importantly sets `MultipleActiveResultSets = true` and a connect timeout. The SQL password is stored encrypted on disk (`MNSqlPasswordEncrypted`, encrypt/decrypt via `StringEncryptingDecrypting`), never as plain text in the profile XML.
3. `eInitalCatalog` (`SourceCode/Abaki.Common.Enums/eInitalCatalog.cs`) only has two live values: `Demographic` (the main Pracnet catalogue, value 0) and `DaySurgery` (value 3). The old `Medical` and `MedicalContent` values are commented out. In normal operation everything runs against `Demographic`.

So the profile decides server, catalogue, and credentials. The rest of the data layer never re-derives that; it reads it back off the singleton.

### 5.3 DatabaseConnectionHelper: one raw string, many EF wrappers

`SourceCode/Abaki.Common.Helpers/DatabaseConnectionHelper.cs` is a lazy, double-checked-lock singleton (`DatabaseConnectionHelper.Instance`). It holds two raw strings: `ProviderConnectionString` (the main one set at login) and `P2KProviderConnectionString` (migration only).

Its job is to turn that one raw SqlClient string into the various **EF connection strings** each context needs. It does this by wrapping the raw string with an `EntityConnectionStringBuilder` and attaching the correct EF metadata resource triple (csdl, ssdl, msl) for the model that context uses. Each property returns a differently-badged connection string over the identical provider connection:

| Property | Metadata (model) | Used by context |
|---|---|---|
| `CoreConnectionString` | `Domains.CoreEntities.*` | `CoreContext` |
| `CoreViewConnectionString` | `Domains.CoreViews.*` | `CoreViewContext` |
| `CommonConnectionString` | `Domains.CommonEntities.*` | `CommonContext` |
| `NewCommonConnectionString` | `Domains.Common.*` | `NewCommonContext` / `PracnetCore1Entities` |
| `NewCommonViewConnectionString` | `Domains.CommonView.*` | `NewCommonViewContext` |
| `BillingConnectionString` | `Domains.Billing.*` | `BillingContext` |
| `BillingViewConnectionString` | `Domains.BillingViews.*` | `BillingViewsContext` |
| `MedicareConnectionString` | `Domains.MedicareEntities.*` | `MedicareContext` |
| `AcMedicareOnlineConnectionString` | `Domains.MedicareOnlineModel.*` | `MedicareOnlineContext` |
| `SecurityConnectionString` | `Domains.SercurityModel.*` (note the historical misspelling of "Security") | security data access |
| `AcirConnectionString` | `ACIR.ACIRModel.*` | ACIR / immunisation |
| `TransactionLogConnectionString` | `Domains.TransactionModel.*` | transaction log |

The `res://*/...` syntax means "find this embedded metadata resource in any loaded assembly". Because these are resource names, a mismatch between the metadata triple here and the resource actually embedded in the model assembly produces an obscure runtime failure, not a compile error. Do not rename metadata resources casually.

Gotcha: `EntityBuilder` is a single static field reused across calls, and `GenerateGeneralConnectionString` mutates it each time. This is fine because callers read the result immediately, but it is not thread-safe in the strict sense. In practice the string is stable after login.

### 5.4 The context zoo

There are two families of contexts, and the naming is genuinely confusing.

Family A: the thin per-model wrappers in project `Abaki.Data.DataContext` (folder `SourceCode/Abaki.Data.DataContext/`, assembly `Abaki.Data.DataContexts`). Each is about 15 lines and just injects the right connection string into an EF-generated container living in the `Abaki.Data.OldDomains` namespace:

- `BillingContext : BillingEntities`
- `BillingViewsContext : BillingViewsEntities`
- `CommonContext : CommonEntities`
- `CoreContext : CoreEntities`
- `CoreViewContext : CoreViewEntities`
- `MedicareContext : MedicareEntities`
- `MedicareOnlineContext : MedicareOnlineEntities`

The `*Entities` base classes are code-generated from .edmx files (the `OldDomains` set) at build time; you will not find them as hand-written .cs in the tree.

Family B: the richer contexts in `SourceCode/Abaki.Data/DataContexts/`, which is where the day-to-day action is. The most important one:

`NewCommonContext` (`SourceCode/Abaki.Data/DataContexts/NewCommonContext.cs`) extends `PracnetCore1Entities`, the large ObjectContext generated from `Common.edmx`. This is the primary read/write context for the core Pracnet schema (patients, invoices, providers, fee rates, appointments, and hundreds more). Note it is EF6 but uses the **older ObjectContext API** (`System.Data.Objects`, `ObjectStateManager`, `ContextOptions`), not the newer `DbContext`/`DbSet` API. You will see `NewCommonContext.ContextInstance.<EntitySet>` everywhere.

`NewCommonContext` has two accessors with very different semantics:

- `NewCommonContext.ContextInstance` (line 67): a process-wide **singleton** context, guarded by a lock, with proxy creation off, lazy loading on, and a 90-second command timeout. It reopens the connection if it has dropped. Because it is a singleton, its `ObjectStateManager` accumulates tracked entities for the life of the app; this is the context that owns "the current unit of work" in most screens.
- `NewCommonContext.ThreadInstance` (line 31): a fresh, disposable context with the same options, for code that must not touch the shared singleton (background threads, isolated reads). Use this when you do not want your query to interact with entities another screen is editing.
- `DisposeCurrentConnection()` (line 110) tears down the singleton (used on logout / profile switch).

`NewCommonViewContext` is the read-only companion for view-backed entities (for example `ReferringRequestingProvidersViews`).

### 5.5 SaveChanges is not a plain SaveChanges

Do not assume `SaveChanges` on `NewCommonContext` is the vanilla EF call. The override starts at `SourceCode/Abaki.Data/DataContexts/NewCommonContext.cs:146` and does a lot:

1. It scrubs "modified" entries that have no actual value change back to `Unchanged` (via `MyEFExtension.HasModifiedValue`), so no-op edits do not generate audit noise or write traffic.
2. It runs the audit-log pipeline (`AbakiSaveChangeWrapper.GeneralProcessPreSaveChanges` / `GeneralProcessPostSaveChanges`) over the added/modified/deleted entries, gated by `AbakiAuditLog.IsAuditableObject`. This is how the `AuditLog` table is populated. If you add a new entity type that must be audited, it must flow through this context, not through Dapper.
3. It wraps the real `base.SaveChanges` in a `TransactionScope`.
4. It has a retry loop (up to 10 attempts) for general exceptions, and dedicated handling for `DbEntityValidationException` (logs each property error, shows a fatal message box, returns -1) and `OptimisticConcurrencyException` (refresh, accept, re-save).

Consequences for a new developer:
- Because saves are audited and audited-through-a-singleton, avoid calling `SaveChanges` in tight loops; batch your entity changes and save once where the surrounding code allows it.
- The retry loop and the message boxes mean data-layer save failures can surface as UI dialogs. That is by design here; do not "fix" it by swallowing exceptions.
- The commented-out constructor around line 119 shows a literal connection string with a password. Never uncomment it and never copy that pattern; the real string comes from the profile.

### 5.6 Dapper: DapperContext

`SourceCode/Abaki.Data.SpecialDtoObjects/DapperContext/DapperContext.cs` (`DapperContext.Instance`, singleton) is the second data access path. It opens a brand-new `SqlConnection` per call using `DatabaseConnectionHelper.Instance.ProviderConnectionString` (the same raw string EF uses), wrapped in `using` so it disposes deterministically. See the `DapperConnection` property at line 36.

Use Dapper when:
- you want a specific projection and do not want EF to materialise a full entity graph;
- you are calling a stored procedure that returns a shape not mapped in the .edmx (Dapper calls with `CommandType.StoredProcedure`, for example `sp_OverdueInvoiceSearch` at line 1445 and `sp_GetOutstandingInvoiceQueues` at line 1464);
- you want a cheap scalar (`GetServerDateTime` = `SELECT GETDATE()`, `IsCloudEnv` reads a `Clinic` parameter row);
- you are doing a bulk set-based insert/update that would be painful as tracked entities (`UpdateFeeBBOToBBGP`, `UpdateServiceText`).

Important properties of the Dapper path:
- It maps straight onto the same EF-generated entity classes in many cases (`cnn.QueryFirstOrDefault<Contact>`, `<Patient>`, `<SecurityUser>` etc.), so a Dapper read and an EF read can return the same POCO type. The difference is that the Dapper result is **not tracked** by any context.
- Table and column names are hardcoded SQL. There is no compile-time check that these match the schema. If you rename a column in a migration, `grep` the Dapper queries.
- Parameters are always passed as anonymous objects (`new { Id = guid }`), so these are parameterised and safe from injection. A few methods interpolate a caller-supplied `sqlWhere` fragment (for example `GetInvoiceCount(string sqlWhere)` at line 1346) - those trust the caller to have built a safe fragment, so never pass user text straight in.
- The pervasive `catch (Exception ex) { throw ex; }` pattern is a wart (it resets the stack trace). Do not copy it into new code; use `throw;`.

### 5.7 DTOs and the SpecialDtoObjects project

Two projects carry data between layers:
- `Abaki.Data.DTOs` - straightforward data-transfer objects mirroring the security and other entity groups.
- `Abaki.Data.SpecialDtoObjects` - richer DTOs plus the `DapperContext`, the `DomainDtoConverter` extension methods (for example `OnDTOWithoutChildrenInformation()`), and editable-object infrastructure (`EditableDTO`, `IEnhancedEditableObject`). `PatientDTO`, `ContactDTO`, `FamilyRelationshipDTO`, `OverdueInvoiceDto` and friends live here. When a presenter needs a patient with a controlled subset of related data, it typically calls `DapperContext` and converts to a DTO rather than dragging a lazy-loaded EF graph up into the UI.

Rule of thumb: entities (`Abaki.Data.Domains`) are the persistence shape; DTOs are the transport shape; do not bind WinForms grids directly to tracked entities from the singleton context if you can bind to a DTO instead.

### 5.8 Caches: CacheCommonData

`SourceCode/Abaki.Data/Cache/CacheCommonData.cs` is a static in-memory cache for slow-changing reference data (fee rates, appointment types, provider business addresses, referring providers, bank accounts, MBS items, etc.). It is populated lazily on first access and backed by `NewCommonContext.ContextInstance` / `NewCommonViewContext.ContextInstance`.

Patterns to know:
- Most cached lists are `private static` fields with a public accessor that lazily loads on null, plus a `RefreshXxx` flag or method to force reload. Example: `ProviderBusinessAddresses` (line 50), `AppointmentTypes` (line 105) with `RefreshAppointmentTypes` (line 117).
- `ClearCacheCommonData()` (line 93) nulls the fields and calls `GC.Collect()`. This is invoked on events that invalidate reference data broadly.
- `GetFeeRates()` (line 436) returns active fee rates, explicitly excluding soft-deleted rows and the internal `SYSDRVI+` derived-item codes: `FeeRates.Where(f => !f.DeletedDate.HasValue && !f.Code.Contains("SYSDRVI+"))`.
- Stored-procedure reads are exposed here too and go through EF function imports, not Dapper: `GetMbsItemMatchedItemCode` (line 302) calls `NewCommonContext.ContextInstance.GetOneBillingItemBaseOnItemCode(...)`; `GetFeeRateDetails` (line 330) calls `GetItemRateByItemCodeAndFeeRate(...)`. Both wrap in try/catch and return null on failure, so a mapping mismatch presents as "the fee did not populate" rather than a hard crash (see drift note below).

Gotcha: because the cache is static and keyed to the process, it is effectively tied to the logged-in profile. On profile switch / logout the cache must be cleared (`ClearCacheCommonData`) or you will show one clinic's reference data against another.

### 5.9 How to find an entity, a stored procedure, and add a query

Finding an entity. The core Pracnet entities (mapped to `Common.edmx` / `PracnetCore1Entities`) exist as partial classes grouped under `SourceCode/Abaki.Data/Domains/`:
- `Entities_Common/` (for example `AccountType.cs`, `FeeRate.cs`, `BulkBillClaiming.cs`, `AuditLog.cs`)
- `Entities_Billing/` (for example `Alloc.cs`, `BulkBillVoucher.cs`, `AccountTypeFeeRateMapping.cs`)
- `Entities_MedicarOnline/`, `Entities_Others/`, `Entities_Security/`

These hand-written partials add behaviour; the generated half of each entity, plus the whole `PracnetCore1Entities` context (all `ObjectSet<T>` properties and all function imports), is in the generated `SourceCode/Abaki.Data/Domains/Common.Designer.cs`. That file is enormous (over 120,000 lines), so search it with `grep`, do not open it whole. To confirm an entity is mapped, search `Common.Designer.cs` for `public ObjectSet<YourEntity>`.

Finding a stored procedure that EF can call. EF exposes stored procs as "function imports" that become methods on the context using `base.ExecuteFunction<T>(...)`. Search `Common.Designer.cs` for the proc name, for example `GetOneBillingItemBaseOnItemCode` (method at line 5116, `ExecuteFunction` call at 5148) or `GetItemRateByItemCodeAndFeeRate` (line 5779). If the proc returns a non-entity shape, EF generates a `_Result` complex type (for example `GetItemRateByItemCodeAndFeeRate_Result`). Stored procs that are not imported into the model are still reachable from `DapperContext` with `CommandType.StoredProcedure`.

Adding a query. Decide the path:
- Simple typed read or an aggregate/report projection that is annoying to map: add a method to `DapperContext`. Follow the existing shape (`using (var cnn = DapperConnection)`, parameterised anonymous object, return a DTO or a list). Add a corresponding pure-logic helper and a unit test where any non-trivial logic exists (see the repository unit-test rule).
- Read/write of tracked core entities: use `NewCommonContext.ContextInstance`, mutate the entity, and call `SaveChanges` once. Remember the audit pipeline runs automatically.
- New stored procedure you want strongly typed through EF: you must add a function import to the model (`Common.edmx`) in Visual Studio, which regenerates `Common.Designer.cs`. That is a heavier change and it changes the .edmx; coordinate it, and remember the metadata is what the running app validates the reader against.
- New table/column: add a numbered migration script (see next section) and, if EF needs to see the new shape, update the .edmx too.

### 5.10 Migrations and how the EF model drifts ahead of the schema

Schema changes are shipped as numbered SQL scripts, not EF Code-First migrations. They live under `Database/PracnetDatabaseFrom0.1.6/Revision_<version>/` (currently `Revision_0.1.6` through `Revision_0.1.9`), split into `FullScriptsFromPreviousVersion` and `UpdateScriptsForCurrentVersion`. Files are named `<id>_<description>.sql` (for example `785_PRACNET3006_add_indicators_to_sp_ViewInvoiceSearch.sql`) and run in id order.

The applier is `PracnetDatabaseUpdateTool` (a WinForms tool, `SourceCode/PracnetDatabaseUpdateTool/`). The pure decision/parsing logic was extracted into `SourceCode/Abaki.Common.Helpers/DbUpdateScriptHelper.cs` so it can be unit tested; read that file's class comment for the exact algorithm. In short:
- Each `.sql` file is split into `GO`-separated batches; `IsRunnableChunk` skips empty/whitespace batches.
- `TryParseScriptFileId` reads the leading numeric id from the filename (leading zeros ignored).
- `ParseRevisionToken` turns a folder like `Revision_0.1.9` into an ordering token and skips non-version folders (for example `IndexingScripts`).
- The tool compares packaged script ids on disk against ids already recorded in the `DatabaseUpgradeTrackings` table, applying only the missing ones. `FirstMissingScriptId` finds an interior gap so a run can resume mid-revision. `IsDatabaseBehind` powers an explicit "DB still behind build" warning so a partial upgrade is never silent.
- Which revision folder the tool even looks at is gated on the `Clinic.DatabaseVersion` parameter row. The first script in a revision bumps it (for example `001_UpdateDatabaseVersion.sql` sets `DatabaseVersion = '0.1.9'`). A stale `DatabaseVersion` makes the tool skip a whole revision folder.

Why this matters, and the classic drift symptom. The EF model (`Common.edmx` / `Common.Designer.cs`) is edited in the source tree, while the database is upgraded separately by the update tool on each site. So on any given SIT or customer database, the compiled EF model can be **ahead of the actual schema** if a migration batch did not run. This drift presents in two distinct ways, and telling them apart tells you exactly what is missing:

- EF error "the data reader is incompatible ... member '<Col>' does not have a corresponding column" - the reader came back without the column EF expected. For view/stored-proc-backed reads this means the stored procedure or view was **not updated** to select that column, so its migration script has not been applied. Because procs like `sp_ViewInvoiceSearch` build their SELECT as dynamic SQL (`SET @SQLString = '...'; EXEC(@SQLString)`), `ALTER PROCEDURE` never validates columns, so the failure only shows up at runtime through EF.
- `SqlException` "Invalid column name '<Col>'" - the proc/view WAS updated to reference the column, but the underlying table column does not exist, so the **table-add migration** did not run.

Diagnosing safely (no patient data): read `OBJECT_DEFINITION(OBJECT_ID('dbo.<proc>'))`, check `sys.columns` on the table, check `DatabaseUpgradeTrackings` for the expected script ids, and check the `Clinic.DatabaseVersion` row. Remediation on shared or customer environments is to run `PracnetDatabaseUpdateTool`, not to hand-run ALTER statements in SSMS, so the tracking table and version stay consistent.

### 5.11 Security and configuration gotchas

- The checked-in `SourceCode/Abaki.Data/App.Config` contains design-time EF connection strings (`PracnetCore1Entities`, `NewCommonViewEntities`, `TransactionEntities`, `PracnetDevEntities`) that include developer SQL Server credentials in clear text. These are design-time only and are always overridden at runtime by the login profile via `DatabaseConnectionHelper`; treat them as legacy scaffolding, do not rely on them, and do not add real customer credentials there. The metadata portion of each entry is still useful as the authoritative mapping of connection-string name to model (for example `PracnetCore1Entities` uses `Domains.Common.*`).
- Profile passwords on disk are encrypted (`DatabaseSetting.MNSqlPasswordEncrypted`), so do not log the decrypted `MNSqlPassword`.
- `MultipleActiveResultSets=true` is set on the runtime connection. Code relies on it (nested readers within a single connection). Do not strip it.

### 5.12 Quick reference: file map

| Concern | File |
|---|---|
| Raw connection string, per-model EF wrappers | `SourceCode/Abaki.Common.Helpers/DatabaseConnectionHelper.cs` |
| Profile to SqlClient string, catalogue selection, auth | `SourceCode/Abaki.Common.LocalSettings/DatabaseSetting.cs`, `ProfileDB.cs` |
| Catalogue enum | `SourceCode/Abaki.Common.Enums/eInitalCatalog.cs` |
| Connection set at login | `SourceCode/Abaki.SecurityGUI/frmLogin.cs:143` |
| Primary write context (audit, retry, TransactionScope) | `SourceCode/Abaki.Data/DataContexts/NewCommonContext.cs` |
| Thin per-model EF contexts | `SourceCode/Abaki.Data.DataContext/*.cs` |
| Generated ObjectContext and function imports | `SourceCode/Abaki.Data/Domains/Common.Designer.cs` (search, do not open whole) |
| Entity partials | `SourceCode/Abaki.Data/Domains/Entities_*/` |
| Dapper reads/writes and stored-proc calls | `SourceCode/Abaki.Data.SpecialDtoObjects/DapperContext/DapperContext.cs` |
| DTOs and converters | `SourceCode/Abaki.Data.DTOs/`, `SourceCode/Abaki.Data.SpecialDtoObjects/` |
| Reference-data cache | `SourceCode/Abaki.Data/Cache/CacheCommonData.cs` |
| Migration scripts | `Database/PracnetDatabaseFrom0.1.6/Revision_*/` |
| Migration decision/parse logic (tested) | `SourceCode/Abaki.Common.Helpers/DbUpdateScriptHelper.cs` |
| Migration applier tool | `SourceCode/PracnetDatabaseUpdateTool/` |

---

## 6. Database migrations and the update tool

This is the operations-critical subsystem. Every schema change, stored-procedure change, view change, reference-data change and Medicare configuration change ships as a numbered `.sql` migration script, and the `PracnetDatabaseUpdateTool` is what applies those scripts to a customer or SIT database when a new build is installed. If this tool skips a script or moves the recorded version without actually running the change, the running application ends up with an EF model that is ahead of the physical schema, which surfaces as the "data reader is incompatible" class of runtime error (see the schema-drift discussion at the end). Understand this section before you touch anything in the migration pipeline.

### 6.1 Where the scripts live and how they are named

Migration scripts are stored under:

```
Database/PracnetDatabaseFrom0.1.6/
├── 202_DropTable P2KChangeLogs.sql        (a few loose scripts sit at the root)
├── Revision_0.1.6/
├── Revision_0.1.7/
├── Revision_0.1.8/
├── Revision_0.1.9/
│   ├── FullScriptsFromPreviousVersion/     (baseline full scripts, IsFromFullScripts = true)
│   └── UpdateScriptsForCurrentVersion/     (the incremental migrations you normally add)
├── IndexingScripts/
├── 1stAvailable - Integration/
├── HealthEngine - Integration/
└── ... (other integration and link-processor folders)
```

The folder that matters for day-to-day work is `Revision_0.1.9/UpdateScriptsForCurrentVersion/`. As of writing it holds around 760 scripts. Each incremental migration file is named:

```
<NNN>_<free-text-description>.sql
```

for example `783_PRACNET3006_add_SubmissionAuthorityInd.sql`, `790_BulkBillTransmitFailed_RevertToBatched.sql`, `793_PRACNET3005_set_ProductionMedicareOnlineClientId_V3_0_0.sql`. The leading numeric token (`NNN`) is the **ScriptFileId** and it is the only thing the tool uses to order and gate scripts. The description after the first underscore is for humans and is ignored by the parser.

Key naming rules, all enforced by `DbUpdateScriptHelper.TryParseScriptFileId` (see 6.4):

- Only the token **before the first underscore** is parsed as the id. Everything after is free text.
- Leading zeros are stripped (`0785_x.sql` parses to `785`).
- Any non-digit, non-space character in the id token makes the filename **invalid**, and an invalid file is **silently never run**. The tool records the skipped filename so it can be surfaced in the end-of-run summary (`skippedMisnamedScripts`), but it will not execute. A file called `add_column.sql` with no leading number is therefore a no-op that quietly does nothing. Always start the filename with a number.
- The file must have a `.sql` extension; anything else is logged and skipped.

### 6.2 Non-contiguous numbering is expected and acceptable

Do **not** assume ids are contiguous. Real gaps exist in the shipped set, for example the jump from `749` straight to `780`, and `786` to `788` (787 was never shipped). This release also appends `793` and `794` after `792`. Non-contiguous numbering is deliberate and safe because the tool only ever asks "which packaged ids are greater than the highest id already applied" and "is there an interior gap that corresponds to a script that actually exists in the package". It never assumes id N+1 exists. When you add a migration, use the next number above the current maximum; you do not need to backfill gaps.

There is one subtle safety property worth internalising: when the tool looks for an interior gap to resume from (6.5), it requires the gap id to be **present in the package** before it will try to run it. Without that guard, an absent id like `787` would be treated as "missing and needs running", and the fleet migration would fail trying to execute a script file that does not exist. This is covered by the `FirstMissingScriptId_GapAbsentFromPackage_ReturnsNull` unit test.

### 6.3 The two moving parts: the tool and the extracted pure logic

The subsystem is split into a WinForms tool that does the SQL and UI, and a pure, unit-testable helper that makes all the decisions.

| Component | Path | Responsibility |
|---|---|---|
| Update tool (form) | `SourceCode/PracnetDatabaseUpdateTool/frmDBUpdate.cs` | Profile discovery, connection, reading scripts off disk, running batches, transactions, tracking rows, progress UI |
| Pure decision logic | `SourceCode/Abaki.Common.Helpers/DbUpdateScriptHelper.cs` | Parse ids, parse revision tokens, decide runnable chunks, find gaps, decide "database behind" |
| Batch splitter | `SourceCode/Abaki.Pracnet.ConversionCommonHelper/CommonHelper.cs` (`GetScriptStringFromFile`) | Splits one `.sql` file into GO-delimited batches |
| Unit tests | `SourceCode/TestAbakiCommon/TestDbUpdateScriptHelper.cs` | Covers every branch of the pure logic, including the non-contiguous cases |

The tool's own namespace is `Abaki.MedinetDBUpdate` and it lives in project `Abaki.Pracnet.DatabaseUpdateTool` (the class comments still refer to that project name). The reason the decision logic was extracted into `DbUpdateScriptHelper` is precisely that the form is coupled to WinForms and live SQL and cannot be unit tested, so the id-parsing, gap-finding and behind-detection logic was pulled out into a static, side-effect-free class. Any change to how scripts are selected or ordered should go into `DbUpdateScriptHelper` with a matching test, not inline in the form.

### 6.4 The DbUpdateScriptHelper methods

All five methods are pure (no SQL, no WinForms) and each has direct test coverage in `TestDbUpdateScriptHelper.cs`.

- **`IsRunnableChunk(string sql)`** returns false for null, empty or whitespace-only batches. This guards the per-batch loop against blank GO-segments. Historically the loop did `sql.Substring(0, 4)` and crashed on short batches; this guard replaced that.
- **`TryParseScriptFileId(string fileName, out int id)`** implements the naming rules in 6.1 (leading token, leading-zero stripping, non-digit rejection).
- **`ParseRevisionToken(string folderName, out string formattedVersionString)`** converts a folder name like `Revision_0.1.9` (or a bare `0.1.9`) into an ordering token and the normalised dotted string `0.1.9`. The token is `part0*100 + part1*10 + part2` (so `0.1.9` gives `19`, `0.1.8` gives `18`). It returns `-1` for anything that is not a three-part numeric version, which is how folders like `IndexingScripts` are recognised as non-versioned. This mirrors `frmDBUpdate.GetMajorVersionValue`, which now just delegates to it.
- **`FirstMissingScriptId(appliedIds, packagedIds)`** finds the first id that is packaged but not applied, looking only in the interior range between the smallest and largest applied id, and only returning ids that actually exist in the package. Used to resume mid-revision. Returns null when there is no interior gap. See 6.5.
- **`IsDatabaseBehind(maxAppliedId, packagedIdsForRevision)`** returns true when any packaged id is greater than the highest applied id. This drives the explicit "DB still behind build" warning (6.7).

### 6.5 How the tool decides what to run (forward-only application)

The heavy lifting is in `frmDBUpdate.DoUpdateDatabase` (`frmDBUpdate.cs:461`). The flow per database profile:

1. **Read the recorded version.** `GetCurrentDbMajorVersion` reads `Clinic.Value where Parameter = 'DatabaseVersion'` (a dotted string such as `0.1.9`). `GetCurrentDbSubVersion` (`frmDBUpdate.cs:860`) then reads `select max(ScriptFileId) from DatabaseUpgradeTrackings where DatabaseRevison = '<version>' and IsSuccessful = 1 and IsRollbackTransaction = 0`. That maximum is the high-water mark of what has genuinely been applied. If there are no rows it returns `-1`.
2. **Ensure the tracking table exists.** `GetCurrentDbSubVersion` runs `CheckExistAndCreateTableDatabaseUpgradeTrackings` first (`frmDBUpdate.cs:830`), which creates `DatabaseUpgradeTrackings` if absent and back-fills the `IsRollbackTransaction` column if an older schema is missing it. So the table is self-provisioning; you never ship a migration to create it.
3. **Resume adjustment.** If `GetMinMissingScriptFileId` finds an interior gap (via `FirstMissingScriptId`), the effective sub-version is lowered to `gap - 1` so the run restarts from the first missing script rather than from the high-water mark. This is what lets a partially-applied revision resume cleanly.
4. **Select the pending scripts.** `availableUpdatedScripts` (`frmDBUpdate.cs:503`) selects, from the packaged set, every script whose `ScriptFileId > currentPracnetDbSubVersion` for the current revision (plus everything belonging to other revisions/`IndexingScripts`), ordered by `ScriptFileId`. This is strictly **forward-only**: a script whose id is at or below the recorded max is never re-run.

The consequence for you: the tool never re-runs an applied script, so a migration only ever executes once per database. If you need to change something a previous script did, you write a **new higher-numbered** script; you never edit an already-shipped script, because databases that already ran it will never see the edit.

### 6.6 One transaction per file, tracking row written atomically inside it

This is the most important durability property and it is easy to misread as "all or nothing". It is not. Inside the loop (`frmDBUpdate.cs:565` onward):

- Each `.sql` file is loaded and split into GO-delimited batches by `CommonHelper.GetScriptStringFromFile`. Note that method also strips `--` line comments, strips `/* */` block comments and collapses whitespace, and only treats a line that is **exactly** `GO` (case-insensitive) as a batch separator. A `GO` with a trailing comment or count is not recognised as a separator, so keep batch terminators as a bare `GO` on its own line.
- **One SQL transaction is started per file** (`SqlConnectionObject.StartNewTransaction`). Every runnable batch in that file executes on the same transaction with `CommandTimeout = 0` (no timeout, because some migrations are long).
- If the file is the last script in the revision, the recorded `Clinic.DatabaseVersion` is advanced to the revision's formatted version **inside the same transaction** (`frmDBUpdate.cs:614`). So the dotted version only moves once every file in the revision has actually committed.
- The **DatabaseUpgradeTrackings row for the file is inserted inside the same transaction** as the data change (`WriteTrackingRow` called with the working connection and its transaction, `frmDBUpdate.cs:625`), then `Commit` is called. Because the tracking row and the schema change commit together, the tracking table can never claim a script ran when its change was rolled back, and it can never be missing a row for a change that did commit.

The net behaviour is **durable and resumable, not all-or-nothing**: files committed before a failure stay applied and stay recorded; the failing file rolls back cleanly; the next run resumes at the failing file. A crash mid-run leaves you at a well-defined, consistent point, not a half-applied revision.

### 6.7 How the tool recovers from a bad script, and the "DB still behind" warning

When a batch throws (`frmDBUpdate.cs:631`):

1. The error is logged to `ErrorLogger` and to the per-profile upgrade log file under `Logs/UpgradePracnetDatabase_<profile>_<timestamp>.txt`, including the failing script name and the full SQL path.
2. `SqlConnectionObject.Rollback()` rolls back the failing file's transaction. Everything committed before it survives.
3. A **failure tracking row** is written on a **separate connection with a null transaction** (`trackingLogConnection`, `frmDBUpdate.cs:644`) with `IsSuccessful = 0` and `IsRollbackTransaction = 1`. Because it is outside the rolled-back transaction, this failure row persists and is visible for diagnosis. The `GetCurrentDbSubVersion` query deliberately filters `IsSuccessful = 1 and IsRollbackTransaction = 0`, so a failure row never advances the high-water mark.
4. The script name is added to `lastRunFailedScripts`, the profile is marked "Failed", the grid refreshes and the profile's run returns. Other profiles still get processed.

After a run completes, the tool does an explicit, non-silent behind-check (`frmDBUpdate.cs:663`): it re-reads the applied max and calls `DbUpdateScriptHelper.IsDatabaseBehind`. If the package still holds ids newer than what is applied, `lastRunDbBehind` is set and a "WARNING: database is still behind the packaged scripts after this run" line is written to the log and surfaced in the end-of-run summary message box (`backgroundWorker1_RunWorkerCompleted`, `frmDBUpdate.cs:1163`). The summary box also lists failed scripts and any badly-named scripts that were skipped. The whole point of this hardening (commit `11f5b4e1`, "Harden PracnetDatabaseUpdateTool against silent / all-or-nothing failures") is that an incomplete update is **never silent**: it is recorded in the tracking table, written to the log and shown to the operator.

The result of the whole run is also written to the registry via `WriteUpdateDbStatusToRegister` (`PracnetDatabaseUpdateStatus`): 2 = all profiles succeeded, 1 = at least one succeeded, 3 = all failed, plus workstation / no-profile / no-connection codes. The main application reads this to decide messaging. Note the tool only runs on the **server** machine (`IsComputerTypeServer` / `IsServerMachine`) and auto-runs against the `Pracnet` / `PracnetSample` catalogues; on a workstation it exits with status 4.

### 6.8 How scripts get from the repo to the tool at runtime

The release build (`BuildScripts/PracnetBuild.bat`) copies the whole tree recursively:

```
xcopy "..\Database\PracnetDatabaseFrom0.1.6/*.sql" "..\Output\...\DbScriptsPackage" /s ...
```

At runtime `ReadSqlFiles` (`frmDBUpdate.cs:894`) walks the `DbScriptsPackage` folder next to the tool, treating each subfolder as a revision (via `ParseRevisionToken`), and within each revision reading `FullScriptsFromPreviousVersion` and `UpdateScriptsForCurrentVersion`. `IndexingScripts` is handled specially (all ids treated as 0, never advances the recorded version). So the on-disk folder layout under `Database/PracnetDatabaseFrom0.1.6/` is the exact structure the tool expects; do not rename revision folders or move the `UpdateScriptsForCurrentVersion` subfolder.

### 6.9 How to add a migration correctly

1. **Pick the next id.** Take the current maximum id in `Revision_0.1.9/UpdateScriptsForCurrentVersion/` and add one. Gaps below it are fine; do not reuse a number. Name the file `<NNN>_PRACNET-<ticket>_<short-description>.sql`.
2. **Make it idempotent.** The tool will not re-run a committed script, but SIT and support run scripts by hand, restores replay history, and a failed-then-fixed re-run can re-touch a file. Every script must be safe to run twice. Use guarded DDL and upserts:
   - Column add: wrap in `IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'X' AND Object_ID = Object_ID(N'dbo.Table')) BEGIN ALTER TABLE ... END` (see `783_PRACNET3006_add_SubmissionAuthorityInd.sql`).
   - Reference / config data: `IF EXISTS (...) UPDATE ... ELSE INSERT ...` (see `793_PRACNET3005_set_ProductionMedicareOnlineClientId_V3_0_0.sql`). This upsert form also sets the value even when the row is missing.
   - Stored procedures and views: prefer `CREATE OR ALTER`, or drop-if-exists then create.
3. **Batch with GO.** Put a bare `GO` on its own line to separate statements that SQL Server requires in separate batches (for example a `CREATE PROCEDURE` must be the first statement in its batch). Remember the splitter only recognises a line that is exactly `GO`, strips `--` comment lines, and collapses whitespace, so keep `GO` unadorned and do not rely on a comment surviving beside code on the same line.
4. **Do not edit a shipped script.** Correct a past change with a new higher-numbered script.
5. **Do not bump `Clinic.DatabaseVersion` by hand in the script.** The tool advances it automatically when the last file in the revision commits.
6. **If you touch a stored proc or view that EF projects into a complex type, ship the matching table change and proc change in the same revision** so the model and schema move together. This is the crux of the drift bug below.

### 6.10 The ViewInvoiceSearch schema-drift class of bug and why the hardening matters

`sp_ViewInvoiceSearch` builds its query as dynamic SQL (`SET @SQLString = 'SELECT ...'` then `EXEC(@SQLString)`). Because the SELECT is a string, `ALTER PROCEDURE` never validates that the columns it references exist, so a proc-update migration can be applied against a table that has not yet had the column added. When the EF model (the `ViewInvoiceSearch` complex type in `SourceCode/Abaki.Data/Domains/Common.edmx`) is ahead of the physical database, the two failure shapes tell you exactly which migration is missing:

- **EF mapping error, "the data reader is incompatible ... member '<Col>' does not have a corresponding column"** means the **stored proc was not updated** (the SELECT does not return the column). For the 2026 `SubmissionAuthorityInd` incident this proved that `785_PRACNET3006_add_indicators_to_sp_ViewInvoiceSearch.sql` had not been applied, i.e. the whole `Revision_0.1.9` batch had been skipped on that database.
- **`SqlException`, "Invalid column name '<Col>'"** means the proc **was** updated to SELECT the column but the **table column does not exist** because the table-add migration (for example `783`) did not run; `EXEC(@SQLString)` fails resolving the column at runtime.

Diagnose schema-only, with no patient data touched: inspect `OBJECT_DEFINITION(OBJECT_ID('dbo.sp_ViewInvoiceSearch'))`, `sys.columns` on `dbo.Invoices`, the `DatabaseUpgradeTrackings` rows for the relevant script ids, and `Clinic.DatabaseVersion` (a stale recorded version makes the tool skip the revision folder entirely). Remediation on shared SIT or customer environments is to **run `PracnetDatabaseUpdateTool`**, not to hand-patch in SSMS, so the tracking table is brought back into agreement with the schema.

This is exactly the failure mode the hardening in 6.6 and 6.7 is designed to make impossible-to-hide. Before the hardening, a partially-applied or skipped revision could leave the recorded version looking advanced while scripts silently had not run. Now: the tracking row commits atomically with each file so the table cannot lie about what ran; a failure writes a persistent failure row and rolls back only that file; and the explicit `IsDatabaseBehind` check plus the end-of-run summary force any remaining gap to the surface instead of leaving the application to discover it later as a "data reader is incompatible" crash. Any future change to the selection or transaction logic must preserve those three properties, and must come with a matching test in `TestDbUpdateScriptHelper.cs`.

---

## 7. Invoices, billing and fee calculation

The billing engine is the largest and most intricate subsystem in Pracnet. It is spread across four layers: the invoice GUI (`Abaki.Billing.Gui B1`), the presenter and calculation layer (`Abaki.Billing.Bll B1`), the data-access controllers (`Abaki.Billing.Dal B1`), and the shared fee cache and entities (`Abaki.Data`). This section walks through how an invoice is created, how a fee rate is chosen, how each line item computes its fee, rebate, GST and gap, how derived items and incentives work, and how health-fund fee schedules are imported. Everything below is grounded in the current code, with repo-relative paths and file:line citations where it helps.

### 7.1 The moving parts at a glance

| Concern | Type / file |
|---|---|
| Invoice-level orchestration | `InvoicePresenter` in `SourceCode/Abaki.Billing.Bll B1/InvoicePresenter.cs` |
| Per-line-item calculation | `InvItemPresenter` in `SourceCode/Abaki.Billing.Bll B1/InvItemPresenter.cs` |
| Fee-rate selection, rounding, searches | `BillingCommon` (singleton) in `SourceCode/Abaki.Billing.Dal B1/Controllers/BillingCommon.cs` |
| Rebate strategy | `IRebateCalculator` and its two implementations in `SourceCode/Abaki.Billing.Dal B1/Controllers/IRebateCalculator.cs` |
| Derived-item fees | `DerivedFeeCalculator` in `SourceCode/Abaki.Billing.Bll B1/DerivedFeeCalculator.cs` |
| Fee-rate and item-rate cache | `CacheCommonData` in `SourceCode/Abaki.Data/Cache/CacheCommonData.cs` |
| Invoice item grid UI | `InvItemGridView` in `SourceCode/Abaki.Billing.Gui B1/InvItemGridView.cs` |
| Invoice header (claiming checkboxes) | `InvoiceHeaderCtrl` in `SourceCode/Abaki.Billing.Gui B1/InvoiceHeaderCtrl.cs` |
| Fee-schedule import tool | `frmUpdateBillingItems3` in `SourceCode/BillingItemManagement/frmUpdateBillingItems3.cs` |
| Invoice-item entity (computed money) | `InvItem` in `SourceCode/Abaki.Data/Domains/Entities_Billing/InvItem.cs` |

The presenter classes implement `INotifyPropertyChanged` (they call a `Notify(...)` helper) and are data-bound to the WinForms grid, so most calculations run as a side effect of a property setter firing.

### 7.2 Account types drive everything

An invoice is opened against a billing participant, and the participant's account type governs which fee schedule is selected, which claiming path is available, and which header fields are enabled. Account types are identified by fixed GUID constants (`ConstantValues.AccountType_*`), the important ones being:

- `AccountType_BulkBill` (Medicare bulk bill, 100 percent rebate, DB4 form)
- `AccountType_PrivatePatient` (private patient, gap billed)
- `AccountType_PrivateHealthFunds` (health-fund schedules)
- `AccountType_VeteranAffair` (DVA)
- `AccountType_IMC` (in-patient medical claim) and `AccountType_OVS` (overseas)
- `AccountType_Workcover` and `AccountType_TAC` (workers compensation and transport accident)
- `AccountType_Other`, `AccountType_NoCharge`, `AccountType_ChildImm` (child immunisation)

These GUIDs are compared directly all over the billing code (see for example the enable/disable block in `InvoiceHeaderCtrl.cs` around lines 624 to 686 and the account-specific overpayment logic in `BillingCommon.CheckOverpayment`, `BillingCommon.cs:505`). Treat the account-type GUID as the primary switch when tracing any billing behaviour.

### 7.3 Fee-rate selection: `BillingCommon.GetDefaultFeeRate`

When an invoice or an item needs a schedule, the entry point is `BillingCommon.Instance.GetDefaultFeeRate(...)` (`BillingCommon.cs:1214`). `BillingCommon` is a singleton (`BillingCommon.Instance`) that also caches `ScheduleTypes` (the `AccountType` list) and owns the money-rounding helper and the two rebate calculators. Selection precedence:

1. **Child immunisation short-circuit.** If `accountType == AccountType_ChildImm`, it returns the hardcoded `ConstantValues.FeeRate_ChildIM` immediately (`BillingCommon.cs:1218`).
2. **Patient preferred fee rate.** If the caller passes `patientFeeRateGuids` and it matches the patient's `patientAccountTypeGuid`, that fee rate wins (`BillingCommon.cs:1221`).
3. **Participant-id lookup for IMC / OVS / health funds.** For those three account types the method filters `CacheCommonData.GetFeeRates()` by `ParticipantId == fundId`; if exactly one schedule matches, it is used (`BillingCommon.cs:1238`). Note the deliberate "exactly one" guard: an ambiguous fund id falls through rather than guessing.
4. **AccountType default, then global default.** Still inside the private/no-charge/other/IMC/OVS/health-fund branch, it looks up the `AccountType` row and uses `accType.DefaultFeeRateGUID`, then `RecallGlobalSetting.Instance.DefaultFeeRate` as a fallback (`BillingCommon.cs:1247` to `1255`).
5. **`AccountTypeFeeRateMappings` table.** The general path queries `AccountTypeFeeRateMappings` filtered by `AccountTypeGUID`, then optionally by `ServiceType` (substring match, so a mapping row can list several service types), `State` and `FundId` (`BillingCommon.cs:1259`). When more than one row survives, it disambiguates by exact `State` match first, then by `HasFacilityId` (`BillingCommon.cs:1271` to `1292`). With a single row it uses that row; with none it returns null and the caller falls back.

Gotcha: the `ServiceType` and `State` and `FundId` filters use "column is null OR equals" semantics, meaning a mapping row with a null column is treated as a wildcard. Do not assume a mapping is scoped narrowly just because you passed a state or fund.

### 7.4 Per-item flow: sync, calculate, round

Each line item is an `InvItemPresenter`. When the item code, fee-rate code or service date changes (or when the grid tells it to), the item recomputes via `SyncFeeWithSchedule` (`InvItemPresenter.cs:705`):

1. `SyncFeeWithSchedule(serviceTypeCde, locationId)` clears `MbsItemCode`, then asks `BillingCommon.Instance.GetRebateCalculator(this.FeeRateCode)` for the right strategy and calls `calculator.Calculate(this, serviceTypeCde, locationId)`. If the item code is empty it returns early, and if the code is not in the database it defaults `MbsItemCode` back to `ItemCode`. It then fires `Notify("Fee")`, `Notify("GST")` and `Notify("Description")` so the grid refreshes.
2. `Calculate` (on the chosen `IRebateCalculator`) loads the matched item through `CacheCommonData.GetMbsItemMatchedItemCode(feeRateGuid, serviceDate, itemCode)`, sets `Fee`, GST, `Description`, `MbsItemCode`, `FeeIncludeGST` and `RebatePerItem`, then calls `det.CalculateMoney()`.
3. `CalculateMoney()` (`InvItemPresenter.cs:737`) turns per-item values into the persisted money fields. It works out the GST rate (either a fixed GST amount converted to a percentage, or a `GSTPercent` from the `ItemRate`), then:
   - if `FeeIncludeGST` is true, `NetAmount = Fee * Qty` and GST is extracted from the total after discount (`Total / (1 + gst)`);
   - otherwise `GSTAmount = round(gst * (Fee * Qty - Discount))` and `NetAmount = Fee * Qty + GSTAmount`.
   It also sets `Rebate = RebatePerItem * Qty`. All rounding goes through `BillingCommon.Instance.RoundMoney`.

**Rounding invariant.** `BillingCommon.RoundMoney(amount)` is `Decimal.Round(amount, 2, MidpointRounding.ToEven)`, i.e. banker's rounding (`BillingCommon.cs:288`). There is a second, schedule-driven rounding path used inside the rebate calculators: `CommonMethods.RoundFeeByRoudingMethod(fee, roundMethodType)`, where `roundMethodType` comes from `FeeRate.RoundType` (default vs 5-cent). The two are different: money totals use banker's rounding, per-schedule fees use the schedule's `RoundType`. Do not conflate them.

**Computed money fields** live on the `InvItem` entity (`SourceCode/Abaki.Data/Domains/Entities_Billing/InvItem.cs`): `Total = NetAmount - Discount` (`InvItem.cs:92`), `Gap = Total - Rebate` (`InvItem.cs:87`), and `Balance` derives from paid amounts. These are read-only getters, so they are always consistent with `NetAmount`, `Discount` and `Rebate`; never try to set them directly.

### 7.5 Rebate calculators: bulk bill vs private

`BillingCommon.GetRebateCalculator` returns one of two singletons based on the account/schedule type (`BillingCommon.cs:145`). Both implement `IRebateCalculator` (`IRebateCalculator.cs:17`).

**`BulkBill_RebateCalculator`** (`IRebateCalculator.cs:24`): the fee is the MBS schedule fee, and `RebatePerItem = Fee` (100 percent rebate, no gap). If the matched item is not found it zeroes everything. GST is normally zero for bulk bill.

**`Private_RebateCalculator`** (`IRebateCalculator.cs:104`): the fee is the selected schedule's fee (rounded by the schedule's `RoundType`), and the rebate is the bulk-bill-equivalent fee. The rebate lookup depends on service type and location:
- Service type `O` (GP) tries `BBGP`, then falls back to `BBO` (rooms) or `BBI` (hospital, `locationId == "H"`).
- Service types `S`/`P` (specialist / pathology) try `BBO` (rooms) or `BBI` (hospital) first, then fall back to `BBGP`.
Gap is then `Fee - Rebate`, surfaced through the entity's `Gap` getter.

The key predicate is `Private_RebateCalculator.IsPrivateSchedule(feeRateCode)` (`IRebateCalculator.cs:111`). It returns **false** for a hardcoded list of known non-private codes (BBI, BBO, BBGP, VAGPLMO, RMFSI, RMFSO, VADI, VAP, VAA, MBS100, WCVIC/NSW/SA/QLD, TACVIC, IMMU, GMH, BUD, FIT, FHI, RAC, HBF, MPL, AHM, SLM) and also false if the code is a registered participant id (`DapperContext.Instance.CheckExistParticipantId`). Everything else is treated as a private schedule and gets the bulk-bill fallback rebate lookup. If you add a new well-known schedule code, this list is the place that decides whether it is treated as private.

### 7.6 Derived-item fees

Derived items are items whose fee depends on other items on the same invoice (assistance at anaesthesia, multiple-procedure percentages, time-based items, etc.). There are two configuration sources:

- **`MbsDerivedItemInfo`** for MBS schedule derived items, cached in `DerivedFeeCalculator.AllDerivedItemsCache` (keyed by item number, populated by `InitAllDerivedItemsCache` at `DerivedFeeCalculator.cs:36`).
- **`OtherDerivedItemInfo`** for per-fee-schedule derived items (keyed by `ItemGUID` plus `FeeRateCode`), which supports range syntax in its associated-items list.

**Dispatch.** `InvoicePresenter.CalculateDerivedItem(item)` (`InvoicePresenter.cs:1349`) flags the item as an MBS derived item, builds the item-number and amount lists for the invoice, resolves the bulk-bill fee rate, then calls `InvoicePresenter.DoCalculateDerive(...)` (`InvoicePresenter.cs:1440`). `DoCalculateDerive` is a `switch` on `MbsDerivedItemInfo.CalculatedTypeEnum` (`eMbsDerivedItemCalculatedType`) that routes to one static strategy on `DerivedFeeCalculator`:

| Strategy enum | Method | Formula in brief |
|---|---|---|
| PercentageFromAssociatedItems | `CalculatePercentageFromAssociatedItems` (`DerivedFeeCalculator.cs:80`) | `Fee = Percentage x AssociatedItemFee x (MedicalGapPercent/100)` |
| ProcedureDiscontinued | `CalculateProcedureDiscontinued` (`:147`) | reduced fee based on remaining items |
| ExcisionMalignantTumour | `CalculateExcisionMalignantTumour` (`:174`) | percentage of associated item fee |
| AssistanceAnaesthesia | `CalculateAssistanceAnaesthesia` (`:238`) | percentage / gap-adjusted |
| NumberOfPatientsSeen | `CalculateNumberOfPatientsSeen` (`:345`) | base fee plus per-patient adjustment |
| BasicUnits | `CalculateBasicUnits` (`:593`) | units x configured fee |
| CombinationOperations | `CalculateCombinationOperations` (`:641`) | percentage of invoice total excluding derived items |
| FieldQuantity | `CalculateFieldQuantity` (`:709`) | `BaseFee + ((FieldQuantity - Limit) x OverLimitQuantityPlus)` |
| TimeDuration | `CalculateTimeDuration` (`:840`) | time-based over-limit plus |
| AdditionalFoetusTested / OriginalAmputationFee | `CalculateAdditionalFoetusTested` (`:925`), `CalculateOriginalAmputationFee` (`:930`) | special-case handlers |

`NumberOfPatientsSeen` is the most elaborate. It has three sub-paths in `CalculateNumberOfPatientsSeen`: (A) use the pre-configured `CalculateFromFee` as the base, (B) use pre-calculated `DerivedItemRateCalculated` rows from the database via `TryGetPreCalculatedFee` (`:493`), or (C) look up the associated item base fee and apply patient-count and gap-percent adjustments through `ApplyPatientCountAndGap` (`:474`). The gap percentage is chosen by `FeeRateInfo.IsBulkBill` (`:419` to `:439`).

Non-MBS (health-fund) derived items use `CheckOtherDerivedFeeNumberOfPatientsSeen` (`DerivedFeeCalculator.cs:541`). It only runs for non-private schedules with a positive patient count, looks up the associated item fee from the schedule's `ItemRates`, then falls back to any `RMFS*` fee rate, then to the MBS base fee via `CacheCommonData.GetBaseItemFeeFromMbsItem` if the fee is still zero. Important nuance: this non-MBS path does **not** multiply by `MedicalGapPercent`, because health-fund schedules already inherit fees adjusted for gap from their parent (see 7.8).

**Pre-calculated override.** `DerivedItemRateCalculated` rows keyed by `OrderNum` can override the formula for specific patient counts / field quantities. Several strategies check these first and only fall back to the formula if no active (non-deleted) pre-calculated row matches.

Gotcha: `AllDerivedItemsCache` is a process-wide static dictionary populated once. If the derived-item configuration changes in the database mid-session, the cache is stale until re-initialised.

### 7.7 Fee-rate cache: `CacheCommonData`

`CacheCommonData` (static, `SourceCode/Abaki.Data/Cache/CacheCommonData.cs`) is the single choke point for reading fee data:

- `GetFeeRates()` (`:436`) returns all active `FeeRate` entities, explicitly excluding deleted rows and any code containing `"SYSDRVI+"` (system-derived internal schedules). The list is cached in `_feeRates`.
- `GetMbsItemMatchedItemCode(feeRateGuid, date, itemCode)` (`:302`) is the per-item lookup. It calls the EF stored proc `GetOneBillingItemBaseOnItemCode` and returns the first match; on any exception it logs and returns null (so calculators must tolerate a null item, which they do).
- `GetFeeRateDetails(feeRateGuid, serviceDate, itemCode)` (`:330`) returns a lightweight `ItemRate` carrying `MedicalGapPercent`, `OverLimitQuantityPlus` and `PercentageFromAssociatedItemFee`, via the stored proc `GetItemRateByItemCodeAndFeeRate`.
- `GetBaseItemFeeFromMbsItem(itemCode)` (`:316`) reads `MBSItem1.ScheduleFee` as a last-resort base fee.

Gotcha: because these swallow exceptions and return null / zero, a broken stored proc or a schema drift will silently produce zero fees rather than an error. If fees come out as zero unexpectedly, check the stored procs and the error log rather than the calculators.

### 7.8 Health-fund parent-child inheritance and the import tool

Health funds do not each carry their own hand-entered fees. They inherit from a parent schedule (for example HBF inherits from AHSA), with the parent code stored in `FeeRate.FeeTable`.

The import lives in `SourceCode/BillingItemManagement/frmUpdateBillingItems3.cs`. It downloads a zipped XML fee file from a public URL defined at `frmUpdateBillingItems3.cs:70` (saved locally under the name at `:59`), parses schedule definitions including parent-child relationships (children are listed pipe-delimited in a `Children` element, split at `:381`), and:

- for **parent** schedules, creates `ItemRates` carrying `Fee`, `GST` and `MedicalGapPercent` from the XML;
- for **child** health funds, copies every parent `ItemRate` with a bulk `INSERT INTO ItemRates (...) SELECT ... FROM ItemRates WHERE FeeRateGUID = '{parentGuid}' AND DeletedDate IS null` (`frmUpdateBillingItems3.cs:934` to `:935`). This copies `Fee`, `GST` and `MedicalGapPercent` identically, which is why a child fund's fees match its parent unless later overridden.

When a schedule's parent changes, `ResolveFeeRateConflict` handles the reconciliation. It deletes conflicting `DerivedItemRateCalculated` rows (the delete starts at `:799`), re-copies item rates, and updates the fee-related columns. The `FeeRateType` byte (`eFeeRateType.Update` / `Confict` / others) plus `FeeTableSavedInDb` vs `FeeTable` are how the tool decides whether a schedule is a plain update or a parent-conflict needing a re-copy (`:371`, `:599`, `:638`).

Gotcha: the child copy is a raw SQL `INSERT ... SELECT`, not an EF operation, so it bypasses change tracking and any entity-level validation. After running the import, the in-memory `CacheCommonData._feeRates` and `DerivedFeeCalculator.AllDerivedItemsCache` are stale until reloaded.

### 7.9 MMM (Modified Monash Model) incentives

Bulk-bill and DVA invoices can auto-attach government incentive items based on the provider's location remoteness. The logic is in `InvoicePresenter.GetIncentiveCodeFromMonashModifiedModel(itemNum)` (`InvoicePresenter.cs:2723`), called from the incentive-adding routine around `InvoicePresenter.cs:2686`.

Flow:
1. The provider's business address must have State, Postcode and Suburb; otherwise the user is warned and no incentive is added (`:2731`).
2. `CommonMethods.GetModifiedMonashModelItems` reads `MMMClassification.csv` from the application directory (`Path.Combine(Environment.CurrentDirectory, "MMMClassification.csv")`, `:2739`) and matches on State + Postcode + Suburb (case-insensitive).
3. The matched row's `MMMClassification` (1 to 7 in the data, where 1 is metropolitan and higher is more remote) plus the provider's doctor group (GP vs specialist) select an incentive item code. The eligible source item codes are grouped in the `gpcol1/2/3` and `spcol1/2/3` lists (`:2713` to `:2721`), with `MMM1Exception` excluding certain specialist codes at MMM1. Incentive codes returned include 10990, 75870, 75880 and others depending on classification.
4. DVA has its own branch that uses `10990` / `10991` based on `IsEligibleArea` and treatment location (`:2689` to `:2698`).

Incentives are only added once per source item (`IsItemIncentiveAdded` guard) and only for items not already in `ConstantValues.IncentiveServices`. Gotchas: the CSV is read from the working directory every call (no caching in this method), and a suburb/postcode/state mismatch silently yields no incentive.

### 7.10 Invoice header claiming checkboxes: `InvoiceHeaderCtrl`

The "Claiming" group on `InvoiceHeaderCtrl` (`SourceCode/Abaki.Billing.Gui B1/InvoiceHeaderCtrl.cs`) hosts the tri-state indicator checkboxes that feed the Medicare/DVA/health-fund claim payloads. Their enable/disable and visibility logic is in `UpdateControl()` (around `:604` to `:689`), all gated on `DataSource.AccountTypeGUID`:

- `chkSubmission` (Online Submission Authorised, bound to `Invoice.ClaimSubmissionAuthorised`) is enabled for all account types except WorkCover, TAC and Other (`:629`).
- `chkFinancial` (Financial Interest Disclosed, bound to `Invoice.FinancialInterestDisclosureInd`) is enabled only for Medicare/IMC/OVS style types (disabled for bulk bill, DVA, private, WorkCover, TAC, Other) (`:640`).
- `chkCompensation` (Compensation Claim) is enabled only for IMC and OVS (`:668`).
- `chkSubmissionAuthority` (Submission Authority, PRACNET-3006, bound to `Invoice.SubmissionAuthorityInd`) is visible only for bulk bill and only on or after `DB4ReportRouter.NewDB4FormsActivationDate` (`:638`). This confirms the signed paper DB4 form has been received.
- `chkAssignmentAuthorised` (Assignment Authorised Requested, PRACNET-3010, bound to `Invoice.BenefitAssignmentAuthorisedInd`) is visible only for IMC and only when the Medicare V2 activation date has passed (`:681`). Unticked means "I" (Implied), ticked means "R" (Requested), mapped by `BenefitAssignmentAuthorisedIndMapper`. Because the underlying field is a string rather than a bool, it uses manual `CheckedChanged` wiring rather than data binding, and `UpdateControl` deliberately unsubscribes the handler before setting `.Checked` to avoid dirtying the EF entity (`:671` to `:686`).

`RepositionClaimingCheckboxes()` (`:691`) re-flows the visible checkboxes vertically so hidden ones do not leave gaps. Gotcha: the manual subscribe/unsubscribe pattern on `chkAssignmentAuthorised` exists specifically to prevent a round-trip (I to I) marking the entity dirty and a handler leak when switching account types; preserve it if you touch this control.

### 7.11 Invoice item grid: `InvItemGridView`

`InvItemGridView` (`SourceCode/Abaki.Billing.Gui B1/InvItemGridView.cs`) is the Infragistics UltraGrid bound to the list of `InvItemPresenter`s. Notable behaviour:

- Permanent columns are ItemCode, Fee, Description and FeeRateCode (`:132`); Fee is editable, and Rebate/Gap/GST/GSTAmount/Discount/Total are derived.
- The FeeRateCode column is a dropdown populated from `CacheCommonData.GetFeeRates()` with autocomplete (`AutoCompleteMode.SuggestAppend`, `AutoSuggestFilterMode.Contains`, `:215` to `:218`).
- Changing FeeRateCode triggers `SyncFeeWithSchedule` across items (`:342` to `:363`), and it does so for every item so the whole invoice re-prices to the new schedule.
- For `AccountType_NoCharge`, fee editing is suppressed (`UpdateNoChargeColumn`, and the guards at `:181`, `:324`, `:346`, `:362` skip `SyncFeeWithSchedule`).
- The NIB health fund has a special branch (`:1219`) that routes through `InvoicePresenter.CalculateNIB` / `CalculateNIBFee` (`InvoicePresenter.cs:1377` to `:1411`), which applies a percentage-of-MBS calculation.

### 7.12 Fee-related tables (high level)

- **FeeRates** define a schedule: `Code`, `ParticipantId` (health-fund id used by `GetDefaultFeeRate`), `IsHealthFund`, `RoundType` (feeds `RoundFeeByRoudingMethod`), and `FeeTable` (the parent schedule code for inheritance).
- **ItemRates** hold per-item fees within a schedule: `FeeRateGUID`, `ItemGUID`, `Fee`, `GST`, `GSTPercent`, `IncludeGST`, `MedicalGapPercent`, `PercentageFromAssociatedItemFee`, `OverLimitQuantityPlus`, and effective-date range (`FirstEffectiveDate`, `LastEffectiveDate` with null meaning current). Child collection `DerivedItemRateCalculateds` holds pre-computed derived fees.
- **InvoiceItems** persist the line items: `Fee`, `Rebate`, `GSTAmount`, `Discount`, `NetAmount`, plus `FeeRateGUID`, `ItemNum`, `MbsItemCode`, `ServiceDate`, `Quantity`. `Total`, `Gap` and `Balance` are computed getters on the entity.
- **AccountTypeFeeRateMappings** map an account type to a fee schedule by `ServiceType` (O/S/P/D), `State`, `FundId` and `HasFacilityId`, and are the fifth-precedence source in `GetDefaultFeeRate`.

### 7.13 Where new work usually lands, and the traps

- Adding or changing a **fee calculation** almost always means `IRebateCalculator` (per-item fee/rebate) or `DerivedFeeCalculator` (dependent items). Prefer extracting a pure static helper and unit-testing it in `Test.Abaki.Billing.Dal`, per the repo's unit-test rule, because the presenters are tightly coupled to the EF context.
- **Rounding mistakes** are the most common billing bug. Money totals must go through `BillingCommon.RoundMoney` (banker's rounding); schedule fees use `CommonMethods.RoundFeeByRoudingMethod` with the schedule's `RoundType`. Using the wrong one produces one-cent drift that only shows up on reconciliation.
- **Null tolerance.** `CacheCommonData` returns null / zero on failure and the calculators branch on that. When you extend a calculator, keep the null-item branch; do not dereference the matched item unconditionally.
- **Stale caches.** `CacheCommonData._feeRates` and `DerivedFeeCalculator.AllDerivedItemsCache` are process statics. After the import tool runs, or after any direct SQL change to fee data, they are stale for the rest of the session.
- **Account-type GUIDs are the master switch.** Almost every branch in this subsystem keys off `ConstantValues.AccountType_*`. When adding a new account type or claiming path, expect to touch `GetDefaultFeeRate`, `InvoiceHeaderCtrl.UpdateControl`, `BillingCommon.CheckOverpayment` and the grid's per-account column logic.
- **Header checkbox bindings differ.** Bool checkboxes use `DataBindings`; the string-backed `chkAssignmentAuthorised` uses manual `CheckedChanged` wiring with deliberate unsubscribe-before-set. Follow the existing pattern to avoid dirtying entities or leaking handlers.

---

## 8. Medicare Online claiming (BBSW, IMC, DVA, OVS, OEC) and PRODA

This section covers how Pracnet submits claims to Services Australia (Medicare) over the modern REST/JSON Medicare Claiming (MCOL) web services, how it authenticates through PRODA, how the production and test environments are switched, how endpoint versioning and the 01/07/2026 V1 to V2 cutover work, and the claim-status conventions that keep the six transmit paths from silently losing status. Everything here is grounded in the actual code. Read this alongside the two rule files that codify the conventions: `.claude/rules/claim-transmit-conventions.md` and `.claude/rules/code-review-spec-compliance.md`.

### 8.1 The big picture

There are three cooperating assemblies:

| Assembly / project | Role |
|---|---|
| `SourceCode/ProdaAuthentication` (assembly `ProdaAuthentication`) | PRODA device activation, RSA key management, and PRODA access-token acquisition. Knows nothing about claims. |
| `SourceCode/PrimaryClinic.MedicareOnline` | The low-level "processor" layer. One processor class per Medicare operation. Builds the JSON request, adds the `dhs-*` and `X-IBM-Client-Id` headers, POSTs, and deserialises the response. Contains the endpoint routing tables and the V1/V2 cutover gate. |
| `SourceCode/Abaki.Pracnet.OnlineClaiming` | The orchestration layer. `OnlineClaimingHelper.*` (a set of `partial class` files, one per claim family) turns Pracnet domain objects (invoices, claim entities) into processor requests, calls the processor, and writes results back to the entities. Also home to `MedicareOnlineClaimingHelper` (the token/config singleton). |

Above those sits the Billing GUI service layer, `SourceCode/Abaki.Billing.Gui B1/BillingService_*.cs`, which is what the WinForms claim-management screens actually call. `BillingService` is one big `static partial class` split across files by claim family:

| Claim family | GUI service file | Primary entry point(s) | Orchestration helper |
|---|---|---|---|
| BBSW / Bulk Bill | `BillingService_MedicareOnline.cs` | `MakeBulkBillClaim` | `OnlineClaimingHelper.SendBulkBillClaim` (`OnlineClaimingHelperBulkBill.cs`) |
| DVA | `BillingService_MedicareOnline.cs` | `MakeDVAClaim` | `OnlineClaimingHelperDVA.cs` |
| DVA Allied Health | `BillingService_MedicareOnline.cs` | `MakeDvaAlliedClaim` | `OnlineClaimingHelperDvaAlliedHealth.cs` |
| IMC (In-Patient Medical Claim) | `BillingService_IMC.cs` | `SendImcClaims2` (current), `SendImcClaims` (legacy) | `OnlineClaimingHelperIMC.cs` |
| OVS (Overseas) | `BillingService_OVS.cs` | `SendOvsClaims2` (current), `SendOvsClaims` (legacy) | `OnlineClaimingHelperOVS.cs` |
| OEC (Online Eligibility Check) | `BillingService_OEC.cs` | `SendOECClaims` | `PerformOnlineEligibilityCheck` via `OnlineEligibilityProcessor` |

All six share the same skeleton, and understanding that skeleton once is enough to read any of them.

### 8.2 The common transmit shape

Every transmit path is a loop over claims of the same family, and each iteration performs the same five steps:

1. Build the config. `MedicareOnlineClaimingHelper.Instance.GetMedicareOnlineConfiguration()` returns a `MedicareOnlineConfiguration` carrying the base URL, `LocationId`/`MinorId`, `ProductId` (the `dhs-productid`), `MedicareOnlineClientId` (the `X-IBM-Client-Id`), and a fresh PRODA `Token`. See `SourceCode/Abaki.Pracnet.OnlineClaiming/MedicareOnlineClaimingHelper.cs`.
2. OPV pre-check. Before transmit, the linked patients are run through `BillingService.CheckOpvForPatient` (`BillingService_MedicareOnlineHelper.cs:46`). This is a hard invariant, see section 8.6.
3. CreateClaimObject / GenerateClaimRequest. The orchestration helper maps invoices to a request model. This can return null (unbuildable payload), so callers must null-guard before dereferencing (see `OnlineClaimingHelperBulkBill.cs:87`).
4. Transmit. The processor's `Transmit(request)` (which is just `ToMedicareWebService`, `MedicareClaimProcessor.cs:40`) validates via DataAnnotations, JSON-serialises with `NullValueHandling.Ignore`, adds headers, and POSTs through `ProdaHttpClient`. The response is parsed by `HandleResponse` (HTTP 200) or `HandleErrorResponse` (everything else) in `MedicareOnlineBaseService.cs`.
5. Response handling. On HTTP 200 the claim and its invoices are marked transmitted and IDs are recorded; otherwise error objects are collected and the claim status is handled per the family's convention (see section 8.7).

The processor hierarchy is: `MedicareOnlineBaseService<TRequest,TResponse>` (headers, transaction-id generation, response parsing) to `MedicareClaimProcessor<TRequest,TResponse>` (validate, serialise, POST) to the concrete processors (`BulkBillClaimProcessor`, `InPatientMedicalClaimProcessor`, `OVSClaimProcessor`, `OnlineEligibilityProcessor`, plus the report and verification processors). `AddHeaders` is overridden at each level and chains to `base.AddHeaders` so the common `dhs-productid` / `X-IBM-Client-Id` / `dhs-messageid` block is always emitted (`MedicareOnlineBaseService.cs:38`).

### 8.3 PRODA authentication

Medicare MCOL is protected by a PRODA (Provider Digital Access) bearer token. Pracnet acts as a PRODA "device" registered against the practice's PRODA organisation (identified by an RA number).

Key files, all under `SourceCode/ProdaAuthentication`:

- `ProdaAuthenticationService.cs` — device activation, key refresh, and token acquisition.
- `Helper/ProdaTokenHelper.cs` — builds the signed JWT assertion.
- `Helper/RsaKeyHelper.cs` — RSA keypair creation and JWK export.
- `Helper/ProdaHttpClient.cs` — the raw HTTP client used for PRODA and, reused, for Medicare.

The token grant is the OAuth 2.0 `jwt-bearer` grant. `ProdaTokenHelper.CreateJwtAssertion` (`ProdaTokenHelper.cs:14`) builds a JWT whose claims are: `sub` = device name, `iss` = the PRODA organisation RA number, `aud` = `http://proda.humanservices.gov.au`, `token.aud` = the target relying party (for Medicare this is `https://medicareaustralia.gov.au/MCOL`), plus `exp`/`iat`. The JWT is RS256-signed with the device's RSA private key using the `Jose.JWT` library, and a `kid` header carries the device name so PRODA can find the matching public key.

The token request body (`ProdaAuthenticationService.BuildAccessTokenRequest`, line 218) is:

```
client_id=<ProdaClientId>&grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion=<signed JWT>
```

There is deliberately no client secret anywhere in this flow. PRODA authenticates the caller purely by the RSA signature on the assertion (proof of possession of the device private key). The RSA keypair itself is generated on the machine and the private key is stored in the settings (see `DeviceActivationKeyPair` in section 8.5); only the public JWK is ever sent to PRODA during activation/refresh.

Token audience gotcha: `GetAccessToken` has a default `tokenAudience` argument, but the Medicare caller passes `"https://medicareaustralia.gov.au/MCOL"` explicitly and the code comment (`MedicareOnlineClaimingHelper.cs:66`) warns you never to rely on the default. Omitting the Medicare audience yields a generic PRODA token that Medicare rejects with HTTP 401 code 18 ("user is not authorised for the relying party").

Token caching: `MedicareOnlineClaimingHelper.GetProdaToken` (line 59) reuses the cached `AccessToken` while it is still valid, and only requests a new one when expired or when `forceRequestNewToken` is set. When a new token is issued, its expiry is stored at 80 percent of the reported lifetime (`PercentageExpiredTime = 0.8`, line 22) to avoid using an about-to-expire token. A failed token request throws rather than persisting a broken token (line 87).

The critical distinction to internalise:

- ProdaClientId — the OAuth `client_id` sent to the PRODA token endpoint. Read from `MedicareOnlineSetting.Instance.ProdaClientId`, which resolves to `ProductionProdaClientId` or `TestProdaClientId` depending on the environment switch. Supplied to the token flow via `ProdaConfigurationService.GetClientId()` (`SourceCode/Abaki.Pracnet.OnlineClaiming/ProdaConfigurationService.cs:36`). This authenticates to PRODA.
- X-IBM-Client-Id (`MedicareOnlineClientId`) — a completely separate identifier sent on every Medicare claim/verification request as the `X-IBM-Client-Id` header (`MedicareOnlineBaseService.cs:42`). It identifies the software product's subscription in the Services Australia IBM API Connect gateway. Read from `MedicareOnlineSetting.Instance.MedicareClientId`, which resolves to `ProductionMedicareOnlineClientId` or `TestMedicareOnlineClientId`. This authorises the product against the Medicare API subscription. Getting this wrong is exactly the PRACNET-3005 incident (section 8.8).

Device activation and key refresh (`ActivateDevice`, `RefreshKey` in `ProdaAuthenticationService.cs`) are driven from the Medicare Online settings screen (`Abaki.Billing.Gui B1/frmMedicareOnlineSetting2.cs`) using the device name, RA number, and a one-time activation code (OTAC) issued in PRODA. Device and key expiry dates are tracked in settings, and `NotificationDeiveExpiryCtrl.cs` warns before expiry (default 14 days, `NotifyBeforeXDaysDeviceExpiry`).

### 8.4 Endpoint versioning and the 01/07/2026 V1 to V2 cutover

Each Medicare operation is addressed by a URL path plus a `dhs-restoperationname` header carrying a versioned "operation name". These live as constants in `SourceCode/PrimaryClinic.MedicareOnline/Models/StringConstants.cs`. The version suffix convention:

- URL ends in `/v1` or `/v2`.
- Operation name ends in `@1` or `@2`.

Not every service moves to V2 at the same time. Per the Health Insurance Regulations 2018, the V2 formats become mandatory on 01/07/2026. Under PRACNET-3005 to 3012 the code was written to hold V2-shaped constants but downgrade them to V1 before the cutover date, so Pracnet keeps hitting the live V1 endpoints during the transition window.

The single source of truth for the date is `DB4ReportRouter.NewDB4FormsActivationDate = new DateTime(2026, 7, 1)` (`SourceCode/Abaki.Common.Helpers/DB4ReportRouter.cs:31`). The cutover logic is centralised in `MedicareApiVersion` (`SourceCode/PrimaryClinic.MedicareOnline/Services/MedicareApiVersion.cs`):

- `MedicareApiVersion.IsV2Active(todayOverride)` — true on/after the activation date. Use this to decide whether to emit V2-only payload fields.
- `MedicareApiVersion.MaybeDowngradeToV1(token, todayOverride)` — pre-cutover, rewrites a trailing `/v2` to `/v1` and `@2` to `@1`; on/after the date it returns the token unchanged; unmatched tokens pass through untouched.

Which services are V2-gated:

- IMC — `IMCClaimRoutes.GetApiUrl`/`GetOperationName` (`SourceCode/PrimaryClinic.MedicareOnline/Services/IMCClaimRoutes.cs`) resolve one of 15 `(claimType, serviceType)` combinations to a `...\/v2` URL and `...@2` operation name, then pass them through `MaybeDowngradeToV1`. Claim types are AG, SC, MO, MB, PC crossed with service types General/Pathology/Specialist. The route table is deliberately extracted from the HTTP processor so it can be unit-tested (`Test.Abaki.Billing.Dal.TestIMCClaimRoutes`).
- OEC — `OnlineEligibilityProcessor` (`OnlineEligibilityProcessor.cs`) selects one of the four `mcp/onlineeligibilitycheck/.../v2` URLs (OEC, ECM, ECF, ECO) and the matching `@2` operation name, then downgrades both.

Which services stay V1 (their constants already end `/v1` and `@1`, so `MaybeDowngradeToV1` is a no-op for them):

- BBSW / Bulk Bill claim submission.
- Patient verification (OPV/PVM/PVF, `mcp/patientverification/.../v1`, `OPVVerification.cs`) — this is what the OPV pre-check calls, so the pre-check keeps working on V1 even after IMC/OEC move to V2.
- DVA, DVA Allied Health, OVS, the bulk-bill/DVA report retrievals, AIR, participants, and enterprise verification.

This asymmetry (BBSW and patient verification on V1, IMC and OEC on V2) is exactly why the PRACNET-3005 incident presented the way it did: verification and bulk bill kept succeeding while IMC/OEC failed.

### 8.5 The two credentials and the production/test switch

The settings backbone is `MedicareOnlineSetting` (`SourceCode/ClinicCoreSetting/MedicareOnlineSetting.cs`, namespace `Abaki.Core.Setting`). It is an `ApplicationSettingsBase` singleton backed by the SQL `dbo.Clinic` table (parameter/value rows) via `ClinicCoreSqlSettingProvider`, and it uses `SqlDependency` to live-reload when the `Clinic` table changes. (Note: there is a commented-out legacy `MedicareOnlineSetting` under `Abaki.Common.LocalSettings` and a tiny unrelated `MedicareOnlineSettings` POCO under the MedicareOnline project — neither is the active one.)

The environment is a single string setting `MedicareOnlineEnvironment` (values `"Production"` or `"Test"`). The boolean property `IsMedicareOnlineProductionUsed` reads/writes that string. Every environment-sensitive value is a computed property that branches on it:

```
ProdaClientId       => IsMedicareOnlineProductionUsed ? ProductionProdaClientId          : TestProdaClientId
MedicareClientId    => IsMedicareOnlineProductionUsed ? ProductionMedicareOnlineClientId : TestMedicareOnlineClientId
MedicareEndpoint    => IsMedicareOnlineProductionUsed ? ProductionMedicareOnlineEndpoint : TestMedicareOnlineEndpoint
ProdaActivationEndpoint / ProdaAuthenticationEndpoint  — same pattern
```

So there are two independent credentials, each with a production and a test variant:

1. `ProductionProdaClientId` / `TestProdaClientId` — the PRODA OAuth client id (section 8.3).
2. `ProductionMedicareOnlineClientId` / `TestMedicareOnlineClientId` — the Medicare `X-IBM-Client-Id` (section 8.3).

Do not treat these as interchangeable; they are issued by different systems and validated at different points in the request lifecycle.

The environment radio buttons live on `frmMedicareOnlineSetting2.cs` (lines 132, 471). Because the values persist in the `Clinic` table, they are provisioned fleet-wide by migration scripts rather than typed in per site (section 8.8).

The composite `dhs-productid`: `MedicareOnlineClaimingHelper.GetMedicareOnlineConfiguration` builds it as `ProductId = $"{productName}{productVersion}"` (`MedicareOnlineClaimingHelper.cs:131`) where `productName` = `MedicareOnlineProductName` (default `"PrimaryClinicPractice"`) and `productVersion` = `MedicareOnlineProductVersion`. So with product version `V3.0.0` the header reads `PrimaryClinicPracticeV3.0.0`, which must match the product name registered in the Services Australia Software Developers Portal. This header identifies the software build to Services Australia; it does not select the endpoint version (that is date-gated, section 8.4).

### 8.6 The OPV pre-check invariant

Before any claim family transmits, the linked patients are verified through Online Patient Verification. The entry point is `BillingService.CheckOpvForPatient` (`SourceCode/Abaki.Billing.Gui B1/BillingService_MedicareOnlineHelper.cs:46`), which is already auto-fired pre-transmit by every service:

- BBSW: `BillingService_MedicareOnline.cs:618` (inside `MakeBulkBillClaim`).
- IMC: `BillingService_IMC.cs:446` and `:542`.
- OVS: `BillingService_OVS.cs:390` and `:486`.
- OEC: `BillingService_OEC.cs:145`.

This satisfies Services Australia BBSW spec section 2.1 (p.11), which requires `mcp-patient-verification-medicare@1` to establish eligibility before a claim is submitted. Do not add a code path that bypasses it. If the pre-check returns errors (after filtering the practice's ignore list of PV codes via `eFreeTextCode.PV_IgnoredCode`), the claim must not transmit.

OEC is the exception to "pre-transmit": per OECW section 2.1 (p.10) OEC is an advisory IFC tool, not a claim precondition. Pracnet runs OEC on demand from the Quotes screen (`frmQuoteManagement`, Open then Quotes; see `QuoteCtrl.cs:365`), not automatically before a claim transmit. `SendOECClaims` itself still runs an OPV check on the patient before performing the eligibility check.

### 8.7 Claim-status conventions and the silent-fail bug classes

These six services historically shared five "silent-fail" bug classes where a failure path forgot to update status, leaving claims stuck in an ambiguous state. The conventions are codified in `.claude/rules/claim-transmit-conventions.md`; the summary you need day to day:

Five paths that must handle status. On every one of these, the service must deal with both the claim-level status and each linked `Invoice.SentClaimStatus`:

1. OPV pre-check failure (after `CheckOpvForPatient` adds errors, before the early return).
2. HTTP transmit failure (non-OK response from Services Australia).
3. Transmit-failure invoice propagation (the caller's `else` branch after the helper returns with errors).
4. Null `requestModel` from CreateClaimObject/GenerateClaimRequest (guard before dereferencing).
5. Per-claim exception catch (handle the in-flight claim, and wrap any recovery SaveChanges in its own try/catch so a save failure does not mask the original exception).

Note the design shift for Bulk Bill: under PRACNET-3014 the BB path intentionally stays `Batched` on every failure path (OPV, daily-limit, null model, non-OK response, exceptions) rather than transitioning to `TransmitFailed`, so the user can fix the underlying detail and retransmit from the Batched tab without status churn (`OnlineClaimingHelperBulkBill.cs:125`, `BillingService_MedicareOnline.cs:637` and `:676`). DVA and DVA Allied are conformant by accident (they never wrote `TransmitFailed`); the guard is a regression test, not new code. IMC still marks `Transmitted`/failure per claim (`BillingService_IMC.cs:475`). Verify the current intent against the code and the rule file before "fixing" anything — several apparent bugs dissolve under inspection (see the "Verify, don't echo" section of the rule file).

Status cast types differ, and the compiler will not catch a wrong cast on a nullable constant:

| Entity field | Type | Cast to use |
|---|---|---|
| `BulkBillClaiming.Status` | `Nullable<Int32>` | `(int)eClaimStatus.X` |
| `InPatientMedicalClaimLevel.ClaimStatus` | `Byte` (non-null) | `(byte)eClaimStatus.X` |
| `Invoice.SentClaimStatus` | `Nullable<Byte>` | `(byte)eClaimStatus.X` |

`(int)` on a `Nullable<Byte>` compiles because of compile-time constant narrowing but breaks the instant someone swaps the literal for a variable. Always match the field's underlying type. Also note `InvoiceItem.SentClaimStatus` does not exist on the entity (only on the `InvoiceItemView` projection), so there is nothing to propagate there.

SaveChanges placement: BBSW saves per claim (prior claims persist before the next starts); IMC's `SendImcClaims2` saves once at the end of the batch (mid-batch exceptions can discard prior successes, mitigated by a recovery SaveChanges in the outer catch).

Single-writer principle: when both the helper and the caller could write the same status field, pick one. The caller usually owns the transition because it also handles invoice-level propagation. Reviewers will flag double writes.

Diagnostic logging: SIT-debugging log lines carry a `[DIAG-3009]` prefix so they can be stripped before release with `git grep '\[DIAG-3009\]'`. They log to `ClaimingErrorLogger` (writes to `Logs/ClaimingLog_*`), not `ErrorLogger`, because the claiming log is what SIT testers share. Levels: Info for happy-path markers, Warn for handled failures, Error for recovery-of-recovery failures. Include claim number, invoice count, HTTP status, and error code.

### 8.8 Real-world note: the PRACNET-3005 HTTP 401 incident

After the 01/07/2026 V2 cutover went live, In-Patient Medical Claims (and OEC) started failing at transmit with HTTP 401 "Cannot find valid subscription for the incoming API request." Bulk bill and patient verification kept working, because they stay on V1 (section 8.4) and their subscription was fine; only the V2 IMC/OEC calls failed.

Root cause: the production `X-IBM-Client-Id` (`ProductionMedicareOnlineClientId`) in use was a value whose IBM API Connect subscription only covered the V1 product. The V2 IMC/OEC operations had no valid subscription under it, so the gateway rejected them with 401 before the request ever reached the claim logic. Note this was an authorisation/subscription problem (`X-IBM-Client-Id`), not a PRODA authentication problem (`ProdaClientId`) — the token was valid, which is why the failure was specific to the V2 endpoints.

Fix: set `ProductionMedicareOnlineClientId` to the `X-IBM-Client-Id` registered for the `PrimaryClinicPracticeV3.0.0` production application (the single key that covers both the V1 endpoints and the V2 ones), and set `MedicareOnlineProductVersion` to `V3.0.0` so `dhs-productid` reads `PrimaryClinicPracticeV3.0.0`. Both values live in the `dbo.Clinic` settings table and are provisioned fleet-wide by two idempotent upsert migrations:

- `Database/PracnetDatabaseFrom0.1.6/Revision_0.1.9/UpdateScriptsForCurrentVersion/793_PRACNET3005_set_ProductionMedicareOnlineClientId_V3_0_0.sql`
- `Database/PracnetDatabaseFrom0.1.6/Revision_0.1.9/UpdateScriptsForCurrentVersion/794_PRACNET3005_set_MedicareOnlineProductVersion_V3_0_0.sql`

The actual client-id value is not reproduced here — it is a credential and it lives in those migration scripts and the `Clinic` table. Debugging tip captured in project memory: a Medicare 401 "Cannot find valid subscription" almost always means the wrong `ProductionMedicareOnlineClientId`; check it against the current Services Australia production information document before looking anywhere else, and remember it is separate from `ProdaClientId`.

### 8.9 Where the Services Australia specs and NOI test plan live

Authoritative specifications and the NOI (Notice of Integration) Web Services Test Plan are stored under the shared GlobalHealth OneDrive path referenced in the CLAUDE.md and in `.claude/rules/code-review-spec-compliance.md` (path convention: the `Medicare_update` folder under the shared PrimaryClinic source area — not reproduced here as a machine-local path). Relevant files include the BBSW, OECW, and IMCW technical specs, the Common Rules document, the Developers Guide, the DB4 Assignment of Benefit form templates, and the NOI test plan spreadsheet. PDFs can be converted with `pdftotext` and the DOCX unzipped for grepping. Any PR touching Medicare claiming payloads, printed claim forms, or their persistence must cite the relevant spec section/page and the NOI test row, and give a verdict of Compliant, Out-of-spec, or Spec-silent, as required by the spec-compliance rule. Always verify scope against the spec rather than trusting an in-repo `PRACNET-XXXX-implementation.md`, since those plans have diverged from the specs before.

### 8.10 Quick orientation for a new developer

- Start at the GUI service entry point for the family you care about (`BillingService_*.cs`), find the transmit loop, and follow it into `OnlineClaimingHelper*.cs`, then into the processor in `PrimaryClinic.MedicareOnline/Services`.
- If a claim fails at transmit with an auth/subscription error, first decide whether it is PRODA (token, `ProdaClientId`) or IBM gateway (`X-IBM-Client-Id`), then check whether the failing service is V1 or V2 and whether the date gate has flipped.
- Read `ClaimingLog_*` under `Logs/` for the request/response trace; `ProdaHttpClient.LogDetailForRequest` logs headers (with `Authorization` redacted), the request body, and the full response body at Debug level.
- Tests for the routing and version gate are in `Test.Abaki.Billing.Dal` (`TestIMCClaimRoutes`, plus DB4 split and visible-claim-status tests). Follow `.claude/rules/unit-tests.md` when you change any `.cs` here.

---

## 9. Claiming reports (DB4 and DVA forms)

This subsystem produces the printed paper claim forms that a practice hands to a patient (or the payer) as part of a Medicare Bulk Bill or DVA claim. It is distinct from the online-claiming transmit paths (BBSW, IMC, DVA, OVS, OEC): those send the claim to Services Australia over the wire, whereas the reports here are the physical assignment-of-benefit documents. The two are complementary, not alternatives. The most important one is the DB4, the Bulk Bill Assignment of Benefit form the patient signs to assign their Medicare benefit to the provider.

Everything here is built on Crystal Reports. The report layouts are `.rpt` files, each paired with a generated `.cs` strongly-typed wrapper class, all living in `SourceCode/Abaki.Billing.Report B1/`. The reports are driven off SQL views (chiefly `InvoiceReportView`), selected by invoice GUID at print time, and rendered/printed through `SourceCode/Abaki.CrystalReport/ReportUtility.cs`.

### 9.1 Where it starts: the Print button

The user-facing entry point is the Print (and Preview) button on the invoice search grid control, `SourceCode/Abaki.Billing.Gui B1/BillingInvoiceGridViewCtrl.cs`. `btPrint_Click` (BillingInvoiceGridViewCtrl.cs:264) checks the `ePracnetFeatures.Invoices` / `ePracnetPermission.Print` permission, gathers the selected invoices, and calls:

```
BillingService.PrintListInvoices(invoices, sender == btPrint, this.ParentForm);
```

The `sender == btPrint` boolean is the `toPrinter` flag: true when the real Print button was clicked (send to printer), false for Preview (show in the report viewer). `PrintListInvoices` is called from many other places too (invoice save flows, batch print, etc.); grep `PrintListInvoices` in `SourceCode/Abaki.Billing.Gui B1/BillingService.cs` returns a dozen call sites.

### 9.2 The two enums

`SourceCode/Abaki.Common.Enums/eClaimTypeReport.cs` names which report to build. Note this has grown well beyond what older documentation describes; the current members are:

- `DB4` — legacy single Bulk Bill Assignment of Benefit form (renders `rptDB42.rpt`). Still the active form until the activation date (see 9.5).
- `DB4_MBS` — post-assignment DB4 for Other MBS (non-pathology, non-DI) services. Renders `rptDB4_MBS_PostAssignment.rpt`.
- `DB4_Pathology` — post-assignment DB4 for pathology services. Renders `rptDB4_Path.rpt`.
- `DB4_DI` — post-assignment DB4 for diagnostic imaging. Renders `rptDB4_DI.rpt`. Added under PRACNET-3014 after NOI feedback that DI invoices were incorrectly printed on the Other MBS form (which lacks the DI-specific assignment statement and the "Date of Imaging Procedure" column header).
- `DVA_D1216` — DVA Dental, Optical, Psych, Allied.
- `DVA_D1216S` — DVA General, Pathology, Specialist (the catch-all for DVA).
- `DVA_D1083` — DVA Community Nursing.
- `DVA_D695` — DVA Speech Pathology.

`SourceCode/Abaki.Common.Enums/eServiceTypeCde.cs` is the single-character service-type code stored per invoice (`Invoices.ServiceTypeCde`, a `varchar(1)`). It drives the DVA splitting and, post-activation, the DB4 splitting:

| Code | Meaning |
|------|---------|
| `P` | Pathology |
| `S` | Specialist |
| `O` | General |
| `F` | DVA Community Nursing |
| `G` | DVA Dental |
| `L` | DVA Optical |
| `I` | DVA Speech Pathology |
| `J` | DVA Allied |
| `K` | DVA Psych |

There is no `D` member for Diagnostic Imaging in the enum. The DB4 router treats the literal string `"D"` defensively (see 9.5); the common DI case is instead detected via a Specialist invoice (`S`) carrying a diagnostic referral (`RequestTypeCde = "D"`).

### 9.3 ReportHelper: building the report object

`SourceCode/Abaki.Billing.Report B1/ReportHelper.cs` is the factory layer. The relevant method is `GetClaimingInvoiceReportDocument(eClaimTypeReport type, List<Guid> invoiceGUIDs, string locationID, bool toPrinter, int? inVoiceNum)` (ReportHelper.cs:39). It:

1. `switch`es on `eClaimTypeReport` to `new` up the correct `.rpt` wrapper class. The `default` branch falls through to `rptDVA_D1216S` (the DVA catch-all), which is deliberate.
2. Builds a Crystal record-selection formula that filters `InvoiceReportView` by the supplied invoice GUIDs:
   ```
   {InvoiceReportView.InvoiceGUID} IN [{guid1}, {guid2}, ...]
   ```
   Each GUID is wrapped in braces and quotes (`'{...}'`) because that is the literal string form Crystal expects for the GUID column.
3. Sets the `LocationID` report parameter from the caller (which passes `RecallGlobalSetting.Instance.LocationId`).
4. Applies the database logon via `ReportUtility.ApplyLogon(rpt)` and returns the built `ReportDocument` without printing. The caller collects these into a list and prints them together.

`GetClaimingInvoiceReportDocument` returns the report without printing; the older `PrintClaimingInvoice` (ReportHelper.cs:15) both builds and prints in one call and only knows about `DB4` versus `DVA_D1216`. New code should prefer `GetClaimingInvoiceReportDocument`. ReportHelper also hosts the many other claiming/report factory methods (private claim, IMC, OVS, OEC disclaimer, processing/payment reports, banking, etc.), each following the same "build formula from GUIDs, ApplyLogon, print" shape against its own view.

Note the current DB4 (`rptDB42`) takes three report parameters in the older `PrintClaimingInvoice` path (`LocationID`, `MedicareCardNum`, `MedicareCardRef`), whereas `GetClaimingInvoiceReportDocument` only sets `LocationID`; the Medicare card fields for the split forms come through `InvoiceReportView` columns rather than parameters.

### 9.4 InvoiceReportView: the data source

Every claiming invoice report binds to the SQL view `InvoiceReportView`. Its authoritative definition is the highest-numbered migration that (re)creates it, currently `Database/PracnetDatabaseFrom0.1.6/Revision_0.1.9/UpdateScriptsForCurrentVersion/789_alter_InvoiceReportView_AddClaimingCheckboxes.sql`. The view is redefined repeatedly across the migration set (search `CREATE VIEW [dbo].[InvoiceReportView]`); the file with the largest numeric prefix in `Revision_0.1.9/UpdateScriptsForCurrentVersion/` wins on a fresh apply, so always read that one, not an older `668_alter_InvoiceView.sql` or `03_Pracnet_Views.sql`.

The view joins `Invoices` to `InvoiceItems`, an allocation view, patient/payee/payer/provider/referral detail views, `ClaimView`, extended invoice/item tables, and `Hospitals`. Fields the DB4 and DVA forms rely on include:

- Item level: `ItemNum`, `MBSItemNum`, `ItemDescription`, `ItemLSPNum`, `ItemSCPId`, `ItemFee`, `ItemGSTAmount`, `ItemNetAmount`, `ItemRebate`, `ItemDiscount`, `ItemServiceDate`, `ItemQuantity`, `ItemNoOfPatientsSeen`.
- Patient: `MedicareNumber`, `MedicareRef`, `DvaNumber`, `PatientDOB`, `PatientSurname`, `PatientFirstName`, address and phone fields.
- Provider/Payee: `ProviderProviderNum`, `PayeeProviderNum`, `PayeeLSPNum`, `PayeeSCPId`, `ProviderLSPNum`, `ProviderSCPId`, BSB and account fields.
- Referral: `ReferralProviderNum`, `RequestTypeCde` (this is how the print path can tell a diagnostic-imaging referral apart from an ordinary specialist one).
- Routing/context: `InvoiceServiceTypeCde`, `AccountTypeGUID`, `SentClaimStatus`, `IsPrinted`, `FacilityDesc`.

PRACNET-3006 / PRACNET-3010 added three Claiming-group checkbox columns straight off `dbo.Invoices` so the DB4 variants can render a ticked/unticked glyph: `SubmissionAuthorityInd` (Bulk Bill, PRACNET-3006), `ClaimSubmissionAuthorised` (general), and `BenefitAssignmentAuthorisedInd` (IMC I/R/null, PRACNET-3010). These were additive projection changes: no existing column was renamed or dropped. After a view change like this, the `.rpt` files must be opened in the Crystal designer and re-verified (Crystal Reports -> Database -> Verify Database) so Field Explorer picks up the new columns; the migration header calls this out explicitly.

### 9.5 DB4 routing and the activation-date gate (DB4ReportRouter)

The heart of DB4 selection is `SourceCode/Abaki.Common.Helpers/DB4ReportRouter.cs`. It lives in `Abaki.Common.Helpers` (not in the Billing GUI) so both the GUI print path and the test project can reference it without a circular dependency, the same arrangement as `SubmissionAuthorityIndMapper` and the other Claiming-group mappers.

`DB4ReportRouter.SplitBulkBillByServiceType<T>(...)` is generic over the invoice type so it can be called with either the domain `Invoice` (which exposes `GUID` and a `ReferralRequest` navigation) or the `ViewInvoice` search-grid projection (which exposes `InvoiceGUID` but no referral data). Callers pass selector lambdas for the service type, the GUID, and optionally the referral `RequestTypeCde`.

The routing has two stages:

1. Date gate. `NewDB4FormsActivationDate` is hardcoded to 01/07/2026 (DB4ReportRouter.cs:31). Before that date the method returns every invoice under a single `DB4` group, so the caller renders the legacy `rptDB42.rpt`. This exists because PRACNET-3005 mandates that the new Assignment of Benefit Agreement forms must not go live in production before 01/07/2026; Services Australia accepts both V1 (old DB4) and V2 (the split forms) during the transition window, and Pracnet stays on V1 until the gate opens. There is a `todayOverride` parameter used only by tests to exercise both sides of the gate.
2. Service-type split (on/after the activation date). A single pass partitions the invoices into three buckets:
   - Pathology: `ServiceTypeCde == "P"` (compared `OrdinalIgnoreCase`, so a stray lowercase code from a manual SQL fix-up still routes correctly) -> `DB4_Pathology`.
   - Diagnostic imaging: `ServiceTypeCde == "D"` (a defensive direct match, since `D` is not in the enum) OR `ServiceTypeCde == "S"` with referral `RequestTypeCde == "D"` (the common workflow: a specialist invoice with a diagnostic referrer) -> `DB4_DI`.
   - Everything else, including null/empty/unknown service types: `DB4_MBS`. MBS is the deliberate safe default because it covers General plus Specialist and matches the pre-split behaviour of a single DB4 form.

The result is a `List<KeyValuePair<eClaimTypeReport, List<Guid>>>`, not a dictionary, specifically so iteration order is deterministic (MBS, then Pathology, then DI). That determinism matters: staff hand the printed forms to patients in the order the engine emits them, so print order needs to be stable across runs.

The DI-via-referral detection only works when the caller supplies the `getRequestTypeCde` selector. The main invoice-list print path does supply it (via the `Invoice.ReferralRequest.RequestTypeCde` navigation, or a pre-fetched dictionary lookup on the view path). Call sites that cannot reach referral data pass null and accept the limitation: a specialist-plus-diagnostic-referrer invoice then falls through to MBS, matching pre-PRACNET-3014 behaviour.

### 9.6 The split call sites in BillingService

`SourceCode/Abaki.Billing.Gui B1/BillingService.cs` contains more than one method that groups invoices by account type and fans out to `ReportHelper`. They share the same shape but operate on different invoice representations, and they are NOT all on the same generation of the routing code. Be careful which one you are editing.

- The current, fully-updated path is the public `PrintListInvoices(List<Invoice> ivs, bool toPrinter, Form parentForm)` (BillingService.cs:4336) and its `ViewInvoice`-based sibling (around BillingService.cs:4046). Both call `DB4ReportRouter.SplitBulkBillByServiceType` for Bulk Bill invoices and then loop the returned groups into `GetClaimingInvoiceReportDocument`. The `ViewInvoice` path first does one round-trip to fetch `RequestTypeCde` per invoice GUID via the `Invoices -> ReferralRequest` navigation (because the `ViewInvoice` projection does not expose it) and feeds a dictionary-backed selector to the router. This avoids an EDMX or SQL-view migration.
- There is at least one older path (around BillingService.cs:4239, over an `AgedDebtorsView`-style collection) that still calls `GetClaimingInvoiceReportDocument(eClaimTypeReport.DB4, ...)` directly for Bulk Bill, i.e. it always renders the legacy `rptDB42` and does not go through the router. If you are asked to change DB4 behaviour, confirm which method the user's screen actually invokes before assuming the router runs.

DVA splitting is done inline in each of these methods (the router covers Bulk Bill only). For a Veteran Affairs account type the invoices are partitioned by `ServiceTypeCde`:

- Group D1216: service types `G` (Dental), `L` (Optical), `J` (Allied), `K` (Psych) -> `DVA_D1216`.
- Group D1083: service type `F` (Community Nursing) -> `DVA_D1083`.
- Group D695: DVA Speech Pathology -> `DVA_D695`.
- Group D1216S: everything not already placed (General, Pathology, Specialist) -> `DVA_D1216S`, the catch-all.

A running `processedList` of already-placed GUIDs prevents an invoice being emitted on more than one form; the D1216S group is defined as "not in processedList". Gotcha worth flagging: the D695 (Speech Pathology) filter is not consistent across the copies. In the primary `Invoice`-based split it correctly filters `ServiceTypeCde == "I"` (Speech, BillingService.cs:4120), but in some of the other copies (for example BillingService.cs:4269 and BillingService.cs:4409) the D695 group filters on `eServiceTypeCde.L` (Optical) instead of `I`. Because `L` invoices are also claimed by the D1216 group that runs first, `processedList` usually swallows them and the D695 group ends up empty on those paths, but the intent is clearly wrong and any real Speech Pathology invoice on those paths will not print a D695. If you touch DVA report splitting, reconcile all copies to use `I` for D695 and add a regression test.

After building the list of `ReportDocument`s, non-claiming invoices are handled separately (`otherTypeInvs`, printed as ordinary invoice documents), a duplicate-print prompt is shown (`AskForPrintDuplicatedInvoices`), paid invoices produce receipt reports, then everything is handed to `ReportHelper.PrintListReports(...)`. Finally each report is `Close()`d and `Dispose()`d, and `InvoiceReceiptMarkPrintedDate` stamps `Invoices.PrintedDate` so the grid can show the invoice as printed.

### 9.7 Rendering and the database logon

`SourceCode/Abaki.CrystalReport/ReportUtility.cs` does the actual output. `ApplyLogon(rpt)` (ReportUtility.cs:31) walks every `Table` in the report's `Database.Tables` collection and re-points each one at the live SQL Server connection (server, database, user, password held in static fields set once via `SetConnectionInfo`), then calls `rpt.SetDatabaseLogon(...)`. This must be called on every report before printing or the report opens against whatever connection was baked into the `.rpt` at design time. The connection credentials are configuration held in these static fields; they are not stored in the report definitions and are not reproduced here.

`PrintReport(...)` has several overloads (single report, list of reports, with or without audit record GUIDs). When `toPrinter` is true it picks the printer from `LocalSetting.Instance.PracnetPrinters` (the Medicare printer for `isMedicareReport` reports, otherwise the billing printer), formats the page, and calls `rpt.PrintToPrinter(...)`, then writes a print audit log via `AbakiAuditLog.SavePrintLog`. When false it shows the report in the viewer form `SourceCode/Abaki.CrystalReport/frmReport.cs`.

### 9.8 The DB4 physical workflow

The DB4 exists because Medicare requires the patient to physically assign their benefit to the provider for a bulk-billed service. The end-to-end loop is:

1. Staff create a Bulk Bill invoice in Pracnet.
2. Staff click Print on the invoice grid. `PrintListInvoices` routes the Bulk Bill invoices through `DB4ReportRouter` and prints the appropriate DB4 variant (legacy `rptDB42` before 01/07/2026; `DB4_MBS` / `DB4_Pathology` / `DB4_DI` after).
3. The patient signs the printed form to assign the benefit.
4. Staff scan the signed form back into Pracnet through the document scanning subsystem (see the Document scanning section), where it is stored as a password-protected ZIP against the patient and linked to the claim.

The online BBSW transmit path is separate and runs its own OPV pre-check and payload build; the DB4 print here is the paper artefact, not the wire submission. The `chkSubmissionAuthority` checkbox on the invoice header (PRACNET-3006, backed by `Invoices.SubmissionAuthorityInd`, now surfaced on `InvoiceReportView`) is how staff record that the signed paper form has been received; the DB4 forms can render its state.

### 9.9 Gotchas and invariants

- Crystal Reports csproj integrity. This is the big one. The Visual Studio Crystal designer can silently drop OTHER reports' entries from `Abaki.Billing.Report.csproj` when you edit any single `.rpt` in the designer. A real incident: editing `rptDB4_MBS.rpt` removed `rptDB42`'s `<Compile>` / `<EmbeddedResource>` pairs from the csproj; the files stayed on disk but fell out of the build, producing "rptDB42 could not be found" at compile time. Each report needs both a `<Compile Include="rptXxx.cs"><DependentUpon>rptXxx.rpt</DependentUpon></Compile>` and an `<EmbeddedResource Include="rptXxx.rpt">` entry. After any `.rpt` edit, run `git diff -- "SourceCode/Abaki.Billing.Report B1/Abaki.Billing.Report.csproj"`, look for removed entries for reports you were not touching, restore them, and rebuild. The rule file is `.claude/rules/crystal-reports-csproj.md`. When last checked, all of `rptDB4`, `rptDB42`, `rptDB4_DI`, `rptDB4_MBS_PreAssignment`, `rptDB4_MBS_PostAssignment`, `rptDB4_Path` and the four DVA reports had both entries present.
- Pre- vs post-assignment MBS form. There are two MBS variants on disk, `rptDB4_MBS_PreAssignment` and `rptDB4_MBS_PostAssignment`, but `GetClaimingInvoiceReportDocument` currently always routes `DB4_MBS` to the PostAssignment layout (ReportHelper.cs:50, with a TODO to switch on a pre/post claim-status flag once the call site can distinguish them). Do not assume the pre-assignment layout is wired up.
- Activation-date behaviour is time-dependent. Because of the 01/07/2026 gate in `DB4ReportRouter`, the DB4 form a build prints changes purely with the calendar even with no code change. Tests must pin the date with `todayOverride`; do not write assertions that assume "today" is one side of the gate.
- Read the newest view migration. `InvoiceReportView` is redefined many times across the migration folders. The current shape is the highest-numbered `*_InvoiceReportView.sql` under `Revision_0.1.9/UpdateScriptsForCurrentVersion/`. Editing an older copy has no effect on a freshly-applied database.
- Multiple print methods, not all current. As noted in 9.6, at least one Bulk Bill print path still hardcodes `eClaimTypeReport.DB4` and bypasses the router. Do not assume every Print button on every screen goes through `DB4ReportRouter`.
- GUID formula formatting. The record-selection formula wraps each GUID as `'{guid}'`. If you build a formula by hand for a new report, match that exact form or Crystal will silently select zero rows.

### 9.10 Spec compliance note

Any change to the printed DB4 or DVA forms, or to `InvoiceReportView`, is in scope for the code-review spec-compliance rule (`.claude/rules/code-review-spec-compliance.md`). The reviewer must cross-check against the relevant Services Australia technical specification (BBSW for DB4, the Common Rules for shared identifiers) and the NOI Web Services Test Plan, and cite spec page numbers and NOI test-case rows. The DB4 form layouts themselves are prescribed by the Services Australia DB4 post-assignment form templates (Path / DI / Other MBS variants). This is general information only. Please confirm with the relevant team before use.

---

## 10. Tyro EFTPOS integration

Tyro is the integrated EFTPOS and Medicare/health-fund claiming terminal used in Pracnet. It covers two distinct capabilities that share one terminal:

1. Integrated EFTPOS card payments (Purchase, Refund) that settle a receipt at the counter.
2. Tyro Easyclaim and HealthPoint claiming, where the terminal transmits Bulk Bill, Private Patient (patient claim) and health-fund (HealthPoint) claims to Medicare or the fund over the terminal's own network link instead of Pracnet's Medicare Online web-service path.

Both go through the same low-level wrapper (`TyroWrapper`) which talks to the vendor SDK (`Tyro.Integ.dll`). The higher-level orchestration lives in the Billing GUI project as partial classes of `BillingService`.

### Where the code lives

| Concern | Path |
|---|---|
| Vendor SDK facade and events | `SourceCode/TyroAdapter/TyroWrapper.cs` |
| Test/manual purchase form | `SourceCode/TyroAdapter/frmPurchase.cs` |
| Transaction-result display form | `SourceCode/TyroAdapter/frmTransactionResult.cs` |
| Claim request/response DTOs (Bulk Bill) | `SourceCode/TyroAdapter/BulkBillClaim/` |
| Claim request/response DTOs (Private Patient) | `SourceCode/TyroAdapter/PrivatePatientClaim/` |
| Claim request/response DTOs (HealthPoint) | `SourceCode/TyroAdapter/HealthPoint/` |
| Shared base classes | `SourceCode/TyroAdapter/CommonBase/` |
| Payload build, deserialise, field validation | `SourceCode/TyroAdapter/CommonHelper/TyroCommonMethod.cs` |
| Constants (namespace, max vouchers/services) | `SourceCode/TyroAdapter/CommonHelper/Constant.cs` |
| Enums (transaction results, claim object typing) | `SourceCode/TyroAdapter/CommonHelper/TyroEnum.cs` |
| Claiming orchestration (BB / PP / HealthPoint) | `SourceCode/Abaki.Billing.Gui B1/BillingService_Tyro.cs` |
| Medicare-model to Tyro-model conversion | `SourceCode/Abaki.Billing.Gui B1/BillingService_MedicareOnlineHelper.cs` (three overloads of `ConvertMedicareModelToTyroModel`, from line 938) |
| EFTPOS Purchase/Refund/PayGap entry points | `SourceCode/Abaki.Billing.Gui B1/BillingService.cs` lines 4715 to 4771 |
| Receipt-screen EFTPOS wiring | `SourceCode/Abaki.Billing.Gui B1/ReceiptCtrl.cs` |
| Purchase-tile control | `SourceCode/Abaki.Billing.Gui B1/TyroPurchaseProcessCtrl.cs` |
| EFTPOS result value object | `SourceCode/Abaki.Billing.Gui B1/Tyro/TyroResult.cs` |
| Legacy/idle claiming shell | `SourceCode/Abaki.Billing.Gui B1/Tyro/TyroServices.cs` |
| Transaction browser (grids/tabs) | `SourceCode/Abaki.Billing.Gui B1/Tyro/frmTyroTransactions.cs` and siblings |
| Enums (shared) | `SourceCode/Abaki.Common.Enums/eTyroEftposTransactionType.cs`, `eTyroTransactionResult.cs`, `eTyroTransactionStatus.cs`, `eTyroEasyclaimsTransactionStatus.cs`, `eTyroTransactionType.cs` |

Namespaces: the adapter assembly is `TyroAdapter` (contains `TyroWrapper` plus the DTO subtrees under `TyroAdapter.BulkBillClaim`, `TyroAdapter.PrivatePatientClaim`, `TyroAdapter.HealthPoint`, `TyroAdapter.CommonBase`, `TyroAdapter.CommonHelper`, `TyroAdapter.CommonObject`, `TyroAdapter.Contracts`). The orchestration is `Abaki.Billing.Gui` (the `BillingService_Tyro.cs` partial) and `Abaki.Billing.Gui.Tyro` (`TyroServices`, `TyroResult`).

### The vendor SDK facade: TyroWrapper

`TyroWrapper` (`SourceCode/TyroAdapter/TyroWrapper.cs`) is the only class that references the Tyro SDK types (`Tyro.Integ.TerminalAdapter`, `TerminalAdapterSync`, `TerminalAdapterEasyclaim`, `TerminalAdapterHealthpoint`, and the domain types `Transaction`, `Receipt`, `Error`). It is a static facade with lazily-created singleton adapters:

- `Adapter` (`TerminalAdapter`) drives asynchronous EFTPOS Purchase and Refund. It exposes three static events that callers subscribe to: `OnTransactionCompleted`, `OnReceiptReturned` and `OnErrorOccured`. These are the callback surface for the async flow.
- `AdapterSync` (`TerminalAdapterSync`) drives the synchronous variants (`PurchaseSync`).
- `EasyClaimAdapter` (`TerminalAdapterEasyclaim`) drives Bulk Bill and Private Patient claims. Note that each `Send*Claim*` call re-creates `_easyClaimAdapter` before use (`TyroWrapper.cs` lines 260 to 296), so the easyclaim adapter is effectively per-call rather than a true singleton.
- `HealthPointAdapter` (`TerminalAdapterHealthpoint`) drives HealthPoint claims, cancellation and pay-gap.

`PosInfo` is a `POSInformation("Abaki", "Pracnet", "1.0")` handed to every adapter to identify the POS product to the terminal.

The terminal identity is not held in the database. `TyroWrapper.TerminalID` reads the registry key `HKEY_CURRENT_USER\Software\Tyro\Terminal.ID` (see `TyroWrapper.cs` line 91). The merchant id (MID) comes from the selected bank account (`TyroPurchaseDto.BankAccount.MerchantID`), not the registry.

Key method groups:

- Async EFTPOS: `Purchase`, `PurchaseMidTid`, `Refund`, `RefundMidTid`, `PurchaseAdditionalData`, `PurchaseMidTidAdditionalData`. The `*MidTid` variants target a specific merchant and terminal; the plain variants use the terminal's default.
- Sync EFTPOS: `PurchaseSync` (returns an `ITransactionResult`). `RefundSync` exists but its body is commented out and it currently returns null (`TyroWrapper.cs` lines 227 to 245); do not rely on it.
- Easyclaim: `SendBulkBillClaim` / `SendBulkBillClaimMidTid` (the `rightAssigned` boolean is passed straight through to the SDK), `SendPrivatePatientClaim` / `SendPrivatePatientClaimMidTid` (dispatches to `FullyPaidClaim` vs `PartPaidClaim` on an `isFullyPaid` flag).
- HealthPoint: `SendHealthPointClaim`, `CancelHealthPointClaim`, and the co-payment helpers `HealthPointClaimPayGap` / `HealthPointClaimPayGapMidTid` (these send a normal EFTPOS purchase but attach the healthpoint transaction id as extra JSON data so the gap payment links to the claim).
- Helpers: `DeserializeResult`, `DisplayEasyClaimResult`, `DisplayTransaction`, `DisplayError`, `ShowConfigDialog` (opens the SDK `PreferencesForm`), and `CheckTyroIntegrated`.

`CheckTyroIntegrated` (line 426) is the guard that decides whether Tyro is even usable on this workstation. It reflection-loads the referenced assemblies of `PreferencesForm` and `TerminalAdapter`; if any fail to load (SDK not deployed) it returns false and caches the result. `ReceiptCtrl` combines this with the practice setting: `_isTyroIntegrated = TyroWrapper.CheckTyroIntegrated() && MedicareOnlineSetting.Instance.TyroUsed` (`ReceiptCtrl.cs` line 228). So both the SDK presence and the `TyroUsed` flag must be true for any Tyro UI to appear.

Payloads: the `#region Sample payload` block at the top of `TyroWrapper.cs` and the `TestBulkBillClaim` / `TestPartPaid` / `TestFullyPaid` methods are hard-coded sample claims for manual terminal testing (they contain fake Medicare and provider numbers). They are not part of the production path and are only reachable from the debug buttons on `frmPurchase`.

### EFTPOS payment flow (Purchase / Refund at the receipt screen)

This is the counter-payment path, driven from the receipt window.

1. When a receipt is opened, `ReceiptCtrl` checks `_isTyroIntegrated`. If true it calls `AttachTyroTransactionEvents()` which subscribes to `TyroWrapper.OnTransactionCompleted` and `TyroWrapper.OnErrorOccured` (`ReceiptCtrl.cs` lines 262 to 271). It always detaches on close via `DetachTyroTransactionEvents()`, which matters because the wrapper events are static (see gotchas).
2. The operator allocates payment lines and selects a bank account (which carries the MID). The purchase tiles are held in `_purchaseAllocList` (a list of `TyroPurchaseDto`) and rendered by `TyroPurchaseProcessCtrl` (embedding `TyroPurchaseGridViewCtrl`).
3. Pressing Process on `TyroPurchaseProcessCtrl` (`btnProcess_Click`) dispatches on `EftposTransactionType` (`Purchase`, `Refund`, or `HealthPointPayGap`) to `BillingService.TyroPurchaseProcess`, `BillingService.TyroRefundProcess`, or `BillingService.TyroHealthPointPayGapProcess` (`BillingService.cs` lines 4715 to 4771). Amounts are converted to integer cents via `amount.ConvertToCents()` / `(int)(amount * 100)`. If a MID is present these call the `*MidTid` wrapper methods; otherwise the plain default-terminal methods.
4. The terminal processes the card asynchronously. On completion the SDK raises `TerminalAdapter.TransactionCompleted`, which `TyroWrapper` forwards to `OnTransactionCompleted`, landing in `ReceiptCtrl.TyroTransactionCompleted` (`ReceiptCtrl.cs` line 765).
5. `TyroTransactionCompleted` reads `transaction.GetStatus()` and `transaction.GetResult()`, and parses the extra-data XML to recover the MID (`<mid>` element). If the status is `COMPLETE` and the result is `APPROVED`, it marks the matching `TyroPurchaseDto` completed, locks the allocation and bank-account edits (`allocCtrl.LockAllocationChanged`), and marks the receipt payment as Tyro-paid (`recItemCtrl.LockTyroPaidPayment`). It sets `IsTyroProcessed = true`. Otherwise it flags the purchase as failed (`allocCtrl.IndicateTyroFailedTransactions`).
6. Either way it records `AuthoriseNum`, `ExtraData`, `TransactionID` and `TransactionReference` back onto the DTO and calls `SaveTransactionLog(purchase)`.
7. On a successful integrated payment it then prompts the operator to also send the claim to Medicare, and if accepted invokes the online-claim button handler.

`TyroTransactionErrorOccured` (line 844) is the failure callback: it stamps the DTO status to the error result, records the error transaction id and message in `ExtraData`, logs via `ErrorLogger`, and saves the transaction log.

Guard rails on the receipt: `IsTyroProcessed` prevents closing the receipt with a processed-but-unsaved Tyro transaction, and the save path refuses to save while there is an unprocessed Tyro purchase (`MessageStrings.TyroReceiptPreventSaveOnTransactionUnprocessed`). `DoTyroPrepaymentCheck` blocks over-allocation of a Tyro-paid amount against the receipt item.

`TyroResult` (`Tyro/TyroResult.cs`) is a small immutable value object describing a completed EFTPOS transaction (type, transaction id, success, amount, cash-out). It is produced by the `TyroServices.Purchase` / `TyroServices.Refund` helpers, but note those helpers are largely commented out (see below), so in the current receipt flow the DTO and the transaction log carry the state rather than `TyroResult`.

### Claiming flow (Bulk Bill, Private Patient, HealthPoint via the terminal)

Tyro Easyclaim is an alternative transport for Medicare and health-fund claims. A claim is flagged to use Tyro by the boolean `UseTyro` on the claim entity (`MedicareOnlineClaiming.UseTyro`, `PrivatePatientClaim.UseTyro`). The claim-management screens route Tyro-flagged claims to the terminal path and everything else to the normal Medicare Online web-service path.

Entry point (Bulk Bill), `frmMedicareOnlineManagement.cs` around line 300:

- Selected batched claims are split into two sets: `!c.UseTyro` (normal, goes to `BillingService.CreateBulkBillDvaClaim`) and `c.UseTyro` (goes to `BillingService.PerformBBTyro(tyroClaims, TyroError, true)`, `frmMedicareOnlineManagement.cs` line 349). The `true` argument is `rightAssigned`.
- Both sets are locked via `ObjectLockManager` while in flight and released in the `finally`.

`PerformBBTyro` (`BillingService_Tyro.cs` line 614) processes each `BulkBillClaiming`:

1. Collects the non-deleted invoices behind the claim's `ClaimingQueueBulkBillAndDVAs`.
2. Builds a Medicare claim model via `BulkBillClaimProcessLevel.CreateClaimModelObjectOnly` (the same builder used by the normal online path), accumulating errors into an `ErrorObject` list.
3. Converts that model to the Tyro request DTO with `ConvertMedicareModelToTyroModel(BulkBillClaimProcessLevel)`.
4. Validates the DTO with `tyroClaim.claim.IsValidObject()` (per-field cardinality/type checks, see below). Any error string aborts this claim.
5. Serialises to XML via `TyroCommonMethod.CreatePayload(tyroClaim)`, logs the payload to `MedicareOnlineLogger`, and sends it with `TyroWrapper.SendBulkBillClaim(xmlString, rightAssigned)`.
6. Creates an `EftposTransactions` log row via `CreateEasyClaimTransactionLog(...)`.
7. Response handling: `transactionResult.ErrorOccurred()`, then a text-contains check for `ERROR`, then a contains-check for `CANCELLED` (mapped from `eTTAResult.CANCELLED`). On success it deserialises the response with `TyroCommonMethod.DeserializeResponse` into a `TyroBulkBillResponse`, stamps `mClaim.Status = (int)eClaimStatus.Transmitted`, `mClaim.UseTyro = true`, sets `SentDate` and `TransactionId`, soft-deletes the claiming-queue rows, and propagates `SentClaimStatus` and `ClaimNumber` (prefixed with "E") to every linked invoice.
8. Per-service transaction-log detail rows are added, and one `TransactionContext.SaveChanges()` is issued at the end of the whole loop.

Private Patient claims follow the same shape in the two `PerformPPTyro` overloads (`BillingService_Tyro.cs` lines 64 and 256). Differences worth noting:

- The account-paid indicator drives the SDK call: `TyroWrapper.SendPrivatePatientClaim(xmlString, tyroClaim.claim.accountPaidInd == "Y")`.
- Response type is `TyroPrivatePatientResponse`. A `medicareAcceptanceType` of `NAC` is mapped to `eClaimStatus.FullyRejected`; anything else is `eClaimStatus.Assessed` and produces `PrivatePatientPciBenefitPaymentReport` rows carrying the explanation code, explanation text (resolved via `ErrorCodeMessageHelper.GetErrorMessage`) and the benefit amount.
- The second overload additionally generates and persists the domain claim (`claimProcess.GenerateDomainObject`), and on a cancelled transaction rolls back the just-added claim/voucher/service graph by deleting `EntityState.Added` objects.

HealthPoint (health-fund) claims are handled by `PerformHealthPointTyro` (`BillingService_Tyro.cs` line 787):

1. Builds a `HealthPointClaiming` from the invoice, mapping each invoice item to a `HealthPointClaimItem` (claim amount, service date, MBS item code padded to 5 chars, description trimmed to 32, patient id, service reference).
2. Converts via `ConvertMedicareModelToTyroModel(HealthPointClaiming)`, validates each `ClaimItem`, serialises with `TyroCommonMethod.CreatePayload(tyroClaim, false)` (note: no namespace, and special chars escaped via `ReplaceXmlSpecialChars`), and sends with `TyroWrapper.SendHealthPointClaim(providerNum, serviceType, itemCount, totalChargeAmountInCents, xml)`.
3. Response handling is richer than the Medicare paths because HealthPoint has its own error and void codes. It checks `GetHealthpointErrorCode`, the `ERROR` text, `VOIDED`, `DECLINED`, `VOID_DECLINED` and per-item `ResponseCode`. Item rebate amounts are matched back to claim items by a composite key (claim amount, item number padded to 5, service ref, date of service, patient id). Claim status becomes `Assessed`, `FullyRejected` or `Aborted` accordingly.
4. On any early failure it calls `CleanHealthPointClaim` to delete the `EntityState.Added` graph and `RecordFirstError` to stamp the transaction note.

HealthPoint post-processing helpers, all in `BillingService_Tyro.cs`:

- `CancelHealthPointTyro` (line 1042) replays the stored `Payload` to `TyroWrapper.CancelHealthPointClaim` and moves the claim/invoice to `Aborted`.
- `AllocateHealthPointClaim` (line 1113) turns an assessed HealthPoint claim into receipts: it builds a `Receipt` with `ReceiptPayment` and `BillingAllocation` rows, allocates the returned rebate per item, handles over-rebate as pre-payment credit, adds discount allocations, and finally queues the receipt to Xero (`XeroIntegration.Utility.UpdateReceiptToQueue`). This is the tie-in from claiming back into the invoice/receipt ledger.
- `HealthPointClaimPayGap` (line 1330) drives the patient co-payment for the residual gap after the fund rebate, opening a `ReceiptCtrl` with `IsTyroHealthPointPayGap = true` so the gap is charged on the terminal and tagged to the healthpoint transaction id.

### Model conversion and payload construction

`ConvertMedicareModelToTyroModel` (three overloads in `BillingService_MedicareOnlineHelper.cs`) is the bridge from Pracnet's internal claim model objects (`*ClaimModelObject`, `*VoucherModelObject`, `*ServiceModelObject`) to the serialisable Tyro DTOs. It maps patient/claimant/provider/payee identifiers, referral and request objects (only when service type is not "O"), service lines with charge amounts (`ConvertAmountToString(7)`), item override codes, self-deemed codes, LSPN, SCPID and so on. The Bulk Bill overload also sets `cevRequestInd = "N"` by default and nulls it when the service type is specialist ("S").

`TyroCommonMethod.CreatePayload` (`CommonHelper/TyroCommonMethod.cs` line 72) serialises the DTO with `XmlSerializer`, then prunes the tree: it strips empty attributes, the xsi/xsd/type/p3 attributes, and iteratively removes empty leaf elements. This "remove empty nodes" pass is why the DTOs can leave optional properties null and still produce a clean payload. Set `addNameSpace` to false (HealthPoint does this) to omit the `ns1` namespace.

`TyroCommonMethod.DeserializeResponse` deserialises the terminal's XML response string into the matching `Tyro*Response` type.

Field validation lives in `TyroCommonMethod.IsValidClaimElementValue` plus the per-type helpers (`IsValidType_A`, `IsValidType_AN`, `IsValidType_ANS`, `IsValidType_N`, `IsValidType_D`, `IsValidType_DT`). Each DTO's `IsValidObject()` (for example `TyroBulkBillClaimLevel.IsValidObject`) calls these with the field name, max length, type class (`ClaimObjectTypeEnum`) and cardinality (`ClaimObjectInputEnum`, where M is mandatory, O optional, C conditional, N must-not-be-set). This is the client-side gate before a payload is ever sent to the terminal, and it returns a concatenated human-readable error string (empty means valid).

### Request/response data types

The DTOs mirror the Medicare eclaiming v2 schema. Requests are `Tyro{BulkBill,PrivatePatient}Request` (each wrapping a `*ClaimLevel`) and `TyroHealthPointRequest`. The claim/voucher/service hierarchy for Bulk Bill and Private Patient derives from shared abstract bases in `CommonBase/`: `TyroClaimLevelBase` (holds `Vouchers` and enforces `MaxNumOfVouchers` via `SetVoucher`), `TyroVoucherLevelBase` (holds services, `SetService`) and `TyroServiceLevelBase`. Shared leaf objects (`PatientObject`, `ProviderObject`, `ReferralRequestObject`, `IdentityObject`, `ExplanationObject`, plus the `EnumObject` enum wrappers) live in `CommonObject/`.

Responses are `TyroBulkBillResponse` / `TyroBulkBillClaimResponse`, `TyroPrivatePatientResponse` / `TyroPrivatePatientClaimResponse`, and `TyroHealthPointResponse` / `TyroHealthPointClaimItemResponse`. `TyroBulkBillClaimResponse` (`BulkBillClaim/TyroBulkBillClaimResponse.cs`) is representative: it carries the response `Vouchers`, `medicareEligibilityStatus`, `concessionStatus`, a `patient` response object, and computed convenience properties (`PatientInfo`, `ProviderInfo`, flattened `Services`), plus non-serialised bookkeeping fields `claimNum` and `ClaimGuid` that the orchestration stamps after send so the response can be tied back to the Pracnet claim.

### Persistence: EftposTransactions log

Every Tyro send writes an `EftposTransactions` row through `CreateEasyClaimTransactionLog` (`BillingService_Tyro.cs` line 509). The concrete type depends on the claim type: `BulkBillEasyClaimTrans`, `PrivateEasyClaimTrans`, or `HealthPointClaimTrans` (a table-per-hierarchy on `EftposTransactions`). The row stores the request payload, the raw response, transaction id, transaction reference, claim transaction id, status, note (error code plus resolved message), amounts, and invoice/provider/payee context. Item-level detail is stored in `EftposTransaction_InvoiceItem` rows appended via the `AddTransactionItem` extension. These logs are surfaced in `frmTyroTransactions` and the `Tyro*TransactionsGridViewCtrl` grids. Error-only records are also written to `TYROError` (see `TyroServices.SaveError`).

### Configuration and deployment

- Practice-level enable flag: `MedicareOnlineSetting.Instance.TyroUsed`. If false, no Tyro UI is wired up regardless of SDK presence.
- Vendor SDK: referenced as `Tyro.Integ` (HintPath `..\Referenced Assemblies\Tyro.Integ.dll`, see `SourceCode/TyroAdapter/TyroAdapter.csproj` line 126). The adapter also references Infragistics, Castle and log4net.
- Terminal id: registry key under `HKEY_CURRENT_USER\Software\Tyro\Terminal.ID`. Terminal preferences are edited through the SDK's own `PreferencesForm` (opened by `TyroWrapper.ShowConfigDialog`).
- The Medicare eclaiming XML namespace used for all claim payloads is defined once in `Constant.TyroNameSpace` (`CommonHelper/Constant.cs`).
- Cardinality limits: `Constant.MaxNumOfBulkBillVouchers` and `MaxNumOfPrivatePatientVouchers` are both 1; `MaxNumOfBulkBillServices` and `MaxNumOfPrivatePatientServices` are both 14. The Billing GUI also enforces `TyroServices.MAX_ITEM_COUNT = 24` in the older builder.

### Gotchas for a new developer

- Static events. `TyroWrapper.OnTransactionCompleted`, `OnReceiptReturned` and `OnErrorOccured` are static. Any consumer must detach on close or handlers leak and multiple screens can react to one terminal callback. `ReceiptCtrl` does this correctly via `DetachTyroTransactionEvents`; follow that pattern in any new consumer.
- SDK presence is optional at runtime. Always gate on `TyroWrapper.CheckTyroIntegrated()` (it swallows `ReflectionTypeLoadException`). Calling an adapter when the SDK is not deployed will throw.
- `TyroServices` is mostly dormant. In `SourceCode/Abaki.Billing.Gui B1/Tyro/TyroServices.cs`, `EasyClaim`, `Purchase` and `SaveTransaction` have their bodies commented out (they return null/false and one path even contains a `throw new Exception("TODO:Review this removed property")` inside dead code). The live claiming path is `BillingService_Tyro.cs`, and the live EFTPOS path is `BillingService.TyroPurchaseProcess` and friends. Do not assume `TyroServices` is doing the work.
- `RefundSync` is a stub that returns null (`TyroWrapper.cs` lines 227 to 245). Use the async `Refund` / `RefundMidTid` path.
- Amounts are integer cents. Every SDK call takes cents. Conversions are done inconsistently as either `amount.ConvertToCents()` or an inline `(int)(amount * 100)`; both appear in `BillingService.cs`. Watch for rounding when adding new call sites and prefer `ConvertToCents()`.
- `SetVoucher` has an off-by-one. `TyroClaimLevelBase.SetVoucher` (`CommonBase/TyroClaimLevelBase.cs` line 37) tests `Vouchers.Count <= MaxNumOfVouchers`, so with `MaxNumOfVouchers = 1` it will accept a second voucher (count 0 then count 1 both pass). In practice the converters only ever add one Bulk Bill / Private Patient voucher, so this is masked, but do not lean on `SetVoucher` for cardinality enforcement.
- Response classification is string-based. The code branches on `result.Contains("ERROR")`, `result.Contains("CANCELLED")`, `VOIDED`, `DECLINED`, `VOID_DECLINED`, and status/result equality against `eTTAResult` / `eTyroTransactionStatus` / `eTyroTransactionResult` names. There are two different result enums (`eTTAResult` in `TyroAdapter.CommonHelper` and `eTyroTransactionResult` in `Abaki.Common.Enums`) with overlapping but not identical members; be careful which one a given comparison uses.
- SaveChanges placement differs by path. `PerformBBTyro` saves once at the end of the whole loop, so a mid-batch exception can discard earlier successes; `CreateEasyClaimTransactionLog` and `PerformHealthPointTyro` save per claim. This mirrors the wider claim-transmit conventions but the Tyro path has not been through the same silent-fail hardening as the Medicare Online BBSW/IMC paths, so treat status propagation on failure branches with suspicion when changing it.
- Spec compliance. Tyro claim payloads are Medicare eclaiming v2 (the namespace is `http://medicareaustralia.gov.au/eclaiming/version-2`). Any change to `ConvertMedicareModelToTyroModel`, the DTO field set, or `IsValidObject` cardinality is a wire-format change and must be cross-checked against the relevant Services Australia specification and NOI test rows before merge, per the code-review spec-compliance rule.
- Test/sample payloads carry fake credentials. The sample XML in `TyroWrapper.cs` uses placeholder Medicare and provider numbers for manual terminal testing only; never treat them as reference data.

This is general information only. Please confirm with the relevant team before use.

---

## 11. LanternPay and eTAC (TAC) integration

This section covers how Pracnet submits Transport Accident Commission (TAC) claims (and related WorkCover / "Other" program claims) to the LanternPay provider gateway. LanternPay is HICAPS' third-party biller platform. Pracnet talks to it over HTTPS / JSON using OAuth2 client-credentials authentication. The integration also drives eligibility pre-checks (predetermination), asynchronous claim-status notifications, and automatic receipting of funded claims.

If you are new to this subsystem, read these files in order: the client (`SourceCode/Abaki.Core.Gui B1/eTAC/LanternPayService.cs`), the request model (`SourceCode/Abaki.Core.Gui B1/eTAC/LPSubmitInvoiceRequest.cs`), the entity-to-payload mapper (`SourceCode/Abaki.Core.Gui B1/eTAC/Models/LPClaim.cs`), and the orchestration layer (`SourceCode/Abaki.Billing.Gui B1/BillingService_TAC.cs`).

### 11.1 What "eTAC" and LanternPay are

- **TAC** is the Victorian Transport Accident Commission. Providers bill the TAC for services rendered to accident-scheme clients.
- **LanternPay** is the biller/payment gateway (a HICAPS product) that sits between the practice-management system and the funder. Pracnet does not talk to the TAC directly; it submits invoices to LanternPay, which adjudicates and returns per-claim benefit amounts.
- **"eTAC"** is Pracnet's internal name for the feature (namespace `Abaki.Core.Gui.eTAC`, and `eClaimType.eTAC`). The same LanternPay pipeline is reused for WorkCover (`eClaimType.eWorkcover`) and generic "Other" programs (`eClaimType.eOthers`) when `AllowWorkCoverLanternPay` is enabled, selected by the LanternPay `program` code (for example `tac`, `wsv`, `wcq`). See `LanternPayService.GetProgramCodes()` in `LanternPayService.cs:38` for the full program list; most entries other than `tac` are marked deprecated.

### 11.2 Key classes and files

| Component | File | Role |
|---|---|---|
| LanternPay HTTP client | `SourceCode/Abaki.Core.Gui B1/eTAC/LanternPayService.cs` | OAuth token retrieval, request signing, submit-invoice, predetermination, notification poll and delete |
| Runtime config holder | `LPConfig` class in `LanternPayService.cs:400` | Carries `ApiKey`, `ApiSecret`, `ApiClientId`, `BillerNumber`, `BaseURL` into the service |
| OAuth token DTO | `SourceCode/Abaki.Core.Gui B1/eTAC/TokenResponse.cs` | Deserialises `access_token` and `expires_in` |
| Submit-invoice request DTO | `SourceCode/Abaki.Core.Gui B1/eTAC/LPSubmitInvoiceRequest.cs` | JSON body for invoice submission (`program`, `member`, `claims[]`) |
| Notification response DTOs | `SourceCode/Abaki.Core.Gui B1/eTAC/NotificationResponse.cs` and the `TAC*` classes at the bottom of `LanternPayService.cs` | Async claim-status messages from the notification cache |
| Entity to payload mapper | `SourceCode/Abaki.Core.Gui B1/eTAC/Models/LPClaim.cs` | Maps `TACClaiming` / `TACClaimingView` entities to LanternPay request objects |
| Orchestration / business logic | `SourceCode/Abaki.Billing.Gui B1/BillingService_TAC.cs` | Batch, validate, transmit, poll status, auto-receipt |
| Clinic-level settings UI | `SourceCode/Abaki.Core.Gui B1/eTAC/eTACCtrl.cs` (and `eTACCtrl.Designer.cs`) | Enable flag, credentials, staging toggle, Test Service button |
| Provider-level settings UI | `SourceCode/Abaki.Core.Gui B1/eTAC/eTACProviderCtrl.cs` | Per-provider credential override control (subclass of `eTACCtrl`) |
| Settings form host | `SourceCode/Abaki.Billing.Gui B1/frmMedicareOnlineSetting2.cs` | Loads and saves the clinic-level eTAC settings |
| Provider settings host | `SourceCode/Abaki.Core.Gui B1/Controls/StaffCtrl.cs` | Loads and saves per-provider eTAC credentials on the Staff record |
| Claim management screen | `SourceCode/Abaki.Billing.Gui B1/HICOnline/frmTACClaimManagement.cs` | Main workbench: check eligibility, send, poll status, allocate |
| New-claim / batching screen | `SourceCode/Abaki.Billing.Gui B1/HICOnline/frmNewTACClaim.cs` | Batches selected invoices into claims and sends |
| Eligibility result screen | `SourceCode/Abaki.Billing.Gui B1/HICOnline/frmTACCheckEligibilityResult.cs` | Displays predetermination results |
| Settings backing store | `SourceCode/ClinicCoreSetting/RecallGlobalSetting.cs` (TAC properties around lines 304 to 415) | Clinic-wide TAC settings |
| Settings provider | `SourceCode/ClinicCoreSetting/ClinicCoreSqlSettingProvider.cs` | Persists `RecallGlobalSetting` to SQL, not app.config |

### 11.3 Authentication mechanism (OAuth2 client credentials)

`LanternPayService.GetAccessToken(clientId, clientSecret)` (`LanternPayService.cs:74`) is the single choke point for auth. It performs a standard OAuth2 **client-credentials** grant:

- It POSTs a `FormUrlEncodedContent` body with `scope=openid`, `client_id`, `client_secret` and `grant_type=client_credentials` to the LanternPay auth endpoint.
- Endpoint selection is environment-driven off `RecallGlobalSetting.Instance.UseTACStaging`: the sandbox auth host when staging is on, otherwise the production auth host (both are hard-coded literals in `GetAccessToken`).
- The response is deserialised into `TokenResponse` (bearer `access_token` plus `expires_in` seconds).
- **Token caching.** The bearer token and its expiry are cached in the `private static` fields `_accessToken` and `_tokenExpiry` (`LanternPayService.cs:34-35`). The expiry is set to `DateTime.UtcNow.AddSeconds(expires_in - 60)`, a 60-second safety buffer, at `LanternPayService.cs:105`. On the next call, if the cached token is still valid it is returned without a round-trip (`LanternPayService.cs:79`). Because these are static fields, the cache is process-wide and is **not** keyed by clientId or biller; if two different billers were used in one session the cache could hand back the first biller's token. In practice a single clinic uses one credential set, so this is latent rather than an active bug, but be aware of it if you add multi-biller support.

The two OAuth inputs `client_id` and `client_secret` map to the practice's LanternPay credentials:

- `clientId` argument = `LPConfig.ApiKey` (the practice's LanternPay API key).
- `clientSecret` argument = `LPConfig.ApiSecret` (the practice's LanternPay API secret).

Note the naming is slightly counter-intuitive: the OAuth **client_id** is populated from the setting labelled "API key" and the OAuth **client_secret** from "API secret". There is a separate value, `LPConfig.ApiClientId` (setting `TACApiClientId`), which is **not** the OAuth client id. It is sent on every non-auth request as the `X-LanternPay-Api-Client-Id` HTTP header (see `CreateRequest`, `LanternPayService.cs:158`). Keep these three distinct when debugging a 401 versus a 403.

### 11.4 Request signing and headers

All non-token requests go through `LanternPayService.CreateRequest(method, relativeUrl, contentJson, baseUri)` (`LanternPayService.cs:138`), which:

1. Forces `ServicePointManager.SecurityProtocol = Tls12` (required by the gateway; the app targets .NET Framework 4.5 where TLS 1.2 is not the default).
2. Calls `GetAccessToken(Config.ApiKey, Config.ApiSecret)` and sets `Authorization: Bearer <token>`.
3. Adds `X-LanternPay-Api-Client-Id: <Config.ApiClientId>`.
4. Adds `Host: <base host>`.
5. Adds `X-Hicaps-Api-Client-Instance-Id: <machine id>`. The machine id comes from `GetUniqueMachineId()` (`LanternPayService.cs:168`), which reads the Windows registry `MachineGuid` under `SOFTWARE\Microsoft\Cryptography`, falling back to BIOS then motherboard serial via WMI, then the literal `"UNKNOWN"`. This is how LanternPay pins a submission to a physical device.
6. Logs the full request (URL, headers, body) via `ClaimingErrorLogger` at Debug level. Response logging happens in `parseResponse` / `LogDetailForResponse`.

Every request and response is written to the claiming log (`ClaimingErrorLogger`, which writes to `Logs/ClaimingLog_*`), not the general `ErrorLogger`. When a tester reports a TAC failure, this is the log to pull.

### 11.5 Where the credentials are stored (secrets)

Do not hard-code or print any of these values; the notes below describe only their location and purpose.

**Clinic-wide credentials** live in `RecallGlobalSetting` (`SourceCode/ClinicCoreSetting/RecallGlobalSetting.cs`), which is an `ApplicationSettingsBase` subclass decorated with `[SettingsProvider(typeof(ClinicCoreSqlSettingProvider))]` (line 19). This means the values are persisted to the **SQL database** by `ClinicCoreSqlSettingProvider` (a `SqlSettingBase`), not to a local `.config` file. The relevant properties:

- `EnableTAC` (bool, line 358) - master on/off for the feature.
- `TACBillerNumber` (string, line 403) - the LanternPay biller number, used to build every request URL (`billers/{BillerNumber}/...`).
- `TACApiKey` (string, line 385) - the practice API key (fed to OAuth `client_id`). Secret.
- `TACApiSecret` (string, line 394) - the practice API secret (fed to OAuth `client_secret`). Secret.
- `TACApiClientId` (string, line 412) - the API client id sent as the `X-LanternPay-Api-Client-Id` header.
- `UseTACStaging` / `UseTACProduction` (bool, lines 367 and 376) - environment selector.
- `TACStagingURL` / `TACProductionURL` (string, lines 340 and 349) - base URLs carried in `LPConfig.BaseURL`. Note: the actual API and auth hosts used inside `LanternPayService` are hard-coded literals switched on `UseTACStaging`; these URL settings feed `LPConfig.BaseURL` and the settings UI's Test Service path but the live HTTP calls do not read them. This is a known inconsistency worth tidying if you touch endpoint config.
- `TACProgramCode` / `WCProgramCode` (string) - default program code for TAC and WorkCover.
- `AutoReceiptInvoicesForTACClaim` (bool, line 331) - auto-receipt approved claims.
- `AllowWorkCoverLanternPay` (bool, line 304) - unlocks the WorkCover program-code path in the UI.

**Per-provider credential overrides** are stored on the Staff entity (EF entity in `SourceCode/Abaki.Data/Domains/Common.edmx`, so it is a column on the Contacts/Staff table). A provider can have `EnableTAC`, `TACBillerNumber`, `TACApiKey` and `TACApiSecret` of their own. These are loaded and saved through `StaffCtrl.cs:96-99` and `:168-171` using the `eTACProviderCtrl` UserControl. When present they **override** the clinic-wide values for that provider's invoices (see `GetLanternPayConfig` below).

The credentials are entered in the UI through `eTACCtrl` (clinic-level, hosted in `frmMedicareOnlineSetting2`) and `eTACProviderCtrl` (provider-level, hosted in `StaffCtrl`). `frmMedicareOnlineSetting2.cs:86-93` loads them into the control and `:236-248` writes them back to `RecallGlobalSetting`.

### 11.6 Building the effective config: GetLanternPayConfig

`BillingService_TAC.GetLanternPayConfig(Invoice inv)` (`BillingService_TAC.cs:675`) is the resolver that decides which credentials a given invoice uses:

1. Start with `ApiClientId` and `BaseURL` from the clinic settings.
2. If `EnableTAC` is true clinic-wide, fill `BillerNumber`, `ApiKey`, `ApiSecret` from clinic settings.
3. Determine the payee (prefer `inv.PayeeBusinessAddress`, else `inv.ProviderBusinessAddress`). If that payee's `Staff.EnableTAC` is true and it has a non-empty `TACBillerNumber`, **override** `BillerNumber`, `ApiKey`, `ApiSecret` with the provider's own credentials.

The resulting `LPConfig` is then assigned to the static `LanternPayService.Config` immediately before any transmit call. Because `LanternPayService.Config` is static, this assignment is not thread-safe across concurrent claims, but the workbench sends claims serially on a `BackgroundWorker`, so it is safe in practice. If a claim has no biller number after resolution, the send is rejected with an input-data error ("There is no biller number indicated ...").

### 11.7 Data model and payload mapping

The persisted entities are `TACClaiming` (claim header) and `TACService` (per-invoice-item line), with read-model projections `TACClaimingView` and `TACServiceView` used by the grids. A `TACClaiming` links to an `Invoice`; each `TACService` links to an `InvoiceItem`.

`LPClaim.MapFromEntity(TACClaiming tacClaim, bool notFundedFully = false)` (`Models/LPClaim.cs:31`) builds the `LPSubmitInvoiceRequest` sent for real invoice submission:

- `Program` = the claim's program code (for example `tac`).
- `ResponsePriority` = `"normal"`.
- `BillerInvoiceId` and `InvoiceNumber` = the Pracnet invoice number. This is the key LanternPay echoes back in notifications, so status matching keys off it (see 11.9).
- `Member` = patient details (member number = program client number, given/family name, birth date as `yyyy-MM-dd`, gender mapped via `GetGender` to `male`/`female`/`unspecified`, email).
- `Claims[]` = one `Claim` per `TACService`, each carrying `billerClaimId` (the InvoiceItem GUID, the per-line correlation key), servicing provider (Medicare provider number, name, discipline from `Employee.Specialist`), `itemPublisher = "TAC"`, `itemCode` (the MBS/item number), `quantity`, `unitPrice`, `serviceDate` and description.

A second overload `MapFromEntity(TACClaimingView, Patient, Invoice)` (`Models/LPClaim.cs:94`) builds the older `LPClaim` shape used by the eligibility-check path. `LPService.MapFromEntity` (`LanternPayService.cs:351`) maps a `TACServiceView` line for that path.

`LPSubmitInvoiceRequest` and its nested `Member`, `Claim`, `Provider`, `Person`, `Registrations` classes (all in `LPSubmitInvoiceRequest.cs`) are annotated with `[JsonProperty(...)]` for the exact wire field names, so rename with care.

### 11.8 End-to-end control flow

Entry points are on `frmTACClaimManagement` (the TAC claim workbench, opened from the billing/claiming menu) and `frmNewTACClaim` (batching from a list of invoices). The heavy lifting is all `static` methods on the partial class `BillingService` in `BillingService_TAC.cs`.

**Batching.** `BatchTACClaim(Invoice, eClaimType)` (`BillingService_TAC.cs:1096`) creates a `TACClaiming` with status `eClaimStatus.Batched`, generates a claim number via `ClaimingCommonMethods.GetWC_TACClaimNumber`, sets the program code from settings by claim type, creates a `TACService` per invoice item (computing GST-inclusive unit price, tax code `GST`/`FRE`, quantity and total), sums `TotalClaimAmount`, saves, then stamps the invoice's `ClaimNumber` and `SentClaimStatus`. `BatchTACClaims` iterates a list of `ClaimViewInvoice`.

**Eligibility / predetermination.** `CheckEligibility(TACClaiming, ...)` (`BillingService_TAC.cs:238`) refreshes the claim from the invoice, resolves config, validates, sets `LanternPayService.Config`, maps via `LPClaim.MapFromEntity`, then calls `LanternPayService.SendClaim(lpClaim, SendType.EligilibilityCheck)`. `SendType.EligilibilityCheck` targets the `predetermination` endpoint rather than `invoice` (see `SendClaim`, `LanternPayService.cs:260`). It then immediately polls `GetBillerNotifications()`, finds the notification whose `Data.InvoiceId` matches the response `InvoiceId`, and maps per-line `benefit` amounts and adjudication reasons back onto the `TACServiceView`s. Claim view status is set to `ReadyToReceipt` (approved), `FullyRejected` (rejected) or `Referred` (pending). Note this synchronous poll-immediately-after-send pattern assumes the notification is already available; if LanternPay has not yet published it, the `noti` lookup can throw a null reference, which is caught and surfaced as a runtime error.

**Real submission.** `SendTACClaim(TACClaiming, ...)` (`BillingService_TAC.cs:420`) calls `InvoiceDataService.RefreshInvoice`, resolves config, validates via `ValidateTACClaim` (patient present, patient DOB present, program client number present via `Invoice.Claim`, servicing provider number present), maps the payload, then calls `LanternPayService.SendClaim(lpClaim, SendType.SendInvoice)` against the `invoice` endpoint. On success (`response.IsSuccess`) it sets claim status `Transmitted`, copies the claim number and `SentClaimStatus` onto the invoice, records `TransactionId = response.InvoiceId`, stores the biller number, and `SaveChanges`. The list overload `SendTACClaim(IList<TACClaiming>, ..., BackgroundWorker, ...)` drives a progress bar and accumulates `ErrorObject`s per claim.

**HTTP semantics in SendClaim** (`LanternPayService.cs:250`): a `202 Accepted` is treated as success and deserialised into `TACResponseInvoice` with `IsSuccess = true`. Any other status throws; `403 Forbidden` throws with just the reason phrase (usually a config/credentials problem), everything else throws with the response body appended. The caller catches and turns the exception into an `ErrorObject`.

### 11.9 Asynchronous status notifications

LanternPay adjudicates asynchronously and publishes results to a notification cache. `GetTACClaimStatus(List<TACClaiming>)` (`BillingService_TAC.cs:153`) is the poll:

1. Build an `LPConfig` from clinic settings and assign `LanternPayService.Config`.
2. `LanternPayService.GetBillerNotifications()` (`LanternPayService.cs:112`) GETs `billers/{BillerNumber}/notifications?maxNumberOfMessages=10` from the notification-cache host (sandbox or prod per `UseTACStaging`). A `403` throws a "check LanternPay configuration" error.
3. For every returned notification, `DeleteBillerNotifications(notificationId, receiptHandle)` (`LanternPayService.cs:214`) is fired to acknowledge/remove it. This runs on a **detached `Task`** with a one-hour `HttpClient` timeout and its result is intentionally ignored (the delete is fire-and-forget). Be careful: because notifications are deleted right after fetch, a run that crashes mid-processing can lose status updates. This is why the delete loop and the processing loop are separate passes over the same list.
4. Match each notification to a claim by `BillerInvoiceId` == `Invoice.InvoiceNum`. Refresh the claim from the invoice, capture the provider's default bank account, sum per-claim `benefit` into `claim.Funded`, compute `Unfunded`, and push per-line funded/unfunded onto each `TACService` keyed by `billerClaimId` == InvoiceItem GUID.
5. If any claim status is `approved`, set claim status `ReadyToReceipt` and, when `AutoReceiptInvoicesForTACClaim` is on, call `AllocateTACClaim`. If any is `rejected`, set `FullyRejected` and store the adjudication reason. `SaveChanges` after each.

The notification DTO tree is duplicated: `NotificationResponse.cs` defines `NotificationResponse/Notification/...` and the bottom of `LanternPayService.cs` defines a parallel `TACRoot/TACNotification/...` set with nullable `Benefit`. `GetBillerNotifications` deserialises into the `TACRoot` set, which is the one actually used. Do not confuse the two when adding fields.

### 11.10 Receipting funded claims

`AllocateTACClaim(IList<TACClaiming>, ...)` (`BillingService_TAC.cs:558`) turns funded claims into receipts. It processes only claims with status `ReadyToReceipt` and no prior `ProcessAmount`. For each, it builds a `Receipt` and a `ReceiptPayment` (default payment type `DirectCredit`), then for each funded `TACService` creates a `BillingAllocation` via `MakeBillingAllocation`, resolving or creating a `BankAccount` from the claim's BSB/account. Discounts get their own allocation and a discount `ReceiptPayment` (item type `101`). Claim status becomes `PartlyPaid` or `FullyPaid` depending on funded versus total, the invoice `SentClaimStatus` and `RunDate` are updated, `ProcessAmount` is recorded, and finally `XeroIntegration.Utility.UpdateReceiptToQueue` queues the receipts for Xero sync.

### 11.11 Testing connectivity

`eTACCtrl.TestService()` (`eTACCtrl.cs:97`) calls `LanternPayService.TestConnection(apiKey, apiSecret, apiClientId, billerNumber, baseUrl)` (`LanternPayService.cs:58`), which simply attempts `GetAccessToken` and returns true if a non-empty token comes back. It exercises only the OAuth handshake, not a claim submission, so a green "Test connection successfully" means the API key/secret and environment are right; it does not prove the biller number or client-id header are valid (those only fail at submit time with a 403). The Test Service button is at `eTACCtrl.btnTestService_Click`.

### 11.12 Invariants and gotchas for a new maintainer

- **Three credential-like values, three roles.** `TACApiKey` -> OAuth `client_id`; `TACApiSecret` -> OAuth `client_secret`; `TACApiClientId` -> `X-LanternPay-Api-Client-Id` header. A 401 at the token endpoint means the first two are wrong; a 403 at submit usually means the header client-id or biller number is wrong. All three are stored in SQL via the settings provider, never in app.config.
- **Static token and static Config.** `LanternPayService._accessToken`, `_tokenExpiry` and `Config` are all `static`. The token cache is not keyed by credentials, and `Config` is reassigned per claim. Serial sending keeps this safe today; do not parallelise TAC sends without reworking this.
- **Hard-coded endpoints.** Auth, providers-API and notification-cache hosts are string literals inside `LanternPayService`, switched only by `UseTACStaging`. The `TACStagingURL` / `TACProductionURL` settings are largely vestigial for the live calls. If Services/LanternPay change a host, edit the literals in `LanternPayService.cs`.
- **TLS 1.2 is forced per request.** Required on .NET Framework 4.5. Removing that line will break the handshake.
- **`202 Accepted` is the success code**, not `200`. Any other status throws.
- **Notifications are deleted immediately after fetch** on a fire-and-forget task. A crash mid-processing can lose updates. Status matching is by invoice number (`BillerInvoiceId`) at the claim level and by InvoiceItem GUID (`billerClaimId`) at the line level, so those two identifiers must stay stable across the round-trip.
- **Eligibility check polls synchronously right after send** and can null-reference if the notification is not yet published; it is wrapped in a try/catch that reports a runtime error.
- **Provider credentials override clinic credentials** whenever the payee provider has `EnableTAC` and a biller number. Check the payee, not just the servicing provider, when a claim uses unexpected credentials (`GetLanternPayConfig`).
- **All request/response logging goes to `ClaimingErrorLogger`** (`Logs/ClaimingLog_*`) at Debug level, including full headers and bodies. This is the first place to look for a failed TAC claim, and note that it will contain the bearer token and header client-id in plain text, so handle those logs as sensitive.
- **Program reuse.** The same pipeline serves TAC, WorkCover and "Other" via `eClaimType` and the LanternPay `program` code. WorkCover is only exposed when `AllowWorkCoverLanternPay` is set. Most non-`tac` program codes in `GetProgramCodes()` are flagged deprecated by LanternPay.

This is general information only. Please confirm with the relevant team before use.

---

## 12. Medinet clinical-system integration and Pracnet-Medinet sync

Medinet is the separate clinical (GP consulting) product that a practice runs alongside Pracnet. Pracnet owns demographics, appointments and billing; Medinet owns the clinical record (progress notes, prescriptions, pathology, immunisations, letters and clinical documents). At a linked site the two products run over their own SQL Server databases and are stitched together by three mechanisms:

1. A cross-product record linkage layer (the `Abaki.LinkingMedinetAdaptor*` projects plus `Abaki.MedinetAdaptor`) that keeps individual demographic and reference records in step on save.
2. A batched, offline replication tool (`Abaki.Sync`) that copies whole tables and document files between a server database and a client (laptop) database using the Microsoft Sync Framework.
3. A one-way document backfill tool (`Abaki.LinkingMedinetPracnetLetterDocument`) that pushes historical Pracnet documents into Medinet.

Plus single sign-on: when Pracnet is launched from inside Medinet it reads the logged-in user out of a shared-memory segment and skips the login prompt.

This section covers each in turn. Read it end to end before touching any of these projects, because they use overlapping terminology ("sync", "link", "adaptor") for genuinely different things.

### 12.1 Terminology and a correction to the folklore

- "Linking" or "adaptor" = the per-record, save-time facade in `Abaki.LinkingMedinetAdaptor*`. It runs in-process inside Pracnet (and inside the Medinet-side tools). Despite the folder layout mirroring a WCF stack (`Contracts` -> `ServiceContracts` / `DataContracts` / `BusinessContracts`, plus `Service` and `Business` assemblies), this is not a live WCF service. The interfaces in `SourceCode/Abaki.LinkingMedinetAdaptorContracts/ServiceContracts/` are plain C# interfaces with no `[ServiceContract]` / `[OperationContract]` attributes, the contracts csproj has no `System.ServiceModel` reference, and there is no `ServiceHost` / `ChannelFactory` anywhere. The CLAUDE.md and repo overview describe these as "WCF-based"; treat that as historical intent, not current fact. It is an ordinary layered facade (interface -> Service class -> Business class -> DatabaseHelper / EF) invoked by method call. If you were told to "start the Medinet WCF service", there is nothing to start.
- "Sync" = the batched, whole-table + whole-file replication in `Abaki.Sync` (Microsoft Sync Framework). This is a standalone executable, not part of `Pracnet.exe`.
- "Medinet demographic" database = the shared demographic DB (Pracnet-style `TContacts` / `TPatients` schema, referred to in code as `MNDemographicCatalog`). "Medinet clinical / medical" database = the `CN_*` clinical schema (`MNMedicalCatalog`). "Medinet content" = the reference/knowledge DB (`Medinet_Content`, drug and ICPC2 data). Three physically distinct catalogs.

### 12.2 Detecting that Medinet is present

Two detection paths coexist.

Runtime detection inside Pracnet is via `StorageService` (singleton, `SourceCode/Abaki.Core.Service/StorageService.cs`). `StorageService.Instance.MedinetInstallationPath` has a setter that calls `CheckMedinetIntegrated(value)` (`StorageService.cs:995`), which just checks the path is non-empty and the file exists. When the path is valid it calls `LoadMedinetSetting()`, which:

- Reads `CustomSetting.xml` (constant `MedinetSetting.MedinetLocalSettingFileName`, `StorageService.cs:148`) from the Medinet install directory and XML-deserialises it into `StorageService.Instance.MedinetSettingInstance` (a `MedinetSetting`, root element `LocalDatabaseSetting`). This yields `MNDataSource`, `MNDemographicCatalog`, `MNMedicalCatalog`, `MNSqlUsername`, an encrypted password and `MNAuthenticateMode`.
- Then `LoadMedinetDocumentPath()` (`StorageService.cs:277`) opens the Medinet demographic DB, reads `SELECT TOP 1 Value FROM Clinic WHERE Parameter = 'General_DocumentPath'` to find where Medinet stores its documents, builds the demographic and medical connection strings, and finally sets `IsMedinetLoaded = true`.

So `StorageService.Instance.IsMedinetLoaded` is the runtime flag "Medinet config was found and its DB is reachable", and `MedinetSettingInstance` is the deserialised config. Note the password field on `MedinetSetting` is stored encrypted (see `SqlPassword` which round-trips through `EncryptedUtil`) - do not log it.

Tool-side detection (the standalone linking/sync tools) is via the Windows registry, using `RegistryHelper` (`SourceCode/Abaki.Common.Helpers/RegistryHelper.cs`). The relevant keys under `HKLM`:

- Pracnet install path: `SOFTWARE\Wow6432Node\GlobalHealth\Pracnet\` (or the older `...\Abaki\Pracnet\`), value name `InstallationPath`.
- Medinet install path: `SOFTWARE\Wow6432Node\GlobalHealth\Medinet\` (32-bit fallback `Software\GlobalHealth\Medinet\`), value name `MedinetInstallationPath`.

`RegistryHelper.GetPracnetRegistryValueByName` / `GetMedinetRegistryValueByName` resolve these. The linking tools then read `CustomSetting.xml` next to the Medinet install path via `LocalDatabaseSetting.ReadFromFile(...)` (see `Abaki.LinkingMedinetPracnetLetterDocument/frmMainForm.cs:756-788`, method `LoadMedinetDatabaseInfo`). The Pracnet-side DB profile is read from the Pracnet install path's `DbSetting` / `LocalSettings.xml` (`frmMainForm.cs:48-112`), honouring the last-used profile name from `LocalSettings.xml`'s `PracnetProfileName` element.

The master on/off switch for the in-process linking layer is `LinkingFileSetting.Instance.LinkingFile_EnableMedinetAdapter`, surfaced as `GlobalInfo.IsEnableLinkingAdapterWithMedinet` at login (`frmLogin.cs:500`). If this is false, Pracnet never calls the Linking services.

### 12.3 The in-process linking (record-linkage) layer

Projects (all under `SourceCode/`):

| Project | Assembly / role |
|---|---|
| `Abaki.MedinetAdaptor` | `Abaki.LinkingMedinetAdapterCore` - domain models (EF) and DB helpers. Namespaces `...Domains.Pracnet`, `.Medinet`, `.MedinetClinic`, `.P2KMigration`; `DatabaseHelper`, `SqlCommandHelper`, `LinkingConstants` |
| `Abaki.LinkingMedinetAdaptorContracts` | Interfaces (`ILinking*`), DTO contracts (`ILinkingPatient` etc.), and the crucial `Common/DatabaseConnectionManagement` static class |
| `Abaki.LinkingMedinetAdaptorBusiness` | Business logic (`Linking*Business`), one per entity type |
| `Abaki.LinkingMedinetAdaptorService` | Thin service facades (`Linking*Service`) that Pracnet/tools call |
| `Abaki.LinkingMedinetAdaptorUtils` | Helpers such as `SqlStoreProcedureCommonMethods` |
| `Abaki.LinkingMedinetAdaptorLogger` | Logging |
| `Abaki.LinkingMedinetAdapterLinkAutoExistingData` | Standalone tool that first-time links existing records between the two DBs |
| `Abaki.LinkingPracnetMedinetSetup` | Setup/config utility for the linking file settings |

Central plumbing: `DatabaseConnectionManagement` (`Abaki.LinkingMedinetAdaptorContracts/Common/DatabaseConnectionManagement.cs`) is a static holder of three connection infos and their lazily created helpers/contexts:

- `PracnetDatabaseConnectionInfo` -> `PracnetDatabaseHelper` (raw ADO helper) and `PracnetContext` (EF `PracnetModel`).
- `MedinetDatabaseConnectionInfo` -> `MedinetDatabaseHelper` and `MedinetContext` (EF `MedinetModel`, the demographic DB).
- `MedinetClinicalDatabaseConnectionInfo` -> `MedinetClinicalDatabaseHelper` and `MedinetClinicContext` (EF `MedinetClinicModel`, the `CN_*` clinical DB).
- Plus `P2KMigrationDbContext` (a `<PracnetDb>_MigrationLog` database) and Dapper connections.

There is a directionality flag: `DatabaseConnectionManagement.IsWorkingWithPracnet`. `true` = running inside Pracnet (updates flow Pracnet -> Medinet); `false` = running inside a Medinet-side tool (updates flow Medinet -> Pracnet). Business classes branch on this to decide which stored procedure to call (`sp_UpdateMedinetPatient` vs `sp_UpdatePracnetPatient`) and which external-id column to write. The connection infos are populated at Pracnet login by `frmLogin.InitLinkingManagement(currentProfile)` (`Abaki.SecurityGUI/frmLogin.cs:681`), which sets `IsWorkingWithPracnet = true`, points `PracnetDatabaseConnectionInfo` at the current profile, and points the two Medinet infos at `LinkingFileSetting.Instance.LinkingFile_Medinet*` values (data source, demographic catalog, medical catalog, credentials, auth mode, timeout).

GUID linkage columns (the join keys between the two systems):

- Contacts (patients, providers, referral doctors, businesses): Pracnet `Contacts.MedinetContactGuid` <-> Medinet `TContacts.PracnetContactGuid`. Set in `LinkingPatientBusiness.LinkPatientInfo` (`Abaki.LinkingMedinetAdaptorBusiness/LinkingPatientBusiness.cs:479-480`), and persisted by the update queries `UpdatePracnetExternalIdQuery` / `UpdateMedinetExternalIdQuery` at the top of that file.
- Security users: Pracnet `SecurityUsers.MedinetUserGuid` (the reverse mapping is read from that same column). Used by the document tools to translate created/updated/deleted user GUIDs.

How a link/update runs (patient is the canonical example, `LinkingPatientBusiness.cs`):

- `LinkPatientInfo(pracnetObj, medinetObj, replaceByPracnet)` loads both sides via EF, then field-by-field merges every demographic column using `DatabaseConnectionManagement.MixValue(pracnetVal, medinetVal, replaceByPracnet)`. `MixValue` prefers the non-empty side; when both are populated `replaceByPracnet` decides the winner. It also maps account types via `LinkingConstants.GetMedinetAccountType` / `GetPracnetAccountType`, and finally writes the two cross-reference GUIDs. Saves are batched: it only calls `SaveChanges()` on both contexts once a static counter `nCount` exceeds 1000 (`LinkingPatientBusiness.cs:482`). This is a batch-link path, not a per-record commit - be aware records can sit uncommitted in the context.
- `UpdatePatientInfo(patient)` is the incremental save path used at runtime. `LinkingPatientService.UpdatePatientInfo` first calls `CheckLinkPatientExist` to decide `LinkCode = "U"` (update) vs `"A"` (add), then the business layer builds a 113-element `SqlParameter[]` (`CreateLinkingParameters`) and calls `sp_UpdateMedinetPatient` (from Pracnet) or `sp_UpdatePracnetPatient` (from Medinet) via `DatabaseConnectionManagement.*DatabaseHelper.ExecNonQueryProc`. The stored proc returns the newly created external contact GUID as an output parameter, which is then written back into the linking column. So the actual cross-DB write for the common case is done by a SQL Server stored procedure, not EF.

There is a whole family of these (`LinkingImmunisationBusiness`, `LinkingReferralDoctorBusiness`, `LinkingDoctorStaffBusiness`, `LinkingLetterHeadBusiness`, `LinkingWaitingRoomBusiness`, `LinkingReferralNetConfigBusiness`, etc.), each following the same interface -> service -> business shape. One concrete call site to know: at login `LoadDataAsync` calls `LinkingReferralNetConfigService.MergeReferralNetConfig()` when `IsEnableLinkingAdapterWithMedinet` is true (`frmLogin.cs:579-584`), which reconciles ReferralNet configuration between the two systems.

Gotchas in this layer:

- Exception handling is uniformly `catch (Exception ex) { throw ex; }`, which resets the stack trace. Do not rely on stack traces from this layer; log the message.
- The batched `SaveChanges` in `LinkPatientInfo` means a crash mid-batch loses the whole uncommitted window. `nCount` is a static field shared across calls.
- `MixValue` treats whitespace-only as empty, so blanking a field on one side does not propagate a clear - the other side's value wins.

### 12.4 Batched replication - Abaki.Sync (the heart of "sync")

`Abaki.Sync` is a standalone Windows Forms executable (`Program.Main`, `SourceCode/Abaki.Sync/Program.cs`) that runs the Microsoft Sync Framework to move whole tables and whole document files between a "server" database and a "client" database. It is used to keep a laptop / branch (client) in step with the main practice server, offline-tolerant, for both the Pracnet demographic DB and the Medinet clinical DB. Assemblies referenced: `Microsoft.Synchronization`, `Microsoft.Synchronization.Data`, `Microsoft.Synchronization.Data.SqlServer`, `Microsoft.Synchronization.Files`.

Startup logic (`Program.Main`):

- If `ClinNetSetting.Instance.SyncConfig` is empty, run the configuration wizard (`RunWizard` -> `PrepareSyncWizard1`), which produces an `MDSync` object and serialises it into `SyncConfig`. Command-line flags: `/dem` (delete existing metadata), `/co` (config only).
- Otherwise, `AutoDetectUpdate(...)` checks whether the server has a newer Medinet build (compares `\\<server>\MedinetShare\updatedDate.txt` against the local copy and offers to run `medinetsetup.exe`), then `RunSync()` deserialises the saved `MDSync`, and shows `frmMedinetSync` to drive the sync.

`MDSync` (`SourceCode/Abaki.Sync/MDSync.cs`) is the orchestrator and the serialisable config. Key members:

- `Server` and `Client`: `MedinetSyncPeer` objects. Each holds `DataSource`, credentials or Windows auth, and the two catalog names `PracnetDatabase` and `MedinetDatabase`. The default `Server` peer uses SQL auth; the default `Client` peer uses Windows auth (`AllowWindowAuthenticate = true`).
- `ServerDocPath` / `ClientDocPath`: the two document folder roots for file sync.
- `SyncDocuments` (default `true`): whether to also sync document files after the DB sync.
- `Direction`: a `Microsoft.Synchronization.SyncDirectionOrder`, default `DownloadAndUpload` (i.e. bidirectional). Also `Upload` / `Download` are possible values.
- `ScopeName`: always `"Medinet"` (set in the constructor, `MDSync.cs:147`, and re-asserted in `UpdateNewConfig`). This is the Sync Framework scope name used for both databases.
- `ServerId` / `ClientId`: replica GUIDs used by the file-sync provider.
- `CheckAuthenticate`: if true, the user must authenticate and hold the `Synchronization` permission before a sync runs.

`MDSync.Synchronize()` (and the near-identical `SynchronizeSilence()` for background runs, and `Synchronize(List<Guid> filterPatients)` for a patient-filtered run) does, in order:

1. Optional auth gate: `Program.IsUserCanSync(Server)` shows `frmLogin` against the Pracnet security DB and checks the `Synchronization` right (`Program.cs:163`).
2. Writes a DB audit log entry ("Medinet Sync ... Run sync from <client>").
3. Pracnet DB sync: builds a `SyncCase`, `Server = Server.CreatePeer(Server.PracnetDatabase)`, `Client = Client.CreatePeer(Client.PracnetDatabase)`, `ScopeName = "Medinet"`, then `Synchronize(Direction)`. (If `CopyPracnet` is set it instead backs up and restores the whole Pracnet DB - a full copy rather than an incremental sync.)
4. Medinet DB sync: another `SyncCase` over the two `MedinetDatabase` catalogs, again scope `"Medinet"`, with `DuplicateDataWhenConflict` taken from `Abaki.Sync.Properties.Settings.Default.DuplicateDataWhenConflict`.
5. File sync (only if `SyncDocuments`): wires progress/status handlers and calls `SyncFileUtil.DoBidirectionalSync(ClientDocPath, ClientId, ServerDocPath, ServerId[, filterTable])`.
6. Records `LastSync = DateTime.Now`.

Note that the DB sync direction is effectively hard-coded to bidirectional inside `SyncCase` regardless of `MDSync.Direction` - see the gotcha below.

#### 12.4.1 Database sync internals (SyncCase, SyncPeer, MedinetSyncPeer)

`SyncCase` (`SourceCode/Abaki.Sync/SyncCase.cs`) runs one database's sync:

- `Synchronize(SyncDirectionOrder dir = DownloadAndUpload)` validates both peers hold the scope (`Server.IsScopeValid(ScopeName)` via `SqlSyncScopeProvisioning.ScopeExists`), creates a `SqlSyncProvider` on each side with `CommandTimeout = 0` (no timeout - deliberate, to survive long first syncs, comment attributed to a 2013 fix), optionally sets a batching directory and memory cache size, then runs a `SyncOrchestrator` with `LocalProvider = client`, `RemoteProvider = server`.
- Watch out: line 126 forces `orchestrator.Direction = SyncDirectionOrder.DownloadAndUpload` regardless of the `dir` argument. So even if `MDSync.Direction` is set to Upload-only or Download-only, the DB sync is always bidirectional. The direction field is effectively vestigial for DB sync.
- Conflict resolution: `clientProvider.ApplyChangeFailed` is handled in `clientProvider_ApplyChangeFailed`. For a `LocalUpdateRemoteUpdate` conflict:
  - If `DuplicateDataWhenConflict` is true, `ChangePKToApplyChange` inserts the conflicting local row under a brand-new GUID primary key (only when the table has a single `uniqueidentifier` PK and the rows genuinely differ), then `RetryWithForceWrite`. This deliberately duplicates rather than overwrites, so both edits survive.
  - Otherwise it just `RetryWithForceWrite` (remote wins / force apply).
  The server-side `ApplyChangeFailed` handler is commented out, so only client-apply conflicts are handled.

`SyncPeer` (`SyncCase.cs` peers, defined in `SyncPeer.cs`) is one database endpoint: builds the connection string (SQL or integrated auth, 30-minute connect timeout), exposes `CreateProvider(scopeName)` (`SqlSyncProvider` with `MemoryDataCacheSize = 100000`, `ApplicationTransactionSize = 50000`, `CommandTimeout` from settings), `IsScopeValid`, and `Provision(scopeName)`.

`MedinetSyncPeer` (`SourceCode/Abaki.Sync/MedinetSyncPeer.cs`) is the paired (Pracnet DB + Medinet DB) endpoint and owns provisioning:

- The set of tables that participate in the sync scope is not discovered dynamically from the whole schema; it is an explicit allow-list in the app config. `PracnetSyncTables` and `MedinetSyncTables` come from `Abaki.Sync.Properties.Settings.Default` (defaults declared in `SourceCode/Abaki.Sync/app.config`). Pracnet side is the `T*` demographic/appointment/billing tables (`TContacts`, `TPatients`, `TAppointments`, `TVisits`, `TSecurityUser*`, `TWorkCoverTACClaims`, ...). Medinet side is the `CN_*` clinical tables (`CN_TPROGRESS`, `CN_TPRESCRIPTION`, `CN_PATHOLOGY`, `CN_IMMUNISATION`, `CN_FILES`, `CN_LETTER_TEMPLATE`, ...). If you add a table that must replicate, add it to the appropriate list and re-provision - it will not sync otherwise.
- `Provision(...)` only enrols tables that have a primary key and at least one `uniqueidentifier` column; it also strips auto-increment `int` identity columns from the tracked description (so identity churn does not cause conflicts).
- `ReProvision*` drops the tracking objects and rebuilds the scope config via a `_temp` scope then swaps `scope_config` (see the raw SQL in `MedinetSyncPeer.ReProvision` and `MDSync.ReProvision`). Use this after schema changes.
- There is dormant patient-filtered sync support: `AddFilterData` creates a `Scope_Filter` table of patient GUIDs and the (currently commented-out) filter clause would restrict rows to those patients. The active provisioning path leaves filtering off; the `Synchronize(List<Guid>)` overload only actually filters the file sync (see below), not the DB rows.

The Sync Framework stores its own bookkeeping in each database: `scope_info`, `scope_config`, per-table `*_tracking` tables and `*_insert/update/delete` triggers plus `*_insert/update/delete/select` procedures. `MDSync.DropTrackingForTable` shows exactly which objects are created per tracked table - useful when a sync is corrupted and you need to tear it down.

#### 12.4.2 File sync internals (SyncFileUtil)

`SyncFileUtil.DoBidirectionalSync(pathA, replicaA, pathB, replicaB, filterFiles = null)` (`SourceCode/Abaki.Sync/SyncFileUtil.cs`) syncs the physical document files (the zipped documents/letters that back the DB records) between the two document roots using `FileSyncProvider`:

- Metadata lives in a local folder: `ClinNetSetting.Instance.SyncFileMetadata`, defaulting to `<AppBase>\SyncMetadata`, in a metadata file named `MedinetFileSync.metadata`. A `Temp` folder under the app base is used for staging. If you delete `MedinetFileSync.metadata` the next sync re-scans everything as new.
- `providerA` (remote) is a plain `FileSyncProvider(replicaA, pathA, filter, FileSyncOptions.None)`. `providerB` (local) is the metadata-backed provider bound to the metadata folder/file and temp dir.
- Optional filtering: when `filterFiles` is supplied (a DataTable of `FileName` values), only those files are included. `MDSync.Synchronize(List<Guid> filterPatients)` builds that table by querying `SELECT FileName FROM CN_FILES WHERE PatientGUID IN (...)` on the client's Medinet DB - i.e. patient-scoped document sync.
- `providerB.DetectChanges()` runs, conflict policy is `ApplicationDefined`, and a `SyncOrchestrator` runs `Direction = DownloadAndUpload` with `LocalProvider = providerB`, `RemoteProvider = providerA`.
- Conflict resolution for files: `DestinationCallbacks_ItemConstraint` calls `e.SetResolutionAction(ConstraintConflictResolutionAction.RenameSource)`. So when the same file path conflicts, the incoming (source) file is renamed rather than overwriting the destination - no document is silently lost. This is the "RenameSource" policy referenced elsewhere.
- Progress/stats are surfaced via static events `OnProgressChanged` and `OnStatusReport`, which `MDSync` subscribes to for the progress bar and result text.

End-to-end file-sync direction: because both the DB and file syncs run `DownloadAndUpload`, the model is genuinely bidirectional - a document created on the laptop uploads to the server and vice versa, with rename-on-conflict.

Gotchas in Abaki.Sync:

- `MedinetSync.cs` (a legacy/demo variant, distinct from `MDSync.cs`) contains hard-coded sample server names, database names and SQL credentials in its constructor, and `MDSync.cs` has similar values inside a commented-out `#region Test` block. These are demo scaffolding, not live secrets, but do not copy them into anything and do not commit them to shared trackers - treat them as noise to be removed.
- `DuplicateDataWhenConflict` genuinely creates duplicate rows under new GUIDs. If a site reports "duplicate patients/records after sync", this flag is the first suspect.
- The forced `DownloadAndUpload` in `SyncCase` means the `Direction` config knob does not restrict DB replication.
- `CommandTimeout = 0` on the DB providers means a stuck sync will not self-abort.

### 12.5 One-way document backfill - Pracnet -> Medinet

`SourceCode/Abaki.LinkingMedinetPracnetLetterDocument/frmMainForm.cs` is a standalone tool (assembly in `Abaki.LinkingMedinetPracnetLetterDocument`) that pushes historical Pracnet documents/letters into Medinet's clinical DB and document store. This is a one-off / catch-up backfill, distinct from the ongoing file sync above.

Flow (`btnRun_Click` -> `CreateDataManuallyStartDoWork` on a `BackgroundWorker`):

1. Resolve connections. `frmMainForm_Shown` loads the Pracnet DB profile from the Pracnet install path (`LoadPracnetDatabaseInfo`) and the Medinet config from `CustomSetting.xml` at the Medinet install path (`LoadMedinetDatabaseInfo`). `OpenPracnetAndMedinetDatabaseConnections` then reads the Pracnet document root from `Clinic.Value WHERE Parameter='SharedStoragePath'` and the Medinet document root from Medinet's `Clinic.Value WHERE Parameter='General_DocumentPath'`, and populates `DatabaseConnectionManagement`.
2. Build the two GUID maps used to translate identifiers between systems:
   - `GetLinkingSecurityUsers` runs `SELECT GUID, MedinetUserGuid FROM SecurityUsers` -> dictionary Pracnet user GUID -> Medinet user GUID. Used to translate `CreatedGUID` / `UpdatedGUID` / `DeletedGUID`.
   - `GetPracnetMedinetContactGuidCache` runs `SELECT ... FROM Contacts WHERE MedinetContactGuid IS NOT NULL` -> dictionary Pracnet contact GUID -> (Medinet contact GUID, full name). If zero contacts are linked it throws "Pracnet and Medinet has not been linked yet" and tells the operator to run the Link Auto Existing Data tool first. Linking must precede document backfill.
3. `ConvertLetterDocumentFromPracnetToMedinet` reads the source rows:
   - `SELECT ... FROM Documents WHERE ISNULL(Tags,'') <> 'P2K RESULT'` - i.e. every Pracnet document except those tagged `P2K RESULT` (legacy Practice 2000 migration results are excluded). It first `COUNT(*)`s to size the progress bar.
   - For each row it constructs a Medinet `CN_FILE` entity (new `FILESGUID`), copying `DocTitle`, `DocType`, `DocDate`, `DateEntered`, `DocDesc`, `DocSource`, `DocSettings`, `FileName`, `Tags`, `TemplateType`, and the created/updated/deleted dates. `Archived` is set from whether `DeletedDate` has a value.
   - It translates GUIDs through the two maps: user GUIDs via `_pracnetMedinetUserGuids`; patient/provider/referring-provider/copy-to via `_pracnetMedinetContactGuids`, also stamping `PatientName` / `DoctorName` / `RefDoctorName` from the cached full name. A `CopyToGUID` becomes a `CN_FileCCRefDoc` (carbon-copy recipient) row.
   - Rows are added to `MedinetClinicContext.CN_FILE` / `CN_FileCCRefDoc` and `SaveChanges()` is called in chunks (`totalRecordLoops`) for progress and memory reasons, with a final flush after the loop.
4. Optional physical file copy (`chkCopyDocument`): `CopyDocument` copies the actual file from the Pracnet store to the Medinet store. Source subfolder is `Documents` or `Letters` depending on `DocSource == 2` (document vs letter), and both stores are date-partitioned into `Docs{yyyyMM}` folders (`GetCurrentFolder`). Files are copied by name into the matching Medinet `Docs{yyyyMM}` folder, creating it if absent.

Notes: the tool only writes into Medinet (one-way, Pracnet is read-only). It relies entirely on the linkage GUIDs already being populated - a document for an unlinked patient/provider will be created without a `PatientGUID`/`DoctorGUID` mapping. The tool's `catch` blocks also use `throw ex;` (stack reset). Both this tool and the Link Auto Existing Data tool have `private const bool IsTestMode = true;` left in the code - inspect whether it gates anything before trusting a run.

### 12.6 First-time linkage - Link Auto Existing Data

`SourceCode/Abaki.LinkingMedinetAdapterLinkAutoExistingData/frmMainForm.cs` is the tool operators run once when first connecting an existing Pracnet site to an existing Medinet site (or vice versa). It walks the existing records on both sides, matches them, and writes the cross-reference GUIDs (`Contacts.MedinetContactGuid` / `TContacts.PracnetContactGuid`, `SecurityUsers.MedinetUserGuid`) that everything else depends on. It reuses the same `DatabaseConnectionManagement` plumbing and `Linking*Business` classes described in 12.3, driving them in batch. It has an "only unlinked objects" mode and an optional "run update script" step. Run order at a site: Link Auto Existing Data (establish GUID links) -> document backfill / ongoing sync.

### 12.7 Single sign-on (Medinet -> Pracnet)

When Pracnet is launched from within Medinet, the user should not be asked to log in again. This is done through a named shared-memory segment, not through the linking or sync layers.

- The shared-memory key is derived from the Medinet executable path: `PracnetLocalSetting.GetClinicalSoftwareShareKey()` (`SourceCode/Abaki.Common.LocalSettings/PracnetLocalSetting.cs:237`) takes `MedinetInstalledPath`, strips it to directory + filename-without-extension, lowercases it and passes it through `GeneralUtils.GenerateGuid(...)`. So both products deterministically compute the same key from the shared install location.
- At startup `MainApp.AuthenticateUser` (`SourceCode/Pracnet B1/MainApp.cs:705`) calls `MedinetAuthenticate(...)` (`MainApp.cs:598`) before falling back to the interactive login form. `MedinetAuthenticate`:
  - Reads the user GUID that Medinet wrote into the shared-memory segment (`SharedMemory.Read(shareMemoryName, IntPtr.Size)`).
  - If empty, and the machine is domain-joined, it falls back to resolving the current Windows user via `DomainUserManager.GetADUserByName(Environment.UserName)` to a domain user GUID.
  - Looks the user up with `DapperContext.Instance.GetUser(userGuid, domainUserGuid)`, rejects `Admin` and archived/deleted staff, and on success builds a `CustomPrincipal` / `CustomIdentity`, sets the thread principal, initialises Pracnet data and the main form, and returns true (skipping `frmLogin`).
- If SSO fails, `AuthenticateUser` proceeds to the normal login form. On a successful interactive login it writes the user GUID back into the shared-memory segment (`SharedMemory.Write(MainApp.sharedMemory, GlobalInfo.UserInfo.UserID.ToString(), IntPtr.Size)`) so a subsequent relaunch can SSO.

So the SSO contract is: Medinet writes the logged-in user's Pracnet-security GUID into a shared-memory key derived from the Medinet install path; Pracnet reads it and auto-authenticates. The linking layer (12.3) is what makes those user/contact GUIDs meaningful across the two databases.

### 12.8 End-to-end data flow summary

- Real-time, per-record (only when `LinkingFile_EnableMedinetAdapter` is on): a demographic edit in Pracnet calls a `Linking*Service.Update*Info`, which runs `sp_UpdateMedinetPatient` (or the reverse from Medinet) against the other database and writes back the cross-reference GUID. Same-schema demographic data stays consistent on save. Runs in-process, no service to host.
- Batched replication (run manually or in the background via the `Abaki.Sync` executable): the allow-listed `T*` (Pracnet) and `CN_*` (Medinet clinical) tables are bidirectionally reconciled through the Sync Framework scope `"Medinet"`, and the physical document files are bidirectionally reconciled via `FileSyncProvider` with rename-on-conflict. This is how a laptop/branch and the server stay in step offline.
- One-off backfill: `Abaki.LinkingMedinetPracnetLetterDocument` pushes historical Pracnet `Documents` (excluding `Tags='P2K RESULT'`) into Medinet `CN_FILE` and copies the files into Medinet's `Docs{yyyyMM}` store, one way.
- Login: shared-memory SSO ties a Medinet session to a Pracnet session so the two products present as one to the user.

### 12.9 Where to look first when something breaks

- "Medinet tab / linking does nothing in Pracnet": check `LinkingFileSetting.Instance.LinkingFile_EnableMedinetAdapter` and the `LinkingFile_Medinet*` connection values loaded by `frmLogin.InitLinkingManagement`; check `StorageService.Instance.IsMedinetLoaded` and `MedinetSettingInstance`.
- "Sync fails / hangs": `Abaki.Sync` writes a log4net rolling log to a `Log\` folder next to its exe (`app.config`). Check scope validity (`scope_info`), `SyncConfig` in ClinNetSetting, and the `MedinetFileSync.metadata` file. Remember `CommandTimeout = 0`.
- "Duplicate records after sync": `DuplicateDataWhenConflict` setting.
- "Documents missing in Medinet": confirm linkage ran (contacts have `MedinetContactGuid`), then decide whether it is the backfill tool or the ongoing file sync in play; check the `Docs{yyyyMM}` folder partitioning and the `General_DocumentPath` / `SharedStoragePath` clinic parameters.
- "New table not replicating": it is not in `PracnetSyncTables` / `MedinetSyncTables` in the Sync app config, or the scope needs re-provisioning.
- "SSO not working": check `MedinetInstalledPath`, the derived shared-memory key (`GetClinicalSoftwareShareKey`), and that the user exists and is not `Admin`/archived in `DapperContext.GetUser`.

---

## 13. AIR and ACIR immunisation

This section covers how Pracnet reports childhood and adult immunisation encounters to the Australian Immunisation Register (AIR). It has two generations of code living side by side:

- The **modern PRODA REST path** (live). AIR is a set of JSON web services hosted behind the same Services Australia PRODA gateway as Medicare Online. Pracnet talks to it through `PrimaryClinic.MedicareOnline/Services/AIR/*` (the processors) and `Abaki.Pracnet.OnlineClaiming/OnlineClaimingHelperACIR.cs` (the orchestration), with the claiming queue and status transitions in `Abaki.Billing.Gui B1/ACIRServices.cs`.
- The **legacy HIC classic path** (dead). `Abaki.Billing.Bll B1/HICOnline/AcirClaimController.cs` builds "business objects" through a `Wrapper` and `SendContent(BusinessObject.GeneralImmunisationClaim)`. This is the old terminal-style HIC Online claiming that predates the REST APIs. A repo-wide search finds no callers of `AcirClaimController` outside its own file, so treat it as reference-only. Do not extend it. If you touch anything here you almost certainly want the modern path. One tell that it is stale: the date formatting uses `.ToString("DDMMYYYY")` (see `AcirClaimController.cs:189`), which is not a valid .NET custom format string (the real tokens are `ddMMyyyy`), so it would emit garbage if it were ever run.

Terminology note. Historically the register was the Australian Childhood Immunisation Register (ACIR), and the Pracnet code and database entities still carry the `Acir*` prefix everywhere (`AcirClaiming`, `AcirVoucher`, `AcirService`, `ClaimingQueueAcir`, `AcirClaimProcessLevel`). The register has since been renamed and broadened to the Australian Immunisation Register (AIR), and the web-service processors and payload models use the `AIR*` prefix. In this codebase `ACIR` and `AIR` refer to the same subsystem: `ACIR` is the internal/entity naming, `AIR` is the wire/API naming.

### 13.1 The AIR web-service processors

Location: `SourceCode/PrimaryClinic.MedicareOnline/Services/AIR/`. Each processor is a thin subclass of `AIRProcessorBase<TRequest, TResponse>` that hard-wires one AIR operation's URL, operation-name header value, and whether a Date-of-Birth subject id is sent.

The class hierarchy:

```
MedicareOnlineBaseService<TRequest,TResponse>   (ProdaHttpClient, GenerateTransactionId, HandleResponse/HandleErrorResponse)
  └─ MedicareClaimProcessor<TRequest,TResponse>  (Transmit(): validate → set TransactionId → AddHeaders → POST → HandleResponse)
       └─ AIRProcessorBase<TRequest,TResponse>   (AIR-specific dhs-* headers, dhs-subjectid = DOB)
            ├─ AddEncounterClaimProcessor        (record an encounter — the claim path)
            ├─ UpdateEncounterProcessor          (amend a previously recorded encounter)
            ├─ ImmunisationHistoryProcessor      (individual immunisation history details)
            ├─ IdentifyIndividualProcessor       (identify individual → returns individualIdentifier)
            └─ GetAuthorisationProcessor         (provider authorisation access list)
```

`RefDataVaccinesProcessor` is the odd one out: it does **not** extend the base classes. It issues a plain HTTP GET (all the others POST), so it re-implements the header block and `GenerateTransactionId` inline (`RefDataVaccinesProcessor.cs:45-61`). Keep this in mind if you change header conventions: you must update both `AIRProcessorBase.AddHeaders` and `RefDataVaccinesProcessor.Transmit`.

The operation URLs and operation-name header values are the source of truth for the AIR API versions Pracnet is certified against. They live in `SourceCode/PrimaryClinic.MedicareOnline/Models/StringConstants.cs:90-101`:

| Processor | URL constant (value) | Operation-name header constant (value) |
|---|---|---|
| `AddEncounterClaimProcessor` | `AIREncounterUrl` = `air/immunisation/v1.4/encounters/record` | `AIREncounterVersion` = `air-immunisation-encounter-record@1.4.0-eigw-post` |
| `UpdateEncounterProcessor` | `AIRUpdateEncounterUrl` = `air/immunisation/v1.3/encounter/update` | `AIRUpdateEncounterVersion` = `air-immunisation-encounter-update@1.3.0-eigw-post` |
| `ImmunisationHistoryProcessor` | `AIREncounterHistoryUrl` = `air/immunisation/v1.2/individual/immunisation-history/details` | `AIREncounterHistoryVersion` = `air-immunisation-history-details@1.2.0` |
| `IdentifyIndividualProcessor` | `AIRIdentifyIndividualUrl` = `air/immunisation/v1.1/individual/details` | `AIRIdentifyIndividualVersion` = `air-immunisation-individual-details@1` |
| `GetAuthorisationProcessor` | `AIRAuthorisationAccessUrl` = `air/immunisation/v1/authorisation/access/list` | `AIRAuthorisationAccessVersion` = `air-authorisation-access-list@1` |
| `RefDataVaccinesProcessor` | `AIRRefDataVaccineUrl` = `air/immunisation/v1/refdata/vaccine` | `AIRRefDataVaccineVersion` = `air-immunisation-refdata-vaccine@1` |

Gotcha: these operations are on **different API versions** (v1.4 for record, v1.3 for update, v1.2 for history, v1.1 for identify, v1 for authorisation and vaccine reference data). When Services Australia publishes a new AIR release, you typically only bump the operations that changed, and you must change the URL and the operation-name header together (the `@x.y.z` suffix in the operation name has to match the version in the path). The `-eigw-post` suffix on the record and update operation names denotes the External Information Gateway POST variant. There is a commented-out `-eigw-post` alternative on the vaccine reference-data constant (`StringConstants.cs:101`), left as a hint that the variant exists.

The `dhs-subjectidtype` header for AIR is always `"Date of Birth"` (`StringConstants.DOBSubjectIdType`, `StringConstants.cs:122`), and `dhs-subjectid` is the patient's date of birth formatted as an 8-character claiming string. Every processor is constructed with `patient.DOB.ToClaimingString8()` as the subject id. `GetAuthorisationProcessor` is the exception: it passes `string.Empty` as the subject id (it is provider-scoped, not patient-scoped) and constructs its base with `includeDOBSubjectType: false` so the DOB subject-type header is suppressed (`GetAuthorisationProcessor.cs:14`, `AIRProcessorBase.cs:25`).

The full AIR header block is set in `AIRProcessorBase.AddHeaders` (`AIRProcessorBase.cs:38-49`): `dhs-subjectid`, `dhs-auditIdType` = `"Minor Id"`, `dhs-auditid` (Minor/Location id), `X-IBM-Client-Id`, `dhs-productid`, `dhs-messageid` (a fresh `urn:uuid:`), `dhs-subjectidtype`, `dhs-correlationid` (the transaction id, `urn:uuid:`-prefixed if not already), and `dhs-restoperationname` (the operation-name value from the table). Note the AIR audit id type is `"Minor Id"`, whereas the non-AIR Medicare base uses `"Location Id"` (`MedicareOnlineBaseService.cs:40`).

### 13.2 Shared PRODA auth and X-IBM-Client-Id with Medicare Online

AIR does not have its own credentials or endpoint. It reuses the Medicare Online `MedicareOnlineConfiguration` verbatim. Every AIR processor is constructed with `MedicareOnlineClaimingHelper.Instance.GetMedicareOnlineConfiguration()`, the same call the Bulk Bill, DVA, IMC and OVS claim paths use.

`GetMedicareOnlineConfiguration` (`SourceCode/Abaki.Pracnet.OnlineClaiming/MedicareOnlineClaimingHelper.cs:98-140`) builds the config with:

- `Token` = a PRODA OAuth access token from `ProdaAuthenticationService` (constructed against `MedicareOnlineSetting.Instance.ProdaAuthenticationEndpoint`, `MedicareOnlineClaimingHelper.cs:26`). The helper caches the token and only requests a new one when the cached one is near expiry (the `PercentageExpiredTime = 0.8` guard). `GetMedicareOnlineConfiguration()` (no argument) does a defensive re-fetch with `forceRequestNewToken: true` if the token came back empty (`MedicareOnlineClaimingHelper.cs:100-104`).
- `MedicareOnlineClientId` = `MedicareOnlineSetting.Instance.MedicareClientId` — this is the value sent as the `X-IBM-Client-Id` header. It is the software-vendor client id issued by the Services Australia Software Developers Portal, shared across every Medicare Online and AIR call. See the memory note on PRACNET-3005: a 401 "Cannot find valid subscription" from AIR or Medicare almost always means this client id is wrong for the environment, so check it against the Services Australia production doc first.
- `BaseUrl` = `MedicareOnlineSetting.Instance.MedicareEndpoint` (the shared gateway base). The processor's `ProdaHttpClient` is constructed with `BaseUrl` + `Token`, and the per-operation URL from the table is appended.
- `ProductId` = `"{productName}{productVersion}"`, `MinorId` = `LocationId` = `GlobalInfo.LocationId`.

The practical consequence: if PRODA auth or the client id is broken, AIR and Medicare Online break together. There is no separate AIR toggle. `MedicareOnlineConfiguration.IsValid()` requires all of `BaseUrl`, `Token`, `LocationId`, `ProductId`, `MinorId` and `MedicareOnlineClientId` to be non-empty (`MedicareConfiguration.cs:47-52`).

Secrets note: the PRODA signing key, device credentials and client id are provisioned/configured through the PRODA authentication service and `MedicareOnlineSetting`, not hard-coded in this subsystem. Do not paste live client ids or tokens into tickets or logs.

### 13.3 How an encounter is built and transmitted (the claim path)

The entry method is `OnlineClaimingHelper.SendGeneralHistoryImmunisationClaim` in `SourceCode/Abaki.Pracnet.OnlineClaiming/OnlineClaimingHelperACIR.cs:86`. It is a `static partial` extension of the shared `OnlineClaimingHelper`. End-to-end flow for a real (non-queue) send:

1. **Load and refresh.** Fetch the `Immunisation` entities for the given GUIDs from `NewCommonContext`, then `RefreshImmunizations` calls `Refresh(StoreWins, ...)` on each so the send works against database-current values (`OnlineClaimingHelperACIR.cs:28-40, 89-90`).
2. **Resolve the AIR provider number.** `GetProviderNumber` (`OnlineClaimingHelperACIR.cs:42-65`) prefers the `ProviderBusinessAddress.ProviderNum` for the information provider, then falls back to `BusinessAddress.AIRProviderNumber`. This is the information provider (the payee/practice), distinct from the per-episode immunisation provider (the vaccinator).
3. **Build the payload.** `CreatePayload` (`OnlineClaimingHelperACIR.cs:262-358`) constructs an `AddEncounterRequestType` with three parts:
   - `Individual` (`IndividualIdentifierType`): personal details (name, DOB, gender, initial), Medicare card number + IRN, address (postcode left-padded to 4), IHI number, and an ATSI indicator derived from `patient.ATSI` mapping `eATSI.Aboriginal`/`TorresStraitIslander` → `Y`, `eATSI.None` → `N`, else null.
   - `Encounters`: assembled by `AssembleEncounters` (`OnlineClaimingHelperACIR.cs:187-260`). Immunisations are grouped into encounters keyed by distinct (DateOfImmunisation, SchoolId, CountryAdministered, AdministeredOverseas, IsAntenatal). Each grouped immunisation becomes an `EpisodeType` (vaccine code, batch, dose, funding type, route of administration). The per-episode immunisation provider (vaccinator) and their HPI-I/HPI-O are attached at encounter level.
   - `InformationProvider` (`ProviderIdentifierType`): provider number, HPI-I (`GetHPIINumber`), HPI-O (`RecallGlobalSetting.Instance.HIService_HPIO`).
   - Empty sub-objects are nulled out before send so the JSON omits them (the request is serialised with `NullValueHandling.Ignore` in `MedicareClaimProcessor.ToMedicareWebService`, `MedicareClaimProcessor.cs:65-69`).
4. **Build the process-level claim object.** `AcirClaimProcessLevel.CreateClaimObject(imms, request, ...)` mirrors the payload into the internal `AcirClaiming` / `AcirVoucher` / `AcirService` structure so it can be persisted and displayed (`AcirClaimProcessLevel.cs:28-65`). `LoadProperties(mClaim)` then copies those into the `AcirClaiming` entity.
   - **PRACNET-3018/3019 gotcha (AutoMapper purge).** `LoadProperties` used to `Mapper.Map` the voucher/service model objects onto the entities. That was replaced by the explicit hand-written copies `ToAcirVoucher` and `ToAcirService` (`AcirClaimProcessLevel.cs:114-220`) because the static AutoMapper registration has a race-prone double-checked-locking init and the ACIR path never warmed it, so a post-Transmit `AutoMapperMappingException` could leave the claim half-persisted. When you add a field to `AcirVoucher`/`AcirService`, add it to these copy helpers by hand — there is no convention-mapping fallback any more. These helpers are `internal` and reachable from `Test.Abaki.Billing.Dal` via `InternalsVisibleTo`; `TestAcirLoadProperties.cs` covers field fidelity.
5. **Transmit.** Construct `AddEncounterClaimProcessor(medicareSetting, DOB8)` and call `Transmit(request)` (`OnlineClaimingHelperACIR.cs:117-119`). `MedicareClaimProcessor.Transmit` validates the request, generates a transaction id if none, applies headers, POSTs via `ProdaHttpClient`, and deserialises the response (or an error response) into `AddEncounterResponseType`.
6. **Handle the response.** `HandleResponse` (`OnlineClaimingHelperACIR.cs:458-488`) branches on `resp.StatusCode`:
   - `AIR-I*` (informational/success) → `SetClaimStatus(..., eClaimStatus.Transmitted, ...)` and soft-delete the queue entries. `Transmitted` = 3.
   - `AIR-E*` (error) → `SetClaimStatus(..., eClaimStatus.TransmitFailed, ...)`, and error text/codes are unpacked into `ErrorObject`s and an `AIRClaimLevelDisplayModel` for the UI.
   - anything else (for example `AIR-W-1004`) → `SetClaimStatus(..., eClaimStatus.ExceptionsToReview, ...)`. This is the "accept and confirm" review path (see 13.5).
   `SetClaimStatus` (`OnlineClaimingHelperACIR.cs:528-631`) records `PmsClaimId` (the AIR-assigned claim id), status, sent date, per-voucher/per-episode returned codes, and returned vaccine echo fields.
7. **Propagate status to the source rows.** After a real send, the loop at `OnlineClaimingHelperACIR.cs:123-128` sets `imm.SentClaimStatus = mClaim.Status` and the linked `imm.Invoice.SentClaimStatus`. Then `SaveChanges` persists if there were no request errors.

Encounter dates on the wire use `ToClaimingString8()` (the DDMMYYYY 8-char claiming format), not the invalid legacy `DDMMYYYY` .NET format used in the dead `AcirClaimController`.

### 13.4 The immunisation claiming queue

The queue front end lives in `SourceCode/Abaki.Billing.Gui B1/ACIR/` (forms `frmNewAcirClaim`, `frmAcirClaim`, `frmAcirClaimDetail`, `frmUpdateEncounter`, and the search control `ImmunSearchCtrl`). The orchestration is `ACIRServices.cs`.

`ACIRServices.CreateAcirClaim(lstImmView, isQueue, onePerClaim, ...)` (`ACIRServices.cs:1361`) is the batch entry point:

1. **Un-batch anything already batched.** Immunisations currently at `SentClaimStatus == Batched` (2) that are being re-selected are reset to `null` (both the `Immunisation` and its `Invoice`), and their existing `ClaimingQueueAcir` rows soft-deleted (`ACIRServices.cs:1369-1405`). This lets a user reshape a batch.
2. **Build the queue.** `CreateGeneralHistoryClaimQueue` groups the immunisations, creating one `AcirClaiming` per (patient, servicing provider, information provider) group, split into packages of at most `NumberOfVouchersInClaimContants.Acir_MaxNumberOfVouchers` vouchers (`ACIRServices.cs:1481-1522`). Each claim starts at `Status = Batched` (2), with a `ClaimingQueueAcir` row per immunisation, and each immunisation's `SentClaimStatus` set to `Batched`. `onePerClaim` splits every immunisation into its own single-item claim.
3. **Optionally transmit immediately.** If `!isQueue`, each built claim is sent via `SendAcirClaim` right away (`ACIRServices.cs:1423-1432`). If `isQueue`, the claims are left `Batched` for a later run (batch/auto-claim).
4. `SaveChanges` and return the claims that reached `Transmitted`.

`SendAcirClaim` (`ACIRServices.cs:1534`) is the per-claim transmit wrapper. It rejects claims where any immunisation is missing a vaccine dose (sequence number), then delegates to `OnlineClaimingHelper.SendGeneralHistoryImmunisationClaim` and `SaveChanges`.

Query-side filtering for the queue/selection grid uses `AcirNewClaimCriteria` (`SourceCode/Abaki.Data/Domains/Entities_Billing/AcirNewClaimCriteria.cs`). Its `ToSqlCondition` builds a WHERE clause; the relevant bit is the **optional** `SentClaimStatusIsNull` flag which, when set, adds `AND (Immunisations.SentClaimStatus is null OR Immunisations.SentClaimStatus = 0)` (`AcirNewClaimCriteria.cs:57-58`). Note "already claimed" is encoded as `SentClaimStatus` being non-null and non-zero; `0` (the enum `All`) is treated the same as null/unclaimed.

### 13.5 Read-side operations: identify, history, update, accept-and-confirm

These are all driven from `ACIRServices.cs` and share the same PRODA config.

- **Identify individual.** `GetIndividualIdentifier(patient, informationProviderGuid, out message)` (`ACIRServices.cs:499`) and a sibling call at `ACIRServices.cs:463` construct `IdentifyIndividualProcessor` and POST an `IdentifyIndividualRequestType`. AIR returns an `individualIdentifier` (an opaque token for the person) plus catch-up date, indigenous status, and various indicators in `IndividualDetailsResponseType` (`Models/AIR/IndividualDetailsResponseType.cs`). The `individualIdentifier` is required by the history and update operations.
- **Immunisation history.** `ImmunisationHistoryProcessor` is used inside `SendUpdateEncounter` (`ACIRServices.cs:304-318`) to look up the AIR immunisation-encounter sequence number (`ImmEncSeqNum`) for a given claim id + claim sequence number before an update can be sent. The request (`AirHistoryRequestType`) carries the `individualIdentifier` and the information provider.
- **Update encounter.** `SendUpdateEncounter` (`ACIRServices.cs:286`) amends a previously recorded encounter: it resolves the individual identifier, fills the missing `ImmEncSeqNum` from history if needed, builds an `UpdateEncounterRequestType` (claim id, sequence numbers, encounter date, school id, overseas/antenatal flags, episodes), and transmits via `UpdateEncounterProcessor`. On `AIR-I*` success it stamps the voucher and immunisation rows with the returned sequence numbers.
- **Accept and confirm / re-transmit / mark-as-resolve.** `ReTransmit` (`ACIRServices.cs:1561`) handles the three `eAcirClaimState` values (`ConfirmAccept=0`, `Retransmit=1`, `MarkAsResolve=2`, defined in `Abaki.Common.Enums/eAcirClaim.cs`). "Accept and confirm" re-sends via `TransmitConfirmAccept` with the original `TransactionId` and `Individual.AcceptAndConfirm = "Y"` (this is how you push through an `AIR-W`/exceptions-to-review response where AIR asked the operator to confirm a possibly-new individual). "Retransmit" clones the immunisations with fresh GUIDs, archives the originals, and re-queues them (see 13.6).
- **Vaccine reference data.** `RefDataVaccinesProcessor` is called from `Abaki.Core.Gui B1/Controls/SelectVaccineUpdateModelCtrl.cs:286` to refresh the AIR vaccine code list used when picking a vaccine.

### 13.6 The auto-re-claim guard (PRACNET-3019)

Background: a customer's immunisation was claimed successfully once, but its `Immunisation.SentClaimStatus` was not propagated after the send (suspected `AutoMapperMappingException` in the old `LoadProperties`). The next day the auto-batch re-selected the same vaccine, re-claimed it, and AIR rejected it as a duplicate (`AIR-E-0102`). PRACNET-3019 (PR #298, merged in `845ef476`) fixed this in three layers of defence:

- **Commit 1 (`878c7ce3`) — remove the failure mode.** Replaced `Mapper.Map` in `AcirClaimProcessLevel.LoadProperties` with the explicit `ToAcirVoucher` / `ToAcirService` copies (see 13.3 step 4), so the post-send persistence cannot throw an AutoMapper exception and abandon the status write.
- **Commit 2 (`ce468fd1`) — defensive re-check + re-attach.** After the status-propagation loop in `SendGeneralHistoryImmunisationClaim` (`OnlineClaimingHelperACIR.cs:130-177`), if the claim succeeded (`mClaim.Status != 0`) but an immunisation's `SentClaimStatus` is still null/0, it logs `[DIAG-3019]`, re-attaches the entity if EF detached it, and re-sets the status. The comment is explicit that this is not a guaranteed second-chance write (re-assigning in the same context that swallowed the first write may not stick) — its value is observability plus the detached-entity recovery.
- **Commit 3 (`2e8fb616`) — hard guard in the queue builder.** `CreateGeneralHistoryClaimQueue` now runs an **unconditional** filter (`ACIRServices.cs:1468-1476`) that drops any immunisation whose `SentClaimStatus` is already set (non-null, non-zero) before building a claim, logging the dropped count under `[DIAG-3019]` with advice to use Retransmit. This runs on every path into the queue builder (batch claim, auto-claim, one-per-claim, future callers), independent of the optional `AcirNewClaimCriteria.SentClaimStatusIsNull` flag. So even if the propagation path regresses again, an already-sent vaccine cannot be silently re-claimed.

Interaction with Retransmit (important gotcha): the hard guard would break the legitimate resend feature, because a retransmitted immunisation already has a non-zero `SentClaimStatus`. The Retransmit branch in `ReTransmit` (`ACIRServices.cs:1580-1624`) therefore **clones** each immunisation via `Immunisation.Copy()`, and because `Copy()` inherits `SentClaimStatus` from the source, it explicitly resets `newIm.SentClaimStatus = null` (`ACIRServices.cs:1596`) so the clone passes the guard as a fresh unclaimed row. The original is soft-deleted/archived and annotated with the clone's GUID so it does not reappear on the immunisation screen. If you ever change how cloning works, preserve this reset or you will silently break resends.

Diagnostic logging convention: the PRACNET-3019 markers use the `[DIAG-3019]` tag and are written to `ClaimingErrorLogger` (the claiming log, `Logs/ClaimingLog_*`), at `Warn` level. This mirrors the `[DIAG-3009]` convention documented in `.claude/rules/claim-transmit-conventions.md`; strip pre-release with a `git grep '\[DIAG-3019\]'`.

### 13.7 Status model and gotchas

- **Status enum.** `eClaimStatus` (`SourceCode/Abaki.Common.Enums/MedicareOnlineEnums.cs:50`) is shared across all claim types. AIR uses `Batched = 2` (queued, not sent), `Transmitted = 3` (accepted by AIR), `TransmitFailed`, `ExceptionsToReview` (the `AIR-W` accept-and-confirm path) and `PartlyTransmitted` (set in `ReTransmit` when some vouchers succeeded and some did not). `All = 0` is treated as "unclaimed" alongside null.
- **Status cast types.** `AcirClaiming.Status` is a `Byte` and is assigned via `Convert.ToByte((int)status)` in `SetClaimStatus` and via `(byte)eClaimStatus.X` in `ReTransmit`. `Immunisation.SentClaimStatus` is a `Nullable<Byte>`. This matches the wider claim-transmit rule that status casts must match the field's underlying type (see `.claude/rules/claim-transmit-conventions.md`).
- **AIR response prefixes drive everything.** The branch on `AIR-I` / `AIR-E` / else in `HandleResponse` is the single decision point that maps AIR's returned status code to a Pracnet claim status. `AIR-I-1007` is special-cased in `SetClaimStatus` to mark vouchers `SUCCESS` when the response has no encounter breakdown.
- **RefDataVaccinesProcessor divergence.** Because it does not extend the base classes, any change to headers, transaction-id format, or auth must be duplicated there.
- **Do not confuse information provider and immunisation provider.** The information provider (practice/payee) is at claim level; the immunisation provider (vaccinator) is at encounter level and may differ per episode. Both resolve HPI-I from the linked employee and HPI-O from `RecallGlobalSetting.Instance.HIService_HPIO`.
- **Overseas-administered vaccines** suppress the immunisation provider number and instead set `CountryCode`/`AdministeredOverseas` on the encounter (`OnlineClaimingHelperACIR.cs:207-210, 235`).

### 13.8 Spec and testing

AIR is an in-scope Medicare Online claiming path, so changes to the payload, the operation URLs/versions, or the printed/queue behaviour fall under the spec-compliance review rule (`.claude/rules/code-review-spec-compliance.md`): cite the relevant Services Australia AIR web-service specification and NOI test rows in the review. The AIR specs sit with the other Medicare technical specs in the shared GlobalHealth source drop (see the "Services Australia spec location" note in the root `CLAUDE.md`).

Tests: the modern ACIR path is largely EF-context-bound and covered by SIT plus code review, per the claim-transmit conventions. The one unit-tested slice is the PRACNET-3019 explicit-copy helpers, in `SourceCode/Test.Abaki.Billing.Dal/TestAcirLoadProperties.cs` (field fidelity, null handling, the non-copied `SerialNumber` field, `CreatedDate` stamping, nullable-date defaulting), reachable because `Abaki.Pracnet.OnlineClaiming` exposes internals to `Test.Abaki.Billing.Dal`.

---

## 14. Printing and Crystal Reports

Every printed artefact in Pracnet is a Crystal Report. Invoices, receipts, banking and transaction reports, debtor lists, doctor fee summaries, and all of the Medicare and DVA claiming forms (DB4, D1216, D1083, D695 and the online-claiming processing and payment reports) run through the same Crystal Reports runtime and the same small utility layer. This section covers that shared infrastructure: the runtime, the two projects that own it (`Abaki.CrystalReport` and `Abaki.Billing.Report B1`), how each report gets its database logon applied, how print versus preview is chosen, and the pattern for adding or editing a report. The claiming forms themselves (DB4 splitting, DVA form selection, spec compliance) are documented in the claiming sections and in the CLAUDE.md "Claiming Reports" block, and are only referenced here where they illustrate the shared plumbing.

### 14.1 The two projects

There are two projects and it matters which does what.

- `SourceCode/Abaki.CrystalReport/` (assembly `Abaki.CrystalReport`, target AnyCPU, .NET 4.5) is the **generic viewer and print engine**. It owns `ReportUtility` (the static print/logon helper), `frmReport` (the preview window), `ReportCtrl` (an embeddable preview UserControl), and the abstract `ReportInfo` base. It knows nothing about billing or claiming. It references the Crystal Reports 4.0 (SAP Crystal Reports 13.0.4000.0) assemblies from `SourceCode/Referenced Assemblies/Crystal Reports 4.0/` and carries an embedded `abaki.crystalreport.dll.licenses` file (the design-time licence for the Crystal viewer control).

- `SourceCode/Abaki.Billing.Report B1/` (assembly `Abaki.Billing.Report`) owns the **actual `.rpt` report definitions** (roughly 80 of them, all prefixed `rpt`) plus the strongly-typed `ReportHelper` facade and the billing-specific `ReportInfo` subclasses (`InvoiceReportInfo`, `ReceiptReportInfo`, `InvoiceListReportInfo`, `DeptorReportInfo`, `DoctorFeeReportInfo`, and so on in `BillingReportInfo`). This is where you go to add or change a report definition.

Callers in the GUI layer (billing screens, the claiming screens, the banking screen) almost always go through `ReportHelper`, never directly to `ReportUtility`. `ReportHelper` builds the `ReportDocument`, sets its record-selection formula and parameters, applies the DB logon, then hands off to `ReportUtility.PrintReport` or `frmReport.ShowReport`.

### 14.2 Runtime dependency and version pinning

Crystal Reports is an out-of-process COM/managed hybrid runtime that must be installed on the workstation (SAP Crystal Reports runtime for Visual Studio, 13.0.4000). The project references pin `SpecificVersion=False` against the DLLs shipped in `SourceCode/Referenced Assemblies/Crystal Reports 4.0/`, so the build uses the checked-in assemblies, but at run time the report engine still needs the matching Crystal runtime installed and registered. If a machine shows reports failing to render or an assembly-load error at first preview, the missing Crystal runtime is the usual cause. There is no NuGet package for this; it is a machine-level install and is expected to be present on any dev or client box.

### 14.3 Database logon: how each report gets its connection (`ReportUtility.ApplyLogon`)

Crystal `.rpt` files embed the connection details that were present when the report was authored in the designer. Those are almost never the credentials the running client should use, so **every report must have its logon overridden at run time before it is rendered**. This is the single most important invariant in this subsystem.

The flow is two-stage:

1. **At login**, `MainApp` seeds the static connection fields once from the active profile. See `SourceCode/Pracnet B1/MainApp.cs:549`:

   ```csharp
   Abaki.CrystalReport.ReportUtility.SetConnectionInfo(_currentProfile.MNDataSource,
       _currentProfile.MNDemographicCatalog, _currentProfile.MNSqlUsername, _currentProfile.MNSqlPassword);
   ```

   `frmMain2.cs:2638` re-seeds the same values if the profile changes. `SetConnectionInfo` just stores server, database, user id and password in the private static fields on `ReportUtility` (`SourceCode/Abaki.CrystalReport/ReportUtility.cs:23`). Note there are placeholder default values hardcoded in the field declarations at the top of `ReportUtility.cs`; they are only a fallback and are overwritten by the login seeding. Treat those literals as a legacy default that should not be relied on and should never be copied elsewhere. Do not add new hardcoded credentials here.

2. **Per report**, immediately after building the `ReportDocument` and before rendering, the caller calls `ReportUtility.ApplyLogon(rpt)` (`ReportUtility.cs:31`). `ApplyLogon` walks **every table in `rpt.Database.Tables`**, rebuilds a `ConnectionInfo` from the seeded fields, assigns it to each table's `LogOnInfo`, calls `tbl.ApplyLogOnInfo(...)`, resets `tbl.Location = tbl.Name`, then finally calls `rpt.SetDatabaseLogon(...)` on the whole document.

The per-table loop is deliberate and load-bearing. Setting the document-level logon alone is not reliable for reports that contain multiple tables or subreports; each table object carries its own connection. Resetting `tbl.Location = tbl.Name` also matters: if a report was saved with a fully-qualified location (for example `CatalogName.dbo.ViewName`) that does not exist in the target database, Crystal throws a "logon failed" or "table could not be found" error at render time. Forcing the location back to the bare object name lets the freshly-applied catalog resolve it. When a report will not render and the message mentions logon or a missing table, check that `ApplyLogon` is being called and that the underlying SQL view actually exists in the profile database.

Note there is a *second* `ApplyLogon` that appears in a commented-out line in `SourceCode/Test.Abaki.Billing.Dal/TestReport.cs` (`ReportHelper.ApplyLogon`). That is stale; the live method is `ReportUtility.ApplyLogon`. There is only one real implementation.

### 14.4 Print versus preview (`ReportUtility.PrintReport` and `frmReport`)

`ReportUtility.PrintReport` is heavily overloaded (single report, list of reports, and a `ReportInfo`-driven overload) but they all share one branch: a `bool toPrinter` parameter chooses between **direct-to-printer** and **on-screen preview**.

- `toPrinter == true`: the method reads `LocalSetting.Instance.PracnetPrinters` and picks either `MedicarePrinter` or `BillingPrinter` (chosen by the `isMedicareReport` flag). It then runs `FormatPagePrinting` to apply orientation (portrait/landscape) and page margins from the stored `PageSettings` (margins are stored in the settings as small integers and multiplied by 10 to reach Crystal's hundredths-of-an-inch units), optionally sets a copy count (clamped to `byte.MaxValue`), and calls `rpt.PrintToPrinter(printerSettings, pageSettings, false)`. Printing is spooled through the OS print system, not previewed.

- `toPrinter == false`: the method opens the preview window via `frmReport.ShowReport(...)`, which shows the report modally and lets the user review, navigate, zoom, export, email, fax, or hit the in-window Print button.

The two configured printers (`MedicarePrinter`, `BillingPrinter`) live on `PracnetPrinters` in `SourceCode/Abaki.Common.LocalSettings/PracnetPrinters.cs`. They are separate so a practice can route claiming forms (DB4, DVA) to a dedicated tray or printer distinct from ordinary invoices and receipts. The `isMedicareReport` flag threaded through `PrintReport` is what selects between them. Most claiming calls in `ReportHelper` pass `isMedicareReport: true` (the eighth positional argument).

Timing of each print is logged to the standard `ErrorLogger` at `Info` level ("Begin print to printer" / "End print to printer" with elapsed milliseconds), which is useful when diagnosing slow printing complaints.

### 14.5 Audit logging of prints

`PrintReport` also records a print audit entry. After a successful print (or after the preview window reports it was printed via `frmReport.IsPrinted`), it calls `AbakiAuditLog.SavePrintLog(...)` with the audit object GUIDs, the `eAuditLogObjectType`, and the current login GUID and username. Callers that do not need auditing pass `null` for the GUID list and `eAuditLogObjectType.Unknown`; most of the claiming report calls in `ReportHelper` do exactly that (their comment reads "No need to pass audit values"). If you add a new report that prints a patient-identifiable document, thread the real object GUIDs and object type through so the print is auditable.

`frmReport.IsPrinted` is a `static bool` that is reset to `false` at the start of `ShowReport` and set to `true` when the user clicks Print inside the preview. Because it is static it is a single shared flag; it works only because the preview is modal. Do not rely on it across concurrent report windows.

### 14.6 The preview window: `frmReport`

`SourceCode/Abaki.CrystalReport/frmReport.cs` is the modal preview form. Key points for a maintainer:

- It can host **one report or a list of reports**. Internally it holds `List<ReportDocument> _rpts` and builds one `CrystalReportViewer` per report into `_viewers`. When you print several claim forms in one action they are shown as a sequence of viewers with the navigation buttons walking across viewer boundaries (`btNavigator_Click` at `frmReport.cs:269` steps to the next/previous viewer when it runs off the end of the current one).
- Every viewer is created with the built-in Crystal toolbar suppressed (`ShowPrintButton = false`, `ShowExportButton = false`, `ToolPanelView = None`, and so on) because the form supplies its own Infragistics toolbar for zoom, page navigation, print, export (PDF/Word/Excel via `ExportToDisk`), email and fax.
- Export targets are hardcoded in `OnExport`: PDF (`PortableDocFormat`), Word (`WordForWindows`, `.doc`) and Excel (`.xls`).
- Email and fax buttons are only enabled when the `ReportInfo` supplies a `SendMail` / `SendFax` delegate (`frmReport_Load` at `frmReport.cs:155`). These are `Func<ReportInfo,bool>` hooks set by the caller.
- `Esc` closes the window (`ProcessCmdKey`).
- `frmReportLoadOnDemand<T>` is a subclass that adds a "load by item" combo so a large multi-record report can be re-queried per selected item via a `DemandItemChanged` event and `currentViewer.RefreshReport()`.

`ReportCtrl` (`SourceCode/Abaki.CrystalReport/ReportCtrl.cs`) is a near-duplicate of `frmReport`'s viewer logic packaged as an embeddable `UserControl` (used where a report preview must sit inside another form rather than pop up modally, for example the invoice-layout sample preview). It has its own copy of the zoom/navigation/export handlers. Be aware the two share almost identical code; a fix in one usually needs mirroring in the other.

### 14.7 The startup warm-up hack (`InitilizeCrystalReport`)

Crystal Reports has a slow first-render cost the first time the engine is touched in a process. `MainApp.InitilizeCrystalReport()` (`SourceCode/Pracnet B1/MainApp.cs:876`, called at `MainApp.cs:556` during startup) deliberately absorbs that cost up front: it constructs an empty `frmReport(new List<ReportDocument>())`, positions it off-screen at `(10000, 10000)` at 200x200, keeps it out of the taskbar, shows it, and immediately closes and disposes it in the `Shown` handler. The user never sees it. The whole method is wrapped in a try/catch that just logs "Pre-load crystal report error" so a warm-up failure never blocks login. If you see a throwaway off-screen report window in the startup path, this is intentional and should not be "cleaned up".

### 14.8 The `ReportInfo` pattern for general (billing) reports

For invoices and receipts the code uses the `ReportInfo` abstraction rather than raw `ReportDocument` calls. `ReportInfo` (`SourceCode/Abaki.CrystalReport/ReportInfo.cs`) is abstract and defines the contract:

- `BuildReport()` returns a fully prepared `ReportDocument`.
- `CreateReportDocument()` (protected) chooses which concrete `.rpt` class to instantiate.
- `SetSelectionFormular(rpt)` (protected) sets the `RecordSelectionFormula` and parameters.
- It carries `ToPrinter`, `PageSettings`, the `SendMail` / `SendFax` delegates, and the print/email/fax/export timestamp fields, plus a `CheckAndSaveLog()` hook.

The billing subclasses live in `SourceCode/Abaki.Billing.Report B1/InvoiceReportInfo.cs`:

- `BillingReportInfo` (abstract) implements `BuildReport()` once for the whole family. It calls `CreateReportDocument()`, then sets the large shared set of parameters (totals, GST, payment-method flags read out of `DataDefinition.ParameterFields`, the customisable invoice/receipt layout items from `RecallGlobalSetting.Instance.CustomiseInvoiceReceiptLayout`, overdue warnings), then calls the subclass `SetSelectionFormular(rpt)`, then finishes with `ReportUtility.ApplyLogon(rpt)`. So an `InvoiceReportInfo` produced report is fully logon-applied by the time `BuildReport()` returns.
- `InvoiceReportInfo` picks between `rptInvoiceSample`, `rptInvoice2b` (single), `rptInvoice2` (default), and `rptInvoice3` (invoice-only, no receipt) in `CreateReportDocument()`, and builds an `InvoiceReportView`-based selection formula from a list of invoice GUIDs. It also applies letterhead fonts to the `LetterHead.rpt` subreport in `ApplyHeaderStyle` (an example of reaching into a subreport's report objects at run time).
- `ReceiptReportInfo` picks between grouped-by-invoice / grouped-by-receipt / grouped-by-payer receipt templates based on `RecallGlobalSetting.Instance.ReceiptTemplateForPrinting`.

When you build one of these, the `ReportUtility.PrintReport(ReportInfo info, ...)` overload (`ReportUtility.cs:262`) calls `info.BuildReport()` for you and honours `info.ToPrinter` for the print/preview branch.

### 14.9 `ReportHelper`: the strongly-typed facade for claiming and other reports

`SourceCode/Abaki.Billing.Report B1/ReportHelper.cs` is a static class of one method per report action (roughly 30 methods). The methods that do not use the `ReportInfo` pattern all follow the same recipe, and this is the recipe to copy when adding a claiming or list report:

1. `new rptXxx()` (the generated Crystal wrapper class for the `.rpt`).
2. Build a Crystal record-selection formula string, typically `"{SomeReportView.GUID} IN [ '{guid1}', '{guid2}' ]"`, and assign `rpt.RecordSelectionFormula`. Note the GUID literals are wrapped in braces inside single quotes because the underlying SQL views store GUIDs in the `{...}` string form.
3. `rpt.SetParameterValue("LocationID", locationID)` and any other parameters.
4. `ReportUtility.ApplyLogon(rpt);` (mandatory, per 14.3).
5. `ReportUtility.PrintReport(rpt, toPrinter, ... , isMedicareReport: true)` and then `rpt.Dispose()`.

`GetClaimingInvoiceReportDocument` (`ReportHelper.cs:39`) is the claiming-form selector: it switches on `eClaimTypeReport` to instantiate the right DB4 / DVA `.rpt` variant (DB4, DB4_MBS, DB4_Pathology, DB4_DI, DVA_D1216, DVA_D1083, DVA_D695, else DVA_D1216S). The DB4 split (Path / DI / MBS post-assignment) is claiming logic covered elsewhere; from a printing-plumbing view the point is simply that report *selection* happens here and everything downstream is the same `RecordSelectionFormula` + `ApplyLogon` + `PrintReport` shape.

Reports that are fed from an in-memory object list rather than a SQL view use `rpt.SetDataSource(list)` instead of a selection formula (see `ShowDeptorReport`, `ShowDoctorFeeRateReport` at `ReportHelper.cs:427`). Those do not call `ApplyLogon` because there is no database table to log onto.

### 14.10 Adding or editing a report: the procedure

To **add** a report:

1. Create the `.rpt` in the Crystal Reports designer (in Visual Studio) under `SourceCode/Abaki.Billing.Report B1/`. Point its data source at the appropriate SQL view. If the report needs new data, add or alter the SQL view under `Database/PracnetDatabaseFrom0.1.6/Revision_*` as a migration rather than editing the database by hand.
2. Confirm the project picked up both the generated `rptXxx.cs` and the `rptXxx.rpt`. In `Abaki.Billing.Report.csproj` each report needs a `<Compile>` entry with `<DependentUpon>rptXxx.rpt</DependentUpon>` and an `<EmbeddedResource>` entry for `rptXxx.rpt` with `<Generator>CrystalDecisions.VSDesigner.CodeGen.ReportCodeGenerator</Generator>` and `<LastGenOutput>rptXxx.cs</LastGenOutput>`. (See `rptDB42` at csproj lines 444 and 708 for the canonical shape.)
3. Add a `ReportHelper` method following 14.9, or a `ReportInfo` subclass following 14.8.
4. Wire the caller in the GUI. Do not skip `ApplyLogon`.
5. Follow the unit-test rule: the calculation/selection logic (for example the selection-formula builder or the DB4 type router) belongs in a testable helper so it can be covered in `Test.Abaki.Billing.Dal` without a live Crystal engine. The `.rpt` itself and the WinForms viewer are exempt (UI / third-party), but the routing that decides *which* report and *what* selection formula is not.

To **edit** an existing report, open the `.rpt` in the designer, make the change, then run the mandatory csproj integrity check in 14.11.

### 14.11 The designer-drops-other-reports csproj hazard (read this before editing any .rpt)

This is the single biggest footgun in the subsystem, and there is a dedicated rule for it at `.claude/rules/crystal-reports-csproj.md`.

When you open and save **one** `.rpt` in the Visual Studio Crystal designer, the designer can silently **remove the `<Compile>` and `<EmbeddedResource>` entries for *other* reports** from `Abaki.Billing.Report.csproj`. The `.cs` and `.rpt` files stay on disk but they drop out of the build, and you get a compile error like `rptDB42 could not be found` that has nothing to do with the report you were actually editing. This has happened in the repo: editing `rptDB4_MBS.rpt` in the designer removed `rptDB42`'s entries.

Mandatory workflow after any `.rpt` edit:

1. `git diff -- "SourceCode/Abaki.Billing.Report B1/Abaki.Billing.Report.csproj"`.
2. Look specifically for **removed** `<Compile>` or `<EmbeddedResource>` lines for reports you did not touch. The diff is easy to miss because the designer also legitimately touched the file for the report you meant to edit.
3. Restore any wrongly-removed entries before committing.
4. Build the project to confirm no "report could not be found" errors.

Each report must have both pairs of entries (Compile with `DependentUpon`, EmbeddedResource with `LastGenOutput`) as shown in 14.10. If a report is on disk but the build cannot see it, this is almost certainly why.

### 14.12 Gotchas checklist

- **Missing `ApplyLogon` is the number-one cause of render failures.** A report authored against a different server or catalog will "logon failed" until `ApplyLogon` overrides its embedded connection per table. Every render path must call it (unless the report is fed by `SetDataSource`).
- **Crystal runtime must be installed on the machine.** Build success does not guarantee render success; the SAP Crystal runtime 13.0.4000 is a separate machine-level install.
- **Do not add hardcoded credentials.** The connection is seeded at login through `SetConnectionInfo` from the profile; the literal defaults at the top of `ReportUtility.cs` are legacy fallbacks, not the intended source.
- **`frmReport.IsPrinted` is static and only safe because the preview is modal.** Do not read it across concurrent windows.
- **Two printers, not one.** `isMedicareReport` selects `MedicarePrinter` vs `BillingPrinter`; passing the wrong flag routes a claiming form to the invoice printer or vice-versa.
- **Margins are stored small and multiplied by 10** in `FormatPagePrinting`; do not double-scale them if you refactor that method.
- **Dispose report documents.** `ReportHelper` disposes each `rpt` after printing and the list overloads call `GC.Collect()`; Crystal `ReportDocument` holds unmanaged handles and leaks add up over a long session. Keep the dispose calls when editing these methods.
- **`ReportCtrl` mirrors `frmReport`.** A bug fixed in the preview form usually needs the same fix in the embedded control.
- **Selection-formula GUID quoting.** GUIDs go into the formula wrapped as `'{guid}'` because the SQL views store them in braced-string form; a plain `'guid'` will silently match nothing and the report will render empty.

### 14.13 Where things live (quick reference)

| Concern | File |
|---|---|
| Print engine, per-table logon, print/preview branch | `SourceCode/Abaki.CrystalReport/ReportUtility.cs` |
| Preview window (modal) | `SourceCode/Abaki.CrystalReport/frmReport.cs` |
| Embeddable preview control | `SourceCode/Abaki.CrystalReport/ReportCtrl.cs` |
| Report descriptor base | `SourceCode/Abaki.CrystalReport/ReportInfo.cs` |
| Strongly-typed report facade | `SourceCode/Abaki.Billing.Report B1/ReportHelper.cs` |
| Invoice / receipt descriptors | `SourceCode/Abaki.Billing.Report B1/InvoiceReportInfo.cs` |
| `.rpt` definitions (all reports) | `SourceCode/Abaki.Billing.Report B1/rpt*.rpt` |
| Project wiring for reports | `SourceCode/Abaki.Billing.Report B1/Abaki.Billing.Report.csproj` |
| Login-time connection seeding | `SourceCode/Pracnet B1/MainApp.cs:549` |
| Startup warm-up hack | `SourceCode/Pracnet B1/MainApp.cs:876` |
| Configured printers | `SourceCode/Abaki.Common.LocalSettings/PracnetPrinters.cs` |
| csproj-integrity rule | `.claude/rules/crystal-reports-csproj.md` |

---

## 15. Document scanning and shared documents

This section covers the whole document subsystem: acquiring images from a scanner, packaging them into the shared document store, recording them in the `Documents` table, viewing and printing them, and how the shared store is reached across the network and pushed to Medinet.

There are three moving parts to keep separate in your head:

1. Scanning UI (`SourceCode/ScanDoc`) which only produces temporary JPEG files on the local machine.
2. Import and storage (`DocumentController` plus `GeneralUtils`) which zips those files into the shared network store and writes a `Documents` row.
3. Viewing, printing, export and Medinet sync which read back from the shared store.

### 15.1 Projects and key files

| Component | File |
|---|---|
| Scanning UI (modern) | `SourceCode/ScanDoc/frmScanNew.cs` |
| Scanning UI (legacy) | `SourceCode/ScanDoc/frmScan.cs` (+ TWAIN plumbing in `frmScan` partial) |
| Legacy TWAIN P/Invoke wrapper | `SourceCode/ScanDoc/ScanProccess.cs` (`EZTW32.DLL`) |
| Window-message hook for TWAIN | `SourceCode/ScanDoc/WinFormsWindowMessageHook.cs` |
| Voice recording helper | `SourceCode/ScanDoc/VoiceRecord.cs` |
| Import and copy orchestration | `SourceCode/Abaki.Core.Gui B1/LetterDocuments/DocumentController.cs` |
| Document list UI and scan trigger | `SourceCode/Abaki.Core.Gui B1/LetterDocuments/frmDocumentList.cs` |
| ClinNet embedded scan storage | `SourceCode/ClinNet/ScanDocumentStorage/ScanDocumentStorageCtrl.cs`, `SourceCode/ClinNet/Controls/DocumentCtrl.cs` |
| Zip / archive / extract helpers | `SourceCode/Abaki.Common.Utils/GeneralUtils.cs` |
| Document entity (EF partial, file helpers) | `SourceCode/Abaki.Data/Domains/Entities_Common/DocumentCore.cs` |
| Document entity (EF generated mapping) | `SourceCode/Abaki.Data/Domains/Common.Designer.cs` (`Document` base at line 28381, `DocumentCore` subtype at line 29288) |
| Document DTO (mirrors entity) | `SourceCode/Abaki.Data.DTOs/Entities_Common/DocumentCore.cs` |
| Document viewer control | `SourceCode/AbakiControls/DocumentViewerCtrl.cs` |
| Image (TIFF/JPEG) viewer control | `SourceCode/AbakiControls/ImageViewerCtrl.cs` |
| Storage path bootstrap and Medinet detection | `SourceCode/Abaki.Core.Service/StorageService.cs` |
| Session storage paths | `SourceCode/Abaki.Common.Helpers/ApplicationSessionStorage.cs` |
| Permissions enum | `SourceCode/Abaki.Common.Enums/SecurityPermissionEnums.cs` (`ePracnetFeatures.Documents = 8`) |
| Medinet document sync tool | `SourceCode/Abaki.LinkingMedinetPracnetLetterDocument/frmMainForm.cs` |
| Medinet database and file sync | `SourceCode/Abaki.Sync/MDSync.cs`, `SourceCode/Abaki.Sync/SyncFileUtil.cs` |

The `ScanDoc` project builds to assembly `Abaki.ScanDoc.dll` with namespace `DocProccess` (note the historical double-c spelling in the namespace and in `ScanProccess`).

### 15.2 Scanning: two form implementations

There are two scan forms and they use two different TWAIN stacks. Both live in namespace `DocProccess` and both are `Abaki.Common.FormBase` subclasses. Both expose the same public contract that callers rely on: `ImgW`, `ImgH`, `ShowScanDialog`, `UseADF`, `UseDuplex`, `BW`, `AutoDetectBorder`, `AutoRotate`, and a read-only `List<string> ImageFiles` that returns the temp file paths of the scanned pages (the keys of the `lvImages` Infragistics `UltraWinListView`).

Modern form: `frmScanNew` (`SourceCode/ScanDoc/frmScanNew.cs`)

- Uses managed TWAIN via `TwainDotNet` (`TwainDotNet.Twain`, `TwainDotNet.ScanSettings`). `CreateScanner()` (frmScanNew.cs:60) wires a `WinFormsWindowMessageHook` so the form's window can receive TWAIN messages, subscribes to `TransferImage` and `ScanningComplete`.
- `Scan()` (frmScanNew.cs:145) builds `ScanSettings` from the checkboxes: `UseDocumentFeeder` (ADF), `ShowTwainUI`, `ShowProgressIndicatorUI`, `UseDuplex`, `Resolution` (`ResolutionSettings.Fax` for black and white, otherwise `ResolutionSettings.ColourPhotocopier`), and rotation / border-detection. Then calls `_scanner.StartScanning(_settings)`.
- Each acquired page fires `scanner_ImageScanned` (frmScanNew.cs:95): the incoming image is resized to `ImgW x ImgH` (defaults 1000 x 1200), saved as a JPEG named `Scan{yyyyMMddHHmmssfff}.jpeg` under the app base directory, then a 90 x 90 thumbnail is generated for the list view.

Legacy form: `frmScan` (`SourceCode/ScanDoc/frmScan.cs` plus the `frmScan` partial in `ScanMan.cs`)

- Uses the unmanaged `TwainLib` / `TwainGui` stack. `TwainMan()` (ScanMan.cs:37) calls `tw.Init(Handle)`, and the form overrides `WndProc` (ScanMan.cs:99) to intercept TWAIN window messages. On `TwainCommand.TransferReady` it pulls native bitmaps via `tw.TransferPictures()`, converts each GDI DIB to a managed `Bitmap` through `GdiPlusLib.Gdip.GdipCreateBitmapFromGdiDib`, and raises `ImageScanned`.
- The page-saved handler `scanner_ImageScanned` (frmScan.cs:68) does the same resize-and-save-JPEG step as the modern form.
- `ScanProccess.cs` is an even older P/Invoke wrapper around `EZTW32.DLL` (`TWAIN_AcquireToClipboard`, `TWAIN_SelectImageSource`, etc). It is not called by `frmScan` directly today; treat it as legacy reference.

Which form runs: the Documents list (`frmDocumentList.btnScan_Click`, frmDocumentList.cs:340) hardcodes `frmScanNew` (the legacy branch is commented out). The ClinNet embedded scan path (`ScanDocumentStorageCtrl.NewScanForm`, ScanDocumentStorageCtrl.cs:403) still honours `PersonalSetting.Instance.UseNewScanForm` to choose between `frmScanNew` and `frmScan`. If you are debugging a scanner-hardware issue, first confirm which form and therefore which TWAIN stack is active.

Scanner capability and defaults are persisted on `PersonalSetting.Instance` (`UseDocumentFeeder`, `UseDuplex`, `BlackWhite`, `AutoDetectBorder`, `AutoRotate`) and `ClinNetSetting.Instance` (`ShowScanDialog`, `ScannedImageW`, `ScannedImageH`), written back when the scan form closes.

### 15.3 Scan workflow end to end

1. Staff clicks Scan. `btnScan_Click` builds the scan form, applies the saved defaults, and shows it modally.
2. The user acquires one or more pages. Each page is resized and written to a temporary JPEG in `AppDomain.CurrentDomain.BaseDirectory` (the running executable's folder), named `Scan{yyyyMMddHHmmssfff}.jpeg`. These are local, per-workstation temp files, not the permanent store.
3. On OK, the caller reads `frm.ImageFiles` and hands them to import. In `frmDocumentList` this goes through `ImportImages` (frmDocumentList.cs:401): if there are fewer than 20 pages it composites them into a single PDF (`ScanPic{...}.pdf`, built with the Infragistics `PdfDocument` writer) under the local `Documents\` folder and imports that one file with `multiPages = true`; 20 or more pages are imported as individual image files. Then `ImportDocs(list, false, true)` is called.
4. Import copies the file(s) into the shared store as a password-protected ZIP and writes the `Documents` row (see 15.4).
5. `frm.Dispose()` then `GC.Collect()` (frmDocumentList.cs:390) is called explicitly because the TWAIN and image objects hold unmanaged handles.

### 15.4 Import and two-tier storage

`DocumentController.ImportDocs` (`SourceCode/Abaki.Core.Gui B1/LetterDocuments/DocumentController.cs:33`) is the canonical import path. `frmDocumentList` has its own near-identical private `ImportDocs` (frmDocumentList.cs:441) for the list UI; if you change one, check the other.

`ImportDocs` builds a `DocumentCore`, sets `DateEntered`, `DocDate`, `CreatedDate` to now, `CreatedGUID` from `GlobalInfo.UserInfo.UserID`, `ProviderGUID` from the current login staff, and `DocType = "Document"` (or `"Photo"` when the file extension is an image type and `ClinNetGlobalSetting.VisibleFilterOnDocument` is on). It generates the archive name `Doc{yyMMddhhmmssfff}.zip`, prompts for the patient (or uses the supplied `patientGuid`), then shows the `DocumentDetailsCtrl` modal so staff can set title, description, tags and referring provider. On OK it copies the file(s) and adds the row(s) via `NewCommonContext.ContextInstance.AddToDocuments(doc)` followed by a single `SaveChanges()`.

Three copy shapes:

- `multiPages == true`: one archive containing all source files, via `GeneralUtils.CopyDocuments(fileNames, doc.FileName, documentPath)`.
- exactly one file: `CopyFile` then `CopyDocument`.
- many files, not multipage: one `DocumentCore` row per file (`CreateDocument` clones the header, each gets its own `Doc...zip`).

`CopyFile` (DocumentController.cs:126) has a subtlety: if the source has no extension it first copies it to a temp file with `ClinNetGlobalSetting.General_DocumentDefaultExtension`, archives that, then deletes the temp.

Storage is two-tier:

| Tier | Location | Format | Naming |
|---|---|---|---|
| Temporary | `AppDomain.CurrentDomain.BaseDirectory` (and a local `Documents\` subfolder for extractions and composited PDFs) | JPEG / PDF | `Scan{yyyyMMddHHmmssfff}.jpeg`, `ScanPic{...}.pdf` |
| Permanent | `<SharedStoragePath>\<ProfileStorageFolder>\Documents\Docs{yyyyMM}\` | password-protected ZIP | `Doc{yyMMddhhmmssfff}.zip` |

The permanent path is computed in `DocumentController.documentPath` as `Path.Combine(setting.SharedStoragePath, setting.ProfileStorageFolder, "Documents")`. The dated `Docs{yyyyMM}` sub-bucket is appended by `GeneralUtils.GetCurrentFolder(root, createdDate)` (GeneralUtils.cs:945), which is why the created date must be preserved to find a document again later.

The zip mechanics all live in `GeneralUtils`:

- `CopyDocument` / `CopyDocuments` (GeneralUtils.cs:695) ensure the store directory exists then call `ArchiveFile`.
- `ArchiveFile` (GeneralUtils.cs:888) uses `Ionic.Zip.ZipFile`, resolves the dated `Docs{yyyyMM}` folder, sets the zip password, adds each source file, and saves `Doc...zip`.
- `GetZippedFiles` (GeneralUtils.cs:1033) lists entries inside an archive (used to populate the viewer file list).
- `ExtractSpecificFile` (GeneralUtils.cs:1076) extracts a single named entry to the local `Documents\` folder, with an `ignoreIfExist` fast-path that reuses today's already-extracted copy.
- `ExtractFile` / `ExtractFiles` (GeneralUtils.cs:951, 1151) extract all entries (used by `OpenDocument`, which then `Process.Start`s the file in the OS-associated viewer).
- `ExportDocument` / `ExportLetter` (GeneralUtils.cs:1215, 1243) extract to an arbitrary destination for the Export feature.
- `EmptyLocalDocumentFolder` (GeneralUtils.cs:1265) prunes the local `Documents\` extraction cache, deleting anything not created today.

Security note on the ZIP passwords: the archives are password protected. Two passwords exist and are hardcoded as string literals inside `GeneralUtils` (in `ArchiveFile`, `ExtractFile`, `ExtractFiles`, and `GetZip`). One is the standard document password; the other is reserved for archives whose filename starts with `HCN` (HealthConnect / secure-messaging results). The read paths try the filename-appropriate password first and fall back to the standard one. The values are not reproduced here; if you need them, read `SourceCode/Abaki.Common.Utils/GeneralUtils.cs`. Because the passwords are compiled into the client, they protect against casual file-share browsing, not against a determined attacker with the binary. Do not paste these values into tickets, PRs or logs.

### 15.5 The Documents table and DocumentCore entity

The entity maps to table `Documents`. There is an inheritance split in the EF model: a base `Document : EntityObject` and a derived `DocumentCore : Document` (both generated in `SourceCode/Abaki.Data/Domains/Common.Designer.cs`). The hand-written partial `SourceCode/Abaki.Data/Domains/Entities_Common/DocumentCore.cs` carries `[Table("Documents")]` and the file-access helpers; it implements `IDocumentContainer`, which is the interface the viewers bind to.

Mapped columns (from the generated `Document` base and `DocumentCore` subtype) include:

- `GUID` (primary key), `FileName` (the `Doc...zip` archive name), `DocTitle`, `DocType` (`"Document"` or `"Photo"`), `DocDesc`, `Tags`.
- `PatientGUID`, `ProviderGUID`, `ReferringProviderGUID`, `CopyToGUID` (referring / cc providers).
- `DocDate`, `DateEntered`, `CreatedDate`, `CreatedGUID`, `UpdatedDate`, `UpdatedGUID`, `DeletedDate`, `DeletedGUID`.
- `IsChecked`, `NotationCode`, and a `DocSource` / `DocSettings` pair used by clinical / result documents.

`CreatedDate` is load-bearing: `GetFileList()` passes it to `GeneralUtils.GetZippedFiles(FileName, DocumentPath, CreatedDate)` so the code can locate the correct `Docs{yyyyMM}` bucket. If `CreatedDate` is wrong the archive will not be found in the dated folder and the code falls back to the un-bucketed root.

`IDocumentContainer` methods on the entity (DocumentCore.cs):

- `GetFileList()` (line 38) lazily lists the archive entries; thread-safe via a private lock.
- `GetExtractedFile(filename, prefix)` (line 64) extracts one entry to the local cache and returns its path.
- `CreateThumbnailFromFile(filename, size)` (line 82) extracts, builds a `Bitmap` thumbnail, caches it under the local `Documents\` folder keyed by size and archive name.

There is a parallel `DocumentCoreDTO` partial (`SourceCode/Abaki.Data.DTOs/Entities_Common/DocumentCore.cs`) with the same three helpers; it exists so DTO-based screens can render documents without loading the full EF entity. Keep the two in sync.

Tag convention worth knowing: rows tagged `P2K RESULT` are migration artefacts and are excluded from the Medinet sync query (see 15.7).

### 15.6 Viewing, printing, export and permissions

`DocumentViewerCtrl` (`SourceCode/AbakiControls/DocumentViewerCtrl.cs`) is the general preview control. You bind it by setting `DocumentContainer` to any `IDocumentContainer` (a `DocumentCore` or `DocumentCoreDTO`). It then:

- Calls `GetFileList()` and fills the `lvFiles` list (DocumentViewerCtrl.cs:348).
- On selection, extracts the entry asynchronously (`ExtractFile` via `_container.GetExtractedFile`) and routes it by type in `ShowDocument` (line 720). `GetDocumentType` (line 797) maps extensions to `eDocumentType`: images (the `GeneralUtils.ImageExtensions` set), `Pdf`, `Rtf` (`.rtf/.rtx/.doc/.docx`), `Txt`, or `Unknow`.
- Renders images through the embedded `ImageViewerCtrl` (`ShowTiff`), PDFs through an `AxAcroPDFLib.AxAcroPDF` ActiveX control with a `WebBrowser` fallback if the Acrobat control fails to create (`CreatepdfViewer`, line 132), and RTF/DOC/DOCX/TXT through a TX Text Control (`ShowRtf`, `ShowRawText`). Unknown types offer an external-viewer link that calls `Process.Start`.
- Printing: `btPrint_Click` prints whatever child viewer is active; `btPrintAll_Click` (line 870) collects all image entries, extracts them and prints them as a multi-page `PrintDocument`.

`ImageViewerCtrl` (`SourceCode/AbakiControls/ImageViewerCtrl.cs`) is the raster viewer (zoom, rotate, print modes via `eImagePrintMode`) used inside `DocumentViewerCtrl` and also directly by the scan review flow.

Permissions are governed by `ePracnetFeatures.Documents` (value 8, `SourceCode/Abaki.Common.Enums/SecurityPermissionEnums.cs:69`). The document list checks `PracnetAuthorization.HasPermission(...)` and gates Scan/Import behind `CanEdit` (`frmDocumentList.btnScan_Click` returns early with a permission warning if `!CanEdit`). The standard `ePracnetPermission` set (Create, Read, Update, Delete, Archive, Export, Print_Email) applies. `Letters` (value 7) is the sibling feature for the Letter Writer, which shares much of the same zip/store plumbing (`SaveLetter` / `LoadLetter` / `ArchiveFiles` in `GeneralUtils`).

### 15.7 Shared store across the network, and Medinet sync

Where the shared store lives. `RecallGlobalSetting.Instance.SharedStoragePath` (defined in `SourceCode/ClinicCoreSetting/RecallGlobalSetting.cs`) is the network root, typically a UNC share, and `ProfileStorageFolder` is the per-profile subfolder. All practice workstations point at the same `SharedStoragePath`, which is how one workstation can scan a document and another can immediately open it: the archive is written to the shared network folder, and only the `Documents` row plus the archive filename and created date are needed to find it.

Bootstrap at login. `StorageService` (`SourceCode/Abaki.Core.Service/StorageService.cs`) owns setup. `Init(sharedStoragePath, profileStorageFolder)` (line 372) sets `ApplicationSessionStorage.DocumentPath = Path.Combine(sharedStoragePath, profileStorageFolder, "Documents")` and the matching `LetterPath`, then creates the folder if missing. Note that `Init` (and `ProcessStorageLocationChanged`) currently force `profileStorageFolder = string.Empty`, so in practice the effective store is `<SharedStoragePath>\Documents\Docs{yyyyMM}\`. `ApplicationSessionStorage.DocumentPath` is the static that `DocumentCore.GetFileList()` and friends read, so it must be set before any document is opened. `frmDocumentList_Load` also re-derives and sets it defensively (frmDocumentList.cs:122).

`IsStoragePathValid()` / `IsStoragePathWritable()` (StorageService.cs:324) verify the share exists and is writable (`GeneralUtils.CheckDirectoryWritable` inspects the ACL). `ProcessStorageLocationChanged` (line 334) background-copies existing document/letter folders when the practice repoints its shared store, so relocating the share does not orphan history. There is also `GeneralUtils.ConvertLocalToNetworkPath` (GeneralUtils.cs:733) which resolves a local path to a UNC share name via WMI `Win32_share`, used when a locally scanned path needs to be expressed as a network path.

Medinet detection. `StorageService` exposes `IsMedinetLoaded` and `MedinetSettingInstance`. `LoadMedinetDocumentPath()` (line 277) connects to the Medinet demographic database, reads the `General_DocumentPath` clinic parameter into `MedinetSettingInstance.DocumentPath`, and sets `IsMedinetLoaded = true`. Note: scanning itself has no Medinet-specific branch. When Medinet is loaded, Pracnet still scans through the same `ScanDoc` forms and stores into its own zip-based store; the Medinet linkage happens afterwards, either by the batch sync tool or by the file-sync framework.

Document sync tool (Pracnet to Medinet, one-way for documents). `SourceCode/Abaki.LinkingMedinetPracnetLetterDocument/frmMainForm.cs` reads Pracnet `Documents` rows excluding `Tags = 'P2K RESULT'` (query at line 463 / 485), maps each to a Medinet `CN_FILE` record (frmMainForm.cs:502), translating GUIDs through lookup dictionaries: patient/provider/referring-provider GUIDs via `_pracnetMedinetContactGuids`, and user GUIDs (`CreatedGUID` / `UpdatedGUID` / `DeletedGUID`) via `_pracnetMedinetUserGuids`. `Archived` is derived from `DeletedDate`. A `CopyToGUID` produces a `CN_FileCCRefDoc` cc-row. When `copyDocument` is set it also physically copies the archive from the Pracnet store to the Medinet `Docs{yyyyMM}` folder via its own `CopyDocument` helper (frmMainForm.cs:575), bucketing by `CreatedDate`. Records are batched and `SaveChanges()`d in loops.

Ongoing bidirectional sync. `SourceCode/Abaki.Sync/MDSync.cs` drives Microsoft Sync Framework. When `SyncDocuments` is true (default, MDSync.cs:152) it first synchronises the databases with a `SyncCase` (scope name from `ScopeName`) and then calls `SyncFileUtil.DoBidirectionalSync(ClientDocPath, ClientId, ServerDocPath, ServerId)` (MDSync.cs:225) to reconcile the physical document files. `SyncFileUtil` (`SourceCode/Abaki.Sync/SyncFileUtil.cs`) uses `FileSyncProvider` with metadata file `MedinetFileSync.metadata` and a `RenameSource` conflict policy. Direction is configurable (`_direction`: upload / download / both). So while the batch tool is one-way (Pracnet to Medinet), the running sync framework can move document files both ways.

### 15.8 Invariants and gotchas

- `ApplicationSessionStorage.DocumentPath` must be populated (by `StorageService.Init` at login) before any `DocumentCore.GetFileList()` / `GetExtractedFile()` call, or reads silently return empty and the viewer shows "File not found".
- `CreatedDate` on the row must match the `Docs{yyyyMM}` bucket the archive was written into. A wrong `CreatedDate` sends the lookup to the wrong dated folder; the code falls back to the un-bucketed root but that only works for very old archives that predate bucketing.
- `profileStorageFolder` is force-blanked in `StorageService`, so despite the schema/settings suggesting `<Shared>\<Profile>\Documents`, the real path is `<Shared>\Documents`. Do not "fix" the concatenation without understanding this.
- Two `ImportDocs` implementations exist (`DocumentController` and `frmDocumentList`); they have drifted before. Change both.
- The composited-PDF path in `ImportImages` only triggers under 20 pages; at 20 or more, pages are stored as individual images. Bulk scans therefore behave differently from small ones.
- Temp JPEGs accumulate in the executable folder and extractions accumulate in the local `Documents\` cache; `EmptyLocalDocumentFolder` only removes items not created today, so the cache is never fully emptied within a day.
- The scan forms hold unmanaged TWAIN and GDI resources; callers deliberately `Dispose()` then `GC.Collect()`. Do not remove that.
- ZIP passwords are compiled-in string literals in `GeneralUtils`; treat them as secrets in any external-facing artefact and never print them.
- The PDF viewer depends on the Adobe Acrobat ActiveX control being installed; `CreatepdfViewer` falls back to a `WebBrowser` control (which may prompt to download rather than render inline) when it is not.
- Scanning is unchanged by Medinet: there is no Medinet-specific code path in `frmScanNew` / `frmScan`. Sync to Medinet is a separate, later step.

---

## 16. Conventions, testing and gotchas

This section is the "how we work here" chapter. It captures the naming, git, logging, locale and testing conventions that the whole Pracnet codebase leans on, summarises the machine-readable rule set under `.claude/rules/`, and lists the landmines that have actually bitten people. Read it before your first commit. Everything here is grounded in the current code, not folklore.

### 16.1 Naming conventions

Three naming patterns recur everywhere and you will read and write them constantly.

Namespaces follow `Abaki.<Domain>.<Layer>`. The domain is the functional area (`Billing`, `Core`, `Common`, `Data`, `Pracnet`, `MedinetAdaptor`, `HICOnline`) and the layer is the architectural tier. Examples that exist in the tree: `Abaki.Billing.Dal`, `Abaki.Billing.Bll`, `Abaki.Billing.Gui`, `Abaki.Core.Dal`, `Abaki.Common.Logger`, `Abaki.Common.Helpers`, `Abaki.Common.Enums`, `Abaki.Data.DataContext`, `Abaki.Pracnet.OnlineClaiming`. The `Abaki.*` prefix is the house namespace for almost the entire product. Newer greenfield code sometimes uses `PrimaryClinic.*` (for example `PrimaryClinic.MedicareOnline`, `PrimaryClinic.ProdaAuthentication`), reflecting the current brand, but the bulk of the codebase is still `Abaki.*` and you should match the surrounding file rather than "modernise" it.

WinForms form classes are prefixed `frm<Name>`. Real examples: `frmMain2`, `frmLogin`, `frmSplash`, `frmScanNew`, `frmScan`, `frmReport`, `frmQuoteManagement`, `frmUpdateBillingItems3`. When you add a new form, keep the prefix. Each form is a triple of `frmX.cs`, `frmX.Designer.cs` and `frmX.resx`.

The project folder suffix " B1" (space then capital B then 1) appears on many, but not all, project folders and is easy to trip over. Examples: `Pracnet B1`, `Abaki.Billing.Bll B1`, `Abaki.Billing.Dal B1`, `Abaki.Billing.Gui B1`, `Abaki.Billing.Report B1`, `Abaki.Core.Gui B1`, `AppointmentGUI B1`, `AppointmentDAL B1`. It is a legacy branch/variant marker baked into the on-disk folder name. Critical points:

- The folder name carries the suffix, but the assembly name and namespace usually do not. The folder `Abaki.Billing.Dal B1` produces the `Abaki.Billing.Dal` assembly. Do not put " B1" in namespaces or type names.
- Because the space is part of the path, any command-line reference to these projects must be quoted. This is the single most common shell mistake on this repo. Always write `"SourceCode/Abaki.Billing.Gui B1/BillingService.cs"`, never the unquoted form.
- When you cite a file in a PR, ticket or handover, use the full folder name including " B1" so the path resolves for the next person.

### 16.2 Git workflow

The remote is Bitbucket (`bitbucket.org/global-health/pcpractice`), not GitHub. Integration happens through Bitbucket pull requests, so the GitHub `gh` CLI is not the tool here; use the Bitbucket REST API (`https://api.bitbucket.org/2.0/repositories/global-health/pcpractice/...`) when you script against PRs.

- Main integration branch is `Development`. This is where feature and bugfix branches merge and where releases are cut from. It is not called `main` or `master`.
- Feature branches are `feature/PRACNET-####-short-description` (for example `feature/PRACNET-3005-medicare-clientid-v3`). Bugfix and hotfix branches follow the same shape with a `hotfix/` or descriptive prefix; the `git log --first-parent` on `Development` shows real examples like `hotfix/v3.0.0-skip-credential-update` and `feature/PRACNET-3019-acir-duplicate-claim-guard`.
- Commit messages start with the ticket key: `PRACNET-####: Description of change`. The ticket key is the traceability anchor for version bumps, release notes and spec-compliance reviews, so never omit it on a substantive change.
- Never commit or push directly to `Development`. Branch first, push the branch, open a PR, and let it merge through review. Merge commits on `Development` read `Merged in <branch> (pull request #NNN)`.
- Do not commit machine-local absolute paths into shared history (see 16.8, the `no-local-paths` rule). Repo-relative `SourceCode/...` paths in a commit message are fine; drive-letter and home-directory paths are not.

### 16.3 Logging

Logging is log4net-based but goes through thin static wrapper classes rather than raw `ILog` usage, and there are three distinct loggers that write to three distinct files. Choosing the wrong one means your diagnostics land where nobody looks. All three live in `SourceCode/Abaki.Common.Logger/`.

| Logger class | File | Log file prefix | Use for |
|---|---|---|---|
| `ErrorLogger` | `Abaki.Common.Logger/ErrorLogger.cs` | `Logs\SystemLog_<user>_<date>` | General application logging, runtime errors, everything not claim-related |
| `ClaimingErrorLogger` | `Abaki.Common.Logger/ClaimingErrorLogger.cs` | `Logs\ClaimingLog_<user>_<date>` | Medicare / DVA / health-fund claim transmit paths |
| `MedicareOnlineLogger` | `Abaki.Common.Logger/MedicareOnlineLogger.cs` | Medicare Online specific | Low-level Medicare Online request/response tracing |

Key facts, verified in the code:

- Each logger is a `static class` with a `static` constructor that builds its own log4net `RollingFileAppender` in a private repository (`ErrorLogger` uses repository `RunTimeLogOnLocal`, `ClaimingErrorLogger` uses `ClaimingLogOnLocal`). They are independent; configuring one does not configure the other.
- Logs roll daily (`RollingMode.Date`, `DatePattern yyyyMMdd`) and are written to a `Logs\` subfolder of the application base directory. The current Windows user name is folded into the file name (`WindowsIdentity.GetCurrent().Name` with `\` replaced by `_`), so on a shared terminal-server host each operator gets their own log file.
- Both `ErrorLogger.WriteLog` and `ClaimingErrorLogger.WriteLog` have two overloads: `(object message, Exception ex, ErrorLogLevel logLevel = Error)` and `(object message, ErrorLogLevel logLevel = ...)`. Note the default level differs: the message-only `ErrorLogger` overload defaults to `Info`, whereas both `ClaimingErrorLogger` overloads default to `Error`. Pass the level explicitly if it matters.
- Levels come from the `ErrorLogLevel` enum (`Fatal`, `Error`, `Warn`, `Info`, `Debug`) in `Abaki.Common.Enums`.
- Each `WriteLog` also fans the message out to a "to server" sibling (`ErrorLoggerToServer`, `ClaimingLoggerToServer`, `MedicareOnlineLoggerToServer`) so logs can be centralised as well as written locally.

The convention that matters for claiming work: **claim transmit diagnostics go to `ClaimingErrorLogger`, not `ErrorLogger`.** The claiming log is the file SIT and Services Australia integration testers attach when they report an issue, so if you log a transmit failure to `SystemLog` instead of `ClaimingLog` the tester will not see it. This is spelled out in the claim-transmit rule and is worth repeating. Related: SIT debugging log lines are tagged with a `[DIAG-3009]`-style prefix so they can be found and stripped before release with a `git grep` for the tag.

### 16.4 Locale

The application forces Australian English culture at startup. `SourceCode/Pracnet B1/MainApp.cs:427` sets `Thread.CurrentThread.CurrentCulture = new CultureInfo("en-AU")` right after the splash screen shows and before settings load. Consequences you must respect:

- Dates render and parse as DD/MM/YYYY. Do not hardcode US `MM/dd/yyyy` formats or rely on ambiguous parsing.
- Currency is AUD and decimal handling assumes the `en-AU` number format.
- The domain is Australian healthcare throughout: Medicare, DVA, PBS, MBS item codes, ACIR (Australian Childhood Immunisation Register). Terminology and validation are Australia-specific.
- When you write tests or helpers that touch date or money formatting, either set the culture explicitly in the test or use `CultureInfo.InvariantCulture` deliberately, because the test runner does not necessarily inherit the app's `en-AU` setting.

### 16.5 Testing

The test framework is **MSTest v1** (namespace `Microsoft.VisualStudio.TestTools.UnitTesting`, assembly `Microsoft.VisualStudio.QualityTools.UnitTestFramework`). Deliberately v1 style: `[TestClass]` on the class, `[TestMethod]` on each method, and no `[DataTestMethod]` / `[DataRow]` (those are MSTest v2). Do not introduce v2 attributes into existing projects.

#### Test projects and the source-area mapping

Add tests to the project that matches the source area you changed (from `.claude/rules/unit-tests.md`):

| Source area | Test project |
|---|---|
| `Abaki.Billing.*` | `Test.Abaki.Billing.Dal` |
| `Abaki.Common.*` | `TestAbakiCommon` |
| `Appointment*` | `TestAppointment` |
| `Abaki.Core.*` | `Test.Abaki.Billing.Dal` (or create a new project) |
| `PrimaryClinic.MedicareOnline` | `Test.Abaki.Billing.Dal` (reference added in PR #276) |
| `Abaki.Pracnet.OnlineClaiming` | `Test.Abaki.Billing.Dal` (project reference not yet wired at time of writing) |

Other test projects present in the solution: `TestCustomLayout`, `TestSync`, `Abaki.LinkingMedinetAdaptorTest`, and the UI-automation harness `Test.ClinNet.UiAutomation`. The billing test project is on disk at `SourceCode/Test.Abaki.Billing.Dal/` and its project file is `Test.Abaki.Billing.csproj` (note the file name drops the `.Dal` that the folder carries, and the built DLL is `Test.Abaki.Billing.Dal.dll`).

Representative test files in `Test.Abaki.Billing.Dal` show the house style you should match: `TestDB4ReportSplitting.cs`, `TestDerivedFeeCalculation.cs`, `TestIMCClaimRoutes.cs`, `TestBenefitAssignmentAuthorisedInd.cs`, `TestSubmissionAuthorityIndicator.cs`, `TestMedicareApiVersion.cs`, `TestVisibleClaimStatuses.cs`, `TestOECMandatoryPayloadShape.cs`. Class names are `Test<Feature>`; method names are `<Scenario>_<Expected>` (for example `DVA_Item24_1Patient_ShouldBe_85_80`, `GetApiUrl_AG_Specialist_ReturnsAgSpecialistV2`).

Two MSTest-project mechanics that catch people:

- These are classic (non-SDK) csproj files with **explicit `<Compile Include="..." />` entries**. A `.cs` file that exists on disk but is not listed in the csproj will not compile into the test DLL, so your "new test" silently never runs. Always add the `<Compile>` entry.
- If your test references a production project the test project does not already reference, add the `<ProjectReference>` before writing the test.

#### The per-file test obligation

The rule is per-file, not per-PR. When you modify a `.cs` file under `SourceCode/`, that file needs at least one passing test covering the new or changed behaviour, unless a sibling test already covers it or the file is exempt. Exempt file types: UI/WinForms (forms, `.Designer.cs`, event handlers), pure CRUD data access with no logic, third-party wrappers (Crystal Reports, Infragistics, TX Text Control), auto-generated `.edmx` code, `Properties/AssemblyInfo.cs` version bumps, `.csproj` edits, and files with nothing to execute (enum-only files, DTO/POCO with no logic, interface declarations).

#### Extract pure helpers to make logic testable

Much of the logic lives in classes tightly coupled to singleton EF contexts (`NewCommonContext.ContextInstance`, `DapperContext.Instance`) and to heavy WinForms objects, which makes them awkward to instantiate in a unit test. The house pattern, and the rule's explicit guidance, is to **extract the changed logic into a pure static helper and test the helper directly** rather than mocking the world. Real examples of this pattern in the tree: `DB4ReportRouter.SplitBulkBillByServiceType` (a generic static helper the `TestDB4ReportSplitting` test drives directly), `SubmissionAuthorityIndMapper` and `BenefitAssignmentAuthorisedIndMapper` in `Abaki.Common.Helpers` (deliberately placed in a low-level assembly so both the Billing GUI and the tests can reference them without a circular dependency). If you cannot extract, the fallback is to replicate the calculation in the test with hardcoded inputs so the contract is at least documented, but prefer real extraction.

#### Build-and-run-before-push

Before every `git push` on every branch (not just PR branches) you must:

1. Build the affected test project. If the build fails, fix it before pushing. The billing test project builds with MSBuild from the VS2022 Professional install (`.../MSBuild/Current/Bin/MSBuild.exe`), target `Build`, configuration `Debug`.
2. Run the test class(es) you added or modified with `vstest.console.exe` against the built DLL (`SourceCode/Test.Abaki.Billing.Dal/bin/Debug/Test.Abaki.Billing.Dal.dll`, `/Tests:<TestClassName>`). For a substantial refactor, run the whole DLL.
3. Confirm "Test Run Successful." with zero failures. Do not push a branch with red tests. If a failure is genuinely pre-existing and unrelated, mark it `[Ignore]` with a comment linking the tracking ticket rather than leaving it red.
4. For substantial work, record the evidence in the commit message, for example: `Verified: built Test.Abaki.Billing.csproj clean; ran TestIMCClaimRoutes (38 tests, all pass).`

You may skip the run only for documentation-only changes (no `.cs` touched), pure `.csproj` file/reference additions (the build exercises them), or changes in a project with no test project (note `no tests available for <project>` in the commit). "Tests are slow" or "I'm sure it works" are never valid reasons.

### 16.6 The `.claude/rules/` rule set

Eight rule files under `.claude/rules/` encode the project's operating procedures. They are the authoritative "how" for the areas below; this handover summarises them so you know they exist and when each fires.

- **`unit-tests.md`** — The per-file test obligation, the source-area to test-project mapping, MSTest v1 conventions, the extract-a-pure-helper guidance, and the mandatory build-and-run-before-push procedure. Summarised in 16.5.

- **`assembly-version.md`** — Mechanics of bumping `AssemblyVersion` and `AssemblyFileVersion` in each project's `Properties/AssemblyInfo.cs`. Bump patch (4th digit) for a bug fix, minor (3rd digit) for a feature, and set both attributes to the same value. Only bump projects whose source actually changed, plus two always-bumped projects on every release: `Pracnet B1` (stamps `Pracnet.exe`) and `Abaki.SecurityGUI` (the About dialog's version line comes from this DLL via `frmLogin.GetPracnetVersion()`, so the two must stay in lockstep). Never bump test projects or unmodified projects.

- **`release-process.md`** — When versions are bumped and how release notes are written. The key rule: **versions are bumped only at release time via the `/MakeRelease` command, never per-feature or per-bugfix.** Per-feature bumps create noisy diffs and merge conflicts. A release covers every branch merged to `Development` since the previous release, so diff against the last release tag or commit (`git log <last-release-ref>..HEAD --first-parent`) to find every touched project, not just the current branch. Release notes live in `SourceCode/Pracnet B1/Resources/ReleaseNote.rtf`, Calibri font, newest entry at the top, with bold section headers (`Bug fixes:`, `Enhancement:`) and a separator line between releases.

- **`claim-transmit-conventions.md`** — The conventions for the six Medicare online claim-transmit services (BBSW, IMC, DVA, DvaAllied, OVS, OEC), which all share the loop-over-claims shape and the same five silent-fail bug classes. Covers the five paths that must update both claim status and each linked invoice's `SentClaimStatus` (OPV pre-check failure, HTTP transmit failure, transmit-failure invoice propagation, null request-model guard, per-claim exception catch), the exact status cast types per entity (get these wrong and it will not fail at compile time because of `Nullable<>` constant narrowing), the `[DIAG-3009]` log-tag convention, SaveChanges placement (per-claim for BBSW, batch-level for IMC), the single-writer principle, and a "verify, don't echo" list of bugs that dissolve under inspection. Read this before touching any claiming transmit path.

- **`code-review-spec-compliance.md`** — When reviewing a PR that touches Medicare Online claiming, printed claim forms, or related persistence, the reviewer must cross-check against the relevant Services Australia technical specification and the NOI (Notice of Integration) Web Services Test Plan, and cite spec page numbers and NOI test-case row numbers in the review. "Looks right" without citations is not a review. Each finding gets a verdict of Compliant, Out-of-spec or Spec-silent. Pure UI-only, test-only or behaviour-neutral refactors are out of scope and can be noted as such.

- **`crystal-reports-csproj.md`** — The Visual Studio designer can silently remove **other** reports' entries from `Abaki.Billing.Report.csproj` when you edit any one `.rpt` file (a real incident: editing `rptDB4_MBS.rpt` dropped `rptDB42`'s `<Compile>` and `<EmbeddedResource>` entries, giving a `rptDB42 could not be found` build failure). After every `.rpt` edit, diff the report csproj and restore any removed `<Compile>`/`<EmbeddedResource>` pairs before committing. See 16.7.

- **`no-local-paths.md`** — Strip machine-local paths from anything other developers read (Jira, Bitbucket PRs, Confluence, Slack): absolute Windows paths (drive-letter roots and home-directory paths), local clone and home-directory paths, OS tool paths, file URLs to network shares, and temp-file paths from local conversions. Keep repo-relative `SourceCode/...` paths, file:line citations, class/method names, commit hashes, PR numbers, branch names, spec page references and Jira keys. The chat and uncommitted `.claude/` scratch files are the only places local paths are acceptable.

- **`pr-review-cycle.md`** — When you run a code-review cycle on a Bitbucket PR (spawn reviewer subagent, collect findings, implement fixes, push), post a summary comment on the PR before telling the user the cycle is complete: a one-line verdict, must-fix / should-fix items with the change and commit SHA, deferred items with rationale, and the sanity-checks the reviewer confirmed were already correct. Follows the no-local-paths rule for its file references.

### 16.7 Key gotchas

These are the failure modes that have actually cost time on this codebase. Recognise them by their symptoms.

#### EF model ahead of schema (a migration did not run)

The database is EF6 database-first with `.edmx` models, and the EF model can be ahead of the physical database if a migration script has not been applied on a given environment (SIT, a customer site). The classic case is `sp_ViewInvoiceSearch` / the `ViewInvoiceSearch` complex type. Because that stored proc builds its query as dynamic SQL (`SET @SQLString = 'SELECT ...'` then `EXEC(@SQLString)`), `ALTER PROCEDURE` never validates column existence, so the runtime error tells you precisely which migration is missing:

- EF mapping error "the data reader is incompatible ... member '<Col>' does not have a corresponding column" means the **stored proc was not updated** (the SELECT omits the column the model expects). The proc-update script did not run.
- `SqlException: Invalid column name '<Col>'` means the proc **was** updated to SELECT the column, but the underlying **table column does not exist** (the table-add migration did not run).

For the 2026-06 `SubmissionAuthorityInd` incident the EF-mapping form proved that script `785_PRACNET3006_add_indicators_to_sp_ViewInvoiceSearch.sql` (and the whole `Revision_0.1.9` batch) had been skipped on that database. Diagnose schema-only, with no patient data, via `OBJECT_DEFINITION(OBJECT_ID('dbo.sp_ViewInvoiceSearch'))`, `sys.columns` on the relevant table, `DatabaseUpgradeTrackings` rows for the script ids, and `Clinic.DatabaseVersion` (a stale version makes `PracnetDatabaseUpdateTool` skip a revision folder). Remediation on shared environments is to run `PracnetDatabaseUpdateTool`, not ad-hoc SSMS. Migration scripts live under `Database/PracnetDatabaseFrom0.1.6/Revision_*`.

#### CRLF churn on Windows tooling

The shared repo stores files with CRLF line endings and git is configured with `core.autocrlf=true`. On the Windows git-bash environment, `sed -i` rewrites the whole file with LF endings as a side effect, so even a one-line `sed -i 'Ns/.../.../'` edit makes git show the **entire file** as changed (every line deleted and re-added), swamping the real change and poisoning the diff for reviewers. Rules of thumb:

- For line-precise edits to existing files, use the Edit tool (it preserves the existing endings), not `sed -i`.
- When a line is duplicated or tab-laden and Edit cannot uniquely match, do a binary-safe Python edit: read `'rb'`, `split(b'\n')`, edit by index, write `'wb'`, `b'\n'.join(...)`.
- Confirm the real semantic change with `git diff --ignore-cr-at-eol`.
- If churn is already committed but not yet pushed: `git checkout HEAD~1 -- <file>`, redo the edit binary-safe, then `git commit --amend`.

More broadly, avoid line-normalising tools (`sed -i`, some formatters) on this repo unless you have verified they preserve CRLF.

#### Spec and NOI compliance for claiming changes

Any change to Medicare Online claiming, printed claim forms (DB4, DVA), or the related persistence is contractually tied to the Services Australia specification and the NOI Web Services Test Plan (the contract Services Australia tests the build against before certifying it). Past PRs have passed internal review and still failed NOI (facilityId cardinality drift, benefitAssignmentAuthorisedInd default), so this is not optional polish. Verify scope against the spec before trusting any in-repo `PRACNET-XXXX-implementation.md` plan, because the plans have diverged from the specs before. Cite spec section and page plus NOI test-row numbers in the review, and give each finding a Compliant / Out-of-spec / Spec-silent verdict (see 16.6, the code-review-spec-compliance rule).

#### Crystal Reports csproj integrity

As noted in 16.6: editing one `.rpt` in the VS designer can silently strip a different report's `<Compile>` and `<EmbeddedResource>` entries from `Abaki.Billing.Report.csproj`, producing a "report could not be found" build break that is easy to miss in the diff. Always `git diff` the report csproj after any `.rpt` edit and restore removed report entries before committing.

#### Quote the " B1" paths

Repeating from 16.1 because it is the most frequent day-one mistake: project folders with the " B1" suffix contain a space, so every shell reference must be quoted (`"SourceCode/Abaki.Billing.Gui B1/..."`). An unquoted path silently splits into two arguments and the command fails or, worse, operates on the wrong path.

### 16.8 Where the Services Australia specifications live

The authoritative Medicare specifications and test plan are stored outside the repo, on the shared OneDrive under the GlobalHealth source area (`.../GlobalHealth/Source/PrimaryClinic/Medicare_update/`; the exact drive-letter root is machine-specific, so it is not reproduced here per the no-local-paths rule). The current file set includes:

- `TECH.SIS_.MEDICARE.01 - COMMON RULES - V2.1.9.docx` — cross-service rules (identifiers, status codes, shared types).
- `TECH.SIS_.MEDICARE.02 - BBSW V2.1.4_20260312.pdf` — Bulk Bill Store and Forward (DB4).
- `TECH.SIS_.MEDICARE.11 - OECW - V2.0.1_20260312.1.pdf` — Online Eligibility Checking.
- `TECH.SIS_.MEDICARE.14 - IMCW - V1.1.9_1.pdf` — In-patient Medical Claim.
- `Developers Guide - V 2.1.1.pdf` — cross-service developer guide.
- `NOI Web Services Test Plan - PrimaryClinicPracticeV3.0.0.xlsx` — the NOI integration-test rows.
- The DB4 post-assignment form templates (`Path`, `DI`, `Other MBS` variants, dated 20260312), which the DB4 report-split logic mirrors.

To grep the PDFs, convert with `pdftotext` (available on the dev machine); to grep the DOCX, unzip it and read `word/document.xml`. When you need to check a claiming payload field, a form layout or a response/explanation code, read only the section touching the changed behaviour rather than the whole document. If a review agent cannot reach the spec path, install `pdftotext` locally and retry rather than skipping the compliance step.

---

## 17. Deployment: release notes and version increments

This section covers how a release is cut: when versions change, how they are chosen, and how the release notes are maintained. The mechanics are codified in `.claude/rules/release-process.md` and `.claude/rules/assembly-version.md`, and the `/MakeRelease` helper automates the version bumps.

### When versions are bumped

Assembly versions are bumped ONLY at release time, through the `/MakeRelease` flow. They are not bumped while implementing a feature or fixing a bug. Per-feature bumps create noisy diffs, conflict on every merge, and decouple the version from the release that actually shipped, so the release step owns the bump.

| Action | Bump versions? |
|---|---|
| Implementing a feature | No |
| Fixing a bug | No |
| Refactoring or test-only changes | No |
| Running `/MakeRelease` | Yes, for every project changed since the last release |

### How the version number is chosen

Each project stores its version in `Properties/AssemblyInfo.cs`, in two attributes that are kept identical:

```
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

At release time:

- Bump only the projects whose source actually changed since the last release. Work out that set by diffing against the last release tag or commit, and cover every branch merged to `Development` since then (not just the current branch):
  ```
  git log <last-release-ref>..HEAD --first-parent
  ```
- Increment the patch digit (fourth number) for a bug-fix release, or the minor digit (third number) for a feature release. For example `1.0.0.0` becomes `1.0.0.1` for a fix or `1.0.1.0` for a feature.
- Set both `AssemblyVersion` and `AssemblyFileVersion` to the same value.
- Do not bump test projects or auto-generated files.

### Always-bumped projects

Two projects are bumped to the new release version on every release even if their source did not change:

- `SourceCode/Pracnet B1` stamps `Pracnet.exe` and is what support and users quote as the build version.
- `SourceCode/Abaki.SecurityGUI` supplies the About dialog's version line. `frmLogin.GetPracnetVersion()` reads the executing assembly from inside this DLL, so if only `Pracnet B1` is bumped the About dialog shows a stale version. Keep the two in lockstep.

### Release notes

The release notes live in `SourceCode/Pracnet B1/Resources/ReleaseNote.rtf`, newest entry at the top. Each release block uses Calibri, a bold title "Release Note for version X.Y.Z", bold section headers such as "Bug fixes:" and "Enhancement:", plain bullet lines, and a separator rule between releases. When updating, strip the file and re-emit it cleanly so the formatting stays consistent (the `striprtf` library is handy for reading the existing content first).

### Not the same as the Medicare product version

The `MedicareOnlineProductVersion` Clinic setting (which feeds `dhs-productid`) and the Services Australia application registration are separate from the assembly/build version. Bumping the assembly version does not change what is sent to Medicare, and vice versa. See the Medicare Online claiming section.

### End-to-end deployment path

1. Cut the release with `/MakeRelease`, which bumps the assembly versions of the changed projects and updates `ReleaseNote.rtf`.
2. Merge to `Development`.
3. Bamboo builds the signed updater (`pracnetupdate.exe`) from `BuildScripts/PracnetBuild.bat` (see the continuous-integration and updater-build section).
4. The self-extracting updater is distributed to client sites.
5. On the next launch or update, the client runs `SetupCmd` and the database update tool, which lay down the new binaries and apply any new migration scripts (see the database-migrations section).

---

## 18. Continuous integration and the updater build

This section explains what runs in CI, and how the client updater (`pracnetupdate.exe`) is built and applied.

### What runs in Bitbucket Pipelines

The only pipeline defined in the repository is `bitbucket-pipelines.yml`, and it does one thing: on every pull request it runs a "Claude Code Review" step. That step clones the internal `global-health/scripting-operations` repository using a pipeline token and runs `code-review/claude-review.sh` against the PR. In other words, Bitbucket Pipelines is used only for automated PR review, not for compiling or shipping the product.

### How Bamboo builds the updater

The product build is produced by `BuildScripts/PracnetBuild.bat`, which Bamboo runs on a Windows build agent. The Bamboo plan configuration lives on the Bamboo server, not in the repository, so the exact plan steps and triggers should be confirmed in the Bamboo UI. What is reproducible from the repo is the script the plan executes.

The agent needs Visual Studio 2017 Professional (the script hardcodes the VS2017 MSBuild path), plus `signtool`, `nuget` and the bundled 7-Zip. `PracnetBuild.bat` does the following, in order:

1. Clears the `Output/` folder.
2. Restores NuGet packages for `SourceCode/Pracnet.sln`.
3. Runs `DeleteLicenseLicxCmd.exe` to strip `.licx` license files under `SourceCode` (Infragistics and similar).
4. MSBuild REBUILD of `SourceCode/Pracnet B1/Pracnet.csproj` in Release / x86 into `Output/PracnetUpdate`.
5. MSBuild REBUILD of `SourceCode/PracnetDatabaseUpdateTool/Abaki.Pracnet.DatabaseUpdateTool.csproj` in Release / x86 into `Output/PracnetUpdate/DatabaseUpdateModule`.
6. Copies every `Database/PracnetDatabaseFrom0.1.6/**/*.sql` into `DatabaseUpdateModule/DbScriptsPackage` (recursive, excluding the folders listed in `ExcludeSqlFolderName.txt`). This is how the migration scripts ride along inside the updater.
7. Deletes `.pdb` files from both output folders.
8. Writes a build-stamp file `PracnetUpdatedDate.txt`.
9. Copies `UpdatePackageStoringTool.exe` and `SetupCmd.exe` into the package.
10. Signs every DLL and executable (both the application package and the database update module) with `globalhealth.pfx` using `signtool` and a timestamp server. The signing password is a build-time secret embedded in the script; do not reproduce it in documentation or tickets.
11. Zips the application package to `Output/PracnetUpdate.7z`, then builds the self-extracting updater by concatenating the 7-Zip SFX module (`7zsd_All.sfx`) plus a config header (`PracnetUpdate.txt`) plus the archive into `Output/pracnetupdate.exe`. That `pracnetupdate.exe` is the updater shipped to client sites.
12. Builds and signs `ExternalSettingTool.exe` the same way (its own 7-Zip self-extracting archive) for the external settings utility.

### Legacy packaging (Paquet Builder)

The `.pbpx` and `.pbd` files in `BuildScripts`, along with `GeneratePBDScript.exe` and the commented `PBuilder.exe` calls, are a legacy packaging path (Paquet Builder). Every Paquet Builder step in the script is commented out. The current mechanism is the 7-Zip self-extracting archive described above. The files are kept for history only.

### How a client applies the update

Running `pracnetupdate.exe` on a client self-extracts the payload and runs the update flow. `SetupCmd.exe` orchestrates it: it lays down the new application binaries, then invokes the database update tool (`Abaki.Pracnet.DatabaseUpdateTool.exe`, the `frmDBUpdate` form) which applies the packaged migration scripts against the client database. See the database-migrations section for how the update tool decides which scripts to run, its per-file transactions and its "DB still behind build" safeguard. `UpdatePackageStoringTool.exe` handles storing and distributing the update package.

### Gotcha: hardcoded MSBuild path

`PracnetBuild.bat` pins the Visual Studio 2017 Professional MSBuild path. Day-to-day development machines run VS2019 or VS2022, so a local release build should use the Developer Command Prompt or the IDE rather than this script directly. The pinned path is for the Bamboo agent, which is provisioned with VS2017.

---
