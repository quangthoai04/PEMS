/**
 * What the shared editor is allowed to do, per mode (V4 §5.1, §5.4).
 *
 * One component serves two screens that differ in exactly one respect: who is writing. An administrator
 * authoring a TEMPLATE writes the wording every future recipient will read, so they may place variables
 * and decide where the system's action area sits. A sender in COMPOSE is writing one message to one
 * person from an already-substituted draft, so the variables are gone — resolved to real values before
 * they ever saw the text — and there is nothing to place, only something to move.
 *
 * The distinction is expressed as capabilities rather than as `if (mode === 'TEMPLATE')` scattered through
 * the component, because the interesting cases are not the modes but the individual permissions: "may
 * move the action block" and "may delete the action block" are different questions with different
 * answers, and a template that has no action area at all answers both with no.
 */

export type EmailEditorMode = 'TEMPLATE' | 'COMPOSE';

export interface EmailEditorCapabilities {
  /** May insert `{{variable}}` chips. Template authoring only — COMPOSE text is already substituted. */
  allowVariables: boolean;
  allowImages: boolean;
  allowLinks: boolean;
  /** May place a NEW system action node. Only an author of the template decides an action area exists. */
  allowSystemBlockInsert: boolean;
  /** May drag the existing action node elsewhere. This is the COMPOSE freedom (§9.3). */
  allowSystemBlockMove: boolean;
  /** May remove the action node entirely. Refused where the message IS the action. */
  allowSystemBlockDelete: boolean;
  /** May insert/edit an atomic table. `false` inside the callout-content mini-editor only — a table
   * nested inside an atomic callout embed is untested surface no product requirement asks for. */
  allowTables: boolean;
  /** May insert a divider. Same reasoning and same one exception as `allowTables`. */
  allowDividers: boolean;
  /**
   * Whether an existing styled `<div>` container round-trips as the atomic `pemsEmailCallout` embed at
   * all (the callout frames plan's fidelity fix). `true` for both real editing screens — an existing
   * frame must always survive their own load/save cycle. `false` only for the callout-content
   * mini-editor's own inner fragment, so that fragment's conversion can never manufacture a NESTED
   * callout from a stray styled div (there is no legitimate reason for one to exist there, and no
   * shipped template ever produces one).
   */
  preserveCallouts: boolean;
  /**
   * Whether this editor instance may itself manage frames — Add/Edit-content/Change-type/Remove. `true`
   * for both TEMPLATE and COMPOSE main editors (an operator may reshape presentation in either stage).
   * `false` only for the callout-content mini-editor, which is what actually prevents nesting: it may
   * edit what is already inside a callout, but may never wrap, retype, or unwrap a frame of its own.
   */
  allowCalloutAuthoring: boolean;
  /**
   * Always false. Present as a field rather than omitted so that "can the user write raw HTML?" has an
   * explicit, greppable answer of no (V4 §5.4, §6.4) instead of being an absence somebody might read as
   * an oversight and add later.
   */
  allowRawHtml: false;
}

/**
 * TEMPLATE: the administrator writes wording for every future send, so they place variables and decide
 * where the action area goes. Deleting it is theirs too — a template that should carry no buttons is a
 * legitimate editorial choice, and the backend refuses the combination that would break a send.
 */
export const TEMPLATE_CAPABILITIES: EmailEditorCapabilities = {
  allowVariables: true,
  allowImages: true,
  allowLinks: true,
  allowSystemBlockInsert: true,
  allowSystemBlockMove: true,
  allowSystemBlockDelete: true,
  allowTables: true,
  allowDividers: true,
  preserveCallouts: true,
  allowCalloutAuthoring: true,
  allowRawHtml: false,
};

/**
 * COMPOSE: the sender rewrites one already-substituted message. No variables (there is nothing left to
 * substitute), no new action areas (the flow decides whether this message has one), but full freedom to
 * move the one that is there — which is the whole point of §9.3.
 *
 * Deleting is withheld by default because on the templates that reach this mode the buttons ARE the
 * message: an invitation whose accept link the sender removed is a mail asking for a reply it gives no
 * way to send. Callers whose flow genuinely permits it pass their own capabilities.
 */
export const COMPOSE_CAPABILITIES: EmailEditorCapabilities = {
  allowVariables: false,
  allowImages: true,
  allowLinks: true,
  allowSystemBlockInsert: false,
  allowSystemBlockMove: true,
  allowSystemBlockDelete: false,
  allowTables: true,
  allowDividers: true,
  preserveCallouts: true,
  allowCalloutAuthoring: true,
  allowRawHtml: false,
};

export function capabilitiesFor(mode: EmailEditorMode): EmailEditorCapabilities {
  return mode === 'TEMPLATE' ? TEMPLATE_CAPABILITIES : COMPOSE_CAPABILITIES;
}
