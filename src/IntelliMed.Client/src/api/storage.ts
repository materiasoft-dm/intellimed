// The same localStorage keys the Blazor client used, so a signed-in session carries across the cut-over.
export const StorageKeys = {
  token: 'intellimed_token',
  userEmail: 'intellimed_user_email',
  userName: 'intellimed_user_name',
  clinicId: 'intellimed_current_clinic_id',
  sidebarCollapsed: 'intellimed_sidebar_collapsed',
} as const;

export const storage = {
  get(key: string): string | null {
    try {
      return window.localStorage.getItem(key);
    } catch {
      return null;
    }
  },
  set(key: string, value: string | null | undefined): void {
    try {
      if (value === null || value === undefined) window.localStorage.removeItem(key);
      else window.localStorage.setItem(key, value);
    } catch {
      // Storage blocked (private mode, quota): the app still works for this page load.
    }
  },
};
