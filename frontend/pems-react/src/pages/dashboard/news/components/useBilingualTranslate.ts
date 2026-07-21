import { useEffect, useRef, useState } from 'react';
import httpClient from '../../../../shared/api/httpClient';

export interface BilingualSectionInput {
  sectionOrder: number;
  sectionTitle: string;
  sectionBodyHtml: string;
}

export interface TranslatedSection {
  sectionOrder: number;
  sectionTitle: string;
  sectionBodyHtml: string;
}

interface DraftTranslateResponse {
  title: string;
  summary?: string | null;
  sections: TranslatedSection[];
}

interface AutoTranslateResponse {
  title: string;
  summary?: string | null;
  sections: TranslatedSection[];
}

/**
 * Debounced Vietnamese → English auto-translate for the bilingual News editor.
 *
 * - `newsId` present  → EditNews.tsx editing an existing post: calls the real
 *   POST /news/{newsId}/translations/auto-translate?save=false (TranslateNewsCommand preview).
 * - `newsId` absent   → CreateNews.tsx composing a brand-new post (no NewsId yet): calls
 *   POST /news/translate-draft (TranslateNewsDraftCommand), which never persists anything.
 *
 * Debounces on the serialized Vietnamese content, cancels/ignores stale in-flight responses via
 * a request-sequence counter, and never overwrites a field the user has manually edited in the
 * English column (tracked by the caller via `touched`, honored by `applyIfUntouched`).
 */
export function useBilingualTranslate(params: {
  enabled: boolean;
  newsId?: number | null;
  title: string;
  summary: string;
  sections: BilingualSectionInput[];
  onTranslated: (result: { title: string; summary: string; sections: TranslatedSection[] }) => void;
  debounceMs?: number;
}) {
  const { enabled, newsId, title, summary, sections, onTranslated, debounceMs = 700 } = params;
  const [translating, setTranslating] = useState(false);
  const seqRef = useRef(0);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const snapshot = JSON.stringify({ title, summary, sections });

  async function runTranslate(): Promise<boolean> {
    const mySeq = ++seqRef.current;
    setTranslating(true);
    try {
      const payload = {
        sourceLanguage: 'vi',
        targetLanguage: 'en',
        title,
        summary,
        sections: sections.map(s => ({
          sectionOrder: s.sectionOrder,
          sectionTitle: s.sectionTitle,
          sectionBodyHtml: s.sectionBodyHtml,
        })),
      };

      const { data } = newsId
        ? await httpClient.post<AutoTranslateResponse>(
            `/news/${newsId}/translations/auto-translate`,
            { sourceLanguage: 'vi', targetLanguage: 'en', save: false },
          )
        : await httpClient.post<DraftTranslateResponse>('/news/translate-draft', payload);

      // A newer call already started (or finished) — this response is stale, ignore it.
      if (mySeq !== seqRef.current) return false;

      onTranslated({
        title: data.title ?? '',
        summary: data.summary ?? '',
        sections: data.sections ?? [],
      });
      return true;
    } catch {
      return false;
    } finally {
      if (mySeq === seqRef.current) setTranslating(false);
    }
  }

  // Debounced auto-translate on Vietnamese content change.
  useEffect(() => {
    if (!enabled) return;
    if (!title.trim() && sections.every(s => !s.sectionTitle.trim() && !s.sectionBodyHtml.trim())) return;

    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => { void runTranslate(); }, debounceMs);
    return () => { if (timerRef.current) clearTimeout(timerRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, snapshot, debounceMs]);

  /** Manual "Dịch lại" — forces an immediate translate ignoring the debounce. */
  async function retranslateNow() {
    if (timerRef.current) clearTimeout(timerRef.current);
    return runTranslate();
  }

  return { translating, retranslateNow };
}
