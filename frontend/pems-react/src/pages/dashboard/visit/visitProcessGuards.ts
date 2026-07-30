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
