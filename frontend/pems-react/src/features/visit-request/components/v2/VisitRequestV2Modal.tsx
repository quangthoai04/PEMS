import React, { useCallback, useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { VisitRequestFormV2 } from './VisitRequestFormV2';
import { VisitRequestV2SuccessPanel } from './VisitRequestV2SuccessPanel';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import type { V2CreateResponse } from '../../api/visitRequestV2Api';
import type { UseVisitRequestFormV2Options } from '../../hooks/useVisitRequestFormV2';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  mode: UseVisitRequestFormV2Options['mode'];
  draftNamespace?: string;
  onSuccess: (result: V2CreateResponse, values: VisitRequestV2Schema) => void;
}

/**
 * Modal shell around the SHARED v2 form — the CTA experience users expect from v1: the form opens
 * over whatever page they were on instead of navigating away. `/visit-registration/v2` and
 * `/visit/create-v2` remain as real routes for deep links and refresh, and both render the exact
 * same `VisitRequestFormV2`; only the shell differs, so there is no second form implementation.
 *
 * Layout is a three-row grid — sticky header, scrolling body, sticky footer — rather than a tall
 * page inside a scrolling overlay. The body is the ONLY scroll container, so the form's own top
 * never slides under the site header, and the submit actions stay reachable in a long form.
 */
export const VisitRequestV2Modal: React.FC<Props> = ({
  isOpen, onClose, mode, draftNamespace, onSuccess,
}) => {
  const { t } = useTranslation(['visitRequestV2', 'common']);
  const [footerEl, setFooterEl] = useState<HTMLDivElement | null>(null);
  const [dirty, setDirty] = useState(false);
  const [confirmClose, setConfirmClose] = useState(false);
  const [result, setResult] = useState<{ response: V2CreateResponse; values: VisitRequestV2Schema } | null>(null);
  const [draftControls, setDraftControls] =
    useState<{ saveDraftNow: () => void; discardDraft: () => void } | null>(null);
  const dialogRef = useRef<HTMLDivElement>(null);

  // Closing with typed data always asks first — an accidental Esc must not discard the form.
  const requestClose = useCallback(() => {
    if (dirty) setConfirmClose(true);
    else onClose();
  }, [dirty, onClose]);

  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.stopPropagation(); requestClose(); }
    };
    document.addEventListener('keydown', onKeyDown);
    // The page behind must not scroll while a near-fullscreen modal is open.
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [isOpen, requestClose]);

  useEffect(() => {
    if (isOpen) dialogRef.current?.focus();
  }, [isOpen]);

  // Reset transient shell state between openings so a previous session cannot leak in.
  useEffect(() => {
    if (!isOpen) { setDirty(false); setConfirmClose(false); setResult(null); }
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-2 sm:p-4"
      onMouseDown={e => { if (e.target === e.currentTarget) requestClose(); }}
    >
      <div
        ref={dialogRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby="v2-modal-title"
        data-testid="v2-create-modal"
        className="grid h-[96vh] w-full max-w-[1400px] grid-rows-[auto_1fr_auto] overflow-hidden rounded-2xl bg-white shadow-2xl outline-none"
      >
        {/* Sticky header (grid row, so it cannot be overlapped by the site header) */}
        <div className="flex items-center justify-between gap-4 border-b border-slate-200 bg-white px-4 py-3 sm:px-6 sm:py-4">
          <h2 id="v2-modal-title" className="truncate text-lg font-extrabold text-[#004c91] sm:text-xl">
            {t('visitRequestV2:modal.title')}
          </h2>
          <button
            type="button"
            aria-label={t('visitRequestV2:modal.close')}
            data-testid="v2-modal-close"
            onClick={requestClose}
            className="rounded-lg p-2 text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* The ONLY scrolling region */}
        <div className="overflow-y-auto overscroll-contain bg-slate-50/60 px-3 py-4 sm:px-6 sm:py-5">
          {result ? (
            <VisitRequestV2SuccessPanel response={result.response} values={result.values} />
          ) : (
            <VisitRequestFormV2
              mode={mode}
              draftNamespace={draftNamespace}
              onSuccess={(response, values) => {
                // Stay open and show the receipt — closing here would hide the request code and,
                // in the public flow, the whole point of completing OTP.
                setResult({ response, values });
                setDirty(false);
                onSuccess(response, values);
              }}
              footerSlot={footerEl}
              onDirtyChange={setDirty}
              onDraftControls={setDraftControls}
            />
          )}
        </div>

        {/* Sticky footer — the form portals its submit actions in here */}
        <div
          ref={setFooterEl}
          data-testid="v2-modal-footer"
          className="border-t border-slate-200 bg-white px-4 py-3 sm:px-6"
        >
          {result && (
            <div className="flex justify-end">
              <button
                type="button"
                onClick={onClose}
                className="rounded-xl bg-[#004c91] px-6 py-2.5 text-sm font-bold text-white hover:bg-[#003a6f]"
              >
                {t('visitRequestV2:modal.done')}
              </button>
            </div>
          )}
        </div>
      </div>

      {confirmClose && (
        <div className="fixed inset-0 z-[110] flex items-center justify-center bg-black/40 p-4">
          <div role="alertdialog" aria-labelledby="v2-close-title" className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h3 id="v2-close-title" className="text-base font-extrabold text-slate-900">
              {t('visitRequestV2:modal.discardTitle')}
            </h3>
            <p className="mt-2 text-sm text-slate-600">{t('visitRequestV2:modal.discardBody')}</p>
            {/* Three outcomes, as v1 offered: keep the work for later, keep editing, or throw
                it away deliberately. Closing is never the same as discarding. */}
            <div className="mt-4 flex flex-wrap justify-end gap-2">
              <button
                type="button"
                className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold"
                onClick={() => setConfirmClose(false)}
              >
                {t('visitRequestV2:modal.keepEditing')}
              </button>
              <button
                type="button"
                data-testid="v2-modal-save-draft"
                className="rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white hover:bg-[#003a6f]"
                onClick={() => {
                  draftControls?.saveDraftNow();
                  setConfirmClose(false);
                  onClose();
                }}
              >
                {t('visitRequestV2:modal.saveDraftAndExit')}
              </button>
              <button
                type="button"
                data-testid="v2-modal-discard"
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white"
                onClick={() => {
                  draftControls?.discardDraft();
                  setConfirmClose(false);
                  onClose();
                }}
              >
                {t('visitRequestV2:modal.discardConfirm')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
