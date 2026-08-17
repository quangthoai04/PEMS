#!/usr/bin/env node
/**
 * Regression guard for docs/CanhIter3FixBug/GopYCQuyen/PEMS_Form_Control_Typography_Normalization_Plan.md
 *
 * Rule: the VALUE a user types or selects inside a form control (<input>, <textarea>, <select>,
 * and react-select's control/input/singleValue/placeholder) must render `font-normal` — bold
 * weight is reserved for titles, labels, buttons, badges and other intentional emphasis.
 *
 * A naive `/<input[^>]*font-(bold|semibold|medium)/` regex breaks on real JSX: an event handler
 * like `onChange={(e) => setX(e.target.value)}` inside the tag contains a bare `>` (from `=>`)
 * that a plain regex sees as the tag's closing bracket, so it stops scanning before it ever
 * reaches the className. This script instead walks the tag character-by-character, tracking
 * string-quote and `{}` nesting depth, and only treats a top-level (depth 0, not mid-string) `>`
 * as the real end of the opening tag — the same rule a JSX parser applies.
 *
 * Usage:  node scripts/audit-form-typography.mjs   (exit 1 on a violation not in WHITELIST)
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dirname } from 'node:path';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const SRC = join(ROOT, 'src');

/**
 * Exact (file, line, note) exceptions that are allowed to keep a heavier weight on a form-control
 * value, each with a stated reason. Never whitelist a whole file/feature — only a specific line.
 */
const WHITELIST = new Set([
  // `file:font-semibold` styles the browser-native "Choose file" BUTTON pseudo-element
  // (::file-selector-button) of an <input type="file">, not a typed/selected value — button text
  // is explicitly out of scope. The `\b` word boundary in WEIGHT_CLASS matches the substring after
  // `file:`, which is a false positive of this script, not a real violation.
  'src/pages/dashboard/partners/PartnerDetail.tsx:793',
]);

const EXTENSIONS = ['.ts', '.tsx'];
const WEIGHT_CLASS = /\bfont-(medium|semibold|bold|extrabold|black)\b/;
const TAG_NAME = /<(input|textarea|select)(?=[\s/>])/g;

/**
 * Custom components that render a form-control VALUE (not just a label/wrapper) and accept a
 * `className` from the caller — that className lands directly on the value, the same way a
 * literal `<input>` would, so a bold weight passed at the call site is just as real a violation.
 * Caught in the wild: `<UnitPriceInput className="... font-bold ...">` (GeneralExpensePanel.tsx,
 * LogisticsExpensePanel.tsx) — the native-tag scan above can't see it because the tag name isn't
 * `input`. Add a component here the moment it's confirmed to forward `className` onto its value.
 */
const CUSTOM_VALUE_COMPONENTS = ['UnitPriceInput'];
const CUSTOM_TAG_NAME = new RegExp(`<(${CUSTOM_VALUE_COMPONENTS.join('|')})(?=[\\s/>])`, 'g');

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

/** From `start` (index of the `<` in `<input`), find the index of the `>` that closes THIS tag,
 *  skipping over string literals and `{...}` JS-expression attribute values (which may contain
 *  their own `>`, e.g. `=>`, generics, or comparisons). Returns -1 if unterminated. */
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

function lineAt(text, index) {
  return text.slice(0, index).split('\n').length;
}

function auditTags(file, rel, text, tagRegex) {
  const violations = [];
  tagRegex.lastIndex = 0;
  let m;
  while ((m = tagRegex.exec(text))) {
    const tagStart = m.index;
    const tagEnd = findTagEnd(text, tagStart);
    if (tagEnd === -1) continue;
    const tagSource = text.slice(tagStart, tagEnd + 1);
    const weightMatch = tagSource.match(WEIGHT_CLASS);
    if (!weightMatch) continue;
    const line = lineAt(text, tagStart);
    const key = `${rel}:${line}`;
    if (WHITELIST.has(key)) continue;
    violations.push({
      line,
      kind: m[1],
      weight: weightMatch[0],
      snippet: text.split('\n')[line - 1].trim().slice(0, 140),
    });
  }
  return violations;
}

function auditNativeControls(file, rel, text) {
  return auditTags(file, rel, text, TAG_NAME);
}

function auditCustomValueComponents(file, rel, text) {
  return auditTags(file, rel, text, CUSTOM_TAG_NAME);
}

/** react-select styles config: flag `fontWeight` set heavier than normal on the keys that render
 *  the value/input itself (control/singleValue/input/placeholder/valueContainer). Menu/option
 *  styling is exempt — the plan does not require selected-in-list options to be non-bold. */
const VALUE_STYLE_KEYS = new Set(['control', 'singleValue', 'input', 'placeholder', 'valueContainer']);
const STYLE_KEY_HEADER = /^\s*([a-zA-Z]+)\s*:\s*\(/;
const FONT_WEIGHT_PROP = /fontWeight\s*:\s*(['"]?)(\w+)\1/;
const NORMAL_WEIGHTS = new Set(['400', 'normal']);

function auditReactSelectStyles(file, rel, text) {
  if (!text.includes('react-select')) return [];
  const violations = [];
  const lines = text.split('\n');
  for (let idx = 0; idx < lines.length; idx++) {
    const fw = lines[idx].match(FONT_WEIGHT_PROP);
    if (!fw) continue;
    if (NORMAL_WEIGHTS.has(fw[2])) continue;
    let key = null;
    for (let back = idx; back >= 0 && idx - back < 30; back--) {
      const h = lines[back].match(STYLE_KEY_HEADER);
      if (h) { key = h[1]; break; }
    }
    if (!key || !VALUE_STYLE_KEYS.has(key)) continue;
    const line = idx + 1;
    const wkey = `${rel}:${line}`;
    if (WHITELIST.has(wkey)) continue;
    violations.push({ line, kind: `react-select.${key}`, weight: `fontWeight:${fw[2]}`, snippet: lines[idx].trim().slice(0, 140) });
  }
  return violations;
}

const offenders = [];
for (const file of walk(SRC)) {
  const rel = relative(ROOT, file).split(sep).join('/');
  const text = readFileSync(file, 'utf8');
  const violations = [
    ...auditNativeControls(file, rel, text),
    ...auditCustomValueComponents(file, rel, text),
    ...auditReactSelectStyles(file, rel, text),
  ];
  if (violations.length > 0) offenders.push({ rel, violations });
}

if (offenders.length === 0) {
  console.log('✓ form-typography audit: no bold/semibold/medium weight found on form control values.');
  process.exit(0);
}

console.error('✗ form-typography audit: form control VALUES must render font-normal.\n');
for (const { rel, violations } of offenders) {
  console.error(`  ${rel}`);
  for (const v of violations) console.error(`    ${v.line}: [${v.kind}] ${v.weight} — ${v.snippet}`);
}
console.error(
  '\nMove the weight class off the value (input/textarea/select/react-select control) — keep bold' +
  '\nfor labels, headings, buttons and badges. If a specific line is a deliberate, reviewed exception,' +
  '\nadd `path:line` to WHITELIST in scripts/audit-form-typography.mjs with a comment explaining why.');
process.exit(1);
