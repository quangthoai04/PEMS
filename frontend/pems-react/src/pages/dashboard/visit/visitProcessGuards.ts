/**
 * Pure guards for the Visit Process prep panel. They encode a single Phase 4.5 rule: a dependency
 * whose load FAILED must never be treated as "empty" and must block the action that consumes it, so a
 * mutation is never sent built on default/blank data.
 */

/**
 * Reminders Save/Cancel. The saved schedule is loaded separately; if that load failed we do NOT know
 * the real schedule, so saving would overwrite it with the UI's defaults. Block until a reload
 * succeeds. `busy` covers the in-flight submit.
 */
export function canSubmitReminders(args: {
  canConfigurePrep: boolean;
  remindersLoadFailed: boolean;
  busy: boolean;
}): boolean {
  return args.canConfigurePrep && !args.remindersLoadFailed && !args.busy;
}

/**
 * Assigning a responsible person on an agenda row. The candidate list is loaded separately; a failed
 * load yields an empty dropdown that must NOT be read as "no candidates exist". Block assignment
 * (and, by extension, saving an agenda that needs an assignee) until the candidates reload.
 */
export function canAssignResponsible(args: {
  canEditAgenda: boolean;
  candidatesLoadFailed: boolean;
}): boolean {
  return args.canEditAgenda && !args.candidatesLoadFailed;
}

/**
 * Whether the "no supporting candidates — add an agenda item instead" hint is truthful. It is only
 * truthful when the candidate list actually LOADED and is empty; on a failed load the emptiness is an
 * artifact of the failure, so the hint must be suppressed in favour of a retry.
 */
export function candidatesAreGenuinelyEmpty(args: {
  candidatesLoadFailed: boolean;
  supportingCandidateCount: number;
}): boolean {
  return !args.candidatesLoadFailed && args.supportingCandidateCount === 0;
}
