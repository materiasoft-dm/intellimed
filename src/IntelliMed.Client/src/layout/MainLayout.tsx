import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Navigate, NavLink, Outlet, useLocation, useNavigate, useNavigation } from 'react-router';
import { AdminApi, ClinicApi, ClinicContext } from '@/api/admin';
import { AuthApi, LogApi } from '@/api/auth';
import { ResultApi } from '@/api/clinical';
import type { CurrentUserResponse, MyClinicDto } from '@/api/types.gen';
import { storage, StorageKeys } from '@/api/storage';
import { AppVersion } from '@/components/AppVersion';
import { CommandPalette } from '@/components/CommandPalette';
import { Dropdown } from '@/components/Dropdown';
import { ErrorBoundary } from '@/components/ErrorBoundary';
import { Modal } from '@/components/Modal';
import { NotificationBell } from '@/components/NotificationBell';
import { initials } from '@/lib/format';
import { lowerSet, SessionProvider, type Session } from '@/session';

function SideLink({ to, icon, label, end = true, onClick, children }: {
  to: string; icon: string; label: string; end?: boolean; onClick: () => void; children?: ReactNode;
}) {
  return (
    <NavLink to={to} end={end} className={({ isActive }) => `nav-link mdi-nav-link ${isActive ? 'active' : ''}`}
             onClick={onClick} title={label}>
      <i className={`bi ${icon}`} /> <span className="mdi-nav-label">{label}</span>
      {children}
    </NavLink>
  );
}

