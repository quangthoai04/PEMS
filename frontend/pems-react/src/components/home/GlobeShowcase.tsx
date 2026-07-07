/**
 * Component GlobeShowcase
 * Quả cầu 3D cho Homepage — ghim đúng các quốc gia đối tác THẬT (GET /public/partners/countries),
 * không dùng danh sách cứng. Click ghim → /partners?country=<value> (áp dụng lọc thật ở đó).
 * Nặng (react-globe.gl + three.js) nên component này luôn được cha lazy-load qua React.lazy.
 */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Globe from 'react-globe.gl';
import * as THREE from 'three';
import { publicPartnersApi } from '../../features/public-partners/api/publicPartnersApi';
import { resolveCountryCoordinates } from '../../shared/constants/countryCoordinates';
import countriesGeoJson from '../../assets/ne_110m_admin_0_countries.geojson?url';

const VIETNAM_HUB = { lat: 21.0285, lng: 105.8542, name: 'FPT University' };

// Dữ liệu ranh giới quốc gia (cho lớp chấm hex-polygon trang trí trên quả cầu) được bundle
// làm asset tĩnh của dự án thay vì gọi CDN GitHub — CDN đó từng bị rate-limit (429) khi
// test liên tục, khiến quả cầu mất hẳn lớp chấm bi. File không đổi theo thời gian nên
// bundle sẵn là lựa chọn ổn định nhất, không phụ thuộc dịch vụ ngoài.
async function loadCountriesGeoJson(): Promise<{ features: unknown[] }> {
  const res = await fetch(countriesGeoJson);
  if (!res.ok) throw new Error(`GeoJSON fetch failed: ${res.status}`);
  return res.json();
}

// 3 màu luân phiên cho hiệu ứng dây nối chạy qua các điểm ghim.
const ARC_COLORS = ['#16a34a', '#004c91', '#f37021']; // xanh lá, xanh dương, cam

interface GlobePoint {
  lat: number;
  lng: number;
  name: string;
  countryValue: string | null;
  isHub: boolean;
  size: number;
  color: string;
  count?: number;
}

interface GlobeArc {
  startLat: number;
  startLng: number;
  endLat: number;
  endLng: number;
  color: string;
}

/** Nối hub Việt Nam tới từng quốc gia đối tác, và nối các quốc gia đối tác liền kề với nhau
 *  thành một vòng — tạo cảm giác "mạng lưới" thay vì chỉ toả tia từ 1 điểm. */
function buildArcs(points: GlobePoint[]): GlobeArc[] {
  const hub = points.find((p) => p.isHub);
  const partners = points.filter((p) => !p.isHub);
  if (!hub || partners.length === 0) return [];

  const arcs: GlobeArc[] = partners.map((p, idx) => ({
    startLat: hub.lat,
    startLng: hub.lng,
    endLat: p.lat,
    endLng: p.lng,
    color: ARC_COLORS[idx % ARC_COLORS.length],
  }));

  // Nối vòng giữa các quốc gia đối tác liền kề (bỏ qua khi chỉ có 1 quốc gia).
  if (partners.length > 1) {
    partners.forEach((p, idx) => {
      const next = partners[(idx + 1) % partners.length];
      arcs.push({
        startLat: p.lat,
        startLng: p.lng,
        endLat: next.lat,
        endLng: next.lng,
        color: ARC_COLORS[(idx + 1) % ARC_COLORS.length],
      });
    });
  }

  return arcs;
}

interface GlobeRing {
  lat: number;
  lng: number;
  maxR: number;
  propagationSpeed: number;
  repeatPeriod: number;
  color: string;
}

/** Vòng sóng lan toả tại mỗi điểm ghim (giống hiệu ứng ở trang Đối tác) — hub Việt Nam
 *  pulse mạnh/nhanh hơn, các quốc gia đối tác pulse nhẹ nhàng hơn bằng màu cam. */
function buildRings(points: GlobePoint[]): GlobeRing[] {
  return points.map((p) =>
    p.isHub
      ? { lat: p.lat, lng: p.lng, maxR: 5, propagationSpeed: 1.4, repeatPeriod: 1400, color: '#ff3c00' }
      : { lat: p.lat, lng: p.lng, maxR: 3.2, propagationSpeed: 1.0, repeatPeriod: 1800, color: '#f37021' },
  );
}

