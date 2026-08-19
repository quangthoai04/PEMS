import React from 'react';
import { X, Loader2, AlertTriangle, Info, CheckCircle2 } from 'lucide-react';

interface ConfirmModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  variant?: 'danger' | 'warning' | 'default' | 'success';
  isLoading?: boolean;
  hideCancel?: boolean;
}

export function ConfirmModal({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmText = 'Xác nhận',
  cancelText = 'Hủy',
  variant = 'default',
  isLoading = false,
  hideCancel = false
}: ConfirmModalProps) {
  if (!isOpen) return null;

  const getVariantStyles = () => {
    switch (variant) {
      case 'danger':
        return {
          icon: <AlertTriangle className="w-6 h-6 text-red-600" />,
          bgIcon: 'bg-red-100',
          btnConfirm: 'bg-red-600 hover:bg-red-700 text-white',
        };
      case 'warning':
        return {
          icon: <AlertTriangle className="w-6 h-6 text-yellow-600" />,
          bgIcon: 'bg-yellow-100',
          btnConfirm: 'bg-yellow-600 hover:bg-yellow-700 text-white',
        };
      case 'success':
        return {
          icon: <CheckCircle2 className="w-6 h-6 text-green-600" />,
          bgIcon: 'bg-green-100',
          btnConfirm: 'bg-green-600 hover:bg-green-700 text-white',
        };
      default:
        return {
          icon: <Info className="w-6 h-6 text-blue-600" />,
          bgIcon: 'bg-blue-100',
          btnConfirm: 'bg-[#004c91] hover:bg-[#013565] text-white',
        };
    }
  };

  const styles = getVariantStyles();

  return (
    <div className="fixed inset-0 z-[200] flex items-center justify-center p-4 bg-black/50 backdrop-blur-[2px]" onMouseDown={onClose}>
      <div
        // Overlay is a flat p-4 (2rem vertical gutter). `message` allows multi-line free text
        // (whitespace-pre-line), so without a height cap + its own scroll region a long message
        // could push the confirm/cancel buttons past the visible viewport on a short screen.
        className="bg-white rounded-xl shadow-xl w-full max-w-md overflow-hidden transform transition-all animate-in fade-in zoom-in-95 duration-200 flex flex-col max-h-[calc(100dvh-2rem)]"
        onMouseDown={e => e.stopPropagation()}
      >
        <div className="p-6 overflow-y-auto min-h-0">
          <div className="flex items-start gap-4">
            <div className={`shrink-0 flex items-center justify-center w-12 h-12 rounded-full ${styles.bgIcon}`}>
              {styles.icon}
            </div>
            <div className="flex-1 mt-1">
              <h3 className="text-lg font-bold text-gray-900 leading-none mb-2">{title}</h3>
              <p className="text-sm text-gray-600 whitespace-pre-line break-words">{message}</p>
            </div>
            <button
              onClick={onClose}
              className="shrink-0 text-gray-400 hover:text-gray-500 hover:bg-gray-100 p-1 rounded-lg transition-colors"
              disabled={isLoading}
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>
        <div className="bg-gray-50 px-6 py-4 flex items-center justify-end gap-3 border-t border-gray-100 shrink-0">
          {!hideCancel && (
            <button
              type="button"
              onClick={onClose}
              disabled={isLoading}
              className="px-4 py-2 text-sm font-semibold text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50"
            >
              {cancelText}
            </button>
          )}
          <button
            type="button"
            onClick={onConfirm}
            disabled={isLoading}
            className={`flex items-center gap-2 px-4 py-2 text-sm font-semibold rounded-lg transition-colors shadow-sm disabled:opacity-60 ${styles.btnConfirm}`}
          >
            {isLoading && <Loader2 className="w-4 h-4 animate-spin" />}
            {confirmText}
          </button>
        </div>
      </div>
    </div>
  );
}
