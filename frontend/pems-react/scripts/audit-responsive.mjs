#!/usr/bin/env node
/**
 * System-wide responsive audit (docs/CanhIter3FixBug/GopYCQuyen/PEMS_System_Wide_Responsive_UI_Audit_and_Fix_Plan.md Sec45/46).
 *
 * Static regex scan for high-risk responsive patterns across src/**\/*.tsx and src/**\/*.ts. This is
 * a CANDIDATE report, not a pass/fail gate -- a candidate must be read in context and classified as
 * SAFE_KEEP / REVIEWED_KEEP / FIX_REQUIRED / INTENTIONAL_SCROLLER before anything is changed.
 *
 * Usage:  node scripts/audit-responsive.mjs               (summary counts by pattern)
 *         node scripts/audit-responsive.mjs --by-file      (per-file worklist, sorted by hit count)
 *         node scripts/audit-responsive.mjs --json         (machine-readable, for batching fix work)
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dirname } from 'node:path';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const SRC = join(ROOT, 'src');

const PATTERNS = [
  { key: 'viewport-height', re: /\bh-screen\b|\bmax-h-screen\b|\bmin-h-screen\b|(?:h|max-h|min-h)-\[[0-9]+(?:\.[0-9]+)?vh\]/g },
  { key: 'fixed-width', re: /\b(?:w|min-w|max-w)-\[[0-9]+(?:\.[0-9]+)?px\]/g },
  { key: 'content-hiding', re: /\bw-max\b|\bmin-w-max\b|\bwhitespace-nowrap\b|\btruncate\b/g },
  { key: 'overflow-hidden', re: /\boverflow-hidden\b|\boverflow-x-hidden\b/g },
  { key: 'base-multicol-grid', re: /(?<![a-z0-9]:)(?<!-)\bgrid-cols-[2-9]\b/g },
  { key: 'position', re: /(?:^|[\s"'`{])(?:fixed|absolute|sticky)(?:$|[\s"'`}])/g },
  { key: 'tiny-text', re: /\btext-\[(?:9|10|11)px\]/g },
];

const EXCLUDE_DIRS = new Set(['node_modules', 'dist', '.git']);

function walk(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    if (EXCLUDE_DIRS.has(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      walk(full, out);
    } else if (entry.endsWith('.tsx') || entry.endsWith('.ts')) {
      out.push(full);
    }
  }
  return out;
}

const files = walk(SRC);
const byPattern = Object.fromEntries(PATTERNS.map(p => [p.key, []]));
const byFile = {};

for (const file of files) {
  const rel = relative(ROOT, file).split(sep).join('/');
  const text = readFileSync(file, 'utf8');
  const lines = text.split('\n');
  for (const { key, re } of PATTERNS) {
    lines.forEach((lineText, i) => {
      re.lastIndex = 0;
      if (re.test(lineText)) {
        byPattern[key].push({ rel, line: i + 1, text: lineText.trim().slice(0, 140) });
        byFile[rel] ??= {};
        byFile[rel][key] = (byFile[rel][key] ?? 0) + 1;
      }
    });
  }
}

const args = process.argv.slice(2);

if (args.includes('--json')) {
  console.log(JSON.stringify({ scannedFiles: files.length, byPattern, byFile }, null, 2));
  process.exit(0);
}

if (args.includes('--by-file')) {
  const sorted = Object.entries(byFile).sort((a, b) => {
    const totalA = Object.values(a[1]).reduce((s, n) => s + n, 0);
    const totalB = Object.values(b[1]).reduce((s, n) => s + n, 0);
    return totalB - totalA;
  });
  console.log(`Responsive audit -- ${sorted.length} files with >=1 candidate hit (of ${files.length} scanned)\n`);
  for (const [rel, counts] of sorted) {
    const parts = Object.entries(counts).map(([k, n]) => `${k}=${n}`).join(' ');
    console.log(`  ${rel}  (${parts})`);
  }
  process.exit(0);
}

console.log(`Responsive audit -- ${files.length} .ts/.tsx files scanned\n`);
for (const { key } of PATTERNS) {
  const hits = byPattern[key];
  const fileCount = new Set(hits.map(h => h.rel)).size;
  console.log(`-- ${key}: ${hits.length} occurrences in ${fileCount} files`);
}
console.log('\nRun with --by-file for a per-file worklist, or --json for machine-readable output.');
console.log('This is a candidate list, not a fail gate -- triage each occurrence in context:');
console.log('SAFE_KEEP / REVIEWED_KEEP / FIX_REQUIRED / INTENTIONAL_SCROLLER.');
