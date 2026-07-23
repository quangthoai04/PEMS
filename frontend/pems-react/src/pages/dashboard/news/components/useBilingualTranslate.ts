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
 * One-shot Vietnamese → English auto-translate for the bilingual News editor.
 *
 * - `newsId` present  → EditNews.tsx editing an existing post: calls the real
 *   POST /news/{newsId}/translations/auto-translate?save=false (TranslateNewsCommand preview).
 * - `newsId` absent   → CreateNews.tsx composing a brand-new post (no NewsId yet): calls
 *   POST /news/translate-draft (TranslateNewsDraftCommand), which never persists anything.
 *
 * Translates exactly once, the moment `enabled` flips from false → true (i.e. the admin just
 * clicked the "EN" toggle) — never on every keystroke/debounce, per the "translate once at save
 * time, not per character" rule shared with FAQ/Partner. A second translate only happens if the
 * admin explicitly clicks "Dịch lại" (`retranslateNow`). Cancels/ignores stale in-flight responses
 * via a request-sequence counter, and never overwrites a field the user has manually edited in the
 * English column (tracked by the caller via `touched`, honored by `applyIfUntouched`).
 */
export function useBilingualTranslate(params: {
  enabled: boolean;
  newsId?: number | null;
  title: string;
  summary: string;
  sections: BilingualSectionInput[];
  onTranslated: (result: { title: string; summary: string; sections: TranslatedSection[] }) => void;
}) {
  const { enabled, newsId, title, summary, sections, onTranslated } = params;
  const [translating, setTranslating] = useState(false);
  const seqRef = useRef(0);
  const wasEnabledRef = useRef(false);

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

  // Translate exactly once, the instant the EN column is switched on — never again on further
  // Vietnamese edits (that would be a per-keystroke translate, which this hook must not do).
  useEffect(() => {
    const justEnabled = enabled && !wasEnabledRef.current;
    wasEnabledRef.current = enabled;
    if (!justEnabled) return;
    if (!title.trim() && sections.every(s => !s.sectionTitle.trim() && !s.sectionBodyHtml.trim())) return;

    void runTranslate();
    // Only re-run when the toggle itself changes — intentionally excludes title/summary/sections
    // so typing in the Vietnamese column never re-triggers a translate call.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled]);

  /** Manual "Dịch lại" — the only other way to re-translate besides the initial toggle-on. */
  async function retranslateNow() {
    return runTranslate();
  }

  return { translating, retranslateNow };
}
