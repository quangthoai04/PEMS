import React from 'react';
import { ProtectedRoute } from './ProtectedRoute';
import type { DashboardRouteKey } from './dashboardRouteAccess';

interface RouteAccessGuardProps {
  routeKey: DashboardRouteKey;
  children: React.ReactNode;
}

/**
 * Wraps a dashboard screen with the policy declared for its route key.
 *
 *   <RouteAccessGuard routeKey="CAMPUS_LIST">
 *     <CampusManagement />
 *   </RouteAccessGuard>
 *
 * `routeKey` is required, which is the point: a dashboard route can no longer be added
 * with a bare <ProtectedRoute> that only checks "is signed in". That was how
 * /dashboard/campus stayed open to every authenticated role — the route was guarded, but
 * the guard had nothing to enforce.
 *
 * Children are not rendered until the check passes, so a denied screen never mounts and
 * never fires its data fetch.
 */
export function RouteAccessGuard({ routeKey, children }: RouteAccessGuardProps) {
  return <ProtectedRoute routeKey={routeKey}>{children}</ProtectedRoute>;
}

export default RouteAccessGuard;
