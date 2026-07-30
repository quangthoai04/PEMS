/**
 * The recipient rules, checked against the behaviour of the backend `EmailRecipientValidator` they
 * mirror. These are contract tests as much as unit tests: if the server tightens a rule and this file
 * still passes, the two have drifted and the UI will start promising something the API rejects.
 */
import { describe, expect, it } from 'vitest';
import {
  EMAIL_ERROR_CODES,
  countRecipients,
  emptyEnvelope,
  hasHeaderBreak,
  isWellFormedEmail,
  normalizeEmail,
  splitPastedRecipients,
  validateEnvelope,
  type RecipientEnvelope,
} from '../types/recipients';

const to = (...emails: string[]): RecipientEnvelope => ({
  ...emptyEnvelope(),
  TO: emails.map(email => ({ email })),
});

const envelope = (t: string[], c: string[] = [], b: string[] = []): RecipientEnvelope => ({
  TO: t.map(email => ({ email })),
  CC: c.map(email => ({ email })),
  BCC: b.map(email => ({ email })),
});

const codes = (problems: { code: string }[]) => problems.map(p => p.code);

describe('isWellFormedEmail — mirrors EmailRecipientValidator.IsWellFormed', () => {
  it.each([
    'ha.nguyen@fpt.edu.vn',
    'a@b.co',
    'first+tag@sub.domain.vn',
  ])('accepts %s', email => {
    expect(isWellFormedEmail(email)).toBe(true);
  });

  it.each([
    ['empty', ''],
    ['no at sign', 'nobody'],
    ['two at signs', 'a@b@c.com'],
    ['empty local part', '@fpt.edu.vn'],
    ['domain shorter than 3', 'a@b'],
    ['domain without a dot', 'a@localhost'],
    ['leading dot in domain', 'a@.fpt.vn'],
    ['trailing dot in domain', 'a@fpt.vn.'],
    ['consecutive dots', 'a@fpt..vn'],
    ['whitespace inside', 'a b@fpt.vn'],
  ])('rejects %s', (_label, email) => {
    expect(isWellFormedEmail(email)).toBe(false);
  });

  it('rejects an address carrying a header break, which is how a Bcc header gets injected', () => {
    expect(isWellFormedEmail('a@b.com\r\nBcc: attacker@evil.com')).toBe(false);
    expect(hasHeaderBreak('a@b.com\r\nBcc: x@y.com')).toBe(true);
  });
});

describe('normalizeEmail', () => {
  it('compares case-insensitively but is not what we display', () => {
    expect(normalizeEmail('  Ha.Nguyen@FPT.edu.vn ')).toBe('ha.nguyen@fpt.edu.vn');
  });
});

describe('validateEnvelope', () => {
  it('requires at least one TO when the caller says TO is required', () => {
    expect(codes(validateEnvelope(emptyEnvelope(), 50))).toContain(EMAIL_ERROR_CODES.recipientRequired);
  });

  it('does not require TO for reply, whose TO is the original sender', () => {
    expect(codes(validateEnvelope(emptyEnvelope(), 50, false)))
      .not.toContain(EMAIL_ERROR_CODES.recipientRequired);
  });

  it('accepts a well-formed envelope', () => {
    expect(validateEnvelope(envelope(['a@fpt.vn'], ['b@fpt.vn'], ['c@fpt.vn']), 50)).toEqual([]);
  });

  it('flags an invalid address and says which group it was in', () => {
    const problems = validateEnvelope(envelope(['nope']), 50);
    expect(problems[0].code).toBe(EMAIL_ERROR_CODES.recipientInvalid);
    expect(problems[0].group).toBe('TO');
    expect(problems[0].message).toContain('Đến');
  });

  it.each([
    ['TO', envelope(['a@fpt.vn', 'A@FPT.VN'])],
    ['CC', envelope(['x@fpt.vn'], ['a@fpt.vn', 'a@fpt.vn'])],
    ['BCC', envelope(['x@fpt.vn'], [], ['a@fpt.vn', 'A@fpt.vn'])],
  ])('rejects a duplicate within %s, case-insensitively', (_group, env) => {
    expect(codes(validateEnvelope(env, 50))).toContain(EMAIL_ERROR_CODES.recipientDuplicate);
  });

  it.each([
    ['TO and CC', envelope(['a@fpt.vn'], ['A@fpt.vn'])],
    ['TO and BCC', envelope(['a@fpt.vn'], [], ['A@fpt.vn'])],
    ['CC and BCC', envelope(['x@fpt.vn'], ['a@fpt.vn'], ['A@fpt.vn'])],
  ])('rejects the same address across %s', (_pair, env) => {
    expect(codes(validateEnvelope(env, 50)))
      .toContain(EMAIL_ERROR_CODES.recipientCrossGroupDuplicate);
  });

  it('counts TO + CC + BCC against the ceiling, not each group separately', () => {
    const env = envelope(
      Array.from({ length: 2 }, (_, i) => `t${i}@fpt.vn`),
      Array.from({ length: 2 }, (_, i) => `c${i}@fpt.vn`),
      Array.from({ length: 2 }, (_, i) => `b${i}@fpt.vn`),
    );
    expect(countRecipients(env)).toBe(6);
    expect(codes(validateEnvelope(env, 5))).toContain(EMAIL_ERROR_CODES.recipientLimitExceeded);
    expect(codes(validateEnvelope(env, 6))).not.toContain(EMAIL_ERROR_CODES.recipientLimitExceeded);
  });

  it('reports every problem, not only the first — the backend throws on the first, a person needs all', () => {
    const problems = validateEnvelope(envelope(['bad', 'also-bad']), 50);
    expect(problems.length).toBeGreaterThanOrEqual(2);
  });

  it('reports a header break in a display name', () => {
    const env = emptyEnvelope();
    env.TO = [{ email: 'a@fpt.vn', name: 'Ha\r\nBcc: x@y.com' }];
    expect(codes(validateEnvelope(env, 50))).toContain(EMAIL_ERROR_CODES.headerInvalid);
  });

  it('ignores blank entries rather than calling them invalid', () => {
    expect(validateEnvelope(to('a@fpt.vn', '   '), 50)).toEqual([]);
  });
});

describe('splitPastedRecipients', () => {
  it('splits on comma, semicolon, whitespace and newlines', () => {
    expect(splitPastedRecipients('a@f.vn, b@f.vn; c@f.vn\nd@f.vn e@f.vn'))
      .toEqual(['a@f.vn', 'b@f.vn', 'c@f.vn', 'd@f.vn', 'e@f.vn']);
  });

  it('drops empty fragments from trailing separators', () => {
    expect(splitPastedRecipients('a@f.vn,,  ,')).toEqual(['a@f.vn']);
  });
});
