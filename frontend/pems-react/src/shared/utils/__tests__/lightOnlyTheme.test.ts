import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * NP-06 — PEMS renders Light on every machine.
 *
 * <p>Tailwind v4's stock `dark:` variant keys on `prefers-color-scheme`, so every `dark:*` class in
 * the codebase fired on any machine whose OS or browser was set to dark. Most PEMS components only
 * ever got a dark BACKGROUND — no dark text, hover, disabled or form styling — so the result was a
 * half-light/half-dark UI that differed from machine to machine and, in places, lost its contrast
 * entirely.</p>
 *
 * <p>Two lines in `index.css` fix that, and they are the kind of line somebody removes while tidying
 * imports, so they are pinned here rather than left to a manual check on a dark-themed laptop:</p>
 *
 * <ul>
 *   <li>`@custom-variant dark (&:where(.dark, .dark *))` — `dark:*` becomes opt-in. The app never
 *   adds `.dark`, so the existing classes go dormant instead of firing.</li>
 *   <li>`:root { color-scheme: light }` — the same for the controls the browser paints itself
 *   (input, select, textarea, date pickers, scrollbars).</li>
 * </ul>
 *
 * <p>These are asserted against the FILE rather than a rendered page because jsdom applies no CSS —
 * a DOM-level test here would pass whatever the stylesheet said.</p>
 */
describe('NP-06: the app is Light-only', () => {
  const css = readFileSync(join(__dirname, '../../../index.css'), 'utf8');

  it('redefines the dark variant so it is opt-in rather than OS-driven', () => {
    expect(css).toMatch(/@custom-variant\s+dark\s*\(\s*&:where\(\.dark,\s*\.dark\s*\*\)\s*\)/);
  });

  it('declares the light color-scheme for native controls', () => {
    expect(css).toMatch(/:root\s*\{[^}]*color-scheme:\s*light/);
  });

  it('declares the dark variant AFTER importing tailwind, or the override does not apply', () => {
    const tailwind = css.indexOf('@import "tailwindcss"');
    const variant = css.indexOf('@custom-variant dark');
    expect(tailwind).toBeGreaterThanOrEqual(0);
    expect(variant).toBeGreaterThan(tailwind);
  });

  it('never adds the .dark class to the document itself', () => {
    // The variant above is harmless only while nothing opts in. A theme toggle would need a real
    // design system and a full audit of the partially-dark components first.
    const src = readFileSync(join(__dirname, '../../../App.tsx'), 'utf8');
    expect(src).not.toMatch(/classList\.(add|toggle)\(\s*['"]dark['"]/);
  });
});
