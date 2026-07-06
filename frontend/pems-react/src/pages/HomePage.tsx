/**
 * Trang HomePage
 * Cổng vào hệ thống — rẽ nhánh theo trạng thái đăng nhập:
 *  - Chưa đăng nhập / VISITOR → PublicHomePage (landing page quốc tế).
 *  - Role nội bộ đã đăng nhập → InternalHomePage (cổng vào nội bộ, không phải Dashboard).
 */

import React from 'react';
import { useAuth } from '../shared/hooks/useAuth';
import { PublicHomePage } from './PublicHomePage';
import { InternalHomePage } from './InternalHomePage';

export function HomePage() {
  const { user, isAuthenticated, isLoading } = useAuth();

  // Trong lúc auth đang bootstrap (chỉ xảy ra khi có access token cũ), vẫn hiển thị
  // Public Homepage để tránh chớp màn hình loading cho phần lớn khách truy cập ẩn danh.
  if (isLoading || !isAuthenticated || !user || user.roleCode === 'VISITOR') {
    return <PublicHomePage />;
  }

  return <InternalHomePage user={user} />;
}
