/**
 * Trang NewsPage (Public)
 * Trang tin tức chính thức công cộng của trường.
 * Chỉ hiển thị bài viết đã PUBLISHED — dữ liệu thật từ GET /api/public/news.
 */

import React, { useEffect, useState } from 'react';
import { ChevronLeft, ChevronRight, Calendar, Newspaper, RefreshCw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { publicContentApi } from '../features/public-content/api/publicContentApi';
import { PublicNewsListItem } from '../features/public-content/types/publicContent.types';
import { resolveFileUrl } from '../shared/utils/resolveFileUrl';

const PAGE_SIZE = 5;
const FALLBACK_IMAGE =
  'data:image/svg+xml;utf8,' +
  encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="800" height="500"><rect width="100%" height="100%" fill="#eef5fa"/><text x="50%" y="50%" font-family="Arial" font-size="28" fill="#9db8cf" text-anchor="middle" dominant-baseline="middle">FPT University</text></svg>`
  );

function formatDate(iso?: string | null): string {
  if (!iso) return '';
  try {
    const d = new Date(iso);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
  } catch {
    return '';
  }
}

function ArticleImage({ item, className }: { item: PublicNewsListItem; className: string }) {
  const [failed, setFailed] = useState(false);
  const src = !failed && item.coverUrl ? resolveFileUrl(item.coverUrl) ?? FALLBACK_IMAGE : FALLBACK_IMAGE;
  return (
    <img
      src={src}
      alt={item.title}
      loading="lazy"
      onError={() => setFailed(true)}
      className={className}
    />
  );
}

export function NewsPage() {
  const [items, setItems] = useState<PublicNewsListItem[]>([]);
  const [latest, setLatest] = useState<PublicNewsListItem[]>([]);
  const [totalPages, setTotalPages] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  // Main paged list
  useEffect(() => {
    let cancelled = false;
    const fetchList = async () => {
      setLoading(true);
      setError(false);
      try {
        const data = await publicContentApi.getPublicNewsList({
          pageIndex: currentPage,
          pageSize: PAGE_SIZE,
        });
        if (cancelled) return;
        setItems(data.items ?? []);
        setTotalPages(data.totalPages ?? 0);
        // Sidebar "Bài viết mới nhất" = 3 bài mới nhất (trang 1)
        if (currentPage === 1) {
          setLatest((data.items ?? []).slice(0, 3));
        }
      } catch {
        if (!cancelled) setError(true);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    fetchList();
    return () => { cancelled = true; };
  }, [currentPage, reloadKey]);

  // Sidebar khi đang ở trang > 1 mà chưa có dữ liệu (vd. vào thẳng trang 2)
  useEffect(() => {
    if (latest.length > 0) return;
    let cancelled = false;
    publicContentApi
      .getPublicNewsList({ pageIndex: 1, pageSize: 3 })
      .then(data => { if (!cancelled) setLatest(data.items ?? []); })
      .catch(() => { /* sidebar là phụ — bỏ qua lỗi */ });
    return () => { cancelled = true; };
  }, [latest.length, reloadKey]);

  const handleNextPage = () => { if (currentPage < totalPages) setCurrentPage(currentPage + 1); };
  const handlePrevPage = () => { if (currentPage > 1) setCurrentPage(currentPage - 1); };

  return (
    <div className="pt-28 md:pt-36 pb-12 md:pb-20 bg-white">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-16">
          <h1 className="text-3xl md:text-3xl font-bold text-[#004c91] uppercase mb-4">
            Bảng tin
          </h1>
          <div className="w-24 h-1 bg-[#f37021] mx-auto rounded-full"></div>
        </div>

        {/* Body */}
        <div className="flex flex-col lg:flex-row gap-12 lg:gap-0">

          {/* Left Part: Articles */}
          <div className="lg:w-2/3 lg:pr-10 lg:border-r lg:border-gray-200">

            {loading ? (
              /* Loading skeleton — giữ đúng bố cục card */
              <div className="flex flex-col space-y-10">
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="flex flex-col sm:flex-row gap-6 items-start animate-pulse">
                    <div className="w-full sm:w-[300px] shrink-0">
                      <div className="aspect-[4/3] sm:aspect-[4/2.5] rounded-lg bg-gray-100" />
                    </div>
                    <div className="flex-1 w-full space-y-3 pt-1">
                      <div className="h-5 bg-gray-100 rounded w-11/12" />
                      <div className="h-4 bg-gray-100 rounded w-32" />
                      <div className="h-4 bg-gray-100 rounded w-full" />
                      <div className="h-4 bg-gray-100 rounded w-3/4" />
                    </div>
                  </div>
                ))}
              </div>
            ) : error ? (
              /* Error state */
              <div className="py-20 text-center">
                <p className="text-gray-700 font-bold mb-2">Không thể tải danh sách tin tức.</p>
                <p className="text-gray-500 text-sm mb-6">Vui lòng kiểm tra kết nối và thử lại.</p>
                <button
                  onClick={() => setReloadKey(k => k + 1)}
                  className="inline-flex items-center gap-2 px-5 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003b70] transition-colors"
                >
                  <RefreshCw className="w-4 h-4" /> Thử lại
                </button>
              </div>
            ) : items.length === 0 ? (
              /* Empty state */
              <div className="py-20 text-center">
                <div className="w-16 h-16 bg-slate-100 text-slate-400 rounded-full flex items-center justify-center mx-auto mb-4">
                  <Newspaper className="w-8 h-8" />
                </div>
                <p className="text-gray-700 font-bold mb-1">Chưa có tin tức nào được đăng.</p>
                <p className="text-gray-500 text-sm">Vui lòng quay lại sau.</p>
              </div>
            ) : (
              <div className="flex flex-col space-y-10">
                {items.map((article) => (
                  <Link to={`/news/${article.newsId}`} key={article.newsId} className="flex flex-col sm:flex-row gap-6 items-start group block">
                    {/* Image */}
                    <div className="w-full sm:w-[300px] shrink-0">
                      <div className="aspect-[4/3] sm:aspect-[4/2.5] rounded-lg overflow-hidden ring-1 ring-black/5 bg-gray-50">
                        <ArticleImage
                          item={article}
                          className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                        />
                      </div>
                    </div>
                    {/* Content */}
                    <div className="flex-1 flex flex-col">
                      <h2 className="text-[17px] font-bold text-gray-900 leading-snug uppercase group-hover:text-[#ea580c] transition-colors cursor-pointer">
                        {article.title}
                      </h2>
                      <div className="flex items-center gap-3 mt-3 mb-3">
                        {article.publishedAt && (
                          <div className="flex items-center gap-1.5 text-gray-500 text-[15px]">
                            <Calendar className="w-4 h-4" />
                            <span>{formatDate(article.publishedAt)}</span>
                          </div>
                        )}
                      </div>
                      {article.summary && (
                        <p className="text-[15px] text-gray-600 line-clamp-3 leading-relaxed">
                          {article.summary}
                        </p>
                      )}
                    </div>
                  </Link>
                ))}
              </div>
            )}

            {/* Pagination */}
            {!loading && !error && totalPages > 1 && (
              <div className="mt-12 flex items-center justify-center space-x-2">
                <button
                  onClick={handlePrevPage}
                  disabled={currentPage === 1}
                  className="p-2 border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  aria-label="Previous page"
                >
                  <ChevronLeft className="w-5 h-5 text-gray-600" />
                </button>

                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <button
                    key={page}
                    onClick={() => setCurrentPage(page)}
                    className={`w-10 h-10 rounded text-sm font-medium transition-colors ${
                      currentPage === page
                        ? 'bg-[#f37021] text-white border border-[#f37021]'
                        : 'border border-gray-300 text-gray-700 hover:bg-gray-50'
                    }`}
                  >
                    {page}
                  </button>
                ))}

                <button
                  onClick={handleNextPage}
                  disabled={currentPage === totalPages}
                  className="p-2 border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  aria-label="Next page"
                >
                  <ChevronRight className="w-5 h-5 text-gray-600" />
                </button>
              </div>
            )}
          </div>

          {/* Right Part: Latest Articles */}
          <div className="lg:w-1/3 lg:pl-10">
            <h3 className="text-xl font-bold text-gray-900 mb-3">
              Bài viết mới nhất
            </h3>
            <div className="h-0.5 w-full bg-[#ea580c] mb-6"></div>

            {latest.length === 0 && !loading ? (
              <p className="text-sm text-gray-400">Chưa có bài viết.</p>
            ) : (
              <div className="flex flex-col space-y-6">
                {latest.map((article) => (
                  <Link to={`/news/${article.newsId}`} key={`latest-${article.newsId}`} className="flex gap-4 items-start group cursor-pointer border-b border-gray-100 pb-4 last:border-0 last:pb-0 block">
                    {/* Thumbnail */}
                    <div className="w-[100px] h-[100px] shrink-0 rounded-lg overflow-hidden bg-gray-50">
                      <ArticleImage
                        item={article}
                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                      />
                    </div>
                    {/* Content */}
                    <div className="flex-1 flex flex-col pt-1">
                      <h4 className="text-[15px] font-bold text-gray-900 group-hover:text-[#ea580c] transition-colors leading-snug line-clamp-3">
                        {article.title}
                      </h4>
                      <div className="flex items-center gap-2 mt-3">
                        {article.publishedAt && (
                          <div className="flex items-center gap-1.5 text-gray-500 text-[13px]">
                            <Calendar className="w-3.5 h-3.5" />
                            <span>{formatDate(article.publishedAt)}</span>
                          </div>
                        )}
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </div>

        </div>
      </div>
    </div>
  );
}
