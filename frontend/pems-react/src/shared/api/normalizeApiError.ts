import axios from 'axios';
import i18n from '../i18n/config';
import { getApiErrorMessage, maskSecrets } from '../utils/toast';

/**
 * A single, app-wide classification of a failed request. Screens branch on `category` instead of
 * sniffing message strings — so a 409, a 500 and a dropped connection can never be flattened into the
 * same "no data" / empty-list outcome (the Phase 4.5 defect this file exists to kill).
 *
 *   forbidden  → 403 (no permission)
 *   notFound   → 404 (the thing does not exist)
 *   conflict   → 409 business conflict (see `errorCode`; VISIT_FORM_DETAIL_MISSING is flagged separately)
 *   validation → 422 / 400 (the input/data does not meet processing rules)
 *   server     → 5xx (the system errored)
 *   network    → no response reached us (offline, DNS, CORS, server down)
 *   timeout    → the request was aborted for taking too long
 *   unknown    → a response arrived but we could not classify it (contract/parse failure, odd status)
 */
export type ApiErrorCategory =
  | 'forbidden'
  | 'notFound'
  | 'conflict'
  | 'validation'
  | 'server'
  | 'network'
  | 'timeout'
  | 'unknown';

/** Backend error code raised when a Pure V2 request is missing its per-campus form detail. */
export const VISIT_FORM_DETAIL_MISSING = 'VISIT_FORM_DETAIL_MISSING';

export interface NormalizedApiError {
  category: ApiErrorCategory;
  /** HTTP status when a response reached us; undefined for network/timeout. */
  status?: number;
  /** Backend `errorCode` when present (e.g. VISIT_FORM_DETAIL_MISSING, CAMPUS_INACTIVE_ACCESS_DENIED). */
  errorCode?: string;
  /**
   * True only for the specific 409 that means the Pure V2 data is incomplete for this record. The UI
   * shows "dữ liệu chưa đầy đủ" guidance, NOT a generic conflict and NOT an empty state.
   */
  isVisitFormDetailMissing: boolean;
  /** Human-facing, i18n-resolved and secret-masked message, safe to render directly. */
  message: string;
  /** A correlation / trace id if the backend supplied one — surfaced in the error UI for support. */
  correlationId?: string;
  /** The original thrown value, kept for logging (never rendered). */
  cause: unknown;
}

/** timeout markers axios / the browser use when a request is aborted for exceeding its deadline. */
function isTimeout(err: { code?: string; message?: string }): boolean {
  const code = err.code ?? '';
  if (code === 'ECONNABORTED' || code === 'ETIMEDOUT') return true;
  return /timeout/i.test(err.message ?? '');
}

function readCorrelationId(error: unknown): string | undefined {
  if (!axios.isAxiosError(error)) return undefined;
  const headers = error.response?.headers as Record<string, string> | undefined;
  const fromHeader = headers?.['x-correlation-id'] ?? headers?.['x-trace-id'] ?? headers?.['x-request-id'];
  if (typeof fromHeader === 'string' && fromHeader.trim()) return fromHeader.trim();
  const data = error.response?.data as { correlationId?: unknown; traceId?: unknown } | undefined;
  const fromBody = data?.correlationId ?? data?.traceId;
  return typeof fromBody === 'string' && fromBody.trim() ? fromBody.trim() : undefined;
}

function categoryFromStatus(status: number): ApiErrorCategory {
  if (status === 403) return 'forbidden';
  if (status === 404) return 'notFound';
  if (status === 409) return 'conflict';
  if (status === 422 || status === 400) return 'validation';
  if (status >= 500) return 'server';
  return 'unknown';
}

/**
 * Classify any thrown request failure into a stable {@link NormalizedApiError}. This is the ONE place
 * that decides what kind of failure occurred; everything else (toasts, error panels, action gating)
 * consumes the result.
 */
export function normalizeApiError(error: unknown): NormalizedApiError {
  const err = (error ?? {}) as { code?: string; message?: string; response?: { status?: number; data?: unknown }; request?: unknown };
  const errorCode = ((): string | undefined => {
    const data = err.response?.data as { errorCode?: unknown } | undefined;
    return typeof data?.errorCode === 'string' && data.errorCode.trim() ? data.errorCode.trim() : undefined;
  })();
  const status = typeof err.response?.status === 'number' ? err.response.status : undefined;
  const correlationId = readCorrelationId(error);

  // No response reached us: it is either a timeout (aborted for taking too long) or a plain network
  // failure. Both must stay DISTINCT from an HTTP error and from an empty result.
  const noResponse = err.response === undefined
    && (err.request !== undefined || err.code === 'ERR_NETWORK' || err.message === 'Network Error' || isTimeout(err));
  if (noResponse) {
    if (isTimeout(err)) {
      return {
        category: 'timeout',
        errorCode,
        isVisitFormDetailMissing: false,
        message: maskSecrets(i18n.t('toast:common.timeoutError')),
        correlationId,
        cause: error,
      };
    }
    return {
      category: 'network',
      errorCode,
      isVisitFormDetailMissing: false,
      message: getApiErrorMessage(error),
      correlationId,
      cause: error,
    };
  }

  const category = status !== undefined ? categoryFromStatus(status) : 'unknown';
  return {
    category,
    status,
    errorCode,
    isVisitFormDetailMissing: status === 409 && errorCode === VISIT_FORM_DETAIL_MISSING,
    message: getApiErrorMessage(error),
    correlationId,
    cause: error,
  };
}
