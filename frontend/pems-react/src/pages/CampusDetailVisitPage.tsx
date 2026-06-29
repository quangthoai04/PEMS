/**
 * Trang CampusDetailVisitPage (Public)
 * Hiển thị VisitFPTU Gallery công khai của một campus theo 2 tầng (UC_Public_VisitFPTU_Gallery_Location_Grid):
 *   Tầng 1 — LOCATION_GRID: click một vị trí → lưới toàn bộ gallery item của vị trí đó (mỗi card = media chính).
 *   Tầng 2 — ITEM_DETAIL: click một card → chi tiết gallery item (breadcrumb, title, mô tả, toàn bộ media).
 * Dữ liệu lấy từ public API. Không có virtual tour 360 (BR-PGAL-17).
 */

import React, { useState, useEffect, useCallback, useMemo, useRef } from "react";
import { useParams, useNavigate, useSearchParams } from "react-router-dom";
import { motion, AnimatePresence } from "motion/react";
import {
  ChevronRight,
  ChevronLeft,
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
  Play,
} from "lucide-react";

import bgHN from "../assets/FPTbanner_visit/hola_new.jpg";
import bgHCM from "../assets/FPTbanner_visit/HCM.png";
import bgCT from "../assets/FPTbanner_visit/CanTho.png";
import bgDN from "../assets/FPTbanner_visit/DaNang.png";
import bgQN from "../assets/FPTbanner_visit/QuyNhon.png";

