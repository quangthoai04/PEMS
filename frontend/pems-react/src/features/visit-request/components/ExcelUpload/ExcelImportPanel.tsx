import React from 'react';
import { AlertTriangle, CheckCircle2, Download, Loader2, RefreshCw, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { canApplyImport, EXCEL_MAX_MEMBERS, type ExcelImportReport, type ExcelTranslator } from './excelValidator';
import { downloadExcelErrorReport } from './excelDownload';

export interface ExcelImportState {
  /** Name of the file being checked; drives the loading line. */
  loadingFileName?: string;
  report?: ExcelImportReport;
  /**
   * What the form ACTUALLY did with the rows, and what the list holds now (IMP-04).
   *
   * <p>The panel used to be headed "Nhập Excel thành công" over a report whose "Danh sách hiện tại"
   * was computed for an APPEND — so after a replace it stated a size the list did not have, and the
   * word "thành công" described a check rather than a change. Every line the user reads now names an
   * action that has already happened to a list that is named.</p>
   */
  applied?: { phase: 'APPENDED' | 'REPLACED'; resultingCount: number };
}

interface Props {
  state: ExcelImportState;
  /** Campus name for the downloadable report — the report travels outside this screen. */
  campusLabel: string;
  /** Which list this panel is reporting on, so the message can say so (IMP-02/IMP-04). */
  kind: 'visitors' | 'supportTeam';
  /** Re-opens the file picker for THIS section. */
  onChooseAnother: () => void;
  onDismiss: () => void;
  testId: string;
}

const Row: React.FC<{ label: string; value: React.ReactNode; strong?: boolean }> = ({ label, value, strong }) => (
  <div className="flex items-baseline justify-between gap-3">
    <span className="text-slate-600">{label}</span>
    <span className={strong ? 'font-bold text-slate-900' : 'font-semibold text-slate-800'}>{value}</span>
  </div>
);

/**
 * The outcome of ONE section's import (plan §4). Guests and support staff each render their
 * own instance: a single shared message at the bottom of the card could not say which list
 * it was talking about, and the second import silently overwrote the first one's result.
 */
export const ExcelImportPanel: React.FC<Props> = ({
  state, campusLabel, kind, onChooseAnother, onDismiss, testId,
}) => {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);
  const excelT: ExcelTranslator = (key, options) => t(key, options) as string;
  const listLabel = t(kind === 'visitors'
    ? 'visitRequestV2:excel.report.listGuests'
    : 'visitRequestV2:excel.report.listSupport');

  if (state.loadingFileName) {
    return (
      <div
        data-testid={`${testId}-loading`}
        role="status"
        className="mt-3 flex items-center gap-2 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 text-sm font-semibold text-slate-700"
      >
        <Loader2 className="h-4 w-4 shrink-0 animate-spin text-[#004c91]" />
        {t('visitRequestV2:excel.checking', { fileName: state.loadingFileName })}
      </div>
    );
  }

  const report = state.report;
  if (!report) return null;

  const applied = canApplyImport(report);

  if (applied) {
    return (
      <div
        data-testid={`${testId}-success`}
        role="status"
        className="mt-3 rounded-xl border border-emerald-200 bg-emerald-50 p-3"
      >
        <div className="mb-2 flex items-center gap-2 text-sm font-extrabold text-emerald-800">
          <CheckCircle2 className="h-4 w-4 shrink-0" />
          {/* Names the ACTION and the LIST. "Nhập Excel thành công" said neither, so on a card with
              two importers it could not be told which list had just changed, or how. */}
          {t(state.applied?.phase === 'REPLACED'
            ? 'visitRequestV2:excel.report.replacedTitle'
            : 'visitRequestV2:excel.report.appendedTitle', { list: listLabel })}
          <button
            type="button"
            aria-label={t('visitRequestV2:excel.report.dismiss')}
            className="ml-auto rounded-lg p-1 text-emerald-700 hover:bg-emerald-100"
            onClick={onDismiss}
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="space-y-1 text-sm">
          <Row label={t('visitRequestV2:excel.report.targetList')} value={listLabel} />
          <Row label={t('visitRequestV2:excel.report.fileName')} value={report.fileName} />
          <Row label={t('visitRequestV2:excel.report.totalRows')} value={report.totalRows} />
          <Row label={t('visitRequestV2:excel.report.imported')} value={report.validRows} strong />
          <Row label={t('visitRequestV2:excel.report.duplicateSkipped')} value={report.duplicateRows} />
          <Row
            label={t('visitRequestV2:excel.report.currentList')}
            value={t('visitRequestV2:excel.report.currentListValue', {
              // The size the list ACTUALLY has. `report.resultingCount` is computed for an append, so
              // after a replace it named a number that was never true.
              count: state.applied?.resultingCount ?? report.resultingCount, max: EXCEL_MAX_MEMBERS,
            })}
          />
        </div>
      </div>
    );
  }

  return (
    <div
      data-testid={`${testId}-error`}
      role="alert"
      className="mt-3 rounded-xl border border-red-200 bg-red-50 p-3"
    >
      <div className="mb-2 flex items-center gap-2 text-sm font-extrabold text-red-800">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        {t('visitRequestV2:excel.report.failedTitle')}
        <button
          type="button"
          aria-label={t('visitRequestV2:excel.report.dismiss')}
          className="ml-auto rounded-lg p-1 text-red-700 hover:bg-red-100"
          onClick={onDismiss}
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      {report.fatalMessage ? (
        <p className="text-sm font-semibold text-red-700">{report.fatalMessage}</p>
      ) : (
        <>
          <div className="space-y-1 text-sm">
            <Row label={t('visitRequestV2:excel.report.fileName')} value={report.fileName} />
            <Row label={t('visitRequestV2:excel.report.totalRows')} value={report.totalRows} />
            <Row label={t('visitRequestV2:excel.report.validRows')} value={report.validRows} />
            <Row label={t('visitRequestV2:excel.report.errorRows')} value={report.errorRows} strong />
            <Row label={t('visitRequestV2:excel.report.duplicateRows')} value={report.duplicateRows} />
            {report.overLimitRows > 0 && (
              <Row label={t('visitRequestV2:excel.report.overLimitRows')} value={report.overLimitRows} strong />
            )}
          </div>

          {report.overLimitRows > 0 && (
            <p className="mt-2 rounded-lg bg-red-100 px-2.5 py-2 text-xs font-semibold text-red-800">
              {t('visitRequestV2:excel.report.overLimitHint', {
                remaining: report.remainingSlots, max: EXCEL_MAX_MEMBERS,
              })}
            </p>
          )}

          {report.errors.length > 0 && (
            <div className="mt-3 max-h-64 overflow-auto rounded-lg border border-red-200 bg-white">
              <table data-testid={`${testId}-error-table`} className="w-full border-collapse text-left text-xs">
                <thead className="sticky top-0 bg-red-100/70">
                  <tr>
                    <th scope="col" className="w-16 p-2 font-bold text-red-900">
                      {t('visitRequestV2:excel.report.colRow')}
                    </th>
                    <th scope="col" className="w-40 p-2 font-bold text-red-900">
                      {t('visitRequestV2:excel.report.colColumn')}
                    </th>
                    <th scope="col" className="p-2 font-bold text-red-900">
                      {t('visitRequestV2:excel.report.colMessage')}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {report.errors.map((e, i) => (
                    <tr key={`${e.row}-${e.column}-${i}`} className="border-t border-red-100">
                      <td className="p-2 font-bold text-slate-700">{e.row}</td>
                      <td className="p-2 text-slate-700">{e.column}</td>
                      <td className="p-2 text-slate-700">{e.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <p className="mt-2 text-xs font-semibold text-red-700">
            {t('visitRequestV2:excel.report.formUnchanged')}
          </p>
        </>
      )}

      <div className="mt-3 flex flex-wrap gap-2">
        {report.errors.length > 0 && (
          <button
            type="button"
            data-testid={`${testId}-download`}
            className="inline-flex items-center gap-1.5 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-xs font-bold text-red-700 hover:bg-red-50"
            onClick={() => downloadExcelErrorReport(report, campusLabel, excelT)}
          >
            <Download className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.report.download')}
          </button>
        )}
        <button
          type="button"
          data-testid={`${testId}-retry`}
          className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-50"
          onClick={onChooseAnother}
        >
          <RefreshCw className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.report.chooseAnother')}
        </button>
        <button
          type="button"
          className="inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-bold text-slate-500 hover:bg-slate-100"
          onClick={onDismiss}
        >
          {t('visitRequestV2:excel.report.close')}
        </button>
      </div>
    </div>
  );
};
