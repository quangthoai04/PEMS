/**
 * A brand-new instance (no agenda yet) should apply its default template automatically — the
 * registrant already picked a visit type on the public form, so the matching default is exactly
 * what the instance should start with, and requiring the host to click "Áp dụng" for the obvious
 * case was a step for nothing. This only ever fires the FIRST time: once `hasExistingAgenda` flips
 * true, re-opening the panel (to change the template) always lands on the normal manual picker.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import type { AgendaSetupForInstance } from '../../types/agendaTemplates.types';

const getSetupForInstance = vi.fn();
const apply = vi.fn();

vi.mock('../../api/agendaTemplatesApi', () => ({
  default: {
    getSetupForInstance: (...a: unknown[]) => getSetupForInstance(...a),
    apply: (...a: unknown[]) => apply(...a),
  },
}));

import { AgendaSetupPanel } from '../AgendaSetupPanel';

const template = (id: number, isDefault: boolean) => ({
  agendaTemplateId: id,
  campusId: 1,
  campusScopeKey: 'CAMPUS:1',
  visitType: 'CAMPUS_TOUR' as const,
  name: `Mẫu ${id}`,
  description: null,
  status: 'ACTIVE' as const,
  itemCount: 1,
  isDefault,
  items: [{
    agendaTemplateItemId: id * 10,
    displayOrder: 1,
    startOffsetMinutes: 0,
    durationMinutes: 20,
    title: 'Đón đoàn tại sảnh',
    description: null,
    location: 'Sảnh chính',
    responsibleRoleLabel: null,
  }],
});

const setup = (overrides: Partial<AgendaSetupForInstance> = {}): AgendaSetupForInstance => ({
  visitInstanceId: 501,
  visitRequestId: 9001,
  campusId: 1,
  visitType: 'CAMPUS_TOUR',
  plannedStartAt: '2026-08-29T09:00:00',
  plannedEndAt: '2026-08-29T10:00:00',
  relation: 'HOST',
  canApply: true,
  defaultTemplateId: 1,
  defaultScope: 'CAMPUS',
  hasExistingAgenda: false,
  selectableTemplates: [template(1, true), template(2, false)],
  currentAgenda: [],
  ...overrides,
});

const applyResponse = {
  visitInstanceId: 501,
  agendaTemplateId: 1,
  count: 1,
  requestVisitType: 'CAMPUS_TOUR' as const,
  templateVisitType: 'CAMPUS_TOUR' as const,
  visitTypeMismatch: false,
  items: [],
  message: 'Áp dụng thành công',
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe('AgendaSetupPanel — auto-apply on a fresh instance', () => {
  it('applies the default template automatically, with no click, when there is no agenda yet', async () => {
    getSetupForInstance.mockResolvedValue(setup());
    // A deferred promise so the in-flight loading state is observable deterministically, instead of
    // racing an already-resolved mock.
    let resolveApply!: (v: typeof applyResponse) => void;
    apply.mockImplementation(() => new Promise(resolve => { resolveApply = resolve; }));
    const onApplied = vi.fn();

    render(<AgendaSetupPanel visitInstanceId={501} onApplied={onApplied} />);

    await waitFor(() => expect(apply).toHaveBeenCalledWith({
      visitInstanceId: 501, agendaTemplateId: 1, replaceExisting: false,
    }));
    // While the request is in flight it shows a loading notice, never the manual dropdown/button —
    // the host is never asked to click anything for the obvious case.
    await screen.findByText(/Đang áp dụng mẫu Agenda mặc định/);
    expect(screen.queryByRole('button', { name: /Áp dụng/ })).not.toBeInTheDocument();

    resolveApply(applyResponse);
    await waitFor(() => expect(onApplied).toHaveBeenCalledTimes(1));
  });

  it('does NOT auto-apply when the instance already has an agenda — falls back to the manual picker', async () => {
    getSetupForInstance.mockResolvedValue(setup({ hasExistingAgenda: true }));

    render(<AgendaSetupPanel visitInstanceId={501} />);

    await screen.findByRole('button', { name: 'Áp dụng' });
    expect(apply).not.toHaveBeenCalled();
  });

  it('does NOT auto-apply for a viewer without apply rights (canApply=false)', async () => {
    getSetupForInstance.mockResolvedValue(setup({ canApply: false }));

    render(<AgendaSetupPanel visitInstanceId={501} />);

    await screen.findByText('Chọn mẫu');
    expect(apply).not.toHaveBeenCalled();
  });

  it('does NOT auto-apply when there is no template to pre-select', async () => {
    getSetupForInstance.mockResolvedValue(setup({
      selectableTemplates: [], defaultTemplateId: null,
    }));

    render(<AgendaSetupPanel visitInstanceId={501} />);

    await screen.findByText(/Chưa có mẫu agenda khả dụng/);
    expect(apply).not.toHaveBeenCalled();
  });

  it('only auto-applies once per instance — a later manual re-open never re-triggers it', async () => {
    getSetupForInstance.mockResolvedValue(setup());
    apply.mockResolvedValue(applyResponse);
    const onApplied = vi.fn();

    const { rerender } = render(<AgendaSetupPanel visitInstanceId={501} onApplied={onApplied} />);
    await waitFor(() => expect(apply).toHaveBeenCalledTimes(1));

    // Simulate the parent re-opening the panel after the agenda now exists (post-apply state).
    getSetupForInstance.mockResolvedValue(setup({ hasExistingAgenda: true }));
    rerender(<AgendaSetupPanel visitInstanceId={501} onApplied={onApplied} key="reopen" />);

    await screen.findByRole('button', { name: 'Áp dụng' });
    expect(apply).toHaveBeenCalledTimes(1);
  });
});
