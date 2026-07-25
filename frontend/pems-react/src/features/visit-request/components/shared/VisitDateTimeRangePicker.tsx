import React, { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CalendarDays } from 'lucide-react';
import { TimeSelect, type TimeOption } from './TimeSelect';
import {
  addMinutes,
  durationMinutes,
  earliestStart,
  joinWallClock,
  splitDuration,
  splitWallClock,
  timeSlots,
  wallClockToMinutes,
} from './visitDateTime';

/** Suggested granularity of the time lists. The user may still type any minute. */
const STEP_MINUTES = 15;
/** Mirrors V2_MIN_DURATION_MINUTES / CampusVisitFormDtoValidator.MinDurationMinutes. */
const MIN_DURATION_MINUTES = 30;
/** What a fresh end time is offered as when the start moves (plan §12). */
const DEFAULT_DURATION_MINUTES = 60;

export interface VisitDateTimeRangeProps {
  /** Vietnam wall-clock "YYYY-MM-DDTHH:mm", '' when unset. */
  startValue: string;
  endValue: string;
  onChange: (next: { start: string; end: string }) => void;
  /** 72h for a new submit, 24h for visitor edit/resubmit — never hardcoded here. */
  minAdvanceHours: number;
  /** Submit-time messages from the schema; shown alongside the live ones. */
  startError?: string;
  endError?: string;
  disabled?: boolean;
  idPrefix: string;
}

const fieldLabel = 'text-sm font-bold text-slate-900';

/**
 * The visit window, entered the way a calendar app asks for it (plan §9–§13): one date, a start
 * time, and an end time whose options carry the resulting duration — with an explicit switch for
 * the case where the visit ends on another day.
 *
 * The stored contract is unchanged: two wall-clock strings, `startDatetime` and `endDatetime`,
 * exactly as before. Splitting date and time is a presentation choice only, so drafts written by
 * the old two-input version still load.
 *
 * Validation is shown LIVE here because the form validates on submit: a schedule that reads
 * "ends before it starts" must say so while the user is still looking at it, not three sections
 * later. The schema remains the authority — nothing is auto-corrected behind the user's back.
 */
