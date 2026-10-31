import { useState } from 'react';
import { Link } from 'react-router';
import { EmptyState, LoadingState } from '@/components/States';
import { usePageTitle } from '@/lib/hooks';
import { useSession } from '@/session';

interface AdminTile { title: string; description: string; icon: string; href: string; pageKey: string }
interface AdminSection { title: string; badgeColor: string; tiles: AdminTile[] }

// Set-up/configuration pages only. Everyday working pages (clients, appointments, invoices) keep their own
// sidebar entries and don't belong here.
const ALL_SECTIONS: AdminSection[] = [
  {
    title: 'Security & Access', badgeColor: 'blue', tiles: [
      { title: 'User Management', description: 'Manage system users', icon: 'bi-person', href: '/admin/users', pageKey: 'admin/users' },
      { title: 'Role Configuration', description: 'Configure role permissions', icon: 'bi-shield-lock', href: '/admin/roles', pageKey: 'admin/roles' },
    ],
  },
  {
    title: 'Scheduling', badgeColor: 'amber', tiles: [
      { title: 'Appointment Types', description: 'Configure the appointment-type/duration-preset catalogue', icon: 'bi-tags', href: '/admin/appointment-types', pageKey: 'admin/appointment-types' },
    ],
  },
  {
    title: 'Communications', badgeColor: 'green', tiles: [
      { title: 'Email Templates', description: 'Author and assign email templates to system events (invite, forgot password)', icon: 'bi-envelope-paper', href: '/admin/email-templates', pageKey: 'admin/email-templates' },
    ],
  },
  {
    title: 'Clinical Reference Data', badgeColor: 'purple', tiles: [
      { title: 'Medicine Catalog', description: 'Manage the medicines reference-data catalog and its CSV import', icon: 'bi-capsule', href: '/admin/medicines', pageKey: 'admin/medicines' },
      { title: 'Drug Interactions', description: 'Manage the drug-interaction reference table used by the prescribing safety check', icon: 'bi-shield-exclamation', href: '/admin/drug-interactions', pageKey: 'admin/drug-interactions' },
    ],
  },
  {
    title: 'System', badgeColor: 'blue', tiles: [
      { title: 'Command Palette Actions', description: 'Manage the entries searchable from the global command palette', icon: 'bi-command', href: '/admin/search-actions', pageKey: 'admin/search-actions' },
      { title: 'System Settings', description: 'Configure system parameters', icon: 'bi-gear', href: '/admin/settings', pageKey: 'admin/settings' },
      { title: 'Database Backups', description: 'Configure and manage scheduled SQLite database backups', icon: 'bi-hdd-stack', href: '/admin/database-backups', pageKey: 'admin/database-backups' },
    ],
  },
];

export default function Administration() {
  usePageTitle('Administration - IntelliMed');
  const session = useSession();
  const [search, setSearch] = useState('');

  const term = search.trim().toLowerCase();
  const visible = ALL_SECTIONS
    .map((s) => ({
      ...s,
      tiles: s.tiles
        .filter((t) => session.can(t.pageKey))
        .filter((t) => !term || t.title.toLowerCase().includes(term) || t.description.toLowerCase().includes(term)),
    }))
    .filter((s) => s.tiles.length > 0);

  return (
    <>
      <h3 className="mb-1">Administration</h3>
      <p className="text-muted mb-4">Set-up and configuration pages for the practice.</p>

      <div className="mb-4">
        <div className="input-group" style={{ maxWidth: 360 }}>
          <span className="input-group-text bg-white"><i className="bi bi-search" /></span>
          <input className="form-control" placeholder="Search administration pages..." value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
      </div>

      {!session.loaded ? (
        <LoadingState text="Loading..." compact={false} />
      ) : visible.length === 0 ? (
        <EmptyState message="No matching administration pages." icon="bi-search" compact={false} />
      ) : visible.map((section) => (
        <div key={section.title}>
          <h6 className="text-muted text-uppercase small mt-4 mb-2">{section.title}</h6>
          <div className="row g-3 mb-2">
            {section.tiles.map((tile) => (
              <div key={tile.href} className="col-sm-6 col-lg-4">
                <Link to={tile.href} className="text-decoration-none">
                  <div className="card h-100 imed-card-link">
                    <div className="card-body d-flex align-items-center gap-3">
                      <span className={`stat-icon-badge stat-icon-badge--${section.badgeColor}`}><i className={`bi ${tile.icon}`} /></span>
                      <div>
                        <div className="fw-semibold">{tile.title}</div>
                        <div className="text-muted small">{tile.description}</div>
                      </div>
                      <i className="bi bi-chevron-right ms-auto text-muted small imed-quick-link-chevron" />
                    </div>
                  </div>
                </Link>
              </div>
            ))}
          </div>
        </div>
      ))}
    </>
  );
}
