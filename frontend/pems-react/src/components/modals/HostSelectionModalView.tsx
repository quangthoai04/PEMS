/**
 * Shared PRESENTATION for "pick a Host and confirm a decision" — the visual source of truth both
 * `AssignHostModal` (ordinary List/Detail approval) and `AssignHostPicker` (the Staff Leader's "Lưu và
 * duyệt" inside the per-campus pending edit) render through, so the two screens stop drifting apart
 * visually. It owns none of the write: candidates, submit state and errors are all props, and the
 * schedule-conflict "are you sure" is the only thing kept as local state here, because it is pure UI —
 * asking twice before calling `onSubmit`, never a second network call of its own.
 *
 * SHARED UI is deliberately NOT shared write orchestration: `AssignHostModal` calls
 * `delegationsApi.approveCampusInstance` itself and reports its own submitting/error state back down as
 * props; `AssignHostPicker` calls neither API — it hands the (hostUserId, decisionNote) choice to
 * `EditPendingCampusV2Page`, which sends it in the SAME combined edit+approve request. Collapsing that
 * distinction into "the shared component submits" would silently turn the Staff Leader's atomic
 * "Lưu và duyệt" into two separate writes.
 */
import { useEffect, useState, type ReactNode } from 'react';
import { motion } from 'motion/react';
import { X, AlertTriangle, Check, Users, Loader2, Search, UserCheck } from 'lucide-react';
import type { HostCandidate } from '../../features/delegations/types/delegations.types';
import { DECISION_NOTE_MAX_LENGTH } from '../../features/visit-request/utils/decisionConflict';
import { formatVietnamDateTime } from '../../shared/utils/vietnamTime';

const formatDateTime = (value?: string | null) => (value ? formatVietnamDateTime(value) : '-');

export interface HostSelectionModalViewProps {
  title: string;
  subtitle?: string | null;
  /** Optional short banner under the header (e.g. "approving requires naming a host"). */
  infoBanner?: ReactNode;
  candidates: HostCandidate[];
  isLoading: boolean;
  loadError: string | null;
  currentHostUserId?: number | null;
  selectedId: number | null;
  onSelect: (userId: number) => void;
  decisionNote: string;
  onDecisionNoteChange: (value: string) => void;
  decisionNoteLabel: string;
  decisionNotePlaceholder?: string;
  isSubmitting: boolean;
  /** Rendered instead of `submitError` when set — a blocking state, not a transient error line. */
  versionConflict?: boolean;
  versionConflictMessage?: string;
  onReloadRequested?: () => void;
  reloadLabel: string;
  submitError: string | null;
  confirmLabel: string;
  /**
   * Called ONLY once the user has truly confirmed — including past the schedule-conflict overlay.
   * Takes just the chosen host: the decision note is already the CALLER's own state (`decisionNote`
   * above came from them), so it is read there rather than re-derived here — trimming an empty note
   * to `null` is a per-caller normalization choice, not something this shared view should impose on
   * both callers alike.
   */
  onSubmit: (hostUserId: number) => void;
  onClose: () => void;
  searchPlaceholder: string;
  emptyCandidatesText: string;
  noMatchText: string;
  conflictLabel: (count: number) => string;
  conflictOverlayTitle: string;
  conflictOverlayBody: (fullName: string) => ReactNode;
  conflictOverlayCancel: string;
  conflictOverlayConfirm: string;
  cancelLabel: string;
  submittingLabel: string;
  loadingCandidatesLabel: string;
  hostCardTestId?: (userId: number) => string;
  confirmTestId?: string;
  decisionNoteTestId?: string;
  closeTestId?: string;
  /**
   * The small fixed labels on each candidate card. Kept out of this component's own JSX text so a
   * genuinely Visitor-reachable caller (`AssignHostPicker`) can supply i18n'd values while an
   * internal-tool-only caller (`AssignHostModal`, hardcoded Vietnamese by design) keeps its own —
   * this shared view carries no fixed vocabulary of its own.
   */
  labels: {
    selfHostBadge: string;
    leaderBadge: string;
    currentHostBadge: string;
    noConflict: string;
    hasConflict: string;
    conflictSourceCalendar: string;
    conflictSourceVisit: string;
  };
}

