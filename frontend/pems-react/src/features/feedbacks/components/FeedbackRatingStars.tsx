import React from 'react';
import { Star } from 'lucide-react';

interface Props {
  rating: number;
  size?: 'sm' | 'md';
}

export function FeedbackRatingStars({ rating, size = 'sm' }: Props) {
  const starClass = size === 'md' ? 'w-5 h-5' : 'w-4 h-4';
  return (
    <>
      {Array.from({ length: 5 }).map((_, i) => (
        <Star
          key={i}
          className={`${starClass} ${i < rating ? 'fill-yellow-400 text-yellow-400' : 'fill-slate-100 text-slate-200'}`}
        />
      ))}
    </>
  );
}