import { publicVisitFptuApi } from "../features/visit-fptu/publicVisitFptuApi";
import type {
  PublicGalleryArea,
  PublicGalleryGridItem,
  PublicGalleryItemDetail,
  PublicLocationGalleryGrid,
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

const mediaKindLabel = (kind?: string) => {
  const k = (kind || "").toUpperCase();
  if (k === "VIDEO") return "Video";
  if (k === "MIXED") return "Hỗn hợp";
  return "Hình ảnh";
};

/** Resolves the media URL through the dev server (vite proxies /api → backend). URLs come absolute. */
const mediaSrc = (url?: string | null) => url || "";

/**
 * One card in the location album grid (museum-style): primary media on a 16/10 frame, gradient bottom,
 * media-kind pill, title, and a centered "Xem chi tiết" CTA on hover (BR-PGAL-GRID-02/05/06).
 */
function GridCard({ item, onOpen }: { item: PublicGalleryGridItem; onOpen: () => void }) {
  const [failed, setFailed] = useState(false);
  const pm = item.primaryMedia;
  const video = isVideo(pm?.mediaType);
  // For a video card prefer the thumbnail; fall back to the video element itself if no thumbnail.
  const imgSrc = video ? pm?.thumbnailUrl : pm?.url;

  return (
    <button
      onClick={onOpen}
      title={item.title}
      className="group relative aspect-[16/10] rounded-[20px] overflow-hidden cursor-pointer outline-none bg-white/[0.06] shadow-[0_18px_45px_rgba(0,0,0,0.28)] transition-all duration-300 hover:-translate-y-1.5 hover:shadow-[0_28px_70px_rgba(0,0,0,0.42)] focus-visible:ring-2 focus-visible:ring-fpt-orange"
    >
      {!pm || failed ? (
        <div className="absolute inset-0 flex items-center justify-center text-white/40">
          <ImageOff className="w-9 h-9" />
        </div>
      ) : imgSrc ? (
        <img
          src={mediaSrc(imgSrc)}
          alt={pm.altText || item.title}
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
        {mediaKindLabel(item.mediaKind)}
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
          <ZoomIn className="w-4 h-4" /> Xem chi tiết
        </span>
      </div>

      {/* Title at the bottom */}
      <div className="absolute inset-x-0 bottom-0 z-[3] p-3 text-left pointer-events-none">
        <p className="text-white text-sm font-bold leading-tight line-clamp-2 drop-shadow">{item.title}</p>
      </div>
    </button>
  );
}

export function CampusDetailVisitPage() {
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
  const [activeLocationId, setActiveLocationId] = useState<number | null>(null);
  const [selectedAreaId, setSelectedAreaId] = useState<number | null>(null);
  const [selectedGalleryItemId, setSelectedGalleryItemId] = useState<number | null>(null);
  const [currentMediaIndex, setCurrentMediaIndex] = useState(0);
  const [isLightboxOpen, setIsLightboxOpen] = useState(false);
  const [zoomScale, setZoomScale] = useState(1);
  const [showShareMenu, setShowShareMenu] = useState(false);
  const [failedMediaIds, setFailedMediaIds] = useState<Set<number>>(new Set());
  const [isSpeaking, setIsSpeaking] = useState(false);

  const gridRequestId = useRef(0);
  const detailRequestId = useRef(0);

  const areas: PublicGalleryArea[] = nav?.areas ?? [];
  const campusName = nav?.campus?.campusName || `Campus ${campusCode}`;
  const hasContent = areas.length > 0;

  const isDetailView = selectedGalleryItemId != null;

  // Flattened, ordered list (for finding a location's parent area, defaults, etc.).
  const flatLocations = useMemo(
    () =>
      areas.flatMap((a) =>
        a.locations.map((l) => ({ ...l, areaId: a.areaId, areaName: a.areaName })),
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
            ? "Không tìm thấy cơ sở này."
            : "Không thể tải dữ liệu Gallery. Vui lòng thử lại.",
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
      setGridError("Vị trí này hiện chưa có nội dung Gallery công khai.");
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
      setDetailNotice("Nội dung này hiện không còn được hiển thị.");
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

  // Deep-link / reload: ?locationId opens the grid; +?itemId also opens the item detail.
  const deepLinkHandled = useRef(false);
  useEffect(() => {
    if (isNavLoading || !hasContent || deepLinkHandled.current) return;
    const locParam = Number(searchParams.get("locationId"));
    const itemParam = Number(searchParams.get("itemId"));
    const loc = locParam ? findLocation(locParam) : null;
    if (locParam && loc) {
      deepLinkHandled.current = true;
      setIsSidebarOpen(true);
      setSelectedAreaId(loc.areaId);
      setActiveLocationId(locParam);
      void loadLocationGrid(locParam);
      if (itemParam) {
        setSelectedGalleryItemId(itemParam);
        void loadItemDetail(itemParam);
      }
    }
  }, [isNavLoading, hasContent, searchParams, findLocation, loadLocationGrid, loadItemDetail]);

  // Lock body scroll while the gallery overlay is open.
  useEffect(() => {
    document.body.style.overflow = activeLocationId ? "hidden" : "";
    return () => {
      document.body.style.overflow = "";
    };
  }, [activeLocationId]);

  // ── Navigation helpers ───────────────────────────────────────────────────
  const startTour = useCallback(() => {
    if (!hasContent) return;
    setIsSidebarOpen(true);
    const first = areas[0]?.locations[0];
    if (first) openLocation(first.locationId);
  }, [areas, hasContent, openLocation]);

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
    `${gridItems.length} nội dung`,
    gridImageCount > 0 ? `${gridImageCount} hình ảnh` : null,
    gridVideoCount > 0 ? `${gridVideoCount} video` : null,
    gridMixedCount > 0 ? `${gridMixedCount} hỗn hợp` : null,
  ].filter(Boolean).join(" · ");

  // Sibling locations of the current area → quick-switch chips under the header.
  const currentArea = areas.find((a) => a.areaId === selectedAreaId) ?? null;
  const siblingLocations = currentArea?.locations ?? [];

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

  // ESC closes lightbox → detail → overlay.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      if (isLightboxOpen) setIsLightboxOpen(false);
      else if (isDetailView) backToGrid();
      else if (activeLocationId) closeOverlay();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [isLightboxOpen, isDetailView, activeLocationId, backToGrid, closeOverlay]);

  // "Nghe thuyết minh": read the description aloud via the browser's speech synthesis (Vietnamese).
  const stopNarration = useCallback(() => {
    if (typeof window !== "undefined" && window.speechSynthesis) window.speechSynthesis.cancel();
    setIsSpeaking(false);
  }, []);

  const toggleNarration = useCallback(() => {
    const synth = typeof window !== "undefined" ? window.speechSynthesis : undefined;
    const text = detail?.galleryItem.description?.trim();
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

  const shareCurrentLink = (channel: "copy" | "facebook" | "twitter") => {
    const url = window.location.href;
    if (channel === "copy") {
      navigator.clipboard.writeText(url);
      setShowShareMenu(false);
      return;
    }
    const target =
      channel === "facebook"
        ? `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(url)}`
        : `https://twitter.com/intent/tweet?url=${encodeURIComponent(url)}`;
    window.open(target, "_blank");
    setShowShareMenu(false);
  };

  return (
    <div className="relative min-h-[calc(100vh-64px)] w-full flex flex-col bg-gray-900">
      {/* ── Floating sidebar: areas + hover flyout of locations ── */}
      {hasContent && (
        <div
          className={`fixed top-1/2 left-0 z-50 flex transition-transform duration-500 ease-in-out ${
            isSidebarOpen ? "translate-x-4 md:translate-x-6 -translate-y-1/2" : "-translate-x-full -translate-y-1/2"
          }`}
        >
          <div className="w-56 h-auto max-h-[calc(100vh-140px)] bg-black/30 backdrop-blur-xl flex flex-col overflow-visible rounded-2xl shadow-[0_8px_32px_rgba(0,0,0,0.4)] border border-white/20">
            <nav className="flex-1 flex flex-col relative">
              <div className="absolute -inset-0.5 bg-gradient-to-b from-fpt-orange/20 to-transparent opacity-50 rounded-2xl pointer-events-none"></div>
              {areas.map((area, index) => {
                const mediaKinds = new Set(area.locations.map((l) => l.mediaKind?.toUpperCase()));
                const showVideoIcon = mediaKinds.has("VIDEO") && mediaKinds.size === 1;
                return (
                  <div
                    key={area.areaId}
                    className="relative"
                    onMouseEnter={() => setHoveredAreaId(area.areaId)}
                    onMouseLeave={() => setHoveredAreaId(null)}
                  >
                    <button
                      onClick={() => {
                        const firstLoc = area.locations[0];
                        if (firstLoc) openLocation(firstLoc.locationId);
                      }}
                      className={`w-full flex items-center justify-between px-4 py-3 border-b border-white/10 transition-all duration-300 text-left group relative z-10 ${
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
                        {area.areaName}
                      </span>
                      <span className="opacity-90">
                        {showVideoIcon ? <VideoIcon className="w-5 h-5" /> : <ImageIcon className="w-5 h-5" />}
                      </span>
                    </button>

                    {/* Hover flyout: location list (hover does not open anything; click does) */}
                    <AnimatePresence>
                      {hoveredAreaId === area.areaId && area.locations.length > 0 && (
                        <motion.div
                          initial={{ opacity: 0, x: -10, scale: 0.95 }}
                          animate={{ opacity: 1, x: 0, scale: 1 }}
                          exit={{ opacity: 0, x: -10, scale: 0.95 }}
                          transition={{ duration: 0.2, ease: "easeOut" }}
                          className="absolute top-0 left-full ml-2 w-72 max-h-[60vh] overflow-y-auto backdrop-blur-2xl rounded-2xl shadow-[0_10px_40px_rgba(0,0,0,0.5)] z-50 border border-white/20 [&::-webkit-scrollbar]:hidden [-ms-overflow-style:none] [scrollbar-width:none]"
                          style={{ background: "linear-gradient(135deg, rgba(235,116,45,0.85) 0%, rgba(200,80,30,0.95) 100%)" }}
                        >
                          <div className="flex flex-col py-3">
                            {area.locations.map((loc) => (
                              <button
                                key={loc.locationId}
                                onClick={() => openLocation(loc.locationId)}
                                className={`w-full text-left px-5 py-3 text-sm transition-all flex justify-between items-center group/sub ${
                                  activeLocationId === loc.locationId
                                    ? "bg-white/25 text-white"
                                    : "text-white hover:bg-white/20"
                                }`}
                              >
                                <span className="font-medium tracking-wide drop-shadow-sm group-hover/sub:translate-x-1 transition-transform">
                                  {loc.locationName}
                                </span>
                                <span className="flex items-center gap-2 shrink-0">
                                  {loc.publicGalleryItemCount > 0 && (
                                    <span className="text-[10px] font-bold bg-white/25 rounded-full px-1.5 py-0.5">
                                      {loc.publicGalleryItemCount}
                                    </span>
                                  )}
                                  <MapPin className="w-4 h-4 opacity-70 group-hover/sub:opacity-100 group-hover/sub:scale-110 transition-all" />
                                </span>
                              </button>
                            ))}
                          </div>
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </div>
                );
              })}
            </nav>
          </div>

          {/* Sidebar toggle */}
          <button
            onClick={() => setIsSidebarOpen(!isSidebarOpen)}
            className="absolute top-6 -right-8 w-8 h-12 bg-black/40 backdrop-blur-xl border border-white/20 border-l-0 rounded-r-xl flex items-center justify-center text-white hover:bg-fpt-orange hover:border-fpt-orange hover:shadow-[0_0_20px_rgba(243,112,33,0.5)] transition-all cursor-pointer shadow-xl group"
          >
            {isSidebarOpen ? (
              <ChevronLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
            ) : (
              <ChevronRight className="w-5 h-5 group-hover:translate-x-1 transition-transform" />
            )}
          </button>
        </div>
      )}

      {/* Back button */}
      <motion.button
        initial={{ opacity: 0, x: -20 }}
        animate={{ opacity: 1, x: 0 }}
        transition={{ delay: 0.2 }}
        onClick={() => navigate("/visit-fptu")}
        className="absolute top-24 left-6 sm:top-28 z-40 p-3 bg-black/30 backdrop-blur-md rounded-full border border-white/20 text-white hover:bg-fpt-orange hover:border-fpt-orange hover:scale-110 hover:shadow-[0_0_20px_rgba(243,112,33,0.5)] transition-all flex items-center gap-2 group"
      >
        <ArrowLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
        <span className="hidden sm:inline font-medium pr-2 text-sm tracking-wide">Trở Về</span>
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
            {fallback.description}
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
                <span>Đang tải nội dung Gallery…</span>
              </div>
            ) : navError ? (
              <div className="px-6 py-3 rounded-full bg-red-500/20 border border-red-300/30 text-white">
                {navError}
              </div>
            ) : !hasContent ? (
              <div className="px-6 py-3 rounded-full bg-white/10 border border-white/20 text-white backdrop-blur-md">
                Campus này hiện chưa có nội dung Gallery công khai.
              </div>
            ) : (
              <>
                <button
                  onClick={startTour}
                  className="px-8 py-3.5 bg-fpt-orange hover:bg-fpt-orange/90 text-white rounded-full font-medium transition-all hover:scale-105 hover:shadow-[0_0_25px_rgba(243,112,33,0.6)] flex items-center gap-2 group"
                >
                  Bắt đầu tham quan <ChevronRight className="w-5 h-5 ml-1 group-hover:translate-x-1 transition-transform" />
                </button>
                <button
                  onClick={() => setIsSidebarOpen(true)}
                  className="px-8 py-3.5 bg-white/10 hover:bg-white/20 text-white rounded-full font-medium backdrop-blur-md border border-white/20 transition-all hover:scale-105"
                >
                  Xem các khu vực
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
                <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">Khu vực</span>
              </div>
              <div className="flex flex-col items-center">
                <span className="text-2xl font-bold text-white mb-1 drop-shadow-md">{flatLocations.length}</span>
                <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">Vị trí tham quan</span>
              </div>
              <div className="hidden sm:flex flex-col items-center">
                <span className="text-2xl font-bold text-white mb-1 drop-shadow-md">5</span>
                <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">Cơ sở toàn quốc</span>
              </div>
            </motion.div>
          )}
        </div>
      </div>

      {/* ── Gallery overlay ── */}
      <AnimatePresence>
        {activeLocationId && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.4 }}
            className={`fixed top-[64px] inset-x-0 bottom-0 z-40 flex items-center justify-center transition-all duration-500 ${isSidebarOpen ? "md:pl-56" : ""} p-4 sm:p-6 md:p-8`}
          >
            <div className="fixed inset-0 bg-black/50 backdrop-blur-md" onClick={closeOverlay} />

            <motion.div
              initial={{ opacity: 0, scale: 0.92, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.96, y: 10 }}
              transition={{ duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
              className={`relative z-10 w-full flex flex-col gap-4 overflow-y-auto [&::-webkit-scrollbar]:hidden [-ms-overflow-style:none] [scrollbar-width:none] drop-shadow-2xl ${
                isDetailView ? "max-w-6xl h-[82vh]" : "max-w-[1280px] max-h-[88vh]"
              }`}
            >
              {/* ====== Tier 2: ITEM DETAIL ====== */}
              {isDetailView ? (
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 lg:gap-6 pb-6 pr-2 pl-2 flex-1 min-h-0">
                  {/* Left: breadcrumb / title / description / back-to-grid */}
                  <div className="lg:col-span-5 flex flex-col gap-4 h-full">
                    <motion.div className="bg-white/15 dark:bg-black/20 backdrop-blur-2xl border border-white/30 rounded-[1.5rem] sm:rounded-[2rem] p-5 sm:p-6 shadow-[0_8px_32px_rgba(0,0,0,0.1)] relative group transition-all duration-500 hover:shadow-[0_8px_40px_rgba(255,255,255,0.1)] shrink-0">
                      <div className="absolute inset-0 overflow-hidden rounded-[inherit] pointer-events-none">
                        <div className="absolute -top-20 -right-20 w-40 h-40 bg-fpt-orange/20 rounded-full blur-3xl group-hover:bg-fpt-orange/30 transition-colors duration-500"></div>
                        <div className="absolute -bottom-20 -left-20 w-40 h-40 bg-blue-500/20 rounded-full blur-3xl group-hover:bg-blue-400/30 transition-colors duration-500"></div>
                      </div>

                      <div className="flex items-center justify-between mb-5 relative z-10 gap-3">
                        <div className="inline-flex items-center gap-2 px-4 py-1.5 bg-fpt-orange/90 text-white font-semibold text-[10px] sm:text-xs tracking-widest uppercase rounded-full border border-white/30 backdrop-blur-md shadow-[0_0_15px_rgba(243,112,33,0.4)] max-w-full overflow-hidden">
                          <span className="truncate">{breadcrumbArea}</span>
                          <ChevronRight className="w-3.5 h-3.5 shrink-0 opacity-80" />
                          <span className="truncate">{breadcrumbLocation}</span>
                        </div>
                        <div className="flex items-center gap-2 relative shrink-0">
                          <button
                            onClick={() => setShowShareMenu(!showShareMenu)}
                            className="text-white/70 hover:text-fpt-orange transition-all hover:scale-110"
                            title="Chia sẻ"
                          >
                            <Share2 className="w-5 h-5" />
                          </button>
                          <button
                            onClick={closeOverlay}
                            title="Đóng"
                            className="w-8 h-8 flex items-center justify-center rounded-full bg-white/20 hover:bg-white/40 text-white border border-white/30 transition-all hover:scale-110 group"
                          >
                            <X className="w-4 h-4 group-hover:rotate-90 transition-transform duration-300" />
                          </button>
                          <AnimatePresence>
                            {showShareMenu && (
                              <motion.div
                                initial={{ opacity: 0, y: 10, scale: 0.9 }}
                                animate={{ opacity: 1, y: 0, scale: 1 }}
                                exit={{ opacity: 0, y: 10, scale: 0.9 }}
                                className="absolute right-0 top-full mt-2 w-48 bg-black/80 backdrop-blur-xl border border-white/20 rounded-2xl shadow-2xl p-2 z-50 flex flex-col gap-1"
                              >
                                <button onClick={() => shareCurrentLink("copy")} className="flex items-center gap-3 px-3 py-2 text-sm text-white/90 hover:text-white hover:bg-white/10 rounded-xl transition-colors text-left">
                                  <LinkIcon className="w-4 h-4" /> Sao chép liên kết
                                </button>
                                <button onClick={() => shareCurrentLink("facebook")} className="flex items-center gap-3 px-3 py-2 text-sm text-white/90 hover:text-white hover:bg-blue-500/20 rounded-xl transition-colors text-left">
                                  <Facebook className="w-4 h-4" /> Facebook
                                </button>
                                <button onClick={() => shareCurrentLink("twitter")} className="flex items-center gap-3 px-3 py-2 text-sm text-white/90 hover:text-white hover:bg-sky-500/20 rounded-xl transition-colors text-left">
                                  <Twitter className="w-4 h-4" /> Twitter
                                </button>
                              </motion.div>
                            )}
                          </AnimatePresence>
                        </div>
                      </div>

                      <h3 className="text-2xl sm:text-3xl font-black text-transparent bg-clip-text bg-gradient-to-br from-white via-white to-white/70 mb-3 leading-tight tracking-tight drop-shadow-sm relative z-10 min-h-[2rem]">
                        {isDetailLoading ? "Đang tải…" : detail?.galleryItem.title || "—"}
                      </h3>
                      <div className="w-24 h-1.5 bg-gradient-to-r from-fpt-orange to-transparent rounded-full opacity-80 relative z-10" />
                    </motion.div>

                    <div className="bg-white/80 dark:bg-white/10 backdrop-blur-3xl border border-white/40 rounded-[1.5rem] sm:rounded-[2rem] p-5 sm:p-6 shadow-[0_8px_32px_rgba(0,0,0,0.1)] grow flex flex-col justify-between overflow-y-auto overflow-x-hidden relative">
                      <div className="flex justify-between items-start gap-4 mb-4">
                        <div className="prose prose-base text-black dark:text-white font-light leading-relaxed grow min-w-0">
                          {isDetailLoading ? (
                            <p className="text-gray-600 dark:text-gray-300">Đang tải mô tả…</p>
                          ) : detailNotice ? (
                            <p className="text-red-600 dark:text-red-400 font-medium">{detailNotice}</p>
                          ) : (
                            <p className="text-black dark:text-gray-100 whitespace-pre-line break-words [overflow-wrap:anywhere] first-letter:text-4xl first-letter:font-bold first-letter:text-fpt-orange first-letter:mr-1 first-letter:float-left">
                              {detail?.galleryItem.description}
                            </p>
                          )}
                        </div>
                        {detail && !detailNotice && !isDetailLoading && (
                          <button
                            onClick={toggleNarration}
                            title={isSpeaking ? "Dừng thuyết minh" : "Nghe thuyết minh"}
                            className={`shrink-0 flex items-center justify-center w-10 h-10 sm:w-12 sm:h-12 rounded-full transition-all duration-300 hover:scale-110 hover:shadow-[0_0_15px_rgba(243,112,33,0.4)] ${
                              isSpeaking
                                ? "bg-fpt-orange text-white animate-pulse"
                                : "bg-fpt-orange/10 text-fpt-orange hover:bg-fpt-orange hover:text-white"
                            }`}
                          >
                            {isSpeaking ? <VolumeX className="w-5 h-5 sm:w-6 sm:h-6" /> : <Volume2 className="w-5 h-5 sm:w-6 sm:h-6" />}
                          </button>
                        )}
                      </div>

                      {/* Back to grid */}
                      <div className="flex items-center justify-start pt-6 border-t border-gray-200 dark:border-white/10 mt-auto">
                        <button
                          onClick={backToGrid}
                          className="flex items-center gap-2 px-4 py-2 text-sm font-bold text-gray-700 dark:text-gray-200 hover:text-fpt-orange dark:hover:text-fpt-orange hover:bg-fpt-orange/10 dark:hover:bg-fpt-orange/20 rounded-xl transition-all hover:scale-105 active:scale-95"
                        >
                          <ArrowLeft className="w-5 h-5" />
                          Quay lại danh sách hình ảnh
                        </button>
                      </div>
                    </div>
                  </div>

                  {/* Right: media viewer (carousel of THIS item's media) */}
                  <div className="lg:col-span-7 flex flex-col gap-4 lg:gap-6 min-h-[300px] md:min-h-[400px] lg:min-h-0 h-full">
                    <div className="bg-white/15 dark:bg-white/5 backdrop-blur-2xl border border-white/30 rounded-[1.5rem] sm:rounded-[2rem] shadow-[0_8px_32px_rgba(0,0,0,0.15)] w-full h-full relative overflow-hidden group hover:border-white/50 transition-all duration-500 flex flex-col">
                      <div className="relative w-full h-full rounded-[1.5rem] sm:rounded-[2rem] overflow-hidden bg-black/30">
                        {isDetailLoading ? (
                          <div className="absolute inset-0 flex items-center justify-center text-white/80">
                            <Loader2 className="w-8 h-8 animate-spin" />
                          </div>
                        ) : !currentMedia ? (
                          <div className="absolute inset-0 flex flex-col items-center justify-center text-white/70 gap-2">
                            <ImageOff className="w-10 h-10" />
                            <span className="text-sm">{detailNotice || "Không có media để hiển thị."}</span>
                          </div>
                        ) : currentMediaFailed ? (
                          <div className="absolute inset-0 flex flex-col items-center justify-center text-white/70 gap-2">
                            <ImageOff className="w-10 h-10" />
                            <span className="text-sm">Không thể tải media.</span>
                          </div>
                        ) : (
                          <AnimatePresence mode="popLayout" initial={false}>
                            <motion.div
                              key={currentMedia.mediaId}
                              initial={{ opacity: 0, scale: 1.05 }}
                              animate={{ opacity: 1, scale: 1 }}
                              exit={{ opacity: 0, scale: 0.95 }}
                              transition={{ duration: 0.4, ease: "easeInOut" }}
                              className="absolute inset-0 w-full h-full"
                            >
                              {isVideo(currentMedia.mediaType) ? (
                                <video
                                  src={mediaSrc(currentMedia.url)}
                                  poster={mediaSrc(currentMedia.thumbnailUrl)}
                                  controls
                                  muted
                                  playsInline
                                  onError={() => markMediaFailed(currentMedia.mediaId)}
                                  className="w-full h-full object-contain bg-black"
                                />
                              ) : (
                                <img
                                  src={mediaSrc(currentMedia.url)}
                                  alt={currentMedia.altText || detail?.galleryItem.title || "FPTU"}
                                  onClick={() => {
                                    setIsLightboxOpen(true);
                                    setZoomScale(1);
                                  }}
                                  onError={() => markMediaFailed(currentMedia.mediaId)}
                                  className="w-full h-full object-cover cursor-zoom-in"
                                />
                              )}
                            </motion.div>
                          </AnimatePresence>
                        )}

                        {/* Zoom hint (image only) */}
                        {currentMedia && !isVideo(currentMedia.mediaType) && (
                          <div
                            className={`absolute inset-0 bg-gradient-to-t from-black/70 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500 flex items-end justify-center pointer-events-none ${
                              media.length > 1 ? "pb-16" : "pb-8"
                            }`}
                          >
                            <span className="flex items-center gap-1.5 text-white text-xs font-medium px-3.5 py-1.5 rounded-full bg-white/20 backdrop-blur-md border border-white/40 shadow-lg">
                              <ZoomIn className="w-4 h-4" /> Phóng to
                            </span>
                          </div>
                        )}

                        {/* Media prev/next (within this gallery item) */}
                        {media.length > 1 && (
                          <>
                            <button
                              onClick={() => stepMedia(-1)}
                              className="absolute left-4 top-1/2 -translate-y-1/2 p-2 bg-white/10 hover:bg-white/30 border border-white/20 text-white rounded-full backdrop-blur-md transition-all z-10 hover:scale-110 shadow-lg"
                            >
                              <ChevronLeft className="w-6 h-6" />
                            </button>
                            <button
                              onClick={() => stepMedia(1)}
                              className="absolute right-4 top-1/2 -translate-y-1/2 p-2 bg-white/10 hover:bg-white/30 border border-white/20 text-white rounded-full backdrop-blur-md transition-all z-10 hover:scale-110 shadow-lg"
                            >
                              <ChevronRight className="w-6 h-6" />
                            </button>
                          </>
                        )}
                      </div>

                      {/* Media dots */}
                      {media.length > 1 && (
                        <div className="absolute bottom-6 left-1/2 -translate-x-1/2 flex items-center gap-3 z-20 px-4 py-2 bg-black/40 backdrop-blur-md rounded-full border border-white/20">
                          {media.map((m, idx) => (
                            <button
                              key={m.mediaId}
                              onClick={() => setCurrentMediaIndex(idx)}
                              className={`w-2.5 h-2.5 rounded-full transition-all duration-300 hover:scale-125 ${
                                currentMediaIndex === idx ? "bg-white shadow-[0_0_12px_rgba(255,255,255,1)] w-6" : "bg-white/50 hover:bg-white/80"
                              }`}
                            />
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              ) : (
                /* ====== Tier 1: LOCATION GRID (album) ====== */
                <div className="flex flex-col gap-5 pb-8 px-1 sm:px-2">
                  {/* Premium location header */}
                  <div className="relative rounded-[28px] p-6 sm:p-8 border border-white/15 bg-gradient-to-br from-white/[0.12] to-white/[0.04] backdrop-blur-xl shadow-[0_10px_40px_rgba(0,0,0,0.25)]">
                    <button
                      onClick={closeOverlay}
                      title="Đóng"
                      className="absolute top-4 right-4 w-9 h-9 flex items-center justify-center rounded-full bg-white/20 hover:bg-white/40 text-white border border-white/30 transition-all hover:scale-110 group z-10"
                    >
                      <X className="w-4 h-4 group-hover:rotate-90 transition-transform duration-300" />
                    </button>
                    <div className="inline-flex items-center gap-2 px-4 py-2 rounded-full bg-fpt-orange/90 text-white font-extrabold text-[10px] sm:text-xs tracking-[0.08em] uppercase border border-white/30 backdrop-blur-md shadow-[0_0_15px_rgba(243,112,33,0.4)] max-w-[calc(100%-3rem)] overflow-hidden">
                      <span className="truncate">{breadcrumbArea}</span>
                      <ChevronRight className="w-3.5 h-3.5 shrink-0 opacity-90" />
                      <span className="truncate">{breadcrumbLocation}</span>
                    </div>
                    <h2 className="mt-4 text-3xl sm:text-4xl font-black text-white leading-[1.1] tracking-tight drop-shadow">
                      {breadcrumbLocation || "Khu vực"}
                    </h2>
                    <p className="mt-2.5 text-white/70 text-sm sm:text-base">
                      Khám phá những hình ảnh và video nổi bật tại khu vực này.
                    </p>
                    {!isGridLoading && !gridError && gridItems.length > 0 && (
                      <p className="mt-3.5 text-white/80 text-sm font-semibold">{gridStats}</p>
                    )}
                  </div>

                  {/* Location chips — quick-switch between locations of the same area */}
                  {siblingLocations.length > 1 && (
                    <div className="flex flex-wrap gap-2.5">
                      {siblingLocations.map((loc) => {
                        const active = loc.locationId === activeLocationId;
                        return (
                          <button
                            key={loc.locationId}
                            onClick={() => openLocation(loc.locationId)}
                            className={`px-4 py-2 rounded-full text-sm font-semibold border transition-all duration-200 ${
                              active
                                ? "text-white bg-fpt-orange border-transparent shadow-[0_10px_24px_rgba(243,112,33,0.26)]"
                                : "text-white/80 bg-white/[0.08] border-white/15 hover:text-white hover:bg-fpt-orange/80 hover:border-transparent"
                            }`}
                          >
                            {loc.locationName}
                          </button>
                        );
                      })}
                    </div>
                  )}

                  {/* Grid body — flows in the page-scroll container so the bottom row never gets cropped */}
                  {isGridLoading ? (
                    <div className="min-h-[280px] flex items-center justify-center text-white/80">
                      <Loader2 className="w-8 h-8 animate-spin" />
                    </div>
                  ) : gridError ? (
                    <div className="min-h-[280px] flex flex-col items-center justify-center text-white/70 gap-2">
                      <ImageOff className="w-10 h-10" />
                      <span className="text-sm">{gridError}</span>
                    </div>
                  ) : gridItems.length === 0 ? (
                    <div className="min-h-[280px] flex flex-col items-center justify-center text-white/70 gap-2">
                      <ImageOff className="w-10 h-10" />
                      <span className="text-sm">Vị trí này hiện chưa có nội dung Gallery công khai.</span>
                    </div>
                  ) : (
                    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4 sm:gap-5">
                      {gridItems.map((item) => (
                        <GridCard key={item.galleryItemId} item={item} onOpen={() => openItem(item.galleryItemId)} />
                      ))}
                    </div>
                  )}
                </div>
              )}
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Lightbox (detail media only) ── */}
      <AnimatePresence>
        {isLightboxOpen && currentMedia && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[100] flex items-center justify-center bg-black/95 backdrop-blur-md overflow-hidden"
            onClick={() => setIsLightboxOpen(false)}
            onWheel={(e) => {
              if (isVideo(currentMedia.mediaType)) return;
              setZoomScale((prev) =>
                e.deltaY < 0 ? Math.min(prev + 0.1, 4) : Math.max(prev - 0.1, 0.5),
              );
            }}
          >
            <button
              onClick={(e) => {
                e.stopPropagation();
                setIsLightboxOpen(false);
              }}
              className="absolute top-6 right-6 z-[110] p-3 bg-black/50 hover:bg-white/20 text-white rounded-full backdrop-blur-md transition-colors"
            >
              <X className="w-6 h-6" />
            </button>

            {media.length > 1 && (
              <>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    stepMedia(-1);
                    setZoomScale(1);
                  }}
                  className="absolute left-6 top-1/2 -translate-y-1/2 z-[110] p-3 bg-black/50 hover:bg-white/20 text-white rounded-full backdrop-blur-md transition-colors"
                >
                  <ChevronLeft className="w-7 h-7" />
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    stepMedia(1);
                    setZoomScale(1);
                  }}
                  className="absolute right-6 top-1/2 -translate-y-1/2 z-[110] p-3 bg-black/50 hover:bg-white/20 text-white rounded-full backdrop-blur-md transition-colors"
                >
                  <ChevronRight className="w-7 h-7" />
                </button>
              </>
            )}

            <div
              className="relative w-full h-full flex items-center justify-center overflow-hidden"
              onClick={(e) => e.stopPropagation()}
            >
              {isVideo(currentMedia.mediaType) ? (
                <video
                  src={mediaSrc(currentMedia.url)}
                  poster={mediaSrc(currentMedia.thumbnailUrl)}
                  controls
                  autoPlay
                  muted
                  playsInline
                  className="max-w-[90vw] max-h-[90vh] object-contain"
                />
              ) : (
                <motion.img
                  drag
                  dragConstraints={{ top: -500, bottom: 500, left: -500, right: 500 }}
                  dragElastic={0.2}
                  whileTap={{ cursor: "grabbing" }}
                  src={mediaSrc(currentMedia.url)}
                  alt={currentMedia.altText || "FPTU"}
                  animate={{ scale: zoomScale }}
                  transition={{ type: "spring", stiffness: 300, damping: 30 }}
                  className="object-contain cursor-grab"
                  style={{ maxWidth: "90vw", maxHeight: "90vh" }}
                  draggable={false}
                />
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
