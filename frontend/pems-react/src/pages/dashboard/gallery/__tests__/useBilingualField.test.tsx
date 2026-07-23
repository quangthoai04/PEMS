/**
 * Tests for the bilingual-name translation state machine used by the area/location create + edit
 * modals (§12–15 of the translation-preview plan): preview apply, manual edits, VI drift → STALE
 * (blocking) vs manual review warning (non-blocking), and the save-payload origin/hash decisions.
 */

import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import {
  fieldBlockingError,
  fieldPayload,
  normalizeVi,
  useBilingualField,
} from '../locationModalShared';

describe('normalizeVi', () => {
  it('trims and collapses whitespace, preserving diacritics/casing', () => {
    expect(normalizeVi('  Tòa    Alpha  ')).toBe('Tòa Alpha');
    expect(normalizeVi('TÒA\t Delta\n đẹp')).toBe('TÒA Delta đẹp');
  });
});

describe('useBilingualField', () => {
  it('starts IDLE with origin NONE', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', ''));
    expect(result.current.state).toBe('IDLE');
    expect(result.current.origin).toBe('NONE');
    expect(fieldPayload(result.current)).toEqual({ en: null, origin: 'NONE', sourceHash: null });
  });

  it('applyPreview → READY / AUTO_PREVIEW and the payload carries en + hash', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', ''));
    act(() => result.current.applyPreview('Alpha Building', 'hash-1'));

    expect(result.current.state).toBe('READY');
    expect(result.current.origin).toBe('AUTO_PREVIEW');
    expect(result.current.en).toBe('Alpha Building');
    expect(fieldPayload(result.current)).toEqual({
      en: 'Alpha Building', origin: 'AUTO_PREVIEW', sourceHash: 'hash-1',
    });
    expect(fieldBlockingError(result.current, 'Khu vực')).toBeNull();
  });

  it('VI drift after an auto preview → STALE (blocks the save), typing it back → READY again', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', ''));
    act(() => result.current.applyPreview('Alpha Building', 'hash-1'));

    act(() => result.current.setVi('Tòa Alpha mới'));
    expect(result.current.state).toBe('STALE');
    expect(fieldBlockingError(result.current, 'Khu vực')).not.toBeNull();
    // A stale auto-preview must never be sent as AUTO_PREVIEW.
    expect(fieldPayload(result.current).origin).not.toBe('AUTO_PREVIEW');

    act(() => result.current.setVi('Tòa   Alpha')); // same normalized source as the preview
    expect(result.current.state).toBe('READY');
    expect(fieldBlockingError(result.current, 'Khu vực')).toBeNull();
  });

  it('hand-editing the EN → MANUAL, and VI drift only warns (never blocks)', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', ''));
    act(() => result.current.applyPreview('Alpha Building', 'hash-1'));
    act(() => result.current.setEnManual('Alpha Tower'));

    expect(result.current.origin).toBe('MANUAL');
    expect(result.current.manuallyEdited).toBe(true);
    expect(fieldPayload(result.current)).toEqual({ en: 'Alpha Tower', origin: 'MANUAL', sourceHash: null });

    act(() => result.current.setVi('Tòa Alpha mới'));
    expect(result.current.manualNeedsReview).toBe(true);
    expect(fieldBlockingError(result.current, 'Khu vực')).toBeNull(); // manual EN is keepable (§15.2)
    expect(fieldPayload(result.current).origin).toBe('MANUAL');
  });

  it('clearing the EN resets to NONE so the backend auto-translates on save', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', ''));
    act(() => result.current.applyPreview('Alpha Building', 'hash-1'));
    act(() => result.current.setEnManual(''));

    expect(result.current.origin).toBe('NONE');
    expect(result.current.state).toBe('IDLE');
    expect(fieldPayload(result.current)).toEqual({ en: null, origin: 'NONE', sourceHash: null });
  });

  it('a prefilled stored EN that was never touched is sent as NONE (edit modal)', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', 'Alpha Building'));
    // Untouched: the backend decides (keep when VI unchanged / re-translate when it changed).
    expect(fieldPayload(result.current)).toEqual({ en: null, origin: 'NONE', sourceHash: null });
  });

  it('editing ONLY the VI over a prefilled stored EN raises the review warning (edit modal)', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', 'Alpha Building'));
    expect(result.current.manualNeedsReview).toBe(false);

    act(() => result.current.setVi('Tòa MEGA lớn nhất FPT'));
    expect(result.current.manualNeedsReview).toBe(true);
    expect(fieldBlockingError(result.current, 'Khu vực')).toBeNull(); // warning only, never blocks

    act(() => result.current.setVi('Tòa   Alpha')); // reverting to the stored VI clears it
    expect(result.current.manualNeedsReview).toBe(false);
  });

  it('reset() with a stored EN re-arms the review warning basis', () => {
    const { result } = renderHook(() => useBilingualField());
    act(() => result.current.reset('Trước tòa', 'In Front of the Building'));
    act(() => result.current.setVi('Sảnh phía trước'));
    expect(result.current.manualNeedsReview).toBe(true);
  });

  it('no review warning when there is no EN to review', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', ''));
    act(() => result.current.setVi('Tòa Beta'));
    expect(result.current.manualNeedsReview).toBe(false);
    expect(result.current.state).toBe('IDLE');
  });

  it('reset() re-initializes everything (edit modal prefill)', () => {
    const { result } = renderHook(() => useBilingualField());
    act(() => result.current.applyPreview('Alpha Building', 'hash-1'));
    act(() => result.current.reset('Trước tòa', 'In Front of the Building'));

    expect(result.current.vi).toBe('Trước tòa');
    expect(result.current.en).toBe('In Front of the Building');
    expect(result.current.origin).toBe('NONE');
    expect(result.current.state).toBe('IDLE');
    expect(result.current.manuallyEdited).toBe(false);
  });

  it('markFailed only applies while TRANSLATING (an old error cannot clobber a newer preview)', () => {
    const { result } = renderHook(() => useBilingualField('Tòa Alpha', ''));
    act(() => result.current.beginTranslating());
    expect(result.current.state).toBe('TRANSLATING');
    act(() => result.current.markFailed());
    expect(result.current.state).toBe('FAILED');

    act(() => result.current.applyPreview('Alpha Building', 'hash-2'));
    act(() => result.current.markFailed()); // stale failure arriving late
    expect(result.current.state).toBe('READY');
  });
});
