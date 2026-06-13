/**
 * Component GlobeComponent
 * Hiển thị mô hình quả địa cầu 3D để trực quan hóa mạng lưới liên kết đối tác quốc tế.
 */

// Đây là component hiển thị quả địa cầu 3D với hệ thống ghim đối tác quốc tế thông minh
import React, { useEffect, useRef, useState } from 'react';
import Globe from 'react-globe.gl';
import * as THREE from 'three';

// Coordinates of actual partner countries from the dataset
const PARTNER_PINS = [
  { lat: -33.8688, lng: 151.2093, country: 'Úc', city: 'Sydney' },
  { lat: 35.6762, lng: 139.6503, country: 'Nhật Bản', city: 'Tokyo' },
  { lat: 13.7563, lng: 100.5018, country: 'Thái Lan', city: 'Bangkok' },
  { lat: 25.0330, lng: 121.5654, country: 'Đài Loan', city: 'Taipei' },
  { lat: 3.1390, lng: 101.6869, country: 'Malaysia', city: 'Kuala Lumpur' },
  { lat: 37.7749, lng: -122.4194, country: 'Mỹ', city: 'San Francisco' },
  { lat: 1.3521, lng: 103.8198, country: 'Singapore', city: 'Singapore' },
  { lat: 37.5665, lng: 126.9780, country: 'Hàn Quốc', city: 'Seoul' },
  { lat: 51.5074, lng: -0.1278, country: 'Anh', city: 'London' }
];

// FPT University Vietnam Hub points
const VIETNAM_BASES = [
  { lat: 21.0285, lng: 105.8542, name: 'FPTU Hà Nội', isHub: true },
  { lat: 10.7626, lng: 106.6601, name: 'FPTU TP. HCM', isHub: true }
];

// Merge all points into a single WebGL points array for absolute styling and positioning
const ALL_3D_POINTS = [
  ...VIETNAM_BASES.map(hub => ({
    lat: hub.lat,
    lng: hub.lng,
    name: hub.name,
    country: 'Việt Nam',
    city: hub.name,
    isHub: true,
    size: 1.6, // Larger point for hubs
    color: '#ff5a00'
  })),
  ...PARTNER_PINS.map(partner => ({
    lat: partner.lat,
    lng: partner.lng,
    name: partner.country,
    country: partner.country,
    city: partner.city,
    isHub: false,
    size: 1.1, // Larger point for partners so they are easily clickable
    color: '#f37021'
  }))
];

// Merge Vietnam hubs and partner pins into the rings data to make them ALL glow with elegant pulsing rings
const RINGS = [
  // Vietnam bases (stronger pulses)
  { lat: 21.0285, lng: 105.8542, maxR: 5, propagationSpeed: 1.4, repeatPeriod: 1400, color: '#ff3c00' },
  { lat: 10.7626, lng: 106.6601, maxR: 4.5, propagationSpeed: 1.4, repeatPeriod: 1700, color: '#ff3c00' },
  // Partner countries (subtle, beautiful glowing orange highlight rings)
  ...PARTNER_PINS.map(pin => ({
    lat: pin.lat,
    lng: pin.lng,
    maxR: 3.2,
    propagationSpeed: 1.0,
    repeatPeriod: 1800,
    color: '#f37021'
  }))
];

interface GlobeComponentProps {
  onSelectCountry?: (country: string) => void;
}

