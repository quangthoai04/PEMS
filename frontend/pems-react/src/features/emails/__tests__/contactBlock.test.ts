/**
 * The shared contact-block helper (§6 of the visibility prompt).
 *
 * It replaced four separate inline regexes. The tests below pin the two properties that made a shared
 * helper worth having: it matches EXACTLY what the backend's substitution matches — no more, so a save
 * is never blocked over a placeholder that would have sent fine, and no less, so an unsubstituted one
 * never reaches a recipient — and removing a block does not disturb the text an operator wrote around it.
 */
import { describe, expect, it } from 'vitest';
import {
  CONTACT_BLOCK_MARKER,
  CONTACT_BLOCK_NAME,
  containsContactInformationBlock,
  removeContactBlockFromBoth,
  removeContactInformationBlock,
} from '../utils/contactBlock';

describe('the literal it matches', () => {
  it('is the same token the backend spells out', () => {
    expect(CONTACT_BLOCK_NAME).toBe('contactInformationBlock');
    expect(CONTACT_BLOCK_MARKER).toBe('{{contactInformationBlock}}');
  });

  it('finds the plain placeholder', () => {
    expect(containsContactInformationBlock('<p>Xin chào</p>{{contactInformationBlock}}')).toBe(true);
  });

  /** Quill URL-encodes braces inside an anchor. Both forms resolve at send time, so both count. */
  it('finds the URL-encoded form a rich editor produces', () => {
    expect(containsContactInformationBlock('%7B%7BcontactInformationBlock%7D%7D')).toBe(true);
  });

  it('tolerates whitespace inside the braces', () => {
    expect(containsContactInformationBlock('{{ contactInformationBlock }}')).toBe(true);
  });

  it('says no for empty and missing content', () => {
    expect(containsContactInformationBlock('')).toBe(false);
    expect(containsContactInformationBlock(null)).toBe(false);
    expect(containsContactInformationBlock(undefined)).toBe(false);
    expect(containsContactInformationBlock('<p>Không có khối nào</p>')).toBe(false);
  });

  /**
   * The near-miss cases, which is the whole reason the name is matched exactly.
   *
   * The backend substitutes case-sensitively, so none of these would ever be replaced at send time.
   * Treating one as "the contact block" would offer a removal that fixes nothing while hiding the real
   * fault — an unknown placeholder — behind the wrong message.
   */
  it('does not match a variable whose name merely looks similar', () => {
    expect(containsContactInformationBlock('{{contactInformationBlockX}}')).toBe(false);
    expect(containsContactInformationBlock('{{contactinformationblock}}')).toBe(false);
    expect(containsContactInformationBlock('{{ContactInformationBlock}}')).toBe(false);
    expect(containsContactInformationBlock('{{contactInformation}}')).toBe(false);
    expect(containsContactInformationBlock('{{myContactInformationBlock}}')).toBe(false);
  });

  /** A stateful /g regex held at module scope returns false on every other call. It is not. */
  it('answers the same way when asked twice', () => {
    const body = '<p>{{contactInformationBlock}}</p>';
    expect(containsContactInformationBlock(body)).toBe(true);
    expect(containsContactInformationBlock(body)).toBe(true);
    expect(containsContactInformationBlock(body)).toBe(true);
  });
});

describe('removing the block', () => {
  it('removes every occurrence, not only the first', () => {
    const out = removeContactInformationBlock(
      '<p>A</p>{{contactInformationBlock}}<p>B</p>{{contactInformationBlock}}<p>C</p>');

    expect(containsContactInformationBlock(out)).toBe(false);
    expect(out).toContain('<p>A</p>');
    expect(out).toContain('<p>B</p>');
    expect(out).toContain('<p>C</p>');
  });

  it('removes the URL-encoded form too', () => {
    const out = removeContactInformationBlock('<p>A</p>%7B%7BcontactInformationBlock%7D%7D');
    expect(containsContactInformationBlock(out)).toBe(false);
    expect(out).toContain('<p>A</p>');
  });

  /** The text around it is the operator's. A tidy-up they did not ask for is an edit they cannot see. */
  it('leaves neighbouring text exactly as it was', () => {
    const out = removeContactInformationBlock(
      '<p>Trân trọng,</p>{{contactInformationBlock}}<p style="color:#666">Phòng Đối ngoại</p>');

    expect(out).toBe('<p>Trân trọng,</p><p style="color:#666">Phòng Đối ngoại</p>');
  });

  /**
   * The one exception: a paragraph that existed only to hold the block. Left behind, `<p></p>` adds a
   * blank line to every mail sent from the template.
   */
  it('drops a paragraph the removal emptied', () => {
    expect(removeContactInformationBlock('<p>Xin chào</p><p>{{contactInformationBlock}}</p>'))
      .toBe('<p>Xin chào</p>');
  });

  it('drops an emptied list item and an emptied div as well', () => {
    expect(removeContactInformationBlock('<ul><li>{{contactInformationBlock}}</li></ul>'))
      .toBe('<ul></ul>');
    expect(removeContactInformationBlock('<div>{{contactInformationBlock}}</div>')).toBe('');
  });

  /** A paragraph that still has content is not this helper's business, blank line or not. */
  it('keeps a paragraph that still holds other text', () => {
    expect(removeContactInformationBlock('<p>Liên hệ: {{contactInformationBlock}}</p>'))
      .toBe('<p>Liên hệ: </p>');
  });

  it('does not remove a variable whose name merely looks similar', () => {
    const body = '<p>{{contactInformationBlockX}}</p>';
    expect(removeContactInformationBlock(body)).toBe(body);
  });

  it('answers with an empty string for empty and missing content', () => {
    expect(removeContactInformationBlock('')).toBe('');
    expect(removeContactInformationBlock(null)).toBe('');
    expect(removeContactInformationBlock(undefined)).toBe('');
  });

  /**
   * Both languages, always. A policy is one setting for the whole template, so clearing Vietnamese and
   * leaving English produces a template that is still refused — having already asked the operator to fix
   * it once.
   */
  it('clears both bodies in one call', () => {
    const out = removeContactBlockFromBoth({
      vi: '<p>Xin chào</p>{{contactInformationBlock}}',
      en: '<p>Hello</p>{{contactInformationBlock}}',
    });

    expect(containsContactInformationBlock(out.vi)).toBe(false);
    expect(containsContactInformationBlock(out.en)).toBe(false);
    expect(out.vi).toContain('Xin chào');
    expect(out.en).toContain('Hello');
  });
});
