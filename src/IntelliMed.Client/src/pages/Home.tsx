import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { ClientApi } from '@/api/clients';
import { LogApi } from '@/api/auth';
import { AccountTypeEnum, type ClientDto } from '@/api/types.gen';
import { BarChart } from '@/components/BarChart';
import { formatDate, toDate } from '@/lib/dates';
import { humanize } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { useSession } from '@/session';

interface Stats {
  total: number;
  active: number;
  archived: number;
  newThisWeek: number;
  recent: ClientDto[];
  byAccountType: { label: string; count: number }[];
}

function StatTile({ icon, tone, value, label }: { icon: string; tone: string; value: number | null; label: string }) {
  return (
    <div className="col-sm-6 col-lg-3">
      <div className="card h-100">
        <div className="card-body d-flex align-items-center gap-3">
          <span className={`stat-icon-badge stat-icon-badge--${tone}`}><i className={`bi ${icon}`} /></span>
          <div>
            {value === null
              ? <div className="stat-tile-value"><span className="imed-skeleton-bar" style={{ width: 48, height: '1.4rem' }} /></div>
              : <div className="stat-tile-value">{value.toLocaleString('en-AU')}</div>}
            <div className="text-muted small">{label}</div>
          </div>
        </div>
      </div>
    </div>
  );
}

function QuickLink({ to, icon, tone, title, text }: { to: string; icon: string; tone: string; title: string; text: string }) {
  return (
    <div className="col-sm-6 col-lg-3">
      <Link to={to} className="text-decoration-none">
        <div className="card h-100 imed-card-link">
          <div className="card-body d-flex align-items-center gap-3">
            <span className={`stat-icon-badge stat-icon-badge--${tone}`}><i className={`bi ${icon}`} /></span>
            <div>
              <div className="fw-semibold">{title}</div>
              <div className="text-muted small">{text}</div>
            </div>
            <i className="bi bi-chevron-right ms-auto text-muted small imed-quick-link-chevron" />
          </div>
        </div>
      </Link>
    </div>
  );
}

export default function Home() {
  usePageTitle('IntelliMed - Dashboard');
  const session = useSession();
  const [stats, setStats] = useState<Stats | null>(null);

  useMountEffect(async (alive) => {
    try {
      const result = await ClientApi.search({ page: 1, pageSize: 1000, includeArchived: true });
      const clients = result.items;
      const weekAgo = Date.now() - 7 * 24 * 3600 * 1000;
      const counts = new Map<AccountTypeEnum, number>();
      for (const c of clients) counts.set(c.accountType, (counts.get(c.accountType) ?? 0) + 1);
      if (!alive()) return;
      setStats({
        total: clients.length,
        active: clients.filter((c) => c.isActive).length,
        archived: clients.filter((c) => !c.isActive).length,
        newThisWeek: clients.filter((c) => (toDate(c.createdAt)?.getTime() ?? 0) >= weekAgo).length,
        recent: [...clients].sort((a, b) => (toDate(b.createdAt)?.getTime() ?? 0) - (toDate(a.createdAt)?.getTime() ?? 0)).slice(0, 5),
        byAccountType: [...counts.entries()]
          .map(([type, count]) => ({ label: humanize(AccountTypeEnum[type] ?? String(type)), count }))
          .sort((a, b) => b.count - a.count),
      });
    } catch (e) {
      LogApi.error(e, 'Home.LoadClientStatsAsync');
      if (alive()) setStats({ total: 0, active: 0, archived: 0, newThisWeek: 0, recent: [], byAccountType: [] });
    }
  });

  const labels = useMemo(() => stats?.byAccountType.map((x) => x.label) ?? [], [stats]);
  const data = useMemo(() => stats?.byAccountType.map((x) => x.count) ?? [], [stats]);
  const showAdminCard = [...session.accessiblePages].some((p) => p.startsWith('admin/'));
  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';

  return (
    <>
      <h3 className="mb-1">Welcome, {session.user ? session.userName : '…'}</h3>
      <p className="text-muted mb-4">{greeting} — here's what's happening across your practice today.</p>

      <div className="row g-3 mb-3">
        <StatTile icon="bi-people" tone="blue" value={stats?.total ?? null} label="Total Clients" />
        <StatTile icon="bi-person-check" tone="green" value={stats?.active ?? null} label="Active Clients" />
        <StatTile icon="bi-person-plus" tone="amber" value={stats?.newThisWeek ?? null} label="New This Week" />
        <StatTile icon="bi-archive" tone="purple" value={stats?.archived ?? null} label="Archived" />
      </div>

      <div className="row g-3 mb-3">
        <div className="col-lg-7">
          <div className="card h-100">
            <div className="card-body">
              <h6 className="mb-1">Clients by Account Type</h6>
              <p className="text-muted small mb-3">Distribution across all registered clients.</p>
              {!stats ? (
                <div className="imed-chart-placeholder" style={{ height: 260 }} aria-hidden="true" />
              ) : stats.total === 0 ? (
                <p className="text-muted small">No clients yet — add one to see this chart.</p>
              ) : (
                <BarChart labels={labels} data={data} label="Clients by account type" />
              )}
            </div>
          </div>
        </div>
        <div className="col-lg-5">
          <div className="card h-100">
            <div className="card-body">
              <div className="d-flex justify-content-between align-items-center mb-1">
                <h6 className="mb-0">Recent Clients</h6>
                <Link to="/clients/search" className="small text-decoration-none">View all</Link>
              </div>
              <p className="text-muted small mb-3">Most recently registered.</p>
              {!stats ? (
                <ul className="list-group list-group-flush" aria-hidden="true">
                  {[0, 1, 2, 3, 4].map((i) => (
                    <li key={i} className="list-group-item px-0 d-flex justify-content-between align-items-center">
                      <span className="imed-skeleton-bar" style={{ width: `${45 + i * 7}%` }} />
                      <span className="imed-skeleton-bar" style={{ width: 70 }} />
                    </li>
                  ))}
                </ul>
              ) : stats.recent.length === 0 ? (
                <p className="text-muted small">No clients yet.</p>
              ) : (
                <ul className="list-group list-group-flush">
                  {stats.recent.map((c) => (
                    <li key={c.id} className="list-group-item px-0 d-flex justify-content-between align-items-center">
                      <Link to={`/clients/edit/${c.id}`} className="text-reset text-decoration-none imed-recent-client">{c.fullName}</Link>
                      <span className="text-muted small">{formatDate(c.createdAt, 'dd/MM/yyyy')}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </div>
      </div>

      <div className="row g-3">
        <QuickLink to="/clients/search" icon="bi-search" tone="blue" title="Find Client" text="Search and manage clients" />
        <QuickLink to="/clients/add" icon="bi-plus-lg" tone="green" title="Add Client" text="Register a new client" />
        <QuickLink to="/appointments" icon="bi-calendar-week" tone="amber" title="Appointments" text="Schedule and review visits" />
        <QuickLink to="/invoices" icon="bi-receipt" tone="purple" title="Invoices" text="Billing and payments" />
        {showAdminCard && <QuickLink to="/admin" icon="bi-shield-lock" tone="blue" title="Administration" text="Set-up and configuration pages" />}
      </div>
    </>
  );
}
