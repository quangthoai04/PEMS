/**
 * One idempotency key per logical send, kept across a retry (G11 / R-103).
 *
 * `useGuardedSend` already stops a second click while the first request is in flight. It cannot stop the
 * case that actually duplicates emails: the browser gives up on a slow request, the promise rejects, the
 * in-flight flag clears, and the user — told it failed — presses the button again. The server never saw
 * the disconnect and runs the whole thing a second time.
 *
 * The fix is not a longer timeout or a cleverer spinner. It is that the retry has to be recognisable AS a
 * retry, which means carrying the same name as the attempt it is repeating. This hook owns those names.
 *
 * Two rules decide when a key is reused and when a new one is minted, and both are about intent:
 *
 *   • Same attempt → same key. A network failure, a timeout, an "đang xử lý" or an unknown outcome all
 *     leave the key in place, because the user is still trying to complete ONE send.
 *   • New attempt → new key. A confirmed success, a refusal the server decided before sending anything,
 *     or a change to what is being sent — all of these end the attempt, and the next click is a new one.
 *
 * Keys live in `sessionStorage`, so a reload in the middle of a send does not turn the retry into a
 * second send. Not `localStorage`: a key that outlives the tab would let tomorrow's deliberate re-send be
 * mistaken for today's retry, which is the opposite failure and a worse one.
 */
import { useCallback } from 'react';

const STORAGE_PREFIX = 'pems.idem.';

/** `crypto.randomUUID` where available; a v4-shaped fallback for older/insecure contexts. */
function newKey(): string {
  const c = globalThis.crypto;
  if (c && typeof c.randomUUID === 'function') return c.randomUUID();

  if (c && typeof c.getRandomValues === 'function') {
    const bytes = c.getRandomValues(new Uint8Array(16));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
  }

  // Last resort. Still unique enough to name one attempt in one session, which is all a key must do.
  return `k-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}

/** sessionStorage can throw (private mode, storage disabled). A send must not fail because of that. */
function read(name: string): string | null {
  try { return sessionStorage.getItem(name); } catch { return null; }
}
function write(name: string, value: string): void {
  try { sessionStorage.setItem(name, value); } catch { /* keep the in-memory key for this attempt */ }
}
function remove(name: string): void {
  try { sessionStorage.removeItem(name); } catch { /* nothing to do */ }
}

export interface IdempotentSend {
  /**
   * The key for this attempt. Returns the stored one when an attempt is already open — that is what
   * makes a retry a retry — and mints one otherwise.
   */
  keyFor: (operation: string, resourceId: string | number) => string;

  /** The attempt is over: the next click starts a new one with a new key. */
  complete: (operation: string, resourceId: string | number) => void;
}

export function useIdempotentSend(): IdempotentSend {
  const keyFor = useCallback((operation: string, resourceId: string | number) => {
    const name = `${STORAGE_PREFIX}${operation}.${resourceId}`;
    const existing = read(name);
    if (existing) return existing;

    const key = newKey();
    write(name, key);
    return key;
  }, []);

  const complete = useCallback((operation: string, resourceId: string | number) => {
    remove(`${STORAGE_PREFIX}${operation}.${resourceId}`);
  }, []);

  return { keyFor, complete };
}

/**
 * Whether a failed send has definitely ended, so its key may be retired.
 *
 * Only two things end an attempt on the failure side. The server refused it before doing anything
 * (a 4xx with a body — a scope error, a missing recipient, a bad price), or the key itself is unusable
 * and a new one is the fix.
 *
 * Everything else keeps the key. A timeout, an aborted request, a 502 from a proxy, a 5xx, an
 * "đang xử lý" and an unknown outcome all share one property: the send may already have happened. Minting
 * a fresh key for any of them is exactly how one click becomes two emails.
 */
export function attemptIsOver(error: unknown): boolean {
  const err = error as { response?: { status?: number; data?: { errorCode?: string } } } | undefined;
  const status = err?.response?.status;

  // No response at all — timeout, offline, connection reset. The request may have been served.
  if (status === undefined) return false;

  const code = err?.response?.data?.errorCode;

  // In-progress and unknown-outcome are 409s that explicitly mean "do not start over".
  if (code === 'EMAIL_IDEMPOTENCY_IN_PROGRESS') return false;
  if (code === 'EMAIL_IDEMPOTENCY_OUTCOME_UNKNOWN') return false;

  // A key the server will not accept, or one already spent on a different request: retire it so the
  // user's next click can succeed rather than repeating the same refusal.
  if (code === 'EMAIL_IDEMPOTENCY_KEY_REQUIRED') return true;
  if (code === 'EMAIL_IDEMPOTENCY_KEY_INVALID') return true;
  if (code === 'IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST') return true;

  // A 5xx may have been served before it failed; a 4xx was decided before anything was sent.
  return status < 500;
}

/** The message to show for a failure, distinguishing "not sent" from "we do not know". */
export function sendFailureMessage(error: unknown, fallback: string): string {
  const err = error as
    | { response?: { status?: number; data?: { message?: string; errorCode?: string } } }
    | undefined;

  const code = err?.response?.data?.errorCode;
  if (code === 'EMAIL_IDEMPOTENCY_IN_PROGRESS')
    return 'Yêu cầu đang được xử lý. Vui lòng đợi kết quả, đừng gửi lại.';
  if (code === 'EMAIL_IDEMPOTENCY_OUTCOME_UNKNOWN')
    return 'Chưa xác định được kết quả lần gửi trước — email có thể đã được gửi. Kiểm tra lịch sử email trước khi gửi lại.';
  if (code === 'IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST')
    return 'Nội dung đã thay đổi so với lần gửi trước. Bấm gửi lại để thực hiện một lần gửi mới.';

  // A request that never got an answer is NOT reported as a failure — saying "gửi thất bại" here is what
  // invites the second click.
  if (err?.response === undefined)
    return 'Mất kết nối trước khi có kết quả. Bấm gửi lại để tiếp tục đúng lần gửi này.';

  return err?.response?.data?.message || fallback;
}

export default useIdempotentSend;
