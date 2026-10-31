import { createContext, useContext, type ReactNode } from 'react';
import type { CurrentUserResponse, MyClinicDto } from './api/types.gen';

export interface Session {
  /** True once the user, page permissions and clinics have loaded. */
  loaded: boolean;
  user: CurrentUserResponse | null;
  userName: string;
  userEmail: string;
  roles: string[];
  /** Page keys this user's roles allow (case-insensitive), as returned by /role-permissions/my-pages. */
  accessiblePages: Set<string>;
  can: (pageKey: string) => boolean;
  clinics: MyClinicDto[];
  currentClinicId: number | null;
  currentClinicName: string | null;
  /** Re-read the unreviewed-results count shown on the sidebar badge. */
  refreshResultsBadge: () => void;
}

const SessionContext = createContext<Session | null>(null);

export function SessionProvider({ value, children }: { value: Session; children: ReactNode }) {
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

/** The signed-in user's session, provided by MainLayout. */
export function useSession(): Session {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error('useSession must be used inside MainLayout');
  return ctx;
}

export function lowerSet(values: Iterable<string>): Set<string> {
  return new Set(Array.from(values, (v) => v.toLowerCase()));
}
