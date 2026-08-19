import React, { useCallback, useEffect, useRef, useState } from 'react';
import { X, AlertCircle } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { showSuccessToast } from '../../../../shared/utils/toast';
import { VisitRequestFormV2 } from './VisitRequestFormV2';
import { VisitRequestV2SuccessPanel } from './VisitRequestV2SuccessPanel';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import { V2_DRAFT_NOTHING_TO_SAVE, type SaveV2DraftResult } from '../../utils/visitRequestV2DraftStorage';
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
  /**
   * The open close-prompt, and which of the two it is:
   *   'save'    — the form holds something, so "save draft and close" is offered;
   *   'emptied' — the form has been emptied over a stored draft. There is nothing left to save, so
   *               that button is not shown at all; the only two ways on are to carry on filling the
   *               form in, or to leave — which deletes the stored draft, and says so.
   * Decided when the prompt opens, so a button cannot say one thing and do another.
   */
  const [closePrompt, setClosePrompt] = useState<'save' | 'emptied' | null>(null);
  /** Set when "save draft and close" came back a failure — the prompt STAYS open rather than lie. */
  const [saveDraftFailed, setSaveDraftFailed] = useState<'empty' | 'error' | null>(null);
  const [result, setResult] = useState<{ response: V2CreateResponse; values: VisitRequestV2Schema } | null>(null);
  /** Bumped to remount the form for "create another", so nothing survives from the finished one. */
  const [formGeneration, setFormGeneration] = useState(0);
  const [draftControls, setDraftControls] = useState<{
    saveDraftNow: () => SaveV2DraftResult;
    abandonEdits: () => void;
    isDirty: () => boolean;
    hasMeaningfulData: () => boolean;
    hasPersistedDraft: () => boolean;
    isBusy: () => boolean;
  } | null>(null);
  const dialogRef = useRef<HTMLDivElement>(null);

  /**
   * The ONE way this modal closes — X, the receipt's own button, Escape and the backdrop all come
   * through here, so they cannot drift into behaving differently.
   *
   * Order matters, and getting it wrong is what froze the receipt. The busy check used to run FIRST,
   * before the receipt check. `isBusy()` reads `stageRef` inside the form component — but the form
   * unmounts in the very same commit that shows the receipt, so React never runs the effect that
   * would have advanced that ref to CREATE_CONFIRMED. It stayed on VERIFYING_OTP forever, `isBusy()`
   * kept returning true, and X / Escape / backdrop silently did nothing on a request that had
   * already been created. Only the receipt's own "Đóng" worked, because it called onClose directly.
   *
   * Once a receipt exists the submission is finished by definition: nothing is in flight and there is
   * no draft left to protect, so it closes immediately with no busy check and no dirty guard.
   */
  const requestCloseModal = useCallback((_source: 'X' | 'BUTTON' | 'ESCAPE' | 'BACKDROP') => {
    if (result) { onClose(); return; }
    // No receipt yet: a verify in flight may be committing right now, and tearing the shell down
    // would destroy the only place its outcome can arrive (plan §5, §9).
    if (draftControls?.isBusy?.()) return;

    // Three separate questions, and all three are needed. "Dirty" alone was asking the wrong one:
    // React Hook Form counts a structural change as dirty, so adding an empty guest row to a form
    // nobody has typed in made X open a prompt whose own save button then answered "there is nothing
    // to save". Dirty says the form MOVED; meaningful says it holds something; persisted says there
    // is a stored draft standing behind it. A prompt is only worth showing when the movement puts
    // one of the latter two at risk.
    const dirty = draftControls?.isDirty?.() ?? false;
    const meaningful = draftControls?.hasMeaningfulData?.() ?? false;
    const persisted = draftControls?.hasPersistedDraft?.() ?? false;
    if (!dirty || (!meaningful && !persisted)) { onClose(); return; }

    setSaveDraftFailed(null);
    setClosePrompt(meaningful ? 'save' : 'emptied');
  }, [draftControls, result, onClose]);

  /**
   * "Save draft and close". The save is forced (no waiting on the 700ms autosave, which has not
   * necessarily fired for the field the user typed last) and its OUTCOME decides what happens next:
   * only a real success closes the modal and says so. A failure keeps the prompt open with the other
   * two choices still there, because closing on a save that did not happen is precisely the silent
   * data loss this prompt exists to prevent.
   */
  const saveDraftAndClose = useCallback(() => {
    const outcome = draftControls?.saveDraftNow?.();
    if (outcome && outcome.success === false) {
      setSaveDraftFailed(outcome.error === V2_DRAFT_NOTHING_TO_SAVE ? 'empty' : 'error');
      return;
    }
    // No outcome at all means no draft controls to ask — nothing was saved, so nothing is claimed.
    if (outcome?.success) {
      showSuccessToast(t('visitRequestV2:draft.saved'), 'v2-draft-saved');
    }
    setSaveDraftFailed(null);
    setClosePrompt(null);
    onClose();
  }, [draftControls, onClose, t]);

  /**
   * "Exit without saving" — the ONE way out that keeps nothing. The stored draft for this namespace
   * is deleted along with the edits on screen, whether it was restored at the start of this session
   * or autosaved during it; the prompt says as much before the user commits to it. There is no second
   * "delete the draft" button, because there is no second deleting outcome to tell apart.
   *
   * The confirmation is only shown when something was actually deleted — after a form the user merely
   * opened and emptied there is no draft to report on, and announcing one would be a lie about work
   * that never existed.
   */
  const closeWithoutSaving = useCallback(() => {
    const hadDraft = draftControls?.hasPersistedDraft?.() ?? false;
    draftControls?.abandonEdits?.();
    if (hadDraft) showSuccessToast(t('visitRequestV2:draft.deleted'), 'v2-draft-deleted');
    setSaveDraftFailed(null);
    setClosePrompt(null);
    onClose();
  }, [draftControls, onClose, t]);

  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.stopPropagation(); requestCloseModal('ESCAPE'); }
    };
    document.addEventListener('keydown', onKeyDown);
    // The page behind must not scroll while a near-fullscreen modal is open.
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [isOpen, requestCloseModal]);

  useEffect(() => {
    if (isOpen) dialogRef.current?.focus();
  }, [isOpen]);

  // Reset transient shell state between openings so a previous session cannot leak in.
  useEffect(() => {
    if (!isOpen) { setClosePrompt(null); setSaveDraftFailed(null); setResult(null); }
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-2 sm:p-4"
      onMouseDown={e => { if (e.target === e.currentTarget) requestCloseModal('BACKDROP'); }}
    >
      <div
        ref={dialogRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby="v2-modal-title"
        data-testid="v2-create-modal"
        // Overlay padding is p-2 (8px/side => 1rem vertical gutter) below `sm`, p-4 (16px/side =>
        // 2rem) at `sm` and up. Height = 100dvh minus that gutter, not an arbitrary vh percentage --
        // guarantees the modal never exceeds the visible viewport regardless of mobile browser
        // chrome (address bar / keyboard), matching exactly what the overlay already reserves.
        className="grid h-[calc(100dvh-1rem)] sm:h-[calc(100dvh-2rem)] w-full max-w-[1400px] grid-rows-[auto_1fr_auto] overflow-hidden rounded-2xl bg-white shadow-2xl outline-none"
      >
        {/* Sticky header (grid row, so it cannot be overlapped by the site header) */}
        <div className="flex items-center justify-between gap-4 border-b border-slate-200 bg-white px-4 py-3 sm:px-6 sm:py-4">
          <div>
            <h2 id="v2-modal-title" className="truncate text-xl font-extrabold text-[#004c91] sm:text-2xl">
              {t('visitRequestV2:modal.title')}
            </h2>
          </div>
          <button
            type="button"
            aria-label={t('visitRequestV2:modal.close')}
            data-testid="v2-modal-close"
            onClick={() => requestCloseModal('X')}
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
              // Through the shared handler like every other close, not straight to onClose. It used
              // to be the one path that worked while X and Escape were frozen, which is exactly how
              // that bug stayed invisible in testing.
              onClose={() => requestCloseModal('BUTTON')}
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

        {/* Sticky footer — the form portals its submit actions in here. Once the receipt is on
            screen it stays empty: closing is one of the receipt's own actions, and a second
            "done" button beside it only made the user wonder whether they differed. */}
        <div
          ref={setFooterEl}
          data-testid="v2-modal-footer"
          className="border-t border-slate-200 bg-white px-4 py-3 sm:px-6"
        />
      </div>

      <AnimatePresence>
        {closePrompt && (
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
                <p className="mt-2 text-sm font-normal text-slate-600 leading-relaxed">
                  {closePrompt === 'emptied'
                    ? t('visitRequestV2:draft.emptiedDesc')
                    : t('visitRequest:cancelConfirm.desc')}
                </p>
                {saveDraftFailed && (
                  <p
                    role="alert"
                    data-testid="v2-modal-save-draft-failed"
                    className="mt-3 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-normal text-amber-800"
                  >
                    {saveDraftFailed === 'empty'
                      ? t('visitRequestV2:draft.nothingToSave')
                      : t('visitRequestV2:draft.saveFailed')}
                  </p>
                )}
              </div>
              <div className="flex flex-col gap-2.5 px-6 pb-6">
                {/* Nothing to save means no save button: an emptied form leaves "keep filling it in"
                    and "leave" as the only two honest ways on. */}
                {closePrompt === 'save' && (
                  <button
                    type="button"
                    data-testid="v2-modal-save-draft"
                    onClick={saveDraftAndClose}
                    className="flex w-full items-center justify-center rounded-xl bg-[#004c91] px-4 py-3 text-sm font-bold text-white transition-colors hover:bg-[#013565] shadow-lg shadow-blue-900/20"
                  >
                    {t('visitRequest:cancelConfirm.saveAndExit')}
                  </button>
                )}
                <button
                  type="button"
                  data-testid="v2-modal-continue-editing"
                  onClick={() => { setSaveDraftFailed(null); setClosePrompt(null); }}
                  className="flex w-full items-center justify-center rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm font-bold text-slate-700 transition-colors hover:bg-slate-50"
                >
                  {t('visitRequest:cancelConfirm.continue')}
                </button>
                {/* The label alone. What "thoát không lưu" does is unchanged — the stored draft
                    still goes with the edits — but the prompt above already frames the choice, and
                    the second line under the button was read as a warning about losing work that
                    the two buttons beside it exist to prevent. */}
                <button
                  type="button"
                  data-testid="v2-modal-discard"
                  onClick={closeWithoutSaving}
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
