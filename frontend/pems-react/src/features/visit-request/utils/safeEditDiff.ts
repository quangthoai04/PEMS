import type { ResolvedVisitForm, SafeEditPayload } from '../api/visitRequestV2Api';

/** The registrant fields the safe-edit form manages. */
export interface SafeEditRegistrantDraft {
  fullName: string;
  organization: string;
  jobTitle: string;
  phone: string;
}

/** The primary-contact fields the safe-edit form manages. */
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
  transportationNote: string;
  noteToFptu: string;
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
  contact: SafeEditContactDraft,
  instances: SafeEditInstanceDraft[],
): SafeEditPayload | null {
  const payload: SafeEditPayload = {
    expectedRequestRowVersion: form.rowVersion,
    registrant: null,
    contact: null,
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

  // ── Contact. Same reasoning: name and phone are both required, so both travel together. ──
  const contactChanged =
    changed(form.primaryContact.fullName, contact.fullName)
    || changed(form.primaryContact.organization, contact.organization)
    || changed(form.primaryContact.phone, contact.phone);
  if (contactChanged) {
    payload.contact = {
      fullName: norm(contact.fullName),
      organization: norm(contact.organization) || null,
      phone: norm(contact.phone),
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
    if (changed(current.noteToFptu, draft.noteToFptu)) {
      patch.noteToFptu = norm(draft.noteToFptu);
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

  const nothingChanged =
    payload.registrant === null && payload.contact === null && payload.instances!.length === 0;
  return nothingChanged ? null : payload;
}
