import { useState, type ReactNode } from 'react';
import { useParams } from 'react-router';
import { PrescriptionApi } from '@/api/clinical';
import { MedicineSchedule, PbsRestrictionLevel, type PrescriptionIssueDto } from '@/api/types.gen';
import { PrintPage } from '@/components/PrintLayout';
import { formatDate, formatUtc } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

const filled = (v: string | null | undefined): v is string => !!v?.trim();

function Row({ label, children, width }: { label: string; children: ReactNode; width?: number }) {
  return (
    <tr>
      <td className="text-muted" style={width ? { width } : undefined}>{label}</td>
      <td>{children}</td>
    </tr>
  );
}

export default function PrescriptionPrint() {
  const issueId = Number(useParams().issueId);
  const [issue, setIssue] = useState<PrescriptionIssueDto | null>(null);
  const [loading, setLoading] = useState(true);
  usePageTitle(`Script ${issue?.scriptNumber ?? ''} - IntelliMed`);

  useMountEffect(async (alive) => {
    const i = await PrescriptionApi.getIssue(issueId);
    if (!alive()) return;
    setIssue(i);
    setLoading(false);
  });

  return (
    <PrintPage loading={loading} loadingText="Loading script..." missing={!issue} missingText="Prescription issue not found." maxWidth={700}>
      {issue && (
        <>
          {issue.schedule === MedicineSchedule.S8 && (
            <div className="no-print alert alert-warning small py-2">
              <i className="bi bi-exclamation-triangle me-1" />
              Schedule 8 — verify this satisfies your state's S8 prescribing requirements before use.
            </div>
          )}
          <div className="d-flex justify-content-between align-items-start mb-4">
            <div>
              <h5 className="mb-1">{issue.prescriberName}</h5>
              {filled(issue.prescriberQualifications) && <div className="small">{issue.prescriberQualifications}</div>}
              <div className="row g-1 small mt-1">
                {filled(issue.prescriberProviderNumber) && <div className="col-12">Provider No: {issue.prescriberProviderNumber}</div>}
                {filled(issue.prescriberAhpraNumber) && <div className="col-12">AHPRA: {issue.prescriberAhpraNumber}</div>}
                {filled(issue.prescriberHpiiNumber) && <div className="col-12">HPI-I: {issue.prescriberHpiiNumber}</div>}
              </div>
            </div>
            <div className="text-end">
              <div className="d-flex gap-1 justify-content-end mb-1">
                {issue.isPbsListed && <span className="badge bg-info text-dark">PBS</span>}
                {issue.isRpbsListed && <span className="badge bg-info text-dark">RPBS</span>}
                {issue.restrictionLevel != null && (
                  <span className={`badge ${issue.restrictionLevel === PbsRestrictionLevel.AuthorityRequired ? 'bg-danger' : 'bg-warning text-dark'}`}>
                    {issue.restrictionLevel === PbsRestrictionLevel.AuthorityRequired ? 'Authority Required' : PbsRestrictionLevel[issue.restrictionLevel]}
                  </span>
                )}
                {issue.schedule != null && issue.schedule !== MedicineSchedule.Unscheduled && (
                  <span className="badge bg-secondary">{MedicineSchedule[issue.schedule]}</span>
                )}
              </div>
              <div><strong>Script No: {issue.scriptNumber}</strong></div>
              {issue.repeatNumber > 0 && <div className="small">Repeat {issue.repeatNumber}</div>}
              <div className="small">{formatUtc(issue.issuedAt, 'dd/MM/yyyy')}</div>
            </div>
          </div>
          <div className="row mb-4">
            <div className="col-8">
              <div className="text-muted small">Patient</div>
              <div className="fw-semibold">{issue.clientName}</div>
              <div className="small">DOB: {formatDate(issue.clientDateOfBirth)}</div>
              {filled(issue.clientMedicareNumber) && <div className="small">Medicare: {issue.clientMedicareNumber}</div>}
            </div>
          </div>
          <table className="table table-sm table-bordered">
            <tbody>
              <Row label="Medicine" width={140}><strong>{issue.medicineName}</strong></Row>
              {(filled(issue.dose) || filled(issue.frequency) || filled(issue.route)) && (
                <Row label="Dose">{[issue.dose, issue.frequency, issue.route].filter(Boolean).join(' ')}</Row>
              )}
              {filled(issue.quantity) && <Row label="Quantity">{issue.quantity}</Row>}
              <Row label="Repeats">{issue.repeats}</Row>
              {filled(issue.authorityApprovalNumber) && <Row label="Authority No.">{issue.authorityApprovalNumber}</Row>}
              {filled(issue.stateAuthorityCode) && <Row label="State Authority">{issue.stateAuthorityCode}</Row>}
              {filled(issue.prescribingNotes) && <Row label="Notes">{issue.prescribingNotes}</Row>}
              {filled(issue.notesForPharmacy) && <Row label="For pharmacy">{issue.notesForPharmacy}</Row>}
            </tbody>
          </table>
          <div className="small text-muted mt-4">Prescriber signature: _______________________</div>
        </>
      )}
    </PrintPage>
  );
}
