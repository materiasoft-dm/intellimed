# React port — status

The Blazor WebAssembly front end (`src/IntelliMed.Web`) is being replaced by this React app in one go.
This file tracks the port until it is finished; delete it (and fold anything durable into
`STRUCTURE.md` / `ARCHITECTURE.md`) once Blazor is gone.

## Decisions

- **Stack:** Vite 7 + React 19 + TypeScript 5.9, React Router 7 (same URLs as Blazor), TanStack Query 5.
- **Look:** Bootstrap 5.3 + Bootstrap Icons bundled from npm (no CDN), Inter bundled via @fontsource.
  `src/styles/app.css` is the Blazor `app.css` carried over; `src/styles/polish.css` is the refinement
  layer on top. JSX keeps the Blazor markup's Bootstrap classes, ids and label→input structure, because
  the Playwright suite (`IntelliMed.Tests/Ui`) selects on them — it is the parity check for the port.
- **Replacements:** Radzen DataGrid → `components/DataTable`; RadzenScheduler → FullCalendar 6;
  RadzenHtmlEditor → TipTap; chart-interop.js → Chart.js directly; Bootstrap JS dropdowns → `Dropdown`.
- **Types:** `src/api/types.gen.ts` is generated from `IntelliMed.Core` DTOs and enums by
  `scripts/generate-client-types.py`. Never hand-edit it; re-run the script after changing a DTO.
  JSON is camelCase and enums are numbers (the API has no JsonStringEnumConverter).
- **Auth:** same localStorage keys as Blazor (`intellimed_token`, `intellimed_user_email`,
  `intellimed_user_name`, `intellimed_current_clinic_id`), so cut-over does not sign anyone out.
- **Hosting:** `npm run build` → `dist/`; the API serves it from its wwwroot (Api.csproj copies `dist`).
  Dev: `npm run dev` (port 5173) proxies `/api` to the API on 5284.
- **Lists:** same paging as Blazor; Address Book and Medicines admin (which silently capped at 100 / 20
  rows) move to infinite scroll.

## Shared React pieces (use these, don't re-invent)

- `components/Modal` (+ `ConfirmModal`), `Dropdown`, `ContextMenu`, `Toast` (`useToast`), `States` (Loading/Empty/Skeleton),
  `DataGrid` (+ `Pager`; rows have `imed-data-row`), `ClientPicker`, `ClientSafetyBanner`, `AppointmentDetails`, `Inputs`
  (NumberInput/TextInput/TextArea/DateInput/TimeInput/NumberSelect/EnumOptions — they bind like Blazor's @bind), `RichTextEditor`,
  `BarChart`, `StatusBadges`.
- `lib/hooks` (usePageTitle, useMountEffect, usePagedGrid, useLatestSearch, useDebounced), `lib/infinite` (useInfiniteList +
  LoadMoreSentinel), `lib/dates` (formatDate = .NET patterns; formatUtc for server UTC audit stamps), `lib/format` (money =
  .NET "C"), `lib/validation` (DataAnnotations messages), `lib/permissionTree` (port of Core PermissionTree, unit tested).
- Generated `defaultX()` factories = C# `new X()`; spread them for request DTOs.

## Parity rules learnt so far

- Keep element roles: a Blazor `<button>` stays a button (tests use GetByRole(Button)), links stay links.
- Keep `confirm()` where Blazor used `JS.InvokeAsync<bool>("confirm")` — tests accept native dialogs.
- Keep label → input sibling structure (tests' `Field()` helper is an XPath following-sibling).
- Icon buttons keep `title` (tests use GetByTitle) but get a distinct `aria-label` so role-name lookups stay unique.
- Tests that used `.rz-data-row` (AppointmentTests 189/232, FeeScheduleTests 53, InvoiceTests 278/297) must switch to `.imed-data-row`.

## Inventory (Blazor file → status)

Legend: ☑ not started · ◐ in progress · ☑ ported

### Shell & shared
☑ App.razor (router, error boundary) · ☑ Layout/MainLayout · ☑ Layout/PublicLayout
☑ AddressBookPicker · ☑ AppVersion · ☑ AppointmentDetailsModal · ☑ AppointmentDetailsPanel
☑ ClientSafetyBanner · ☑ CommandPalette · ☑ EmptyState · ☑ ErrorDisplay · ☑ FindClientModal
— IMed* (Button/Card/Drawer/Modal/PageHeader/Table): unused in Blazor, not ported · ☑ LoadingState · ☑ NotificationBell
☑ OrganisationPicker · ☑ PermissionTreeNode · ☑ ResizableTable (not needed — DataGrid scrolls sideways)

### Pages
☑ Login · ☑ ForgotPassword · ☑ SetPassword · ☑ NotFound · ☑ Home
☑ ClientSearch · ☑ AddClient (2854 lines) · ☑ AddressBook · ☑ Results
☑ AppointmentList · ☑ NewAppointment · ☑ AppointmentEdit · ☑ AppointmentCalendar · ☑ AppointmentPrint
☑ WaitingRoom · ☑ AppointmentTypeSettings
☑ InvoiceSearch · ☑ AddInvoice · ☑ InvoiceDetail · ☑ InvoicePayments · ☑ InvoicePrint · ☑ ReceiptPrint
☑ SplitInvoice · ☑ FeeSchedules · ☑ DerivedItemConfigs
☑ PrescribeNew · ☑ PrescriptionPrint · ☑ ProgressNoteEditor
☑ Administration · ☑ UserManagement · ☑ RoleConfiguration · ☑ ClinicManager · ☑ ClinicSettings
☑ SystemSettings · ☑ DatabaseBackups · ☑ MedicinesAdmin · ☑ DrugInteractionsAdmin
☑ EmailTemplateList · ☑ EmailTemplateEdit · ☑ SearchActionList · ☑ SearchActionEdit
☑ ProfileSettings · ☑ Help

### Cut-over
☑ Api.csproj builds the client (incremental npm build) and serves `dist` · ☑ Dockerfile builds the client in a Node stage
☐ Playwright suite green against React · ☐ remove `IntelliMed.Web` · ☐ docs updated
