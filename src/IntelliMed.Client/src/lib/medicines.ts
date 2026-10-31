import { MedicineSchedule, PbsRestrictionLevel } from '@/api/types.gen';

export function scheduleBadgeClass(schedule: MedicineSchedule): string {
  switch (schedule) {
    case MedicineSchedule.S8: return 'bg-danger';
    case MedicineSchedule.S4: return 'bg-primary';
    case MedicineSchedule.S3: return 'bg-warning text-dark';
    case MedicineSchedule.S2: return 'bg-success';
    default: return 'bg-secondary';
  }
}

// Same labelling/colouring convention as the Medicines admin catalogue, so a drug looks identical whether seen
// in search results or in the admin list.
export const restrictionLabel = (level: PbsRestrictionLevel) =>
  level === PbsRestrictionLevel.AuthorityRequired ? 'Authority Required' : PbsRestrictionLevel[level];

export function restrictionBadgeClass(level: PbsRestrictionLevel): string {
  switch (level) {
    case PbsRestrictionLevel.AuthorityRequired: return 'bg-danger';
    case PbsRestrictionLevel.Restricted: return 'bg-warning text-dark';
    default: return 'bg-secondary';
  }
}

/**
 * Dosage frequency choices — legacy Medinet's DoseCtrl defaults (lstFrequency): the key is what's stored and
 * printed on the script, the label is what the prescriber reads. Medinet itself has no free-text frequency;
 * the "Custom…" choice in the wizard is an IntelliMed addition.
 */
export const FREQUENCIES: { value: string; label: string }[] = [
  { value: 'stat', label: 'Stat' },
  { value: 'daily', label: 'Daily' },
  { value: 'mane', label: 'In the morning' },
  { value: 'midday', label: 'Midday' },
  { value: 'nocte', label: 'At night' },
  { value: 'b.d.', label: 'Twice a day' },
  { value: 't.i.d.', label: '3 times a day' },
  { value: 'q.i.d.', label: '4 times a day' },
  { value: 'q.2.h.', label: 'Two hourly' },
  { value: 'q.4.h.', label: 'Four hourly' },
  { value: 'q.6.h.', label: 'Six hourly' },
  { value: 'q.8.h.', label: 'Eight hourly' },
  { value: 'weekly', label: 'Weekly' },
];

/** Patient instruction choices — Medinet's DoseCtrl defaults (lstInstruction); its "Other" opens free text. */
export const INSTRUCTIONS: { value: string; label: string }[] = [
  { value: 'p.r.n.', label: 'If required' },
  { value: 'm.d.u.', label: 'As directed' },
  { value: 'a.c.', label: 'Before meals' },
  { value: 'c.c.', label: 'With meals' },
  { value: 'p.c.', label: 'After meals' },
  { value: 'alt.d.', label: 'Every second day' },
  { value: 'left side', label: 'Left side' },
  { value: 'right side', label: 'Right side' },
  { value: 'both sides', label: 'To both sides' },
];

/**
 * Route-of-administration choices. Medinet's list came from MIMS content (CN_ROUTE), which IntelliMed doesn't
 * licence, so these are the usual Australian routes instead; the PBS listing's route still pre-fills, and the
 * field accepts anything typed.
 */
export const ROUTES: { value: string; label: string }[] = [
  { value: 'PO', label: 'oral' },
  { value: 'SL', label: 'sublingual' },
  { value: 'Buccal', label: 'inside the cheek' },
  { value: 'Topical', label: 'applied to skin' },
  { value: 'Transdermal', label: 'patch' },
  { value: 'Inhaled', label: 'inhaler' },
  { value: 'Nebulised', label: 'nebuliser' },
  { value: 'Intranasal', label: 'nose' },
  { value: 'Eye', label: 'ocular' },
  { value: 'Ear', label: 'otic' },
  { value: 'PR', label: 'rectal' },
  { value: 'PV', label: 'vaginal' },
  { value: 'IM', label: 'intramuscular' },
  { value: 'IV', label: 'intravenous' },
  { value: 'SC', label: 'subcutaneous' },
  { value: 'ID', label: 'intradermal' },
];
