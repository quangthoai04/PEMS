import { describe, expect, it } from 'vitest';
import {
  resolveVisitRowRoutes,
  v2EditPath,
  v2ResubmitPath,
} from '../utils/visitVersionRouting';

describe('visitVersionRouting (Pure V2)', () => {
  it('routes every request to the v2 detail/edit/resubmit screens', () => {
    const routes = resolveVisitRowRoutes(42);
    expect(routes.edit).toBe(v2EditPath(42));
    expect(routes.resubmit).toBe(v2ResubmitPath(42));
    expect(routes.detailRoute).toBe('/dashboard/visit/v2/42');
  });

  it('does not depend on a form-version argument — there is exactly one version', () => {
    // The function takes only the id now; the mixed flag and the retired form_schema_version have no
    // say in routing, so a mixed and a uniform request resolve identically.
    expect(resolveVisitRowRoutes(7).detailRoute).toBe('/dashboard/visit/v2/7');
    expect(resolveVisitRowRoutes(7)).toEqual(resolveVisitRowRoutes(7));
  });

  it('never routes to the retired unsupported-version page', () => {
    const routes = resolveVisitRowRoutes(9);
    expect(routes.edit).not.toContain('unsupported-version');
    expect(routes.resubmit).not.toContain('unsupported-version');
    expect(routes.detailRoute).not.toContain('unsupported-version');
  });
});
