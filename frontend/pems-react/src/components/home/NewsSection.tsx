/**
 * Component NewsSection
 * Khu vực hiển thị tin tức sự kiện nổi bật trên trang chủ.
 * Lấy 3 bài PUBLISHED mới nhất từ GET /api/public/news (không dùng mock).
 */

import React, { useEffect, useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import newsPattern from '../../assets/images/news_pattern.svg';
import { publicContentApi } from '../../features/public-content/api/publicContentApi';
import { PublicNewsListItem } from '../../features/public-content/types/publicContent.types';
import { resolveFileUrl } from '../../shared/utils/resolveFileUrl';

const FALLBACK_IMAGE =
  'data:image/svg+xml;utf8,' +
  encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="800" height="500"><rect width="100%" height="100%" fill="#eef5fa"/><text x="50%" y="50%" font-family="Arial" font-size="28" fill="#9db8cf" text-anchor="middle" dominant-baseline="middle">FPT University</text></svg>`
  );

function formatDate(iso?: string | null): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
  } catch {
    return '';
  }
}

function CardImage({ item }: { item: PublicNewsListItem }) {
  const [failed, setFailed] = useState(false);
  const src = !failed && item.coverUrl ? resolveFileUrl(item.coverUrl) ?? FALLBACK_IMAGE : FALLBACK_IMAGE;
  return (
    <img
      src={src}
      alt={item.title}
      loading="lazy"
      onError={() => setFailed(true)}
      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-700 ease-in-out"
    />
  );
}

export function NewsSection() {
  const [items, setItems] = useState<PublicNewsListItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    publicContentApi
      .getPublicNewsList({ pageIndex: 1, pageSize: 3 })
      .then(data => { if (!cancelled) setItems(data.items ?? []); })
      .catch(() => { /* trang chủ vẫn hiển thị bình thường khi API lỗi */ })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  // Không có bài published nào → ẩn cả section để trang chủ gọn gàng.
  if (!loading && items.length === 0) return null;

  return (
    <section id="news" className="py-24 bg-slate-50/80 border-y border-gray-100 relative overflow-hidden">
      {/* Decorative Background Elements */}
      <div className="absolute inset-0 z-0 pointer-events-none opacity-50" style={{ backgroundImage: `url(${newsPattern})`, backgroundSize: '600px 400px' }}></div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
        <div className="flex items-end justify-between mb-12">
          <div>
            <h2 className="text-3xl font-bold text-fpt-navy">Tin Tức Nổi Bật</h2>
            <div className="w-16 h-1.5 bg-fpt-orange mt-4 rounded-full"></div>
          </div>
          <Link to="/news" className="hidden sm:flex items-center gap-2 text-fpt-orange font-medium hover:text-fpt-orange-hover transition-colors">
            Xem tất cả <ArrowRight className="w-4 h-4" />
          </Link>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
          {loading
            ? Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="bg-white border border-gray-100 rounded-2xl overflow-hidden shadow-sm animate-pulse">
                  <div className="h-56 bg-gray-100" />
                  <div className="p-6 space-y-3">
                    <div className="h-5 bg-gray-100 rounded w-11/12" />
                    <div className="h-4 bg-gray-100 rounded w-full" />
                    <div className="h-4 bg-gray-100 rounded w-2/3" />
                  </div>
                </div>
              ))
            : items.map((item) => (
                <Link to={`/news/${item.newsId}`} key={item.newsId} className="group bg-white border border-gray-100 rounded-2xl overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col">
                  <div className="relative h-56 overflow-hidden bg-gray-50">
                    <CardImage item={item} />
                    {item.publishedAt && (
                      <div className="absolute top-4 left-4 bg-white/90 backdrop-blur px-3 py-1 rounded-full text-xs font-semibold text-fpt-navy shadow-sm">
                        {formatDate(item.publishedAt)}
                      </div>
                    )}
                  </div>
                  <div className="p-6 flex flex-col flex-grow">
                    <h3 className="text-lg font-bold text-gray-900 group-hover:text-fpt-orange transition-colors line-clamp-2 mb-3">
                      {item.title}
                    </h3>
                    {item.summary && (
                      <p className="text-gray-600 text-sm leading-relaxed line-clamp-3 mb-6">
                        {item.summary}
                      </p>
                    )}
                    <div className="mt-auto">
                      <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-fpt-navy group-hover:text-fpt-orange transition-colors">
                        Đọc tiếp <ArrowRight className="w-4 h-4 translate-x-0 group-hover:translate-x-1 transition-transform" />
                      </span>
                    </div>
                  </div>
                </Link>
              ))}
        </div>
      </div>
    </section>
  );
}
