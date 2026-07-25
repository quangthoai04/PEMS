import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { VisitRequestFormV2 } from '../../features/visit-request/components/v2/VisitRequestFormV2';
import { VisitRequestV2SuccessPanel } from '../../features/visit-request/components/v2/VisitRequestV2SuccessPanel';
import type { V2CreateResponse } from '../../features/visit-request/api/visitRequestV2Api';
import type { VisitRequestV2Schema } from '../../features/visit-request/schema/visitRequestV2.schema';
import { visitDraftNamespace } from '../../features/visit-request/utils/visitRequestV2DraftStorage';
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

  // Keyed by ACCOUNT, exactly as the dashboard modal is: this route and that modal are the same
  // person filling in the same form, and an email-keyed namespace forked them into two drafts —
  // start on one surface, come back on the other, and the work looked lost. (It also kept an email
  // address in a localStorage key, which is PII sitting where it does not need to be.)
  const draftNamespace = mode === 'authenticated' ? visitDraftNamespace(user?.userId) : undefined;

  if (result) {
    return (
      <div className="mx-auto max-w-3xl px-4 pb-16 pt-24 xl:pt-32 2xl:pt-36">
        <VisitRequestV2SuccessPanel
          response={result.response}
          values={result.values}
          footer={(
            <Link to="/" className="text-sm font-bold text-[#004c91] hover:underline">
              {t('visitRequestV2:success.backHome')}
            </Link>
          )}
        />
      </div>
    );
  }

  return (
    // The site header is `fixed`, so pages must reserve its height or their first heading renders
    // underneath it. The logo (and therefore the header) grows at xl/2xl, hence the step-ups.
    <div className="mx-auto max-w-5xl px-4 pb-16 pt-24 xl:pt-32 2xl:pt-36">
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