export default function GlobeShowcase() {
  const navigate = useNavigate();
  const globeRef = useRef<any>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [dimensions, setDimensions] = useState({ width: 0, height: 0 });
  const [countries, setCountries] = useState({ features: [] });
  const [points, setPoints] = useState<GlobePoint[]>([{
    lat: VIETNAM_HUB.lat,
    lng: VIETNAM_HUB.lng,
    name: VIETNAM_HUB.name,
    countryValue: null,
    isHub: true,
    size: 1.6,
    color: '#ff5a00',
  }]);

  const arcs = useMemo(() => buildArcs(points), [points]);
  const rings = useMemo(() => buildRings(points), [points]);

  useEffect(() => {
    let cancelled = false;
    publicPartnersApi.getPublicPartnerCountries()
      .then((realCountries) => {
        if (cancelled) return;
        const partnerPoints: GlobePoint[] = realCountries
          .map((c): GlobePoint | null => {
            const coord = resolveCountryCoordinates(c.label);
            if (!coord) {
              if (import.meta.env.DEV) {
                // eslint-disable-next-line no-console
                console.warn(`[GlobeShowcase] Không có toạ độ cho quốc gia "${c.label}" — bỏ qua ghim.`);
              }
              return null;
            }
            return {
              lat: coord.lat,
              lng: coord.lng,
              name: c.label,
              countryValue: c.value,
              isHub: false,
              size: 1.1,
              color: '#f37021',
              count: c.count,
            };
          })
          .filter((p): p is GlobePoint => p !== null);

        setPoints((prev) => [prev[0], ...partnerPoints]);
      })
      .catch(() => { /* Không lấy được danh sách quốc gia — vẫn hiển thị quả cầu với hub VN */ });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;
    loadCountriesGeoJson()
      .then((data) => { if (!cancelled) setCountries(data as { features: never[] }); })
      .catch(() => { /* Hex polygon layer là trang trí — thiếu cũng không sao */ });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const updateSize = () => {
      if (containerRef.current) {
        const screenWidth = window.innerWidth;
        const minBound = screenWidth < 768 ? 240 : screenWidth < 1024 ? 400 : 900;
        const size = Math.max(containerRef.current.offsetWidth, containerRef.current.offsetHeight, minBound);
        setDimensions({ width: size, height: size });
      }
    };
    setTimeout(updateSize, 100);
    window.addEventListener('resize', updateSize);
    return () => window.removeEventListener('resize', updateSize);
  }, []);

  useEffect(() => {
    if (globeRef.current) {
      globeRef.current.pointOfView({ lat: 15, lng: 100, altitude: 2.3 }, 0);
      const controls = globeRef.current.controls();
      if (controls) {
        controls.autoRotate = true;
        controls.autoRotateSpeed = 0.5;
        controls.enableZoom = false;
      }
    }
  }, [dimensions.width, countries]);

  return (
    <div ref={containerRef} className="w-full h-full relative cursor-grab active:cursor-grabbing">
      {dimensions.width > 0 && (
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2" style={{ width: dimensions.width, height: dimensions.height }}>
          <Globe
            ref={globeRef}
            rendererConfig={{ antialias: true, alpha: true }}
            width={dimensions.width}
            height={dimensions.height}
            backgroundColor="rgba(0,0,0,0)"
            showGlobe={false}
            animateIn={false}
            customLayerData={[{ type: 'globe' }, { type: 'halo' }]}
            customThreeObject={(d: any) => {
              if (d.type === 'globe') {
                return new THREE.Mesh(
                  new THREE.SphereGeometry(99.5, 128, 128),
                  new THREE.MeshBasicMaterial({ color: '#edf0f9' }),
                );
              }
              const canvas = document.createElement('canvas');
              canvas.width = 512;
              canvas.height = 512;
              const ctx = canvas.getContext('2d');
              if (ctx) {
                ctx.beginPath();
                ctx.arc(256, 256, 245, 0, 2 * Math.PI, false);
                ctx.lineWidth = 6;
                ctx.strokeStyle = 'rgba(147, 197, 253, 0.4)';
                ctx.shadowColor = '#60a5fa';
                ctx.shadowBlur = 10;
                ctx.stroke();
                ctx.beginPath();
                ctx.arc(256, 256, 245, 0, 2 * Math.PI, false);
                ctx.lineWidth = 2;
                ctx.strokeStyle = '#93c5fd';
                ctx.stroke();
              }
              const texture = new THREE.CanvasTexture(canvas);
              const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthWrite: false });
              const sprite = new THREE.Sprite(material);
              sprite.scale.set(209, 209, 1);
              sprite.position.z = -1;
              return sprite;
            }}
            hexPolygonsData={countries.features}
            hexPolygonResolution={3}
            hexPolygonMargin={0.6}
            hexPolygonAltitude={0.001}
            hexPolygonColor={() => '#1e293b'}
            showAtmosphere={true}
            atmosphereColor="#dbeafe"
            atmosphereAltitude={0.15}
            arcsData={arcs}
            arcColor="color"
            arcAltitude={0.28}
            arcStroke={0.6}
            arcDashLength={0.4}
            arcDashGap={0.2}
            arcDashInitialGap={() => Math.random()}
            arcDashAnimateTime={4000}
            pointsData={points}
            pointLat="lat"
            pointLng="lng"
            pointColor="color"
            pointAltitude={0.002}
            pointRadius="size"
            pointResolution={24}
            pointLabel={(d: any) => `
              <div style="
                background: rgba(15, 23, 42, 0.95);
                border: 1px solid rgba(255, 255, 255, 0.1);
                padding: 6px 14px;
                border-radius: 8px;
                box-shadow: 0 10px 15px -3px rgba(15, 23, 42, 0.3), 0 4px 6px -4px rgba(15, 23, 42, 0.3);
                font-family: 'Inter', system-ui, sans-serif;
                font-size: 13px;
                font-weight: 600;
                color: #ffffff;
                pointer-events: none;
                display: flex;
                align-items: center;
                gap: 8px;
                border-left: 3px solid ${d.isHub ? '#ff5a00' : '#f37021'};
                white-space: nowrap;
              ">
                <span style="width: 5px; height: 5px; background-color: ${d.isHub ? '#ff5a00' : '#f37021'}; border-radius: 50%; box-shadow: 0 0 6px ${d.isHub ? '#ff5a00' : '#f37021'};"></span>
                <span>${d.name}${!d.isHub && d.count ? ` (${d.count})` : ''}</span>
              </div>
            `}
            onPointHover={(point: any) => {
              if (containerRef.current) {
                containerRef.current.style.cursor = point && !point.isHub ? 'pointer' : 'grab';
              }
            }}
            onPointClick={(point: any) => {
              if (point && !point.isHub && point.countryValue) {
                navigate(`/partners?country=${encodeURIComponent(point.countryValue)}`);
              }
            }}
            ringsData={rings}
            ringColor={(d: any) => d.color || '#f37021'}
            ringMaxRadius="maxR"
            ringPropagationSpeed="propagationSpeed"
            ringRepeatPeriod="repeatPeriod"
          />
        </div>
      )}
    </div>
  );
}
