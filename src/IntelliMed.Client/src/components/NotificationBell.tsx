import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { NotificationApi } from '@/api/settings';
import type { NotificationDto } from '@/api/types.gen';
import { formatDate } from '@/lib/dates';
import { Dropdown } from './Dropdown';

/** Unread-count bell; polls every 30 s and loads the latest 20 when opened. */
export function NotificationBell() {
  const navigate = useNavigate();
  const [unread, setUnread] = useState(0);
  const [recent, setRecent] = useState<NotificationDto[]>([]);
  const [loaded, setLoaded] = useState(false);

  const refresh = useCallback(async () => setUnread(await NotificationApi.unreadCount()), []);

  useEffect(() => {
    refresh();
    const timer = setInterval(refresh, 30_000);
    return () => clearInterval(timer);
  }, [refresh]);

  const onOpen = async () => {
    setRecent(await NotificationApi.recent());
    setLoaded(true);
  };

  const open = async (n: NotificationDto) => {
    if (!n.isRead) {
      await NotificationApi.markRead(n.id);
      setRecent((all) => all.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
      setUnread((c) => Math.max(0, c - 1));
    }
    if (n.linkUrl) navigate(n.linkUrl);
  };

  const markAll = async () => {
    await NotificationApi.markAllRead();
    setRecent((all) => all.map((x) => ({ ...x, isRead: true })));
    setUnread(0);
  };

  return (
    <Dropdown className="me-2" title="Notifications" onOpen={onOpen}
              toggleClassName="btn btn-outline-secondary border-0 position-relative"
              menuClassName="imed-notification-menu" menuStyle={{ width: 340, maxHeight: 420, overflowY: 'auto' }}
              toggle={<>
                <i className="bi bi-bell" />
                {unread > 0 && (
                  <span className="badge rounded-pill bg-danger position-absolute top-0 start-100 translate-middle" style={{ fontSize: '0.6rem' }}>
                    {unread > 99 ? '99+' : unread}
                  </span>
                )}
              </>}>
      <li className="d-flex justify-content-between align-items-center px-3 py-1">
        <strong>Notifications</strong>
        {unread > 0 && <button className="btn btn-link btn-sm p-0" type="button" onClick={markAll}>Mark all read</button>}
      </li>
      <li><hr className="dropdown-divider" /></li>
      {!loaded && <li className="px-3 py-2"><span className="spinner-border spinner-border-sm text-muted" aria-hidden="true" /></li>}
      {loaded && recent.length === 0 && (
        <li className="px-3 py-3 text-muted small text-center"><i className="bi bi-bell-slash d-block fs-4 mb-1 opacity-50" aria-hidden="true" />No notifications</li>
      )}
      {recent.map((n) => (
        <li key={n.id}>
          <button className={`dropdown-item imed-notification ${n.isRead ? '' : 'fw-bold imed-notification--unread'}`} type="button" onClick={() => open(n)}>
            <div className="small text-wrap">{n.message}</div>
            <div className="text-muted" style={{ fontSize: '0.7rem' }}>{formatDate(n.createdAt, 'g')}</div>
          </button>
        </li>
      ))}
    </Dropdown>
  );
}
