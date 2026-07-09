import React, { Component, ErrorInfo, ReactNode } from 'react';
import i18n from '../../shared/i18n/config';

interface Props {
  children?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    error: null
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Uncaught error:', error, errorInfo);
  }

  public render() {
    if (this.state.hasError) {
      return (
        <div className="flex flex-col items-center justify-center min-h-[70vh] p-8 text-center bg-white rounded-lg shadow-sm border border-gray-100 m-4">
          <div className="w-16 h-16 bg-red-100 rounded-full flex items-center justify-center mb-4">
            <span className="text-red-500 text-2xl">⚠️</span>
          </div>
          <h2 className="text-xl font-bold text-gray-800 mb-2">{i18n.t('errors:boundary.title', 'Đã xảy ra lỗi khi tải màn hình')}</h2>
          <p className="text-gray-500 mb-6 max-w-md">
            {i18n.t('errors:boundary.message', 'Vui lòng thử lại hoặc quay về dashboard.')}
          </p>
          <div className="flex gap-4">
            <button
              onClick={() => {
                this.setState({ hasError: false, error: null });
                window.location.reload();
              }}
              className="px-5 py-2 bg-gray-100 text-gray-700 rounded-lg hover:bg-gray-200 transition-colors font-medium"
            >
              {i18n.t('errors:boundary.reload', 'Tải lại trang')}
            </button>
            <button
              onClick={() => {
                this.setState({ hasError: false, error: null });
                window.location.href = '/dashboard';
              }}
              className="px-5 py-2 bg-[#004c91] text-white rounded-lg hover:bg-blue-800 transition-colors font-medium"
            >
              {i18n.t('errors:boundary.backToDashboard', 'Quay về Dashboard')}
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
