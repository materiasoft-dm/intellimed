/**
 * Safety flags for a client (deceased, warnings, interpreter, minor), from data the page already has —
 * never fetches. The Waiting Room shows a compact icon + tooltip instead (clientFlagsSummary).
 */
export function ClientSafetyBanner({ deceased, warnings, interpreterRequired, interpreterLanguage, age, nextOfKinName, nextOfKinPhone }: {
  deceased?: boolean;
  warnings?: string | null;
  interpreterRequired?: boolean;
  interpreterLanguage?: string | null;
  /** null for an organisation (no date of birth) — never flagged as a minor. */
  age?: number | null;
  nextOfKinName?: string | null;
  nextOfKinPhone?: string | null;
}) {
  const isMinor = age != null && age < 18;
  if (!deceased && !warnings?.trim() && !interpreterRequired && !isMinor) return null;
  const guardian = nextOfKinName?.trim()
    ? ` Next of kin: ${nextOfKinName}${nextOfKinPhone?.trim() ? ` (${nextOfKinPhone})` : ''}.`
    : '';
  return (
    <div className="imed-safety-banner">
      {deceased && (
        <div className="alert alert-danger small py-2 mb-1"><i className="bi bi-exclamation-triangle me-1" />This client is recorded as deceased.</div>
      )}
      {warnings?.trim() && (
        <div className="alert alert-warning small py-2 mb-1"><i className="bi bi-exclamation-triangle me-1" />{warnings}</div>
      )}
      {interpreterRequired && (
        <div className="alert alert-secondary small py-2 mb-1">
          <i className="bi bi-info-circle me-1" />Interpreter required{interpreterLanguage?.trim() ? ` — ${interpreterLanguage}.` : '.'}
        </div>
      )}
      {isMinor && (
        <div className="alert alert-secondary small py-2 mb-0"><i className="bi bi-info-circle me-1" />Patient is a minor (age {age}).{guardian}</div>
      )}
    </div>
  );
}
