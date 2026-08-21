/**
 * The callout-content editing modal (email callout frames plan).
 *
 * <b>Why this component never imports `EmailRichTextEditor`.</b> `EmailRichTextEditor.tsx` is what needs
 * the mini-editor — it opens this dialog to edit one callout's own content. If THIS file also imported
 * `EmailRichTextEditor` to render that mini-editor itself, the two modules would import each other (a real
 * cross-module cycle), which is exactly what was ruled out. Instead `EmailRichTextEditor.tsx` builds the
 * nested editor element itself — a same-module recursive JSX reference, not a cycle — and hands it to this
 * component as `children`. This file knows nothing about Quill, variables, or the conversion pipeline; it
 * is the same dumb modal shell `EmailTableDialog.tsx` already is for the table editor.
 */
import React from 'react';
import { X } from 'lucide-react';

export interface EmailCalloutContentDialogProps {
  children: React.ReactNode;
  onCancel: () => void;
  onApply: () => void;
}

export function EmailCalloutContentDialog({ children, onCancel, onApply }: EmailCalloutContentDialogProps) {
  return (
    <div
      className="fixed inset-0 z-[300] flex items-center justify-center bg-black/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Sửa nội dung khung"
    >
      <div className="flex max-h-full w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-3">
          <h3 className="text-sm font-bold text-gray-800">Sửa nội dung khung</h3>
          <button
            type="button"
            onClick={onCancel}
            title="Đóng"
            aria-label="Đóng"
            className="rounded-lg p-1.5 text-gray-400 hover:bg-gray-50 hover:text-gray-600"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">
          {children}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-100 px-5 py-3">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-semibold text-gray-600 hover:bg-gray-50"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={onApply}
            data-testid="callout-content-dialog-apply"
            className="rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-semibold text-white hover:bg-[#013565]"
          >
            Áp dụng
          </button>
        </div>
      </div>
    </div>
  );
}
