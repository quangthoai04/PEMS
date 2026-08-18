#!/usr/bin/env node
/**
 * Regression guard for docs/CanhIter3FixBug/GopYCQuyen/PEMS_Display_Typography_Normalization_Plan.md
 *
 * Companion to audit-form-typography.mjs, but for DISPLAY (read-only) text instead of form-control
 * values. The tag surface here is much wider (span/p/div/li/dt/strong/b) and many of those tags are
 * legitimately bold on purpose (card titles, KPI numbers, status chips, eyebrow labels), so this
 * script does NOT fail the build on every match the way the form-control guard does. Per the plan
 * (Muc 43-45), its job is to classify: SAFE buckets are matched by a documented heuristic tied
 * directly to the plan's own typography contract (Muc 3) and are not reported; everything else is a
 * NEEDS-REVIEW candidate that a human (or an agent doing the audit) must classify by semantic role
 * before deciding to normalize or keep.
 *
 * A review manifest (typography-review-manifest.json, sibling of this script) records the outcome
 * of that manual classification so re-runs are silent once an occurrence has been triaged - the
 * script then acts as a true regression guard: it only flags something as UNREVIEWED when a NEW
 * occurrence appears (new code, or an old line edited back to bold) that isn't in the manifest and
 * doesn't match a SAFE heuristic.
 *
 * Every occurrence lands in exactly one of four states:
 *   SAFE              - matched an auto heuristic tied to the typography contract (eyebrow/badge/kpi)
 *   REVIEWED_KEEP      - manually reviewed, legitimately bold per contract (manifest category "KEEP")
 *   EXCLUDED_WITH_REASON - manually reviewed, out of plan scope (manifest category "EXCLUDED", e.g.
 *                        marketing hero copy per plan Muc 29) - still reviewed, just not normalized
 *   UNREVIEWED         - not yet classified; the close-out gate for this audit is this count === 0
 *
 * Manifest entries key on (file, snippet) rather than (file, line) so line-number drift from
 * unrelated edits elsewhere in a file never desyncs the manifest.
 *
 * Usage:
 *   node scripts/audit-display-typography.mjs            summary counts + first N UNREVIEWED items
 *   node scripts/audit-display-typography.mjs --all       print every UNREVIEWED candidate (no truncation)
 *   node scripts/audit-display-typography.mjs --json      machine-readable dump of all UNREVIEWED candidates
 *   node scripts/audit-display-typography.mjs --report    full 4-state breakdown per file (for the DoD report)
 *   node scripts/audit-display-typography.mjs --prune-manifest
 *                                                          rewrites the manifest, dropping entries whose
 *                                                          (file, snippet) key no longer matches any
 *                                                          occurrence the scan currently produces (the
 *                                                          code moved/changed since the entry was written)
 *                                                          and collapsing exact-duplicate keys to one
 *                                                          entry. Prints what it removed; does not touch
 *                                                          source files.
 */
import { readdirSync, readFileSync, statSync, existsSync, writeFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dirname } from 'node:path';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const SRC = join(ROOT, 'src');
const args = process.argv.slice(2);
const PRINT_ALL = args.includes('--all');
const AS_JSON = args.includes('--json');
const AS_REPORT = args.includes('--report');
const PRUNE_MANIFEST = args.includes('--prune-manifest');

const MANIFEST_PATH = join(ROOT, 'scripts', 'typography-review-manifest.json');
const manifestEntries = existsSync(MANIFEST_PATH)
  ? JSON.parse(readFileSync(MANIFEST_PATH, 'utf8'))
  : [];
const manifest = new Map();
for (const entry of manifestEntries) {
  manifest.set(`${entry.file} ${entry.snippet}`, entry);
}

const EXTENSIONS = ['.ts', '.tsx'];
const WEIGHT_CLASS = /\bfont-(medium|semibold|bold|extrabold|black)\b/;

