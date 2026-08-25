import { Navigate } from 'react-router-dom';
import { useAuth } from '../../shared/hooks/useAuth';
import { FullScreenLoader } from '../../shared/auth/ProtectedRoute';
import { canCreateVisitRequestV2 } from '../../shared/auth/visitRequestV2Access';
import VisitRequestV2Page from './VisitRequestV2Page';

/**
 * Auth-aware entry for the public-facing `/visit-registration/v2` URL — kept as a real route
 * (external links, QR, bookmarks, email, the public homepage) rather than folded into
 * `/visit/create-v2`, and NEVER redirected away just because the visitor turns out to be signed in.
 *
 * Resolves to exactly one of three branches, mirroring the backend's own actor-role guard
 * (CreateVisitRequestV2CommandHandler: Visitor / Staff / Staff Leader only, else ForbiddenException
 * "Vai trò của bạn không được tạo đoàn khách."):
 *   - anonymous                        -> the unchanged public OTP form;
 *   - VISITOR / STAFF / STAFF_LEADER   -> the authenticated self-registration form, same shell the
 *                                         dashboard and homepage CTA already open;
 *   - any other signed-in role         -> the same /403 (or /invalid-account) every other guarded
 *                                         area in the app already uses.
 *
 * A denied account is NEVER handed the public form as a fallback — that would let a role the
 * backend refuses bypass its own denial by pretending to be nobody. And while auth bootstrap is
 * still resolving, nothing renders except the shared loading shell — guessing "public" here would
 * flash an editable Registrant + OTP UI at someone who turns out to be signed in.
 */
export default function VisitRequestV2EntryRoute() {
  const { isAuthenticated, isLoading, isReady, effectiveRole } = useAuth();

  if (isLoading || !isReady) {
    return <FullScreenLoader />;
  }

  if (!isAuthenticated) {
    return <VisitRequestV2Page mode="public" />;
  }

  if (!effectiveRole) {
    return <Navigate to="/invalid-account" replace />;
  }

  if (!canCreateVisitRequestV2(effectiveRole)) {
    return <Navigate to="/403" replace />;
  }

  return <VisitRequestV2Page mode="authenticated" />;
}
