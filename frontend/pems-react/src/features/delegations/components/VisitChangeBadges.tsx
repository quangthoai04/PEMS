import { useTranslation } from 'react-i18next';
import { AlertCircle, Bell } from 'lucide-react';
import type { VisitListChangeSummary } from '../types/delegations.types';

interface Props {
  summary?: VisitListChangeSummary | null;
  'data-testid'?: string;
}

/**
 * The change indicator for one list row.
 *
 * Rendered BESIDE the status badge, never instead of it. "Chờ xử lý" is where the request IS; these
 * say what happened to it recently. Collapsing the two would mean a Staff Leader filtering by status
 * loses every request that was edited, which is the opposite of the point.
 *
 * Ordering is by urgency, not by recency: what needs a decision comes first, then what merely
 * changed, then the count of everything unseen.
 */
export function VisitChangeBadges({ summary, 'data-testid': testId }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  if (!summary) return null;

  const { pendingAmendmentCount, requiresViewerAction, unreadChangeCount, latestEventCode } = summary;
  const badges: Array<{ key: string; label: string; tone: 'action' | 'change' | 'count' }> = [];

  // Something is waiting on a decision — the only badge that means "do something".
  if (pendingAmendmentCount > 0) {
    badges.push({
      key: 'amendments',
      label: t('visitRequestV2:changeBadges.pendingAmendment', { count: pendingAmendmentCount }),
      tone: 'action',
    });
  }

  // What kind of change, from the most recent one. Unknown codes fall back to the neutral wording
  // rather than printing an internal enum at the user.
  if (unreadChangeCount > 0 && latestEventCode) {
    const known = ['CONTENT_UPDATED', 'STATUS_CHANGED', 'HOST_CHANGED', 'DECIDED'];
    const code = known.includes(String(latestEventCode)) ? String(latestEventCode) : 'CONTENT_UPDATED';
    badges.push({
      key: 'latest',
      label: t(`visitRequestV2:changeBadges.event.${code}`),
      tone: requiresViewerAction && pendingAmendmentCount === 0 ? 'action' : 'change',
    });
  }

  // The count only earns its own badge when there is more than one thing behind it; "1 thay đổi
  // chưa xem" beside a badge already describing that one change is just noise.
  if (unreadChangeCount > 1) {
    badges.push({
      key: 'count',
      label: t('visitRequestV2:changeBadges.unreadCount', { count: unreadChangeCount }),
      tone: 'count',
    });
  }

  if (badges.length === 0) return null;

  const toneClass = (tone: 'action' | 'change' | 'count') =>
    tone === 'action'
      ? 'bg-[#f37021]/10 text-[#c1490a] ring-[#f37021]/30'
      : tone === 'change'
        ? 'bg-blue-50 text-blue-800 ring-blue-200'
        : 'bg-slate-100 text-slate-700 ring-slate-200';

  return (
    <span data-testid={testId ?? 'visit-change-badges'} className="inline-flex flex-wrap items-center gap-1">
      {badges.map(b => (
        <span
          key={b.key}
          data-testid={`change-badge-${b.key}`}
          className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-bold ring-1 ring-inset ${toneClass(b.tone)}`}
        >
          {b.tone === 'action'
            ? <AlertCircle className="h-3 w-3" aria-hidden />
            : <Bell className="h-3 w-3" aria-hidden />}
          {b.label}
        </span>
      ))}
    </span>
  );
}