// 2026-08-18 (2nd pass): the CANDIDATE_TAGS allowlist itself was the bug. Any tag not on that list -
// <a>, <Link>, <tr>, <thead>, <ul>, <summary>, <mark>, <kbd>, framer-motion's <motion.div>, and every
// custom component (<VisitActionButton>, <LogisticsWorkContent>, ...) - was structurally invisible
// regardless of manual-review quality. A repo-wide tag-independent grep found 97 such occurrences;
// manual review turned up 4 real violations (a <tr> that bolded a whole read-only data row via CSS
// inheritance when it conditionally shouldn't, a <ul> wrapper bolding plain checklist text, a
// calendar-event <h4> title inconsistent with its own sibling calendar views, and a shared
// LogisticsWorkContent description component invoked with the wrong weight at one of its three call
// sites). The other 93 were genuinely fine (CTA/nav links, toasts, badges, table headers, class
// constants) and got individual manifest entries instead of a tag-based exemption, because a tag name
// alone doesn't prove intent - only a reviewed reason does.
//
// So there is no tag allowlist anymore. Every occurrence's enclosing tag is found by scanning
// backward from the match to the nearest unclosed `<`, and only a short, deliberately-named set of
// tags is auto-exempt (native heading/label/button/table-header semantics, where the typography
// contract - plan Muc 3 - assigns 500-700 weight by design). Everything else, including custom
// components and `<a>`/`<Link>`/`<tr>`/etc., must clear a SAFE heuristic or a manifest entry, exactly
// like span/p/div always did.

