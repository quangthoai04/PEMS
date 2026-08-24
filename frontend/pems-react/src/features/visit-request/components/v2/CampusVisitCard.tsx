import React, { useMemo, useRef, useState } from 'react';
import { Controller, useFieldArray, type FieldPath, type UseFormReturn } from 'react-hook-form';
import { AlertCircle, ChevronDown, Copy, FileSpreadsheet, Plus, Replace, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import {
  buildCampusVisitSchema,
  V2_MAX_MEMBERS_PER_CAMPUS,
  V2_MIN_ADVANCE_HOURS_CREATE,
} from '../../schema/visitRequestV2.schema';
import type { RegistrationCampusOption } from '../../api/visitRequestApi';
import { FormField, inputCls } from '../shared/FormField';
import { AutoGrowTextarea } from '../shared/AutoGrowTextarea';
import { AutoGrowTextField } from '../shared/AutoGrowTextField';
import { PhoneField } from '../shared/PhoneField';
import { VisitDateTimeRangePicker } from '../shared/VisitDateTimeRangePicker';
import { CountrySelect } from '../shared/CountrySelect';
import { OrganizationCombobox } from '../shared/OrganizationCombobox';
import { showMessageErrorToast, showSuccessToast } from '../../../../shared/utils/toast';
import { countFieldErrors } from '../../utils/formErrorNavigation';
import { validatePersonExcel, canApplyImport, type ExcelTranslator, type PersonRow } from '../ExcelUpload/excelValidator';
import { ExcelImportPanel, type ExcelImportState } from '../ExcelUpload/ExcelImportPanel';
import { downloadVisitorTemplate, downloadSupportTeamTemplate } from '../ExcelUpload/excelDownload';
import { CampusHostSelectionV2Panel } from './CampusHostSelectionV2Panel';
import type { CreatorRole } from '../../schema/visitRequestV2.schema';
import type { CampusHostSelectionChoice } from '../../api/visitRequestApi';
import { HelpTooltip } from '../shared/HelpTooltip';
import { fieldChangeHandler } from '../../../../shared/utils/formRevalidate';
import { createEmptyMember } from '../../utils/visitRequestV2Form';
import {
  campusMemberRows,
  findCampusMemberDuplicates,
  findMemberDuplicates,
  type MemberDuplicatePair,
} from '../../utils/memberDuplicates';

const VISIT_TYPES = ['CAMPUS_TOUR', 'MEETING', 'WORKSHOP', 'SIGNING_CEREMONY', 'EXCHANGE', 'OTHER'] as const;

/**
 * What a quick-fill copies into the per-campus operational contact. This is a ONE-TIME copy, never
 * a link: the registrant and the campus contact are frequently the same person on the day the form
 * is filled and different people a week later, so keeping them in sync would silently undo an
 * edit somebody made deliberately (plan §12).
 */
const QUICK_FILL_FIELDS = ['fullName', 'organization', 'jobTitle', 'phone', 'email'] as const;
/** The only block left to copy a campus contact from: there is no request-level contact any more. */
type QuickFillSource = 'registrant';

/**
 * Per-field ceilings, mirroring `buildCampusVisitSchema` and the FluentValidation rules that
 * back it. Declared here only so the counter and the schema cannot drift apart on screen.
 */
const MAX = {
  delegationName: 200,
  visitTypeOther: 200,
  purpose: 2000,
  workingContent: 4000,
  transportationNote: 2000,
  notes: 2000,
  personName: 150,
  personJobTitle: 150,
  personOrganization: 200,
  contactName: 150,
  contactOrganization: 200,
} as const;

interface Props {
  form: UseFormReturn<VisitRequestV2Schema>;
  index: number;
  /** Stable identity of this card — the React key; NEVER the array index. */
  clientKey: string;
  open: boolean;
  onToggle: () => void;
  campuses: RegistrationCampusOption[];
  campusesLoading: boolean;
  /** Campus CODEs already chosen on ANY card — filtered out here so a campus cannot be picked twice. */
  takenCampusCodes?: string[];
  /** Labels of the OTHER cards offered as one-time copy sources (empty → no copy UI). */
  copySources: Array<{ index: number; label: string }>;
  onCopyFrom: (sourceIndex: number) => void;
  onApplyToAll: () => void;
  onRemove: () => void;
  canRemove: boolean;
  showErrors?: boolean;
  /** Minimum notice a schedule may be set with — the same source as the Zod schema. */
  minAdvanceHours?: number;
  /**
   * Renders the operational contact as a read-only summary instead of a set of inputs.
   *
   * <p>Set for a campus that ALREADY EXISTS on the request being edited. That campus's contact is not
   * this form's to write — the backend refuses every one of the five fields through the edit path
   * (IMMUTABLE_CONTACT_IDENTITY for the address, IMMUTABLE_CONTACT_PROFILE for the rest) — and it is
   * managed on the detail screen, where changing the address can ask the new person to accept.</p>
   *
   * <p>A summary rather than nothing at all, because who coordinates a campus is worth seeing while
   * editing it. A summary rather than disabled inputs, because a greyed-out field still looks like a
   * field somebody could enable.</p>
   *
   * <p>Left false for a campus being ADDED here: it has no contact yet, nobody has been invited, and
   * naming one is part of adding the campus.</p>
   */
  contactReadOnly?: boolean;
  /**
   * Authenticated create only: who will process THIS campus. Omitted entirely for the public
   * form, which never renders internal processing controls and never sends a processing intent.
   */
  processing?: {
    role: CreatorRole;
    ownCampusCode?: string | null;
    /** All choices keyed by campus CODE; this card reads its own by the campus it currently watches. */
    values: Record<string, CampusHostSelectionChoice>;
    onChange: (next: CampusHostSelectionChoice) => void;
  };
}

/**
 * ONE campus visit card (plan §9.1): a complete, independent snapshot — schedule, content,
 * people, operational contact and requirements. Collapsing only HIDES the body (CSS), the
 * fields stay mounted so React Hook Form never unregisters and no typed data is lost.
 */
export const CampusVisitCard: React.FC<Props> = ({
  form,
  index,
  clientKey,
  open,
  onToggle,
  campuses,
  campusesLoading,
  takenCampusCodes = [],
  copySources,
  onCopyFrom,
  onApplyToAll,
  onRemove,
  canRemove,
  showErrors,
  minAdvanceHours = V2_MIN_ADVANCE_HOURS_CREATE,
  contactReadOnly = false,
  processing,
}) => {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);
  const { register, control, watch, formState: { errors } } = form;
  const base = `campusVisits.${index}` as const;

  /**
   * The onChange every `Controller`-wrapped control on this card uses (NP-02).
   *
   * <p>These controls are all custom (AutoGrowTextField/Textarea, the country and organization
   * comboboxes, the date-range picker, the campus select), so they only ever call
   * `field.onChange` — none of RHF's own input plumbing runs for them. Before the first submit the
   * form does not revalidate on change, so any error already sitting on such a field (put there by
   * the profile autofill, a mapped server error, or a `setError`) had nothing to clear it: the user
   * typed a perfectly good value and the red text stayed. `fieldChangeHandler` retriggers the field
   * — and ONLY when it is currently in error, so an untouched long form still stays quiet.</p>
   */
  const revalidatingChange = (field: {
    name: FieldPath<VisitRequestV2Schema>;
    onChange: (value: unknown) => void;
  }) => fieldChangeHandler(form, field.name, field.onChange);
  const cardErrors = errors.campusVisits?.[index];
  const errorCount = countFieldErrors(cardErrors);

  const visitorFields = useFieldArray({ control, name: `campusVisits.${index}.visitors` });
  const supportFields = useFieldArray({ control, name: `campusVisits.${index}.supportTeam` });

  // ONE state per section: guests and support staff are imported separately, and a shared
  // message could not say which list it described (nor survive a second import).
  const [excelState, setExcelState] = useState<Record<'visitors' | 'supportTeam', ExcelImportState>>({
    visitors: {}, supportTeam: {},
  });
  /**
   * A replace waiting to be confirmed, PER SECTION (IMP-01/IMP-02).
   *
   * <p>This used to be one slot with a `kind` tag on it, and one confirmation block rendered after
   * BOTH tables. Importing a guest list therefore put "Thay thế toàn bộ danh sách?" underneath the
   * support staff — asking about one list while pointing at the other — and importing a second file
   * silently overwrote the first pending answer, so one of the two questions disappeared unanswered.
   * Two slots, each rendered inside the section it belongs to, is what makes the question and its
   * subject the same thing on screen.</p>
   */
  const [pendingReplace, setPendingReplace] = useState<
    Record<'visitors' | 'supportTeam', { rows: PersonRow[]; fileName: string } | null>
  >({ visitors: null, supportTeam: null });
  /** Where the confirmation lives, so a freshly raised one is brought into view (IMP-01). */
  const replaceConfirmRefs = {
    visitors: useRef<HTMLDivElement>(null),
    supportTeam: useRef<HTMLDivElement>(null),
  };
  /**
   * Said once, after a replace that dropped the person holding the contact role. Never re-pointed at
   * somebody else automatically: a name matching in the new file does not make it the same person,
   * and choosing the campus's coordinator is the user's decision (IMP-03).
   */
  const [contactLostByReplace, setContactLostByReplace] = useState(false);
  /** A Replace Both that would build a list containing one person twice — nothing was applied. */
  const [replaceBothBlocked, setReplaceBothBlocked] = useState<string | null>(null);
  const [selectedSource, setSelectedSource] = useState<string>('');
  /**
   * Raised the FIRST time somebody edits the member who is this campus's contact, on a campus that
   * already exists (NP-03 §10). One prompt per card, not one per keystroke: the point is to be told
   * once what the relationship is, not to be interrogated while typing.
   */
  const [contactMemberEdit, setContactMemberEdit] = useState<
    { kind: 'visitors' | 'supportTeam'; rowIndex: number; field: string; previous: string } | null
  >(null);
  const contactEditAcknowledgedRef = useRef(false);
  const visitorFileRef = useRef<HTMLInputElement>(null);
  const supportFileRef = useRef<HTMLInputElement>(null);

  const campusCode = watch(`${base}.campus`);
  const visitType = watch(`${base}.visitType`);
  const campusName = campuses.find(c => c.campusCode === campusCode)?.campusName;
  const headerLabel = campusName ?? t('visitRequestV2:card.unselectedCampus');

  /**
   * Whether the header may claim this card is "Đã đủ thông tin" — answered by the SAME schema that
   * validates the submit, never by a hand-picked list of fields.
   *
   * <p>The badge used to read `campus && visitType && delegationName`, three of the dozen fields a
   * card actually requires — and `visitType` is seeded with `CAMPUS_TOUR`, so it was never falsy.
   * Picking a campus and typing one letter into "Tên đoàn" therefore turned the card green while the
   * schedule, purpose, working content, delegation list and campus contact were all still empty. The
   * red branch could not catch it either: the form runs `mode: 'onSubmit'`, so before the first
   * submit `errors` is empty by construction and the count is always 0.</p>
   *
   * <p>Not memoised on the values: `watch` hands back the same object mutated in place, so a
   * `useMemo` keyed on it would freeze at its mount value and the badge would never move again. Only
   * the schema — which depends on nothing but `minAdvanceHours` — is built once. The translator is a
   * stub because this asks pass/fail; the wording of a failure belongs to the resolver, not here.</p>
   */
  const completenessSchema = useMemo(
    () => buildCampusVisitSchema(minAdvanceHours, () => ''),
    [minAdvanceHours],
  );
  const cardComplete = completenessSchema.safeParse(watch(base)).success;

  const excelT: ExcelTranslator = (key, options) => t(key, options);

  const setSection = (kind: 'visitors' | 'supportTeam', next: ExcelImportState) =>
    setExcelState(prev => ({ ...prev, [kind]: next }));

  /** The rows CURRENTLY in a section — the baseline for duplicate detection and the 200 cap. */
  const currentRows = (kind: 'visitors' | 'supportTeam'): PersonRow[] =>
    ((watch(`${base}.${kind}`) ?? []) as PersonRow[]).map(r => ({
      fullName: r?.fullName ?? '', jobTitle: r?.jobTitle ?? '',
      organization: r?.organization ?? '', nationality: r?.nationality ?? '',
    }));

  /**
   * A row the user has not filled in at all. An untouched blank row is scaffolding, not data:
   * it must not consume one of the 200 slots, and appending after it would leave a gap.
   */
  const isBlank = (r: PersonRow) =>
    !r.fullName.trim() && !r.jobTitle.trim() && !r.organization.trim() && !r.nationality.trim();

  /**
   * Imports into THIS campus card only, and only ever ADDS (plan §7): replacing was destroying
   * rows typed by hand with no warning. Replacing is still possible — as its own action, below,
   * behind a confirmation.
   */
  const importExcel = async (kind: 'visitors' | 'supportTeam', file: File) => {
    setSection(kind, { loadingFileName: file.name });
    // A file for THIS section can never invalidate a replace the user is still deciding on for the
    // OTHER one, so only this section's pending answer is dropped (IMP-02).
    setPendingReplace(prev => ({ ...prev, [kind]: null }));
    setReplaceBothBlocked(null);
    const existing = currentRows(kind).filter(r => !isBlank(r));

    // validatePersonExcel never throws: an unreadable workbook comes back as a report with a
    // fatalMessage, so the panel can explain it instead of the promise rejecting silently.
    const report = await validatePersonExcel(file, kind, existing, excelT);
    if (!canApplyImport(report)) {
      // Refused: nothing was applied, so the panel must not claim an action. `applied` stays unset.
      setSection(kind, { report });
      return;
    }

    // An imported spreadsheet carries names, never ids — every row lands as free text, and the
    // matcher offers candidates later. Filling an id in from a name match here would be the exact
    // guess-as-decision this whole change removes (PART-06).
    const rows = report.data.map(r => ({
      ...createEmptyMember(),
      fullName: r.fullName, jobTitle: r.jobTitle, organization: r.organization,
      organizationPartnerId: null, nationality: r.nationality,
    }));
    const fields = kind === 'visitors' ? visitorFields : supportFields;
    const blanks = currentRows(kind).map(isBlank);
    // Drop the untouched placeholder rows so the imported list starts where the user's does.
    for (let i = blanks.length - 1; i >= 0; i--) if (blanks[i]) fields.remove(i);
    fields.append(rows);
    // Reported only now, and as what it is: the rows are in the list (IMP-04).
    setSection(kind, {
      report,
      applied: { phase: 'APPENDED', resultingCount: existing.length + rows.length },
    });
  };

  /**
   * Removes ONE person row and re-validates the list it belonged to.
   *
   * The values shift correctly on their own; the error tree does not always follow, and an entry
   * left at an index that no longer exists is an error nobody can see and nobody can fix — it just
   * keeps the "N fields need attention" count above zero for ever. Re-running the resolver for the
   * list is the same validation the rest of the form uses; a structural change simply has to ask
   * for it, and only once the form has been submitted (before that there are no errors to correct).
   */
  const removePersonRow = (kind: 'visitors' | 'supportTeam', rowIndex: number) => {
    // The person holding the contact role is not deletable from here (NP-03 §9). Removing them
    // leaves the campus with a coordinator who is not in the delegation and a link naming a row that
    // no longer exists — the backend refuses exactly this, so blocking it here turns a failed submit
    // at the end of a long form into a sentence at the moment of the click. Nothing is silently
    // re-pointed at somebody else: choosing a different contact is the user's decision to make.
    if (memberHoldsContactRole(kind, rowIndex)) {
      showMessageErrorToast(t('visitRequestV2:card.contactMemberCannotRemove'));
      return;
    }
    (kind === 'visitors' ? visitorFields : supportFields).remove(rowIndex);
    if (form.formState.isSubmitted) void form.trigger(`${base}.${kind}`);
  };

  /** Whether THIS row is the campus's operational contact — asked by key, never by position. */
  const memberHoldsContactRole = (kind: 'visitors' | 'supportTeam', rowIndex: number): boolean => {
    const key = form.getValues(`${base}.${kind}.${rowIndex}.clientMemberKey`);
    return !!key && key === form.getValues(`${base}.operationalContactClientMemberKey`);
  };

  /** Whether the last import for this section still holds rows a replace could use. */
  const canReplaceFrom = (kind: 'visitors' | 'supportTeam') => {
    const report = excelState[kind].report;
    return !!report && canApplyImport(report);
  };

  const requestReplace = (kind: 'visitors' | 'supportTeam') => {
    const report = excelState[kind].report;
    if (!report || !canApplyImport(report)) return;
    setContactLostByReplace(false);
    setReplaceBothBlocked(null);
    setPendingReplace(prev => ({ ...prev, [kind]: { rows: report.data, fileName: report.fileName } }));
    // The question is raised where the list is, which on a long card can be off screen — the user
    // clicked "Thay thế toàn bộ" and would otherwise see nothing happen. After the paint that
    // creates it, hence the timeout.
    window.setTimeout(() => bringIntoView(replaceConfirmRefs[kind].current), 0);
  };

  /**
   * Scrolls something into view when the environment can, and does nothing when it cannot.
   *
   * <p>`scrollIntoView` is not universally implemented — jsdom has no layout at all, so it is simply
   * absent there. Calling it unguarded from a timeout throws OUTSIDE React's error boundary, which
   * turns a cosmetic nicety into an uncaught exception. Bringing a panel into view is a courtesy;
   * nothing depends on it.</p>
   */
  const bringIntoView = (el: HTMLElement | null) => {
    if (typeof el?.scrollIntoView === 'function') el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  };

  /** Imported rows as member rows: NEW people, each born with its own identity. */
  const toMemberRows = (rows: PersonRow[]) =>
    rows.map(r => ({ ...createEmptyMember(), ...r, organizationPartnerId: null }));

  /**
   * Applies one section's pending replace. Reusing the outgoing keys would silently leave the
   * contact pointing at a row that now describes somebody else entirely, so every row is re-minted
   * and a contact who is no longer in the list is REPORTED rather than re-aimed.
   */
  const confirmReplace = (kind: 'visitors' | 'supportTeam') => {
    const pending = pendingReplace[kind];
    if (!pending) return;
    const contactKey = form.getValues(`${base}.operationalContactClientMemberKey`);
    // The OTHER list is untouched by this replace, so a contact who lives there still exists.
    const survives = contactKey
      ? (form.getValues(`${base}.${kind === 'visitors' ? 'supportTeam' : 'visitors'}`) ?? [])
        .some(m => m?.clientMemberKey === contactKey)
      : true;
    (kind === 'visitors' ? visitorFields : supportFields).replace(toMemberRows(pending.rows));
    setPendingReplace(prev => ({ ...prev, [kind]: null }));
    setContactLostByReplace(!survives);
    setSection(kind, {
      ...excelState[kind],
      applied: { phase: 'REPLACED', resultingCount: pending.rows.length },
    });
  };

  const cancelReplace = (kind: 'visitors' | 'supportTeam') =>
    setPendingReplace(prev => ({ ...prev, [kind]: null }));

  /**
   * Replaces BOTH lists as one indivisible change (IMP-03).
   *
   * <p>Two separate confirmations leave a real state between them — guests replaced, support staff
   * not — in which the campus's delegation is half of one file and half of another. Worse, the
   * duplicate check that matters here is on the MERGED list: a person appearing in both files is
   * only visible once both are in hand, and applying one of them first means the conflict is
   * discovered after the other list has already been destroyed.</p>
   *
   * <p>So the new lists are BUILT first, checked, and only then written. Nothing is applied if
   * anything is wrong — the old lists, including everything typed by hand, are still there to
   * correct.</p>
   */
  const confirmReplaceBoth = () => {
    const guestPending = pendingReplace.visitors;
    const supportPending = pendingReplace.supportTeam;
    if (!guestPending || !supportPending) return;

    const nextVisitors = toMemberRows(guestPending.rows);
    const nextSupport = toMemberRows(supportPending.rows);

    const duplicates = findCampusMemberDuplicates({ visitors: nextVisitors, supportTeam: nextSupport });
    if (duplicates.length > 0) {
      // Named, not counted: "2 dòng trùng" tells the user nothing about which file to fix.
      setReplaceBothBlocked(duplicates.map(d => d.first.fullName).join(', '));
      return;
    }

    // Both keys are re-minted, so a contact picked from either list cannot survive this.
    const contactKey = form.getValues(`${base}.operationalContactClientMemberKey`);
    visitorFields.replace(nextVisitors);
    supportFields.replace(nextSupport);
    setPendingReplace({ visitors: null, supportTeam: null });
    setReplaceBothBlocked(null);
    setContactLostByReplace(!!contactKey);
    setExcelState(prev => ({
      visitors: { ...prev.visitors, applied: { phase: 'REPLACED', resultingCount: nextVisitors.length } },
      supportTeam: { ...prev.supportTeam, applied: { phase: 'REPLACED', resultingCount: nextSupport.length } },
    }));
  };

  // ── Operational-contact quick fill (plan §11–§13) ──────────────────────────────────────────
  // Watched, not read on click, so the buttons enable themselves as soon as the source block is
  // usable rather than staying grey until something else re-renders the card.
  // Only the registrant is left to copy from: there is no request-level contact any more, so
  // "same as the request's contact" no longer names anything a campus could inherit.
  const watchedRegistrant = watch('registerInfo');
  const [pendingQuickFill, setPendingQuickFill] = useState<QuickFillSource | null>(null);
  const [quickFilledFrom, setQuickFilledFrom] = useState<QuickFillSource | null>(null);

  const quickFillValues = (_kind: QuickFillSource = 'registrant') => {
    const src = watchedRegistrant;
    // A real copy into this campus's own five fields, not a display fallback: the snapshot must be
    // complete on its own, because the registrant can change theirs afterwards and the contact this
    // campus agreed with must not change with it.
    return {
      fullName: src?.fullName ?? '',
      organization: src?.organization ?? '',
      jobTitle: src?.jobTitle ?? '',
      phone: src?.phone ?? '',
      email: src?.email ?? '',
    };
  };

  /** A source with no name and no email cannot fill anything useful, so its button stays off. */
  const canQuickFillFrom = (kind: QuickFillSource) => {
    const v = quickFillValues(kind);
    return v.fullName.trim().length > 0 && v.email.trim().length > 0;
  };

  const operationalContactHasData = () => {
    const oc = form.getValues(`${base}.operationalContact`);
    return QUICK_FILL_FIELDS.some(f => (oc?.[f] ?? '').trim().length > 0);
  };

  const applyQuickFill = (kind: QuickFillSource) => {
    const v = quickFillValues(kind);
    // Per-field setValue on THIS card only — a whole-object set on a shared path would have leaked
    // into the other campus cards, which are independent snapshots by design.
    for (const f of QUICK_FILL_FIELDS) {
      const fieldPath = `${base}.operationalContact.${f}` as any;
      form.setValue(fieldPath, v[f], { shouldDirty: true, shouldTouch: true });
      form.clearErrors(fieldPath);
    }
    setQuickFilledFrom(kind);
    setPendingQuickFill(null);
    showSuccessToast(t(kind === 'registrant'
      ? 'visitRequestV2:card.quickFillCopiedRegistrant'
      : 'visitRequestV2:card.quickFillCopiedContact'));
  };

  /**
   * "Dùng người đăng ký" — never overwrites silently: anything already typed here is confirmed away
   * first (plan §13).
   *
   * <p>If the registrant is ALREADY in the delegation, that member is selected instead of a second
   * copy of them being typed into the contact block. Matching is on name + job title + organisation
   * together, never on the name alone: two members of one delegation sharing a name is ordinary, and
   * merging them would attach the visit's coordinator to a stranger. Member rows carry no email, so
   * that is the strongest evidence available here (§8).</p>
   */
  const requestQuickFill = (kind: QuickFillSource) => {
    const v = quickFillValues(kind);
    const sameHuman = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();
    const already = eligibleMembers.find(m =>
      m.complete
      && sameHuman(m.fullName, v.fullName)
      && sameHuman(m.jobTitle, v.jobTitle)
      && sameHuman(m.organization, v.organization));

    if (already) {
      pickContactFromDelegation(already.key);
      showSuccessToast(t('visitRequestV2:card.contactPickedExistingMember', { name: already.fullName }));
      return;
    }

    if (operationalContactHasData()) setPendingQuickFill(kind);
    else applyQuickFill(kind);
    // Copying the registrant in means the contact is NOT one of the members listed below.
    form.setValue(`${base}.operationalContactClientMemberKey`, null, { shouldDirty: true });
  };

  // ── "Đầu mối là ai trong đoàn?" (NP-03) ──────────────────────────────────────────────────────
  // The contact used to be five free-text fields with no relation to anything, so the system could
  // only guess — by comparing strings — whether the person coordinating the visit was also one of
  // the people attending it. Guessing produced both failure modes at once: a contact who WAS in the
  // list appeared twice in the biên bản, and a contact who was NOT in it appeared nowhere.
  //
  // Picking them here answers that outright, by the row's own stable key. A key rather than its
  // position in the list, because a position is not an identity: adding a row above the chosen one,
  // deleting one, or reordering used to re-aim the contact at a different person silently.
  const watchedVisitors = watch(`${base}.visitors`) ?? [];
  const watchedSupport = watch(`${base}.supportTeam`) ?? [];
  const contactMemberKey = watch(`${base}.operationalContactClientMemberKey`) ?? null;
  const watchedContact = watch(`${base}.operationalContact`);
  /**
   * True when a READ-ONLY (existing) campus's contact snapshot is missing one of the fields Manage
   * Contact now requires — a legacy row written before that rule existed. Phone is excluded: it has
   * always been optional. Drives an informational warning only; this form never validates these
   * fields for an existing campus (see the schema's per-campus superRefine), so it must never be
   * confused with a real validation error.
   */
  const legacyContactIncomplete = contactReadOnly && (
    !(watchedContact?.fullName ?? '').trim()
    || !(watchedContact?.organization ?? '').trim()
    || !(watchedContact?.jobTitle ?? '').trim()
    || !(watchedContact?.email ?? '').trim()
  );

  type MemberKind = 'visitors' | 'supportTeam';
  interface EligibleMember {
    kind: MemberKind;
    rowIndex: number;
    key: string;
    fullName: string;
    jobTitle: string;
    organization: string;
    organizationPartnerId: number | null;
    /** Whether every field the role needs is filled in — an incomplete row is shown but not pickable. */
    complete: boolean;
    /**
     * This row's PERSISTENT database id, or null for a row with none yet (freshly added this
     * session). Read by the relationship-only picker (plan CanhIter3FixBug) so a pick on an
     * EXISTING campus can be sent by durable id instead of the ephemeral key alone.
     */
    guestMemberId: number | null;
  }

  /**
   * Everybody who could be this campus's contact: the delegation AND the support staff travelling
   * with it. Support staff are eligible on purpose — an interpreter, an assistant or a coordinator
   * is frequently exactly who the campus rings, and offering only guests meant that person had to be
   * retyped as a separate contact, which is how the link was lost in the first place. FPTU's own
   * people are absent because they are not in these two lists at all.
   */
  // Rebuilt on every render rather than memoized on the watched arrays. React Hook Form mutates its
  // value tree IN PLACE, so `watch('…visitors')` hands back the same array object with different
  // contents — a `useMemo` keyed on it never recomputes, and the picker silently kept showing the
  // list as it stood when the card first mounted.
  const buildEligible = (rows: typeof watchedVisitors, kind: MemberKind): EligibleMember[] =>
    (rows ?? []).map((m, rowIndex) => {
      const fullName = (m?.fullName ?? '').trim();
      const jobTitle = (m?.jobTitle ?? '').trim();
      const organization = (m?.organization ?? '').trim();
      return {
        kind,
        rowIndex,
        key: m?.clientMemberKey ?? '',
        fullName,
        jobTitle,
        organization,
        organizationPartnerId: m?.organizationPartnerId ?? null,
        guestMemberId: m?.guestMemberId ?? null,
        // The same three fields the contact snapshot needs. A row missing any of them cannot
        // describe a contact, so it is listed as "chưa hoàn tất" rather than silently omitted —
        // omitting it left the user hunting for a person they had definitely typed in.
        complete: !!m?.clientMemberKey && !!fullName && !!jobTitle && !!organization,
      };
    });

  const eligibleMembers: EligibleMember[] = [
    ...buildEligible(watchedVisitors, 'visitors'),
    ...buildEligible(watchedSupport, 'supportTeam'),
  ];
  /** Which members exist right now, as a value an effect can actually depend on. */
  const eligibleKeys = eligibleMembers.map(m => m.key).join('|');

  const pickedMember = eligibleMembers.find(m => m.key === contactMemberKey) ?? null;

  // ── One person, one member row (ID-02) ────────────────────────────────────────────────────────
  // Guests and support staff are two doors into the same table, and nothing compared them: the
  // importer de-duplicates inside the list it is importing, and each array was validated on its own.
  // So the same human written into both was submitted as two members, stored with two different
  // guest_member_ids, and every id-first rule downstream then correctly read that as two people —
  // which is how the biên bản ended up listing somebody twice with nothing able to tell that the two
  // rows were one person. Recomputed each render for the same reason `eligibleMembers` is: React
  // Hook Form mutates its value tree in place, so a memo keyed on the watched arrays never reruns.
  const memberDuplicates: MemberDuplicatePair[] = findMemberDuplicates(
    campusMemberRows({ visitors: watchedVisitors, supportTeam: watchedSupport }));

  /** Drops the SECOND of a duplicate pair — the redundant row, not the one already established. */
  const dropDuplicateMember = (pair: MemberDuplicatePair) => {
    if (memberHoldsContactRole(pair.second.kind, pair.second.rowIndex)) {
      showMessageErrorToast(t('visitRequestV2:card.contactMemberCannotRemove'));
      return;
    }
    (pair.second.kind === 'visitors' ? visitorFields : supportFields).remove(pair.second.rowIndex);
    if (form.formState.isSubmitted) void form.trigger(`${base}.${pair.second.kind}`);
  };

  /**
   * "Đây là nhân sự hỗ trợ, không phải khách": ONE member changes list, keeping its identity.
   *
   * <p>The guest row is moved across WITH ITS OWN `clientMemberKey` and the duplicate support row is
   * dropped — so the person the contact picker names, and the member the backend will insert, is the
   * same one throughout. Deleting the guest and typing them into the support list instead would mint
   * a new identity, which is the very thing that produced two rows for one person.</p>
   */
  const moveMemberToSupport = (pair: MemberDuplicatePair) => {
    const guest = pair.first.kind === 'visitors' ? pair.first : pair.second;
    const support = pair.first.kind === 'visitors' ? pair.second : pair.first;
    if (guest.kind !== 'visitors' || support.kind !== 'supportTeam') return;
    const moved = form.getValues(`${base}.visitors.${guest.rowIndex}`);
    if (!moved) return;
    // The duplicate goes first: removing the guest first would shift nothing in the support list,
    // but doing it in one order consistently keeps both index reads valid.
    supportFields.remove(support.rowIndex);
    visitorFields.remove(guest.rowIndex);
    supportFields.append({ ...moved });
    if (form.formState.isSubmitted) {
      void form.trigger(`${base}.visitors`);
      void form.trigger(`${base}.supportTeam`);
    }
    showSuccessToast(t('visitRequestV2:card.duplicateMovedToSupport', { name: guest.fullName }));
  };

  /** Sends the user to the row that has to gain a distinguishing detail — nothing is changed here. */
  const focusDuplicateRow = (pair: MemberDuplicatePair) => {
    const el = document.querySelector<HTMLElement>(
      `[data-testid="${pair.second.kind}-${pair.second.rowIndex}-jobTitle"]`);
    bringIntoView(el);
    el?.focus();
  };

  const memberLabel = (m: EligibleMember) => {
    const kindLabel = t(m.kind === 'visitors'
      ? 'visitRequestV2:card.contactPickKindGuest'
      : 'visitRequestV2:card.contactPickKindSupport');
    if (!m.complete) {
      return t('visitRequestV2:card.contactPickIncomplete', {
        name: m.fullName || t('visitRequestV2:card.contactPickUnnamed', { index: m.rowIndex + 1 }),
        kind: kindLabel,
      });
    }
    return [m.fullName, m.jobTitle, m.organization, kindLabel].filter(Boolean).join(' — ');
  };

  /** Copies the member's three shared fields into the contact snapshot. */
  const syncContactFromMember = React.useCallback((m: {
    fullName: string; jobTitle: string; organization: string;
  }, options?: { touch?: boolean }) => {
    // Only what a delegation row actually knows. Overwriting phone/email with blanks would delete
    // the one thing the campus needs to reach this person, and neither is on a member row.
    ([['fullName', m.fullName], ['jobTitle', m.jobTitle], ['organization', m.organization]] as const)
      .forEach(([field, value]) => {
        const fieldPath = `${base}.operationalContact.${field}` as const;
        if (form.getValues(fieldPath) === value) return;
        form.setValue(fieldPath, value, { shouldDirty: true, shouldTouch: options?.touch ?? false });
        form.clearErrors(fieldPath);
      });
  }, [base, form]);

  const pickContactFromDelegation = (key: string) => {
    if (key === '') {
      form.setValue(`${base}.operationalContactClientMemberKey`, null, { shouldDirty: true });
      return;
    }
    const member = eligibleMembers.find(m => m.key === key);
    if (!member || !member.complete) return;
    form.setValue(`${base}.operationalContactClientMemberKey`, key, { shouldDirty: true });
    syncContactFromMember(member, { touch: true });
    setQuickFilledFrom(null);
  };

  /**
   * RELATIONSHIP-ONLY pick, for an EXISTING campus (`contactReadOnly`) — plan CanhIter3FixBug "Đầu
   * mối hiện tại có nằm trong danh sách đoàn không?".
   *
   * <p>Deliberately the ONLY difference from {@link pickContactFromDelegation}: no
   * {@link syncContactFromMember} call. The contact's five snapshot fields are not just
   * unavailable to edit here (see the `contactReadOnly` branch below) — they must never move as a
   * SIDE EFFECT of this pick either, or "which delegation member is this" would silently become
   * "replace the operational contact", which is a different, actual-contact-holder-changing
   * workflow (Replace/Transfer) that this control must never reach into.</p>
   */
  const pickRelationOnly = (key: string) => {
    if (key === '') {
      form.setValue(`${base}.operationalContactClientMemberKey`, null, { shouldDirty: true });
      return;
    }
    const member = eligibleMembers.find(m => m.key === key);
    if (!member || !member.complete) return;
    form.setValue(`${base}.operationalContactClientMemberKey`, key, { shouldDirty: true });
  };

  /**
   * Keeps the contact snapshot on the SAME person as the member row while the form is open (NP-03).
   *
   * <p>Before submit the contact block is a draft, not a record: the user picks somebody from a row
   * that is often still half-typed ("Khách 1", no job title yet), then fills that row in. The copy
   * used to happen once, at the moment of picking, so everything typed afterwards stayed in the
   * member row and never reached the contact — the request was filed naming a contact with no title
   * and the wrong organisation, and nothing on screen said so.</p>
   *
   * <p>It follows the KEY, so it survives rows being added, removed or reordered around it, and it
   * is deliberately one-directional: the member row is the source, the snapshot the copy. Changing
   * the partner behind a member's organisation is an edit to that member, not a different person, so
   * the pick is untouched and only the text follows.</p>
   *
   * <p>Runs only while the contact is editable. On an EXISTING campus the snapshot is what was
   * agreed and the backend refuses to change it, so following the member there would produce an edit
   * that cannot be saved.</p>
   */
  React.useEffect(() => {
    if (contactReadOnly || !pickedMember || !pickedMember.complete) return;
    syncContactFromMember(pickedMember);
    // The picked member's own VALUES, not the object: `pickedMember` is rebuilt on every render, so
    // depending on it would re-run this constantly. These four are exactly what the copy reads.
  }, [
    contactReadOnly, syncContactFromMember,
    pickedMember?.key, pickedMember?.fullName, pickedMember?.jobTitle, pickedMember?.organization,
  ]);

  /**
   * A pick that no longer names a row — the member was removed by something other than the guarded
   * delete button (an Excel replace, an apply-to-all). The pick is cleared rather than left dangling,
   * because a payload carrying a key that names nobody is refused by the backend.
   */
  React.useEffect(() => {
    if (!contactMemberKey || contactReadOnly) return;
    if (eligibleKeys.split('|').includes(contactMemberKey)) return;
    form.setValue(`${base}.operationalContactClientMemberKey`, null, { shouldDirty: true });
  }, [contactMemberKey, contactReadOnly, eligibleKeys, form, base]);

  /** Focuses the member row the contact is, so "Sửa thông tin thành viên" lands somewhere useful. */
  const focusContactMemberRow = () => {
    if (!pickedMember) return;
    const el = document.querySelector<HTMLElement>(
      `[data-testid="${pickedMember.kind}-${pickedMember.rowIndex}-fullName"]`);
    bringIntoView(el);
    el?.focus();
  };

  /**
   * True when a contact has been typed who matches nobody in the delegation list.
   *
   * <p>Not an error — coordinating a visit you are not attending is completely ordinary — so this
   * only offers to add them, and says what adding them means. Silence would be worse: the user
   * would not learn that this person is absent from the delegation until the biên bản.</p>
   */
  const contactMatchesNoVisitor =
    contactMemberKey === null
    && (watchedContact?.fullName ?? '').trim().length > 0
    && !eligibleMembers.some(m =>
      m.fullName.toLowerCase() === (watchedContact?.fullName ?? '').trim().toLowerCase());

  const addContactToDelegation = () => {
    // Born with its identity, and picked by that identity — never by "the row I just appended",
    // which is a position and would be wrong the moment anything else changed the list.
    const row = { ...createEmptyMember(),
      fullName: (watchedContact?.fullName ?? '').trim(),
      jobTitle: (watchedContact?.jobTitle ?? '').trim(),
      organization: (watchedContact?.organization ?? '').trim(),
      organizationPartnerId: null,
      nationality: '' };
    visitorFields.append(row);
    form.setValue(`${base}.operationalContactClientMemberKey`, row.clientMemberKey ?? null,
      { shouldDirty: true });
    showSuccessToast(t('visitRequestV2:card.contactAddedToDelegation'));
  };

  const fieldError = (path: string): string | undefined => {
    const segs = path.split('.');
    let node: unknown = cardErrors;
    for (const s of segs) {
      if (!node || typeof node !== 'object') return undefined;
      node = (node as Record<string, unknown>)[s];
    }
    const record = node as { message?: unknown; root?: { message?: unknown } } | undefined;
    const msg = record?.message;
    if (typeof msg === 'string') return msg;
    // React Hook Form's useFieldArray puts an array-LEVEL error (e.g. visitors.min(1)) at
    // `<path>.root.message`, not `<path>.message` — without this fallback, an empty required
    // list correctly counts toward the top summary (countFieldErrors walks .root too) but never
    // renders here, leaving the user with "1 field needs fixing" and no visible reason why.
    const rootMsg = record?.root?.message;
    return typeof rootMsg === 'string' ? rootMsg : undefined;
  };

  const bodyId = `campus-card-body-${clientKey}`;

  const cellError = (msg?: string) =>
    msg ? <p className="px-2 pb-1 text-xs font-normal text-red-600">{msg}</p> : null;

  // Array-level "at least 1 visitor" error (visitRequestV2.schema.ts's .min(1)) — read once so the
  // fieldset wrapper, the message and the focus-target button all agree on the same value.
  const visitorsListError = fieldError('visitors');

  /**
   * Name / job title are plain text; organization and nationality reuse the SAME searchable
   * comboboxes as v1 (free text still allowed when nothing matches). Every field goes through
   * Controller because each row is rendered twice — desktop table and mobile card — and two
   * uncontrolled `register()` refs for one field make React Hook Form track only the last one.
   */
  const personFieldCell = (
    kind: 'visitors' | 'supportTeam',
    rowIndex: number,
    field: 'fullName' | 'jobTitle' | 'organization' | 'nationality',
    isCell: boolean,
  ) => {
    const name = `${base}.${kind}.${rowIndex}.${field}` as const;
    const hasError = !!fieldError(`${kind}.${rowIndex}.${field}`);
    const placeholder = t(`visitRequestV2:person.${field}`);

    /**
     * Editing the person who is the campus's contact, on a campus that ALREADY EXISTS (NP-03 §10).
     *
     * <p>After submit the contact's five recorded fields are history — what was agreed, when it was
     * agreed — and the backend refuses to change them through an edit. The member row is still
     * freely editable, so the two can legitimately end up describing the same person differently,
     * and that has to be a deliberate act rather than something the user discovers later on the
     * detail screen. The edit is applied and then explained, with an undo, because interrupting a
     * keystroke to ask permission loses what was typed.</p>
     *
     * <p>The identity of the contact does NOT move: same person, updated details, same
     * <c>guest_member_id</c>.</p>
     */
    const noteContactMemberEdit = (previous: string) => {
      if (!contactReadOnly || contactEditAcknowledgedRef.current) return;
      if (!memberHoldsContactRole(kind, rowIndex)) return;
      setContactMemberEdit({ kind, rowIndex, field, previous });
    };

    if (field === 'organization' || field === 'nationality') {
      return (
        <Controller
          name={name}
          control={control}
          render={({ field: f }) => (field === 'nationality' ? (
            <CountrySelect
              strict
              value={f.value ?? ''}
              onChange={revalidatingChange(f)}
              onBlur={f.onBlur}
              hasError={hasError}
              placeholder={placeholder}
              isCell={isCell}
            />
          ) : (
            <OrganizationCombobox
              value={f.value ?? ''}
              partnerId={watch(`${base}.${kind}.${rowIndex}.organizationPartnerId`) ?? null}
              onChange={(val, pickedPartnerId) => {
                noteContactMemberEdit((f.value as string | undefined) ?? '');
                revalidatingChange(f)(val);
                // The id is a SIBLING field, so it has to be written explicitly. Doing it in the same
                // handler is what keeps the two honest: text and identity always change together, and
                // typing over a picked organization clears the id rather than leaving the form
                // pointing at a partner the user has edited away from (PART-01).
                form.setValue(
                  `${base}.${kind}.${rowIndex}.organizationPartnerId`,
                  pickedPartnerId,
                  { shouldDirty: true },
                );
              }}
              onBlur={f.onBlur}
              hasError={hasError}
              placeholder={placeholder}
              isCell={isCell}
            />
          ))}
        />
      );
    }

    // Name and job title WRAP rather than scroll sideways: a long name in a table cell used to
    // be readable only by dragging the caret through it (plan §21.3).
    const max = field === 'fullName' ? MAX.personName : MAX.personJobTitle;
    return (
      <Controller
        name={name}
        control={control}
        render={({ field: f }) => (
          <AutoGrowTextField
            value={f.value ?? ''}
            onChange={(val: string) => {
              noteContactMemberEdit((f.value as string | undefined) ?? '');
              revalidatingChange(f)(val);
            }}
            onBlur={f.onBlur}
            placeholder={placeholder}
            ariaLabel={placeholder}
            hasError={hasError}
            maxLength={max}
            isCell={isCell}
            testId={`${kind}-${rowIndex}-${field}`}
          />
        )}
      />
    );
  };

  /**
   * The "thay thế toàn bộ?" question for ONE section, rendered INSIDE that section (IMP-01).
   *
   * <p>It used to live once, after both tables, so importing a guest list asked the question
   * underneath the support staff. The two are now separate questions in separate places, and both
   * can be open at the same time without either overwriting the other's answer.</p>
   */
  const replaceConfirm = (kind: 'visitors' | 'supportTeam') => {
    const pending = pendingReplace[kind];
    if (!pending) return null;
    const label = t(kind === 'visitors'
      ? 'visitRequestV2:excel.report.listGuests'
      : 'visitRequestV2:excel.report.listSupport');
    return (
      <div
        ref={replaceConfirmRefs[kind]}
        role="alertdialog"
        data-testid={`v2-replace-confirm-${kind}`}
        className="mt-3 rounded-xl border border-amber-300 bg-amber-50 p-3"
      >
        <p className="text-sm font-bold text-amber-900">
          {t('visitRequestV2:excel.replaceConfirmTitleFor', { list: label })}
        </p>
        <p className="mt-1 text-sm text-amber-800">
          {t('visitRequestV2:excel.replaceConfirmBodyFor', {
            list: label,
            fileName: pending.fileName,
            count: pending.rows.length,
            current: currentRows(kind).filter(r => !isBlank(r)).length,
          })}
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          <button
            type="button"
            data-testid={`v2-replace-confirm-yes-${kind}`}
            className="rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-amber-700"
            onClick={() => confirmReplace(kind)}
          >
            {t('visitRequestV2:excel.replaceConfirmYesFor', { list: label })}
          </button>
          <button
            type="button"
            data-testid={`v2-replace-confirm-no-${kind}`}
            className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-bold text-amber-800 hover:bg-amber-100"
            onClick={() => cancelReplace(kind)}
          >
            {t('visitRequestV2:excel.replaceConfirmNo')}
          </button>
        </div>
      </div>
    );
  };

  const PERSON_COLUMNS = ['fullName', 'jobTitle', 'organization', 'nationality'] as const;

  /**
   * Desktop renders a table whose leading column is a RE-DERIVED ordinal (`i + 1`), so removing a
   * row renumbers the rest automatically — the number is never stored. Below `lg` the same rows
   * become stacked cards, since a 6-column table cannot stay readable on a phone.
   */
  const personTable = (
    kind: 'visitors' | 'supportTeam',
    rows: { id: string }[],
    onRemoveRow: (i: number) => void,
    canRemoveRow: (i: number) => boolean,
    onAddRow?: () => void,
  ) => {
    if (rows.length === 0 && kind === 'supportTeam') {
      return (
        <div className="flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-slate-200 bg-slate-50/50 p-6 text-center">
          <p className="text-sm font-normal text-slate-500">
            {t('visitRequestV2:person.noSupportTeam')}
          </p>
          {onAddRow && (
            <button
              type="button"
              className="mt-3 inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm font-semibold text-slate-600 transition-colors hover:bg-slate-50"
              onClick={onAddRow}
            >
              <Plus className="h-4 w-4" /> {t('visitRequestV2:card.addSupport')}
            </button>
          )}
        </div>
      );
    }

    return (
    <>
      <div className="hidden overflow-x-auto rounded-xl border border-slate-200 bg-white lg:block">
        <table data-testid={`v2-${kind}-table`} className="w-full min-w-[760px] table-fixed border-collapse text-sm">
          <colgroup>
            <col style={{ width: '4%' }} />
            <col style={{ width: '22%' }} />
            <col style={{ width: '24%' }} />
            <col style={{ width: '25%' }} />
            <col style={{ width: '17%' }} />
            <col style={{ width: '8%' }} />
          </colgroup>
          <thead className="border-b border-slate-200 bg-[#F8FAFC]">
            <tr>
              <th scope="col" className="border-r border-slate-200 p-3 text-center align-middle font-bold text-slate-600">
                {t('visitRequestV2:person.stt')}
              </th>
              {PERSON_COLUMNS.map(c => (
                <th key={c} scope="col" className="border-r border-slate-200 p-3 text-left align-middle font-bold text-slate-600">
                  {t(`visitRequestV2:person.${c}`)}
                </th>
              ))}
              <th scope="col" className="p-3 text-center align-middle font-bold text-slate-600">
                {/* 
                  Rút gọn tiêu đề thành icon hoặc text ngắn. "Thao tác" khá ngắn (8 ký tự).
                  Với 5% width, icon phù hợp hơn nếu không đủ chỗ, nhưng tạm giữ text theo i18n
                  và tránh xuống dòng với whitespace-nowrap.
                */}
                <span className="whitespace-nowrap">{t('visitRequestV2:person.actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200">
            {rows.map((f, i) => (
              <tr key={f.id} className="transition-colors hover:bg-slate-50">
                {/* The ordinal is RE-DERIVED, never stored — and the badge says which row the campus's
                    contact is, so editing that person is a deliberate act rather than a surprise. */}
                <td className="border-r border-slate-200 p-3 text-center align-middle font-bold text-slate-400">
                  {i + 1}
                  {memberHoldsContactRole(kind, i) && (
                    <span
                      data-testid={`${kind}-${i}-is-contact`}
                      title={t('visitRequestV2:card.contactMemberBadgeTitle')}
                      className="mt-1 block text-[10px] font-bold uppercase leading-tight text-[#0F5DA6]"
                    >
                      {t('visitRequestV2:card.contactMemberBadge')}
                    </span>
                  )}
                </td>
                {PERSON_COLUMNS.map(c => (
                  <td key={c} className="group border-r border-slate-200 p-0 align-top focus-within:bg-white focus-within:shadow-[inset_0_0_0_2px_rgba(15,93,166,0.16)] hover:bg-slate-50/50">
                    {personFieldCell(kind, i, c, true)}
                    {cellError(fieldError(`${kind}.${i}.${c}`))}
                  </td>
                ))}
                <td className="p-2 text-center align-middle">
                  {/* Deliberately still CLICKABLE for the contact's own row: the click is what
                      surfaces the reason. A disabled button explains nothing on a phone, where
                      there is no tooltip to hover. */}
                  <button
                    type="button"
                    aria-label={kind === 'visitors' ? t('visitRequestV2:person.removeGuest') : t('visitRequestV2:person.removeSupport')}
                    title={memberHoldsContactRole(kind, i)
                      ? t('visitRequestV2:card.contactMemberCannotRemove')
                      : (kind === 'visitors' ? t('visitRequestV2:person.removeGuest') : t('visitRequestV2:person.removeSupport'))}
                    disabled={!canRemoveRow(i)}
                    className="mx-auto flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30"
                    onClick={() => onRemoveRow(i)}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="space-y-3 lg:hidden">
        {rows.map((f, i) => (
          <div key={f.id} className="rounded-xl border border-slate-200 p-3">
            <div className="mb-2 flex items-center justify-between">
              <span className="text-xs font-bold text-slate-400">
                {t('visitRequestV2:person.stt')} {i + 1}
              </span>
              <button
                type="button"
                aria-label={t('visitRequestV2:card.removeRow')}
                disabled={!canRemoveRow(i)}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30"
                onClick={() => onRemoveRow(i)}
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
            <div className="space-y-2">
              {PERSON_COLUMNS.map(c => (
                <div key={c}>
                  {personFieldCell(kind, i, c, false)}
                  {cellError(fieldError(`${kind}.${i}.${c}`))}
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </>
    );
  };

  const isToneA = index % 2 === 0;
  const toneBg = isToneA ? 'bg-white' : 'bg-[#f6f9fc]';
  const toneHeaderOpenBg = isToneA ? 'bg-slate-50' : 'bg-[#f0f5fa]';

  return (
    <div data-testid={`campus-edit-card-${campusCode || 'new'}`} className={`rounded-2xl border shadow-sm transition-all duration-200 border-slate-200 ${toneBg}`}>
      {/* Header — always visible; collapsing hides ONLY the body below */}
      <div className={`flex min-h-[56px] items-center gap-2 p-3 sm:p-4 transition-colors ${open ? `border-b border-slate-100 ${toneHeaderOpenBg} rounded-t-2xl` : 'rounded-2xl'}`}>
        <button
          type="button"
          aria-expanded={open}
          aria-controls={bodyId}
          aria-label={open ? t('visitRequestV2:card.collapse') : t('visitRequestV2:card.expand')}
          title={open ? t('visitRequestV2:card.collapse') : t('visitRequestV2:card.expand')}
          className="flex min-w-0 flex-1 items-center gap-3 text-left"
          onClick={onToggle}
        >
          <div className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full transition-colors ${open ? 'bg-slate-100 text-slate-500' : 'bg-slate-100 text-slate-500 hover:bg-slate-200'}`}>
            <ChevronDown className={`h-5 w-5 transition-transform duration-200 ${open ? 'rotate-180' : '-rotate-90'}`} />
          </div>
          <div className="flex flex-col min-w-0">
            <span className="truncate text-base font-bold text-slate-900">
              {t('visitRequestV2:card.cardN', { n: index + 1 })} {campusCode ? `- ${headerLabel}` : ''}
            </span>
            {!open && watch(`${base}.startDatetime`) && watch(`${base}.endDatetime`) && (
              <span className="truncate text-xs font-normal text-slate-500">
                {(() => {
                  const start = watch(`${base}.startDatetime`);
                  const end = watch(`${base}.endDatetime`);
                  if (!start || !end) return null;
                  const sParts = start.split('T');
                  const eParts = end.split('T');
                  if (sParts.length !== 2 || eParts.length !== 2) return null;
                  const sDate = sParts[0].split('-').reverse().join('/');
                  const eDate = eParts[0].split('-').reverse().join('/');
                  return sDate === eDate 
                    ? `${sDate} ${sParts[1]} - ${eParts[1]}`
                    : `${sDate} ${sParts[1]} - ${eDate} ${eParts[1]}`;
                })()}
              </span>
            )}
          </div>
          {errorCount > 0 ? (
            <span
              role="status"
              className="ml-2 hidden sm:inline-flex items-center gap-1 rounded-full bg-red-100 px-2.5 py-1 text-xs font-bold text-red-700"
            >
              <AlertCircle className="h-3.5 w-3.5" />
              {t('visitRequestV2:card.errorBadge', { count: errorCount })}
            </span>
          ) : cardComplete ? (
            <span className="ml-2 hidden sm:inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-bold text-emerald-700">
              {t('visitRequestV2:status.complete')}
            </span>
          ) : (
            <span className="ml-2 hidden sm:inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-bold text-slate-600">
              {t('visitRequestV2:status.incomplete')}
            </span>
          )}
        </button>

        {copySources.length > 0 && (
          <div className="flex items-center gap-2 text-xs" onClick={e => e.stopPropagation()}>
            {index > 0 && (
              <>
                <span className="font-semibold text-indigo-700 flex items-center gap-1">
                  <Copy className="h-3.5 w-3.5 text-indigo-500" />
                  {t('visitRequestV2:card.copyFromLabel')}
                </span>
                <select
                  id={`copy-src-${clientKey}`}
                  className="h-8 rounded-lg border border-indigo-200 bg-white px-2 py-0 pl-2 pr-7 text-xs font-normal text-indigo-900 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  value={selectedSource}
                  onChange={e => {
                    const val = e.target.value;
                    const src = Number(val);
                    if (!Number.isNaN(src) && val !== '') {
                      setSelectedSource(val);
                      onCopyFrom(src);
                    }
                  }}
                >
                  <option value="" disabled>{t('visitRequestV2:card.copyFromPlaceholder')}</option>
                  {copySources.map(s => (
                    <option key={s.index} value={String(s.index)}>{s.label}</option>
                  ))}
                </select>
              </>
            )}
            <button
              type="button"
              className="rounded-lg border border-indigo-200 bg-indigo-50/70 px-2.5 py-1.5 text-xs font-bold text-indigo-700 transition-colors hover:bg-indigo-100"
              onClick={onApplyToAll}
            >
              {t('visitRequestV2:card.applyToAll')}
            </button>
          </div>
        )}

        {canRemove && (
          <button
            type="button"
            aria-label={t('visitRequestV2:card.removeCampus')}
            className="rounded-lg p-2 text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600"
            onClick={onRemove}
          >
            <Trash2 className="h-4 w-4" />
          </button>
        )}
      </div>

      <div id={bodyId} className={open ? 'space-y-6 border-t border-slate-100 p-4 sm:p-6' : 'hidden'}>

        {/* Campus and Schedule */}
        <div className="grid grid-cols-12 gap-x-6 gap-y-5">
          <div className="col-span-12 xl:col-span-3">
            <FormField label={t('visitRequestV2:card.campus')} required error={fieldError('campus')} showValidIcon={false}>
              {/* CONTROLLED, not `register()`-bound. An uncontrolled select shows whatever the browser
                  picked — the first option — until something writes its value, and the option list here
                  arrives asynchronously: on a fresh card that made the box read as a real campus while
                  the form still held '', and on a hydrated card it did the reverse. Driving `value`
                  from the form state makes the label and `campusId` the same fact by construction. */}
              <Controller
                name={`${base}.campus`}
                control={control}
                render={({ field }) => (
                  <select
                    name={field.name}
                    ref={field.ref}
                    value={field.value ?? ''}
                    onChange={revalidatingChange(field)}
                    onBlur={field.onBlur}
                    className={inputCls(!!fieldError('campus'), !!campusCode, false)}
                    disabled={campusesLoading}
                  >
                    <option value="">{t('visitRequestV2:card.campusPlaceholder')}</option>
                    {campuses
                      // Hide campuses already taken by another card; this card keeps its own selection.
                      .filter(c => c.campusCode === campusCode
                        || !takenCampusCodes.includes(c.campusCode.toUpperCase()))
                      .map(c => (
                        <option key={c.campusCode} value={c.campusCode}>{c.campusName}</option>
                      ))}
                  </select>
                )}
              />
            </FormField>
          </div>

          <div className="col-span-12 xl:col-span-9">
            <Controller
              name={`${base}.startDatetime`}
              control={control}
              render={({ field: startField }) => (
                <Controller
                  name={`${base}.endDatetime`}
                  control={control}
                  render={({ field: endField }) => (
                    <VisitDateTimeRangePicker
                      idPrefix={`campus-${index}`}
                      minAdvanceHours={minAdvanceHours}
                      startValue={startField.value ?? ''}
                      endValue={endField.value ?? ''}
                      // Both halves revalidate: the range rules are cross-field (end after start,
                      // minimum notice), so fixing either one has to be able to clear the other's
                      // error too (NP-02).
                      onChange={({ start, end }) => {
                        revalidatingChange(startField)(start);
                        revalidatingChange(endField)(end);
                      }}
                      startError={fieldError('startDatetime')}
                      endError={fieldError('endDatetime')}
                    />
                  )}
                />
              )}
            />
          </div>
        </div>

        {/* Content */}
        <div className="grid grid-cols-12 gap-x-6 gap-y-5">
          <FormField className="col-span-12 lg:col-span-7" label={t('visitRequestV2:card.delegationName')} required error={fieldError('delegationName')} showValidIcon={false}>
            <Controller
              name={`${base}.delegationName`}
              control={control}
              render={({ field }) => (
                <AutoGrowTextField
                  testId="campus-delegation-input"
                  value={field.value ?? ''}
                  onChange={revalidatingChange(field)}
                  onBlur={field.onBlur}
                  hasError={!!fieldError('delegationName')}
                  maxLength={MAX.delegationName}
                  ariaLabel={t('visitRequestV2:card.delegationName')}
                />
              )}
            />
          </FormField>
          <FormField className="col-span-12 lg:col-span-5" label={t('visitRequestV2:card.visitType')} required error={fieldError('visitType')} showValidIcon={false}>
            <select {...register(`${base}.visitType`)} className={inputCls(!!fieldError('visitType'), false, false)}>
              {VISIT_TYPES.map(vt => (
                <option key={vt} value={vt}>
                  {t(`visitRequest:step2Info.visitTypes.${vt}`, vt)}
                </option>
              ))}
            </select>
          </FormField>
          {visitType === 'OTHER' && (
            <FormField className="col-span-12 lg:col-span-12" label={t('visitRequestV2:card.visitTypeOther')} required error={fieldError('visitTypeOther')} showValidIcon={false}>
              <Controller
                name={`${base}.visitTypeOther`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextField
                    value={field.value ?? ''}
                    onChange={revalidatingChange(field)}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('visitTypeOther')}
                    maxLength={MAX.visitTypeOther}
                    ariaLabel={t('visitRequestV2:card.visitTypeOther')}
                  />
                )}
              />
            </FormField>
          )}
        </div>

        {/* Purpose and working content sit side by side on desktop and stack when narrow — they are
            read together, and both grow with their content rather than scrolling internally. */}
        <div className="grid grid-cols-12 gap-x-6 gap-y-5">
          <FormField className="col-span-12 lg:col-span-6" label={t('visitRequestV2:card.purpose')} required error={fieldError('purpose')} showValidIcon={false}>
            <Controller
              name={`${base}.purpose`}
              control={control}
              render={({ field }) => (
                <AutoGrowTextarea
                  value={field.value ?? ''}
                  onChange={revalidatingChange(field)}
                  onBlur={field.onBlur}
                  hasError={!!fieldError('purpose')}
                  maxLength={MAX.purpose}
                  minRows={3}
                />
              )}
            />
          </FormField>
          <FormField className="col-span-12 lg:col-span-6" label={t('visitRequestV2:card.workingContent')} required error={fieldError('workingContent')} showValidIcon={false}>
            <Controller
              name={`${base}.workingContent`}
              control={control}
              render={({ field }) => (
                <AutoGrowTextarea
                  value={field.value ?? ''}
                  onChange={revalidatingChange(field)}
                  onBlur={field.onBlur}
                  hasError={!!fieldError('workingContent')}
                  maxLength={MAX.workingContent}
                  minRows={3}
                />
              )}
            />
          </FormField>
        </div>

        {/* ── Editing the member who IS this campus's contact, after submit (NP-03 §10) ──
            Says what the edit does and, just as importantly, what it does NOT do: the contact's
            recorded details stay as they were agreed, and the person is still the same person. The
            third option the workflow would need — "update the recorded contact too" — is not offered
            here because it is not this form's to make: changing those five fields can require the new
            person to accept, so it lives on the detail screen with the rest of that workflow. */}
        {contactMemberEdit && (
          <div
            role="alertdialog"
            data-testid={`campus-contact-member-edit-${index}`}
            className="rounded-xl border border-amber-300 bg-amber-50 p-3"
          >
            <p className="text-sm font-bold text-amber-900">
              {t('visitRequestV2:card.contactMemberEditTitle')}
            </p>
            <p className="mt-1 text-xs text-amber-900">
              {t('visitRequestV2:card.contactMemberEditBody')}
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              <button
                type="button"
                data-testid={`campus-contact-member-edit-keep-${index}`}
                className="rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-amber-700"
                onClick={() => { contactEditAcknowledgedRef.current = true; setContactMemberEdit(null); }}
              >
                {t('visitRequestV2:card.contactMemberEditKeep')}
              </button>
              <button
                type="button"
                data-testid={`campus-contact-member-edit-undo-${index}`}
                className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-bold text-amber-800 hover:bg-amber-100"
                onClick={() => {
                  form.setValue(
                    `${base}.${contactMemberEdit.kind}.${contactMemberEdit.rowIndex}.${contactMemberEdit.field}` as
                      FieldPath<VisitRequestV2Schema>,
                    contactMemberEdit.previous,
                    { shouldDirty: true },
                  );
                  setContactMemberEdit(null);
                }}
              >
                {t('visitRequestV2:card.contactMemberEditUndo')}
              </button>
            </div>
          </div>
        )}

        {/* ── Two files waiting at once (IMP-03) ────────────────────────────────────────────
            Answering the two confirmations one after the other leaves a real state in between —
            guests from the new file, support staff from the old — and, worse, the duplicate check
            that matters spans the two lists: a person in both files is only visible once both are
            in hand. Doing it as one action is what lets that be checked BEFORE anything is
            destroyed. "Xử lý từng danh sách" is not a second mode; it is just the two confirmations
            below, which are always available. */}
        {pendingReplace.visitors && pendingReplace.supportTeam && (
          <div
            data-testid={`campus-replace-both-${index}`}
            className="rounded-xl border border-amber-300 bg-amber-50 p-3"
          >
            <p className="text-sm font-bold text-amber-900">
              {t('visitRequestV2:excel.replaceBothTitle')}
            </p>
            <p className="mt-1 text-sm text-amber-800">
              {t('visitRequestV2:excel.replaceBothBody', {
                guestFile: pendingReplace.visitors.fileName,
                guestCount: pendingReplace.visitors.rows.length,
                supportFile: pendingReplace.supportTeam.fileName,
                supportCount: pendingReplace.supportTeam.rows.length,
              })}
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              <button
                type="button"
                data-testid={`campus-replace-both-yes-${index}`}
                className="rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-amber-700"
                onClick={confirmReplaceBoth}
              >
                {t('visitRequestV2:excel.replaceBothYes')}
              </button>
            </div>
            {/* Nothing was applied — both old lists, including everything typed by hand, are
                untouched. Naming the person is what makes the file fixable. */}
            {replaceBothBlocked && (
              <p
                data-testid={`campus-replace-both-blocked-${index}`}
                role="alert"
                className="mt-2 rounded-lg bg-amber-100 px-2.5 py-2 text-xs font-semibold text-amber-900"
              >
                {t('visitRequestV2:excel.replaceBothBlocked', { names: replaceBothBlocked })}
              </p>
            )}
          </div>
        )}

        {/* The campus is left WITHOUT a coordinator rather than with a guessed one: a name matching
            in the new file does not make it the same person (IMP-03). */}
        {contactLostByReplace && (
          <div
            role="alert"
            data-testid={`campus-contact-lost-${index}`}
            className="rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm font-normal text-amber-900"
          >
            {t('visitRequestV2:card.contactLostAfterReplace')}
          </div>
        )}

        {/* Visitors */}
        <fieldset data-field-error={visitorsListError ? 'true' : undefined}>
          <legend className="mb-2 flex w-full flex-wrap items-center gap-2 text-sm font-extrabold text-slate-900">
            {t('visitRequestV2:card.visitors')} <span className="text-red-500">*</span>
            <span className="text-xs font-normal text-slate-400">
              {t('visitRequestV2:card.memberCount', { count: visitorFields.fields.length, max: V2_MAX_MEMBERS_PER_CAMPUS })}
            </span>
            <span className="ml-auto flex items-center gap-2">
              <button
                type="button"
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50"
                onClick={() => downloadVisitorTemplate(excelT)}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.template')}
              </button>
              <button
                type="button"
                data-testid="v2-visitors-import"
                disabled={!!excelState.visitors.loadingFileName}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50 disabled:opacity-50"
                onClick={() => visitorFileRef.current?.click()}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.importForCampus')}
              </button>
            </span>
          </legend>
          <input
            ref={visitorFileRef}
            type="file"
            accept=".xlsx,.xls"
            className="hidden"
            aria-hidden
            onChange={e => {
              const file = e.target.files?.[0];
              if (file) void importExcel('visitors', file);
              e.target.value = '';
            }}
          />
          {visitorsListError && (
            <p role="alert" className="mb-2 text-xs font-normal text-red-600">{visitorsListError}</p>
          )}
          <ExcelImportPanel
            testId="v2-excel-visitors"
            state={excelState.visitors}
            kind="visitors"
            campusLabel={headerLabel}
            onChooseAnother={() => visitorFileRef.current?.click()}
            onDismiss={() => setSection('visitors', {})}
          />
          {canReplaceFrom('visitors') && !pendingReplace.visitors && (
            <button
              type="button"
              data-testid="v2-visitors-replace"
              className="mt-2 inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50"
              onClick={() => requestReplace('visitors')}
            >
              <Replace className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.replaceAllGuests')}
            </button>
          )}
          {replaceConfirm('visitors')}
          {personTable(
            'visitors',
            visitorFields.fields,
            i => removePersonRow('visitors', i),
            () => true,
            () => visitorFields.append(createEmptyMember())
          )}
          <button
            type="button"
            data-error-focus-target={visitorsListError ? 'true' : undefined}
            className="mt-4 inline-flex items-center justify-center gap-2 w-full sm:w-auto rounded-xl border-2 border-dashed border-[#004c91]/20 px-4 py-2 text-sm font-semibold text-[#004c91] transition-colors hover:bg-[#004c91]/5 disabled:opacity-40"
            disabled={visitorFields.fields.length >= V2_MAX_MEMBERS_PER_CAMPUS}
            onClick={() => visitorFields.append(createEmptyMember())}
          >
            <Plus className="h-4 w-4" /> {t('visitRequestV2:card.addVisitor')}
          </button>
        </fieldset>

        {/* Support team */}
        <fieldset>
          <legend className="mb-2 flex w-full flex-wrap items-center gap-2 text-sm font-extrabold text-slate-900">
            {t('visitRequestV2:card.supportTeam')}
            <span className="text-xs font-normal text-slate-400">
              {t('visitRequestV2:card.memberCount', { count: supportFields.fields.length, max: V2_MAX_MEMBERS_PER_CAMPUS })}
            </span>
            <span className="ml-auto flex items-center gap-2">
              <button
                type="button"
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50"
                onClick={() => downloadSupportTeamTemplate(excelT)}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.template')}
              </button>
              <button
                type="button"
                data-testid="v2-support-import"
                disabled={!!excelState.supportTeam.loadingFileName}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50 disabled:opacity-50"
                onClick={() => supportFileRef.current?.click()}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.importForCampus')}
              </button>
            </span>
          </legend>
          <input
            ref={supportFileRef}
            type="file"
            accept=".xlsx,.xls"
            className="hidden"
            aria-hidden
            onChange={e => {
              const file = e.target.files?.[0];
              if (file) void importExcel('supportTeam', file);
              e.target.value = '';
            }}
          />
          <ExcelImportPanel
            testId="v2-excel-support"
            state={excelState.supportTeam}
            kind="supportTeam"
            campusLabel={headerLabel}
            onChooseAnother={() => supportFileRef.current?.click()}
            onDismiss={() => setSection('supportTeam', {})}
          />
          {canReplaceFrom('supportTeam') && !pendingReplace.supportTeam && (
            <button
              type="button"
              data-testid="v2-support-replace"
              className="mt-2 inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50"
              onClick={() => requestReplace('supportTeam')}
            >
              <Replace className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.replaceAllSupport')}
            </button>
          )}
          {replaceConfirm('supportTeam')}
          {personTable(
            'supportTeam',
            supportFields.fields,
            i => removePersonRow('supportTeam', i),
            () => true,
            () => supportFields.append(createEmptyMember())
          )}
          {supportFields.fields.length > 0 && (
            <button
              type="button"
              className="mt-4 inline-flex items-center justify-center gap-2 w-full sm:w-auto rounded-xl border-2 border-dashed border-[#004c91]/20 px-4 py-2 text-sm font-semibold text-[#004c91] transition-colors hover:bg-[#004c91]/5 disabled:opacity-40"
              disabled={supportFields.fields.length >= V2_MAX_MEMBERS_PER_CAMPUS}
              onClick={() => supportFields.append(createEmptyMember())}
            >
              <Plus className="h-4 w-4" /> {t('visitRequestV2:card.addSupport')}
            </button>
          )}
        </fieldset>

        {/* ── One person, one member row (ID-02) ────────────────────────────────────────────
            Rendered under BOTH tables because that is the only place the conflict exists: each
            list on its own is fine, and it is the pair spanning them that would become two
            members, two guest_member_ids and two rows in the biên bản. Nothing is merged or
            deleted automatically — both answers are legitimate and only the user knows which. */}
        {memberDuplicates.length > 0 && (
          <div
            role="alert"
            data-testid={`campus-member-duplicates-${index}`}
            className="rounded-xl border border-amber-300 bg-amber-50 p-3"
          >
            <p className="text-sm font-bold text-amber-900">
              {t('visitRequestV2:card.duplicateMemberTitle')}
            </p>
            <ul className="mt-2 space-y-3">
              {memberDuplicates.map(pair => (
                <li key={pair.id} className="rounded-lg border border-amber-200 bg-white p-2.5">
                  <p className="text-xs font-normal text-amber-900">
                    {t(pair.crossList
                      ? 'visitRequestV2:card.duplicateMemberCross'
                      : 'visitRequestV2:card.duplicateMemberSameList', {
                      name: pair.first.fullName,
                      first: t(pair.first.kind === 'visitors'
                        ? 'visitRequestV2:card.contactPickKindGuest'
                        : 'visitRequestV2:card.contactPickKindSupport'),
                      second: t(pair.second.kind === 'visitors'
                        ? 'visitRequestV2:card.contactPickKindGuest'
                        : 'visitRequestV2:card.contactPickKindSupport'),
                      firstRow: pair.first.rowIndex + 1,
                      secondRow: pair.second.rowIndex + 1,
                    })}
                  </p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    <button
                      type="button"
                      data-testid={`campus-duplicate-drop-${index}`}
                      className="rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-amber-700"
                      onClick={() => dropDuplicateMember(pair)}
                    >
                      {t('visitRequestV2:card.duplicateActionDrop')}
                    </button>
                    {/* Only for a pair that spans the lists: within one list "chuyển sang hỗ trợ"
                        would answer a question nobody asked. */}
                    {pair.crossList && (
                      <button
                        type="button"
                        data-testid={`campus-duplicate-move-${index}`}
                        className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-bold text-amber-800 hover:bg-amber-100"
                        onClick={() => moveMemberToSupport(pair)}
                      >
                        {t('visitRequestV2:card.duplicateActionMove')}
                      </button>
                    )}
                    <button
                      type="button"
                      data-testid={`campus-duplicate-differentiate-${index}`}
                      className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-bold text-amber-800 hover:bg-amber-100"
                      onClick={() => focusDuplicateRow(pair)}
                    >
                      {t('visitRequestV2:card.duplicateActionDifferent')}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
            {/* Says why the third button does not simply dismiss this: two rows agreeing on every
                field the form collects have nothing left that could tell them apart, so the server
                refuses them — and "add the detail" is the answer that makes them two people. */}
            <p className="mt-2 text-xs text-amber-800">
              {t('visitRequestV2:card.duplicateMemberHint')}
            </p>
          </div>
        )}

        {/* Operational contact (per-campus working contact — a snapshot, never a login) */}
        <fieldset>
          <legend className="mb-2 flex w-full flex-wrap items-center gap-2 text-sm font-extrabold text-slate-900">
            <span className="flex items-center">
              {t('visitRequestV2:card.operationalContact')}
              <HelpTooltip content={t('visitRequestV2:card.operationalContactHint')} className="ml-1.5" />
            </span>
            {/* Both sources are offered because both happen: sometimes the registrant coordinates the
                campus themselves, sometimes the request's primary contact does, and they are often
                two different people. One button could only ever serve half the cases.
                Absent when the contact is read-only — copying into fields nobody can save is a button
                that does nothing. */}
            {!contactReadOnly && (
              <span className="flex flex-wrap items-center gap-2 sm:ml-auto">
                <button
                  type="button"
                  data-testid={`campus-opcontact-use-registrant-${index}`}
                  disabled={!canQuickFillFrom('registrant')}
                  onClick={() => requestQuickFill('registrant')}
                  className="inline-flex items-center gap-1.5 rounded-xl border-2 border-slate-200 bg-white px-3 sm:px-4 py-2 text-sm font-semibold text-slate-700 transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  <Copy className="h-4 w-4" /> {t('visitRequestV2:card.quickFillRegistrant')}
                </button>
              </span>
            )}
          </legend>

          {/* ── Read-only summary: an EXISTING campus's contact, shown but not editable here ──
              Five labelled values and nothing else. No input, no disabled input, and no button that
              jumps to it — a control here would be a second door into a workflow that has one, and
              the last version of this card had exactly that.
              Where the workflow does live is said in the legend's `?` tooltip rather than as a
              standing paragraph: it is guidance for the person who wonders, not a caption the same
              reader has to scroll past on every campus of every visit (plan §6). */}
          {contactReadOnly ? (
            <>
            <div
              data-testid={`campus-opcontact-readonly-${index}`}
              className="rounded-xl border border-slate-200 bg-slate-50 p-3"
            >
              <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2 lg:grid-cols-3">
                {([
                  ['fullName', t('visitRequestV2:person.fullName')],
                  ['organization', t('visitRequestV2:person.organization')],
                  ['jobTitle', t('visitRequestV2:person.jobTitle')],
                  ['phone', t('visitRequestV2:card.phone')],
                  ['email', t('visitRequestV2:card.email')],
                ] as const).map(([field, label]) => (
                  <div key={field} className="min-w-0">
                    <dt className="text-xs font-medium text-slate-500">{label}</dt>
                    <dd
                      className="break-words text-sm font-normal text-slate-900"
                      data-testid={`campus-opcontact-readonly-${field}-${index}`}
                    >
                      {watch(`${base}.operationalContact.${field}`) || '—'}
                    </dd>
                  </div>
                ))}
              </dl>
              {/* Informational, non-blocking (plan §12). A legacy snapshot predating the
                  Organization-required rule can hold blanks here; that is never this form's error to
                  raise — Phone is excluded because it has always been optional — and it must not count
                  toward the card's validation-error badge (see countFieldErrors: this block produces no
                  RHF error, only this notice). */}
              {legacyContactIncomplete && (
                <p
                  data-testid={`campus-opcontact-legacy-warning-${index}`}
                  role="status"
                  className="mt-2 text-xs font-normal text-amber-700"
                >
                  {t('visitRequestV2:card.legacyContactIncomplete')}
                </p>
              )}
            </div>

            {/* ── Liên kết với danh sách đoàn (plan CanhIter3FixBug) ────────────────────────
                A SEPARATE control from the read-only profile above: it answers "is the current
                contact also one of the people travelling with this delegation, and if so, which
                one" — it never touches the five fields above. Kept out of the general Amendment
                modal on purpose (plan §14) — that surface bundles schedule/purpose/members and
                bundling this pick there read as "change who the contact is". This is the SAME
                underlying relation as the create-mode picker above, on the same field
                (operationalContactClientMemberKey); only the mechanism differs by whether the
                member list this save touches is changing (ephemeral key, unaffected) or not
                (durable id, resolved from this same key at submit time — see toApiCampusVisit). */}
            <div className="mt-3 rounded-xl border border-slate-200 bg-slate-50/70 p-3">
              <div className="mb-1.5 flex items-center">
                <label
                  htmlFor={`campus-opcontact-relation-pick-${index}`}
                  className="block text-xs font-bold text-slate-700"
                >
                  {t('visitRequestV2:amend.contactPickLabel')}
                </label>
                <HelpTooltip
                  testId={`campus-opcontact-relation-pick-help-${index}`}
                  label={t('visitRequestV2:amend.contactPickLabel')}
                  content={t('visitRequestV2:amend.contactPickHint')}
                />
              </div>
              <select
                id={`campus-opcontact-relation-pick-${index}`}
                data-testid={`campus-opcontact-relation-pick-${index}`}
                value={contactMemberKey ?? ''}
                onChange={e => pickRelationOnly(e.target.value)}
                className={inputCls(false, contactMemberKey !== null, false)}
              >
                <option value="">{t('visitRequestV2:card.contactPickNone')}</option>
                {eligibleMembers.map(m => (
                  <option
                    key={m.key || `${m.kind}-${m.rowIndex}`}
                    value={m.key}
                    disabled={!m.complete}
                  >
                    {memberLabel(m)}
                  </option>
                ))}
              </select>
              {pickedMember && (
                <p
                  data-testid={`campus-opcontact-relation-picked-${index}`}
                  className="mt-1.5 text-xs text-slate-500"
                >
                  {t('visitRequestV2:card.contactRelationPickedNotice', { name: pickedMember.fullName })}
                </p>
              )}
            </div>
            </>
          ) : (
          <>
          {quickFilledFrom && (
            <p data-testid={`campus-opcontact-copied-${index}`} className="mb-2 text-xs font-normal text-slate-500">
              {t(quickFilledFrom === 'registrant'
                ? 'visitRequestV2:card.quickFillCopiedRegistrant'
                : 'visitRequestV2:card.quickFillCopiedContact')}{' '}
              {t('visitRequestV2:card.quickFillIndependent')}
            </p>
          )}

          {/* Replacing what is already there is confirmed, never silent (plan §13). */}
          {pendingQuickFill && (
            <div
              role="alertdialog"
              data-testid={`campus-opcontact-replace-confirm-${index}`}
              className="mb-3 rounded-xl border border-amber-300 bg-amber-50 p-3"
            >
              <p className="text-sm font-bold text-amber-900">
                {t('visitRequestV2:card.quickFillReplaceTitle')}
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                <button
                  type="button"
                  data-testid={`campus-opcontact-replace-yes-${index}`}
                  className="rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-amber-700"
                  onClick={() => applyQuickFill(pendingQuickFill)}
                >
                  {t('visitRequestV2:card.quickFillReplaceYes')}
                </button>
                <button
                  type="button"
                  className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-bold text-amber-800 hover:bg-amber-100"
                  onClick={() => setPendingQuickFill(null)}
                >
                  {t('visitRequestV2:common.cancel')}
                </button>
              </div>
            </div>
          )}

          {/* ── Đầu mối là ai trong đoàn? (NP-03) ────────────────────────────────────────────
              Picking here is what gives the contact a real identity instead of a name that has to
              be string-matched later. Everything below still edits freely — the pick fills the
              three shared fields and records WHO, it does not lock anything. */}
          <div className="mb-4 rounded-xl border border-slate-200 bg-slate-50/70 p-3">
            {/* The long explanation moved into the `?` (UX-01). It was four lines of standing text
                under the dropdown — read once, scrolled past on every campus of every visit after
                that — and it pushed the field it was explaining off the first screenful. */}
            <div className="mb-1.5 flex items-center">
              <label
                htmlFor={`campus-opcontact-pick-${index}`}
                className="block text-xs font-bold text-slate-700"
              >
                {t('visitRequestV2:card.contactPickLabel')}
              </label>
              <HelpTooltip
                testId={`campus-opcontact-pick-help-${index}`}
                label={t('visitRequestV2:card.contactPickLabel')}
                content={t('visitRequestV2:card.contactPickHint')}
              />
            </div>
            <select
              id={`campus-opcontact-pick-${index}`}
              data-testid={`campus-opcontact-pick-${index}`}
              value={contactMemberKey ?? ''}
              onChange={e => pickContactFromDelegation(e.target.value)}
              className={inputCls(false, contactMemberKey !== null, false)}
            >
              <option value="">{t('visitRequestV2:card.contactPickNone')}</option>
              {/* An unfinished row is LISTED but not selectable: hiding it would leave the user
                  looking for somebody they had definitely typed in, and enabling it would let the
                  campus's contact be a person with no name. The label says which it is. */}
              {eligibleMembers.map(m => (
                <option
                  key={m.key || `${m.kind}-${m.rowIndex}`}
                  value={m.key}
                  disabled={!m.complete}
                >
                  {memberLabel(m)}
                </option>
              ))}
            </select>
            {/* What is left under the control is the one consequence the user needs at the moment of
                choosing; the rest is in the `?` above. */}
            <p className="mt-1.5 text-xs text-slate-500">
              {t('visitRequestV2:card.contactPickMicrocopy')}
            </p>

            {/* What picking somebody MEANS, said where the consequence is visible: the three shared
                fields below stop being editable here and follow that person's row instead. Without
                this the read-only fields read as a bug. */}
            {pickedMember && (
              <div
                data-testid={`campus-opcontact-picked-${index}`}
                className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border border-slate-200 bg-white p-3"
              >
                <p className="min-w-0 flex-1 text-xs text-slate-600">
                  {t('visitRequestV2:card.contactPickedNotice', { name: pickedMember.fullName })}
                </p>
                <button
                  type="button"
                  data-testid={`campus-opcontact-edit-member-${index}`}
                  onClick={focusContactMemberRow}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-50"
                >
                  {t('visitRequestV2:card.contactEditMember')}
                </button>
              </div>
            )}

            {contactMatchesNoVisitor && (
              <div
                data-testid={`campus-opcontact-not-in-delegation-${index}`}
                className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3"
              >
                <p className="text-xs font-normal text-amber-900">
                  {t('visitRequestV2:card.contactNotInDelegation')}
                </p>
                <button
                  type="button"
                  data-testid={`campus-opcontact-add-to-delegation-${index}`}
                  onClick={addContactToDelegation}
                  className="mt-2 inline-flex items-center gap-1.5 rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-amber-700"
                >
                  <Plus className="h-3.5 w-3.5" />
                  {t('visitRequestV2:card.contactAddToDelegation')}
                </button>
              </div>
            )}
          </div>

          <div className="grid grid-cols-12 gap-x-3 xl:gap-x-4 gap-y-5">
            {/* ── The three fields a delegation row already answers ──────────────────────────
                Editable while the contact is somebody outside the delegation; READ-ONLY the moment a
                member is picked, because then the member row is the source and this is its copy.
                Leaving them editable is what let the form hold "Daniel Kim's id" beside a different
                person's name and job title — one record describing two people, with nothing to say
                which half was true. Correcting them is still possible, at the row itself, through
                "Sửa thông tin thành viên" above. */}
            {pickedMember ? (
              <FormField className="col-span-12 lg:col-span-2 xl:col-span-2" label={t('visitRequestV2:person.fullName')} required showValidIcon={false}>
                <p data-testid="campus-opcontact-name-readonly" className="break-words rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-normal text-slate-900">
                  {watchedContact?.fullName || '—'}
                </p>
              </FormField>
            ) : (
            <FormField className="col-span-12 lg:col-span-2 xl:col-span-2" label={t('visitRequestV2:person.fullName')} required error={fieldError('operationalContact.fullName')} showValidIcon={false}>
              <Controller
                name={`${base}.operationalContact.fullName`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextField
                    value={field.value ?? ''}
                    onChange={revalidatingChange(field)}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('operationalContact.fullName')}
                    maxLength={MAX.contactName}
                    ariaLabel={t('visitRequestV2:person.fullName')}
                    testId="campus-opcontact-name"
                  />
                )}
              />
            </FormField>
            )}
            {/* The same searchable organization control the guest rows use. The stored value is
                still plain text — this contact is a snapshot and the schema has no relation to a
                partner record, so picking a known organization links nothing and the request's own
                partner selection is untouched. When a member holds the role their organisation (and
                the partner behind it) is theirs, so this shows it rather than offering a second,
                contradictable copy. */}
            {pickedMember ? (
              <FormField className="col-span-12 lg:col-span-3 xl:col-span-3" label={t('visitRequestV2:person.organization')} required showValidIcon={false}>
                <p data-testid="campus-opcontact-org-readonly" className="break-words rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-normal text-slate-900">
                  {watchedContact?.organization || '—'}
                </p>
              </FormField>
            ) : (
            <FormField className="col-span-12 lg:col-span-3 xl:col-span-3" label={t('visitRequestV2:person.organization')} required error={fieldError('operationalContact.organization')} showValidIcon={false}>
              <Controller
                name={`${base}.operationalContact.organization`}
                control={control}
                render={({ field }) => (
                  <OrganizationCombobox
                    value={field.value ?? ''}
                    onChange={revalidatingChange(field)}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('operationalContact.organization')}
                    placeholder={t('visitRequestV2:person.organization')}
                    ariaLabel={t('visitRequestV2:person.organization')}
                    testId="campus-opcontact-org"
                  />
                )}
              />
            </FormField>
            )}
            {/* Required, like fullName/organization/email — Phone (right below) is the one exception
                in this block and stays optional. Job title tells the campus whether the person on the
                other end of the phone can settle a schedule or has to go and ask; every detail screen
                has always had a row for it, and the row was blank on every request because this field
                did not exist. */}
            {pickedMember ? (
              <FormField className="col-span-12 lg:col-span-2 xl:col-span-2" label={t('visitRequestV2:person.jobTitle')} required showValidIcon={false}>
                <p data-testid="campus-opcontact-jobtitle-readonly" className="break-words rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-normal text-slate-900">
                  {watchedContact?.jobTitle || '—'}
                </p>
              </FormField>
            ) : (
            <FormField className="col-span-12 lg:col-span-2 xl:col-span-2" label={t('visitRequestV2:person.jobTitle')} required error={fieldError('operationalContact.jobTitle')} showValidIcon={false}>
              <Controller
                name={`${base}.operationalContact.jobTitle`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextField
                    value={field.value ?? ''}
                    onChange={revalidatingChange(field)}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('operationalContact.jobTitle')}
                    maxLength={MAX.contactName}
                    ariaLabel={t('visitRequestV2:person.jobTitle')}
                    testId="campus-opcontact-jobtitle"
                  />
                )}
              />
            </FormField>
            )}
            <FormField className="col-span-12 lg:col-span-2 xl:col-span-2" label={t('visitRequestV2:card.phone')} error={fieldError('operationalContact.phone')} showValidIcon={false}>
              <PhoneField
                field={register(`${base}.operationalContact.phone`)}
                hasError={!!fieldError('operationalContact.phone')}
                error={fieldError('operationalContact.phone')}
                testId={`campus-opcontact-phone-${index}`}
              />
            </FormField>
            <FormField
              className="col-span-12 lg:col-span-3 xl:col-span-3"
              label={t('visitRequestV2:card.email')}
              required
              error={fieldError('operationalContact.email')}
              showValidIcon={false}
            >
              <input
                type="email"
                data-testid={`campus-opcontact-email-${index}`}
                {...register(`${base}.operationalContact.email`)}
                className={inputCls(!!fieldError('operationalContact.email'), false, false)}
              />
            </FormField>
          </div>
          </>
          )}
        </fieldset>

        {/* Additional requirements */}
        <fieldset className="mb-1">
          <legend className="mb-3 text-sm font-extrabold text-slate-900">{t('visitRequestV2:card.additional')}</legend>
          <div className="grid grid-cols-12 gap-x-4 xl:gap-x-6 gap-y-4">
            <FormField className="col-span-12 lg:col-span-6 xl:col-span-2" label={t('visitRequestV2:card.workingLanguage')} required error={fieldError('workingLanguage')} showValidIcon={false}>
              <select {...register(`${base}.workingLanguage`)} className={inputCls(false, false, false)}>
                <option value="VI">{t('visitRequestV2:card.languageVi')}</option>
                <option value="EN">{t('visitRequestV2:card.languageEn')}</option>
              </select>
            </FormField>
            <FormField
              className="col-span-12 lg:col-span-6 xl:col-span-2"
              label={
                <span className="inline-flex items-center gap-1">
                  {t('visitRequestV2:card.mediaConsent')}
                  <HelpTooltip content={t('visitRequestV2:card.mediaConsentTooltip')} />
                </span>
              }
              required
              error={fieldError('mediaConsentStatus')}
              showValidIcon={false}
            >
              <select {...register(`${base}.mediaConsentStatus`)} className={inputCls(false, false, false)}>
                <option value="AGREED">{t('visitRequestV2:card.mediaAgreed')}</option>
                <option value="DECLINED">{t('visitRequestV2:card.mediaDeclined')}</option>
              </select>
            </FormField>
            <FormField className="col-span-12 lg:col-span-6 xl:col-span-3" label={t('visitRequestV2:card.transportationNote')} error={fieldError('transportationNote')} showValidIcon={false}>
              <Controller
                name={`${base}.transportationNote`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextField
                    value={field.value ?? ''}
                    onChange={revalidatingChange(field)}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('transportationNote')}
                    maxLength={MAX.transportationNote}
                    ariaLabel={t('visitRequestV2:card.transportationNote')}
                  />
                )}
              />
            </FormField>
            <FormField className="col-span-12 lg:col-span-6 xl:col-span-5" label={t('visitRequestV2:card.notes')} error={fieldError('notes')} showValidIcon={false}>
              <Controller
                name={`${base}.notes`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextField
                    value={field.value ?? ''}
                    onChange={revalidatingChange(field)}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('notes')}
                    maxLength={MAX.notes}
                    ariaLabel={t('visitRequestV2:card.notes')}
                  />
                )}
              />
            </FormField>
          </div>

          {/* Who processes THIS campus — authenticated create only; absent for public submit. */}
          {processing && (
            <CampusHostSelectionV2Panel
              campusCode={campusCode}
              role={processing.role}
              ownCampusCode={processing.ownCampusCode}
              startDatetime={watch(`${base}.startDatetime`)}
              endDatetime={watch(`${base}.endDatetime`)}
              value={processing.values[(campusCode || '').toUpperCase()]}
              onChange={processing.onChange}
            />
          )}
        </fieldset>
      </div>
    </div>
  );
};
