import { Link, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import VisitRequestV2DetailView from '../../../features/visit-request/components/v2/VisitRequestV2DetailView';

/**
 * Dashboard route `/dashboard/visit/v2/:visitRequestId` — the per-campus v2 detail screen.
 * Reached from the request lists and from the legacy-edit guidance when a v1 flow answers
 * FORM_VERSION_UPGRADE_REQUIRED. Server-side flags OFF ⇒ the view shows its own not-found
 * state; nothing falls back to v1 silently.
 */
export default function VisitRequestV2DetailPage() {
  const { visitRequestId } = useParams<{ visitRequestId: string }>();
  const { t } = useTranslation(['visitRequestV2']);
  const id = Number(visitRequestId);

  return (
    <div className="mx-auto max-w-7xl space-y-4 p-4 sm:p-6">
      <Link
        to="/dashboard/visit"
        className="inline-flex items-center gap-1.5 text-sm font-semibold text-[#004c91] hover:underline"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden /> {t('visitRequestV2:detail.backToList')}
      </Link>
      {Number.isFinite(id) && id > 0 ? (
        <VisitRequestV2DetailView visitRequestId={id} />
      ) : (
        <p role="alert" className="text-sm text-red-600">{t('visitRequestV2:detail.notfound')}</p>
      )}
    </div>
  );
}
