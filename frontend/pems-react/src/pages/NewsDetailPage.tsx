/**
 * Trang NewsDetailPage (Public)
 * Nội dung đọc trực tuyến hiển thị văn bản chi tiết bản tin.
 */

// Đây là trang hiển thị chi tiết một bài viết tin tức ở giao diện phía người dùng
import React, { useEffect, useState } from 'react';
import { Calendar, User, Image as ImageIcon } from 'lucide-react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { publicContentApi } from '../features/public-content/api/publicContentApi';
import { PublicNewsDetail, PublicNewsSection } from '../features/public-content/types/publicContent.types';
import { format } from 'date-fns';
import { vi } from 'date-fns/locale';
import { sanitizeHtml } from '../shared/security/sanitizeHtml';
import { resolveFileUrl } from '../shared/utils/resolveFileUrl';
import { useTranslation } from 'react-i18next';

export function NewsDetailPage() {
  const { t, i18n } = useTranslation(['news']);
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();

  const [article, setArticle] = useState<PublicNewsDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  // Follows the site-wide VI/EN switcher in the header — no separate per-article language
  // buttons; content is re-fetched in the requested language whenever it changes.
  useEffect(() => {
    if (!id) return;

    const fetchDetail = async () => {
      try {
        setLoading(true);
        const data = await publicContentApi.getPublicNewsDetail(id, i18n.language);
        setArticle(data);
      } catch (err) {
        console.error(err);
        setError(true);
      } finally {
        setLoading(false);
      }
    };

    fetchDetail();
  }, [id, i18n.language]);

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [id]);

  const handleBack = () => {
    const state = location.state as { returnTo?: string };
    if (state?.returnTo) {
      navigate(state.returnTo);
    } else {
      navigate(-1);
    }
  };

  if (loading) {
    return (
      <div className="pt-24 md:pt-28 pb-12 md:pb-20 bg-white min-h-screen flex flex-col items-center justify-center">
        <div className="w-12 h-12 border-4 border-[#004c91] border-t-transparent rounded-full animate-spin mb-4"></div>
        <p className="text-gray-500 font-medium">{t('news:detail.loadingArticle')}</p>
      </div>
    );
  }

  if (error || !article) {
    return (
      <div className="pt-24 md:pt-28 pb-12 md:pb-20 bg-white min-h-screen flex flex-col items-center justify-center p-6 text-center">
        <div className="w-16 h-16 bg-slate-200 text-slate-500 rounded-full flex items-center justify-center mb-4">
          <ImageIcon className="w-8 h-8" />
        </div>
        <h2 className="text-xl font-bold text-slate-800 mb-2">{t('news:detail.notFoundTitle')}</h2>
        <p className="text-slate-500 mb-6 max-w-md">
          {t('news:detail.notFoundMsg')}
        </p>
        <button 
          onClick={handleBack}
          className="px-6 py-2 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003b70] transition-colors"
        >
          {t('news:detail.goBack')}
        </button>
      </div>
    );
  }

  const formatDate = (iso: string) => {
    try {
      return format(new Date(iso), 'dd/MM/yyyy', { locale: vi });
    } catch {
      return iso;
    }
  };

  return (
    <div className="pt-24 md:pt-28 pb-12 md:pb-20 bg-white min-h-screen">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        
        {/* Breadcrumbs */}
        <div className="mb-6 flex flex-wrap items-center gap-2 text-sm font-semibold select-none text-slate-400 py-1">
          <button 
            onClick={handleBack} 
            className="hover:text-[#004c91] transition-colors flex items-center gap-1.5 cursor-pointer px-2 py-1 rounded-lg"
          >
            {t('news:detail.goBack')}
          </button>
          
          <span className="text-slate-300">/</span>
          
          <span className="text-slate-600 font-bold truncate max-w-[200px] sm:max-w-[400px]">
            {article.title}
          </span>
        </div>

        {/* Title */}
        <h1 className="text-3xl md:text-4xl font-bold text-gray-900 leading-tight mb-6 mt-0">
          {article.title}
        </h1>
        
        {/* Meta Info */}
        <div className="flex items-center flex-wrap gap-4 mb-6">
          {article.publishedAt && (
            <div className="flex items-center gap-1.5 text-gray-500 text-[15px]">
              <Calendar className="w-4 h-4" />
              <span>{formatDate(article.publishedAt)}</span>
            </div>
          )}
          {article.authorName && (
            <div className="flex items-center gap-1.5 text-gray-500 text-[15px]">
              <User className="w-4 h-4" />
              <span>{article.authorName}</span>
            </div>
          )}
        </div>

        {/* Light Gray Line */}
        <div className="h-[1px] bg-gray-200 w-full mb-8"></div>

        {/* Summary */}
        {article.summary && (
          <div className="flex mb-8">
            <div className="w-[3px] bg-[#f37021] shrink-0 mr-4 rounded-sm"></div>
            <p className="text-[17px] text-gray-700 italic leading-relaxed">
              {article.summary}
            </p>
          </div>
        )}

        {/* Main Image */}
        {article.thumbnailUrl && (
          <div className="mb-10 rounded-lg overflow-hidden">
            <img
              src={resolveFileUrl(article.thumbnailUrl) ?? undefined}
              alt={article.title}
              className="w-full max-h-[500px] object-cover"
            />
          </div>
        )}

        {/* Article Content */}
        <div className="news-content text-gray-800 space-y-8">
          <PublicNewsSections sections={article.sections} />
        </div>
        
        {/* Light Gray Line Bottom */}
        <div className="h-[1px] bg-gray-200 w-full mt-12 mb-6"></div>

        {/* Copyright */}
        <p className="text-[14px] text-gray-400 opacity-80 font-medium pb-8 border-b border-white">
          {t('news:detail.copyright')}
        </p>

      </div>
    </div>
  );
}

