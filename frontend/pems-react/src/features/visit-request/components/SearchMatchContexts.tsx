import { useTranslation } from 'react-i18next';
import { Search } from 'lucide-react';
import type { SearchMatchContext } from '../../delegations/types/delegations.types';

interface Props {
  contexts?: SearchMatchContext[] | null;
}

/**
 * Renders where a search keyword matched, e.g. "Khớp tại: TP.HCM — Mục đích chuyến thăm". The backend
 * already scoped these to the campuses the caller may see, so this component renders the response verbatim
 * and NEVER derives extra campus context. Only stable field CODES arrive (no raw snippet/PII); unknown
 * codes fall back to the code itself. The matched-context badge is a distinct concept from the mixed badge.
 */
export default function SearchMatchContexts({ contexts }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  if (!contexts || contexts.length === 0) return null;

  const fieldLabel = (code: string): string => t(`visitRequestV2:search.fields.${code}`, code);

  return (
    <ul className="mt-1 flex flex-col gap-0.5" aria-label={t('visitRequestV2:search.matchedAt')}>
      {contexts.map((ctx, i) => {
        const where = ctx.scope === 'CAMPUS'
          ? (ctx.campusName ?? t('visitRequestV2:search.requestScope'))
          : t('visitRequestV2:search.requestScope');
        const fields = ctx.matchedFields.map(fieldLabel).join(', ');
        return (
          <li
            key={`${ctx.scope}-${ctx.visitInstanceId ?? 'req'}-${i}`}
            className="inline-flex items-center gap-1 text-xs text-slate-500 dark:text-slate-400"
          >
            <Search className="h-3 w-3 shrink-0 text-slate-400" aria-hidden />
            <span className="font-semibold">{t('visitRequestV2:search.matchedAt')}:</span>
            <span className="font-medium text-slate-600 dark:text-slate-300">{where}</span>
            <span aria-hidden>—</span>
            <span>{fields}</span>
          </li>
        );
      })}
    </ul>
  );
}
