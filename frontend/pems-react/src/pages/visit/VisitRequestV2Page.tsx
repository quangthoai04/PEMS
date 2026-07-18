import { useState } from 'react';
import { Link } from 'react-router-dom';
import { CheckCircle2, Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { VisitRequestFormV2 } from '../../features/visit-request/components/v2/VisitRequestFormV2';
import type { V2CreateResponse } from '../../features/visit-request/api/visitRequestV2Api';
import type { VisitRequestV2Schema } from '../../features/visit-request/schema/visitRequestV2.schema';
import { useAuth } from '../../shared/hooks/useAuth';

interface Props {
  /** 'public' → OTP flow (anonymous); 'authenticated' → direct create with the JWT session. */
  mode: 'public' | 'authenticated';
}

/**
 * Per-campus form v2 page. Hosted on its OWN routes (`/visit-registration/v2`,
 * `/visit/create-v2`) so the v1 flow is untouched: both server-side v2 flags default OFF,
 * in which case the backend answers 404 and the form surfaces that honestly — the page
 * never falls back to the v1 endpoints by itself.
 */
export default function VisitRequestV2Page({ mode }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const { user } = useAuth();
  const [result, setResult] = useState<{ response: V2CreateResponse; values: VisitRequestV2Schema } | null>(null);

  const draftNamespace = mode === 'authenticated' ? (user?.email ?? undefined) : undefined;

  if (result) {
    const { response } = result;
    return (
      <div className="mx-auto max-w-3xl px-4 py-10">
        <div className="rounded-2xl border border-green-200 bg-green-50 p-6">
          <div className="flex items-center gap-3">
            <CheckCircle2 className="h-8 w-8 shrink-0 text-green-600" />
            <div>
              <h1 className="text-lg font-extrabold text-green-900">{t('visitRequestV2:success.title')}</h1>
              <p className="text-sm text-green-800">
                {t('visitRequestV2:success.requestCode', { code: response.requestCode })}
              </p>
            </div>
          </div>
          <ul className="mt-4 space-y-1 text-sm text-green-900">
            <li>
              {t('visitRequestV2:success.campusCount', { count: response.instances.length })}
              {response.hasMixedCampusDetails ? ` — ${t('visitRequestV2:success.mixedNote')}` : ''}
            </li>
            {response.idempotent && <li>{t('visitRequestV2:success.idempotentReplay')}</li>}
          </ul>
          {response.contactClaimPending && (
            <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800" role="status">
              <Info className="mt-0.5 h-4 w-4 shrink-0" />
              <p>{t('visitRequestV2:success.claimPending')}</p>
            </div>
          )}
          <div className="mt-6">
            <Link to="/" className="text-sm font-bold text-[#004c91] hover:underline">
              {t('visitRequestV2:success.backHome')}
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-10">
      <header className="mb-8">
        <h1 className="text-2xl font-extrabold text-[#004c91]">{t('visitRequestV2:page.title')}</h1>
        <p className="mt-1 text-sm text-slate-600">{t('visitRequestV2:page.subtitle')}</p>
      </header>
      <VisitRequestFormV2
        mode={mode}
        draftNamespace={draftNamespace}
        onSuccess={(response, values) => {
          setResult({ response, values });
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }}
      />
    </div>
  );
}
