import { useState, type ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { PractitionerApi } from '@/api/appointments';
import { ClientApi, EpisodeApi } from '@/api/clients';
import { MedicineApi, PrescriptionApi } from '@/api/clinical';
import {
  defaultCreateEpisodeDto, defaultCreatePrescriptionDto, defaultIssuePrescriptionDto, InteractionSeverity, MedicineSchedule, PbsRestrictionLevel, PrescriptionSourceCode,
  AllergySeverity, type ClientDto, type CreatePrescriptionDto, type EpisodeDto, type MedicationSafetyCheckResultDto,
  type MedicineDto, type PractitionerDto,
} from '@/api/types.gen';
import { ChoiceWithOther, DateInput, EnumOptions, NumberInput, NumberSelect, SuggestInput, TextArea, TextInput } from '@/components/Inputs';
import { LoadingState } from '@/components/States';
import { todayIso } from '@/lib/dates';
import { FREQUENCIES, INSTRUCTIONS, restrictionBadgeClass, restrictionLabel, ROUTES, scheduleBadgeClass } from '@/lib/medicines';
import { useLatestSearch, useMountEffect, usePageTitle } from '@/lib/hooks';
import { useSession } from '@/session';

enum WizardStep { Medicine, Dosage, QuantityRepeats, Authority, StateAuthority, Reason, Review }
const ALL_STEPS = [WizardStep.Medicine, WizardStep.Dosage, WizardStep.QuantityRepeats, WizardStep.Authority,
  WizardStep.StateAuthority, WizardStep.Reason, WizardStep.Review];

// Card headers keep legacy's fixed step numbers (6. Reason stays 6 when Quantity/Authority are skipped).
const STEP_NUMBERS: Record<WizardStep, number> = {
  [WizardStep.Medicine]: 1, [WizardStep.Dosage]: 2, [WizardStep.QuantityRepeats]: 3, [WizardStep.Authority]: 4,
  [WizardStep.StateAuthority]: 5, [WizardStep.Reason]: 6, [WizardStep.Review]: 7,
};

const STEP_LABELS: Record<WizardStep, string> = {
  [WizardStep.Medicine]: 'Medicine',
  [WizardStep.Dosage]: 'Dosage',
  [WizardStep.QuantityRepeats]: 'Quantity',
  [WizardStep.Authority]: 'Authority',
  [WizardStep.StateAuthority]: 'State Auth.',
  [WizardStep.Reason]: 'Reason',
  [WizardStep.Review]: 'Review',
};

const PURPOSE_LABELS: Partial<Record<number, string>> = {
  [PrescriptionSourceCode.PrintedPrescription]: 'Printed prescription',
  [PrescriptionSourceCode.HandwrittenPrescription]: 'Handwritten prescription',
  [PrescriptionSourceCode.ProductAdvisedHere]: 'Product advised here',
  [PrescriptionSourceCode.ProductSuppliedHere]: 'Product supplied here',
  [PrescriptionSourceCode.AdvisedOrPrescribedElsewhere]: 'Advised or prescribed elsewhere',
};

/** Schedule / PBS / RPBS / restriction badges for a catalogue medicine. */
function MedicineBadges({ medicine, spaced }: { medicine: MedicineDto; spaced?: boolean }) {
  const ms = spaced ? ' ms-1' : '';
  return (
    <>
      {medicine.schedule != null && <span className={`badge ${scheduleBadgeClass(medicine.schedule)}${ms}`}>{MedicineSchedule[medicine.schedule]}</span>}
      {medicine.isPbsListed && <span className={`badge bg-info text-dark${ms}`}>PBS</span>}
      {medicine.isRpbsListed && <span className={`badge bg-info text-dark${ms}`}>RPBS</span>}
      {medicine.restrictionLevel != null && (
        <span className={`badge ${restrictionBadgeClass(medicine.restrictionLevel)}${ms}`}>{restrictionLabel(medicine.restrictionLevel)}</span>
      )}
    </>
  );
}

const describe = (m: MedicineDto, withManufacturer: boolean) => [
  m.genericName, m.strength, m.form,
  m.packSize?.trim() ? `— pack of ${m.packSize}` : null,
  withManufacturer && m.manufacturer?.trim() ? `(${m.manufacturer})` : null,
].filter(Boolean).join(' ');

/** Quantity is free text ("2 x 30", "1 tube"), so only a cleanly numeric value can be compared against a limit. */
const numericQuantity = (q: string | null): number | null => {
  const t = q?.trim() ?? '';
  return /^[+-]?\d+$/.test(t) ? Number(t) : null;
};

export default function PrescribeNew() {
  const clientId = Number(useParams().clientId);
  const navigate = useNavigate();
  const session = useSession();
  usePageTitle('New Prescription - IntelliMed');

  const [client, setClient] = useState<ClientDto | null>(null);
  const [practitioners, setPractitioners] = useState<PractitionerDto[]>([]);
  const [episodes, setEpisodes] = useState<EpisodeDto[]>([]);
  const [form, setForm] = useState<CreatePrescriptionDto>(() => ({ ...defaultCreatePrescriptionDto(), clientId, startDate: todayIso() }));
  const [selected, setSelected] = useState<MedicineDto | null>(null);
  const [safety, setSafety] = useState<MedicationSafetyCheckResultDto | null>(null);
  const [step, setStep] = useState(WizardStep.Medicine);
  const [stepError, setStepError] = useState<string | null>(null);
  const [rbAcknowledged, setRbAcknowledged] = useState(false);
  const [term, setTerm] = useState('');
  const search = useLatestSearch((t: string) => MedicineApi.search(t), [] as MedicineDto[]);
  const [newEpisodeTitle, setNewEpisodeTitle] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [createdId, setCreatedId] = useState<number | null>(null);
  const [createdScriptNumber, setCreatedScriptNumber] = useState<string | null>(null);
  const [issuing, setIssuing] = useState(false);

  const set = (patch: Partial<CreatePrescriptionDto>) => setForm((f) => ({ ...f, ...patch }));

  useMountEffect(async (alive) => {
    const [c, p, e] = await Promise.all([ClientApi.getById(clientId), PractitionerApi.active(), EpisodeApi.list(clientId)]);
    if (!alive()) return;
    setClient(c);
    setPractitioners(p);
    setEpisodes(e);
    // Pre-fill (not lock) the prescriber for a logged-in Doctor with a linked practitioner record — still an
    // editable dropdown either way, since front-desk-prepares-for-doctor is a real workflow.
    const me = session.user;
    if (me?.practitionerId != null && session.roles.includes('Doctor')) {
      set({ prescribedByPractitionerId: me.practitionerId });
    }
  });

  // The gating below mirrors legacy Medinet's step sequence: Quantity/Repeats only for Limited scripts,
  // Authority/RB only when the medicine's RestrictionLevel (or Section 100, or an over-limit script) triggers
  // it, State Authority only for Schedule 8.
  const exceedsRepeats = (selected?.maxRepeats ?? 0) > 0 && form.repeats > selected!.maxRepeats!;
  const overQuantity = numericQuantity(form.quantity);
  const exceedsQuantity = (selected?.maxQuantity ?? 0) > 0 && overQuantity != null && overQuantity > selected!.maxQuantity!;
  // A null limit means "not stated", never "zero".
  const exceedsPbsLimits = selected != null && (exceedsRepeats || exceedsQuantity);
  const requiresAuthorityStep =
    selected?.restrictionLevel === PbsRestrictionLevel.AuthorityRequired
    || selected?.restrictionLevel === PbsRestrictionLevel.Restricted
    || selected?.isSection100 === true
    || exceedsPbsLimits;
  // An approval number is the remedy for everything except a plain Restricted Benefit, which is satisfied by
  // confirming the indication instead.
  const needsApprovalNumber =
    selected?.restrictionLevel === PbsRestrictionLevel.AuthorityRequired || selected?.isSection100 === true || exceedsPbsLimits;
  const authorityStepTitle = needsApprovalNumber ? 'Authority' : 'Restricted Benefit';

  const isApplicable = (s: WizardStep) => {
    switch (s) {
      case WizardStep.QuantityRepeats: return form.isLimitedDuration;
      case WizardStep.Authority: return requiresAuthorityStep;
      case WizardStep.StateAuthority: return selected?.schedule === MedicineSchedule.S8;
      default: return true;
    }
  };
  const visibleSteps = ALL_STEPS.filter(isApplicable);
  const currentIndex = visibleSteps.indexOf(step);

  const canSave =
    !!form.medicineName.trim()
    && form.prescribedByPractitionerId > 0
    && (safety == null || !safety.hasWarnings || (form.safetyOverrideAcknowledged && !!form.safetyOverrideNote?.trim()));

  const runSafetyCheck = async (medicineId: number | null, medicineName: string, genericName: string | null) => {
    set({ safetyOverrideAcknowledged: false, safetyOverrideNote: null });
    setSafety(await PrescriptionApi.checkSafety(clientId, { medicineId, medicineName, genericName }));
  };

  const selectMedicine = async (candidate: MedicineDto) => {
    setSelected(candidate);
    // Pre-fill from the PBS listing where it publishes a value. Both stay editable — the listing is the
    // starting point, not a cap; going over simply routes the prescriber through the Authority step.
    setForm((f) => ({
      ...f,
      medicineId: candidate.id,
      medicineName: candidate.name,
      quantity: candidate.packSize,
      route: f.route ?? candidate.route,
      repeats: (candidate.maxRepeats ?? 0) > 0 ? candidate.maxRepeats! : f.repeats,
    }));
    await runSafetyCheck(candidate.id, candidate.name, candidate.genericName);
  };

  const takeFreeText = async () => {
    setSelected(null);
    set({ medicineId: null, medicineName: term });
    await runSafetyCheck(null, term, null);
  };

  const changeMedicine = () => {
    setSelected(null);
    set({ medicineId: null, medicineName: '' });
    setTerm('');
    search.clear();
    setSafety(null);
  };

  const createEpisode = async () => {
    const title = newEpisodeTitle?.trim();
    if (!title) return;
    const id = await EpisodeApi.create({ ...defaultCreateEpisodeDto(), clientId, title });
    if (id != null) {
      setEpisodes(await EpisodeApi.list(clientId));
      set({ episodeId: id });
      setNewEpisodeTitle(null);
    }
  };

  // Per-step validation before advancing — mirrors legacy Medinet's per-dialog OK validation, with the
  // Schedule 8 state authority code treated as a hard gate.
  const validateStep = (): string | null => {
    switch (step) {
      case WizardStep.Medicine:
        return form.medicineName.trim() ? null : 'Select or enter a medicine to continue.';
      case WizardStep.QuantityRepeats:
        if (!form.quantity?.trim()) return 'Quantity is required for a limited-duration script.';
        if (form.durationDays == null || form.durationDays <= 0) return 'Duration (days) must be greater than 0 for a limited-duration script.';
        return null;
      case WizardStep.Authority:
        return selected?.restrictionLevel === PbsRestrictionLevel.Restricted && !rbAcknowledged
          ? 'Confirm the Restricted Benefit indication before continuing.' : null;
      case WizardStep.StateAuthority:
        return form.stateAuthorityCode?.trim() ? null : 'A state authority code is mandatory for Schedule 8 prescribing.';
      default:
        return null;
    }
  };

  const goNext = () => {
    const error = validateStep();
    setStepError(error);
    if (error) return;
    const idx = visibleSteps.indexOf(step);
    if (idx < visibleSteps.length - 1) setStep(visibleSteps[idx + 1]);
  };

  const goBack = () => {
    setStepError(null);
    const idx = visibleSteps.indexOf(step);
    if (idx > 0) setStep(visibleSteps[idx - 1]);
  };

  const save = async () => {
    setSaving(true);
    setSaveError(null);
    try {
      const id = await PrescriptionApi.create(form);
      if (id == null) {
        setSaveError('Save failed — check your permissions.');
      } else {
        setCreatedId(id);
        const created = (await PrescriptionApi.listForClient(clientId, true)).find((p) => p.id === id);
        setCreatedScriptNumber(created?.scriptNumber ?? null);
      }
    } finally {
      setSaving(false);
    }
  };

  const issueAndPrint = async () => {
    if (createdId == null) return;
    setIssuing(true);
    try {
      const issueId = await PrescriptionApi.issue(clientId, createdId, { ...defaultIssuePrescriptionDto(), issuedByPractitionerId: form.prescribedByPractitionerId });
      if (issueId != null) navigate(`/prescriptions/print/${issueId}`);
    } finally {
      setIssuing(false);
    }
  };

  const backToClient = `/clients/edit/${clientId}`;
  const errorLine = stepError && <div className="small text-danger mt-2" role="alert">{stepError}</div>;
  const nav = (first = false) => (
    <div className="d-flex gap-1">
      <button type="button" className="btn btn-primary" onClick={goNext}>Continue <i className="bi bi-arrow-right ms-1" /></button>
      {first
        ? <Link to={backToClient} className="btn btn-outline-secondary">Cancel</Link>
        : <button type="button" className="btn btn-outline-secondary" onClick={goBack}>Back</button>}
    </div>
  );
  const card = (title: string, body: ReactNode) => (
    <div className="card mb-3">
      <div className="card-header"><strong>{STEP_NUMBERS[step]}. {title}</strong></div>
      <div className="card-body">{body}{errorLine}</div>
    </div>
  );

  return (
    <div style={{ maxWidth: 1180 }}>
      <h4 className="mb-1">New Prescription</h4>
      <p className="text-muted small mb-4">{client ? `for ${client.firstName} ${client.lastName}` : ''}</p>

      {createdId == null ? (
        <>
          <ol className="imed-stepper" aria-label="Prescription steps">
            {visibleSteps.map((s, i) => (
              <li key={s} className={s === step ? 'is-current' : i < currentIndex ? 'is-done' : ''} aria-current={s === step ? 'step' : undefined}>
                <span className="imed-stepper-dot" aria-hidden="true">{i < currentIndex && <i className="bi bi-check-lg" />}</span>
                <span className="imed-stepper-label">{STEP_LABELS[s]}</span>
              </li>
            ))}
          </ol>

          {step === WizardStep.Medicine && (
            <>
              {card('Medicine', !form.medicineName ? (
                <>
                  <input className="form-control form-control-sm mb-2" placeholder="Search by name..." autoFocus value={term}
                         onChange={(e) => { setTerm(e.target.value); void search.run(e.target.value); }} />
                  <div className="imed-medicine-results-wrap">
                    {!term.trim() ? (
                      <div className="text-muted small">Start typing a medicine name to search the catalog.</div>
                    ) : search.searching ? (
                      <LoadingState text="Searching..." />
                    ) : search.results.length === 0 ? (
                      <div className="text-muted small">No matches.</div>
                    ) : (
                      // Medinet's search grid: one row per catalogue listing, each attribute in its own column
                      <table className="table table-sm table-hover align-middle mb-0 imed-medicine-results">
                        <colgroup>
                          <col style={{ width: '19%' }} /><col style={{ width: '13%' }} /><col style={{ width: '26%' }} />
                          <col style={{ width: '12%' }} /><col style={{ width: '5%' }} /><col style={{ width: '5%' }} />
                          <col style={{ width: '13%' }} /><col style={{ width: '7%' }} />
                        </colgroup>
                        <thead>
                          <tr>
                            <th>Drug name</th>
                            <th>Generic</th>
                            <th>Form</th>
                            <th>Strength</th>
                            <th className="text-end">Qty</th>
                            <th className="text-end">Rpts</th>
                            <th>Subsidy</th>
                            <th>Schedule</th>
                          </tr>
                        </thead>
                        <tbody>
                          {search.results.map((m) => (
                            <tr key={m.id} className="imed-row-clickable" tabIndex={0} onClick={() => void selectMedicine(m)}
                                onKeyDown={(e) => { if (e.key === 'Enter') void selectMedicine(m); }}>
                              <td>
                                <strong>{m.name}</strong>
                                {m.manufacturer?.trim() && <div className="small text-muted">{m.manufacturer}</div>}
                              </td>
                              <td>{m.genericName}</td>
                              <td>{m.form}</td>
                              <td className="text-nowrap">{m.strength}</td>
                              <td className="text-end">{m.packSize}</td>
                              <td className="text-end">{m.maxRepeats ?? ''}</td>
                              <td>
                                <span className="d-inline-flex flex-wrap gap-1">
                                  {m.isPbsListed && <span className="badge bg-info text-dark">PBS</span>}
                                  {m.isRpbsListed && <span className="badge bg-info text-dark">RPBS</span>}
                                  {m.restrictionLevel != null && (
                                    <span className={`badge ${restrictionBadgeClass(m.restrictionLevel)}`}>{restrictionLabel(m.restrictionLevel)}</span>
                                  )}
                                  {m.isSection100 && <span className="badge bg-warning text-dark">S100</span>}
                                </span>
                                {m.pbsItemCode && <div className="small text-muted">{m.pbsItemCode}</div>}
                              </td>
                              <td>{m.schedule != null && <span className={`badge ${scheduleBadgeClass(m.schedule)}`}>{MedicineSchedule[m.schedule]}</span>}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )}
                  </div>
                  {term.trim() && (
                    <button type="button" className="btn btn-sm btn-link px-0 mt-2" onClick={() => void takeFreeText()}>
                      Can't find it? Use "{term}" as free text
                    </button>
                  )}
                </>
              ) : (
                <div className="d-flex justify-content-between align-items-start">
                  <div>
                    <div>
                      <strong>{form.medicineName}</strong>
                      {form.medicineId == null && <span className="badge bg-secondary ms-1">free text</span>}
                      {selected && <MedicineBadges medicine={selected} spaced />}
                    </div>
                    {selected && <div className="small text-muted">{describe(selected, false)}</div>}
                  </div>
                  <button type="button" className="btn btn-sm btn-outline-secondary" onClick={changeMedicine}>Change</button>
                </div>
              ))}
              {nav(true)}
            </>
          )}

          {step === WizardStep.Dosage && (
            <>
              {card('Dosage', (
                <div className="row g-2">
                  <div className="col-md-3">
                    <label className="form-label small mb-0">Dose</label>
                    <TextInput className="form-control form-control-sm" value={form.dose} onChange={(v) => set({ dose: v })} placeholder="e.g. 500mg" />
                  </div>
                  <div className="col-md-3">
                    <label className="form-label small mb-0" htmlFor="rxFrequency">Frequency</label>
                    <ChoiceWithOther id="rxFrequency" value={form.frequency} onChange={(v) => set({ frequency: v })} options={FREQUENCIES}
                                     noneLabel="Nil" otherLabel="Custom…" otherPlaceholder="e.g. 1-2 tablets 4 hourly, max 8 a day" />
                  </div>
                  <div className="col-md-3">
                    <label className="form-label small mb-0" htmlFor="rxRoute">Route</label>
                    <SuggestInput id="rxRoute" className="form-control form-control-sm" value={form.route} onChange={(v) => set({ route: v })}
                                  options={ROUTES} placeholder="Select or type..." />
                  </div>
                  <div className="col-md-3">
                    <label className="form-label small mb-0">
                      Purpose{' '}
                      <i className="bi bi-info-circle text-muted" title="How this script came about (legacy Medinet's source code). Recorded on the clinical record only — it doesn't change the workflow." />
                    </label>
                    <NumberSelect enumType={PrescriptionSourceCode} className="form-select form-select-sm" value={form.purpose} onChange={(v) => set({ purpose: v as PrescriptionSourceCode | null })}>
                      <option value="">Select...</option>
                      <EnumOptions enumType={PrescriptionSourceCode} labels={PURPOSE_LABELS} />
                    </NumberSelect>
                  </div>
                  <div className="col-12">
                    <label className="form-label small mb-0" htmlFor="rxInstruction">Instruction (for patient)</label>
                    <ChoiceWithOther id="rxInstruction" value={form.instruction} onChange={(v) => set({ instruction: v })} options={INSTRUCTIONS}
                                     noneLabel="None" otherPlaceholder="e.g. Take with food" />
                  </div>
                  <div className="col-md-4">
                    <label className="form-label small mb-0">Start date</label>
                    <DateInput className="form-control form-control-sm" value={form.startDate} onChange={(v) => set({ startDate: v })} />
                  </div>
                  <div className="col-md-8">
                    <label className="form-label small mb-0 d-block">Duration</label>
                    <div className="btn-group btn-group-sm" role="group">
                      <input type="radio" className="btn-check" id="durLong" checked={!form.isLimitedDuration} onChange={() => set({ isLimitedDuration: false })} />
                      <label className="btn btn-outline-secondary" htmlFor="durLong">Long term</label>
                      <input type="radio" className="btn-check" id="durLimited" checked={form.isLimitedDuration} onChange={() => set({ isLimitedDuration: true })} />
                      <label className="btn btn-outline-secondary" htmlFor="durLimited">Limited</label>
                    </div>
                  </div>
                </div>
              ))}
              {nav()}
            </>
          )}

          {step === WizardStep.QuantityRepeats && (
            <>
              {card('Quantity & Repeats', (
                <div className="row g-2">
                  <div className="col-md-3">
                    <label className="form-label small mb-0">Quantity</label>
                    <TextInput className="form-control form-control-sm" value={form.quantity} onChange={(v) => set({ quantity: v })} placeholder="e.g. 20" />
                  </div>
                  <div className="col-md-3">
                    <label className="form-label small mb-0">Duration (days)</label>
                    <NumberInput className="form-control form-control-sm" nullable value={form.durationDays} onChange={(v) => set({ durationDays: v })} />
                  </div>
                  <div className="col-md-3">
                    <label className="form-label small mb-0">Repeats</label>
                    <NumberInput className="form-control form-control-sm" value={form.repeats} onChange={(v) => set({ repeats: v ?? 0 })} />
                  </div>
                  <div className="col-md-3">
                    <label className="form-label small mb-0">Repeat interval (days)</label>
                    <NumberInput className="form-control form-control-sm" nullable value={form.repeatIntervalDays} onChange={(v) => set({ repeatIntervalDays: v })} />
                  </div>
                </div>
              ))}
              {nav()}
            </>
          )}

          {step === WizardStep.Authority && (
            <>
              {card(authorityStepTitle, (
                <>
                  {/* This step can be reached four ways, so say which one applies — otherwise a prescriber who
                      exceeded a quantity limit sees an unexplained Authority screen. */}
                  {selected?.isSection100 && (
                    <div className="alert alert-warning small py-2 mb-2">
                      <i className="bi bi-info-circle me-1" />
                      <strong>Section 100</strong> item — supplied through the Highly Specialised Drugs or equivalent
                      programme, and Authority-only regardless of quantity.
                    </div>
                  )}
                  {exceedsPbsLimits && (
                    <div className="alert alert-warning small py-2 mb-2">
                      <i className="bi bi-exclamation-triangle me-1" />
                      This prescription exceeds the PBS listing:{' '}
                      {exceedsRepeats && <span>{form.repeats} repeats requested, listing allows {selected!.maxRepeats}. </span>}
                      {exceedsQuantity && <span>Quantity {overQuantity} requested, listing allows {selected!.maxQuantity}. </span>}
                      Increased quantities need Authority approval.
                    </div>
                  )}
                  {/* Each block keys off its own trigger — a Restricted item prescribed over its quantity limit
                      legitimately needs both an indication confirmation and an approval number. */}
                  {selected?.restrictionLevel === PbsRestrictionLevel.AuthorityRequired && (
                    <div className="alert alert-warning small py-2 mb-2">
                      <i className="bi bi-telephone me-1" />
                      Authority Required — phone the Authority Hotline before issuing, or record a prior approval number below.
                    </div>
                  )}
                  {needsApprovalNumber && (
                    <div className="row g-2 mb-2">
                      <div className="col-md-4">
                        <label className="form-label small mb-0">Authority approval number</label>
                        <TextInput className="form-control form-control-sm" value={form.authorityApprovalNumber}
                                   onChange={(v) => set({ authorityApprovalNumber: v })} placeholder="Not validated live — record only" />
                      </div>
                    </div>
                  )}
                  {selected?.restrictionLevel === PbsRestrictionLevel.Restricted && (
                    <>
                      <div className="alert alert-secondary small py-2 mb-2">
                        <i className="bi bi-info-circle me-1" />
                        Restricted Benefit — subsidised only for the listed indication. No separate approval number is required.
                      </div>
                      <div className="form-check">
                        <input className="form-check-input" type="checkbox" id="chkRb" checked={rbAcknowledged} onChange={(e) => setRbAcknowledged(e.target.checked)} />
                        <label className="form-check-label small" htmlFor="chkRb">Indication confirmed — this prescription meets the Restricted Benefit criteria</label>
                      </div>
                    </>
                  )}
                </>
              ))}
              {nav()}
            </>
          )}

          {step === WizardStep.StateAuthority && (
            <>
              {card('State Authority (Schedule 8)', (
                <>
                  <div className="alert alert-warning small py-2 mb-2">
                    <i className="bi bi-exclamation-triangle me-1" />
                    Schedule 8 (drug of dependence) — this may not satisfy your state's S8 prescribing requirements
                    (sequentially-numbered forms or accredited e-prescribing software may be required). Verify before
                    use. A state authority code is required to continue.
                  </div>
                  <div className="row g-2">
                    <div className="col-md-4">
                      <label className="form-label small mb-0">State authority code</label>
                      <TextInput className="form-control form-control-sm" value={form.stateAuthorityCode} onChange={(v) => set({ stateAuthorityCode: v })} />
                    </div>
                  </div>
                </>
              ))}
              {nav()}
            </>
          )}

          {step === WizardStep.Reason && (
            <>
              {card('Reason', (
                <>
                  <div className="mb-2">
                    <label className="form-label small mb-0">Reason for prescribing</label>
                    <TextArea className="form-control form-control-sm" rows={2} value={form.reason} onChange={(v) => set({ reason: v })} placeholder="e.g. Acute sinusitis" />
                  </div>
                  <div className="row g-2">
                    <div className="col-md-6">
                      <label className="form-label small mb-0">Episode</label>
                      {newEpisodeTitle != null ? (
                        <div className="input-group input-group-sm">
                          <input className="form-control" placeholder="New episode title" autoFocus value={newEpisodeTitle}
                                 onChange={(e) => setNewEpisodeTitle(e.target.value)}
                                 onKeyDown={(e) => { if (e.key === 'Enter') void createEpisode(); }} />
                          <button type="button" className="btn btn-outline-primary" onClick={() => void createEpisode()}>Add</button>
                          <button type="button" className="btn btn-outline-secondary" onClick={() => setNewEpisodeTitle(null)}>Cancel</button>
                        </div>
                      ) : (
                        <div className="d-flex">
                          <NumberSelect className="form-select form-select-sm" value={form.episodeId} onChange={(v) => set({ episodeId: v })}>
                            <option value="">-- None --</option>
                            {episodes.map((ep) => <option key={ep.id} value={ep.id}>{ep.title}</option>)}
                          </NumberSelect>
                          <button type="button" className="btn btn-sm btn-link text-nowrap" onClick={() => setNewEpisodeTitle('')}>+ New</button>
                        </div>
                      )}
                    </div>
                  </div>
                  <div className="row g-2 mt-1">
                    <div className="col-md-6">
                      <label className="form-label small mb-0">Prescribing notes</label>
                      <TextArea className="form-control form-control-sm" rows={2} value={form.prescribingNotes} onChange={(v) => set({ prescribingNotes: v })} />
                    </div>
                    <div className="col-md-6">
                      <label className="form-label small mb-0">Notes for pharmacy</label>
                      <TextArea className="form-control form-control-sm" rows={2} value={form.notesForPharmacy} onChange={(v) => set({ notesForPharmacy: v })} />
                    </div>
                  </div>
                </>
              ))}
              {nav()}
            </>
          )}

          {step === WizardStep.Review && (
            <>
              <div className="card mb-3">
                <div className="card-header"><strong>{STEP_NUMBERS[WizardStep.Review]}. Review &amp; Issue</strong></div>
                <div className="card-body">
                  <dl className="row small mb-0">
                    <dt className="col-sm-3">Medicine</dt><dd className="col-sm-9">{form.medicineName}</dd>
                    <dt className="col-sm-3">Dose</dt><dd className="col-sm-9">{[form.dose, form.frequency, form.route].filter(Boolean).join(' ')}</dd>
                    {form.instruction?.trim() && <><dt className="col-sm-3">Instruction</dt><dd className="col-sm-9">{form.instruction}</dd></>}
                    <dt className="col-sm-3">Duration</dt>
                    <dd className="col-sm-9">{form.isLimitedDuration ? `Limited — ${form.quantity ?? ''} qty, ${form.repeats} repeats` : 'Long term'}</dd>
                    {form.authorityApprovalNumber?.trim() && <><dt className="col-sm-3">Authority #</dt><dd className="col-sm-9">{form.authorityApprovalNumber}</dd></>}
                    {form.stateAuthorityCode?.trim() && <><dt className="col-sm-3">State authority</dt><dd className="col-sm-9">{form.stateAuthorityCode}</dd></>}
                    {form.reason?.trim() && <><dt className="col-sm-3">Reason</dt><dd className="col-sm-9">{form.reason}</dd></>}
                  </dl>
                </div>
              </div>
              <div className="card mb-3">
                <div className="card-header"><strong>Prescriber</strong></div>
                <div className="card-body">
                  <NumberSelect className="form-select form-select-sm" style={{ maxWidth: 320 }} nullable={false}
                                value={form.prescribedByPractitionerId} onChange={(v) => set({ prescribedByPractitionerId: v ?? 0 })}>
                    <option value="0">Select...</option>
                    {practitioners.map((p) => <option key={p.id} value={p.id}>{[p.title, p.firstName, p.lastName].filter(Boolean).join(' ')}</option>)}
                  </NumberSelect>
                </div>
              </div>
              {safety && (
                <div className="card mb-3">
                  <div className="card-header"><strong>Safety Check</strong></div>
                  <div className="card-body">
                    {!safety.canFullyCheck && (
                      <div className="alert alert-secondary small py-2 mb-2">
                        <i className="bi bi-info-circle me-1" />
                        This is a free-text/compounded item — it can't be automatically checked against interactions or allergies. Verify manually.
                      </div>
                    )}
                    {safety.interactionDatabaseEmpty && (
                      <div className="alert alert-secondary small py-2 mb-2">
                        <i className="bi bi-info-circle me-1" />
                        The interaction reference database has no data loaded — this check found nothing to compare against, not confirmed-safe.
                      </div>
                    )}
                    {selected?.schedulingUnknown && (
                      <div className="alert alert-secondary small py-2 mb-2">
                        <i className="bi bi-info-circle me-1" />
                        Poisons scheduling is not recorded for this medicine — the PBS feed doesn't publish it. If this is
                        a Schedule 8 drug, the extra S8 requirements still apply and this wizard has not prompted for them.
                        Verify before issuing.
                      </div>
                    )}
                    {!safety.hasWarnings ? (
                      <div className="text-success small"><i className="bi bi-check-circle me-1" />No known interactions, duplicate therapy or allergy conflicts found.</div>
                    ) : (
                      <>
                        {safety.interactionWarnings.map((w, i) => (
                          <div key={`i${i}`} className={`alert alert-${w.severity === InteractionSeverity.Major ? 'danger' : 'warning'} small py-2 mb-1`}>
                            <strong>Interaction ({InteractionSeverity[w.severity]}):</strong> with {w.conflictingMedicineName}{' '}
                            ({w.ingredientAName} / {w.ingredientBName}). {w.description}
                          </div>
                        ))}
                        {safety.duplicateTherapyWarnings.map((w, i) => (
                          <div key={`d${i}`} className="alert alert-warning small py-2 mb-1">
                            <strong>Duplicate therapy:</strong> already prescribed {w.existingMedicineName}
                            {w.therapeuticGroupTitle?.trim() ? `, same therapeutic group (${w.therapeuticGroupTitle})` : ''}.
                          </div>
                        ))}
                        {safety.allergyWarnings.map((w, i) => (
                          <div key={`a${i}`} className="alert alert-danger small py-2 mb-1">
                            <strong>Allergy{w.isConfirmedMatch ? '' : ' (possible — please verify)'}:</strong>{' '}
                            recorded allergy to {w.allergySubstance}{w.severity != null ? ` (${AllergySeverity[w.severity]})` : ''}
                          </div>
                        ))}
                        <div className="form-check mt-2">
                          <input className="form-check-input" type="checkbox" id="chkOverride" checked={form.safetyOverrideAcknowledged}
                                 onChange={(e) => set({ safetyOverrideAcknowledged: e.target.checked })} />
                          <label className="form-check-label small" htmlFor="chkOverride">I've reviewed these warnings and want to proceed anyway</label>
                        </div>
                        {form.safetyOverrideAcknowledged && (
                          <TextArea className="form-control form-control-sm mt-1" rows={2} placeholder="Why is it safe to proceed? (required)"
                                    value={form.safetyOverrideNote} onChange={(v) => set({ safetyOverrideNote: v })} />
                        )}
                      </>
                    )}
                  </div>
                </div>
              )}
              {saveError && <div className="small text-danger mb-2" role="alert">{saveError}</div>}
              <div className="d-flex gap-1">
                <button type="button" className="btn btn-primary" disabled={!canSave || saving} onClick={() => void save()}>
                  {saving && <span className="spinner-border spinner-border-sm me-1" />}
                  Create Prescription
                </button>
                <button type="button" className="btn btn-outline-secondary" onClick={goBack}>Back</button>
                <Link to={backToClient} className="btn btn-outline-secondary">Cancel</Link>
              </div>
            </>
          )}
        </>
      ) : (
        <>
          <div className="alert alert-success">
            <i className="bi bi-check-circle me-1" /> Prescription created — script number <strong>{createdScriptNumber}</strong>.
          </div>
          <div className="d-flex gap-1">
            <button type="button" className="btn btn-primary" disabled={issuing} onClick={() => void issueAndPrint()}>
              {issuing && <span className="spinner-border spinner-border-sm me-1" />}
              <i className="bi bi-printer me-1" /> Issue &amp; Print Now
            </button>
            <Link to={backToClient} className="btn btn-outline-secondary">Done — issue later</Link>
          </div>
        </>
      )}
    </div>
  );
}
