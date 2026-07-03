import React from 'react';
import { Star } from 'lucide-react';

interface Props {
  value: number;                 // 0 = chưa chấm
  onChange?: (v: number) => void;
  readOnly?: boolean;
  /** sm cho row desktop, md cho mobile/touch */
  size?: 'sm' | 'md';
}

/** Rating sao 1–5 dạng compact, đủ lớn để bấm trên mobile. */
export function CompactStarRating({ value, onChange, readOnly = false, size = 'sm' }: Props) {
  const px = size === 'sm' ? 'w-4.5 h-4.5 w-[18px] h-[18px]' : 'w-6 h-6';
  return (
    <div className="inline-flex items-center gap-0.5" role="radiogroup" aria-label="Đánh giá sao">
      {[1, 2, 3, 4, 5].map((star) => {
        const filled = star <= value;
        return (
          <button
            key={star}
            type="button"
            role="radio"
            aria-checked={star === value}
            aria-label={`${star} sao`}
            disabled={readOnly}
            onClick={() => onChange?.(star)}
            className={`p-0.5 outline-none ${readOnly ? 'cursor-default' : 'cursor-pointer hover:scale-110 transition-transform'}`}
          >
            <Star
              className={`${px} ${filled ? 'fill-amber-400 text-amber-400' : 'fill-transparent text-slate-300'}`}
            />
          </button>
        );
      })}
    </div>
  );
}
