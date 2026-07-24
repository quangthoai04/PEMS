import { describe, expect, it } from 'vitest';
import { normalizeApiError, VISIT_FORM_DETAIL_MISSING } from '../normalizeApiError';

const httpError = (
  status: number,
  data?: unknown,
  headers?: Record<string, string>,
) =>
  Object.assign(new Error(`Request failed with status code ${status}`), {
    isAxiosError: true,
    code: 'ERR_BAD_RESPONSE',
    response: { status, data, headers: headers ?? {} },
    config: {},
  });

const networkError = () =>
  Object.assign(new Error('Network Error'), { isAxiosError: true, code: 'ERR_NETWORK', request: {} });

const timeoutError = () =>
  Object.assign(new Error('timeout of 1000ms exceeded'), { isAxiosError: true, code: 'ECONNABORTED', request: {} });

describe('normalizeApiError — HTTP status → category', () => {
  it('maps each status to a distinct category', () => {
    expect(normalizeApiError(httpError(403)).category).toBe('forbidden');
    expect(normalizeApiError(httpError(404)).category).toBe('notFound');
    expect(normalizeApiError(httpError(409)).category).toBe('conflict');
    expect(normalizeApiError(httpError(422)).category).toBe('validation');
    expect(normalizeApiError(httpError(400)).category).toBe('validation');
    expect(normalizeApiError(httpError(500)).category).toBe('server');
    expect(normalizeApiError(httpError(503)).category).toBe('server');
  });

  it('flags the Pure V2 "form detail missing" 409 separately from a generic conflict', () => {
    const missing = normalizeApiError(httpError(409, { errorCode: VISIT_FORM_DETAIL_MISSING }));
    expect(missing.category).toBe('conflict');
    expect(missing.isVisitFormDetailMissing).toBe(true);
    expect(missing.errorCode).toBe(VISIT_FORM_DETAIL_MISSING);

    const generic = normalizeApiError(httpError(409, { errorCode: 'SOME_OTHER_CONFLICT' }));
    expect(generic.category).toBe('conflict');
    expect(generic.isVisitFormDetailMissing).toBe(false);
    expect(generic.errorCode).toBe('SOME_OTHER_CONFLICT');
  });
});

describe('normalizeApiError — no response reached us', () => {
  it('keeps network and timeout DISTINCT (and neither is an HTTP error)', () => {
    const net = normalizeApiError(networkError());
    expect(net.category).toBe('network');
    expect(net.status).toBeUndefined();
    expect(net.message.toLowerCase()).toContain('reach the server');

    const to = normalizeApiError(timeoutError());
    expect(to.category).toBe('timeout');
    expect(to.status).toBeUndefined();
    expect(to.message.toLowerCase()).toContain('too long');
  });

  it('classifies an unrecognizable throw as unknown, not empty', () => {
    expect(normalizeApiError(new Error('weird')).category).toBe('unknown');
    expect(normalizeApiError(null).category).toBe('unknown');
  });
});

describe('normalizeApiError — diagnostics', () => {
  it('extracts a correlation id from a response header', () => {
    const err = httpError(500, { message: 'boom' }, { 'x-correlation-id': 'abc-123' });
    expect(normalizeApiError(err).correlationId).toBe('abc-123');
  });

  it('extracts a correlation id from the response body traceId', () => {
    const err = httpError(500, { message: 'boom', traceId: 'trace-9' });
    expect(normalizeApiError(err).correlationId).toBe('trace-9');
  });

  it('masks secrets that leak into an error message', () => {
    const err = httpError(500, { message: 'client_secret=hunter2 leaked into the log' });
    const msg = normalizeApiError(err).message;
    expect(msg).not.toContain('hunter2');
    expect(msg).toContain('[hidden]');
  });
});
