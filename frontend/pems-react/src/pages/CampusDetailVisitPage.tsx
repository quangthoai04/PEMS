/**
 * Trang CampusDetailVisitPage (Public)
 * Hiển thị VisitFPTU Gallery công khai của một campus theo 2 tầng (UC_Public_VisitFPTU_Gallery_Location_Grid):
 *   Tầng 1 — LOCATION_GRID: click một vị trí → lưới toàn bộ gallery item của vị trí đó (mỗi card = media chính).
 *   Tầng 2 — ITEM_DETAIL: click một card → chi tiết gallery item (breadcrumb, title, mô tả, toàn bộ media).
 * Dữ liệu lấy từ public API. Không có virtual tour 360 (BR-PGAL-17).
 */

import React, { useState, useEffect, useLayoutEffect, useCallback, useMemo, useRef } from "react";
import { useParams, useNavigate, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { motion, AnimatePresence } from "motion/react";
import {
  ChevronRight,
  ChevronLeft,
  ChevronUp,
  ChevronDown,
  MapPin,
  Image as ImageIcon,
  Video as VideoIcon,
  ZoomIn,
  X,
  ArrowLeft,
  Share2,
  Facebook,
  Twitter,
  Link as LinkIcon,
  Loader2,
  ImageOff,
  Volume2,
  VolumeX,
  Languages,
  Play,
  Users,
} from "lucide-react";

import bgHN from "../assets/FPTbanner_visit/hola_new.jpg";
import bgHCM from "../assets/FPTbanner_visit/HCM.png";
import bgCT from "../assets/FPTbanner_visit/CanTho.png";
import bgDN from "../assets/FPTbanner_visit/DaNang.png";
import bgQN from "../assets/FPTbanner_visit/QuyNhon.png";

import { youtubeEmbedUrl } from "../shared/utils/youtube";
import {
  buildGalleryShareUrl,
  copyTextToClipboard,
  openFacebookShare,
} from "../shared/utils/galleryShare";
import { showMessageErrorToast, showSuccessToast } from "../shared/utils/toast";
import { localizedDbText, isEnglishLanguage } from "../shared/i18n/localizedDbText";
import { publicVisitFptuApi } from "../features/visit-fptu/publicVisitFptuApi";
import type {
  PublicGalleryArea,
  PublicGalleryGridItem,
  PublicGalleryItemDetail,
  PublicGalleryShowcaseItem,
  PublicLocationGalleryGrid,
  PublicLocationShowcase,
  PublicGalleryNavigation,
} from "../features/visit-fptu/publicVisitFptu.types";

// Local fallback hero artwork + descriptions, keyed by the route id (campus code is the real source).
const CAMPUS_FALLBACK: Record<string, { bg: string; description: string }> = {
  hn: {
    bg: bgHN,
    description:
      "Đại học FPT Hà Nội tọa lạc tại Khu Công nghệ cao Hòa Lạc. Kiến trúc hiện đại, không gian xanh và cơ sở vật chất tiên tiến cho môi trường học tập tối ưu.",
  },
  hcm: {
    bg: bgHCM,
    description:
      "Đại học FPT TP. HCM mang đến môi trường học thuật sôi động với công nghệ hiện đại và cộng đồng sinh viên năng động.",
  },
  dn: {
    bg: bgDN,
    description: "Tọa lạc tại FPT City Đà Nẵng, môi trường học tập hiện đại hòa mình cùng thiên nhiên.",
  },
  ct: {
    bg: bgCT,
    description: "Đại học FPT Cần Thơ — trung tâm công nghệ và giáo dục ở vùng đồng bằng sông Cửu Long.",
  },
  qn: {
    bg: bgQN,
    description: "Đại học FPT Quy Nhơn chú trọng AI và toán học, xây dựng tại thành phố biển xinh đẹp.",
  },
};

const isVideo = (mediaType?: string) => (mediaType || "").toUpperCase() === "VIDEO";

const mediaKindLabel = (kind: string | undefined, t: any) => {
  const k = (kind || "").toUpperCase();
  if (k === "VIDEO") return t("visitFptu:gallery.mediaKinds.video");
  if (k === "MIXED") return t("visitFptu:gallery.mediaKinds.mixed");
  return t("visitFptu:gallery.mediaKinds.image");
};

/** Resolves the media URL through the dev server (vite proxies /api → backend). URLs come absolute. */
const mediaSrc = (url?: string | null) => url || "";

/**
 * One card in the location album grid (museum-style): primary media on a 16/10 frame, gradient bottom,
 * media-kind pill, title, and a centered "Xem chi tiết" CTA on hover (BR-PGAL-GRID-02/05/06).
 */
function GridCard({ item, onOpen }: { item: PublicGalleryGridItem; onOpen: () => void }) {
  const { t, i18n } = useTranslation(['visitFptu']);
  const language = i18n.resolvedLanguage ?? i18n.language;
  // DB-backed bilingual strings: EN when the header language is EN and a translation exists, else VI.
  const displayTitle = localizedDbText(item.title, item.titleEn, language);
  const [failed, setFailed] = useState(false);
  const pm = item.primaryMedia;
  const video = isVideo(pm?.mediaType);
  // For a video card prefer the thumbnail; fall back to the video element itself if no thumbnail.
  const imgSrc = video ? pm?.thumbnailUrl : pm?.url;

  return (
    <button
      onClick={onOpen}
      title={displayTitle}
      className="group relative aspect-[16/10] rounded-[20px] overflow-hidden cursor-pointer outline-none bg-white/[0.06] shadow-[0_18px_45px_rgba(0,0,0,0.28)] transition-all duration-300 hover:-translate-y-1.5 hover:shadow-[0_28px_70px_rgba(0,0,0,0.42)] focus-visible:ring-2 focus-visible:ring-fpt-orange"
    >
      {!pm || failed ? (
        <div className="absolute inset-0 flex items-center justify-center text-white/40">
          <ImageOff className="w-9 h-9" />
        </div>
      ) : imgSrc ? (
        <img
          src={mediaSrc(imgSrc)}
          alt={pm.altText || displayTitle}
          loading="lazy"
          onError={() => setFailed(true)}
          className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-[1.08]"
        />
      ) : (
        <video
          src={mediaSrc(pm.url)}
          muted
          playsInline
          preload="metadata"
          onError={() => setFailed(true)}
          className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-[1.08]"
        />
      )}

      {/* Bottom gradient for legibility */}
      <div className="absolute inset-0 bg-gradient-to-t from-black/75 via-black/10 to-transparent pointer-events-none" />

      {/* Media-kind pill (top-left) */}
      <span className="absolute top-3 left-3 z-10 inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-slate-900/60 backdrop-blur-md text-white text-[11px] font-bold border border-white/20">
        {video ? <VideoIcon className="w-3 h-3" /> : <ImageIcon className="w-3 h-3" />}
        {mediaKindLabel(item.mediaKind, t)}
      </span>

      {/* Idle: play icon for videos (hidden on hover, replaced by CTA) */}
      {video && (
        <div className="absolute inset-0 flex items-center justify-center pointer-events-none opacity-100 group-hover:opacity-0 transition-opacity duration-200">
          <span className="w-12 h-12 rounded-full bg-black/45 backdrop-blur-md border border-white/40 flex items-center justify-center text-white">
            <Play className="w-5 h-5 ml-0.5" fill="currentColor" />
          </span>
        </div>
      )}

      {/* Hover CTA: darken + "Xem chi tiết" */}
      <div className="absolute inset-0 z-[2] flex items-center justify-center bg-black/20 opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none">
        <span className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-full bg-fpt-orange/95 text-white text-sm font-extrabold shadow-[0_12px_28px_rgba(243,112,33,0.32)]">
          <ZoomIn className="w-4 h-4" /> {t('visitFptu:gallery.actions.viewDetails')}
        </span>
      </div>

      {/* Title at the bottom */}
      <div className="absolute inset-x-0 bottom-0 z-[3] p-3 text-left pointer-events-none">
        <p className="text-white text-sm font-bold leading-tight line-clamp-2 drop-shadow">{displayTitle}</p>
      </div>
    </button>
  );
}

/**
 * Area/Location Showcase fullscreen background. For an IMAGE cover: a plain <img>, falling back to the
 * campus artwork (AC-PGAL-AREA-12). For a VIDEO cover (new area MP4): an autoplay/muted/loop/playsInline
 * <video> that fades in only after `canplay`, over the campus artwork as a gradient/fallback — a broken
 * or undecodable video never crashes the page (it just keeps the fallback). Only the selected area's
 * video is mounted; switching areas remounts it (`key={src}`), so the old clip is torn down and stopped,
 * and the video is paused when the tab is hidden. The failed flag resets on src change.
 */
function ShowcaseBackground({
  src,
  fallbackSrc,
  alt,
  mediaType,
}: {
  src?: string | null;
  fallbackSrc: string;
  alt: string;
  mediaType?: 'IMAGE' | 'VIDEO';
}) {
  const [failed, setFailed] = useState(false);
  const [videoReady, setVideoReady] = useState(false);
  const videoRef = useRef<HTMLVideoElement | null>(null);

  useEffect(() => {
    setFailed(false);
    setVideoReady(false);
  }, [src]);

  // Pause THIS clip when its source changes or the showcase unmounts (§15.4). The element is captured at
  // effect setup — NOT read from the ref at teardown — because `<video key={src}>` remounts on an area
  // switch, so by cleanup time the ref already points at the NEXT area's video; touching that one would
  // blank the new video (both old and new would go dark). We only pause (reversible, StrictMode-safe);
  // the key-based remount is what releases the old clip's buffer when its element unmounts.
  useEffect(() => {
    const video = videoRef.current;
    return () => {
      video?.pause();
    };
  }, [src]);

  const isVideo = mediaType === 'VIDEO' && !!src && !failed;

  // Pause when the tab is hidden; resume when it becomes visible again (§15.5). Video only.
  useEffect(() => {
    if (!isVideo) return;
    const onVis = () => {
      const video = videoRef.current;
      if (!video) return;
      if (document.hidden) video.pause();
      else video.play().catch(() => { /* autoplay may be blocked; ignore */ });
    };
    document.addEventListener('visibilitychange', onVis);
    return () => document.removeEventListener('visibilitychange', onVis);
  }, [isVideo, src]);

  if (isVideo) {
    return (
      <>
        {/* Gradient / fallback artwork shown until the video is ready (and if it ever fails). */}
        <img
          src={fallbackSrc}
          alt={alt}
          className="absolute inset-0 w-full h-full object-cover object-center z-0"
        />
        <video
          key={src ?? undefined}
          ref={videoRef}
          src={src ?? undefined}
          autoPlay
          muted
          loop
          playsInline
          preload="auto"
          onCanPlay={async () => {
            const video = videoRef.current;
            if (!video) return;
            try {
              await video.play();
              setVideoReady(true);
            } catch {
              setVideoReady(false);
            }
          }}
          onError={() => {
            setFailed(true);
            setVideoReady(false);
          }}
          className={`absolute inset-0 w-full h-full object-cover object-center z-0 transition-opacity duration-500 ${
            videoReady ? 'opacity-100' : 'opacity-0'
          }`}
        />
      </>
    );
  }

  const finalSrc = !src || failed ? fallbackSrc : src;
  return (
    <img
      src={finalSrc}
      alt={alt}
      onError={() => setFailed(true)}
      className="absolute inset-0 w-full h-full object-cover object-center z-0"
    />
  );
}

/**
 * One location cover thumbnail in the Area Showcase rail (placeholder when the cover is missing/broken).
 * Active and inactive render identically (same size, same object-cover fill) — the "active" emphasis is
 * done purely on the button wrapper (scale/lift + glow), so the image itself never changes shape.
 */
function LocationThumbImage({ url, alt }: { url?: string | null; alt: string }) {
  const [failed, setFailed] = useState(false);
  if (!url || failed) {
    return (
      <div className="w-full h-full flex items-center justify-center bg-white/10 text-white/40">
        <ImageOff className="w-5 h-5" />
      </div>
    );
  }
  return (
    <img
      src={url}
      alt={alt}
      loading="lazy"
      onError={() => setFailed(true)}
      className="w-full h-full object-cover"
    />
  );
}

/** Primary-media thumbnail of a Location Showcase gallery item (image, or video poster + play badge). */
function ShowcaseItemThumb({ item }: { item: PublicGalleryShowcaseItem }) {
  const { i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTitle = localizedDbText(item.title, item.titleEn, language);
  const [failed, setFailed] = useState(false);
  const pm = item.primaryMedia;
  const isVid = (pm?.mediaType || "").toUpperCase() === "VIDEO";
  const src = isVid ? pm?.thumbnailUrl : pm?.thumbnailUrl || pm?.url;
  if (!pm || !src || failed) {
    return (
      <div className="w-full h-full flex items-center justify-center bg-white/10 text-white/40">
        {isVid ? <Play className="w-5 h-5" /> : <ImageOff className="w-5 h-5" />}
      </div>
    );
  }
  return (
    <div className="relative w-full h-full">
      <img
        src={src}
        alt={pm.altText || displayTitle}
        loading="lazy"
        onError={() => setFailed(true)}
        className="w-full h-full object-cover"
      />
      {isVid && (
        <span className="absolute inset-0 flex items-center justify-center pointer-events-none">
          <span className="w-6 h-6 rounded-full bg-black/50 border border-white/50 flex items-center justify-center text-white">
            <Play className="w-3 h-3 ml-0.5" fill="currentColor" />
          </span>
        </span>
      )}
    </div>
  );
}

/**
 * Reusable vertical thumbnail rail — used by the Location Showcase MEDIA column (same look/behaviour as the
 * Area Showcase rail: reveal-on-demand smooth scroll, white-glow active, up/down arrows, NN/MM counter).
 * Loop on the arrows is handled by the parent's onStep.
 */
function VerticalThumbRail({
  items,
  activeIndex,
  onSelect,
  onStep,
  renderThumb,
  keyOf,
  label,
  t,
}: {
  items: PublicGalleryShowcaseItem[];
  activeIndex: number;
  onSelect: (index: number) => void;
  onStep: (dir: -1 | 1) => void;
  renderThumb: (item: PublicGalleryShowcaseItem) => React.ReactNode;
  keyOf: (item: PublicGalleryShowcaseItem, index: number) => React.Key;
  label?: string;
  t: any;
}) {
  const railRef = useRef<HTMLDivElement | null>(null);
  const thumbRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const total = items.length;
  const safe = total > 0 ? Math.min(activeIndex, total - 1) : 0;

  useEffect(() => {
    const raf = requestAnimationFrame(() => {
      const rail = railRef.current;
      const el = thumbRefs.current[safe];
      if (!rail || !el) return;
      const pad = 12;
      const viewTop = rail.scrollTop;
      const viewBottom = viewTop + rail.clientHeight;
      const elTop = el.offsetTop;
      const elBottom = elTop + el.offsetHeight;
      const maxScroll = Math.max(0, rail.scrollHeight - rail.clientHeight);
      let target = viewTop;
      if (elTop < viewTop + pad) target = elTop - pad;
      else if (elBottom > viewBottom - pad) target = elBottom - rail.clientHeight + pad;
      target = Math.max(0, Math.min(target, maxScroll));
      if (target !== viewTop) rail.scrollTo({ top: target, behavior: "smooth" });
    });
    return () => cancelAnimationFrame(raf);
  }, [safe, total]);

  if (total === 0) return null;
  const pad2 = (n: number) => String(n).padStart(2, "0");

  return (
    <div className="flex flex-col items-center gap-3">
      <button
        onClick={() => onStep(-1)}
        title={t('visitFptu:gallery.actions.up')}
        className="w-10 h-10 rounded-full border border-white/30 bg-black/30 text-white flex items-center justify-center backdrop-blur-md transition-all hover:bg-white/20 hover:scale-105"
      >
        <ChevronUp className="w-5 h-5" />
      </button>
      {label && (
        <div className="text-white/85 text-[11px] font-bold uppercase tracking-[0.18em] text-center drop-shadow">
          {label}
        </div>
      )}
      <div
        ref={railRef}
        className="relative flex flex-col items-center gap-4 max-h-[min(52vh,404px)] overflow-y-auto px-6 py-4 [&::-webkit-scrollbar]:hidden [-ms-overflow-style:none] [scrollbar-width:none]"
      >
        {items.map((it, idx) => {
          const active = idx === safe;
          return (
            <button
              key={keyOf(it, idx)}
              ref={(el) => {
                thumbRefs.current[idx] = el;
              }}
              onClick={() => onSelect(idx)}
              className={`relative w-[72px] h-[72px] sm:w-[78px] sm:h-[78px] rounded-[10px] overflow-hidden cursor-pointer shrink-0 transition-all duration-300 ${
                active
                  ? "z-10 border-2 border-white opacity-100 scale-[1.14] shadow-[0_0_0_3px_rgba(255,255,255,0.20),0_0_26px_rgba(255,255,255,0.6),0_10px_24px_rgba(0,0,0,0.45)]"
                  : "border-2 border-white/25 opacity-65 hover:opacity-90 hover:border-white/50"
              }`}
            >
              {renderThumb(it)}
            </button>
          );
        })}
      </div>
      <button
        onClick={() => onStep(1)}
        title={t('visitFptu:gallery.actions.down')}
        className="w-10 h-10 rounded-full border border-white/30 bg-black/30 text-white flex items-center justify-center backdrop-blur-md transition-all hover:bg-white/20 hover:scale-105"
      >
        <ChevronDown className="w-5 h-5" />
      </button>
      <div className="mt-1 text-white font-extrabold tracking-[0.08em] text-sm text-center drop-shadow-[0_8px_20px_rgba(0,0,0,0.45)]">
        {pad2(safe + 1)}/{pad2(total)}
      </div>
    </div>
  );
}

/**
 * Horizontal mirror of {@link VerticalThumbRail} — used by the Location Showcase "Đoàn khách đã tới thăm"
 * row. Identical thumbnail size/style/gap/counter as the vertical MEDIA column, just laid out left→right
 * with `< >` arrows on the sides. Shows up to ~4 thumbnails and reveal-scrolls to keep the active one in view.
 */
function HorizontalThumbRail({
  items,
  activeIndex,
  onSelect,
  onStep,
  renderThumb,
  keyOf,
  title,
  t,
}: {
  items: PublicGalleryShowcaseItem[];
  activeIndex: number;
  onSelect: (index: number) => void;
  onStep: (dir: -1 | 1) => void;
  renderThumb: (item: PublicGalleryShowcaseItem) => React.ReactNode;
  keyOf: (item: PublicGalleryShowcaseItem, index: number) => React.Key;
  title?: string;
  t: any;
}) {
  const railRef = useRef<HTMLDivElement | null>(null);
  const thumbRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const total = items.length;
  const safe = total > 0 ? Math.min(activeIndex, total - 1) : 0;

  useEffect(() => {
    const raf = requestAnimationFrame(() => {
      const rail = railRef.current;
      const el = thumbRefs.current[safe];
      if (!rail || !el) return;
      const pad = 12;
      const viewLeft = rail.scrollLeft;
      const viewRight = viewLeft + rail.clientWidth;
      const elLeft = el.offsetLeft;
      const elRight = elLeft + el.offsetWidth;
      const maxScroll = Math.max(0, rail.scrollWidth - rail.clientWidth);
      let target = viewLeft;
      if (elLeft < viewLeft + pad) target = elLeft - pad;
      else if (elRight > viewRight - pad) target = elRight - rail.clientWidth + pad;
      target = Math.max(0, Math.min(target, maxScroll));
      if (target !== viewLeft) rail.scrollTo({ left: target, behavior: "smooth" });
    });
    return () => cancelAnimationFrame(raf);
  }, [safe, total]);

  if (total === 0) return null;
  const pad2 = (n: number) => String(n).padStart(2, "0");

  return (
    <div className="flex flex-col items-start">
      {title && (
        <div className="flex items-center gap-2 text-white font-bold text-sm mb-3 drop-shadow">
          <Users className="w-4 h-4" /> {title}
        </div>
      )}
      {/* Arrow-row + counter grouped and centred together (so the counter lines up under the thumbnails) */}
      <div className="flex flex-col items-center">
      <div className="flex items-center gap-3">
        <button
          onClick={() => onStep(-1)}
          title={t('visitFptu:gallery.actions.previous')}
          className="w-10 h-10 shrink-0 rounded-full border border-white/30 bg-black/30 text-white flex items-center justify-center backdrop-blur-md transition-all hover:bg-white/20 hover:scale-105"
        >
          <ChevronLeft className="w-5 h-5" />
        </button>
        {/* Exactly 4 thumbnails (78px·4 + gap·3 + px·2), the 5th clipped out; then reveal-scroll horizontally */}
        <div
          ref={railRef}
          className="relative flex flex-row items-center gap-4 max-w-[min(82vw,400px)] overflow-x-auto px-6 py-4 [&::-webkit-scrollbar]:hidden [-ms-overflow-style:none] [scrollbar-width:none]"
        >
          {items.map((it, idx) => {
            const active = idx === safe;
            return (
              <button
                key={keyOf(it, idx)}
                ref={(el) => {
                  thumbRefs.current[idx] = el;
                }}
                onClick={() => onSelect(idx)}
                className={`relative w-[72px] h-[72px] sm:w-[78px] sm:h-[78px] rounded-[10px] overflow-hidden cursor-pointer shrink-0 transition-all duration-300 ${
                  active
                    ? "z-10 border-2 border-white opacity-100 scale-[1.14] shadow-[0_0_0_3px_rgba(255,255,255,0.20),0_0_26px_rgba(255,255,255,0.6),0_10px_24px_rgba(0,0,0,0.45)]"
                    : "border-2 border-white/25 opacity-65 hover:opacity-90 hover:border-white/50"
                }`}
              >
                {renderThumb(it)}
              </button>
            );
          })}
        </div>
        <button
          onClick={() => onStep(1)}
          title={t('visitFptu:gallery.actions.next')}
          className="w-10 h-10 shrink-0 rounded-full border border-white/30 bg-black/30 text-white flex items-center justify-center backdrop-blur-md transition-all hover:bg-white/20 hover:scale-105"
        >
          <ChevronRight className="w-5 h-5" />
        </button>
      </div>
      <div className="mt-1 text-white font-extrabold tracking-[0.08em] text-sm text-center drop-shadow-[0_8px_20px_rgba(0,0,0,0.45)]">
        {pad2(safe + 1)}/{pad2(total)}
      </div>
      </div>
    </div>
  );
}

/**
 * Detail modal for a public gallery item (opened by clicking a MEDIA or "Đoàn khách" thumbnail). Faithful
 * re-creation of the original two-card detail design: left — a breadcrumb pill + share menu, a gradient
 * title with an orange underline, and a drop-cap description with a "Nghe thuyết minh" narration button and
 * a prev/next footer; right — the item's media carousel (image/video, prev/next, dots). Anonymous — media
 * come from the scoped public proxy. Loads the item's full media set via getGalleryItemDetail.
 */
function GalleryItemDetailModal({
  detail,
  isLoading,
  notFound,
  onClose,
  onPrev,
  onNext,
  hasNav,
  t,
  language,
}: {
  detail: PublicGalleryItemDetail | null;
  isLoading: boolean;
  notFound: boolean;
  onClose: () => void;
  onPrev: () => void;
  onNext: () => void;
  hasNav: boolean;
  t: any;
  /** Global header language (i18n.resolvedLanguage) — drives the modal's default VI/EN content tab. */
  language: string;
}) {
  const [idx, setIdx] = useState(0);
  const [failed, setFailed] = useState(false);
  const [showShareMenu, setShowShareMenu] = useState(false);
  const shareMenuRef = useRef<HTMLDivElement | null>(null);
  const [zoomOpen, setZoomOpen] = useState(false);
  const itemId = detail?.galleryItem.galleryItemId;

  // ── Bilingual content + audio (speaker icon) ──
  // The item carries a Vietnamese + English description and a ready-to-play audio recording for each.
  // The DEFAULT tab follows the global header language (VI header → VI tab, EN header → EN tab); the
  // user may still switch tabs manually, and that manual choice sticks until the global language or the
  // shown item changes. Switching language stops any playing audio and swaps description + audio URL —
  // it NEVER autoplays the other recording, refetches the item or resets the carousel.
  type GalleryLanguage = "vi" | "en";
  const globalGalleryLanguage: GalleryLanguage = isEnglishLanguage(language) ? "en" : "vi";
  const [selectedLanguage, setSelectedLanguage] = useState<GalleryLanguage>(globalGalleryLanguage);
  const [audioState, setAudioState] = useState<"idle" | "loading" | "playing" | "error">("idle");
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const activeContent =
    selectedLanguage === "vi" ? detail?.galleryItem.content?.vi : detail?.galleryItem.content?.en;

  // Localized DB-backed strings (breadcrumb + title). Fall back to VI when EN is missing.
  const displayAreaName = localizedDbText(detail?.area.areaName, detail?.area.areaNameEn, language);
  const displayLocationName = localizedDbText(
    detail?.location.locationName, detail?.location.locationNameEn, language);
  const displayItemTitle = localizedDbText(
    detail?.galleryItem.title, detail?.galleryItem.titleEn, language);

  const stopAudio = useCallback(() => {
    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current.removeAttribute("src");
      audioRef.current.load();
      audioRef.current = null;
    }
    setAudioState("idle");
  }, []);

  const toggleNarration = async () => {
    if (audioState === "playing" || audioState === "loading") {
      stopAudio();
      return;
    }
    const url = activeContent?.audioUrl;
    if (!url) {
      setAudioState("error");
      return;
    }
    setAudioState("loading");
    const audio = new Audio(mediaSrc(url));
    audioRef.current = audio;
    audio.onended = () => setAudioState("idle");
    audio.onerror = () => setAudioState("error");
    try {
      await audio.play();
      setAudioState("playing");
    } catch {
      setAudioState("error");
    }
  };

  // Switch language: stop the current audio and swap content — never autoplay the new language.
  const changeLanguage = (lng: GalleryLanguage) => {
    if (lng === selectedLanguage) return;
    stopAudio();
    setSelectedLanguage(lng);
  };

  // Reset carousel + stop audio whenever the shown item changes; the language tab resets to the
  // CURRENT global header language (not hard-coded VI).
  useEffect(() => {
    setIdx(0);
    setFailed(false);
    setZoomOpen(false);
    setShowShareMenu(false);
    stopAudio();
    setSelectedLanguage(globalGalleryLanguage);
    // globalGalleryLanguage is intentionally omitted: this effect only handles ITEM changes — the
    // global-language change while the same item stays open is handled by the effect below.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [itemId, stopAudio]);

  // Global header language changed while the modal is open → stop the playing audio and follow it
  // (description + audio URL + names swap in place; nothing refetches, nothing autoplays).
  useEffect(() => {
    stopAudio();
    setSelectedLanguage(globalGalleryLanguage);
  }, [globalGalleryLanguage, stopAudio]);

  // Stop audio when the modal unmounts.
  useEffect(() => () => stopAudio(), [stopAudio]);

  // Dismiss the share menu on a click outside it, or on Escape.
  //
  // Escape is taken in the CAPTURE phase and stopped there: the page keeps a window-level (bubbling)
  // Escape handler that closes the whole item modal, and without this the first Escape would blow past
  // the open menu and shut the item too. With the menu closed, that page handler behaves as before.
  useEffect(() => {
    if (!showShareMenu) return;

    const closeOnOutside = (e: Event) => {
      if (!shareMenuRef.current?.contains(e.target as Node)) setShowShareMenu(false);
    };
    const closeOnEscape = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      e.stopPropagation();
      e.stopImmediatePropagation();
      setShowShareMenu(false);
    };

    document.addEventListener("mousedown", closeOnOutside);
    document.addEventListener("touchstart", closeOnOutside);
    document.addEventListener("keydown", closeOnEscape, true);
    return () => {
      document.removeEventListener("mousedown", closeOnOutside);
      document.removeEventListener("touchstart", closeOnOutside);
      document.removeEventListener("keydown", closeOnEscape, true);
    };
  }, [showShareMenu]);

  const media = detail?.media ?? [];
  const cur = media[idx] ?? null;
  const step = (d: -1 | 1) => {
    if (media.length > 1) {
      setIdx((i) => (i + d + media.length) % media.length);
      setFailed(false);
    }
  };

  // Paginate with a small window of dots (so 14 media don't cram 14 dots) + a NN/MM counter below.
  const total = media.length;
  const DOT_WINDOW = 7;
  const dotStart = total <= DOT_WINDOW ? 0 : Math.max(0, Math.min(idx - Math.floor(DOT_WINDOW / 2), total - DOT_WINDOW));
  const dotIndices = Array.from({ length: Math.min(DOT_WINDOW, total) }, (_, k) => dotStart + k);
  const pad2 = (n: number) => String(n).padStart(2, "0");

  // The canonical deep link of THIS item — campus + location + item, rebuilt from the loaded detail
  // rather than from window.location.href, so the shared URL always points at the item on screen
  // (the modal's prev/next steps through items) and carries nothing else. Null until the detail
  // arrives; the share actions are disabled until then.
  const shareUrl = detail
    ? buildGalleryShareUrl({
        campusCode: detail.campus.campusCode,
        locationId: detail.location.locationId,
        galleryItemId: detail.galleryItem.galleryItemId,
      })
    : null;

  const shareLink = async (channel: "copy" | "facebook") => {
    if (!shareUrl) return;
    setShowShareMenu(false);

    if (channel === "facebook") {
      // Facebook scrapes the canonical URL itself — Vercel routes its crawler to the backend Open
      // Graph endpoint, so the card carries the item's own title/description/image.
      openFacebookShare(shareUrl);
      return;
    }

    const copied = await copyTextToClipboard(shareUrl);
    if (copied) showSuccessToast(t('visitFptu:gallery.toasts.copyLinkSuccess'));
    else showMessageErrorToast(t('visitFptu:gallery.toasts.copyLinkError'));
  };

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
      className="fixed inset-0 z-[120] flex items-center justify-center p-4 sm:p-6 md:p-8"
    >
      <div className="absolute inset-0 bg-black/50 backdrop-blur-md" onClick={onClose} />

      <motion.div
        initial={{ opacity: 0, scale: 0.92, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.96, y: 10 }}
        transition={{ duration: 0.4, ease: [0.22, 1, 0.36, 1] }}
        className="relative z-10 w-full max-w-6xl h-[82vh] grid grid-cols-1 lg:grid-cols-12 gap-4 lg:gap-6"
      >
        {/* ── Left: breadcrumb / title / description ── */}
        <div className="lg:col-span-5 flex flex-col gap-4 h-full min-h-0">
          <div className="bg-white/15 dark:bg-black/20 backdrop-blur-2xl border border-white/30 rounded-[1.5rem] sm:rounded-[2rem] p-5 sm:p-6 shadow-[0_8px_32px_rgba(0,0,0,0.1)] relative z-30 group transition-all duration-500 hover:shadow-[0_8px_40px_rgba(255,255,255,0.1)] shrink-0">
            <div className="absolute inset-0 overflow-hidden rounded-[inherit] pointer-events-none">
              <div className="absolute -top-20 -right-20 w-40 h-40 bg-fpt-orange/20 rounded-full blur-3xl group-hover:bg-fpt-orange/30 transition-colors duration-500" />
              <div className="absolute -bottom-20 -left-20 w-40 h-40 bg-blue-500/20 rounded-full blur-3xl group-hover:bg-blue-400/30 transition-colors duration-500" />
            </div>

            {/* z-40 (NOT z-10) is load-bearing: the <h3> below is also positioned and comes later in the
                DOM, so at an equal z-index it painted OVER this row — and the share dropdown, being inside
                this row's stacking context, could never escape above it whatever z-index it asked for.
                The result was a menu you could see but not click on its upper half. */}
            <div className="flex items-center justify-between mb-5 relative z-40 gap-3">
              <div className="inline-flex items-center gap-2 px-4 py-1.5 bg-fpt-orange/90 text-white font-semibold text-[10px] sm:text-xs tracking-widest uppercase rounded-full border border-white/30 backdrop-blur-md shadow-[0_0_15px_rgba(243,112,33,0.4)] max-w-full overflow-hidden">
                <span className="truncate">{displayAreaName}</span>
                <ChevronRight className="w-3.5 h-3.5 shrink-0 opacity-80" />
                <span className="truncate">{displayLocationName}</span>
              </div>
              <div ref={shareMenuRef} className="flex items-center gap-2 relative shrink-0">
                <button
                  onClick={() => setShowShareMenu((s) => !s)}
                  aria-haspopup="menu"
                  aria-expanded={showShareMenu}
                  className={`w-9 h-9 flex items-center justify-center rounded-full border transition-all hover:scale-105 ${
                    showShareMenu
                      ? "bg-fpt-orange text-white border-fpt-orange shadow-[0_0_18px_rgba(243,112,33,0.55)]"
                      : "bg-white/15 text-white border-white/30 hover:bg-fpt-orange hover:border-fpt-orange"
                  }`}
                  title={t('visitFptu:gallery.actions.share')}
                >
                  <Share2 className="w-4 h-4" />
                </button>
                {/* The panel is deliberately NOT role="menu" with role="menuitem" children: that would
                    override the native button roles while promising arrow-key roving focus this popover
                    does not implement. aria-haspopup/aria-expanded on the trigger say enough. */}
                <AnimatePresence>
                  {showShareMenu && (
                    <motion.div
                      aria-label={t('visitFptu:gallery.actions.share')}
                      initial={{ opacity: 0, y: -6, scale: 0.96 }}
                      animate={{ opacity: 1, y: 0, scale: 1 }}
                      exit={{ opacity: 0, y: -6, scale: 0.96 }}
                      transition={{ duration: 0.16 }}
                      className="absolute right-0 top-full mt-3 w-[17rem] rounded-2xl border border-white/15 bg-[#0f1218] shadow-[0_24px_60px_rgba(0,0,0,0.75)] p-2 z-[80]"
                    >
                      {/* Caret tying the panel to the button it came from. */}
                      <div className="absolute -top-1.5 right-3.5 w-3 h-3 rotate-45 bg-[#0f1218] border-l border-t border-white/15" />

                      <p className="px-3 pt-2 pb-1.5 text-[11px] font-bold uppercase tracking-[0.14em] text-white/45">
                        {t('visitFptu:gallery.actions.share')}
                      </p>

                      <button
                        onClick={() => shareLink("copy")}
                        disabled={!shareUrl}
                        className="group/share w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-left transition-colors hover:bg-white/10 focus-visible:bg-white/10 focus-visible:outline-none disabled:opacity-40 disabled:pointer-events-none"
                      >
                        <span className="w-9 h-9 shrink-0 rounded-full bg-white/10 text-white flex items-center justify-center transition-colors group-hover/share:bg-white/20">
                          <LinkIcon className="w-4 h-4" />
                        </span>
                        <span className="min-w-0">
                          <span className="block text-sm font-semibold text-white">
                            {t('visitFptu:gallery.actions.copyLink')}
                          </span>
                          <span className="block text-xs text-white/55 truncate">
                            {t('visitFptu:gallery.share.copyHint')}
                          </span>
                        </span>
                      </button>

                      <button
                        onClick={() => shareLink("facebook")}
                        disabled={!shareUrl}
                        className="group/share w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-left transition-colors hover:bg-[#1877F2]/20 focus-visible:bg-[#1877F2]/20 focus-visible:outline-none disabled:opacity-40 disabled:pointer-events-none"
                      >
                        <span className="w-9 h-9 shrink-0 rounded-full bg-[#1877F2] text-white flex items-center justify-center">
                          <Facebook className="w-4 h-4" />
                        </span>
                        <span className="min-w-0">
                          <span className="block text-sm font-semibold text-white">Facebook</span>
                          <span className="block text-xs text-white/55 truncate">
                            {t('visitFptu:gallery.share.facebookHint')}
                          </span>
                        </span>
                      </button>
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            </div>

            <h3 className="text-2xl sm:text-3xl font-black text-transparent bg-clip-text bg-gradient-to-br from-white via-white to-white/70 mb-3 leading-tight tracking-tight drop-shadow-sm relative z-10 min-h-[2rem]">
              {isLoading ? t('visitFptu:gallery.labels.loading') : displayItemTitle || "—"}
            </h3>
            <div className="w-24 h-1.5 bg-gradient-to-r from-fpt-orange to-transparent rounded-full opacity-80 relative z-10" />
          </div>

          {/* Same glass surface as the title card above (bg / border / blur / shadow kept in sync on
              purpose) — this card was previously a light panel, so its contents are white-on-glass. */}
          <div className="bg-white/15 dark:bg-black/20 backdrop-blur-2xl border border-white/30 rounded-[1.5rem] sm:rounded-[2rem] p-5 sm:p-6 shadow-[0_8px_32px_rgba(0,0,0,0.1)] grow flex flex-col overflow-hidden relative min-h-0">
            {/* Language toggle + speaker — both languages are always available (both are mandatory). */}
            {detail && !notFound && !isLoading && (
              <div className="flex items-center justify-between gap-2 mb-3 shrink-0">
                <div className="inline-flex items-center rounded-full bg-white/10 border border-white/15 p-1 gap-1" title={t('visitFptu:gallery.actions.switchLanguage')}>
                  {(["vi", "en"] as GalleryLanguage[]).map((lng) => {
                    const active = selectedLanguage === lng;
                    return (
                      <button
                        key={lng}
                        onClick={() => changeLanguage(lng)}
                        className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold transition-all ${
                          active ? "bg-fpt-orange text-white shadow" : "text-white/70 hover:text-fpt-orange"
                        }`}
                      >
                        <Languages className="w-3.5 h-3.5" />
                        {lng === "vi" ? t('visitFptu:gallery.labels.vietnamese') : t('visitFptu:gallery.labels.english')}
                      </button>
                    );
                  })}
                </div>
                <button
                  onClick={toggleNarration}
                  disabled={!activeContent?.audioUrl}
                  title={
                    audioState === "playing"
                      ? t('visitFptu:gallery.actions.stopNarration')
                      : t('visitFptu:gallery.actions.listenNarration')
                  }
                  className={`flex items-center justify-center w-11 h-11 rounded-full transition-all duration-300 hover:scale-110 hover:shadow-[0_0_15px_rgba(243,112,33,0.4)] disabled:opacity-40 disabled:cursor-not-allowed ${
                    audioState === "playing"
                      ? "bg-fpt-orange text-white animate-pulse"
                      : "bg-fpt-orange/20 text-fpt-orange hover:bg-fpt-orange hover:text-white"
                  }`}
                >
                  {audioState === "loading" ? (
                    <Loader2 className="w-5 h-5 animate-spin" />
                  ) : audioState === "playing" ? (
                    <VolumeX className="w-5 h-5" />
                  ) : (
                    <Volume2 className="w-5 h-5" />
                  )}
                </button>
              </div>
            )}
            {audioState === "error" && (
              <span className="text-[11px] leading-snug text-red-300 mb-2 shrink-0">
                {t('visitFptu:gallery.errors.narrationFailed')}
              </span>
            )}

            {/* Description — the ONLY scrolling element (flex-col grow so it's height-bounded). */}
            <div className="grow min-h-0 overflow-y-auto pr-2 mb-4 prose prose-base text-white font-light leading-relaxed [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-white/10 [&::-webkit-scrollbar-track]:rounded-full [&::-webkit-scrollbar-thumb]:bg-fpt-orange/50 [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb:hover]:bg-fpt-orange/80 [scrollbar-width:thin] [scrollbar-color:rgba(243,112,33,0.55)_transparent]">
              {isLoading ? (
                <p className="text-white/70">{t('visitFptu:gallery.labels.loadingDescription')}</p>
              ) : notFound ? (
                <p className="text-red-300 font-medium">{t('visitFptu:gallery.errors.contentHidden')}</p>
              ) : (
                <p className="text-white/90 whitespace-pre-line break-words [overflow-wrap:anywhere] first-letter:text-4xl first-letter:font-bold first-letter:text-fpt-orange first-letter:mr-1 first-letter:float-left">
                  {activeContent?.description}
                </p>
              )}
            </div>

            {/* Prev / next item footer — fixed at the card bottom, never scrolls away */}
            {hasNav && (
              <div className="flex items-center justify-between pt-4 border-t border-white/15 shrink-0">
                <button
                  onClick={onPrev}
                  className="flex items-center gap-2 px-3 py-2 text-sm font-bold text-white/80 hover:text-fpt-orange hover:bg-fpt-orange/20 rounded-xl transition-all hover:scale-105 active:scale-95"
                >
                  <ChevronLeft className="w-5 h-5" /> {t('visitFptu:gallery.actions.previous')}
                </button>
                <button
                  onClick={onNext}
                  className="flex items-center gap-2 px-3 py-2 text-sm font-bold text-white/80 hover:text-fpt-orange hover:bg-fpt-orange/20 rounded-xl transition-all hover:scale-105 active:scale-95"
                >
                  {t('visitFptu:gallery.actions.next')} <ChevronRight className="w-5 h-5" />
                </button>
              </div>
            )}
          </div>
        </div>

        {/* ── Right: media carousel ── */}
        <div className="lg:col-span-7 flex flex-col gap-4 min-h-[280px] lg:min-h-0 h-full order-first lg:order-last">
          <div className="bg-white/15 dark:bg-white/5 backdrop-blur-2xl border border-white/30 rounded-[1.5rem] sm:rounded-[2rem] shadow-[0_8px_32px_rgba(0,0,0,0.15)] w-full h-full relative overflow-hidden group flex flex-col">
            <button
              onClick={onClose}
              title={t('visitFptu:gallery.actions.close')}
              className="absolute top-4 right-4 z-20 w-9 h-9 flex items-center justify-center rounded-full bg-black/50 hover:bg-white/20 text-white border border-white/25 backdrop-blur-md transition-all hover:scale-110 opacity-0 group-hover:opacity-100 group/close"
            >
              <X className="w-4 h-4 group-hover/close:rotate-90 transition-transform duration-300" />
            </button>

            <div className="relative w-full h-full rounded-[1.5rem] sm:rounded-[2rem] overflow-hidden bg-black/30">
              {isLoading ? (
                <div className="absolute inset-0 flex items-center justify-center text-white/80">
                  <Loader2 className="w-8 h-8 animate-spin" />
                </div>
              ) : notFound || !cur ? (
                <div className="absolute inset-0 flex flex-col items-center justify-center text-white/70 gap-2">
                  <ImageOff className="w-10 h-10" />
                  <span className="text-sm">{notFound ? t('visitFptu:gallery.errors.contentHidden') : t('visitFptu:gallery.errors.noMedia')}</span>
                </div>
              ) : failed ? (
                <div className="absolute inset-0 flex flex-col items-center justify-center text-white/70 gap-2">
                  <ImageOff className="w-10 h-10" />
                  <span className="text-sm">{t('visitFptu:gallery.errors.mediaLoadFailed')}</span>
                </div>
              ) : (
                <AnimatePresence mode="popLayout" initial={false}>
                  <motion.div
                    key={cur.mediaId}
                    initial={{ opacity: 0, scale: 1.04 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.98 }}
                    transition={{ duration: 0.35, ease: "easeInOut" }}
                    className="absolute inset-0 w-full h-full"
                  >
                    {cur.sourceType === "YOUTUBE" ? (
                      <iframe
                        src={cur.embedUrl || youtubeEmbedUrl(cur.youtubeVideoId || "")}
                        title={cur.altText || displayItemTitle || "YouTube video"}
                        className="w-full h-full"
                        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                        allowFullScreen
                      />
                    ) : isVideo(cur.mediaType) ? (
                      <video
                        src={mediaSrc(cur.url)}
                        poster={mediaSrc(cur.thumbnailUrl)}
                        controls
                        muted
                        playsInline
                        onError={() => setFailed(true)}
                        className="w-full h-full object-contain bg-black"
                      />
                    ) : (
                      <img
                        src={mediaSrc(cur.url)}
                        alt={cur.altText || displayItemTitle || "FPTU"}
                        onError={() => setFailed(true)}
                        className="w-full h-full object-cover"
                      />
                    )}
                  </motion.div>
                </AnimatePresence>
              )}

              {media.length > 1 && (
                <>
                  <button
                    onClick={() => step(-1)}
                    className="absolute left-4 top-1/2 -translate-y-1/2 p-2 bg-white/10 hover:bg-white/30 border border-white/20 text-white rounded-full backdrop-blur-md transition-all z-10 hover:scale-110 shadow-lg opacity-0 group-hover:opacity-100"
                  >
                    <ChevronLeft className="w-6 h-6" />
                  </button>
                  <button
                    onClick={() => step(1)}
                    className="absolute right-4 top-1/2 -translate-y-1/2 p-2 bg-white/10 hover:bg-white/30 border border-white/20 text-white rounded-full backdrop-blur-md transition-all z-10 hover:scale-110 shadow-lg opacity-0 group-hover:opacity-100"
                  >
                    <ChevronRight className="w-6 h-6" />
                  </button>
                </>
              )}
            </div>

            {/* Zoom button + pagination dots + counter — shown for any item with ≥ 1 media */}
            {!isLoading && !notFound && cur && !failed && (
              <div className="absolute bottom-5 left-1/2 -translate-x-1/2 z-20 flex flex-col items-center gap-2">
                <button
                  onClick={() => setZoomOpen(true)}
                  className="inline-flex items-center gap-2 px-4 py-2 rounded-full bg-black/50 hover:bg-black/70 text-white text-sm font-semibold border border-white/25 backdrop-blur-md transition-all hover:scale-105 opacity-0 group-hover:opacity-100"
                >
                  <ZoomIn className="w-4 h-4" /> {t('visitFptu:gallery.actions.zoomIn')}
                </button>
                <div className="flex items-center gap-2 px-3.5 py-2 bg-black/45 backdrop-blur-md rounded-full border border-white/20">
                  {dotIndices.map((i) => (
                    <button
                      key={i}
                      onClick={() => {
                        setIdx(i);
                        setFailed(false);
                      }}
                      className={`rounded-full transition-all duration-300 ${
                        idx === i
                          ? "w-6 h-2.5 bg-white shadow-[0_0_12px_rgba(255,255,255,0.9)]"
                          : "w-2 h-2 bg-white/50 hover:bg-white/80 hover:scale-125"
                      }`}
                    />
                  ))}
                </div>
                <span className="text-white text-xs font-extrabold tracking-[0.1em] drop-shadow-[0_4px_12px_rgba(0,0,0,0.7)]">
                  {pad2(idx + 1)}/{pad2(total)}
                </span>
              </div>
            )}
          </div>
        </div>
      </motion.div>

      {/* Zoom lightbox for the current media */}
      <AnimatePresence>
        {zoomOpen && cur && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="fixed inset-0 z-[130] flex items-center justify-center bg-black/95 backdrop-blur-md"
            onClick={() => setZoomOpen(false)}
          >
            <button
              onClick={(e) => {
                e.stopPropagation();
                setZoomOpen(false);
              }}
              title={t('visitFptu:gallery.actions.close')}
              className="absolute top-6 right-6 z-10 w-11 h-11 flex items-center justify-center rounded-full bg-black/50 hover:bg-white/20 text-white border border-white/25 backdrop-blur-md transition-all hover:scale-110"
            >
              <X className="w-6 h-6" />
            </button>
            {media.length > 1 && (
              <>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    step(-1);
                  }}
                  className="absolute left-6 top-1/2 -translate-y-1/2 z-10 p-3 rounded-full bg-black/50 hover:bg-white/20 text-white border border-white/20 backdrop-blur-md transition-all hover:scale-110"
                >
                  <ChevronLeft className="w-7 h-7" />
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    step(1);
                  }}
                  className="absolute right-6 top-1/2 -translate-y-1/2 z-10 p-3 rounded-full bg-black/50 hover:bg-white/20 text-white border border-white/20 backdrop-blur-md transition-all hover:scale-110"
                >
                  <ChevronRight className="w-7 h-7" />
                </button>
              </>
            )}
            <div className="relative flex items-center justify-center" onClick={(e) => e.stopPropagation()}>
              {cur.sourceType === "YOUTUBE" ? (
                <div className="w-[92vw] max-w-[1280px] aspect-video bg-black">
                  <iframe
                    src={cur.embedUrl || youtubeEmbedUrl(cur.youtubeVideoId || "")}
                    title={cur.altText || displayItemTitle || "YouTube video"}
                    className="w-full h-full"
                    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                    allowFullScreen
                  />
                </div>
              ) : isVideo(cur.mediaType) ? (
                <video
                  src={mediaSrc(cur.url)}
                  poster={mediaSrc(cur.thumbnailUrl)}
                  controls
                  autoPlay
                  muted
                  playsInline
                  className="max-w-[92vw] max-h-[90vh] object-contain"
                />
              ) : (
                <img
                  src={mediaSrc(cur.url)}
                  alt={cur.altText || displayItemTitle || "FPTU"}
                  className="max-w-[92vw] max-h-[90vh] object-contain"
                />
              )}
            </div>
            {media.length > 1 && (
              <span className="absolute bottom-6 left-1/2 -translate-x-1/2 text-white text-sm font-extrabold tracking-[0.1em] drop-shadow-[0_4px_12px_rgba(0,0,0,0.8)]">
                {pad2(idx + 1)}/{pad2(total)}
              </span>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}

export function CampusDetailVisitPage() {
  const { t, i18n } = useTranslation(['visitFptu']);
  // Global header language for DB-backed bilingual strings. resolvedLanguage handles "en-US"/"vi-VN"
  // variants. Display strings are DERIVED at render time only — the bilingual payloads stay in state
  // untouched, so toggling VI/EN re-renders without refetching or resetting any view state.
  const language = i18n.resolvedLanguage ?? i18n.language;
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const routeId = (id || "hn").toLowerCase();
  const campusCode = routeId.toUpperCase();
  const fallback = CAMPUS_FALLBACK[routeId] || CAMPUS_FALLBACK.hn;

  // ── Server state ───────────────────────────────────────────────────────────
  const [nav, setNav] = useState<PublicGalleryNavigation | null>(null);
  const [isNavLoading, setIsNavLoading] = useState(true);
  const [navError, setNavError] = useState<string | null>(null);

  // Tier 1: location album grid.
  const [grid, setGrid] = useState<PublicLocationGalleryGrid | null>(null);
  const [isGridLoading, setIsGridLoading] = useState(false);
  const [gridError, setGridError] = useState<string | null>(null);

  // Tier 2: one gallery item's detail.
  const [detail, setDetail] = useState<PublicGalleryItemDetail | null>(null);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [detailNotice, setDetailNotice] = useState<string | null>(null);

  // ── UI state ─────────────────────────────────────────────────────────────
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [hoveredAreaId, setHoveredAreaId] = useState<number | null>(null);
  // Hover flyout for the sidebar area list. It is rendered on a fixed layer OUTSIDE the (transformed)
  // sidebar container so the area list can scroll independently without clipping the flyout.
  const [areaFlyout, setAreaFlyout] = useState<{ areaId: number; top: number; left: number; maxHeight: number } | null>(null);
  const activeAreaButtonRef = useRef<HTMLButtonElement | null>(null);
  const flyoutCloseTimer = useRef<number | null>(null);
  const [activeLocationId, setActiveLocationId] = useState<number | null>(null);
  const [selectedAreaId, setSelectedAreaId] = useState<number | null>(null);
  // Area Showcase (BR-PGAL-AREA-01..12): fullscreen area cover + vertical location-cover thumbnail rail.
  const [showcaseAreaId, setShowcaseAreaId] = useState<number | null>(null);
  const [activeLocationThumbnailIndex, setActiveLocationThumbnailIndex] = useState(0);
  // Vertical centre (px, in the rail's own box) of the active thumbnail → anchors the left-side name label.
  const [activeThumbY, setActiveThumbY] = useState(0);
  // Location Showcase (AC-LOC/MEDIA/DELEGATION): opened by clicking a location thumbnail in the Area rail.
  const [locationShowcaseId, setLocationShowcaseId] = useState<number | null>(null);
  const [showcaseData, setShowcaseData] = useState<PublicLocationShowcase | null>(null);
  const [isShowcaseLoading, setIsShowcaseLoading] = useState(false);
  const [activeMediaIndex, setActiveMediaIndex] = useState(0);
  const [activeDelegationIndex, setActiveDelegationIndex] = useState(0);
  // Gallery item detail modal (opened by clicking a MEDIA or "Đoàn khách" thumbnail).
  const [detailItemId, setDetailItemId] = useState<number | null>(null);
  const [detailData, setDetailData] = useState<PublicGalleryItemDetail | null>(null);
  const [isItemDetailLoading, setIsItemDetailLoading] = useState(false);
  const [itemDetailNotFound, setItemDetailNotFound] = useState(false);
  // The list (gallery item ids) + position the modal was opened from → drives its prev/next footer.
  const [detailItems, setDetailItems] = useState<number[]>([]);
  const [detailPos, setDetailPos] = useState(0);
  // Set only for an item opened from the URL, so its campus/location ownership is verified once the
  // detail response arrives. Cleared after that check; items opened by clicking never need it.
  const [deepLinkItemId, setDeepLinkItemId] = useState<number | null>(null);
  const [selectedGalleryItemId, setSelectedGalleryItemId] = useState<number | null>(null);
  const [currentMediaIndex, setCurrentMediaIndex] = useState(0);
  const [isLightboxOpen, setIsLightboxOpen] = useState(false);
  const [zoomScale, setZoomScale] = useState(1);
  const [failedMediaIds, setFailedMediaIds] = useState<Set<number>>(new Set());
  const [isSpeaking, setIsSpeaking] = useState(false);

  const gridRequestId = useRef(0);
  const detailRequestId = useRef(0);
  const showcaseRequestId = useRef(0);
  const itemDetailRequestId = useRef(0);

  const areas: PublicGalleryArea[] = nav?.areas ?? [];
  const campusName = nav?.campus?.campusName || `Campus ${campusCode}`;
  const hasContent = areas.length > 0;

  const isDetailView = selectedGalleryItemId != null;

  // Flattened, ordered list (for finding a location's parent area, defaults, etc.).
  const flatLocations = useMemo(
    () =>
      areas.flatMap((a) =>
        a.locations.map((l) => ({ ...l, areaId: a.areaId, areaName: a.areaName, areaNameEn: a.areaNameEn })),
      ),
    [areas],
  );

  const findLocation = useCallback(
    (locationId: number) => flatLocations.find((l) => l.locationId === locationId) || null,
    [flatLocations],
  );

  // ── Load navigation when campus changes ──────────────────────────────────
  useEffect(() => {
    let cancelled = false;
    setIsNavLoading(true);
    setNavError(null);
    setNav(null);
    setActiveLocationId(null);
    setSelectedGalleryItemId(null);
    setShowcaseAreaId(null);
    setActiveLocationThumbnailIndex(0);
    setLocationShowcaseId(null);
    setShowcaseData(null);
    setGrid(null);
    setDetail(null);

    publicVisitFptuApi
      .getNavigation(campusCode)
      .then((data) => {
        if (cancelled) return;
        setNav(data);
      })
      .catch((err) => {
        if (cancelled) return;
        const status = err?.response?.status;
        setNavError(
          status === 404
            ? t('visitFptu:gallery.errors.campusNotFound')
            : t('visitFptu:gallery.errors.galleryLoadFailed'),
        );
      })
      .finally(() => {
        if (!cancelled) setIsNavLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [campusCode]);

  // ── Loaders ──────────────────────────────────────────────────────────────
  const loadLocationGrid = useCallback(async (locationId: number) => {
    const reqId = ++gridRequestId.current;
    setIsGridLoading(true);
    setGridError(null);
    try {
      const data = await publicVisitFptuApi.getLocationGalleryItems(locationId);
      if (reqId !== gridRequestId.current) return; // stale response guard
      setGrid(data);
    } catch {
      if (reqId !== gridRequestId.current) return;
      setGrid(null);
      // BR-PGAL-GRID-11: location may have just lost all public items.
      setGridError(t('visitFptu:gallery.errors.noPublicContent'));
    } finally {
      if (reqId === gridRequestId.current) setIsGridLoading(false);
    }
  }, []);

  const loadItemDetail = useCallback(async (galleryItemId: number) => {
    const reqId = ++detailRequestId.current;
    setIsDetailLoading(true);
    setDetailNotice(null);
    try {
      const data = await publicVisitFptuApi.getGalleryItemDetail(galleryItemId);
      if (reqId !== detailRequestId.current) return; // stale response guard
      setDetail(data);
      setCurrentMediaIndex(0);
      setFailedMediaIds(new Set());
    } catch {
      if (reqId !== detailRequestId.current) return;
      setDetail(null);
      // BR-PGAL-GRID-10/11: content may have just been hidden/disabled.
      setDetailNotice(t('visitFptu:gallery.errors.contentHidden'));
    } finally {
      if (reqId === detailRequestId.current) setIsDetailLoading(false);
    }
  }, []);

  // ── Tier transitions ───────────────────────────────────────────────────────
  /** Open a location → show its album grid (Tier 1). */
  const openLocation = useCallback(
    (locationId: number) => {
      const loc = findLocation(locationId);
      if (!loc) return;
      setSelectedAreaId(loc.areaId);
      setShowcaseAreaId(null); // grid, area showcase and location showcase are mutually exclusive views
      setLocationShowcaseId(null);
      setShowcaseData(null);
      setActiveLocationId(locationId);
      setSelectedGalleryItemId(null);
      setDetail(null);
      setDetailNotice(null);
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.set("locationId", String(locationId));
        next.delete("itemId");
        return next;
      });
      void loadLocationGrid(locationId);
    },
    [findLocation, loadLocationGrid, setSearchParams],
  );

  /** Open one gallery item → show its detail (Tier 2). */
  const openItem = useCallback(
    (galleryItemId: number) => {
      setSelectedGalleryItemId(galleryItemId);
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.set("itemId", String(galleryItemId));
        return next;
      });
      void loadItemDetail(galleryItemId);
    },
    [loadItemDetail, setSearchParams],
  );

  /** Back from detail to the location grid (keep campus/area/location). */
  const backToGrid = useCallback(() => {
    setSelectedGalleryItemId(null);
    setDetail(null);
    setDetailNotice(null);
    setIsLightboxOpen(false);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("itemId");
      return next;
    });
  }, [setSearchParams]);

  const closeOverlay = useCallback(() => {
    setActiveLocationId(null);
    setSelectedGalleryItemId(null);
    setGrid(null);
    setDetail(null);
    setDetailNotice(null);
    setIsLightboxOpen(false);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("locationId");
      next.delete("itemId");
      return next;
    });
  }, [setSearchParams]);

  const deepLinkHandled = useRef(false);

  // Lock body scroll while the gallery overlay or area showcase is open.
  useEffect(() => {
    const locked = activeLocationId != null || showcaseAreaId != null || locationShowcaseId != null;
    document.body.style.overflow = locked ? "hidden" : "";
    return () => {
      document.body.style.overflow = "";
    };
  }, [activeLocationId, showcaseAreaId, locationShowcaseId]);

  // ── Area Showcase (BR-PGAL-AREA-01) ──────────────────────────────────────
  /** Click an area on the sidebar → fullscreen area cover + location-cover thumbnail rail. */
  const openAreaShowcase = useCallback((areaId: number) => {
    setSelectedAreaId(areaId);
    setShowcaseAreaId(areaId);
    setActiveLocationThumbnailIndex(0); // reset counter to 01/n (BR-PGAL-AREA §9.3)
    // Leave any open location grid/detail/showcase behind — these views are exclusive.
    setActiveLocationId(null);
    setSelectedGalleryItemId(null);
    setLocationShowcaseId(null);
    setShowcaseData(null);
    setGrid(null);
    setDetail(null);
    setDetailNotice(null);
    setIsSidebarOpen(true);
  }, []);

  const closeShowcase = useCallback(() => {
    setShowcaseAreaId(null);
    setSelectedAreaId(null);
    setActiveLocationThumbnailIndex(0);
  }, []);

  // ── Sidebar hover flyout (rendered on a fixed layer so the area list can scroll independently) ──
  // The flyout opens BESIDE the hovered row: its top is aligned with the row's top so the Location list sits
  // right next to that area. It simply fills downward to the viewport bottom and scrolls internally, so even
  // the last area (20 Locations) is fully reachable and nothing is cut. A small floor keeps the bottom-most
  // rows from collapsing to a sliver, without lifting the flyout away from its area.
  const openAreaFlyout = useCallback((areaId: number, rect: DOMRect) => {
    if (flyoutCloseTimer.current != null) {
      window.clearTimeout(flyoutCloseTimer.current);
      flyoutCloseTimer.current = null;
    }
    setHoveredAreaId(areaId);
    const topMargin = 88; // stay clear of the public header
    const bottomMargin = 16;
    const maxBottom = window.innerHeight - bottomMargin;
    const top = Math.min(Math.max(topMargin, rect.top), maxBottom - 140); // keep ≥140px visible on last rows
    const maxHeight = maxBottom - top;
    setAreaFlyout({ areaId, top, left: rect.right + 8, maxHeight });
  }, []);

  // Small close delay so moving the pointer from an area button into its flyout doesn't dismiss it.
  const closeAreaFlyoutSoon = useCallback(() => {
    if (flyoutCloseTimer.current != null) window.clearTimeout(flyoutCloseTimer.current);
    flyoutCloseTimer.current = window.setTimeout(() => setHoveredAreaId(null), 160);
  }, []);

  const keepAreaFlyoutOpen = useCallback(() => {
    if (flyoutCloseTimer.current != null) {
      window.clearTimeout(flyoutCloseTimer.current);
      flyoutCloseTimer.current = null;
    }
  }, []);

  // Keep the selected area visible in the (now scrollable) sidebar list when it changes (AC-SCROLL-04).
  useEffect(() => {
    activeAreaButtonRef.current?.scrollIntoView({ block: "nearest", behavior: "smooth" });
  }, [selectedAreaId]);

  // ── Navigation helpers ───────────────────────────────────────────────────
  const startTour = useCallback(() => {
    if (!hasContent) return;
    setIsSidebarOpen(true);
    const first = areas[0];
    if (first) openAreaShowcase(first.areaId);
  }, [areas, hasContent, openAreaShowcase]);

  // ── Detail media ───────────────────────────────────────────────────────────
  const media = detail?.media ?? [];
  const currentMedia = media[currentMediaIndex] ?? null;
  const currentMediaFailed = currentMedia ? failedMediaIds.has(currentMedia.mediaId) : false;
  const gridItems = grid?.items ?? [];

  // Breadcrumb labels (work for both tiers; fall back to nav data while the grid is still loading).
  const activeLoc = activeLocationId ? findLocation(activeLocationId) : null;
  const breadcrumbArea = detail?.area.areaName ?? grid?.area.areaName ?? activeLoc?.areaName ?? "";
  const breadcrumbLocation =
    detail?.location.locationName ?? grid?.location.locationName ?? activeLoc?.locationName ?? "";

  // Grid stats (derived client-side from the items' media_kind — no extra API call).
  const gridImageCount = gridItems.filter((i) => (i.mediaKind || "").toUpperCase() === "IMAGE").length;
  const gridVideoCount = gridItems.filter((i) => (i.mediaKind || "").toUpperCase() === "VIDEO").length;
  const gridMixedCount = gridItems.filter((i) => (i.mediaKind || "").toUpperCase() === "MIXED").length;
  const gridStats = [
    t("visitFptu:gallery.labels.itemsCount", { count: gridItems.length }),
    gridImageCount > 0 ? t("visitFptu:gallery.labels.imagesCount", { count: gridImageCount }) : null,
    gridVideoCount > 0 ? t("visitFptu:gallery.labels.videosCount", { count: gridVideoCount }) : null,
    gridMixedCount > 0 ? t("visitFptu:gallery.labels.mixedCount", { count: gridMixedCount }) : null,
  ].filter(Boolean).join(" · ");

  // Sibling locations of the current area → quick-switch chips under the header.
  const currentArea = areas.find((a) => a.areaId === selectedAreaId) ?? null;
  const siblingLocations = currentArea?.locations ?? [];

  // ── Area Showcase derived data ─────────────────────────────────────────────
  const showcaseArea = useMemo(
    () => areas.find((a) => a.areaId === showcaseAreaId) ?? null,
    [areas, showcaseAreaId],
  );
  const showcaseLocations = showcaseArea?.locations ?? [];
  const showcaseTotal = showcaseLocations.length;
  // Guard the index if the active area changes to one with fewer locations.
  const safeThumbIndex =
    showcaseTotal > 0 ? Math.min(activeLocationThumbnailIndex, showcaseTotal - 1) : 0;
  const showcaseBg = showcaseArea?.areaCoverUrl || fallback.bg;
  const pad2 = (n: number) => String(n).padStart(2, "0");

  const stepThumbnail = useCallback(
    (dir: -1 | 1) => {
      if (showcaseTotal <= 0) return;
      setActiveLocationThumbnailIndex((i) => (i + dir + showcaseTotal) % showcaseTotal); // loop (BR-PGAL-AREA-09)
    },
    [showcaseTotal],
  );

  // Prev/next area navigation (loops), reusing the sidebar order. openAreaShowcase resets the rail to 01/n.
  const stepArea = useCallback(
    (dir: -1 | 1) => {
      if (areas.length === 0) return;
      const cur = areas.findIndex((a) => a.areaId === showcaseAreaId);
      if (cur < 0) return;
      const next = (cur + dir + areas.length) % areas.length;
      openAreaShowcase(areas[next].areaId);
    },
    [areas, showcaseAreaId, openAreaShowcase],
  );

  // Reveal the active thumbnail when it reaches an edge of the rail, and anchor the left-hand name label to
  // it. Uses an INSTANT scrollTop inside useLayoutEffect (no smooth animation, no rAF) so it is 100%
  // reliable — the old smooth scroll got interrupted by the re-renders that onScroll → setActiveThumbY
  // triggers, which is why the list stopped scrolling and the name froze on the first thumbnail.
  // rail is position:relative, so el.offsetTop is measured against the rail itself.
  const thumbRailRef = useRef<HTMLDivElement | null>(null);
  const thumbRefs = useRef<(HTMLButtonElement | null)[]>([]);
  // Recompute where the active thumbnail sits in the rail's viewport (used to anchor the left name label).
  const syncActiveThumbY = useCallback(() => {
    const rail = thumbRailRef.current;
    const el = thumbRefs.current[safeThumbIndex];
    if (!rail || !el) return;
    setActiveThumbY(el.offsetTop - rail.scrollTop + el.offsetHeight / 2);
  }, [safeThumbIndex]);

  useLayoutEffect(() => {
    const rail = thumbRailRef.current;
    const el = thumbRefs.current[safeThumbIndex];
    if (!rail || !el) return;
    const pad = 12; // keep a little breathing room past the edge so the next thumb peeks in
    const viewTop = rail.scrollTop;
    const viewBottom = viewTop + rail.clientHeight;
    const elTop = el.offsetTop;
    const elBottom = elTop + el.offsetHeight;
    const maxScroll = Math.max(0, rail.scrollHeight - rail.clientHeight);

    let target = viewTop;
    if (elTop < viewTop + pad) {
      target = elTop - pad; // active is above the fold → slide up to reveal it
    } else if (elBottom > viewBottom - pad) {
      target = elBottom - rail.clientHeight + pad; // active is below the fold → slide down to reveal it
    }
    target = Math.max(0, Math.min(target, maxScroll));

    rail.scrollTop = target; // instant — never gets stuck
    setActiveThumbY(elTop - target + el.offsetHeight / 2);
  }, [showcaseAreaId, locationShowcaseId, safeThumbIndex, showcaseTotal]);

  // ── Location Showcase (MEDIA column + "Đoàn khách đã tới thăm" row) ─────────
  const locationShowcaseLocation = useMemo(
    () => flatLocations.find((l) => l.locationId === locationShowcaseId) ?? null,
    [flatLocations, locationShowcaseId],
  );
  const locationShowcaseArea = useMemo(
    () => areas.find((a) => a.areaId === locationShowcaseLocation?.areaId) ?? null,
    [areas, locationShowcaseLocation],
  );
  const locationSiblings = locationShowcaseArea?.locations ?? [];
  const mediaItems = showcaseData?.mediaItems ?? [];
  const delegationItems = showcaseData?.visitDelegationItems ?? [];
  const safeMediaIndex = mediaItems.length > 0 ? Math.min(activeMediaIndex, mediaItems.length - 1) : 0;

  const openLocationShowcase = useCallback(
    (locationId: number) => {
      const loc = flatLocations.find((l) => l.locationId === locationId);
      if (loc) {
        setSelectedAreaId(loc.areaId); // keep the sidebar area highlighted
        // Retain the area context (Location Showcase is hidden while area !== null but stays set) so
        // closing / "Trở Về" returns to THIS area's Area Showcase; also line up the area rail's active thumb.
        setShowcaseAreaId(loc.areaId);
        const area = areas.find((a) => a.areaId === loc.areaId);
        const idx = area ? area.locations.findIndex((l) => l.locationId === locationId) : -1;
        if (idx >= 0) setActiveLocationThumbnailIndex(idx);
      }
      setLocationShowcaseId(locationId);
      setShowcaseData(null); // drop the previous location's items so nothing stale flashes while loading
      setActiveMediaIndex(0);
      setActiveDelegationIndex(0);
      // Publish the location and drop any item from the previous one: an itemId only ever means
      // something inside the location it belongs to.
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.set("locationId", String(locationId));
          next.delete("itemId");
          return next;
        },
        { replace: true },
      );
    },
    [areas, flatLocations, setSearchParams],
  );

  const closeLocationShowcase = useCallback(() => {
    setLocationShowcaseId(null);
    setShowcaseData(null);
    setActiveMediaIndex(0);
    setActiveDelegationIndex(0);
    // Leaving the location leaves the item with it (§21.3).
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete("locationId");
        next.delete("itemId");
        return next;
      },
      { replace: true },
    );
  }, [setSearchParams]);

  // Prev/next location within the SAME area (loops), background + lists reload via the fetch effect.
  const stepLocationShowcase = useCallback(
    (dir: -1 | 1) => {
      const loc = flatLocations.find((l) => l.locationId === locationShowcaseId);
      const area = areas.find((a) => a.areaId === loc?.areaId);
      const sibs = area?.locations ?? [];
      if (sibs.length === 0) return;
      const cur = sibs.findIndex((l) => l.locationId === locationShowcaseId);
      if (cur < 0) return;
      const next = (cur + dir + sibs.length) % sibs.length;
      openLocationShowcase(sibs[next].locationId);
    },
    [areas, flatLocations, locationShowcaseId, openLocationShowcase],
  );

  const stepMediaThumbnail = useCallback(
    (dir: -1 | 1) => {
      const n = mediaItems.length;
      if (n <= 0) return;
      setActiveMediaIndex((i) => (i + dir + n) % n); // loop (AC-MEDIA §10.2)
    },
    [mediaItems.length],
  );

  const stepDelegation = useCallback(
    (dir: -1 | 1) => {
      const n = delegationItems.length;
      if (n <= 0) return;
      setActiveDelegationIndex((i) => (i + dir + n) % n); // loop
    },
    [delegationItems.length],
  );

  // Open the detail modal for a clicked MEDIA / "Đoàn khách" gallery item (fetch its full media set).
  // Mirrors the id into ?itemId so the open modal is a shareable/reloadable URL (§21.3) — locationId
  // is left alone, since the item lives inside that location.
  const openItemDetail = useCallback((galleryItemId: number) => {
    setDetailItemId(galleryItemId);
    setDetailData(null);
    setItemDetailNotFound(false);
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.set("itemId", String(galleryItemId));
        return next;
      },
      { replace: true },
    );
    const reqId = ++itemDetailRequestId.current;
    setIsItemDetailLoading(true);
    publicVisitFptuApi
      .getGalleryItemDetail(galleryItemId)
      .then((data) => {
        if (reqId !== itemDetailRequestId.current) return;
        setDetailData(data);
      })
      .catch(() => {
        if (reqId !== itemDetailRequestId.current) return;
        setDetailData(null);
        setItemDetailNotFound(true);
      })
      .finally(() => {
        if (reqId === itemDetailRequestId.current) setIsItemDetailLoading(false);
      });
  }, [setSearchParams]);

  const closeItemDetail = useCallback(() => {
    setDetailItemId(null);
    setDetailData(null);
    setItemDetailNotFound(false);
    // Drop itemId, keep locationId: closing the item returns to the location, not out of it (§21.3).
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete("itemId");
        return next;
      },
      { replace: true },
    );
  }, [setSearchParams]);

  // Open the modal from a specific list (MEDIA or delegation) at a position → enables its prev/next footer.
  const openItemDetailAt = useCallback(
    (items: number[], pos: number) => {
      setDetailItems(items);
      setDetailPos(pos);
      openItemDetail(items[pos]);
    },
    [openItemDetail],
  );

  const stepItemDetail = useCallback(
    (dir: -1 | 1) => {
      setDetailPos((p) => {
        if (detailItems.length <= 1) return p;
        const np = (p + dir + detailItems.length) % detailItems.length;
        openItemDetail(detailItems[np]);
        return np;
      });
    },
    [detailItems, openItemDetail],
  );

  /**
   * Deep link / reload: ?locationId opens that Location Showcase, and ?itemId then opens that item's
   * detail modal on top of it — the URL the search popup produces for a gallery hit.
   *
   * The location id is validated against this campus's navigation before anything opens, so editing
   * the query string by hand cannot pull up a location from another campus; the item is checked the
   * same way once its detail arrives (next effect), because only the response says which location it
   * belongs to. Runs once per load — afterwards the UI owns the URL and re-running would fight the user.
   */
  useEffect(() => {
    if (isNavLoading || !hasContent || deepLinkHandled.current) return;
    const locParam = Number(searchParams.get("locationId"));
    const loc = locParam ? findLocation(locParam) : null;
    if (locParam && loc) {
      deepLinkHandled.current = true;
      setIsSidebarOpen(true);
      openLocationShowcase(locParam);

      const itemParam = Number(searchParams.get("itemId"));
      if (itemParam) {
        setDeepLinkItemId(itemParam);
        openItemDetail(itemParam);
      }
    }
  }, [isNavLoading, hasContent, searchParams, findLocation, openLocationShowcase, openItemDetail]);

  /**
   * An item reached by URL must actually belong to the location in that URL. A mismatch closes the
   * modal and leaves the (valid) location open, rather than showing content from somewhere else.
   * An item that is no longer public fails its fetch instead and is handled by the modal's own
   * not-found state, which is why this only inspects a detail that did arrive.
   */
  useEffect(() => {
    if (deepLinkItemId == null || detailData == null) return;
    if (detailData.galleryItem.galleryItemId !== deepLinkItemId) return;
    setDeepLinkItemId(null);
    if (locationShowcaseId != null && detailData.location.locationId !== locationShowcaseId) {
      closeItemDetail();
    }
  }, [deepLinkItemId, detailData, locationShowcaseId, closeItemDetail]);

  // Fetch MEDIA + delegation items whenever the shown location changes (stale-response guarded, AC-RELOAD-01).
  useEffect(() => {
    if (locationShowcaseId == null) return;
    const reqId = ++showcaseRequestId.current;
    setIsShowcaseLoading(true);
    publicVisitFptuApi
      .getLocationShowcase(locationShowcaseId)
      .then((data) => {
        if (reqId !== showcaseRequestId.current) return;
        setShowcaseData(data);
      })
      .catch(() => {
        if (reqId !== showcaseRequestId.current) return;
        setShowcaseData(null);
      })
      .finally(() => {
        if (reqId === showcaseRequestId.current) setIsShowcaseLoading(false);
      });
  }, [locationShowcaseId]);

  const markMediaFailed = useCallback((mediaId: number) => {
    setFailedMediaIds((prev) => {
      const next = new Set(prev);
      next.add(mediaId);
      return next;
    });
  }, []);

  const stepMedia = useCallback(
    (dir: -1 | 1) => {
      if (media.length <= 1) return;
      setCurrentMediaIndex((i) => (i + dir + media.length) % media.length); // loop within the item
    },
    [media.length],
  );

  // ESC closes lightbox → detail → overlay → area showcase.
  // While the area showcase is open, ↑/↓ step the location thumbnail rail.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        if (detailItemId != null) closeItemDetail();
        else if (isLightboxOpen) setIsLightboxOpen(false);
        else if (isDetailView) backToGrid();
        else if (activeLocationId) closeOverlay();
        else if (locationShowcaseId != null) closeLocationShowcase();
        else if (showcaseAreaId != null) closeShowcase();
        return;
      }
      // While the detail modal is open, ←/→ step through its list; other keys are swallowed.
      if (detailItemId != null) {
        if (e.key === "ArrowRight") {
          e.preventDefault();
          stepItemDetail(1);
        } else if (e.key === "ArrowLeft") {
          e.preventDefault();
          stepItemDetail(-1);
        }
        return;
      }
      // Location Showcase takes key priority: ↑/↓ step MEDIA, ←/→ change location.
      if (locationShowcaseId != null) {
        if (e.key === "ArrowDown") {
          e.preventDefault();
          stepMediaThumbnail(1);
        } else if (e.key === "ArrowUp") {
          e.preventDefault();
          stepMediaThumbnail(-1);
        } else if (e.key === "ArrowRight") {
          e.preventDefault();
          stepLocationShowcase(1);
        } else if (e.key === "ArrowLeft") {
          e.preventDefault();
          stepLocationShowcase(-1);
        }
        return;
      }
      if (showcaseAreaId != null && !activeLocationId) {
        if (e.key === "ArrowDown") {
          e.preventDefault();
          stepThumbnail(1);
        } else if (e.key === "ArrowUp") {
          e.preventDefault();
          stepThumbnail(-1);
        } else if (e.key === "ArrowRight") {
          e.preventDefault();
          stepArea(1);
        } else if (e.key === "ArrowLeft") {
          e.preventDefault();
          stepArea(-1);
        }
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [
    isLightboxOpen,
    isDetailView,
    activeLocationId,
    showcaseAreaId,
    locationShowcaseId,
    detailItemId,
    backToGrid,
    closeOverlay,
    closeShowcase,
    closeLocationShowcase,
    closeItemDetail,
    stepItemDetail,
    stepThumbnail,
    stepArea,
    stepMediaThumbnail,
    stepLocationShowcase,
  ]);

  // "Nghe thuyết minh": read the description aloud via the browser's speech synthesis (Vietnamese).
  const stopNarration = useCallback(() => {
    if (typeof window !== "undefined" && window.speechSynthesis) window.speechSynthesis.cancel();
    setIsSpeaking(false);
  }, []);

  const toggleNarration = useCallback(() => {
    const synth = typeof window !== "undefined" ? window.speechSynthesis : undefined;
    const text = detail?.galleryItem.content?.vi?.description?.trim();
    if (!synth || !text) return;
    if (isSpeaking) {
      stopNarration();
      return;
    }
    synth.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = "vi-VN";
    utterance.rate = 1;
    utterance.onend = () => setIsSpeaking(false);
    utterance.onerror = () => setIsSpeaking(false);
    setIsSpeaking(true);
    synth.speak(utterance);
  }, [detail, isSpeaking, stopNarration]);

  // Stop any narration when the viewed item changes or the overlay closes.
  useEffect(() => {
    stopNarration();
  }, [activeLocationId, detail?.galleryItem.galleryItemId, stopNarration]);

  return (
    <div className="relative min-h-[calc(100vh-64px)] w-full flex flex-col bg-gray-900">
      {/* ── Floating sidebar: areas + hover flyout of locations ── */}
      {hasContent && (
        <div
          style={{ top: 176 }}
          className={`fixed left-0 z-50 flex transition-transform duration-500 ease-in-out ${
            isSidebarOpen ? "translate-x-4 md:translate-x-6" : "-translate-x-full"
          }`}
        >
          {/* Height-bounded to the viewport; the area list scrolls on its own so the last area is always
              reachable/clickable even with many areas (AC-SCROLL-01). The hover flyout is rendered on a
              separate fixed layer (below) so this vertical scroller never clips it. */}
          <div className="w-56 h-auto max-h-[calc(100dvh-200px)] min-h-0 bg-black/30 backdrop-blur-xl flex flex-col overflow-hidden rounded-2xl shadow-[0_8px_32px_rgba(0,0,0,0.4)] border border-white/20">
            <nav className="flex-1 min-h-0 flex flex-col relative">
              <div className="absolute -inset-0.5 bg-gradient-to-b from-fpt-orange/20 to-transparent opacity-50 rounded-2xl pointer-events-none z-0"></div>
              <div className="relative z-10 flex-1 min-h-0 overflow-y-auto overflow-x-hidden overscroll-contain [scrollbar-width:thin] [scrollbar-color:rgba(255,255,255,0.45)_rgba(255,255,255,0.08)] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-thumb]:bg-white/40 [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-track]:bg-white/5">
                {areas.map((area, index) => {
                  const mediaKinds = new Set(area.locations.map((l) => l.mediaKind?.toUpperCase()));
                  const showVideoIcon = mediaKinds.has("VIDEO") && mediaKinds.size === 1;
                  return (
                    <button
                      key={area.areaId}
                      ref={selectedAreaId === area.areaId ? activeAreaButtonRef : undefined}
                      onClick={() => openAreaShowcase(area.areaId)}
                      onMouseEnter={(e) => openAreaFlyout(area.areaId, e.currentTarget.getBoundingClientRect())}
                      onMouseLeave={closeAreaFlyoutSoon}
                      className={`w-full flex items-center justify-between px-4 py-3 border-b border-white/10 transition-all duration-300 text-left group ${
                        selectedAreaId === area.areaId
                          ? "bg-[#F37021] text-white shadow-[0_0_20px_rgba(243,112,33,0.5)]"
                          : hoveredAreaId === area.areaId
                            ? "bg-[#eb742d]/80 text-white backdrop-blur-md"
                            : "bg-transparent text-gray-200 hover:bg-[#eb742d]/80 hover:text-white"
                      } ${index === 0 ? "rounded-t-2xl" : ""} ${
                        index === areas.length - 1 ? "border-b-0 rounded-b-2xl" : ""
                      }`}
                    >
                      <span className="uppercase tracking-widest text-[11px] sm:text-xs font-semibold">
                        {localizedDbText(area.areaName, area.areaNameEn, language)}
                      </span>
                      <span className="opacity-90">
                        {showVideoIcon ? <VideoIcon className="w-5 h-5" /> : <ImageIcon className="w-5 h-5" />}
                      </span>
                    </button>
                  );
                })}
              </div>
            </nav>
          </div>

          {/* Sidebar toggle */}
          <button
            onClick={() => setIsSidebarOpen(!isSidebarOpen)}
            className="absolute top-1/2 -translate-y-1/2 -right-8 w-8 h-14 bg-black/40 backdrop-blur-xl border border-white/20 border-l-0 rounded-r-xl flex items-center justify-center text-white hover:bg-fpt-orange hover:border-fpt-orange hover:shadow-[0_0_20px_rgba(243,112,33,0.5)] transition-all cursor-pointer shadow-xl group"
          >
            {isSidebarOpen ? (
              <ChevronLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
            ) : (
              <ChevronRight className="w-5 h-5 group-hover:translate-x-1 transition-transform" />
            )}
          </button>
        </div>
      )}

      {/* Sidebar hover flyout (fixed layer — never clipped by the scrollable area list) */}
      <AnimatePresence>
        {hasContent && areaFlyout && hoveredAreaId === areaFlyout.areaId && (() => {
          const area = areas.find((a) => a.areaId === areaFlyout.areaId);
          if (!area || area.locations.length === 0) return null;
          return (
            <motion.div
              key={area.areaId}
              initial={{ opacity: 0, x: -10, scale: 0.95 }}
              animate={{ opacity: 1, x: 0, scale: 1 }}
              exit={{ opacity: 0, x: -10, scale: 0.95 }}
              transition={{ duration: 0.2, ease: "easeOut" }}
              onMouseEnter={keepAreaFlyoutOpen}
              onMouseLeave={closeAreaFlyoutSoon}
              style={{ position: "fixed", top: areaFlyout.top, left: areaFlyout.left, maxHeight: areaFlyout.maxHeight }}
              className="w-72 overflow-y-auto rounded-2xl shadow-[0_10px_40px_rgba(0,0,0,0.5)] z-[60] overscroll-contain [scrollbar-width:thin] [scrollbar-color:rgba(255,255,255,0.45)_rgba(255,255,255,0.08)] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-thumb]:bg-white/40 [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-track]:bg-white/5"
            >
              <div
                className="flex flex-col py-3"
                style={{ background: "linear-gradient(135deg, rgba(235,116,45,0.85) 0%, rgba(200,80,30,0.95) 100%)" }}
              >
                {area.locations.map((loc) => (
                  <button
                    key={loc.locationId}
                    onClick={() => { setHoveredAreaId(null); openLocationShowcase(loc.locationId); }}
                    className={`w-full text-left px-5 py-3 text-sm transition-all flex justify-between items-center group/sub ${
                      activeLocationId === loc.locationId ? "bg-white/25 text-white" : "text-white hover:bg-white/20"
                    }`}
                  >
                    <span className="font-medium tracking-wide drop-shadow-sm group-hover/sub:translate-x-1 transition-transform">
                      {localizedDbText(loc.locationName, loc.locationNameEn, language)}
                    </span>
                    <span className="flex items-center shrink-0">
                      <MapPin className="w-4 h-4 opacity-70 group-hover/sub:opacity-100 group-hover/sub:scale-110 transition-all" />
                    </span>
                  </button>
                ))}
              </div>
            </motion.div>
          );
        })()}
      </AnimatePresence>

      {/* Back button */}
      <motion.button
        initial={{ opacity: 0, x: -20 }}
        animate={{ opacity: 1, x: 0 }}
        transition={{ delay: 0.2 }}
        onClick={() => {
          // From the Location Showcase, "Trở Về" goes back to its Area Showcase; otherwise leave the page.
          if (locationShowcaseId != null) closeLocationShowcase();
          else navigate("/visit-fptu");
        }}
        className="absolute top-24 left-6 sm:top-28 z-40 p-3 bg-black/30 backdrop-blur-md rounded-full border border-white/20 text-white hover:bg-fpt-orange hover:border-fpt-orange hover:scale-110 hover:shadow-[0_0_20px_rgba(243,112,33,0.5)] transition-all flex items-center gap-2 group"
      >
        <ArrowLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
        <span className="hidden sm:inline font-medium pr-2 text-sm tracking-wide">{t('visitFptu:gallery.labels.backToPrev')}</span>
      </motion.button>

      {/* ── Hero ── */}
      <div className="relative w-full h-[100vh] flex items-center justify-center overflow-hidden bg-black">
        <div className="absolute inset-0 z-0">
          <motion.img
            initial={{ scale: 1.1, filter: "brightness(0.5)" }}
            animate={{ scale: 1, filter: "brightness(0.7)" }}
            transition={{ duration: 1.5, ease: "easeOut" }}
            src={fallback.bg}
            alt={campusName}
            className="w-full h-full object-cover"
          />
        </div>
        <div className="absolute inset-0 z-10 bg-gradient-to-t from-gray-900 via-gray-900/40 to-black/20 pointer-events-none" />

        <div className="relative z-20 text-center flex flex-col items-center mt-20 px-4">
          <motion.div
            initial={{ opacity: 0, y: 30 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.8, delay: 0.3, ease: "easeOut" }}
            className="inline-block px-4 py-1.5 bg-white/10 text-white font-medium text-xs tracking-[0.2em] uppercase rounded-full border border-white/30 backdrop-blur-md mb-6"
          >
            VisitFPTU Gallery
          </motion.div>
          <motion.h1
            initial={{ opacity: 0, y: 30 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.8, delay: 0.5, ease: "easeOut" }}
            className="text-4xl sm:text-5xl md:text-6xl lg:text-7xl font-black text-transparent bg-clip-text bg-gradient-to-b from-white via-white to-white/70 tracking-tight drop-shadow-2xl max-w-3xl leading-[1.05]"
          >
            {campusName}
          </motion.h1>
          <motion.p
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 1, delay: 0.8 }}
            className="mt-6 text-lg sm:text-xl text-gray-200 font-light max-w-2xl text-center leading-relaxed drop-shadow-md"
          >
            {t(`visitFptu:gallery.campusDescriptions.${routeId}`, { defaultValue: fallback.description })}
          </motion.p>

          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.8, delay: 1, ease: "easeOut" }}
            className="mt-10 flex flex-wrap justify-center gap-4 min-h-[56px] items-center"
          >
            {isNavLoading ? (
              <div className="flex items-center gap-3 text-white/80">
                <Loader2 className="w-5 h-5 animate-spin" />
                <span>{t('visitFptu:gallery.labels.loadingGalleryContent')}</span>
              </div>
            ) : navError ? (
              <div className="px-6 py-3 rounded-full bg-red-500/20 border border-red-300/30 text-white">
                {navError}
              </div>
            ) : !hasContent ? (
              <div className="px-6 py-3 rounded-full bg-white/10 border border-white/20 text-white backdrop-blur-md">
                {t('visitFptu:gallery.labels.noPublicCampusContent')}
              </div>
            ) : (
              <>
                <button
                  onClick={startTour}
                  className="px-8 py-3.5 bg-fpt-orange hover:bg-fpt-orange/90 text-white rounded-full font-medium transition-all hover:scale-105 hover:shadow-[0_0_25px_rgba(243,112,33,0.6)] flex items-center gap-2 group"
                >
                  {t('visitFptu:gallery.labels.startTour')} <ChevronRight className="w-5 h-5 ml-1 group-hover:translate-x-1 transition-transform" />
                </button>
                <button
                  onClick={() => setIsSidebarOpen(true)}
                  className="px-8 py-3.5 bg-white/10 hover:bg-white/20 text-white rounded-full font-medium backdrop-blur-md border border-white/20 transition-all hover:scale-105"
                >
                  {t('visitFptu:gallery.labels.viewAreas')}
                </button>
              </>
            )}
          </motion.div>

          {hasContent && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              transition={{ duration: 1, delay: 1.2 }}
              className="mt-14 grid grid-cols-2 sm:grid-cols-3 gap-4 sm:gap-8 max-w-2xl w-full border-t border-white/10 pt-6"
            >
              <div className="flex flex-col items-center">
                <span className="text-2xl font-bold text-white mb-1 drop-shadow-md">{areas.length}</span>
                <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">{t('visitFptu:gallery.labels.areasCount')}</span>
              </div>
              <div className="flex flex-col items-center">
                <span className="text-2xl font-bold text-white mb-1 drop-shadow-md">{flatLocations.length}</span>
                <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">{t('visitFptu:gallery.labels.locationsCount')}</span>
              </div>
              <div className="hidden sm:flex flex-col items-center">
                <span className="text-2xl font-bold text-white mb-1 drop-shadow-md">5</span>
                <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">{t('visitFptu:gallery.labels.nationwideCampuses')}</span>
              </div>
            </motion.div>
          )}
        </div>
      </div>

      {/* ── Area Showcase: fullscreen area cover + location-cover thumbnail rail (BR-PGAL-AREA) ── */}
      <AnimatePresence>
        {showcaseArea && !activeLocationId && locationShowcaseId == null && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0 }}
            className="fixed top-[64px] inset-x-0 bottom-0 z-30 overflow-hidden"
          >
            {/* Background: area cover (image or MP4 video), fullscreen, cover-fit (BR-PGAL-AREA-02/03) */}
            <ShowcaseBackground
              src={showcaseArea.areaCoverUrl}
              fallbackSrc={fallback.bg}
              alt={localizedDbText(showcaseArea.areaName, showcaseArea.areaNameEn, language)}
              mediaType={showcaseArea.areaCoverMediaType}
            />

            {/* Dark overlay for legibility / cinematic feel (BR-PGAL-AREA-04) */}
            <div
              className="absolute inset-0 z-[1] pointer-events-none"
              style={{
                background:
                  "linear-gradient(to right, rgba(0,0,0,0.62), rgba(0,0,0,0.30) 45%, rgba(0,0,0,0.55)), rgba(0,0,0,0.16)",
              }}
            />

            {/* Close showcase → back to hero */}
            <button
              onClick={closeShowcase}
              title={t('visitFptu:gallery.actions.close')}
              className="absolute top-6 right-6 z-[6] w-10 h-10 flex items-center justify-center rounded-full bg-black/40 hover:bg-white/20 text-white border border-white/25 backdrop-blur-md transition-all hover:scale-110 group"
            >
              <X className="w-5 h-5 group-hover:rotate-90 transition-transform duration-300" />
            </button>

            {/* Area name — bottom-left, white, bold (BR-PGAL-AREA-05) */}
            <div
              className={`absolute z-[3] bottom-14 sm:bottom-20 max-w-[720px] transition-all duration-500 ${
                isSidebarOpen ? "left-8 md:left-[19rem]" : "left-6 md:left-16"
              }`}
            >
              <span className="uppercase tracking-[0.3em] text-white/70 text-[11px] sm:text-xs font-semibold drop-shadow">
                {t('visitFptu:gallery.labels.areasCount')}
              </span>
              <h2 className="mt-2 text-white font-black text-4xl sm:text-5xl md:text-6xl leading-[1.05] tracking-tight drop-shadow-[0_12px_32px_rgba(0,0,0,0.45)]">
                {localizedDbText(showcaseArea.areaName, showcaseArea.areaNameEn, language)}
              </h2>

              {/* Prev / next area navigation (BR-PGAL-AREA §6, loops) */}
              {areas.length > 1 && (
                <div className="mt-5 flex flex-wrap gap-3">
                  <button
                    onClick={() => stepArea(-1)}
                    className="inline-flex items-center gap-2 px-4 py-2.5 rounded-full text-white font-bold text-sm bg-white/10 border border-white/25 backdrop-blur-md transition-all hover:bg-white/20 hover:border-white/50 hover:-translate-y-0.5"
                  >
                    <ChevronLeft className="w-4 h-4" /> {t('visitFptu:gallery.labels.prevArea')}
                  </button>
                  <button
                    onClick={() => stepArea(1)}
                    className="inline-flex items-center gap-2 px-4 py-2.5 rounded-full text-white font-bold text-sm bg-white/10 border border-white/25 backdrop-blur-md transition-all hover:bg-white/20 hover:border-white/50 hover:-translate-y-0.5"
                  >
                    {t('visitFptu:gallery.labels.nextArea')} <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              )}
            </div>

            {/* Location-cover thumbnail rail — right side, vertical (BR-PGAL-AREA-07/08) */}
            <div className="absolute right-3 sm:right-8 top-1/2 -translate-y-1/2 z-[4] flex flex-col items-center gap-3">
              {showcaseTotal > 0 ? (
                <>
                  <button
                    onClick={() => stepThumbnail(-1)}
                    title={t('visitFptu:gallery.actions.up')}
                    className="w-10 h-10 rounded-full border border-white/30 bg-black/30 text-white flex items-center justify-center backdrop-blur-md transition-all hover:bg-white/20 hover:scale-105"
                  >
                    <ChevronUp className="w-5 h-5" />
                  </button>

                  {/* Fixed section label — sits above the first thumbnail, never scrolls away */}
                  <div className="text-white/85 text-[11px] font-bold uppercase tracking-[0.18em] text-center drop-shadow">
                    {t('visitFptu:gallery.labels.specificLocation')}
                  </div>

                  {/* Rail + the active thumbnail's name shown to its LEFT */}
                  <div className="relative">
                    <div
                      className="absolute right-full mr-1.5 -translate-y-1/2 pointer-events-none z-20"
                      style={{ top: activeThumbY }}
                    >
                      <span className="inline-block max-w-[44vw] sm:max-w-[240px] truncate px-3.5 py-1.5 rounded-full bg-black/50 border border-white/20 backdrop-blur-md text-white text-sm font-semibold drop-shadow-[0_8px_20px_rgba(0,0,0,0.5)]">
                        {localizedDbText(
                          showcaseLocations[safeThumbIndex]?.locationName,
                          showcaseLocations[safeThumbIndex]?.locationNameEn,
                          language,
                        )}
                      </span>
                    </div>

                    <div
                      ref={thumbRailRef}
                      onScroll={syncActiveThumbY}
                      className="relative flex flex-col items-center gap-4 max-h-[min(52vh,404px)] overflow-y-auto px-6 py-4 [&::-webkit-scrollbar]:hidden [-ms-overflow-style:none] [scrollbar-width:none]"
                    >
                      {showcaseLocations.map((loc, idx) => {
                        const active = idx === safeThumbIndex;
                        const locDisplayName = localizedDbText(loc.locationName, loc.locationNameEn, language);
                        return (
                          <button
                            key={loc.locationId}
                            ref={(el) => {
                              thumbRefs.current[idx] = el;
                            }}
                            onClick={() => {
                              setActiveLocationThumbnailIndex(idx);
                              openLocationShowcase(loc.locationId); // AC-LOC-01: open Location Showcase
                            }}
                            title={locDisplayName}
                            className={`relative w-[72px] h-[72px] sm:w-[78px] sm:h-[78px] rounded-[10px] overflow-hidden cursor-pointer shrink-0 transition-all duration-300 ${
                              active
                                ? "z-10 border-2 border-white opacity-100 scale-[1.14] shadow-[0_0_0_3px_rgba(255,255,255,0.20),0_0_26px_rgba(255,255,255,0.6),0_10px_24px_rgba(0,0,0,0.45)]"
                                : "border-2 border-white/25 opacity-65 hover:opacity-90 hover:border-white/50"
                            }`}
                          >
                            <LocationThumbImage url={loc.locationCoverUrl} alt={locDisplayName} />
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  <button
                    onClick={() => stepThumbnail(1)}
                    title={t('visitFptu:gallery.actions.down')}
                    className="w-10 h-10 rounded-full border border-white/30 bg-black/30 text-white flex items-center justify-center backdrop-blur-md transition-all hover:bg-white/20 hover:scale-105"
                  >
                    <ChevronDown className="w-5 h-5" />
                  </button>

                  {/* Counter current/total, e.g. 03/12 (BR-PGAL-AREA-10) */}
                  <div className="mt-1 text-white font-extrabold tracking-[0.08em] text-sm text-center drop-shadow-[0_8px_20px_rgba(0,0,0,0.45)]">
                    {pad2(safeThumbIndex + 1)}/{pad2(showcaseTotal)}
                  </div>
                </>
              ) : (
                <div className="w-40 px-4 py-6 rounded-2xl bg-black/35 border border-white/20 text-white/80 text-sm text-center backdrop-blur-md">
                  {t('visitFptu:gallery.labels.noLocationsYet')}
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Location Showcase: location cover bg + MEDIA column + "Đoàn khách" row (AC-LOC/MEDIA/DELEGATION) ── */}
      <AnimatePresence>
        {locationShowcaseId != null && locationShowcaseLocation && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0 }}
            className="fixed top-[64px] inset-x-0 bottom-0 z-30 overflow-hidden"
          >
            {/* Background: location cover, fullscreen (AC-LOC-01/02) — falls back to area cover / campus art */}
            <ShowcaseBackground
              src={locationShowcaseLocation.locationCoverUrl}
              fallbackSrc={locationShowcaseArea?.areaCoverUrl || fallback.bg}
              alt={localizedDbText(
                locationShowcaseLocation.locationName, locationShowcaseLocation.locationNameEn, language)}
            />
            <div
              className="absolute inset-0 z-[1] pointer-events-none"
              style={{
                background:
                  "linear-gradient(to right, rgba(0,0,0,0.66), rgba(0,0,0,0.34) 45%, rgba(0,0,0,0.55)), rgba(0,0,0,0.18)",
              }}
            />

            {/* Close → back to Area Showcase */}
            <button
              onClick={closeLocationShowcase}
              title={t('visitFptu:gallery.actions.close')}
              className="absolute top-6 right-6 z-[6] w-10 h-10 flex items-center justify-center rounded-full bg-black/40 hover:bg-white/20 text-white border border-white/25 backdrop-blur-md transition-all hover:scale-110 group"
            >
              <X className="w-5 h-5 group-hover:rotate-90 transition-transform duration-300" />
            </button>

            {/* Bottom-left: area / location names, prev-next arrows, delegation row */}
            <div
              className={`absolute z-[3] bottom-10 sm:bottom-14 max-w-[min(62vw,760px)] transition-all duration-500 ${
                isSidebarOpen ? "left-8 md:left-[19rem]" : "left-6 md:left-16"
              }`}
            >
              {/* Eyebrow — same style as the Area Showcase "Khu vực" label */}
              <span className="uppercase tracking-[0.3em] text-white/70 text-[11px] sm:text-xs font-semibold drop-shadow">
                {t('visitFptu:gallery.labels.locationPrefix')}
              </span>

              {/* Name line: "TÊN KHU VỰC / TÊN VỊ TRÍ", flanked by prev/next-location arrows (AC-LOC-04/05) */}
              <div className="mt-2 flex items-center gap-3 sm:gap-4">
                {locationSiblings.length > 1 && (
                  <button
                    onClick={() => stepLocationShowcase(-1)}
                    title={t('visitFptu:gallery.labels.prevLocation')}
                    className="w-11 h-11 shrink-0 rounded-full border border-white/28 bg-white/10 text-white flex items-center justify-center backdrop-blur-md transition-all hover:bg-white/20 hover:border-white/50 hover:-translate-y-0.5"
                  >
                    <ChevronLeft className="w-5 h-5" />
                  </button>
                )}
                <h2 className="min-w-0 text-white font-black text-3xl sm:text-4xl md:text-5xl leading-[1.05] tracking-tight drop-shadow-[0_12px_32px_rgba(0,0,0,0.45)]">
                  <span className="uppercase">
                    {localizedDbText(locationShowcaseArea?.areaName, locationShowcaseArea?.areaNameEn, language)}
                  </span>
                  <span className="font-medium text-white/75 text-xl sm:text-2xl md:text-3xl">
                    {" "}/ {localizedDbText(
                      locationShowcaseLocation.locationName, locationShowcaseLocation.locationNameEn, language)}
                  </span>
                </h2>
                {locationSiblings.length > 1 && (
                  <button
                    onClick={() => stepLocationShowcase(1)}
                    title={t('visitFptu:gallery.labels.nextLocation')}
                    className="w-11 h-11 shrink-0 rounded-full border border-white/28 bg-white/10 text-white flex items-center justify-center backdrop-blur-md transition-all hover:bg-white/20 hover:border-white/50 hover:-translate-y-0.5"
                  >
                    <ChevronRight className="w-5 h-5" />
                  </button>
                )}
              </div>

              {/* "Đoàn khách đã tới thăm" — VISIT_DELEGATION row, styled exactly like the MEDIA column */}
              {delegationItems.length > 0 && (
                <div className="mt-10 sm:mt-12">
                  <HorizontalThumbRail
                    items={delegationItems}
                    activeIndex={activeDelegationIndex}
                    onSelect={(i) => {
                      setActiveDelegationIndex(i);
                      openItemDetailAt(delegationItems.map((d) => d.galleryItemId), i); // open detail modal
                    }}
                    onStep={stepDelegation}
                    keyOf={(it) => it.galleryItemId}
                    renderThumb={(it) => <ShowcaseItemThumb item={it} />}
                    title={t('visitFptu:gallery.labels.delegationsVisited')}
                    t={t}
                  />
                </div>
              )}
            </div>

            {/* Right MEDIA column (AC-MEDIA-01..05) — hidden entirely when the location has no MEDIA item */}
            {isShowcaseLoading ? (
              <div className="absolute right-8 sm:right-12 top-1/2 -translate-y-1/2 z-[4] text-white/80">
                <Loader2 className="w-7 h-7 animate-spin" />
              </div>
            ) : mediaItems.length > 0 ? (
              <div className="absolute right-3 sm:right-8 top-1/2 -translate-y-1/2 z-[4]">
                <VerticalThumbRail
                  items={mediaItems}
                  activeIndex={safeMediaIndex}
                  onSelect={(i) => {
                    setActiveMediaIndex(i);
                    openItemDetailAt(mediaItems.map((m) => m.galleryItemId), i); // open detail modal
                  }}
                  onStep={stepMediaThumbnail}
                  keyOf={(it) => it.galleryItemId}
                  renderThumb={(it) => <ShowcaseItemThumb item={it} />}
                  label={t('visitFptu:gallery.labels.visitingImages')}
                  t={t}
                />
              </div>
            ) : null}
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Gallery item detail modal (click a MEDIA / "Đoàn khách" thumbnail) ── */}
      <AnimatePresence>
        {detailItemId != null && (
          <GalleryItemDetailModal
            detail={detailData}
            isLoading={isItemDetailLoading}
            notFound={itemDetailNotFound}
            onClose={closeItemDetail}
            onPrev={() => stepItemDetail(-1)}
            onNext={() => stepItemDetail(1)}
            hasNav={detailItems.length > 1}
            t={t}
            language={language}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
