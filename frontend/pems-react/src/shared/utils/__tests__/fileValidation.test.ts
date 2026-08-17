import { describe, expect, it } from 'vitest';
import { getFileValidationRule, validateFile } from '../fileValidation';

const MB = 1024 * 1024;
const file = (bytes: number, name: string, type: string): File =>
  new File([new Uint8Array(bytes)], name, { type });

/**
 * The visit-photo rule MUST mirror the backend (FileValidationPolicy.VisitRequestPhoto): images only,
 * 5 MB/file, no video, no PDF. It previously (wrongly) allowed 100 MB and mp4/webm — these tests pin
 * the corrected contract so the frontend can never again promise more than the backend accepts.
 */
describe('validateFile — VISIT_REQUEST_PHOTO canonical contract', () => {
  it('accepts a JPEG/PNG/WebP within 5 MB', () => {
    expect(validateFile(file(1024, 'a.jpg', 'image/jpeg'), 'VISIT_REQUEST_PHOTO').ok).toBe(true);
    expect(validateFile(file(1024, 'b.png', 'image/png'), 'VISIT_REQUEST_PHOTO').ok).toBe(true);
    expect(validateFile(file(1024, 'c.webp', 'image/webp'), 'VISIT_REQUEST_PHOTO').ok).toBe(true);
  });

  it('rejects a file larger than 5 MB', () => {
    const r = validateFile(file(6 * MB, 'big.jpg', 'image/jpeg'), 'VISIT_REQUEST_PHOTO');
    expect(r.ok).toBe(false);
  });

  it('rejects video (no mp4/webm on the visit-photo endpoint)', () => {
    expect(validateFile(file(1024, 'clip.mp4', 'video/mp4'), 'VISIT_REQUEST_PHOTO').ok).toBe(false);
    expect(validateFile(file(1024, 'clip.webm', 'video/webm'), 'VISIT_REQUEST_PHOTO').ok).toBe(false);
  });

  it('rejects a PDF', () => {
    expect(validateFile(file(1024, 'doc.pdf', 'application/pdf'), 'VISIT_REQUEST_PHOTO').ok).toBe(false);
  });

  it('rejects a spoofed extension whose MIME is not an allowed image', () => {
    // .exe with an image mime, or a mismatched type — the extension allowlist rejects it.
    expect(validateFile(file(1024, 'evil.exe', 'image/jpeg'), 'VISIT_REQUEST_PHOTO').ok).toBe(false);
  });
});

/** Builds a File of an exact byte size without allocating the bytes. */
function fileOfSize(name: string, type: string, size: number): File {
  const f = new File(['x'], name, { type });
  Object.defineProperty(f, 'size', { value: size });
  return f;
}

describe('fileValidation — gallery image purposes', () => {
  // Gallery ITEM media was split off the shared 5 MB image rule because real gallery photos
  // routinely exceed it. The split must not leak into the covers.
  describe('GALLERY_ITEM_IMAGE (gallery item media)', () => {
    it('caps at 20 MB, matching the backend FileValidationPolicy', () => {
      expect(getFileValidationRule('GALLERY_ITEM_IMAGE').maxSizeBytes).toBe(20 * MB);
    });

    it.each([
      ['8 MB JPG', 'photo.jpg', 'image/jpeg', 8 * MB],
      ['12 MB PNG', 'photo.png', 'image/png', 12 * MB],
      ['19.9 MB WEBP', 'photo.webp', 'image/webp', Math.round(19.9 * MB)],
      ['exactly 20 MB', 'photo.jpg', 'image/jpeg', 20 * MB],
    ])('accepts %s', (_label, name, type, size) => {
      expect(validateFile(fileOfSize(name, type, size), 'GALLERY_ITEM_IMAGE').ok).toBe(true);
    });

    it('rejects anything above 20 MB with the size reason', () => {
      const result = validateFile(fileOfSize('big.jpg', 'image/jpeg', 20 * MB + 1), 'GALLERY_ITEM_IMAGE');
      expect(result.ok).toBe(false);
      expect(result.message).toContain('20MB');
    });

    it.each([
      ['SVG', 'logo.svg', 'image/svg+xml'],
      ['GIF', 'anim.gif', 'image/gif'],
      ['PDF', 'doc.pdf', 'application/pdf'],
      ['MP4 through the image picker', 'clip.mp4', 'video/mp4'],
    ])('still rejects %s regardless of the bigger cap', (_label, name, type) => {
      expect(validateFile(fileOfSize(name, type, 1 * MB), 'GALLERY_ITEM_IMAGE').ok).toBe(false);
    });

    it('rejects an empty file', () => {
      expect(validateFile(fileOfSize('empty.jpg', 'image/jpeg', 0), 'GALLERY_ITEM_IMAGE').ok).toBe(false);
    });
  });

  describe('GALLERY_IMAGE (area / location covers) — unchanged', () => {
    it('still caps at 5 MB', () => {
      expect(getFileValidationRule('GALLERY_IMAGE').maxSizeBytes).toBe(5 * MB);
    });

    it('accepts a 5 MB cover', () => {
      expect(validateFile(fileOfSize('cover.jpg', 'image/jpeg', 5 * MB), 'GALLERY_IMAGE').ok).toBe(true);
    });

    it('rejects a cover above 5 MB even though gallery items now allow 20 MB', () => {
      const result = validateFile(fileOfSize('cover.jpg', 'image/jpeg', 6 * MB), 'GALLERY_IMAGE');
      expect(result.ok).toBe(false);
      expect(result.message).toContain('5MB');
    });
  });

  // Regression: purposes that share the old grouped rule must not have moved.
  it.each(['NEWS_IMAGE', 'VISIT_REQUEST_PHOTO', 'PARTNER_LOGO', 'PARTNER_COVER'] as const)(
    'leaves %s at 5 MB',
    (purpose) => {
      expect(getFileValidationRule(purpose).maxSizeBytes).toBe(5 * MB);
    },
  );

  it('leaves the area cover video rule at MP4 / 100 MB', () => {
    const rule = getFileValidationRule('GALLERY_VIDEO');
    expect(rule.maxSizeBytes).toBe(100 * MB);
    expect(rule.allowedMimeTypes).toContain('video/mp4');
  });
});
