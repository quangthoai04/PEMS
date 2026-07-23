import { useEffect, useRef, useState } from 'react';
import { partnersApi } from '../../../features/partners/api/partnersApi';

/**
 * One-shot Vietnamese → English auto-translate for the bilingual Partner create/edit form. Mirrors
 * News's `useBilingualTranslate` / FAQ's `useFaqBilingualTranslate`: translates exactly once, the
 * moment `enabled` flips from false → true (the admin just clicked the "EN" toggle) — never on
 * every keystroke. A second translate only happens if the admin explicitly clicks "Dịch lại".
 * Always calls POST /partners/translate-draft (preview only, never persists).
 */
export function usePartnerBilingualTranslate(params: {
  enabled: boolean;
  name: string;
  shortName: string;
  country: string;
  city: string;
  description: string;
  address: string;
  onTranslated: (result: { name: string; shortName: string; description: string; address: string }) => void;
}) {
  const { enabled, name, shortName, country, city, description, address, onTranslated } = params;
  const [translating, setTranslating] = useState(false);
  const seqRef = useRef(0);
  const wasEnabledRef = useRef(false);

  async function runTranslate(): Promise<boolean> {
    const mySeq = ++seqRef.current;
    setTranslating(true);
    try {
      const data = await partnersApi.translateDraft({ name, shortName, country, city, description, address });

      // A newer call already started (or finished) — this response is stale, ignore it.
      if (mySeq !== seqRef.current) return false;

      onTranslated({
        name: data.name ?? '',
        shortName: data.shortName ?? '',
        description: data.description ?? '',
        address: data.address ?? '',
      });
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
    if (!name.trim()) return;

    void runTranslate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled]);

  /** Manual "Dịch lại" — the only other way to re-translate besides the initial toggle-on. */
  async function retranslateNow() {
    return runTranslate();
  }

  return { translating, retranslateNow };
}