// Native tags that are heading/label/button/table-header by HTML semantics: the typography
// contract (plan Muc 3) explicitly assigns them 500-700 weight, so a weight class here is
// compliance, not a violation. Never flagged. (A `motion.h1` / `motion.button` etc. from
// framer-motion is the same native element under the hood, so the `motion.` prefix is stripped
// before this check.)
const EXEMPT_TAGS = new Set(['h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'button', 'th', 'label', 'legend']);

// Form-control class constants/tags belong to the separate audit-form-typography.mjs (input value
// typography), not this DISPLAY (read-only) typography guard. Reported as their own bucket so the
// two scripts' scope stays legible instead of silently overlapping.
const FORM_TAGS = new Set(['input', 'select', 'textarea', 'option', 'optgroup']);

/** Finds the start index of the `<...` whose opening tag encloses `pos`, scanning backward. Returns
 * -1 when `pos` sits in a bare string/template-literal class constant that isn't attached to any JSX
 * tag at all (e.g. `const labelCls = '...font-bold...'`) - that case still requires manifest review,
 * it is just reported under tag `null` instead of a wrong guess. */
function findEnclosingTagStart(text, pos) {
  let i = pos;
  while (i >= 0) {
    if (text[i] === '<') {
      const between = text.slice(i, pos);
      let depth = 0, quote = null, closed = false;
      for (let j = 0; j < between.length; j++) {
        const c = between[j];
        if (quote) { if (c === '\\') { j++; continue; } if (c === quote) quote = null; continue; }
        if (c === '"' || c === "'" || c === '`') { quote = c; continue; }
        if (c === '{') { depth++; continue; }
        if (c === '}') { depth--; continue; }
        if (depth === 0 && c === '>') { closed = true; break; }
      }
      if (closed) { i--; continue; }
      return i;
    }
    i--;
  }
  return -1;
}

/** Tag name at a known tag-start index (`<Foo` -> `Foo`), with `motion.` stripped since a
 * `motion.h1`/`motion.button` is the same native element under the hood. */
function tagNameAt(text, tagStart) {
  const m = /^<\s*\/?\s*([A-Za-z][A-Za-z0-9._]*)/.exec(text.slice(tagStart, tagStart + 80));
  if (!m) return null;
  return m[1].startsWith('motion.') ? m[1].slice('motion.'.length) : m[1];
}

/** Forward scan from a tag-start to its closing `>`, respecting quotes/braces so a multi-line
 * ternary className (badge color picked by status, KPI size decided by a variant prop, etc.) is
 * captured whole - the SAFE heuristics need the full tag, not just the line the weight class sits
 * on, or a badge whose color classes live in a different ternary branch reads as a false positive. */
function findTagEnd(text, start) {
  let i = start;
  let depth = 0;
  let quote = null;
  while (i < text.length) {
    const c = text[i];
    if (quote) {
      if (c === '\\') { i += 2; continue; }
      if (c === quote) quote = null;
      i++;
      continue;
    }
    if (c === '"' || c === "'" || c === '`') { quote = c; i++; continue; }
    if (c === '{') { depth++; i++; continue; }
    if (c === '}') { depth--; i++; continue; }
    if (depth === 0 && c === '>') return i;
    i++;
  }
  return -1;
}

function walk(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    const st = statSync(full);
    if (st.isDirectory()) {
      if (entry === 'node_modules' || entry === 'dist' || entry.startsWith('__tests__')) continue;
      walk(full, out);
    } else if (EXTENSIONS.some(ext => entry.endsWith(ext)) && !entry.includes('.test.')) {
      out.push(full);
    }
  }
  return out;
}

function lineAt(text, index) {
  return text.slice(0, index).split('\n').length;
}

// --- SAFE-bucket heuristics, each tied to a typography-contract role (plan Muc 3) -----------------

/** Eyebrow / dt-style label: small uppercase tracking-wide text is the plan's Label role (500-600). */
function looksLikeEyebrowLabel(tagSource) {
  // No trailing \b: "text-[10px]" ends in "]" (non-word) followed by a space (also non-word), so a
  // word-boundary assertion right after "\]" never matches - this silently failed the heuristic for
  // every bracket-size eyebrow label until caught in the 2026-08-18 tag-independent pass.
  return /\buppercase\b/.test(tagSource)
    && /\btracking-(wide|wider|widest)\b/.test(tagSource)
    && /text-(xs|\[1[01]px\])/.test(tagSource);
}

/** Status/badge chip: pill shape + a color background is the plan's Badge/status role (500-600). */
function looksLikeBadge(tagSource) {
  const pill = /\brounded-(full|md|lg)\b/.test(tagSource) && /\bpx-\d/.test(tagSource);
  const colored = /\bbg-(red|green|blue|amber|orange|emerald|rose|purple|gray|slate|yellow|indigo|teal|cyan|lime|pink|sky)-\d{2,3}\b/.test(tagSource)
    || /\bbg-\[#/.test(tagSource);
  return pill && colored;
}

/** Hero/KPI number: large text size + bold is the plan's KPI role (600-700 "neu thuc su la KPI"). */
function looksLikeKpi(tagSource) {
  return /\btext-(2xl|3xl|4xl|5xl|6xl)\b/.test(tagSource);
}

const SAFE_HEURISTICS = [
  ['eyebrow-label', looksLikeEyebrowLabel],
  ['badge', looksLikeBadge],
  ['kpi', looksLikeKpi],
];

function classify(tagSource) {
  for (const [name, test] of SAFE_HEURISTICS) {
    if (test(tagSource)) return name;
  }
  return null;
}

function auditFile(rel, text) {
  const found = [];
  const lines = text.split('\n');
  const re = new RegExp(WEIGHT_CLASS.source, 'g');
  let m;
  while ((m = re.exec(text))) {
    const pos = m.index;
    const tagStart = findEnclosingTagStart(text, pos);
    const tagName = tagStart === -1 ? null : tagNameAt(text, tagStart);
    const line = lineAt(text, pos);
    const lineText = lines[line - 1];
    const snippet = lineText.trim().slice(0, 140);

    if (tagName && EXEMPT_TAGS.has(tagName)) continue;
    if (tagName && FORM_TAGS.has(tagName)) {
      found.push({ line, tag: tagName, weight: m[0], bucket: null, state: 'FORM_CONTROL', reason: undefined, snippet, pass: 'class-based' });
      continue;
    }

    const tagEnd = tagStart === -1 ? -1 : findTagEnd(text, tagStart);
    const heuristicSource = tagEnd !== -1 ? text.slice(tagStart, tagEnd + 1) : lineText;
    const safeBucket = classify(heuristicSource);
    const manifestEntry = manifest.get(`${rel} ${snippet}`);
    let state = 'UNREVIEWED';
    if (safeBucket) state = 'SAFE';
    else if (manifestEntry?.category === 'KEEP') state = 'REVIEWED_KEEP';
    else if (manifestEntry?.category === 'EXCLUDED') state = 'EXCLUDED_WITH_REASON';
    found.push({
      line,
      tag: tagName || '(non-jsx)',
      weight: m[0],
      bucket: safeBucket,
      state,
      reason: manifestEntry?.reason,
      snippet,
      pass: 'class-based',
    });
  }

  // Second surface: <strong>/<b> render bold via the browser's own UA stylesheet with ZERO Tailwind
  // class needed, so the WEIGHT_CLASS pass above (which only fires on a literal font-* token) can
  // never see a bare `<strong>{value}</strong>` - found via a 2026-08-18 repo-wide check that turned
  // up ~90 real instances, 11 of them genuine violations (a metadata value bolded inline, e.g.
  // `Phong ban: <b>{deptName}</b>`, indistinguishable in role from the td/dd values fixed earlier).
  // Only bare tags are examined here: a `<strong className="font-bold ...">` already has a token and
  // was already resolved by the pass above, so re-flagging it here would double count it. A tag that
  // explicitly carries `font-normal` is self-evidently already fixed and needs no manifest entry.
  const STRONG_B_OPEN = /<(strong|b)(?=[\s/>])/g;
  STRONG_B_OPEN.lastIndex = 0;
  let sm;
  while ((sm = STRONG_B_OPEN.exec(text))) {
    const tagStart = sm.index;
    const line = lineAt(text, tagStart);
    const lineText = lines[line - 1];
    const trimmed = lineText.trim();
    // JSDoc/line-comment prose uses the same "<b>Heading.</b> explanation" markdown-ish convention
    // throughout this codebase (features/emails/utils/*) purely as documentation styling - not JSX,
    // never rendered, out of scope entirely.
    if (trimmed.startsWith('*') || trimmed.startsWith('//') || trimmed.startsWith('/**')) continue;
    const tagEnd = findTagEnd(text, tagStart);
    if (tagEnd === -1) continue;
    const openTag = text.slice(tagStart, tagEnd + 1);
    if (/\/>\s*$/.test(openTag)) continue; // self-closing, no content to render bold
    if (WEIGHT_CLASS.test(openTag)) continue; // has an explicit token, already handled above
    if (/\bfont-normal\b/.test(openTag)) continue; // explicitly neutralized already

    const snippet = trimmed.slice(0, 140);
    const safeBucket = classify(openTag);
    const manifestEntry = manifest.get(`${rel} ${snippet}`);
    let state = 'UNREVIEWED';
    if (safeBucket) state = 'SAFE';
    else if (manifestEntry?.category === 'KEEP') state = 'REVIEWED_KEEP';
    else if (manifestEntry?.category === 'EXCLUDED') state = 'EXCLUDED_WITH_REASON';
    found.push({
      line,
      tag: sm[1],
      weight: '(implicit bold, no class)',
      bucket: safeBucket,
      state,
      reason: manifestEntry?.reason,
      snippet,
      pass: 'implicit-bold',
    });
  }
  return found;
}

const allFiles = walk(SRC);
const byFile = [];
const totals = { SAFE: 0, REVIEWED_KEEP: 0, EXCLUDED_WITH_REASON: 0, FORM_CONTROL: 0, UNREVIEWED: 0 };
// Broken out by which detection pass found the occurrence, so "how many are the implicit-bold
// <strong>/<b> ones" is always a number this script prints, never something to reconstruct by hand.
const byPass = { 'class-based': 0, 'implicit-bold': 0 };

for (const file of allFiles) {
  const rel = relative(ROOT, file).split(sep).join('/');
  const text = readFileSync(file, 'utf8');
  const found = auditFile(rel, text);
  if (found.length === 0) continue;
  for (const f of found) { totals[f.state]++; byPass[f.pass]++; }
  byFile.push({ rel, found });
}

if (PRUNE_MANIFEST) {
  const liveKeys = new Set();
  for (const { rel, found } of byFile) {
    for (const f of found) liveKeys.add(`${rel} ${f.snippet}`);
  }

  const seenKeys = new Set();
  const kept = [];
  let orphanCount = 0;
  let duplicateCount = 0;
  const orphanSamples = [];
  for (const entry of manifestEntries) {
    const key = `${entry.file} ${entry.snippet}`;
    if (!liveKeys.has(key)) {
      orphanCount++;
      if (orphanSamples.length < 30) orphanSamples.push(entry);
      continue; // stale: code no longer produces this occurrence at all
    }
    if (seenKeys.has(key)) {
      duplicateCount++;
      continue; // redundant: another entry already covers this exact (file, snippet)
    }
    seenKeys.add(key);
    kept.push(entry);
  }

  console.log(`manifest prune: ${manifestEntries.length} entries -> ${kept.length} kept`);
  console.log(`  orphan (stale, no longer matches any current occurrence): ${orphanCount}`);
  console.log(`  duplicate (same file+snippet as another entry): ${duplicateCount}`);
  if (orphanSamples.length) {
    console.log(`  orphan sample (first ${orphanSamples.length}):`);
    for (const o of orphanSamples) console.log(`    ${o.file} | ${o.snippet}`);
  }
  writeFileSync(MANIFEST_PATH, JSON.stringify(kept, null, 2) + '\n');
  process.exit(0);
}

const unreviewedFiles = byFile
  .map(({ rel, found }) => ({ rel, found: found.filter(f => f.state === 'UNREVIEWED') }))
  .filter(f => f.found.length > 0);

if (AS_JSON) {
  console.log(JSON.stringify(unreviewedFiles, null, 2));
  process.exit(0);
}

if (AS_REPORT) {
  const reportFiles = byFile
    .map(({ rel, found }) => ({ rel, found: found.filter(f => f.state !== 'SAFE') }))
    .filter(f => f.found.length > 0);
  console.log(JSON.stringify({ totals, files: reportFiles }, null, 2));
  process.exit(0);
}

const grandTotal = totals.SAFE + totals.REVIEWED_KEEP + totals.EXCLUDED_WITH_REASON + totals.FORM_CONTROL + totals.UNREVIEWED;
console.log(`display-typography audit (tag-independent): ${allFiles.length} files scanned, ${grandTotal} occurrences`);
console.log(`  by detection pass: ${byPass['class-based']} class-based (font-* Tailwind token) + ${byPass['implicit-bold']} implicit-bold (<strong>/<b> with no class at all) = ${byPass['class-based'] + byPass['implicit-bold']}`);
console.log(`  auto-exempt (heading/button/th/label/legend tags, incl. motion.*): not counted, contract-compliant by role`);
console.log(`  FORM_CONTROL (input/select/textarea — out of scope, see audit-form-typography.mjs): ${totals.FORM_CONTROL}`);
console.log(`  SAFE (eyebrow-label / badge / kpi heuristics): ${totals.SAFE}`);
console.log(`  REVIEWED_KEEP (manifest): ${totals.REVIEWED_KEEP}`);
console.log(`  EXCLUDED_WITH_REASON (manifest): ${totals.EXCLUDED_WITH_REASON}`);
console.log(`  UNREVIEWED: ${totals.UNREVIEWED} across ${unreviewedFiles.length} files`);
console.log('');

const toPrint = PRINT_ALL ? unreviewedFiles : unreviewedFiles.slice(0, 25);
for (const { rel, found } of toPrint) {
  console.log(`  ${rel}  (${found.length})`);
  for (const f of found.slice(0, PRINT_ALL ? undefined : 6)) {
    console.log(`    ${f.line}: <${f.tag}> ${f.weight} — ${f.snippet}`);
  }
}
if (!PRINT_ALL && unreviewedFiles.length > 25) {
  console.log(`  ... and ${unreviewedFiles.length - 25} more files. Re-run with --all or --json.`);
}

process.exit(totals.UNREVIEWED > 0 ? 1 : 0);
