import { describe, expect, it } from 'vitest';

import { isEnglishLanguage, localizedDbText } from '../localizedDbText';

/**
 * localizedDbText — the ONLY rule for displaying DB-backed bilingual strings on the public gallery:
 * EN header + non-blank EN → EN; anything else → VI. Never returns "undefined"/"null"/raw blanks.
 */
describe('localizedDbText', () => {
  it('returns EN when the language is English and EN exists', () => {
    expect(localizedDbText('Tòa Alpha', 'Alpha Building', 'en')).toBe('Alpha Building');
  });

  it('matches regional English variants (en-US)', () => {
    expect(localizedDbText('Tòa Alpha', 'Alpha Building', 'en-US')).toBe('Alpha Building');
  });

  it('falls back to VI when EN is null (translation not READY)', () => {
    expect(localizedDbText('Tòa Alpha', null, 'en')).toBe('Tòa Alpha');
  });

  it('falls back to VI when EN is blank', () => {
    expect(localizedDbText('Tòa Alpha', '   ', 'en')).toBe('Tòa Alpha');
  });

  it('returns VI for Vietnamese language even when EN exists', () => {
    expect(localizedDbText('Tòa Alpha', 'Alpha Building', 'vi')).toBe('Tòa Alpha');
    expect(localizedDbText('Tòa Alpha', 'Alpha Building', 'vi-VN')).toBe('Tòa Alpha');
  });

  it('trims the returned value', () => {
    expect(localizedDbText('  Tòa Alpha  ', '  Alpha Building  ', 'en')).toBe('Alpha Building');
    expect(localizedDbText('  Tòa Alpha  ', null, 'en')).toBe('Tòa Alpha');
  });

  it('never renders "undefined"/"null" — empty string when both missing', () => {
    expect(localizedDbText(null, null, 'en')).toBe('');
    expect(localizedDbText(undefined, undefined, 'vi')).toBe('');
  });

  it('undefined language behaves as Vietnamese', () => {
    expect(localizedDbText('Tòa Alpha', 'Alpha Building', undefined)).toBe('Tòa Alpha');
  });
});

describe('isEnglishLanguage', () => {
  it.each(['en', 'en-US', 'EN', 'en-GB'])('true for %s', (lng) => {
    expect(isEnglishLanguage(lng)).toBe(true);
  });

  it.each(['vi', 'vi-VN', '', undefined])('false for %s', (lng) => {
    expect(isEnglishLanguage(lng as string | undefined)).toBe(false);
  });
});
