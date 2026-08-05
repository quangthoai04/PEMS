/**
 * Builds the `scopeKey` a preview token is bound to.
 *
 * The backend recomputes the same string from the ids the SEND command was itself given, and refuses a
 * token whose scope does not match. That is what stops a message approved for one invitee being sent,
 * word for word, to a different one.
 *
 * Both sides therefore have to spell it identically, and they live in different codebases — so it is
 * built here, once, rather than concatenated at each call site. The backend half is
 * `EmailPreviewFingerprint.Scope`: `name:value` pairs joined by `|`, in declaration order, with null
 * parts SKIPPED rather than written as "null".
 */
export function emailScopeKey(
  ...parts: [name: string, value: number | string | null | undefined][]
): string {
  return parts
    .filter(([, value]) => value !== null && value !== undefined && value !== '')
    .map(([name, value]) => `${name}:${value}`)
    .join('|');
}

/** `visitInstance:{id}|participant:{userId}` — the invitations and the department-staff assignment. */
export function participantScopeKey(
  visitInstanceId: number | null | undefined,
  participantUserId: number | null | undefined,
): string {
  return emailScopeKey(['visitInstance', visitInstanceId], ['participant', participantUserId]);
}

/** `visitInstance:{id}|department:{id}` — a logistics request going to a department. */
export function logisticsRequestScopeKey(
  visitInstanceId: number | null | undefined,
  departmentId: number | null | undefined,
): string {
  return emailScopeKey(['visitInstance', visitInstanceId], ['department', departmentId]);
}

/** `logisticsItem:{id}|assignee:{userId}` — a Department Leader handing an item to their staff. */
export function logisticsAssigneeScopeKey(
  logisticsItemId: number | null | undefined,
  assigneeUserId: number | null | undefined,
): string {
  return emailScopeKey(['logisticsItem', logisticsItemId], ['assignee', assigneeUserId]);
}
