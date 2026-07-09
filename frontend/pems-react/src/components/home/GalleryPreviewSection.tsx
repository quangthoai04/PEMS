/**
 * Component GalleryPreviewSection
 * Xem trước Visit FPTU / Gallery — ảnh thật từ campus đầu tiên (hoặc campus của user nội bộ),
 * lấy qua GET /api/public/visit-fptu/campuses + .../navigation (không dùng mock).
 */

import React, { useEffect, useState } from 'react';
import { ArrowRight, Image as ImageIcon } from 'lucide-react';
import { Link } from 'react-router-dom';
import { publicVisitFptuApi } from '../../features/visit-fptu/publicVisitFptuApi';
import type { PublicGalleryLocation } from '../../features/visit-fptu/publicVisitFptu.types';
import { resolveFileUrl } from '../../shared/utils/resolveFileUrl';
import { useTranslation } from 'react-i18next';

interface GalleryPreviewSectionProps {
  /** campusCode ưu tiên (vd: campus của user nội bộ). Nếu không tìm thấy sẽ dùng campus đầu tiên. */
  preferredCampusCode?: string | null;
}

function LocationThumbnail({ loc }: { loc: PublicGalleryLocation }) {
  const [failed, setFailed] = useState(false);
  const src = !failed && loc.primaryMediaUrl ? resolveFileUrl(loc.primaryMediaUrl) : null;

  if (!src) {
    return (
      <div className="w-full h-full flex items-center justify-center">
        <ImageIcon className="w-8 h-8 text-slate-300" />
      </div>
    );
  }

  return (
    <img
      src={src}
      alt={loc.title}
      loading="lazy"
      onError={() => setFailed(true)}
      className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-700"
    />
  );
}

export function GalleryPreviewSection({ preferredCampusCode }: GalleryPreviewSectionProps) {
  const { t } = useTranslation(['home']);
  const [campusCode, setCampusCode] = useState<string | null>(null);
  const [items, setItems] = useState<PublicGalleryLocation[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const campuses = await publicVisitFptuApi.getCampuses();
        if (cancelled || campuses.length === 0) return;

        const target = (preferredCampusCode
          ? campuses.find((c) => c.campusCode === preferredCampusCode)
          : undefined) ?? campuses[0];

        setCampusCode(target.campusCode);

        const navigation = await publicVisitFptuApi.getNavigation(target.campusCode);
        if (cancelled) return;

        const locations = navigation.areas.flatMap((area) => area.locations);
        setItems(locations.slice(0, 8));
      } catch {
        /* trang chủ vẫn hiển thị bình thường khi API lỗi */
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => { cancelled = true; };
  }, [preferredCampusCode]);

  if (!loading && items.length === 0) return null;

  return (
    <section className="py-14 sm:py-20 lg:py-24 bg-white relative overflow-hidden">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-end justify-between mb-12">
          <div>
            <h2 className="text-3xl font-bold text-fpt-navy">{t('home:galleryPreview.title')}</h2>
            <div className="w-16 h-1.5 bg-fpt-orange mt-4 rounded-full"></div>
          </div>
          <Link
            to={campusCode ? `/visit-fptu/${campusCode}` : '/visit-fptu'}
            className="hidden sm:flex items-center gap-2 text-fpt-orange font-medium hover:text-fpt-orange-hover transition-colors"
          >
            {t('home:galleryPreview.viewAll')} <ArrowRight className="w-4 h-4" />
          </Link>
        </div>

        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 sm:gap-6">
          {loading
            ? Array.from({ length: 8 }).map((_, i) => (
                <div key={i} className="aspect-square rounded-2xl bg-gray-100 animate-pulse" />
              ))
            : items.map((loc) => (
                <Link
                  to={`/visit-fptu/${campusCode}`}
                  key={loc.galleryItemId}
                  className="group relative aspect-square rounded-2xl overflow-hidden bg-slate-100 shadow-sm hover:shadow-xl transition-all duration-300"
                >
                  <LocationThumbnail loc={loc} />
                  <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-black/0 to-transparent opacity-0 group-hover:opacity-100 transition-opacity" />
                  <div className="absolute bottom-0 left-0 right-0 p-3 translate-y-2 opacity-0 group-hover:translate-y-0 group-hover:opacity-100 transition-all">
                    <p className="text-white text-sm font-semibold line-clamp-1">{loc.locationName}</p>
                  </div>
                </Link>
              ))}
        </div>
      </div>
    </section>
  );
}
