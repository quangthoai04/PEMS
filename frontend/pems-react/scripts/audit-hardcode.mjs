/**
 * PEMS Static Hardcode Audit Script
 * Scans all public-facing TSX/TS files for hardcoded Vietnamese or English text
 * that should be in i18n keys instead.
 *
 * Outputs a classified report of findings.
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SRC_DIR = path.resolve(__dirname, '../src');

// Files/directories to skip (not public UI)
const SKIP_DIRS = new Set([
  'node_modules', 'dist', '.git',
  // internal dashboard pages
  'pages/dashboard',
  'pages/auth', // login, forgot, reset pages are "bare" but already audited
  'features/authentication', // already audited separately
  'shared/i18n',  // locale files themselves
]);

// Public scope directories to focus on
const PUBLIC_SCOPES = [
  'pages/HomePage.tsx',
  'pages/NewsPage.tsx',
  'pages/NewsDetailPage.tsx',
  'pages/PartnersPage.tsx',
  'pages/PartnerDetailPage.tsx',
  'pages/VisitFPTUPage.tsx',
  'pages/CampusDetailVisitPage.tsx',
  'pages/FAQPage.tsx',
  'pages/ForbiddenPage.tsx',
  'pages/InvalidAccountPage.tsx',
  'pages/NotFoundPage.tsx',
  'components/layout/Header.tsx',
  'components/layout/Footer.tsx',
  'components/layout/ErrorBoundary.tsx',
  'components/home',
  'components/modals/SearchPopup.tsx',
  'components/modals/LoginModal.tsx',
  'components/modals/VisitingFormPopup.tsx',
  'features/visit-request/components',
  'features/public-partners',
];

// Patterns for Vietnamese characters
const VI_PATTERN = /[ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠàáâãèéêìíòóôõùúăđĩũơưẠ-ỹ]/u;

// Strings that are ALLOWED (brand names, addresses, etc.)
const ALLOWED_PATTERNS = [
  /FPT University|FPT Education|FPTU|FPT/,
  /Hòa Lạc|Đại học FPT|TP\.HCM|TP HCM|Cần Thơ|Đà Nẵng|Quy Nhơn/,
  /^\/\//, // comments
  /i18nKey=|t\('/,  // already using i18n
  /\btest\b|\bspec\b/i,
  /@.*\.com/, // emails
];

// Lines to skip (comments, i18n calls, type comments)
const SKIP_LINE_PATTERNS = [
  /^\s*\/\//,          // single-line comment
  /^\s*\*/           , // JSDoc
  /t\('/,              // already using t()
  /i18nKey=/,          // Trans component
  /\/\*.*\*\//,        // inline block comment
  /console\./,         // console logs
];

function shouldSkipLine(line) {
  return SKIP_LINE_PATTERNS.some(p => p.test(line));
}

function isAllowed(text) {
  return ALLOWED_PATTERNS.some(p => p.test(text));
}

function collectFiles(dir, results = []) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    const rel = path.relative(SRC_DIR, fullPath).replace(/\\/g, '/');

    if (SKIP_DIRS.has(rel) || Array.from(SKIP_DIRS).some(d => rel.startsWith(d))) {
      continue;
    }

    if (entry.isDirectory()) {
      collectFiles(fullPath, results);
    } else if (entry.isFile() && /\.(tsx|ts)$/.test(entry.name) && !entry.name.includes('.test.') && !entry.name.includes('.spec.')) {
      // Check if it's in public scope
      const isPublic = PUBLIC_SCOPES.some(s => rel.startsWith(s) || rel === s);
      if (isPublic) {
        results.push({ fullPath, rel });
      }
    }
  }
  return results;
}

const files = collectFiles(SRC_DIR);

const findings = [];

for (const { fullPath, rel } of files) {
  const content = fs.readFileSync(fullPath, 'utf8');
  const lines = content.split('\n');

  lines.forEach((line, idx) => {
    if (shouldSkipLine(line)) return;

    // Check for Vietnamese characters in JSX content or strings
    if (VI_PATTERN.test(line) && !isAllowed(line)) {
      // Only flag if it looks like UI text (in JSX, strings, aria-label, placeholder, title, alt)
      const isJSX = />[^<{]+<|["'][^"']+["']|placeholder=|aria-label=|title=|alt=/.test(line);
      if (isJSX) {
        findings.push({
          file: rel,
          line: idx + 1,
          content: line.trim().substring(0, 120),
          type: 'VI_HARDCODED',
        });
      }
    }
  });
}

console.log('');
console.log('╔══════════════════════════════════════════════════════╗');
console.log('║   PEMS Static Hardcode Audit — Public UI Files      ║');
console.log('╚══════════════════════════════════════════════════════╝');
console.log('');
console.log(`📁 Scanned ${files.length} public source files\n`);

if (findings.length === 0) {
  console.log('✅ No hardcoded Vietnamese text found in public UI files!\n');
} else {
  console.log(`❌ Found ${findings.length} potential hardcoded text instances:\n`);
  const grouped = {};
  for (const f of findings) {
    if (!grouped[f.file]) grouped[f.file] = [];
    grouped[f.file].push(f);
  }
  for (const [file, items] of Object.entries(grouped)) {
    console.log(`\n  📄 ${file}:`);
    for (const item of items) {
      console.log(`     Line ${item.line}: ${item.content}`);
    }
  }
}

// Save JSON output
const out = { timestamp: new Date().toISOString(), filesScanned: files.length, findings };
fs.writeFileSync(path.resolve(__dirname, '../hardcode-audit-result.json'), JSON.stringify(out, null, 2));
console.log('\n📁 Raw results saved to: hardcode-audit-result.json\n');
