// Đây là component hiển thị quả địa cầu 3D (dùng mô phỏng hoặc trang chủ)
import React, { useEffect, useRef, useState, useMemo } from 'react';
import Globe from 'react-globe.gl';
import * as THREE from 'three';

// Import all logos
const logoModules = import.meta.glob('../assets/Logo/*.{png,jpg}', { eager: true });
const allLogos = Object.values(logoModules).map((module: any) => module.default);

const CITIES = [
  { lat: 21.0285, lng: 105.8542, name: 'Hanoi', size: 1.5 },
  { lat: 10.7626, lng: 106.6601, name: 'HCMC', size: 1.2 },
  { lat: 35.6762, lng: 139.6503, name: 'Tokyo', size: 0.8 },
  { lat: -33.8688, lng: 151.2093, name: 'Sydney', size: 0.8 },
  { lat: 40.7128, lng: -74.0060, name: 'New York', size: 0.9 },
  { lat: 51.5074, lng: -0.1278, name: 'London', size: 0.9 },
  { lat: 1.3521, lng: 103.8198, name: 'Singapore', size: 0.8 },
  { lat: 37.5665, lng: 126.9780, name: 'Seoul', size: 0.8 },
  { lat: 48.8566, lng: 2.3522, name: 'Paris', size: 0.6 },
];

// Locations for partner logos (random cities around the world)
const PARTNER_CITIES = [
  { lat: 39.9042, lng: 116.4074 }, // Beijing
  { lat: 19.0760, lng: 72.8777 },  // Mumbai
  { lat: 55.7558, lng: 37.6173 },  // Moscow
  { lat: -23.5505, lng: -46.6333 },// Sao Paulo
  { lat: 43.6532, lng: -79.3832 }, // Toronto
  { lat: 25.2048, lng: 55.2708 },  // Dubai
  { lat: -37.8136, lng: 144.9631 },// Melbourne
  { lat: 52.5200, lng: 13.4050 },  // Berlin
  { lat: 30.0444, lng: 31.2357 },  // Cairo
  { lat: 34.0522, lng: -118.2437 } // Los Angeles
];

const ARCS: any[] = [];
const RINGS: any[] = [];

// Create simple connections originating from Vietnam
CITIES.forEach((city) => {
  if (city.name !== 'Hanoi' && city.name !== 'HCMC') {
    // Only connect some cities to reduce clutter
    if (['Tokyo', 'Sydney', 'London', 'Singapore', 'New York', 'Paris'].includes(city.name)) {
      const isNorth = Math.random() > 0.4;
      ARCS.push({
        startLat: isNorth ? 21.0285 : 10.7626,
        startLng: isNorth ? 105.8542 : 106.6601,
        endLat: city.lat,
        endLng: city.lng,
        color: '#f37021'
      });
    }
  }
  
  // Add radar ping rings to endpoints
  RINGS.push({
    lat: city.lat,
    lng: city.lng,
    maxR: city.size * 2.0,
    propagationSpeed: 1.5,
    repeatPeriod: 1000 + Math.random() * 2000
  });
});

export default function GlobeComponent() {
  const globeRef = useRef<any>(null);
  const [dimensions, setDimensions] = useState({ width: 0, height: 0 });
  const containerRef = useRef<HTMLDivElement>(null);
  const [countries, setCountries] = useState({ features: [] });
  // Store clicked states
  const [clickedPartners, setClickedPartners] = useState<Record<number, boolean>>({});

  // Memoize random partners to keep them stable
  const partnerData = useMemo(() => {
    // Pick 10 random logos
    const shuffledLogos = [...allLogos].sort(() => 0.5 - Math.random());
    const selectedLogos = shuffledLogos.slice(0, 10);
    
    return PARTNER_CITIES.map((city, index) => ({
      ...city,
      id: index,
      logo: selectedLogos[index % selectedLogos.length]
    }));
  }, []);

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
      // Set initial camera position looking at Southeast Asia
      // Increased altitude slightly to completely avoid clipping edges while keeping it large
      globeRef.current.pointOfView({ lat: 20, lng: 110, altitude: 2.4 }, 0);
      
      // Auto-rotate settings
      const controls = globeRef.current.controls();
      if (controls) {
        controls.autoRotate = true;
        controls.autoRotateSpeed = 0.8;
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
          
          // Custom soft-blue sphere for the globe base and glowing line
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
              // Canvas circle takes up 490/512 of the sprite. Globe diameter is ~200.
              // So sprite scale should be 200 * (512/490) approx = 208.9
              sprite.scale.set(209, 209, 1);
              // Put it very slightly behind the globe so the globe polygons render properly on its face
              sprite.position.z = -1;
              return sprite;
            }
          }}
          
          // Hexagon polygons for countries
          hexPolygonsData={countries.features}
          hexPolygonResolution={3}
          hexPolygonMargin={0.7}
          hexPolygonAltitude={0.001}
          hexPolygonColor={() => '#1e293b'} // slate-800 for faded black dots
          
          showAtmosphere={true}
          atmosphereColor="#dbeafe" // light blue-white glow
          atmosphereAltitude={0.15}

          // Location Points
          pointsData={CITIES}
          pointLat="lat"
          pointLng="lng"
          pointColor={() => '#f37021'} // strictly orange
          pointAltitude={0.02}
          pointRadius="size"
          pointsMerge={false}
          
          // Radar Rings
          ringsData={RINGS}
          ringColor={() => '#f37021'} // strictly orange
          ringMaxRadius="maxR"
          ringPropagationSpeed="propagationSpeed"
          ringRepeatPeriod="repeatPeriod"
          
          // Html Elements for Partner Logos
          htmlElementsData={partnerData}
          htmlElement={(d: any) => {
            const el = document.createElement('div');
            el.style.cursor = 'pointer';
            el.style.pointerEvents = 'auto';
            el.style.display = 'flex';
            el.style.alignItems = 'center';
            el.style.justifyContent = 'center';
            
            let isClicked = false;
            
            const renderContent = () => {
              if (isClicked) {
                el.innerHTML = `<img src="${d.logo}" style="width: 80px; height: 80px; object-fit: contain; background: white; border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); padding: 6px; pointer-events: none; transition: all 0.3s ease; transform: translate(-50%, -50%);" />`;
              } else {
                el.innerHTML = `<div style="width: 14px; height: 14px; background-color: #f37021; border-radius: 50%; box-shadow: 0 0 10px 2px rgba(243,112,33,0.8); border: 2px solid white; pointer-events: none; transition: all 0.3s ease; transform: translate(-50%, -50%);"></div>`;
              }
            };
            
            renderContent();
            
            el.onclick = (e) => {
              e.stopPropagation();
              isClicked = !isClicked;
              renderContent();
            };
            
            return el;
          }}
          htmlLat="lat"
          htmlLng="lng"

          // Network Lines
          arcsData={ARCS}
          arcStartLat="startLat"
          arcStartLng="startLng"
          arcEndLat="endLat"
          arcEndLng="endLng"
          arcColor="color"
          arcDashLength={0.4}
          arcDashGap={0.2}
          arcDashAnimateTime={2000}
          arcAltitudeAutoScale={0.4}
          arcStroke={0.8}
        />
        </div>
      )}
    </div>
  );
}
