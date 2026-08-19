/**
 * Single shared parse of the backend's `metadataJson` (`{"eventKey":"...","params":{...}}`).
 *
 * Both `resolveNotificationPresentation` (VI/EN sentence) and `resolveNotificationDestination`
 * (where a click should go) need the same `eventKey`/`params` pair — reading it independently in
 * two places is how the two readings could drift (one recognizing an eventKey the other doesn't).
 * This file is the only thing that touches `JSON.parse` on a notification row; everything else goes
 * through it. See docs/CanhIter3FixBug/GopYCQuyen/PEMS_Notification_Second_Click_Semantic_Routing_Full_Fix_Plan.md §8.
 */

export type ParsedNotificationSemantic = {
  eventKey: string;
  params: Record<string, unknown>;
};

export function parseNotificationSemantic(
  metadataJson?: string | null,
): ParsedNotificationSemantic | null {
  if (!metadataJson) return null;
  try {
    const parsed = JSON.parse(metadataJson) as { eventKey?: string; params?: Record<string, unknown> };
    if (!parsed.eventKey) return null;
    return { eventKey: parsed.eventKey, params: parsed.params ?? {} };
  } catch {
    return null;
  }
}
