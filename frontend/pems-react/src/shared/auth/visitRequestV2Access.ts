import type { EffectiveRole } from './resolveEffectiveRole';

/**
 * The only three actor types allowed to open Visit Request V2's AUTHENTICATED self-registration
 * create flow. Mirrors the backend guard exactly (CreateVisitRequestV2CommandHandler.cs:
 * `isVisitor || isRegularStaff || isStaffLeader`, else `ForbiddenException("Vai trò của bạn không
 * được tạo đoàn khách.")`) — this list must never grant a role the backend would still reject.
 *
 * Every entry point that can open the authenticated create form while signed in — the homepage/
 * FAQ/Partners CTA (`useVisitEntryCta`), the standalone `/visit-registration/v2` entry route, the
 * `/visit/create-v2` route guard, the dashboard button — reads this ONE list, so none of them can
 * drift from each other or from what the backend actually allows.
 */
export const VISIT_REQUEST_V2_CREATE_ROLES: readonly EffectiveRole[] = ['VISITOR', 'STAFF', 'STAFF_LEADER'];

/** True only for the three roles above. `null`/`undefined` (anonymous, or an unmappable account) is denied. */
export function canCreateVisitRequestV2(role: EffectiveRole | null | undefined): boolean {
  return !!role && VISIT_REQUEST_V2_CREATE_ROLES.includes(role);
}
