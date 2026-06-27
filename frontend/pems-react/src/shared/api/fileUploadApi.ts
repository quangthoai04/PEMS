import httpClient from './httpClient';

/**
 * Shape returned by the shared backend upload services (avatar / gallery / news / document / minutes).
 * Mirrors `UploadedFileDto` on the backend.
 */
export interface UploadedFileResponse {
  fileId: number;
  fileUrl: string;
  webViewUrl?: string;
  downloadUrl?: string;
  thumbnailUrl?: string;
  mimeType: string;
  fileSize: number;
  checksumSha256?: string;
}

/**
 * Posts a single file as `multipart/form-data` to a backend upload endpoint. Centralizes the FormData
 * boilerplate so feature modules (gallery/news/document/…) don't each re-write an axios multipart call.
 *
 * @param endpoint  backend path, e.g. `/profile/avatar` or `/gallery/images`
 * @param fieldName the multipart field name the endpoint expects (e.g. `file`, `avatar`)
 * @param file      the file to upload
 * @param method    HTTP verb (`post` by default; avatar uses `put`)
 */
export async function uploadFileToEndpoint(
  endpoint: string,
  fieldName: string,
  file: File,
  method: 'post' | 'put' = 'post',
): Promise<UploadedFileResponse> {
  const formData = new FormData();
  formData.append(fieldName, file);

  const response = await httpClient.request({
    url: endpoint,
    method,
    data: formData,
    headers: { 'Content-Type': 'multipart/form-data' },
  });

  // Backend envelopes payloads as { data: ... } in most endpoints; fall back to the raw body.
  return (response.data?.data ?? response.data) as UploadedFileResponse;
}
