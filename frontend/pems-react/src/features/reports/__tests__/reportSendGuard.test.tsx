/**
 * G6.6-C3 — pressing "Gửi" repeatedly must not send the report twice.
 *
 * The screens tracked one in-flight id for a whole table. That covered the obvious case (the row's own
 * button is disabled while it sends) and missed a real one: starting a send on a second row overwrote
 * the id, and the first request's `finally` then cleared it — so the second row's button re-enabled while
 * its own request was still running, and the next press sent that report again.
 *
 * Two layers: the guard itself, and the screen actually using it. A hook test alone would still pass if a
 * page kept its old state variable.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { renderHook } from '@testing-library/react';
import { useGuardedSend } from '../hooks/useGuardedSend';

// ── 1. The guard ─────────────────────────────────────────────────────────────

describe('useGuardedSend', () => {
  /** A promise the test releases by hand, so "in flight" is a state the test controls. */
  const deferred = () => {
    let resolve!: () => void;
    let reject!: (e: unknown) => void;
    const promise = new Promise<void>((res, rej) => { resolve = res; reject = rej; });
    return { promise, resolve, reject };
  };

  it('runs one action per id while that id is in flight', async () => {
    const { result } = renderHook(() => useGuardedSend<number>());
    const action = vi.fn().mockReturnValue(deferred().promise);

    await act(async () => {
      void result.current.send(1, action);
      void result.current.send(1, action);
      void result.current.send(1, action);
    });

    expect(action).toHaveBeenCalledTimes(1);
    expect(result.current.isSending(1)).toBe(true);
  });

  it('reports which ids are sending, independently', async () => {
    const { result } = renderHook(() => useGuardedSend<number>());
    const first = deferred();
    const second = deferred();

    await act(async () => {
      void result.current.send(1, () => first.promise);
      void result.current.send(2, () => second.promise);
    });

    expect(result.current.isSending(1)).toBe(true);
    expect(result.current.isSending(2)).toBe(true);

    // The defect: finishing one row must not release the other.
    await act(async () => { first.resolve(); await first.promise; });
    expect(result.current.isSending(1)).toBe(false);
    expect(result.current.isSending(2)).toBe(true);

    await act(async () => { second.resolve(); await second.promise; });
    expect(result.current.isSending(2)).toBe(false);
  });

  it('allows the same id again once its send finished', async () => {
    const { result } = renderHook(() => useGuardedSend<number>());
    const action = vi.fn().mockResolvedValue(undefined);

    await act(async () => { await result.current.send(1, action); });
    await act(async () => { await result.current.send(1, action); });

    expect(action).toHaveBeenCalledTimes(2);
    expect(result.current.isSending(1)).toBe(false);
  });

  it('clears the flag when the action rejects, so a refused send can be retried', async () => {
    const { result } = renderHook(() => useGuardedSend<number>());

    await act(async () => {
      await expect(result.current.send(1, () => Promise.reject(new Error('refused')))).rejects.toThrow('refused');
    });

    expect(result.current.isSending(1)).toBe(false);
  });

  it('tells the caller whether the action ran', async () => {
    const { result } = renderHook(() => useGuardedSend<number>());
    const held = deferred();
    let second: boolean | undefined;

    await act(async () => {
      void result.current.send(1, () => held.promise);
      second = await result.current.send(1, () => Promise.resolve());
    });

    expect(second).toBe(false);
    await act(async () => { held.resolve(); await held.promise; });
  });
});

// ── 2. The screen using it ───────────────────────────────────────────────────

const sendDeptLeaderPersonnelReport = vi.fn();
const getDeptLeaderReportV2 = vi.fn();

vi.mock('../api/reportsApi', () => ({
  reportsApi: {
    getDeptLeaderReportV2: (...a: unknown[]) => getDeptLeaderReportV2(...a),
    sendDeptLeaderPersonnelReport: (...a: unknown[]) => sendDeptLeaderPersonnelReport(...a),
    getDeptLeaderInvoiceItemsV2: vi.fn().mockResolvedValue([]),
    exportDeptLeaderReportV2: vi.fn(),
  },
}));
vi.mock('react-hot-toast', () => ({ default: { success: vi.fn(), error: vi.fn() } }));
vi.mock('recharts', () => {
  const Stub = ({ children }: { children?: React.ReactNode }) => <div>{children}</div>;
  return {
    CartesianGrid: Stub, Legend: Stub, Line: Stub, LineChart: Stub,
    ResponsiveContainer: Stub, Tooltip: Stub, XAxis: Stub, YAxis: Stub,
  };
});
vi.mock('../../../pages/dashboard/departments/TaskHandoverModal', () => ({
  TaskHandoverModal: () => null,
}));