export function HostSelectionModalView({
  title, subtitle, infoBanner, candidates, isLoading, loadError, currentHostUserId,
  selectedId, onSelect, decisionNote, onDecisionNoteChange, decisionNoteLabel, decisionNotePlaceholder,
  isSubmitting, versionConflict, versionConflictMessage, onReloadRequested, submitError,
  confirmLabel, onSubmit, onClose, searchPlaceholder, emptyCandidatesText, noMatchText, conflictLabel,
  conflictOverlayTitle, conflictOverlayBody, conflictOverlayCancel, conflictOverlayConfirm,
  cancelLabel, submittingLabel, loadingCandidatesLabel, reloadLabel,
  hostCardTestId, confirmTestId, decisionNoteTestId, closeTestId, labels,
}: HostSelectionModalViewProps) {
  const [keyword, setKeyword] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const [confirmConflict, setConfirmConflict] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedKeyword(keyword), 300);
    return () => clearTimeout(timer);
  }, [keyword]);

  const filtered = debouncedKeyword.trim()
    ? candidates.filter(c =>
        `${c.fullName} ${c.email} ${c.departmentName ?? ''}`.toLowerCase().includes(debouncedKeyword.trim().toLowerCase()))
    : candidates;

  const selectedCandidate = candidates.find(c => c.userId === selectedId) ?? null;

  const attemptConfirm = () => {
    if (selectedId === null || versionConflict) return;
    if (selectedCandidate?.hasScheduleConflict) {
      setConfirmConflict(true);
      return;
    }
    onSubmit(selectedId);
  };

  const confirmDespiteConflict = () => {
    if (selectedId === null) return;
    setConfirmConflict(false);
    onSubmit(selectedId);
  };

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
      <motion.div
        initial={{ opacity: 0, scale: 0.95, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="bg-white rounded-3xl shadow-2xl w-full max-w-lg overflow-hidden flex flex-col max-h-[calc(100dvh-2rem)]"
      >
        <div className="px-6 py-4 bg-[#004c91] flex items-center justify-between flex-shrink-0">
          <div>
            <h3 className="text-lg font-bold text-white flex items-center gap-2">
              <Users className="w-5 h-5" /> {title}
            </h3>
            {subtitle && <p className="text-xs text-white/80 mt-0.5 truncate max-w-[26rem]">{subtitle}</p>}
          </div>
          <button
            type="button"
            data-testid={closeTestId}
            onClick={onClose}
            disabled={isSubmitting}
            className="text-white/85 hover:text-white transition-colors hover:bg-white/10 rounded-full p-1.5 cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 flex-1 overflow-y-auto">
          {infoBanner && (
            <p className="mb-3 rounded-xl border border-blue-100 bg-blue-50/60 px-3 py-2 text-[12px] text-[#004c91]">
              {infoBanner}
            </p>
          )}

          <div className="relative mb-3">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
            <input
              type="text"
              placeholder={searchPlaceholder}
              value={keyword}
              onChange={e => setKeyword(e.target.value)}
              className="w-full pl-9 pr-3 h-10 bg-white border border-slate-300 rounded-xl text-sm font-normal text-slate-700 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10"
            />
          </div>

          {isLoading ? (
            <div className="py-10 text-center text-slate-500">
              <Loader2 className="w-6 h-6 mx-auto mb-2 animate-spin text-[#004c91]" />
              <p className="text-sm">{loadingCandidatesLabel}</p>
            </div>
          ) : loadError ? (
            <div className="py-10 text-center text-red-500 text-sm font-normal">{loadError}</div>
          ) : filtered.length === 0 ? (
            <div className="py-10 text-center text-slate-500 text-sm">
              <Users className="w-10 h-10 mx-auto mb-2 text-slate-300" />
              {candidates.length === 0 ? emptyCandidatesText : noMatchText}
            </div>
          ) : (
            <div className="space-y-2">
              {filtered.map(c => {
                const selected = selectedId === c.userId;
                const isCurrent = currentHostUserId != null && currentHostUserId === c.userId;
                const isSelfOption = c.isStaffLeaderSelfHostOption === true;
                return (
                  <button
                    key={c.userId}
                    type="button"
                    data-testid={hostCardTestId?.(c.userId)}
                    onClick={() => onSelect(c.userId)}
                    className={`w-full text-left rounded-2xl border p-3 transition-colors outline-none cursor-pointer ${
                      selected
                        ? 'border-[#004c91] bg-blue-50/60 ring-2 ring-[#004c91]/10'
                        : isSelfOption
                          ? 'border-emerald-200 bg-emerald-50/40 hover:border-emerald-400'
                          : 'border-slate-200 hover:border-[#004c91]/40 hover:bg-slate-50'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-bold text-slate-800 truncate">
                          {isSelfOption && <UserCheck className="mr-1 inline-block h-4 w-4 text-emerald-600" />}
                          {c.fullName}
                          {isSelfOption && <span className="ml-2 text-[10px] font-semibold text-emerald-700 bg-emerald-100 rounded px-1.5 py-0.5">{labels.selfHostBadge}</span>}
                          {!isSelfOption && c.subRole?.toUpperCase() === 'LEADER' && <span className="ml-2 text-[10px] font-semibold text-[#004c91] bg-blue-100 rounded px-1.5 py-0.5">{labels.leaderBadge}</span>}
                          {isCurrent && <span className="ml-2 text-[10px] font-semibold text-slate-600 bg-slate-100 rounded px-1.5 py-0.5">{labels.currentHostBadge}</span>}
                        </p>
                        <p className="text-xs text-slate-500 truncate">
                          {c.email}
                          {c.roleLabel ? ` · ${c.roleLabel}` : ''}
                          {c.departmentName ? ` · ${c.departmentName}` : ''}
                        </p>
                      </div>
                      {selected && <Check className="w-5 h-5 text-[#004c91] flex-shrink-0" />}
                    </div>

                    {!c.hasScheduleConflict ? (
                      <p className="mt-1.5 text-[11px] font-normal text-emerald-600 flex items-center gap-1">
                        <Check className="w-3.5 h-3.5" /> {labels.noConflict}
                      </p>
                    ) : (
                      <div className="mt-2 rounded-xl bg-amber-50 border border-amber-200 p-2 text-[11px] text-amber-800">
                        <p className="font-bold flex items-center gap-1">
                          <AlertTriangle className="w-3.5 h-3.5" /> {labels.hasConflict}
                        </p>
                        {c.conflicts[0] && (
                          <p className="mt-0.5 truncate">
                            • {c.conflicts[0].title || (c.conflicts[0].source === 'CALENDAR' ? labels.conflictSourceCalendar : labels.conflictSourceVisit)}: {formatDateTime(c.conflicts[0].startAt)} → {formatDateTime(c.conflicts[0].endAt)}
                          </p>
                        )}
                        {c.conflictCount > 1 && (
                          <p className="mt-0.5 font-normal">{conflictLabel(c.conflictCount)}</p>
                        )}
                      </div>
                    )}
                  </button>
                );
              })}
            </div>
          )}

          <div className="mt-4">
            <label className="block text-sm font-bold text-slate-700 mb-1.5">{decisionNoteLabel}</label>
            <textarea
              data-testid={decisionNoteTestId}
              value={decisionNote}
              onChange={e => onDecisionNoteChange(e.target.value)}
              maxLength={DECISION_NOTE_MAX_LENGTH}
              rows={3}
              placeholder={decisionNotePlaceholder}
              disabled={isSubmitting}
              className="w-full px-3 py-2.5 rounded-xl border border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10 outline-none text-sm resize-none bg-slate-50/50 focus:bg-white"
            />
          </div>

          {versionConflict ? (
            <div role="alert" className="mt-3 rounded-xl border border-amber-300 bg-amber-50 px-3 py-3 text-sm text-amber-900">
              <p className="flex items-start gap-2 font-normal">
                <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
                {versionConflictMessage}
              </p>
              <button
                type="button"
                onClick={() => { onReloadRequested?.(); onClose(); }}
                className="mt-2 rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white outline-none transition-colors hover:bg-amber-700 cursor-pointer"
              >
                {reloadLabel}
              </button>
            </div>
          ) : (
            submitError && <p className="text-red-500 text-sm mt-3">{submitError}</p>
          )}
        </div>

        <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100 flex-shrink-0">
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            data-testid={confirmTestId}
            onClick={attemptConfirm}
            disabled={selectedId === null || isSubmitting || versionConflict}
            className="px-6 py-2 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003b70] shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            {isSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
            {isSubmitting ? submittingLabel : confirmLabel}
          </button>
        </div>

        {confirmConflict && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm overflow-hidden border border-gray-100">
              <div className="px-5 py-4 bg-amber-500 flex items-center gap-2">
                <AlertTriangle className="w-5 h-5 text-white" />
                <h4 className="text-base font-bold text-white">{conflictOverlayTitle}</h4>
              </div>
              <div className="p-5 text-sm text-slate-700">
                <p>{conflictOverlayBody(selectedCandidate?.fullName ?? '')}</p>
              </div>
              <div className="px-5 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
                <button
                  type="button"
                  onClick={() => setConfirmConflict(false)}
                  disabled={isSubmitting}
                  className="px-4 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer"
                >
                  {conflictOverlayCancel}
                </button>
                <button
                  type="button"
                  onClick={confirmDespiteConflict}
                  disabled={isSubmitting}
                  className="px-5 py-2 rounded-xl font-bold text-white bg-amber-600 hover:bg-amber-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 flex items-center gap-2"
                >
                  {isSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
                  {conflictOverlayConfirm}
                </button>
              </div>
            </div>
          </div>
        )}
      </motion.div>
    </div>
  );
}
