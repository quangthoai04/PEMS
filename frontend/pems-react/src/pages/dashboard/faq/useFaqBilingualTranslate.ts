import { useEffect, useRef, useState } from 'react';
import httpClient from '../../../shared/api/httpClient';

interface FaqDraftTranslateResponse {
  question: string;
  answer: string;
}

/**
 * One-shot Vietnamese → English auto-translate for the bilingual FAQ create/edit form. Mirrors
 * News's `useBilingualTranslate` (see pages/dashboard/news/components/useBilingualTranslate.ts):
 * translates exactly once, the moment `enabled` flips from false → true (the admin just clicked
 * the "EN" toggle) — never on every keystroke. A second translate only happens if the admin
 * explicitly clicks "Dịch lại". Always calls POST /faqs/translate-draft (preview only, never
 * persists) since FAQ has no draft/existing-id distinction the way News does.
 */
export function useFaqBilingualTranslate(params: {
  enabled: boolean;
  question: string;
  answer: string;
  onTranslated: (result: { question: string; answer: string }) => void;
}) {
  const { enabled, question, answer, onTranslated } = params;
  const [translating, setTranslating] = useState(false);
  const seqRef = useRef(0);
  const wasEnabledRef = useRef(false);

  async function runTranslate(): Promise<boolean> {
    const mySeq = ++seqRef.current;
    setTranslating(true);
    try {
      const { data } = await httpClient.post<FaqDraftTranslateResponse>('/faqs/translate-draft', {
        sourceLanguage: 'vi',
        targetLanguage: 'en',
        question,
        answer,
      });

      // A newer call already started (or finished) — this response is stale, ignore it.
      if (mySeq !== seqRef.current) return false;

      onTranslated({ question: data.question ?? '', answer: data.answer ?? '' });
      return true;
    } catch {
      return false;
    } finally {
      if (mySeq === seqRef.current) setTranslating(false);
    }
  }

  // Translate exactly once, the instant the EN column is switched on.
  useEffect(() => {
    const justEnabled = enabled && !wasEnabledRef.current;
    wasEnabledRef.current = enabled;
    if (!justEnabled) return;
    if (!question.trim() && !answer.trim()) return;

    void runTranslate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled]);

  /** Manual "Dịch lại" — the only other way to re-translate besides the initial toggle-on. */
  async function retranslateNow() {
    return runTranslate();
  }

  return { translating, retranslateNow };
}
