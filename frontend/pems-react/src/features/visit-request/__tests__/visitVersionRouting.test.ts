import { describe, expect, it } from 'vitest';
import {
  isPerCampusV2,
  resolveVisitRowRoutes,
  v2EditPath,
  v2ResubmitPath,
} from '../utils/visitVersionRouting';

describe('visitVersionRouting', () => {
  it('treats version >= 2 as per-campus v2', () => {
    expect(isPerCampusV2(2)).toBe(true);
    expect(isPerCampusV2(3)).toBe(true);
  });

  it('treats version 1 / missing as invalid/legacy', () => {
    expect(isPerCampusV2(1)).toBe(false);
    expect(isPerCampusV2(null)).toBe(false);
    expect(isPerCampusV2(undefined)).toBe(false);
  });

  it('routes a v2 request (non-mixed) straight to the v2 UI — no waiting for a 409', () => {
    const routes = resolveVisitRowRoutes(42, 2);
    expect(routes.isV2).toBe(true);
    expect(routes.edit).toBe(v2EditPath(42));
    expect(routes.resubmit).toBe(v2ResubmitPath(42));
    expect(routes.detailRoute).toBe('/dashboard/visit/v2/42');
  });

  it('routes a v2 mixed request the same way (v2), not the flat modal', () => {
    // Mixed is irrelevant to routing — only the schema version matters.
    const routes = resolveVisitRowRoutes(7, 2);
    expect(routes.isV2).toBe(true);
    expect(routes.detailRoute).toBe('/dashboard/visit/v2/7');
  });

  it('routes a v1 request to unsupported error routes', () => {
    const routes = resolveVisitRowRoutes(5, 1);
    expect(routes.isV2).toBe(false);
    expect(routes.edit).toBe('/dashboard/visit/unsupported-version');
    expect(routes.resubmit).toBe('/dashboard/visit/unsupported-version');
    expect(routes.detailRoute).toBe('/dashboard/visit/unsupported-version');
  });

  it('routes to unsupported error routes when the version is missing', () => {
    const routes = resolveVisitRowRoutes(9, undefined);
    expect(routes.isV2).toBe(false);
    expect(routes.detailRoute).toBe('/dashboard/visit/unsupported-version');
  });
});
