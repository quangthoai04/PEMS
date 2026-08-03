/**
 * One uploaded file, wherever it is listed — a composer's attachment strip, a sent message's
 * history, a partner's documents. Every surface that shows an attachment shows the same three
 * affordances in the same order, because "can I look at what I attached?" should not have a
 * different answer on each screen.
 *
 * What it deliberately does NOT decide: whether the file may be removed. The delete button appears
 * only when the caller passes `onRemove`, so the business rule (a mandatory report cannot be
 * detached; a sent message's attachment cannot be edited at all) stays with the screen that owns it.
 * `required` only explains the absence — a control that vanishes without a word reads as a bug.
 */
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Download, Eye, FileText, Image as ImageIcon, Loader2, Paperclip, Trash2 } from 'lucide-react';

import { API_ENDPOINTS } from '../../api/endpoints';
import { downloadAuthenticatedFile } from '../../utils/fileDownload';
import { formatFileSize } from '../../utils/fileUtils';
import { canPreview, hasStoredBytes, resolvePreviewKind, type PreviewableFile } from './filePreviewKind';

export type FileAttachmentStatus = 'ready' | 'uploading' | 'failed';

export interface FileAttachmentItemProps {
  file: PreviewableFile;
  /** Opens the shared preview modal. The caller owns the modal so one instance serves a whole list. */
  onPreview: (file: PreviewableFile) => void;
  /** Supplied ONLY when this screen's rules allow detaching this file. */
  onRemove?: () => void;
  /** Renders the "bắt buộc" badge in place of the (absent) delete button. */
  required?: boolean;
  status?: FileAttachmentStatus;
  /** Small caption under the name, e.g. "Ảnh trong nội dung" for an inline image. */
  hint?: string;
  /** Wider row for detail pages; the default chip suits a wrapping strip. */
  variant?: 'chip' | 'card';
  'data-testid'?: string;
}

export function FileAttachmentItem({
  file,
  onPreview,
  onRemove,
  required = false,
  status = 'ready',
  hint,
  variant = 'chip',
  'data-testid': testId,
}: FileAttachmentItemProps) {
  const { t } = useTranslation('files');

  const stored = hasStoredBytes(file);
  const busy = status === 'uploading';
  // Nothing may be requested for a file that has no row yet: there is no id to authorize.
  const actionable = stored && !busy;
  const previewable = actionable && canPreview(file);
  const kind = resolvePreviewKind(file);

  const [downloading, setDownloading] = React.useState(false);

  const handleDownload = async () => {
    if (!actionable || downloading || !file.fileId) return;
    setDownloading(true);
    try {
      await downloadAuthenticatedFile(API_ENDPOINTS.files.download(file.fileId), file.name);
    } finally {
      setDownloading(false);
    }
  };

  const viewTitle = !stored || busy
    ? t('attachment.notUploadedYet')
    : previewable
      ? t('attachment.view')
      : t('attachment.notPreviewable');

  const Icon = busy
    ? Loader2
    : kind === 'image'
      ? ImageIcon
      : kind === 'pdf' || kind === 'text'
        ? FileText
        : Paperclip;

  return (
    <span
      data-testid={testId}
      className={
        variant === 'card'
          ? 'inline-flex w-full max-w-[320px] items-center gap-2.5 rounded-xl border border-gray-200 bg-white px-3 py-2.5 text-xs'
          : 'inline-flex max-w-[260px] items-center gap-2 rounded-lg border border-gray-200 bg-gray-50/70 px-2.5 py-1.5 text-xs'
      }
    >
      <Icon
        className={`h-4 w-4 shrink-0 ${busy ? 'animate-spin text-gray-400' : kind === 'image' ? 'text-violet-500' : 'text-gray-400'}`}
      />

      {/* The name is the primary way in: clicking it opens the file, exactly as it does in a mail
          client. It is a real button so keyboard and screen-reader users get the same route. */}
      <span className="min-w-0 flex-1">
        <button
          type="button"
          onClick={() => previewable && onPreview(file)}
          disabled={!previewable}
          title={previewable ? `${file.name} — ${t('attachment.view')}` : `${file.name} — ${viewTitle}`}
          aria-label={t('attachment.viewName', { name: file.name })}
          data-testid={testId ? `${testId}-name` : undefined}
          className="block w-full truncate text-left font-semibold text-gray-700 outline-none hover:text-[#004c91] hover:underline focus-visible:ring-2 focus-visible:ring-[#004c91]/40 disabled:cursor-default disabled:no-underline disabled:hover:text-gray-700"
        >
          {file.name}
        </button>
        <span className="block truncate text-[10px] text-gray-400">
          {busy
            ? t('attachment.uploading')
            : status === 'failed'
              ? t('attachment.uploadFailed')
              : [hint, file.size != null ? formatFileSize(file.size) : null].filter(Boolean).join(' · ')}
        </span>
      </span>

      <button
        type="button"
        onClick={() => previewable && onPreview(file)}
        disabled={!previewable}
        title={viewTitle}
        aria-label={t('attachment.viewName', { name: file.name })}
        data-testid={testId ? `${testId}-view` : undefined}
        className="shrink-0 rounded p-0.5 text-gray-400 outline-none hover:text-[#004c91] focus-visible:ring-2 focus-visible:ring-[#004c91]/40 disabled:opacity-40 disabled:hover:text-gray-400"
      >
        <Eye className="h-3.5 w-3.5" />
      </button>

      <button
        type="button"
        onClick={() => void handleDownload()}
        disabled={!actionable || downloading}
        title={actionable ? t('attachment.download') : t('attachment.notUploadedYet')}
        aria-label={t('attachment.downloadName', { name: file.name })}
        data-testid={testId ? `${testId}-download` : undefined}
        className="shrink-0 rounded p-0.5 text-gray-400 outline-none hover:text-[#004c91] focus-visible:ring-2 focus-visible:ring-[#004c91]/40 disabled:opacity-40 disabled:hover:text-gray-400"
      >
        {downloading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
      </button>

      {required ? (
        <span
          title={t('attachment.requiredHint')}
          className="shrink-0 rounded bg-[#004c91]/10 px-1.5 py-0.5 text-[10px] font-bold uppercase text-[#004c91]"
        >
          {t('attachment.required')}
        </span>
      ) : onRemove ? (
        <button
          type="button"
          onClick={onRemove}
          title={t('attachment.remove')}
          aria-label={t('attachment.removeName', { name: file.name })}
          data-testid={testId ? `${testId}-remove` : undefined}
          className="shrink-0 rounded p-0.5 text-gray-400 outline-none hover:text-red-500 focus-visible:ring-2 focus-visible:ring-red-400/40"
        >
          <Trash2 className="h-3.5 w-3.5" />
        </button>
      ) : null}
    </span>
  );
}
