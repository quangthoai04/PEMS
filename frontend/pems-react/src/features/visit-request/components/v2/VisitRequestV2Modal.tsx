import React, { useCallback, useEffect, useRef, useState } from 'react';
import { X, AlertCircle } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { showSuccessToast } from '../../../../shared/utils/toast';
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
  /**
   * Opens the request that was just created. Supplied by the HOST rather than navigated here: this
   * shell is a presentational container, and reaching for a router inside it would tie every
   * consumer (and every test) to one.
   */
  onViewRequest?: (visitRequestId: number) => void;
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
  isOpen, onClose, mode, draftNamespace, onSuccess, onViewRequest,
}) => {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest', 'common']);
  const [footerEl, setFooterEl] = useState<HTMLDivElement | null>(null);
  const [confirmClose, setConfirmClose] = useState(false);
  const [result, setResult] = useState<{ response: V2CreateResponse; values: VisitRequestV2Schema } | null>(null);
  /** Bumped to remount the form for "create another", so nothing survives from the finished one. */
  const [formGeneration, setFormGeneration] = useState(0);
  const [draftControls, setDraftControls] = useState<{
    saveDraftNow: () => void;
    discardDraft: () => void;
    isDirty: () => boolean;
    isBusy: () => boolean;
  } | null>(null);
  const dialogRef = useRef<HTMLDivElement>(null);

  // Closing with typed data always asks first — an accidental Esc must not discard the form.
  const requestClose = useCallback(() => {
    // A verify in flight may be committing the request right now; Esc must not tear the shell down
    // while the only place the outcome can arrive is inside it (plan §5, §9).
    if (draftControls?.isBusy?.()) return;
    // Once the receipt is on screen there is nothing left to lose — closing is just closing.
    if (result) { onClose(); return; }
    if (draftControls?.isDirty?.()) setConfirmClose(true);
    else onClose();
  }, [draftControls, result, onClose]);

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
    if (!isOpen) { setConfirmClose(false); setResult(null); }
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
            <VisitRequestV2SuccessPanel
              response={result.response}
              values={result.values}
              onViewRequest={onViewRequest && result.response.visitRequestId
                ? () => {
                    // Close first: the detail lives on a route behind this modal, and leaving the
                    // overlay mounted would put the page the user asked for underneath it.
                    onClose();
                    onViewRequest(result.response.visitRequestId);
                  }
                : undefined}
              onCreateAnother={() => {
                setResult(null);
                setFormGeneration(g => g + 1);
              }}
            />
          ) : (
            <VisitRequestFormV2
              key={formGeneration}
              mode={mode}
              draftNamespace={draftNamespace}
              onSuccess={(response, values) => {
                // Stay open and show the receipt — closing here would hide the request code and,
                // in the public flow, the whole point of completing OTP. Nothing auto-closes: the
                // user decides when they are done reading.
                setResult({ response, values });
                showSuccessToast(
                  t('visitRequestV2:success.toast', { code: response.requestCode }),
                  `v2-created-${response.visitRequestId}`,
                );
                onSuccess(response, values);
              }}
              footerSlot={footerEl}
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

      <AnimatePresence>
        {confirmClose && (
          <div className="fixed inset-0 z-[120] flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-sm">
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="w-full max-w-sm overflow-hidden rounded-3xl bg-white shadow-2xl"
            >
              <div className="px-6 py-6 text-center">
                <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-blue-50">
                  <AlertCircle className="h-7 w-7 text-[#004c91]" />
                </div>
                <h3 className="text-lg font-extrabold text-slate-900">
                  {t('visitRequest:cancelConfirm.title')}
                </h3>
                <p className="mt-2 text-sm font-medium text-slate-600 leading-relaxed">
                  {t('visitRequest:cancelConfirm.desc')}
                </p>
              </div>
              <div className="flex flex-col gap-2.5 px-6 pb-6">
                <button
                  type="button"
                  data-testid="v2-modal-save-draft"
                  onClick={() => {
                    draftControls?.saveDraftNow();
                    setConfirmClose(false);
                    onClose();
                  }}
                  className="flex w-full items-center justify-center rounded-xl bg-[#004c91] px-4 py-3 text-sm font-bold text-white transition-colors hover:bg-[#013565] shadow-lg shadow-blue-900/20"
                >
                  {t('visitRequest:cancelConfirm.saveAndExit')}
                </button>
                <button
                  type="button"
                  onClick={() => setConfirmClose(false)}
                  className="flex w-full items-center justify-center rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm font-bold text-slate-700 transition-colors hover:bg-slate-50"
                >
                  {t('visitRequest:cancelConfirm.continue')}
                </button>
                <button
                  type="button"
                  data-testid="v2-modal-discard"
                  onClick={() => {
                    draftControls?.discardDraft();
                    setConfirmClose(false);
                    onClose();
                  }}
                  className="flex w-full items-center justify-center rounded-xl border border-red-200 bg-white px-4 py-3 text-sm font-bold text-red-600 transition-colors hover:bg-red-50"
                >
                  {t('visitRequest:cancelConfirm.discard')}
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>
    </div>
  );
};
