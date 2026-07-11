/**
 * Component LazyGlobeShowcase
 * Bọc GlobeShowcase (react-globe.gl + three.js — nặng) bằng React.lazy + IntersectionObserver,
 * chỉ tải chunk và mount quả cầu khi section thực sự cuộn tới gần viewport, để không làm nặng
 * thêm lần tải trang đầu tiên của Homepage.
 */

import React, { Suspense, lazy, useEffect, useRef, useState } from 'react';
import { Globe as GlobeIcon } from 'lucide-react';

const GlobeShowcase = lazy(() => import('./GlobeShowcase'));

function GlobeSkeleton() {
  return (
    <div className="w-full h-full flex items-center justify-center">
      <div className="relative w-[70%] aspect-square max-w-[500px] rounded-full bg-gradient-to-br from-slate-100 to-blue-50 animate-pulse flex items-center justify-center">
        <GlobeIcon className="w-16 h-16 text-slate-300" />
      </div>
    </div>
  );
}

class GlobeErrorBoundary extends React.Component<{children: React.ReactNode}, {hasError: boolean}> {
  constructor(props: {children: React.ReactNode}) {
    super(props);
    this.state = { hasError: false };
  }
  static getDerivedStateFromError() {
    return { hasError: true };
  }
  componentDidCatch(error: Error) {
    if (import.meta.env.DEV) {
      console.warn("[GlobeErrorBoundary] Globe disabled due to WebGL error:", error.message);
    }
  }
  render() {
    if (this.state.hasError) return <GlobeSkeleton />;
    return this.props.children;
  }
}

export function LazyGlobeShowcase() {
  const containerRef = useRef<HTMLDivElement>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (shouldLoad || !containerRef.current) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          setShouldLoad(true);
          observer.disconnect();
        }
      },
      { rootMargin: '200px' },
    );
    observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, [shouldLoad]);

  return (
    <div ref={containerRef} className="w-full h-full">
      {shouldLoad ? (
        <GlobeErrorBoundary>
          <Suspense fallback={<GlobeSkeleton />}>
            <GlobeShowcase />
          </Suspense>
        </GlobeErrorBoundary>
      ) : (
        <GlobeSkeleton />
      )}
    </div>
  );
}
