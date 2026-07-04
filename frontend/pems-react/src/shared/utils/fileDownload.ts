import httpClient from '../api/httpClient';

/**
 * Extracts a filename from a `Content-Disposition` header (`attachment; filename="x.pdf"` or the
 * RFC 5987 `filename*=UTF-8''x.pdf` form). Returns null when the header is absent/unparseable so the
 * caller can fall back to a name it already knows client-side.
 */
function parseContentDispositionFileName(header: unknown): string | null {
  if (typeof header !== 'string' || !header) return null;
  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8Match) {
    try { return decodeURIComponent(utf8Match[1]); } catch { /* fall through to plain form */ }
  }
  const plainMatch = /filename="?([^";]+)"?/i.exec(header);
  return plainMatch ? plainMatch[1] : null;
}

/**
 * Downloads a file that lives behind an authenticated backend route (e.g. `/files/{id}/download`)
 * as a blob — sending the Bearer token via httpClient — then triggers a browser save-as using a
 * short-lived object URL. Replaces the broken pattern of opening the raw API URL in an `<a href>`,
 * which never carries the Authorization header and fails with "Authentication required".
 */
export async function downloadAuthenticatedFile(path: string, fallbackFileName = 'file'): Promise<void> {
  const response = await httpClient.get<Blob>(path, { responseType: 'blob' });
  const fileName = parseContentDispositionFileName(response.headers?.['content-disposition']) || fallbackFileName;

  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/** Fetches an authenticated file as a blob object URL (for iframe/object preview). Caller must revoke. */
export async function fetchAuthenticatedBlobUrl(path: string): Promise<string> {
  const response = await httpClient.get<Blob>(path, { responseType: 'blob' });
  return URL.createObjectURL(response.data);
}
