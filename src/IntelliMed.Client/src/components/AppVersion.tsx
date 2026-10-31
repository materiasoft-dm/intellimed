import { useEffect, useState } from 'react';
import { VersionApi } from '@/api/auth';

let cached: Promise<string> | null = null;

/**
 * Small, muted version tag from GET api/version (IntelliMed.Api.csproj's <Version>), so a stale deploy is
 * visible at a glance. Used on the login page and in the sidebar; fetched once per page load.
 */
export function AppVersion() {
  const [version, setVersion] = useState('…');
  useEffect(() => {
    let live = true;
    (cached ??= VersionApi.get()).then((v) => live && setVersion(v));
    return () => { live = false; };
  }, []);
  return <span className="text-muted small">v{version}</span>;
}
