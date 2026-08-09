import { describe, expect, it } from 'vitest';
import i18n from '../../../../shared/i18n/config';
import {
  VisitNewsReasonCode,
  visitNewsReasonKey,
  visitNewsReasonKeyOrNull,
} from '../visitNewsEligibility';

/**
 * Every reason the backend can give has exactly ONE sentence, in both languages.
 *
 * The point of the whole change: the page used to print a single sentence listing three possible
 * causes, so whichever one actually applied, most of what the user read was false.
 */
describe('visitNewsEligibility reason mapping', () => {
  const ALL_CODES = Object.values(VisitNewsReasonCode);

  it('maps every backend reason code to its own key', () => {
    const keys = ALL_CODES.map(code => visitNewsReasonKeyOrNull(code));
    expect(keys.every(k => typeof k === 'string')).toBe(true);
    expect(new Set(keys).size).toBe(ALL_CODES.length);
  });

  it.each(['vi', 'en'] as const)('resolves a distinct %s sentence for every reason', async lng => {
    await i18n.changeLanguage(lng);
    const sentences = ALL_CODES.map(code => i18n.t(visitNewsReasonKey(code)));

    // No sentence may fall through to the raw key, and none may repeat: two causes sharing a
    // sentence is the same failure as one sentence naming two causes.
    sentences.forEach(s => expect(s.startsWith('news:')).toBe(false));
    expect(new Set(sentences).size).toBe(ALL_CODES.length);
    await i18n.changeLanguage('en');
  });

  it('refuses to translate a code that is not a visit-news reason', () => {
    // A failed POST can answer VALIDATION_ERROR. Rendering that as "this visit cannot have news"
    // would replace a true message with a false one.
    expect(visitNewsReasonKeyOrNull('VALIDATION_ERROR')).toBeNull();
    expect(visitNewsReasonKeyOrNull(null)).toBeNull();
    expect(visitNewsReasonKeyOrNull(undefined)).toBeNull();
  });

  it('falls back only for an unknown code on the verdict channel', () => {
    expect(visitNewsReasonKey('SOMETHING_ADDED_LATER')).toBe('news:visitEligibility.unknown');
    expect(visitNewsReasonKey(VisitNewsReasonCode.MediaConsentDenied))
      .toBe('news:visitEligibility.mediaConsentDenied');
  });
});
