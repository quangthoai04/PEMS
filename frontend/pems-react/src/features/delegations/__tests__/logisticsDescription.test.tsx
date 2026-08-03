/**
 * The Host's "Mô tả chi tiết" on a logistics request, on both ends of the round trip:
 * what the compose screen previews, and what the receiving department is shown.
 *
 * The defect these cover: the description was stored correctly all along, but the preview supplied a
 * DIFFERENT field in its place (`offlineCoordinationNote`, which a SYSTEM_REQUEST never sets), and
 * every recipient screen fell back to something from the calendar feed — whose `title` is literally
 * "Yêu cầu: " + the item title — so "NỘI DUNG CHI TIẾT CÔNG VIỆC" read "Yêu cầu: Teabreak".
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import {
  EMPTY_DESCRIPTION,
  EMPTY_QUANTITY,
  EMPTY_TIME,
  buildLogisticsEmailContext,
} from '../utils/logisticsEmailContext';
import {
  EMPTY_DESCRIPTION_TEXT,
  LOADING_DESCRIPTION_TEXT,
  LogisticsWorkContent,
  resolveLogisticsDescription,
} from '../../department-reception-tasks/components/LogisticsWorkContent';

/** The real formatter's contract: string slicing, em dash for nothing. */
const fmtDateTime = (value?: string | null): string => {
  if (!value) return '—';
  const [d, t] = value.replace(' ', 'T').split('T');
  const [y, m, day] = (d ?? '').split('-');
  const hm = (t ?? '').slice(0, 5);
  if (!y || !m || !day) return value;
  return hm ? `${hm} ${day}/${m}/${y}` : `${day}/${m}/${y}`;
};

const ITEM_TYPE_LABEL: Record<string, string> = {
  ROOM: 'Phòng / Hội trường',
  TRANSPORT: 'Xe / Di chuyển',
  MEAL: 'Suất ăn / Teabreak',
  EQUIPMENT: 'Thiết bị',
  BANNER: 'Banner / Standee',
  LED: 'Màn hình LED',
  OTHER: 'Yêu cầu khác',
};

const options = {
  leaderName: 'Phạm Thị Trưởng Phòng',
  requesterName: 'Trần Thị Hà',
  itemTypeLabel: (t: string) => ITEM_TYPE_LABEL[t] ?? t,
  formatDateTime: fmtDateTime,
};

const teabreak = {
  title: 'Teabreak',
  itemType: 'MEAL',
  description: 'Chuẩn bị teabreak cho 20 khách, gồm trà, cà phê,\nnước suối và bánh ngọt.\nBố trí trước giờ họp 15 phút.',
  quantity: 20,
  usageStartAt: '2026-08-23T09:00',
  usageEndAt: '2026-08-23T12:00',
};

describe('preview context for LOGISTICS_REQUEST_TO_DEPARTMENT', () => {
  it('sends the description the Host actually typed', () => {
    const ctx = buildLogisticsEmailContext(teabreak, options);
    expect(ctx.logisticsDescription).toBe(teabreak.description);
  });

  it('declares exactly the eight variables the template does', () => {
    expect(Object.keys(buildLogisticsEmailContext(teabreak, options)).sort()).toEqual([
      'departmentLeaderName',
      'logisticsDescription',
      'logisticsItemType',
      'logisticsTitle',
      'quantity',
      'requesterName',
      'usageEndAt',
      'usageStartAt',
    ]);
  });

  it('carries no response deadline and no coordination note', () => {
    // The renderer refuses an undeclared key, so either of these reappearing breaks the send outright.
    // They are named individually so a reinstatement fails with the reason, not as a key-count mismatch.
    const ctx = buildLogisticsEmailContext(teabreak, options);
    expect(ctx.dueAt).toBeUndefined();
    expect(ctx.coordinationNote).toBeUndefined();
  });

  it('never reads offlineCoordinationNote in place of the description', () => {
    // The exact shape of the old bug: a SYSTEM_REQUEST payload that also happens to carry a
    // coordination note must still preview the DESCRIPTION.
    const ctx = buildLogisticsEmailContext(
      { ...teabreak, offlineCoordinationNote: 'Đã trao đổi trực tiếp với phòng ban.' } as never,
      options,
    );
    expect(ctx.logisticsDescription).toBe(teabreak.description);
    expect(ctx.logisticsDescription).not.toContain('Đã trao đổi');
  });

  it('keeps the line breaks of a multi-line description', () => {
    const ctx = buildLogisticsEmailContext(teabreak, options);
    expect(ctx.logisticsDescription.split('\n')).toHaveLength(3);
  });

  it('uses the same empty wording the server does, never the title', () => {
    for (const blank of [undefined, null, '', '   ', '\n\n']) {
      const ctx = buildLogisticsEmailContext({ ...teabreak, description: blank }, options);
      expect(ctx.logisticsDescription).toBe(EMPTY_DESCRIPTION);
      expect(ctx.logisticsDescription).not.toContain('Teabreak');
    }
  });

  it('shows a business label for the item type, never the column code', () => {
    for (const [code, label] of Object.entries(ITEM_TYPE_LABEL)) {
      const ctx = buildLogisticsEmailContext({ ...teabreak, itemType: code }, options);
      expect(ctx.logisticsItemType).toBe(label);
      expect(ctx.logisticsItemType).not.toBe(code);
    }
  });

  it('states missing quantity and times rather than leaving them blank', () => {
    const ctx = buildLogisticsEmailContext(
      { title: 'Việc khác', itemType: 'OTHER', quantity: null, usageStartAt: null, usageEndAt: null },
      options,
    );
    expect(ctx.quantity).toBe(EMPTY_QUANTITY);
    expect(ctx.usageStartAt).toBe(EMPTY_TIME);
    expect(ctx.usageEndAt).toBe(EMPTY_TIME);
  });

  it('does not mix up two items on the same visit', () => {
    const room = { title: 'Phòng họp', itemType: 'ROOM', description: 'Phòng 30 chỗ, hai micro.', quantity: 1 };
    const a = buildLogisticsEmailContext(teabreak, options);
    const b = buildLogisticsEmailContext(room, options);

    expect(a.logisticsDescription).toBe(teabreak.description);
    expect(b.logisticsDescription).toBe(room.description);
    expect(a.logisticsItemType).toBe('Suất ăn / Teabreak');
    expect(b.logisticsItemType).toBe('Phòng / Hội trường');
  });
});