import { DeptReportManagement } from '../../../pages/dashboard/reports/DeptReportManagement';

const person = (userId: number, fullName: string) => ({
  userId, fullName, email: `u${userId}@fpt.edu.vn`, role: 'DEPT_STAFF' as const,
  taskCount: 3, totalHours: 8, feedbackAverage: 4.5, feedbackCount: 2, declinedCount: 0,
});

const report = {
  generatedAt: '2026-07-29T10:00:00',
  departmentName: 'Phòng CTSV',
  preset: 'THIS_YEAR' as const,
  fromDate: '2026-01-01',
  toDate: '2026-12-31',
  tasks: {
    totalTasks: 5, completed: 4, rejected: 0, notCompleted: 1,
    feedbackCount: 2, feedbackTotalStars: 9, feedbackAverage: 4.5,
    trendGranularity: 'MONTH' as const, trend: [],
  },
  personnel: {
    totalStaff: 2, averageFeedback: 4.5,
    rows: [person(11, 'Nguyễn Văn A'), person(22, 'Trần Thị B')],
  },
  expenses: { totalAmount: 0, rows: [] },
};

const sendButtonFor = (fullName: string) =>
  screen.getByTitle(`Gửi báo cáo hiệu suất qua email cho ${fullName}`);

beforeEach(() => {
  vi.clearAllMocks();
  getDeptLeaderReportV2.mockResolvedValue(report);
});

describe('DeptReportManagement — sending a personnel report', () => {
  it('sends once however many times the button is pressed', async () => {
    let release!: () => void;
    sendDeptLeaderPersonnelReport.mockReturnValue(new Promise<{ message: string }>(res => {
      release = () => res({ message: 'ok' });
    }));

    render(<DeptReportManagement />);
    const button = await waitFor(() => sendButtonFor('Nguyễn Văn A'));

    fireEvent.click(button);
    fireEvent.click(button);
    fireEvent.click(button);

    expect(sendDeptLeaderPersonnelReport).toHaveBeenCalledTimes(1);
    expect(sendButtonFor('Nguyễn Văn A')).toBeDisabled();

    await act(async () => { release(); });
    await waitFor(() => expect(sendButtonFor('Nguyễn Văn A')).toBeEnabled());
  });

  it('keeps each row disabled until its own request finishes', async () => {
    const releases: Array<() => void> = [];
    sendDeptLeaderPersonnelReport.mockImplementation(() =>
      new Promise<{ message: string }>(res => { releases.push(() => res({ message: 'ok' })); }));

    render(<DeptReportManagement />);
    await waitFor(() => sendButtonFor('Nguyễn Văn A'));

    fireEvent.click(sendButtonFor('Nguyễn Văn A'));
    fireEvent.click(sendButtonFor('Trần Thị B'));
    expect(sendDeptLeaderPersonnelReport).toHaveBeenCalledTimes(2);

    // Finishing the first row must not re-enable the second, which is still sending. With one shared
    // id it did, and the next click on B sent B's report a second time.
    await act(async () => { releases[0](); });
    await waitFor(() => expect(sendButtonFor('Nguyễn Văn A')).toBeEnabled());
    expect(sendButtonFor('Trần Thị B')).toBeDisabled();

    fireEvent.click(sendButtonFor('Trần Thị B'));
    expect(sendDeptLeaderPersonnelReport).toHaveBeenCalledTimes(2);

    await act(async () => { releases[1](); });
    await waitFor(() => expect(sendButtonFor('Trần Thị B')).toBeEnabled());
  });

  it('re-enables the row after a refused send so it can be corrected and retried', async () => {
    sendDeptLeaderPersonnelReport.mockRejectedValue({ response: { data: { message: 'Gửi thất bại' } } });

    render(<DeptReportManagement />);
    const button = await waitFor(() => sendButtonFor('Nguyễn Văn A'));

    fireEvent.click(button);
    await waitFor(() => expect(sendButtonFor('Nguyễn Văn A')).toBeEnabled());
    expect(sendDeptLeaderPersonnelReport).toHaveBeenCalledTimes(1);
  });
});
