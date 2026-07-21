import React from 'react';

/**
 * Two-column (VI | EN) layout wrapper — left column always Vietnamese, right column English.
 * Stacks vertically on small screens. Used for the title/summary block and for each content
 * section's title/body block in both CreateNews.tsx and EditNews.tsx.
 */
export function BilingualColumns({
  left,
  right,
  showEnglish,
}: {
  left: React.ReactNode;
  right: React.ReactNode;
  showEnglish: boolean;
}) {
  if (!showEnglish) return <>{left}</>;

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
      <div>{left}</div>
      <div>{right}</div>
    </div>
  );
}

/** Inline language tag placed next to a field's own label (not on its own line) to keep
 * bilingual fields compact — e.g. `<FieldLabel>Tiêu đề tin tức <LanguageColumnLabel>VI</LanguageColumnLabel></FieldLabel>`. */
export function LanguageColumnLabel({ children }: { children: React.ReactNode }) {
  return (
    <span className="ml-1.5 align-middle text-[10px] font-bold uppercase tracking-wide text-gray-400 bg-gray-100 rounded px-1.5 py-0.5">
      {children}
    </span>
  );
}
