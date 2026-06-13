/**
 * Component StatsSection
 * Khu vực hiển thị số liệu thống kê nhanh ở trang chủ (số lượng sinh viên, trường, v.v.).
 */

// Đây là component hiển thị các số liệu thống kê (phần Stats)
import React, { useMemo } from 'react';

const stats = [
  { id: 1, value: '250+', label: 'Đối tác quốc tế', desc: 'Các trường Đại học và tổ chức toàn cầu', color: 'text-fpt-orange', borderColor: 'border-fpt-orange' },
  { id: 2, value: '40+', label: 'Quốc gia', desc: 'Mạng lưới kết nối trên thế giới', color: 'text-fpt-orange', borderColor: 'border-fpt-orange' },
  { id: 3, value: '3,000+', label: 'Sinh viên Outbound', desc: 'Đã tham gia học kỳ nước ngoài', color: 'text-fpt-navy', borderColor: 'border-fpt-navy' },
  { id: 4, value: '1,500+', label: 'Sinh viên Inbound', desc: 'Từ các nước đến trao đổi', color: 'text-fpt-navy', borderColor: 'border-fpt-navy' },
  { id: 5, value: '5,000+', label: 'Khách quốc tế', desc: 'Đã ghé thăm và làm việc tại trường', color: 'text-fpt-navy', borderColor: 'border-fpt-navy' },
];

export function StatsSection() {
  const { points, lines } = useMemo(() => {
    const pts = [];
    // Generate evenly distributed points using golden angle
    for(let i = 0; i < 45; i++) {
      const angle = (i * 137.5) * (Math.PI / 180);
      const r = Math.sqrt(i) * 26;
      if (r <= 170) {
        pts.push({
          id: i,
          x: 200 + r * Math.cos(angle),
          y: 200 + r * Math.sin(angle),
          size: Math.random() * 2 + 1.5,
          isMajor: i % 4 === 0
        });
      }
    }
    
    // Connect close points with lines
    const lns = [];
    for(let i=0; i<pts.length; i++) {
      for(let j=i+1; j<pts.length; j++) {
        const dx = pts[i].x - pts[j].x;
        const dy = pts[i].y - pts[j].y;
        if(Math.sqrt(dx*dx + dy*dy) < 65) {
          lns.push({id: `${i}-${j}`, p1: pts[i], p2: pts[j]});
        }
      }
    }
    return { points: pts, lines: lns };
  }, []);

  return (
    <section className="py-24 bg-fpt-navy overflow-hidden relative">
      <div className="absolute inset-0 opacity-[0.05] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#fff 2px, transparent 2px)', backgroundSize: '40px 40px' }}></div>
      <div className="absolute left-0 top-0 w-96 h-96 bg-fpt-orange/10 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute right-0 bottom-0 w-96 h-96 bg-blue-400/10 rounded-full blur-[100px] pointer-events-none"></div>
      
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
        <div className="flex flex-col lg:flex-row items-center gap-16">
          
          {/* Left Side: Stats Grid */}
          <div className="w-full lg:w-1/2">
            <h2 className="text-3xl lg:text-4xl font-bold text-white mb-3">Những con số nổi bật</h2>
            <div className="w-16 h-1 bg-fpt-orange mt-4 mb-8 rounded-full"></div>
            <p className="text-blue-200 mb-10 text-lg">Mạng lưới hợp tác quốc tế lớn mạnh</p>
            
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6 relative">
              {stats.map((stat, idx) => (
                <div 
                  key={stat.id} 
                  className={`bg-white/5 backdrop-blur-sm rounded-2xl p-8 border border-white/10 shadow-xl ${idx === 4 ? 'sm:col-span-2 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4' : ''}`}
                >
                  <div className="relative z-10">
                    <h3 className={`text-5xl lg:text-5xl font-bold ${stat.color === 'text-fpt-navy' ? 'text-white' : stat.color} mb-2 tracking-tighter`}>{stat.value}</h3>
                    <p className="font-bold text-white text-lg opacity-90">{stat.label}</p>
                    <p className="text-sm text-blue-200/80 mt-2">{stat.desc}</p>
                  </div>
                  {idx === 4 && (
                    <div className="hidden sm:block opacity-20 text-white relative z-0">
                       <svg width="80" height="80" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M12 22C17.5228 22 22 17.5228 22 12C22 6.47715 17.5228 2 12 2C6.47715 2 2 6.47715 2 12C2 17.5228 6.47715 22 12 22Z" stroke="currentColor" strokeWidth="2"/>
                        <path d="M2.5 12H21.5" stroke="currentColor" strokeWidth="2"/>
                        <path d="M12 2C14.5013 4.73835 15.9228 8.29203 16 12C15.9228 15.708 14.5013 19.2616 12 22C9.49872 19.2616 8.07725 15.708 8 12C8.07725 8.29203 9.49872 4.73835 12 2Z" stroke="currentColor" strokeWidth="2"/>
                      </svg>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>

          {/* Right Side: Graphic/Image */}
          <div className="w-full lg:w-1/2 relative lg:h-[700px] h-64 sm:h-96 flex items-center justify-center">
            {/* SVG Wireframe Globe */}
            <div className="absolute w-[280px] h-[280px] sm:w-[450px] sm:h-[450px] lg:w-[700px] lg:h-[700px] right-[-20px] sm:right-[-50px] lg:right-[-100px] translate-y-6 sm:translate-y-12 lg:translate-y-20 pointer-events-none flex items-center justify-center origin-center">
              <svg viewBox="0 0 400 400" className="w-full h-full text-fpt-orange animate-[spin_80s_linear_infinite]" style={{ filter: "drop-shadow(0 0 8px rgba(243,112,33,0.3))" }}>
                 {/* Lat/Long Grid */}
                 <g stroke="currentColor" strokeWidth="0.5" fill="none" strokeOpacity="0.4">
                   {Array.from({ length: 12 }).map((_, i) => {
                     const ry = 180 * Math.cos((i * 15) * (Math.PI / 180));
                     return <ellipse key={`long-${i}`} cx="200" cy="200" rx="180" ry={Math.abs(ry)} />;
                   })}
                   {Array.from({ length: 12 }).map((_, i) => {
                     const rx = 180 * Math.cos((i * 15) * (Math.PI / 180));
                     return <ellipse key={`lat-${i}`} cx="200" cy="200" rx="180" ry={Math.abs(rx)} transform="rotate(90 200 200)" />;
                   })}
                   <circle cx="200" cy="200" r="180" strokeWidth="1" strokeOpacity="0.6" />
                 </g>
                 
                 {/* Connection Lines */}
                 <g stroke="currentColor" strokeWidth="0.5" strokeOpacity="0.5">
                   {lines.map((l) => (
                     <line key={l.id} x1={l.p1.x} y1={l.p1.y} x2={l.p2.x} y2={l.p2.y} />
                   ))}
                 </g>
                 
                 {/* Nodes */}
                 <g fill="currentColor">
                   {points.map((p) => (
                     <g key={`d-${p.id}`}>
                       <circle cx={p.x} cy={p.y} r={p.size} opacity="0.9" />
                       {p.isMajor && (
                         <circle cx={p.x} cy={p.y} r={p.size * 2.5} fillOpacity="0.3" />
                       )}
                     </g>
                   ))}
                 </g>
              </svg>
            </div>
          </div>

        </div>
      </div>
    </section>
  );
}