export default function GlobeComponent({ onSelectCountry }: GlobeComponentProps) {
  const globeRef = useRef<any>(null);
  const [dimensions, setDimensions] = useState({ width: 0, height: 0 });
  const containerRef = useRef<HTMLDivElement>(null);
  const [countries, setCountries] = useState({ features: [] });

  useEffect(() => {
    // Fetch countries data for hex polygons (modern tech style)
    fetch('https://raw.githubusercontent.com/vasturiano/react-globe.gl/master/example/datasets/ne_110m_admin_0_countries.geojson')
      .then(res => res.json())
      .then(setCountries)
      .catch(err => console.log('Error loading countries data', err));
  }, []);

  useEffect(() => {
    const updateSize = () => {
      if (containerRef.current) {
        const screenWidth = window.innerWidth;
        const minBound = screenWidth < 768 ? 320 : (screenWidth < 1024 ? 500 : 900);
        const size = Math.max(containerRef.current.offsetWidth, containerRef.current.offsetHeight, minBound);
        setDimensions({
          width: size,
          height: size
        });
      }
    };
    
    // Initial size
    setTimeout(updateSize, 100);
    
    window.addEventListener('resize', updateSize);
    return () => window.removeEventListener('resize', updateSize);
  }, []);

  useEffect(() => {
    if (globeRef.current) {
      // Set initial camera position looking at Southeast Asia & Vietnam
      globeRef.current.pointOfView({ lat: 18, lng: 112, altitude: 2.2 }, 0);
      
      // Auto-rotate settings
      const controls = globeRef.current.controls();
      if (controls) {
        controls.autoRotate = false;
        controls.enableZoom = false;
      }
    }
  }, [dimensions.width, countries]);

  return (
    <div ref={containerRef} className="w-full h-full relative z-10 cursor-grab active:cursor-grabbing">
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
            
            // Custom soft-blue sphere for the globe base and glowing outer line
            customLayerData={[{ type: 'globe' }, { type: 'halo' }]}
            customThreeObject={(d: any) => {
              if (d.type === 'globe') {
                return new THREE.Mesh(
                  new THREE.SphereGeometry(99.5, 128, 128),
                  new THREE.MeshBasicMaterial({ 
                    color: '#edf0f9',
                  })
                );
              } else if (d.type === 'halo') {
                const canvas = document.createElement('canvas');
                canvas.width = 512;
                canvas.height = 512;
                const context = canvas.getContext('2d');
                if (context) {
                  const centerX = 256;
                  const centerY = 256;
                  const radius = 245; // slightly under edge
                  
                  // outer glow
                  context.beginPath();
                  context.arc(centerX, centerY, radius, 0, 2 * Math.PI, false);
                  context.lineWidth = 6;
                  context.strokeStyle = 'rgba(147, 197, 253, 0.4)'; // blue-300 with opacity
                  context.shadowColor = '#60a5fa';
                  context.shadowBlur = 10;
                  context.stroke();
                  
                  // solid thin inner line
                  context.beginPath();
                  context.arc(centerX, centerY, radius, 0, 2 * Math.PI, false);
                  context.lineWidth = 2;
                  context.strokeStyle = '#93c5fd'; // blue-300
                  context.stroke();
                }
                const texture = new THREE.CanvasTexture(canvas);
                const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthWrite: false });
                const sprite = new THREE.Sprite(material);
                
                sprite.scale.set(209, 209, 1);
                sprite.position.z = -1;
                return sprite;
              }
            }}
            
            // Hexagon polygons for countries (creating a gorgeous high-tech matrix globe)
            hexPolygonsData={countries.features}
            hexPolygonResolution={3}
            hexPolygonMargin={0.7}
            hexPolygonAltitude={0.001}
            hexPolygonColor={() => '#1e293b'} // slate-800 for elegant black-faded land dots
            
            showAtmosphere={true}
            atmosphereColor="#dbeafe" // light blue-white glow
            atmosphereAltitude={0.15}

            // --- POINT LAYER (STRICTLY WEBGL NATIVE FOR 100% PERFECT POSITION SYNC WHILE ROTATING) ---
            pointsData={ALL_3D_POINTS}
            pointLat="lat"
            pointLng="lng"
            pointColor="color"
            pointAltitude={0.002} // Almost zero height so they appear as flat circular dots on the surface
            pointRadius="size"
            pointResolution={24}   // Smooth high quality round circles
            
            // Smooth tooltip styling for country/hub hover
            pointLabel={(d: any) => {
              const borderAccentColor = d.isHub ? '#ff5a00' : '#f37021';
              const labelName = d.isHub ? d.city : d.country;
              return `
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
                  border-left: 3px solid ${borderAccentColor};
                  white-space: nowrap;
                ">
                  <span style="width: 5px; height: 5px; background-color: ${borderAccentColor}; border-radius: 50%; box-shadow: 0 0 6px ${borderAccentColor};"></span>
                  <span>${labelName}</span>
                </div>
              `;
            }}
            
            // Interactive handlers
            onPointHover={(point: any) => {
              if (containerRef.current) {
                // Change cursor dynamically to pointer when hovering over clickable partner pins
                containerRef.current.style.cursor = point && !point.isHub ? 'pointer' : 'grab';
              }
            }}
            
            onPointClick={(point: any) => {
              // Click partner pin triggers filter in PartnersPage list view below
              if (point && !point.isHub && onSelectCountry) {
                onSelectCountry(point.country);
              }
            }}
            
            // Radar Rings (Beautiful glowing pulsing rings on all pins!)
            ringsData={RINGS}
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