export const VisitDateTimeRangePicker: React.FC<VisitDateTimeRangeProps> = ({
  startValue, endValue, onChange, minAdvanceHours, startError, endError, disabled, idPrefix,
}) => {
  const { t, i18n } = useTranslation(['visitRequestV2']);
  const uid = useId();
  const prefix = `${idPrefix}-${uid}`;

  const start = splitWallClock(startValue);
  const end = splitWallClock(endValue);

  /**
   * Multi-day is a MODE, not an inference: a user who ticks it keeps a second date field even
   * before choosing a different day. It starts on whenever the stored values already span two
   * dates, so opening a saved multi-day visit shows it as such.
   */
  const [multiDay, setMultiDay] = useState<boolean>(() => !!start && !!end && start.date !== end.date);
  useEffect(() => {
    if (start && end && start.date !== end.date) setMultiDay(true);
  }, [start?.date, end?.date]); // eslint-disable-line react-hooks/exhaustive-deps

  /**
   * Whether the END was chosen deliberately. Only an untouched (or now-impossible) end is
   * re-suggested when the start moves — a deliberate 3-hour visit is never quietly shortened
   * back to one hour (plan §12).
   */
  const endTouched = useRef<boolean>(!!endValue);

  const minStart = useMemo(() => earliestStart(minAdvanceHours), [minAdvanceHours]);
  const minStartDate = splitWallClock(minStart)?.date ?? '';

  const emit = useCallback((nextStart: string, nextEnd: string) => {
    onChange({ start: nextStart, end: nextEnd });
  }, [onChange]);

  /** Applies a new start, re-suggesting the end only when it is untouched or no longer valid. */
  const applyStart = (nextStart: string) => {
    if (!nextStart) { emit('', endValue); return; }

    const currentDuration = durationMinutes(nextStart, endValue);
    const endIsUsable = currentDuration !== null && currentDuration >= MIN_DURATION_MINUTES;
    if (endTouched.current && endIsUsable) { emit(nextStart, endValue); return; }

    const suggested = addMinutes(nextStart, DEFAULT_DURATION_MINUTES);
    // In same-day mode a suggestion that rolls past midnight would silently become multi-day.
    const suggestedParts = splitWallClock(suggested);
    const nextStartParts = splitWallClock(nextStart);
    if (!multiDay && suggestedParts && nextStartParts && suggestedParts.date !== nextStartParts.date) {
      emit(nextStart, joinWallClock(nextStartParts.date, '23:59'));
      return;
    }
    emit(nextStart, suggested);
  };

  const setStartDate = (date: string) => {
    const time = start?.time ?? '09:00';
    const nextStart = joinWallClock(date, time);
    if (!multiDay) {
      // Same-day mode: the end follows the date the user just picked.
      const nextEnd = end ? joinWallClock(date, end.time) : '';
      const dur = durationMinutes(nextStart, nextEnd);
      if (endTouched.current && dur !== null && dur >= MIN_DURATION_MINUTES) {
        emit(nextStart, nextEnd);
        return;
      }
    }
    applyStart(nextStart);
  };

  const setStartTime = (time: string) => {
    const date = start?.date ?? minStartDate;
    applyStart(joinWallClock(date, time));
  };

  const setEndDate = (date: string) => {
    endTouched.current = true;
    emit(startValue, joinWallClock(date, end?.time ?? start?.time ?? '10:00'));
  };

  const setEndTime = (time: string) => {
    endTouched.current = true;
    const date = multiDay ? (end?.date ?? start?.date ?? '') : (start?.date ?? end?.date ?? '');
    emit(startValue, joinWallClock(date, time));
  };

  const toggleMultiDay = (on: boolean) => {
    setMultiDay(on);
    if (on || !start || !end) return;
    // Coming back to same-day: pull the end onto the start's date rather than leaving a
    // window that silently spans two days while the UI claims otherwise.
    const sameDayEnd = joinWallClock(start.date, end.time);
    const dur = durationMinutes(startValue, sameDayEnd);
    emit(startValue, dur !== null && dur >= MIN_DURATION_MINUTES
      ? sameDayEnd
      : addMinutes(startValue, DEFAULT_DURATION_MINUTES));
  };

  // ── Duration ─────────────────────────────────────────────────────────────
  const total = durationMinutes(startValue, endValue);

  const formatDuration = useCallback((minutes: number): string => {
    const { days, hours, minutes: mins } = splitDuration(minutes);
    const parts: string[] = [];
    if (days > 0) parts.push(t('visitRequestV2:schedule.durationDays', { count: days }));
    if (hours > 0) parts.push(t('visitRequestV2:schedule.durationHours', { count: hours }));
    if (mins > 0 || parts.length === 0) parts.push(t('visitRequestV2:schedule.durationMinutes', { count: mins }));
    return parts.join(' ');
  }, [t]);

  // ── Options ──────────────────────────────────────────────────────────────
  const startOptions: TimeOption[] = useMemo(
    () => timeSlots(STEP_MINUTES).map(value => ({ value })),
    [],
  );

  /**
   * End options are generated FROM the start, each labelled with the visit length it produces —
   * the whole point of the pattern: you pick "2 giờ", not "10:00" and then do the arithmetic.
   * In same-day mode the list stops at midnight, because an end that rolls over is not same-day.
   */
  const endOptions: TimeOption[] = useMemo(() => {
    const startMin = wallClockToMinutes(startValue);
    if (startMin === null) return timeSlots(STEP_MINUTES).map(value => ({ value }));

    const out: TimeOption[] = [];
    const first = Math.ceil(MIN_DURATION_MINUTES / STEP_MINUTES) * STEP_MINUTES;
    const span = multiDay ? 48 * 60 : 24 * 60;
    for (let delta = first; delta <= span; delta += STEP_MINUTES) {
      const candidate = addMinutes(startValue, delta);
      const parts = splitWallClock(candidate);
      if (!parts) break;
      if (!multiDay && parts.date !== splitWallClock(startValue)?.date) break;
      out.push({ value: parts.time, hint: formatDuration(delta) });
    }
    return out;
  }, [startValue, multiDay, formatDuration]);

  // ── Live messages (plan §15) ─────────────────────────────────────────────
  const liveStartError = useMemo(() => {
    if (!startValue) return undefined;
    const startMin = wallClockToMinutes(startValue);
    const floor = wallClockToMinutes(minStart);
    if (startMin === null) return t('visitRequestV2:schedule.invalidDateTime');
    if (floor !== null && startMin < floor) {
      return t('visitRequestV2:schedule.minAdvance', { hours: minAdvanceHours });
    }
    return undefined;
  }, [startValue, minStart, minAdvanceHours, t]);

  const liveEndError = useMemo(() => {
    if (!startValue || !endValue) return undefined;
    if (total === null) return t('visitRequestV2:schedule.invalidDateTime');
    if (total <= 0) return t('visitRequestV2:schedule.endAfterStart');
    if (total < MIN_DURATION_MINUTES) {
      return t('visitRequestV2:schedule.minDuration', { minutes: MIN_DURATION_MINUTES });
    }
    return undefined;
  }, [startValue, endValue, total, t]);

  const showStartError = startError ?? liveStartError;
  const showEndError = endError ?? liveEndError;

  /** "Thứ Sáu, 31/07/2026" — the Vietnamese reading of whatever the date input holds. */
  const readableDate = (value: string | undefined): string => {
    if (!value) return '';
    const parts = splitWallClock(`${value}T00:00`);
    if (!parts) return '';
    const [y, m, d] = value.split('-').map(Number);
    return new Intl.DateTimeFormat(i18n.language === 'en' ? 'en-GB' : 'vi-VN', {
      weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric',
    }).format(new Date(Date.UTC(y, m - 1, d)));
  };

  const dateCls = (hasError?: boolean) =>
    `h-11 w-full min-w-0 rounded-xl border bg-white px-4 text-sm font-semibold text-slate-800 outline-none transition-colors disabled:bg-slate-100 ${
      hasError
        ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-500/10'
        : 'border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10'
    }`;

  return (
    <fieldset data-testid={`${idPrefix}-schedule`} className="rounded-2xl border border-slate-200 bg-slate-50/50 p-3 sm:p-4">
      <legend className="flex items-center gap-1.5 px-1 text-sm font-extrabold text-slate-900">
        <CalendarDays className="h-4 w-4 text-[#004c91]" />
        {t('visitRequestV2:schedule.legend')} <span className="text-red-500">*</span>
      </legend>

      {multiDay ? (
        <div className="space-y-4">
          <div className="grid grid-cols-1 gap-x-5 gap-y-2 sm:grid-cols-[1fr_9rem]">
            <div className="flex flex-col gap-1.5">
              <label className={fieldLabel} htmlFor={`${prefix}-start-date`}>
                {t('visitRequestV2:schedule.startDate')}
              </label>
              <input
                id={`${prefix}-start-date`}
                type="date"
                lang="vi-VN"
                disabled={disabled}
                min={minStartDate || undefined}
                value={start?.date ?? ''}
                onChange={e => setStartDate(e.target.value)}
                className={dateCls(!!showStartError)}
                data-testid={`${idPrefix}-start-date`}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className={fieldLabel} htmlFor={`${prefix}-start-time`}>
                {t('visitRequestV2:schedule.startTime')}
              </label>
              <TimeSelect
                id={`${prefix}-start-time`}
                testId={`${idPrefix}-start-time`}
                ariaLabel={t('visitRequestV2:schedule.startTime')}
                value={start?.time ?? ''}
                onChange={setStartTime}
                options={startOptions}
                hasError={!!showStartError}
                disabled={disabled}
              />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-x-5 gap-y-2 sm:grid-cols-[1fr_9rem]">
            <div className="flex flex-col gap-1.5">
              <label className={fieldLabel} htmlFor={`${prefix}-end-date`}>
                {t('visitRequestV2:schedule.endDate')}
              </label>
              <input
                id={`${prefix}-end-date`}
                type="date"
                lang="vi-VN"
                disabled={disabled}
                min={start?.date || minStartDate || undefined}
                value={end?.date ?? ''}
                onChange={e => setEndDate(e.target.value)}
                className={dateCls(!!showEndError)}
                data-testid={`${idPrefix}-end-date`}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className={fieldLabel} htmlFor={`${prefix}-end-time`}>
                {t('visitRequestV2:schedule.endTime')}
              </label>
              <TimeSelect
                id={`${prefix}-end-time`}
                testId={`${idPrefix}-end-time`}
                ariaLabel={t('visitRequestV2:schedule.endTime')}
                value={end?.time ?? ''}
                onChange={setEndTime}
                options={endOptions}
                emptyHint={t('visitRequestV2:schedule.noEndSlots')}
                hasError={!!showEndError}
                disabled={disabled}
              />
            </div>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-x-5 gap-y-2 sm:grid-cols-[1fr_9rem_9rem]">
          <div className="flex flex-col gap-1.5">
            <label className={fieldLabel} htmlFor={`${prefix}-start-date`}>
              {t('visitRequestV2:schedule.date')}
            </label>
            <input
              id={`${prefix}-start-date`}
              type="date"
              lang="vi-VN"
              disabled={disabled}
              min={minStartDate || undefined}
              value={start?.date ?? ''}
              onChange={e => setStartDate(e.target.value)}
              className={dateCls(!!showStartError)}
              data-testid={`${idPrefix}-start-date`}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={fieldLabel} htmlFor={`${prefix}-start-time`}>
              {t('visitRequestV2:schedule.startTime')}
            </label>
            <TimeSelect
              id={`${prefix}-start-time`}
              testId={`${idPrefix}-start-time`}
              ariaLabel={t('visitRequestV2:schedule.startTime')}
              value={start?.time ?? ''}
              onChange={setStartTime}
              options={startOptions}
              hasError={!!showStartError}
              disabled={disabled}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={fieldLabel} htmlFor={`${prefix}-end-time`}>
              {t('visitRequestV2:schedule.endTime')}
            </label>
            <TimeSelect
              id={`${prefix}-end-time`}
              testId={`${idPrefix}-end-time`}
              ariaLabel={t('visitRequestV2:schedule.endTime')}
              value={end?.time ?? ''}
              onChange={setEndTime}
              options={endOptions}
              emptyHint={t('visitRequestV2:schedule.noEndSlots')}
              hasError={!!showEndError}
              disabled={disabled}
            />
          </div>
        </div>
      )}

      <div className="mt-3 flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <label className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-700">
          <input
            type="checkbox"
            disabled={disabled}
            checked={multiDay}
            onChange={e => toggleMultiDay(e.target.checked)}
            data-testid={`${idPrefix}-multiday`}
            className="h-4 w-4 rounded border-slate-300 text-[#004c91] focus:ring-[#004c91]"
          />
          {t('visitRequestV2:schedule.endsOnAnotherDay')}
        </label>

        {total !== null && total > 0 && (
          <span
            data-testid={`${idPrefix}-duration`}
            className="rounded-full bg-[#004c91]/10 px-3 py-1 text-xs font-bold text-[#004c91]"
          >
            {t('visitRequestV2:schedule.duration', { value: formatDuration(total) })}
          </span>
        )}
      </div>

      {start?.date && (
        <p className="mt-2 text-xs font-medium text-slate-500">
          {multiDay
            ? t('visitRequestV2:schedule.readableRange', {
                from: readableDate(start.date), to: readableDate(end?.date),
              })
            : readableDate(start.date)}
        </p>
      )}

      <p className="mt-1 text-xs font-medium text-slate-500">
        {t('visitRequestV2:schedule.rulesHint', { hours: minAdvanceHours, minutes: MIN_DURATION_MINUTES })}
      </p>

      {showStartError && (
        <p data-testid={`${idPrefix}-start-error`} className="mt-2 text-xs font-semibold text-red-600">
          {showStartError}
        </p>
      )}
      {showEndError && (
        <p data-testid={`${idPrefix}-end-error`} className="mt-1 text-xs font-semibold text-red-600">
          {showEndError}
        </p>
      )}
    </fieldset>
  );
};
