/**
 * Generic file upload/download backing email attachments + inline images.
 * Upload registers a row in `files` and returns its id + URLs; download is fetched as a blob through
 * the authenticated http client (the browser can't attach the bearer token to a raw <img>/<a>), then
 * exposed as an object URL for preview/download.
 */
import httpClient from './httpClient';

export interface UploadedFileDto {
  fileId: number;
  originalFilename: string;
  mimeType?: string | null;
  fileSize?: number | null;
  downloadUrl?: string | null;
  webViewUrl?: string | null;
  thumbnailUrl?: string | null;
}

export const filesApi = {
  /** Upload one file (multipart). `purpose` becomes the storage folder + files.file_purpose. */
  upload: async (file: File, purpose?: string): Promise<UploadedFileDto> => {
    const fd = new FormData();
    fd.append('file', file);
    if (purpose) fd.append('purpose', purpose);
    const { data } = await httpClient.post('/files/upload', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  /** Fetch a stored file's bytes (authenticated) and return an object URL for <img>/download. */
  fetchObjectUrl: async (fileId: number): Promise<string> => {
    const res = await httpClient.get(`/files/${fileId}/download`, { responseType: 'blob' });
    return URL.createObjectURL(res.data as Blob);
  },
};
