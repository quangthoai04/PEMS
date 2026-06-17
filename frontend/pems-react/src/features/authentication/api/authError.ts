import { AxiosError } from 'axios';

interface ApiErrorBody {
  message?: string;
  errors?: Record<string, string[]>;
}

/**
 * Extracts a safe, user-facing message from an API error. Falls back to a
 * generic message so we never surface stack traces or low-level details.
 */
export function getAuthErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  const axiosError = error as AxiosError<ApiErrorBody>;
  const body = axiosError?.response?.data;

  if (body?.message) return body.message;

  if (body?.errors) {
    const first = Object.values(body.errors).flat()[0];
    if (first) return first;
  }

  if (axiosError?.code === 'ERR_NETWORK') {
    return 'Cannot reach the server. Please check your connection and try again.';
  }

  return fallback;
}
