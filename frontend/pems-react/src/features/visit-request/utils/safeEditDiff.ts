import type { ResolvedVisitForm, SafeEditPayload } from '../api/visitRequestV2Api';

/** The registrant fields the safe-edit form manages. */
export interface SafeEditRegistrantDraft {
  fullName: string;
  nationality: string;
  organization: string;
  jobTitle: string;
  phone: string;
  /** Partner profile the organization was picked from, or null for free text. */
  partnerId: number | null;
}

/**
 * One campus row in the safe-edit form.
 *
 * Same-person operational-contact metadata + relation live here now (plan CanhIter3FixBug) — email is
 * deliberately NOT part of this draft; it is rendered read-only from the campus's current contact and
 * echoed straight into the payload at submit time, never held as editable state.
 */
export interface SafeEditInstanceDraft {
  visitInstanceId: number;
  expectedRowVersion: number;
  campusName: string;
  transportationNote: string;
  mediaConsentStatus: string;
  /** "Ghi chú gửi FPTU" — one general remark per campus, independent of media consent. */
  notes: string;
  contactFullName: string;
  contactOrganization: string;
  contactJobTitle: string;
  contactPhone: string;
  /** Which delegation member the contact IS, or null for "not in the delegation". */
  contactGuestMemberId: number | null;
}

/** Trimmed text, with empty treated the same as absent — "  " and null mean the same thing here. */
const norm = (value: string | null | undefined): string => (value ?? '').trim();

/** True when the user actually changed this field. */
const changed = (before: string | null | undefined, after: string): boolean => norm(before) !== norm(after);

/**
 * Builds a SPARSE safe-edit payload: only the fields the user touched, and only the campuses that
 * have at least one touched field.
 *
 * The modal previously sent everything it had loaded. Two things went wrong with that. A user
 * correcting one campus's note also re-submitted the media-consent decision and notes of every other
 * campus — so a campus that had moved inside its window was dragged into the request and the backend
 * refused the whole edit, for a campus the user never touched. And because the form held whatever was
 * loaded when it opened, any value changed server-side in the meantime was quietly written back to
 * its old state.
 *
 * Returns null when nothing changed at all, so the caller can say so instead of sending an empty
 * request and surfacing the backend's "không có thay đổi nào" as if it were a failure.
 */
export function buildChangedOnlyPayload(
  form: ResolvedVisitForm,
  registrant: SafeEditRegistrantDraft,
  instances: SafeEditInstanceDraft[],
  canEditShared: boolean,
): SafeEditPayload | null {
  const payload: SafeEditPayload = {
    expectedRequestRowVersion: form.rowVersion,
    registrant: null,
    instances: [],
  };

  // ── Registrant. Full name is required by the backend, so once ANY registrant field changed the
  //    block carries the name too — otherwise the patch would look like a request to blank it.
  //    partnerId travels ATOMICALLY with organization — never sent independently — so the text and
  //    the id it points at can never be applied as two separate patches.
  //
  //    `canEditShared` is checked HERE too, not only by the disabled fieldset the modal renders — the
  //    request-level capability is the backend's own verdict, and this function must refuse to build a
  //    Registrant patch on its behalf regardless of how `registrant` state got here (a disabled control
  //    that lets a click leak through, a future field added without wiring its own disabled prop). A
  //    diff is not even computed when the shared block is locked, so a stale/mismatched `registrant`
  //    draft can never surface as a patch either. ──
  const registrantChanged =
    canEditShared
    && (changed(form.registrant.fullName, registrant.fullName)
      || changed(form.registrant.nationality, registrant.nationality)
      || changed(form.registrant.organization, registrant.organization)
      || changed(form.registrant.jobTitle, registrant.jobTitle)
      || changed(form.registrant.phone, registrant.phone)
      || (form.partnerId ?? null) !== (registrant.partnerId ?? null));
  if (registrantChanged) {
    payload.registrant = {
      fullName: norm(registrant.fullName),
      nationality: norm(registrant.nationality),
      organization: norm(registrant.organization) || null,
      jobTitle: norm(registrant.jobTitle) || null,
      phone: norm(registrant.phone) || null,
      partnerId: registrant.partnerId,
    };
  }

  // ── Campuses. Each field is sent ONLY if it changed; a campus with no changes is left out. ──
  for (const draft of instances) {
    const current = form.campusVisits.find(c => c.visitInstanceId === draft.visitInstanceId);
    if (!current) continue;

    const patch: NonNullable<SafeEditPayload['instances']>[number] = {
      visitInstanceId: draft.visitInstanceId,
      expectedRowVersion: draft.expectedRowVersion,
    };
    let touched = false;

    if (changed(current.transportationNote, draft.transportationNote)) {
      // "" rather than null: null means "not part of this edit", so clearing a field has to be an
      // explicit empty string.
      patch.transportationNote = norm(draft.transportationNote);
      touched = true;
    }
    if (changed(current.notes, draft.notes)) {
      patch.notes = norm(draft.notes);
      touched = true;
    }
    if (norm(current.mediaConsentStatus) !== norm(draft.mediaConsentStatus)) {
      patch.mediaConsentStatus = norm(draft.mediaConsentStatus);
      touched = true;
    }

    // ── Same-person contact metadata + relation (plan CanhIter3FixBug) — a SEPARATE sparse sub-patch,
    //    omitted entirely when neither metadata nor relation changed. Email is never read from the
    //    draft: always echoed from `current` so the backend can prove identity is unchanged. ──
    const metadataChanged =
      changed(current.operationalContact.fullName, draft.contactFullName)
      || changed(current.operationalContact.organization, draft.contactOrganization)
      || changed(current.operationalContact.jobTitle, draft.contactJobTitle)
      || changed(current.operationalContact.phone, draft.contactPhone);
    const relationChanged =
      (current.operationalContact.guestMemberId ?? null) !== (draft.contactGuestMemberId ?? null);
    if (metadataChanged || relationChanged) {
      patch.operationalContact = {
        fullName: norm(draft.contactFullName),
        organization: norm(draft.contactOrganization) || null,
        jobTitle: norm(draft.contactJobTitle),
        phone: norm(draft.contactPhone) || null,
        email: current.operationalContact.email,
        // Tri-state: the wrapper itself is OMITTED (not { guestMemberId: null }) unless the relation
        // actually changed — "don't touch relation" must stay distinguishable from "explicit unlink".
        ...(relationChanged ? { memberLink: { guestMemberId: draft.contactGuestMemberId ?? null } } : {}),
      };
      touched = true;
    }

    if (touched) payload.instances!.push(patch);
  }

  const nothingChanged = payload.registrant === null && payload.instances!.length === 0;
  return nothingChanged ? null : payload;
}
