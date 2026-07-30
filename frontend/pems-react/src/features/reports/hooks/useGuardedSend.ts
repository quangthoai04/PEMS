/**
 * One in-flight send per row, tracked per row.
 *
 * The report screens used a single `sendingUserId` for a whole table. Pressing "Gửi" on one row while
 * another was still in flight overwrote it, and the first request's `finally` then cleared the flag for
 * the second — so the second row's button re-enabled while its own request was still running, and the
 * next click sent that report a second time. The disabled attribute was the only guard, and a single
 * shared id cannot describe two concurrent sends.
 *
 * A set of ids describes them exactly, and the entry guard makes the rule true in the handler rather
 * than only in the markup: a click that arrives while that row is sending does nothing at all.
 *
 * This is a UI-session guard and nothing more. The commands carry no idempotency key, so a retry after a
 * network timeout — where the browser gave up but the server did not — genuinely can send a second email.
 * That limit is real and is recorded as debt; it is not something this hook can close.
 */
import { useCallback, useRef, useState } from 'react';

export interface GuardedSend<TId> {
  /** True while a send for this id is in flight. */
  isSending: (id: TId) => boolean;
  /** True while any send is in flight. */
  isBusy: boolean;
  /**
   * Runs `action` unless this id is already sending. Returns true when it ran.
   * The flag is cleared whether the action resolves or rejects, so a refused send can be corrected
   * and retried without reloading the screen.
   */
  send: (id: TId, action: () => Promise<void>) => Promise<boolean>;
}

export function useGuardedSend<TId>(): GuardedSend<TId> {
  const [inFlight, setInFlight] = useState<ReadonlySet<TId>>(() => new Set());
  // Read synchronously inside `send`: two clicks in the same tick must not both see the pre-click state.
  const inFlightRef = useRef<Set<TId>>(new Set());

  const isSending = useCallback((id: TId) => inFlight.has(id), [inFlight]);

  const send = useCallback(async (id: TId, action: () => Promise<void>) => {
    if (inFlightRef.current.has(id)) return false;

    inFlightRef.current.add(id);
    setInFlight(new Set(inFlightRef.current));
    try {
      await action();
    } finally {
      inFlightRef.current.delete(id);
      setInFlight(new Set(inFlightRef.current));
    }
    return true;
  }, []);

  return { isSending, isBusy: inFlight.size > 0, send };
}

export default useGuardedSend;
