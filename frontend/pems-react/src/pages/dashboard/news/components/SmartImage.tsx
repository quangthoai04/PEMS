import React from 'react';
import { useAuthenticatedImage } from '../../../../shared/hooks/useAuthenticatedImage';

/**
 * Renders an image whose src may be either a local blob:/data: URL (freshly picked, or from
 * URL.createObjectURL — safe to render directly) or a backend `/api/files/{id}/content` path
 * (an already-saved file, e.g. loaded from a saved post or picked from the đoàn's photo folder) —
 * the latter requires the Authorization header, which a plain <img src> cannot send, so it's
 * fetched through the authenticated-image hook instead.
 */
export function SmartImage({ src, alt, className }: { src: string; alt: string; className?: string }) {
  const isLocal = src.startsWith('blob:') || src.startsWith('data:');
  const authSrc = useAuthenticatedImage(isLocal ? null : src);
  const resolved = isLocal ? src : authSrc;
  if (!resolved) return <div className={`bg-gray-100 animate-pulse ${className ?? ''}`} />;
  return <img src={resolved} alt={alt} className={className} />;
}
