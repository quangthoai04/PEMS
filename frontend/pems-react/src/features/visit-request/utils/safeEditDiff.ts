import type { ResolvedVisitForm, SafeEditPayload } from '../api/visitRequestV2Api';

/** The registrant fields the safe-edit form manages. */
export interface SafeEditRegistrantDraft {
  fullName: string;
  organization: string;
  jobTitle: string;
  phone: string;
}

/**
 * The display half of ONE campus's operational-contact snapshot, as the safe-edit form manages it.
 * Email is deliberately absent: it is what an invitation binds to, so changing it is a replace or a
 * transfer — a re-confirmation, not a typo fix.
 */
export interface SafeEditContactDraft {
  fullName: string;
  organization: string;
  phone: string;
}

/** One campus row in the safe-edit form. */
export interface SafeEditInstanceDraft {
  visitInstanceId: number;
  expectedRowVersion: number;
  campusName: string;
  /** This campus's own contact snapshot — never shared with a sibling campus. */
  contact: SafeEditContactDraft;
  transportationNote: string;
  mediaConsentStatus: string;
  mediaConsentNote: string;
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
): SafeEditPayload | null {
  const payload: SafeEditPayload = {
    expectedRequestRowVersion: form.rowVersion,
    registrant: null,
    instances: [],
  };

  // ── Registrant. Full name is required by the backend, so once ANY registrant field changed the
  //    block carries the name too — otherwise the patch would look like a request to blank it. ──
  const registrantChanged =
    changed(form.registrant.fullName, registrant.fullName)
    || changed(form.registrant.organization, registrant.organization)
    || changed(form.registrant.jobTitle, registrant.jobTitle)
    || changed(form.registrant.phone, registrant.phone);
  if (registrantChanged) {
    payload.registrant = {
      fullName: norm(registrant.fullName),
      organization: norm(registrant.organization) || null,
      jobTitle: norm(registrant.jobTitle) || null,
      phone: norm(registrant.phone) || null,
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
    // Contact: name and phone are both required by the backend, so once ANY of the three changed
    // the block carries all of them — a partial patch would read as a request to blank the rest.
    const contactChanged =
      changed(current.operationalContact.fullName, draft.contact.fullName)
      || changed(current.operationalContact.organization, draft.contact.organization)
      || changed(current.operationalContact.phone, draft.contact.phone);
    if (contactChanged) {
      patch.operationalContact = {
        fullName: norm(draft.contact.fullName),
        organization: norm(draft.contact.organization) || null,
        phone: norm(draft.contact.phone),
      };
      touched = true;
    }
    if (changed(current.mediaConsentNote, draft.mediaConsentNote)) {
      patch.mediaConsentNote = norm(draft.mediaConsentNote);
      touched = true;
    }
    if (norm(current.mediaConsentStatus) !== norm(draft.mediaConsentStatus)) {
      patch.mediaConsentStatus = norm(draft.mediaConsentStatus);
      touched = true;
    }

    if (touched) payload.instances!.push(patch);
  }

  const nothingChanged = payload.registrant === null && payload.instances!.length === 0;
  return nothingChanged ? null : payload;
}
