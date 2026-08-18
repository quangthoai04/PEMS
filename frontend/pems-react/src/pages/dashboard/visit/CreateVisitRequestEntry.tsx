import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { usePerCampusV2Capability } from '../../../shared/features/perCampusV2Capability';
import { V2_AUTHENTICATED_CREATE_PATH } from '../../../shared/features/perCampusV2Entry';
import { useAuth } from '../../../shared/hooks/useAuth';

/**
 * Compatibility entry for the legacy `/dashboard/visit/create` URL. The old prototype page here only
 * navigated away without ever submitting, so this replaces it with a version-aware redirect:
 *   • v2 capability enabled → the real v2 create page;
 *   • real backend OFF      → the visit management screen (its "Tạo đoàn khách" button opens the v1
 *                             authenticated create popup);
 *   • capability FETCH FAILED → an explicit error with a Retry — never a silent downgrade to v1.
 */
export function CreateVisitRequestEntry() {
  const { status, enabled, retry } = usePerCampusV2Capability();
  const { effectiveRole } = useAuth();
  const { t, i18n } = useTranslation('visitTasks');
  const isVisitor = effectiveRole === 'VISITOR';
  const tt = (key: string) => t(key, { lng: isVisitor ? i18n.language : 'vi' });

  if (status === 'loading') {
    return (
      <div className="flex min-h-[40vh] items-center justify-center" role="status" aria-live="polite">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-[#004c91] border-t-transparent" />
        <span className="sr-only">{tt('createEntry.loading')}</span>
      </div>
    );
  }

  if (status === 'error') {
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center gap-3 text-center" role="alert">
        <p className="text-sm font-normal text-slate-700">
          {tt('createEntry.capabilityError')}
        </p>
        <button
          type="button"
          onClick={retry}
          className="rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white hover:bg-[#003a6f]"
        >
          {tt('createEntry.retry')}
        </button>
      </div>
    );
  }

  if (enabled) {
    return <Navigate to={V2_AUTHENTICATED_CREATE_PATH} replace />;
  }

  return <Navigate to="/dashboard/visit" replace />;
}
