import type { ReactNode } from 'react';
import { LoadingState } from './States';

interface ClinicDetails {
  clinicName: string | null;
  clinicAddress: string | null;
  clinicSuburb: string | null;
  clinicState: string | null;
  clinicPostcode: string | null;
  clinicPhone: string | null;
  clinicEmail: string | null;
  clinicAbn: string | null;
}

const has = (s: string | null | undefined) => !!s && !!s.trim();

/** The practice's name, address and contact block at the top of an invoice or receipt. */
export function ClinicLetterhead({ clinic }: { clinic: ClinicDetails }) {
  return (
    <div>
      <h4 className="mb-1">{clinic.clinicName ?? 'Clinic'}</h4>
      {has(clinic.clinicAddress) && <div>{clinic.clinicAddress}</div>}
      {(has(clinic.clinicSuburb) || has(clinic.clinicState) || has(clinic.clinicPostcode)) && (
        <div>{[clinic.clinicSuburb, clinic.clinicState, clinic.clinicPostcode].filter(has).join(' ')}</div>
      )}
      {(has(clinic.clinicPhone) || has(clinic.clinicEmail)) && (
        <div>{[clinic.clinicPhone, clinic.clinicEmail].filter(has).join(' · ')}</div>
      )}
      {has(clinic.clinicAbn) && <div>ABN: {clinic.clinicAbn}</div>}
    </div>
  );
}

/** A printable document: a Print button (hidden when printing), then the loading / missing / content state. */
export function PrintPage({ loading, loadingText, missing, missingText, maxWidth = 900, extraToolbar, children }: {
  loading: boolean;
  loadingText: string;
  missing: boolean;
  missingText: string;
  maxWidth?: number;
  extraToolbar?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="imed-print-page">
      <div className="no-print p-3 d-flex justify-content-end gap-2 imed-print-toolbar">
        {extraToolbar}
        <button className="btn btn-primary" onClick={() => window.print()} disabled={loading || missing}><i className="bi bi-printer" /> Print</button>
      </div>
      {loading ? (
        <div className="p-4"><LoadingState text={loadingText} compact={false} /></div>
      ) : missing ? (
        <div className="p-4"><div className="alert alert-danger">{missingText}</div></div>
      ) : (
        <div className="p-4 imed-print-document" style={{ maxWidth, margin: '0 auto' }}>{children}</div>
      )}
    </div>
  );
}
