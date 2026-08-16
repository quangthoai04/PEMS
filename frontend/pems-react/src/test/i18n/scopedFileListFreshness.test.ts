/**
 * i18n gate — Phase 20 (hardcode-scan file-list freshness).
 *
 * `guestVisitorHardcodeScan.test.ts` scans a manually curated `SCOPED_FILES` list. A hand-
 * maintained list has no way to know when a NEW component becomes Guest/Visitor-reachable (a new
 * route, a newly-imported child component) — nothing would ever flag it as needing triage.
 *
 * This gate closes that gap WITHOUT a full AST dependency analyzer: it derives, from `App.tsx` +
 * `dashboardRouteAccess.ts`, every source file actually reachable (via static `import`/dynamic
 * `import()` statements, followed recursively) from a Guest route or a dashboard route whose
 * `allowedRoles` includes VISITOR. Every file in that derived closure must be accounted for —
 * either already in `SCOPED_FILES` (the hardcode scan covers it) or in
 * `ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS` (proven, with code evidence in that file, to never
 * actually render for VISITOR despite the route guard technically admitting the role). A file in
 * neither list fails this test — it is a real, untriaged gap: either add it to `SCOPED_FILES` (if
 * it belongs in the hardcode scan) or to the exclusion list (with the code evidence proving it's
 * not actually Visitor-reachable).
 *
 * Heuristic by design (regex-based import following, not a real module resolver) — see the
 * `resolveImport` limitations noted inline. It errs toward over-inclusion (flagging a file that
 * turns out to be fine) rather than silently missing one.
 */
import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { SCOPED_FILES, ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS } from './guestVisitorHardcodeScan.test';

const SRC_ROOT = path.resolve(__dirname, '../../');
const APP_TSX = path.join(SRC_ROOT, 'App.tsx');
const ROUTE_ACCESS_TS = path.join(SRC_ROOT, 'shared/auth/dashboardRouteAccess.ts');

function readSrc(relPath: string): string {
  return fs.readFileSync(path.join(SRC_ROOT, relPath), 'utf-8');
}

// ── Step 1: figure out which dashboard routeKeys admit VISITOR ────────────────────────────────

function visitorAllowedRouteKeys(): Set<string> {
  const src = readSrc('shared/auth/dashboardRouteAccess.ts');

  // Named role-group constants, e.g. `const BUSINESS_ROLES: readonly EffectiveRole[] = [...]`.
  const constants = new Map<string, boolean>();
  for (const m of src.matchAll(/const (\w+):\s*readonly EffectiveRole\[\]\s*=\s*(\[[^\]]*\]|[\w.]+)/g)) {
    const [, name, value] = m;
    if (value.startsWith('[')) {
      constants.set(name, value.includes("'VISITOR'"));
    } else {
      // e.g. `ADMIN_ONLY = ['ADMIN']` referencing nothing further; unresolved refs default false.
      constants.set(name, constants.get(value) ?? false);
    }
  }

  const allowed = new Set<string>();
  for (const m of src.matchAll(/key:\s*'([A-Z_]+)'[\s\S]*?allowedRoles:\s*(\[[^\]]*\]|[\w.]+)/g)) {
    const [, key, value] = m;
    const isAllowed = value.startsWith('[') ? value.includes("'VISITOR'") : (constants.get(value) ?? false);
    if (isAllowed) allowed.add(key);
  }
  return allowed;
}

// ── Step 2: entry-point components reachable by Guest or a Visitor-allowed dashboard route ────

