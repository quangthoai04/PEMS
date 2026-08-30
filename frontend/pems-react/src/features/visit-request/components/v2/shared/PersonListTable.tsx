import React from 'react';
import { useTranslation } from 'react-i18next';
import { PartnerSystemBadge } from '../../../../../shared/components/PartnerSystemBadge';

export interface PersonRow {
  /** Stable key from the backend; the index is used only as a last resort. */
  id?: string | number | null;
  fullName: string;
  jobTitle?: string | null;
  organization?: string | null;
  /** Which partner profile `organization` was picked from, or null/undefined for free text
   *  (PART-01) — drives the light "✓ Có trong hệ thống" note next to the organization. */
  organizationPartnerId?: number | null;
  nationality?: string | null;
}

interface Props {
  title: string;
  rows: PersonRow[];
  emptyMessage: string;
  /** Rendered as a trailing column (desktop) / footer (mobile). Purely presentational —
   *  whether an action is allowed at all is the container's decision, never this component's. */
  renderActions?: (row: PersonRow, index: number) => React.ReactNode;
  actionsLabel?: string;
  'data-testid'?: string;
}

/**
 * One way to present a list of people across every v2 surface (guests, support staff, participants,
 * invitees).
 *
 * The ordinal is computed here as `index + 1` and is deliberately NOT stored anywhere: it is a
 * reading aid for whoever is looking at THIS list, so removing a row renumbers the rest instead of
 * leaving a gap that looks like missing data.
 *
 * Below `sm` the table becomes cards carrying the SAME fields — narrowing the screen must not
 * silently drop someone's nationality or organisation, which is exactly the information a
 * receptionist needs on a phone.
 */
export const PersonListTable: React.FC<Props> = ({
  title, rows, emptyMessage, renderActions, actionsLabel, ...rest
}) => {
  const { t } = useTranslation(['visitRequestV2']);
  const EMPTY = '—';

  return (
    <div data-testid={rest['data-testid']}>
      <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2 border-b border-slate-200 pb-1">
        <p className="text-xs font-bold uppercase tracking-wide text-[#004c91]">{title}</p>
        <span className="text-xs font-normal text-slate-500">
          {t('visitRequestV2:person.count', { count: rows.length })}
        </span>
      </div>

      {rows.length === 0 ? (
        <p className="text-sm italic text-slate-400">{emptyMessage}</p>
      ) : (
        <>
          {/* Desktop: a real table. It scrolls inside its own wrapper so a long organisation name
              never forces the whole page sideways. */}
          <div className="hidden overflow-x-auto rounded-lg border border-slate-200 sm:block">
            <table className="w-full table-fixed text-left text-[13px]">
              <thead className="bg-[#004c91] text-white">
                <tr>
                  <th scope="col" className="w-12 px-2.5 py-2 font-semibold">{t('visitRequestV2:person.ordinal')}</th>
                  <th scope="col" className="w-1/4 px-2.5 py-2 font-semibold">{t('visitRequestV2:person.fullName')}</th>
                  <th scope="col" className="w-1/4 px-2.5 py-2 font-semibold">{t('visitRequestV2:person.jobTitle')}</th>
                  <th scope="col" className="w-1/4 px-2.5 py-2 font-semibold">{t('visitRequestV2:person.organization')}</th>
                  <th scope="col" className="w-1/4 px-2.5 py-2 font-semibold">{t('visitRequestV2:person.nationality')}</th>
                  {renderActions && (
                    <th scope="col" className="w-24 px-2.5 py-2 font-semibold">{actionsLabel}</th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {rows.map((row, index) => (
                  <tr key={row.id ?? index} className="transition-colors hover:bg-slate-50">
                    <td className="px-2.5 py-2 text-slate-500">{index + 1}</td>
                    <td className="px-2.5 py-2 font-semibold text-slate-800">{row.fullName || EMPTY}</td>
                    <td className="px-2.5 py-2 text-slate-600">{row.jobTitle || EMPTY}</td>
                    <td className="px-2.5 py-2 text-slate-600">
                      {row.organization || EMPTY}
                      {row.organizationPartnerId != null && <PartnerSystemBadge strength="light" />}
                    </td>
                    <td className="px-2.5 py-2 text-slate-600">{row.nationality || EMPTY}</td>
                    {renderActions && <td className="px-2.5 py-2">{renderActions(row, index)}</td>}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Mobile: the same five fields, one card per person. */}
          <ul className="space-y-2 sm:hidden">
            {rows.map((row, index) => (
              <li key={row.id ?? index} className="rounded-lg border border-slate-200 p-3">
                <div className="flex items-baseline gap-2">
                  <span className="text-xs font-bold text-slate-500">
                    {t('visitRequestV2:person.ordinal')} {index + 1}
                  </span>
                  <span className="min-w-0 break-words text-sm font-semibold text-slate-800">
                    {row.fullName || EMPTY}
                  </span>
                </div>
                <dl className="mt-1.5 space-y-0.5 text-[13px]">
                  <div className="flex gap-2">
                    <dt className="shrink-0 text-slate-500">{t('visitRequestV2:person.jobTitle')}:</dt>
                    <dd className="min-w-0 break-words text-slate-700">{row.jobTitle || EMPTY}</dd>
                  </div>
                  <div className="flex gap-2">
                    <dt className="shrink-0 text-slate-500">{t('visitRequestV2:person.organization')}:</dt>
                    <dd className="min-w-0 break-words text-slate-700">
                      {row.organization || EMPTY}
                      {row.organizationPartnerId != null && <PartnerSystemBadge strength="light" />}
                    </dd>
                  </div>
                  <div className="flex gap-2">
                    <dt className="shrink-0 text-slate-500">{t('visitRequestV2:person.nationality')}:</dt>
                    <dd className="min-w-0 break-words text-slate-700">{row.nationality || EMPTY}</dd>
                  </div>
                </dl>
                {renderActions && <div className="mt-2">{renderActions(row, index)}</div>}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
};
