import { describe, expect, it } from 'vitest';
import { validateFile } from '../fileValidation';

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