/** The signed-in shell: sidebar, top bar (clinic switcher, notifications, user menu) and the page. */
export function MainLayout() {
  const signedIn = AuthApi.isAuthenticated();
  const navigate = useNavigate();
  const location = useLocation();
  const navigation = useNavigation();

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => storage.get(StorageKeys.sidebarCollapsed) === 'true');
  const [user, setUser] = useState<CurrentUserResponse | null>(null);
  const [pages, setPages] = useState<Set<string>>(new Set());
  const [clinics, setClinics] = useState<MyClinicDto[]>([]);
  const [clinicId, setClinicId] = useState<number | null>(() => ClinicContext.get());
  const [switchTarget, setSwitchTarget] = useState<MyClinicDto | null>(null);
  const [resultsWaiting, setResultsWaiting] = useState(0);
  const [loaded, setLoaded] = useState(false);

  // close the mobile drawer on navigation
  useEffect(() => setDrawerOpen(false), [location.pathname]);

  const loadResultsBadge = useCallback(async (accessible: Set<string>, currentClinic: number | null) => {
    // needs a selected clinic, so before one is picked it just shows nothing
    if (!accessible.has('results') || currentClinic == null) return;
    const counts = await ResultApi.counts(true);
    setResultsWaiting(counts.inbox + counts.unmatched);
  }, []);

  useEffect(() => {
    if (!signedIn) return;
    let live = true;
    (async () => {
      const [me, myPages, myClinics] = await Promise.all([
        AuthApi.me(),
        AdminApi.getMyAccessiblePages(),
        ClinicApi.getMyClinics(),
      ]);
      if (!live) return;

      if (me.isAuthenticated) setUser(me);
      let accessible: Set<string>;
      if (myPages) {
        accessible = lowerSet(myPages);
      } else {
        // fallback when the permissions call fails: admins still get the admin pages
        const roles = me.roles ?? [];
        accessible = roles.some((r) => r === 'SuperAdmin' || r === 'Admin')
          ? lowerSet(['admin/users', 'admin/roles', 'admin/settings'])
          : new Set();
      }
      setPages(accessible);

      let current = ClinicContext.get();
      if (myClinics) {
        setClinics(myClinics);
        // A stored clinic (possibly left from another user's session on this browser) must belong to THIS
        // user, or every request would be scoped to a clinic they cannot access.
        if (current != null && !myClinics.some((c) => c.id === current)) {
          current = null;
          ClinicContext.clear();
        }
        // nothing selected yet: auto-select silently when the user has only one clinic
        if (current == null && myClinics.length === 1) {
          current = myClinics[0].id;
          ClinicContext.set(current);
        }
        setClinicId(current);
      }
      setLoaded(true);
      loadResultsBadge(accessible, current).catch((e) => LogApi.error(e, 'MainLayout.LoadResultsBadge'));
    })().catch((e) => LogApi.error(e, 'MainLayout.Init'));
    return () => { live = false; };
  }, [signedIn, loadResultsBadge]);

  const toggleCollapsed = () => {
    setCollapsed((c) => {
      storage.set(StorageKeys.sidebarCollapsed, String(!c));
      return !c;
    });
  };

  const signOut = async () => {
    await AuthApi.logout();
    navigate('/login');
  };

  // Force a full reload so every page's data refetches under the new clinic (pages may already have
  // started fetching under the old one).
  const chooseClinic = (clinic: MyClinicDto) => {
    ClinicContext.set(clinic.id);
    window.location.reload();
  };

  const userName = user?.fullName || user?.email || AuthApi.currentUserName() || 'User';
  const userEmail = user?.email ?? '';
  const currentClinicName = clinics.find((c) => c.id === clinicId)?.name ?? null;
  const can = useCallback((key: string) => pages.has(key.toLowerCase()), [pages]);
  const close = () => setDrawerOpen(false);

  const session = useMemo<Session>(() => ({
    loaded,
    user,
    userName,
    userEmail,
    roles: user?.roles ?? [],
    accessiblePages: pages,
    can,
    clinics,
    currentClinicId: clinicId,
    currentClinicName,
    refreshResultsBadge: () => { loadResultsBadge(pages, clinicId).catch(() => undefined); },
  }), [loaded, user, userName, userEmail, pages, can, clinics, clinicId, currentClinicName, loadResultsBadge]);

  if (!signedIn) return <Navigate to="/login" replace />;

  const anyAdmin = [...pages].some((p) => p.startsWith('admin/'));

  return (
    <SessionProvider value={session}>
      {navigation.state === 'loading' && <div className="imed-route-progress" role="progressbar" aria-label="Loading page" />}
      <div className="mdi-layout">
        {drawerOpen && <div className="mdi-overlay-backdrop" onClick={close} />}

        {/* Sidebar: a docked column on desktop, a slide-over drawer on mobile. It carries the brand,
            so the top bar only holds actions. */}
        <nav className={`mdi-sidebar ${drawerOpen ? 'mdi-sidebar-open' : ''} ${collapsed ? 'mdi-sidebar-collapsed' : ''}`} aria-label="Main">
          <div className="mdi-sidebar-header">
            <span className="mdi-logo-mark">IM</span>
            <h6 className="mb-0">IntelliMed</h6>
          </div>

          <div className="nav flex-column mdi-nav-menu">
            <SideLink to="/" icon="bi-speedometer2" label="Dashboard" onClick={close} />

            {(can('clients') || can('address-book') || can('results')) && <span className="mdi-nav-section">Clients</span>}
            {can('clients') && <SideLink to="/clients/search" icon="bi-search" label="Find Client" onClick={close} />}
            {can('address-book') && <SideLink to="/address-book" icon="bi-person-lines-fill" label="Address Book" onClick={close} />}
            {can('results') && (
              <SideLink to="/results" icon="bi-clipboard2-pulse" label="Results" onClick={close}>
                {resultsWaiting > 0 && <span className="badge rounded-pill bg-danger ms-auto mdi-nav-label" id="resultsBadge">{resultsWaiting}</span>}
              </SideLink>
            )}

            {/* every link is gated on its own page key, so a role without it never sees a failing page */}
            {can('appointments') && (
              <>
                <span className="mdi-nav-section">Appointments</span>
                <SideLink to="/appointments" icon="bi-calendar-week" label="Appointment List" onClick={close} />
              </>
            )}
            {can('appointments/create') && <SideLink to="/appointments/new" icon="bi-plus-lg" label="New Appointment" onClick={close} />}
            {can('appointments') && <SideLink to="/appointments/calendar" icon="bi-calendar3" label="Calendar View" onClick={close} />}
            {can('appointments/waiting-room') && <SideLink to="/appointments/waiting-room" icon="bi-people" label="Waiting Room" onClick={close} />}

            {can('invoices') && (
              <>
                <span className="mdi-nav-section">Invoices</span>
                <SideLink to="/invoices" icon="bi-receipt" label="Invoice List" onClick={close} />
              </>
            )}
            {can('invoices/create') && <SideLink to="/invoices/new" icon="bi-plus-lg" label="New Invoice" onClick={close} />}
            {can('payments') && <SideLink to="/invoices/payments" icon="bi-credit-card" label="Payments" onClick={close} />}
            {can('fee-schedules') && <SideLink to="/fee-schedules" icon="bi-cash-stack" label="Fee Schedules" onClick={close} />}
            {can('derived-item-configs') && <SideLink to="/derived-item-configs" icon="bi-calculator" label="Derived Item Rules" onClick={close} />}

            {/* one hub link; the /admin page shows only the set-up tiles this user can open */}
            {anyAdmin && (
              <>
                <span className="mdi-nav-section">Administration</span>
                <SideLink to="/admin" end={false} icon="bi-gear" label="Administration" onClick={close} />
              </>
            )}

            <span className="mdi-nav-section">Help</span>
            <SideLink to="/about" icon="bi-info-circle" label="About IntelliMed" onClick={close} />
            <SideLink to="/help" icon="bi-question-circle" label="User Guide" onClick={close} />
          </div>

          <div className="mdi-sidebar-footer">
            <AppVersion />
          </div>
        </nav>

        <div className="mdi-column">
          <nav className="mdi-menu-bar">
            <button className="mdi-icon-btn d-lg-none" onClick={() => setDrawerOpen((o) => !o)} title="Menu" aria-label="Menu">
              <i className="bi bi-list" />
            </button>
            <button className="mdi-icon-btn d-none d-lg-inline-flex" onClick={toggleCollapsed}
                    title={collapsed ? 'Show sidebar' : 'Hide sidebar'} aria-label={collapsed ? 'Show sidebar' : 'Hide sidebar'}>
              <i className={`bi ${collapsed ? 'bi-layout-sidebar' : 'bi-layout-sidebar-inset'}`} />
            </button>
            <div className="flex-grow-1" />

            {clinics.length > 0 && (
              <Dropdown toggleClassName="btn btn-outline-secondary border-0 dropdown-toggle d-flex align-items-center gap-2 imed-clinic-switcher"
                        toggle={<>
                          <i className="bi bi-building" />
                          <span className="d-none d-sm-inline small">{currentClinicName ?? 'Select Clinic'}</span>
                        </>}>
                {clinics.map((clinic) => (
                  <li key={clinic.id}>
                    <button type="button" className={`dropdown-item d-flex align-items-center justify-content-between ${clinic.id === clinicId ? 'active' : ''}`}
                            onClick={() => { if (clinic.id !== clinicId) setSwitchTarget(clinic); }}>
                      {clinic.name}
                      {clinic.id === clinicId && <i className="bi bi-check-lg ms-2" />}
                    </button>
                  </li>
                ))}
              </Dropdown>
            )}

            <NotificationBell />

            <Dropdown toggleClassName="mdi-user-chip dropdown-toggle" title={userName}
                      toggle={<>
                        <span className="mdi-avatar">{initials(userName)}</span>
                        <i className="bi bi-chevron-down small text-muted" />
                      </>}>
              <li className="px-3 py-2">
                <div className="fw-semibold">{userName}</div>
                {userEmail && userEmail !== userName && <div className="small text-muted">{userEmail}</div>}
              </li>
              <li><hr className="dropdown-divider" /></li>
              <li><NavLink className="dropdown-item" to="/profile"><i className="bi bi-person-gear me-2" />Profile Settings</NavLink></li>
              {can('clinic-settings') && <li><NavLink className="dropdown-item" to="/clinic-settings"><i className="bi bi-building me-2" />Clinic Settings</NavLink></li>}
              {can('clinic-manager') && <li><NavLink className="dropdown-item" to="/clinic-manager"><i className="bi bi-diagram-3 me-2" />Clinic Manager</NavLink></li>}
              <li><hr className="dropdown-divider" /></li>
              <li><button className="dropdown-item" type="button" onClick={signOut}><i className="bi bi-box-arrow-right me-2" />Sign Out</button></li>
            </Dropdown>
          </nav>

          <main className="mdi-main-content">
            <div className="container-fluid py-4 px-4">
              <ErrorBoundary>
                <div className="imed-page" key={location.pathname}>
                  <Outlet />
                </div>
              </ErrorBoundary>
            </div>
          </main>
        </div>
      </div>

      {(user?.showCommandPalette ?? true) && <CommandPalette accessiblePages={pages} />}

      {/* Mandatory clinic choice: the user belongs to several clinics and has not picked one this session.
          It blocks the UI so a clinic can't be forgotten or left on a stale choice. */}
      {loaded && clinicId == null && clinics.length > 1 && (
        <Modal title="Select a Clinic" titleTag="h5" headerClassName="" staticBackdrop
               footer={<button type="button" className="btn btn-outline-secondary" onClick={signOut}>
                 <i className="bi bi-box-arrow-right me-1" /> Sign Out
               </button>}>
          <p>You belong to multiple clinics. Choose which one you're working in for this session.</p>
          <div className="list-group">
            {clinics.map((clinic) => (
              <button key={clinic.id} type="button" className="list-group-item list-group-item-action d-flex align-items-center gap-2"
                      onClick={() => chooseClinic(clinic)}>
                <i className="bi bi-building" /> {clinic.name}
              </button>
            ))}
          </div>
        </Modal>
      )}

      {switchTarget && (
        <Modal title="Switch Clinic" titleTag="h5" headerClassName="" onClose={() => setSwitchTarget(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setSwitchTarget(null)}>Cancel</button>
                 <button type="button" className="btn btn-primary" onClick={() => chooseClinic(switchTarget)}>
                   <i className="bi bi-arrow-repeat me-1" /> Switch
                 </button>
               </>}>
          <p>Switch to <strong>{switchTarget.name}</strong>? Client, appointment, and invoice lists will reload for this clinic.</p>
        </Modal>
      )}
    </SessionProvider>
  );
}

/** Pages shown before sign-in (login, forgot/set password). */
export function PublicLayout() {
  return <Outlet />;
}
