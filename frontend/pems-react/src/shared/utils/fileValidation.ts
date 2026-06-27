/**
 * Frontend file validation — purely for UX (fast feedback before upload). The backend
 * (`FileValidationPolicy` / `FileContentValidator`) is always the final authority and re-checks size,
 * extension, MIME and magic bytes. SVG is never allowed for image purposes.
 */

export type FilePurpose =
  | 'USER_AVATAR'
  | 'GALLERY_IMAGE'
  | 'GALLERY_VIDEO'
  | 'NEWS_IMAGE'
  | 'NEWS_ATTACHMENT'
  | 'DOCUMENT'
  | 'MINUTES_ATTACHMENT'
  | 'VISIT_REQUEST_ATTACHMENT';

export interface FileValidationRule {
  maxSizeBytes: number;
  allowedMimeTypes: string[];
  allowedExtensions: string[];
}

const IMAGE_MIMES = ['image/jpeg', 'image/png', 'image/webp'];
const IMAGE_EXTS = ['.jpg', '.jpeg', '.png', '.webp'];

const DOC_MIMES = [
  'application/pdf',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  'image/jpeg',
  'image/png',
];
const DOC_EXTS = ['.pdf', '.docx', '.xlsx', '.pptx', '.jpg', '.jpeg', '.png'];

const MB = 1024 * 1024;

export function getFileValidationRule(purpose: FilePurpose): FileValidationRule {
  switch (purpose) {
    case 'USER_AVATAR':
      return { maxSizeBytes: 2 * MB, allowedMimeTypes: IMAGE_MIMES, allowedExtensions: IMAGE_EXTS };
    case 'GALLERY_IMAGE':
    case 'NEWS_IMAGE':
      return { maxSizeBytes: 5 * MB, allowedMimeTypes: IMAGE_MIMES, allowedExtensions: IMAGE_EXTS };
    case 'GALLERY_VIDEO':
      return {
        maxSizeBytes: 100 * MB,
        allowedMimeTypes: ['video/mp4', 'video/webm'],
        allowedExtensions: ['.mp4', '.webm'],
      };
    case 'MINUTES_ATTACHMENT':
      return {
        maxSizeBytes: 10 * MB,
        allowedMimeTypes: [
          'application/pdf',
          'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        ],
        allowedExtensions: ['.pdf', '.docx'],
      };
    case 'VISIT_REQUEST_ATTACHMENT':
      return {
        maxSizeBytes: 10 * MB,
        allowedMimeTypes: [
          'application/pdf',
          'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
          'image/jpeg',
          'image/png',
        ],
        allowedExtensions: ['.pdf', '.docx', '.jpg', '.jpeg', '.png'],
      };
    case 'NEWS_ATTACHMENT':
    case 'DOCUMENT':
      return { maxSizeBytes: 20 * MB, allowedMimeTypes: DOC_MIMES, allowedExtensions: DOC_EXTS };
    default:
      return { maxSizeBytes: 10 * MB, allowedMimeTypes: [], allowedExtensions: [] };
  }
}

export interface FileValidationResult {
  ok: boolean;
  /** Vietnamese message suitable for display when {@link ok} is false. */
  message?: string;
}

/** Validates a file against the rule for `purpose`. Returns `{ ok: true }` or a reason. */
export function validateFile(file: File, purpose: FilePurpose): FileValidationResult {
  const rule = getFileValidationRule(purpose);

  if (file.size <= 0) {
    return { ok: false, message: 'Tệp rỗng hoặc không hợp lệ.' };
  }

  if (file.size > rule.maxSizeBytes) {
    return { ok: false, message: `Tệp vượt quá kích thước tối đa ${Math.round(rule.maxSizeBytes / MB)}MB.` };
  }

  const dot = file.name.lastIndexOf('.');
  const ext = dot >= 0 ? file.name.slice(dot).toLowerCase() : '';
  if (rule.allowedExtensions.length > 0 && !rule.allowedExtensions.includes(ext)) {
    return { ok: false, message: `Định dạng tệp không được hỗ trợ (${rule.allowedExtensions.join(', ')}).` };
  }

  const mime = (file.type || '').toLowerCase();
  if (rule.allowedMimeTypes.length > 0 && mime && !rule.allowedMimeTypes.includes(mime)) {
    return { ok: false, message: 'Kiểu nội dung tệp không được hỗ trợ.' };
  }

  return { ok: true };
}
