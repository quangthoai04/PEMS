#!/usr/bin/env node
/**
 * NP-06 Phase F — regression guard for the "PEMS = Light only" policy.
 *
 * `src/index.css` redefines Tailwind's `dark` variant to `.dark`, so the `dark:*` classes that
 * already exist are dormant rather than dangerous. The risk is the NEXT one: a component that gets
 * a dark background and border but no dark text/hover/disabled/form styling, added long after the
 * person who wrote this has forgotten the rule.
 *
 * So this is an ALLOWLIST, not a ban. The files below already carried `dark:*` when the policy was
 * adopted and are left alone; any OTHER file that grows one fails the audit and has to be looked at
 * by a reviewer (either finish the dark styling properly, or drop it).
 *
 * Usage:  node scripts/audit-dark-mode.mjs        (exit 1 on a new offender)
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dirname } from 'node:path';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const SRC = join(ROOT, 'src');

/** Files that already used `dark:*` when the Light-only policy landed (2026-08-14). Do not grow. */
const ALLOWLIST = new Set([
  'src/features/visit-request/components/SearchMatchContexts.tsx',
  'src/features/visit-request/components/VisitAmendmentPanel.tsx',
  'src/features/visit-request/components/VisitAmendmentSubmitModal.tsx',
  'src/features/visit-request/components/VisitSafeEditModal.tsx',
  'src/pages/CampusDetailVisitPage.tsx',
  'src/pages/identity/VisitContactInvitationPage.tsx',
]);

/** Tailwind's dark variant in a className, e.g. `dark:bg-slate-900`. */
const DARK_VARIANT = /(?:^|[\s"'`{])dark:[a-z0-9[\]/\-.:%#()]+/i;

const EXTENSIONS = ['.ts', '.tsx', '.css'];

function walk(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      if (entry === 'node_modules' || entry === 'dist') continue;
      walk(full, out);
    } else if (EXTENSIONS.some(ext => entry.endsWith(ext))) {
      out.push(full);
    }
  }
  return out;
}

const offenders = [];
for (const file of walk(SRC)) {
  const rel = relative(ROOT, file).split(sep).join('/');
  if (ALLOWLIST.has(rel)) continue;
  const text = readFileSync(file, 'utf8');
  const hits = text
    .split('\n')
    .map((line, i) => ({ line: i + 1, text: line }))
    .filter(l => DARK_VARIANT.test(l.text));
  if (hits.length > 0) offenders.push({ rel, hits });
}

if (offenders.length === 0) {
  console.log(`✓ dark-mode audit: no new dark:* outside the ${ALLOWLIST.size}-file allowlist.`);
  process.exit(0);
}

console.error('✗ dark-mode audit: PEMS renders Light only — new dark:* classes found.\n');
for (const { rel, hits } of offenders) {
  console.error(`  ${rel}`);
  for (const h of hits.slice(0, 5)) console.error(`    ${h.line}: ${h.text.trim().slice(0, 120)}`);
  if (hits.length > 5) console.error(`    … and ${hits.length - 5} more`);
}
console.error(
  '\nEither remove the dark:* classes, or (if a full dark treatment — background, text, border,' +
  '\nhover/focus, disabled AND form controls — is genuinely intended) add the file to ALLOWLIST in' +
  '\nscripts/audit-dark-mode.mjs so a reviewer signs it off.');
process.exit(1);
