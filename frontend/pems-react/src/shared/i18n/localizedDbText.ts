/**
 * Picks the display string for DATABASE-backed bilingual content (VI stored + EN auto-translated).
 * UI-chrome strings keep using i18next resources; this helper is ONLY for values coming from the API.
 *
 * Rules:
 *  - English UI + non-blank EN value → EN.
 *  - Otherwise → VI (never an empty string, never "undefined"/"null" leaking into the UI).
 *
 * `language` should be `i18n.resolvedLanguage ?? i18n.language` — the runtime value can be "en",
 * "en-US", "vi", "vi-VN"…, so match by prefix, never by strict equality.
 */
export function localizedDbText(
  vi: string | null | undefined,
  en: string | null | undefined,
  language: string | undefined,
): string {
  if (isEnglishLanguage(language) && en?.trim()) {
    return en.trim();
  }
  return vi?.trim() ?? '';
}

/** True when the current i18n language is any English variant ("en", "en-US", …). */
export function isEnglishLanguage(language?: string): boolean {
  return language?.toLowerCase().startsWith('en') ?? false;
}
