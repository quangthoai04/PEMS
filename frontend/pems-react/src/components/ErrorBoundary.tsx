/**
 * Component ErrorBoundary
 * Lưới an toàn cuối cùng: bắt mọi lỗi render trong cây React để hệ thống
 * không bao giờ còn rơi vào màn hình trắng. Đây KHÔNG phải lỗi đăng nhập —
 * người dùng vẫn có thể quay lại trang chính, đăng xuất hoặc tải lại trang.
 */

import React from 'react';
import { authStorage } from '../shared/auth/authStorage';

interface ErrorBoundaryProps {
  children: React.ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  // Declared explicitly because this project ships no `@types/react`, so the
  // React.Component generic does not surface `state`/`props` to the type checker.
  declare props: ErrorBoundaryProps;
  state: ErrorBoundaryState = { hasError: false, error: null };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    // Chỉ log chi tiết trong môi trường phát triển — không lộ stack trace ra UI production.
    if (import.meta.env.DEV) {
      // eslint-disable-next-line no-console
      console.error('[ErrorBoundary] Render error captured:', error, errorInfo);
    }
  }

  private handleGoHome = () => {
    window.location.assign('/dashboard');
  };

  private handleLogout = () => {
    authStorage.clear();
    window.location.assign('/');
  };

  private handleReload = () => {
    window.location.reload();
  };

  render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <div className="min-h-screen flex items-center justify-center bg-[#fafafa] px-4">
        <div className="text-center max-w-md">
          <div className="w-16 h-16 mx-auto rounded-2xl bg-amber-50 text-amber-500 flex items-center justify-center mb-5 text-3xl">
            ⚠️
          </div>
          <h1 className="text-2xl font-black text-[#004c91] mb-2">Đã xảy ra lỗi khi hiển thị màn hình</h1>
          <p className="text-gray-700 font-semibold mb-1">Đây không phải lỗi đăng nhập.</p>
          <p className="text-gray-500 text-sm mb-6">
            Bạn có thể quay lại trang chính, đăng xuất hoặc tải lại trang để tiếp tục.
          </p>

          {import.meta.env.DEV && this.state.error && (
            <pre className="text-left text-[11px] text-red-600 bg-red-50 border border-red-100 rounded-lg p-3 mb-6 overflow-auto max-h-40 whitespace-pre-wrap">
              {this.state.error.message}
            </pre>
          )}

          <div className="flex flex-col sm:flex-row items-center justify-center gap-3">
            <button
              onClick={this.handleGoHome}
              className="px-5 py-2.5 bg-[#004c91] hover:bg-[#003a6f] text-white rounded-xl font-bold text-sm w-full sm:w-auto"
            >
              Về trang chính của tôi
            </button>
            <button
              onClick={this.handleReload}
              className="px-5 py-2.5 border border-gray-300 text-gray-700 rounded-xl font-bold text-sm hover:bg-gray-50 w-full sm:w-auto"
            >
              Tải lại trang
            </button>
            <button
              onClick={this.handleLogout}
              className="px-5 py-2.5 border border-red-200 text-red-600 rounded-xl font-bold text-sm hover:bg-red-50 w-full sm:w-auto"
            >
              Đăng xuất
            </button>
          </div>
        </div>
      </div>
    );
  }
}

export default ErrorBoundary;