function PublicNewsSections({ sections }: { sections: PublicNewsSection[] }) {
  if (!sections || sections.length === 0) return null;

  return (
    <>
      {sections.map(section => {
        const bodyText = section.sectionBodyHtml || section.sectionBodyText;

        switch (section.sectionTitle?.toUpperCase()) {
          case 'HEADING':
            return <h3 key={section.sectionId} className="text-2xl font-bold text-gray-900 mt-10 mb-5">{section.sectionTitle || bodyText}</h3>;

          case 'QUOTE':
            return <blockquote key={section.sectionId} className="border-l-4 border-[#f37021] pl-5 italic text-slate-600 my-8 bg-orange-50 p-5 rounded-r-lg">{bodyText}</blockquote>;

          case 'IMAGE':
          case 'GALLERY':
            return (
              <div key={section.sectionId} className="my-8">
                {bodyText && (
                  <div 
                    className="text-lg leading-relaxed mb-5"
                    dangerouslySetInnerHTML={{ __html: sanitizeHtml(bodyText) }}
                  />
                )}
                {section.files && section.files.length > 0 && (
                  <div className={`grid gap-4 ${section.files.length > 1 ? 'grid-cols-2 md:grid-cols-3' : 'grid-cols-1'}`}>
                    {section.files.map(f => (
                      <img key={f.fileId} src={resolveFileUrl(f.url) ?? undefined} alt={f.fileName || ''} loading="lazy" className="rounded-lg w-full object-cover" />
                    ))}
                  </div>
                )}
              </div>
            );

          default:
            return (
              <div key={section.sectionId} className="my-5">
                {section.sectionTitle && <h3 className="text-xl font-bold text-slate-800 mb-4">{section.sectionTitle}</h3>}
                {bodyText && (
                  <div 
                    className="text-[1.05rem] leading-[1.8] whitespace-pre-line"
                    dangerouslySetInnerHTML={{ __html: sanitizeHtml(bodyText) }}
                  />
                )}
                {section.files && section.files.length > 0 && (
                  <div className={`grid gap-4 mt-6 ${section.files.length > 1 ? 'grid-cols-2 md:grid-cols-3' : 'grid-cols-1'}`}>
                    {section.files.map(f => (
                      <img key={f.fileId} src={resolveFileUrl(f.url) ?? undefined} alt={f.fileName || ''} loading="lazy" className="rounded-lg w-full object-cover" />
                    ))}
                  </div>
                )}
              </div>
            );
        }
      })}
    </>
  );
}