describe('what the receiving department is shown', () => {
  it('renders the description from the request detail', () => {
    render(<LogisticsWorkContent detail={{ description: teabreak.description }} />);
    expect(screen.getByText(/Chuẩn bị teabreak cho 20 khách/)).toBeInTheDocument();
    expect(screen.getByText(/Bố trí trước giờ họp 15 phút/)).toBeInTheDocument();
  });

  it('keeps line breaks without dangerouslySetInnerHTML', () => {
    const { container } = render(<LogisticsWorkContent detail={{ description: 'Dòng một\nDòng hai' }} />);
    const p = container.querySelector('p')!;
    // The newline survives in the text node, and CSS — not <br> injection — renders it.
    expect(p.textContent).toBe('Dòng một\nDòng hai');
    expect(p.className).toContain('whitespace-pre-wrap');
    expect(p.innerHTML).not.toContain('<br');
  });

  it('displays HTML in a description instead of executing it', () => {
    const hostile = '<script>alert(1)</script><img src=x onerror=alert(1)>';
    const { container } = render(<LogisticsWorkContent detail={{ description: hostile }} />);

    expect(container.querySelector('script')).toBeNull();
    expect(container.querySelector('img')).toBeNull();
    expect(container.textContent).toContain(hostile);
  });

  it('long unbroken content wraps rather than widening the modal', () => {
    const { container } = render(
      <LogisticsWorkContent detail={{ description: 'A'.repeat(400) }} />,
    );
    expect(container.querySelector('p')!.className).toContain('break-words');
  });

  it('shows the empty state for a legacy row with no description — never the title', () => {
    for (const blank of [null, undefined, '', '   ']) {
      const { container, unmount } = render(
        <LogisticsWorkContent detail={{ description: blank, title: 'Teabreak' } as never} />,
      );
      expect(container.textContent).toBe(EMPTY_DESCRIPTION_TEXT);
      expect(container.textContent).not.toContain('Teabreak');
      expect(container.textContent).not.toContain('Yêu cầu:');
      unmount();
    }
  });

  it('distinguishes "still loading" from "there is none"', () => {
    // A detail that has not arrived must not flash the empty state, which reads as a factual claim.
    expect(resolveLogisticsDescription(null)).toEqual({ state: 'loading' });
    expect(resolveLogisticsDescription(undefined)).toEqual({ state: 'loading' });
    expect(resolveLogisticsDescription({ description: '  ' })).toEqual({ state: 'empty' });

    const { container } = render(<LogisticsWorkContent detail={null} />);
    expect(container.textContent).toBe(LOADING_DESCRIPTION_TEXT);
  });

  it('never falls back to the calendar feed title', () => {
    // The calendar DTO shape that caused the bug: a title prefixed "Yêu cầu: ", and no description.
    const calendarEvent = { title: 'Yêu cầu: Teabreak', purpose: 'Yêu cầu: Teabreak' };
    const { container } = render(<LogisticsWorkContent detail={calendarEvent as never} />);
    expect(container.textContent).toBe(EMPTY_DESCRIPTION_TEXT);
  });

  it('shows the description and not the coordination note when a row carries both', () => {
    const { container } = render(
      <LogisticsWorkContent
        detail={{ description: 'Việc cần làm.', offlineCoordinationNote: 'Ghi chú phối hợp riêng.' } as never}
      />,
    );
    expect(container.textContent).toBe('Việc cần làm.');
    expect(container.textContent).not.toContain('phối hợp');
  });
});