function findEntryComponents(): string[] {
  const app = readSrc('App.tsx');
  const visitorKeys = visitorAllowedRouteKeys();
  const names = new Set<string>();

  // Public/Guest routes: any `<Route path="/xxx" ... element={<Component` before the dashboard
  // parent route starts. Handles both `<Component ... />` and `<ProtectedRoute><Component`.
  const dashboardStart = app.indexOf(`<Route path="/dashboard"`);
  const publicSection = dashboardStart === -1 ? app : app.slice(0, dashboardStart);
  for (const m of publicSection.matchAll(/<Route\s[^>]*element=\{<(?:ProtectedRoute>)?<?(\w+)/g)) {
    names.add(m[1]);
  }
  // A couple of routes render <ProtectedRoute><X /></ProtectedRoute> with X on the same line —
  // the pattern above already captures X via the optional `<ProtectedRoute>` group; also catch
  // `element={<ProtectedRoute><ErrorBoundary><X` (dashboard parent itself is handled separately).

  // Dashboard child routes: `<Route path="..." element={<RouteAccessGuard routeKey="KEY">...<Component`.
  const dashboardSection = dashboardStart === -1 ? '' : app.slice(dashboardStart);
  for (const m of dashboardSection.matchAll(/routeKey="([A-Z_]+)">\s*(?:\{[^}]*\?\s*)?<?(\w+)/g)) {
    const [, key, comp] = m;
    if (visitorKeys.has(key) && /^[A-Z]/.test(comp)) names.add(comp);
  }

  return [...names];
}

// ── Step 3: resolve a component name to its source file via App.tsx's own imports ─────────────

function resolveImportPath(componentName: string): string | null {
  const app = readSrc('App.tsx');
  const re1 = new RegExp(`import\\s+${componentName}\\s+from\\s+['"](\\.[^'"]+)['"]`);
  const re2 = new RegExp(`import\\s*\\{[^}]*\\b${componentName}\\b[^}]*\\}\\s*from\\s+['"](\\.[^'"]+)['"]`);
  const m = app.match(re1) ?? app.match(re2);
  if (!m) return null;
  return path.normalize(path.join(path.dirname(APP_TSX), m[1])).replace(/\\/g, '/');
}

function existingFileFor(basePathNoExt: string): string | null {
  for (const candidate of [
    `${basePathNoExt}.tsx`,
    `${basePathNoExt}.ts`,
    path.join(basePathNoExt, 'index.tsx'),
    path.join(basePathNoExt, 'index.ts'),
  ]) {
    if (fs.existsSync(candidate)) return candidate.replace(/\\/g, '/');
  }
  return null;
}

// ── Step 4: recursively follow relative imports from each entry file ──────────────────────────
//
// A file in ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS is a proven closure BOUNDARY, not just a file to
// skip reporting on: e.g. VisitProcess.tsx is reachable by the route guard but structurally
// Staff/Host tooling, and its OWN imports (expense panels, logistics sections, email composer...)
// are exactly as Staff-only as it is. Recursing into them would flood this gate with hundreds of
// files that are only reachable BECAUSE they sit inside an already-excluded parent's private
// render tree, not because a Visitor can ever reach them directly. The excluded file itself is
// still recorded as visited (so it doesn't get re-queued as an entry point elsewhere) — only its
// own import edges are not followed.
function collectImportClosure(entryAbsPaths: string[], boundaryAbsPaths: Set<string>): Set<string> {
  const visited = new Set<string>();
  const queue = [...entryAbsPaths];

  while (queue.length > 0) {
    const abs = queue.pop()!;
    if (visited.has(abs)) continue;
    visited.add(abs);
    if (boundaryAbsPaths.has(abs)) continue; // record it, but do not follow its imports
    if (!fs.existsSync(abs)) continue;

    const content = fs.readFileSync(abs, 'utf-8');
    const importRe = /(?:from\s+|import\()\s*['"](\.[^'"]+)['"]/g;
    for (const m of content.matchAll(importRe)) {
      const resolved = existingFileFor(path.normalize(path.join(path.dirname(abs), m[1])));
      if (resolved && !visited.has(resolved)) queue.push(resolved);
    }
  }
  return visited;
}

describe('Guest/Visitor scoped-file-list freshness (Phase 20 gate)', () => {
  it('every statically-reachable Guest/Visitor file is in SCOPED_FILES or ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS', () => {
    const entryComponents = findEntryComponents();
    expect(entryComponents.length, 'Found zero entry components — the regex against App.tsx likely broke; fix before trusting this gate.').toBeGreaterThan(5);

    const entryAbsPaths = entryComponents
      .map((name) => resolveImportPath(name))
      .filter((p): p is string => p !== null)
      .map((p) => existingFileFor(p))
      .filter((p): p is string => p !== null);

    const boundaryAbsPaths = new Set(
      Object.keys(ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS).map((rel) => path.join(SRC_ROOT, rel).replace(/\\/g, '/')),
    );
    const closure = collectImportClosure(entryAbsPaths, boundaryAbsPaths);
    const closureRelToSrc = [...closure]
      .map((abs) => path.relative(SRC_ROOT, abs).replace(/\\/g, '/'))
      .filter((rel) => rel.endsWith('.ts') || rel.endsWith('.tsx'));

    const triaged = new Set([...SCOPED_FILES, ...Object.keys(ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS)]);
    const untriaged = closureRelToSrc.filter((rel) => !triaged.has(rel)).sort();

    expect(
      untriaged,
      `New Guest/Visitor-reachable file(s) not yet triaged — add to SCOPED_FILES (guestVisitorHardcodeScan.test.ts) if it belongs in the hardcode scan, or to ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS with code evidence if it's proven never-Visitor-reachable:\n${untriaged.map((f) => `  ${f}`).join('\n')}`,
    ).toEqual([]);
  });
});
