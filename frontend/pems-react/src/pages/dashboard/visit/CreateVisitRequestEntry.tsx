import { Navigate } from 'react-router-dom';
import { usePerCampusV2Capability } from '../../../shared/features/perCampusV2Capability';
import { V2_AUTHENTICATED_CREATE_PATH } from '../../../shared/features/perCampusV2Entry';

/**
 * Compatibility entry for the legacy `/dashboard/visit/create` URL. The old prototype page here only
 * navigated away without ever submitting, so this replaces it with a version-aware redirect:
 *   • v2 capability enabled → the real v2 create page;
 *   • otherwise            → the visit management screen, whose "Tạo đoàn khách" button opens the
 *                            v1 authenticated create popup (fail-safe to v1 while loading / on error).
 * The v1 flow is unchanged when the capability is OFF.
 */
export function CreateVisitRequestEntry() {
  const { status, enabled } = usePerCampusV2Capability();

  if (status === 'loading') {
    return (
      <div className="flex min-h-[40vh] items-center justify-center" role="status" aria-live="polite">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-[#004c91] border-t-transparent" />
        <span className="sr-only">Đang tải…</span>
      </div>
    );
  }

  if (enabled) {
    return <Navigate to={V2_AUTHENTICATED_CREATE_PATH} replace />;
  }

  return <Navigate to="/dashboard/visit" replace />;
}
