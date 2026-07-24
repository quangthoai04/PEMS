import { describe, expect, it } from 'vitest';
import {
  formatMinutesStatus,
  formatAttendanceStatus,
  formatDocumentType,
  formatCoverageType,
  formatVisitStatus,
  formatActionItemStatus,
  UNKNOWN_DOMAIN_LABEL,
} from '../domainLabels';

describe('domainLabels — Vietnamese labels for user-facing enums', () => {
  it('maps minutes statuses', () => {
    expect(formatMinutesStatus('DRAFT')).toBe('Bản nháp');
    expect(formatMinutesStatus('SAVED')).toBe('Đã lưu');
    expect(formatMinutesStatus('PUBLISHED')).toBe('Đã xuất bản');
    expect(formatMinutesStatus('ARCHIVED')).toBe('Đã lưu trữ');
  });

  it('maps attendance statuses', () => {
    expect(formatAttendanceStatus('PRESENT')).toBe('Có mặt');
    expect(formatAttendanceStatus('EXCUSED')).toBe('Vắng có phép');
    expect(formatAttendanceStatus('ABSENT')).toBe('Vắng mặt');
  });

  it('maps document type and coverage', () => {
    expect(formatDocumentType('GENERAL')).toBe('Tài liệu chung');
    expect(formatDocumentType('VISIT')).toBe('Theo đoàn tiếp khách');
    expect(formatCoverageType('COVERAGE_GENERAL')).toBe('Phạm vi chung');
  });

  it('maps visit and action-item statuses', () => {
    expect(formatVisitStatus('AFTER_VISIT')).toBe('Sau tiếp khách');
    expect(formatActionItemStatus('TODO')).toBe('Cần làm');
    expect(formatActionItemStatus('DONE')).toBe('Hoàn tất');
  });

  it('is case-insensitive on the code', () => {
    expect(formatMinutesStatus('draft')).toBe('Bản nháp');
    expect(formatAttendanceStatus('present')).toBe('Có mặt');
  });

  it('returns the neutral label for unknown / empty / non-string, never the raw token', () => {
    expect(formatMinutesStatus('SOMETHING_NEW')).toBe(UNKNOWN_DOMAIN_LABEL);
    expect(formatMinutesStatus('')).toBe(UNKNOWN_DOMAIN_LABEL);
    expect(formatMinutesStatus(null)).toBe(UNKNOWN_DOMAIN_LABEL);
    expect(formatMinutesStatus(undefined)).toBe(UNKNOWN_DOMAIN_LABEL);
    expect(UNKNOWN_DOMAIN_LABEL).toBe('Chưa xác định');
  });
});
