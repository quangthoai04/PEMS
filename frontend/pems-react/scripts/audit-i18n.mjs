/**
 * PEMS i18n Key Parity Audit Script
 * Checks VI vs EN locale files for:
 *   - Missing keys in one locale but not the other
 *   - Empty values
 *   - TODO/placeholder values
 *   - Type mismatches (object vs string)
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const LOCALES_DIR = path.resolve(__dirname, '../src/shared/i18n/locales');

const VI_DIR = path.join(LOCALES_DIR, 'vi');
const EN_DIR = path.join(LOCALES_DIR, 'en');

// Flatten nested object into dot-separated keys
function flattenKeys(obj, prefix = '') {
  const result = {};
  for (const [k, v] of Object.entries(obj)) {
    const fullKey = prefix ? `${prefix}.${k}` : k;
    if (v !== null && typeof v === 'object' && !Array.isArray(v)) {
      Object.assign(result, flattenKeys(v, fullKey));
    } else {
      result[fullKey] = v;
    }
  }
  return result;
}

function getTypeLabel(v) {
  if (v === null) return 'null';
  if (Array.isArray(v)) return 'array';
  return typeof v;
}

function getNestedType(obj, keyPath) {
  const parts = keyPath.split('.');
  let cur = obj;
  for (const p of parts) {
    if (cur == null || typeof cur !== 'object') return 'missing';
    cur = cur[p];
  }
  return getTypeLabel(cur);
}

const BAD_VALUES = new Set(['', 'TODO', 'FIXME', 'missing', 'undefined', 'null', 'TBD']);

function isBad(v) {
  if (v === null || v === undefined) return true;
  if (typeof v === 'string') return BAD_VALUES.has(v.trim()) || v.trim() === '';
  return false;
}

const namespaces = fs.readdirSync(VI_DIR)
  .filter(f => f.endsWith('.json'))
  .map(f => f.replace('.json', ''));

const report = {
  missingInVI: [],
  missingInEN: [],
  emptyValues: [],
  typeMismatches: [],
};

console.log('');
console.log('╔══════════════════════════════════════════════════════╗');
console.log('║      PEMS i18n Key Parity Audit Report               ║');
console.log('╚══════════════════════════════════════════════════════╝');
console.log('');

for (const ns of namespaces) {
  const viPath = path.join(VI_DIR, `${ns}.json`);
  const enPath = path.join(EN_DIR, `${ns}.json`);

  const viRaw = fs.existsSync(viPath) ? JSON.parse(fs.readFileSync(viPath, 'utf8')) : {};
  const enRaw = fs.existsSync(enPath) ? JSON.parse(fs.readFileSync(enPath, 'utf8')) : {};

  const viFlat = flattenKeys(viRaw);
  const enFlat = flattenKeys(enRaw);

  const viKeys = new Set(Object.keys(viFlat));
  const enKeys = new Set(Object.keys(enFlat));

  for (const k of viKeys) {
    if (!enKeys.has(k)) {
      report.missingInEN.push({ namespace: ns, key: k });
    }
    if (isBad(viFlat[k])) {
      report.emptyValues.push({ locale: 'vi', namespace: ns, key: k, value: viFlat[k] });
    }
  }

  for (const k of enKeys) {
    if (!viKeys.has(k)) {
      report.missingInVI.push({ namespace: ns, key: k });
    }
    if (isBad(enFlat[k])) {
      report.emptyValues.push({ locale: 'en', namespace: ns, key: k, value: enFlat[k] });
    }
  }

  // Type mismatch check using original nested objects
  for (const k of viKeys) {
    if (enKeys.has(k)) {
      const viT = getNestedType(viRaw, k);
      const enT = getNestedType(enRaw, k);
      if (viT !== enT) {
        report.typeMismatches.push({ namespace: ns, key: k, viType: viT, enType: enT });
      }
    }
  }
}

// ── Print results ──

console.log(`📋 Namespaces checked: ${namespaces.join(', ')}\n`);

// Missing in EN
if (report.missingInEN.length === 0) {
  console.log('✅ No keys missing in EN locale.\n');
} else {
  console.log(`❌ Keys missing in EN (${report.missingInEN.length} total):`);
  const grouped = {};
  for (const { namespace, key } of report.missingInEN) {
    if (!grouped[namespace]) grouped[namespace] = [];
    grouped[namespace].push(key);
  }
  for (const [ns, keys] of Object.entries(grouped)) {
    console.log(`  [${ns}]: ${keys.join(', ')}`);
  }
  console.log('');
}

// Missing in VI
if (report.missingInVI.length === 0) {
  console.log('✅ No keys missing in VI locale.\n');
} else {
  console.log(`❌ Keys missing in VI (${report.missingInVI.length} total):`);
  const grouped = {};
  for (const { namespace, key } of report.missingInVI) {
    if (!grouped[namespace]) grouped[namespace] = [];
    grouped[namespace].push(key);
  }
  for (const [ns, keys] of Object.entries(grouped)) {
    console.log(`  [${ns}]: ${keys.join(', ')}`);
  }
  console.log('');
}

// Empty values
if (report.emptyValues.length === 0) {
  console.log('✅ No empty/bad values found.\n');
} else {
  console.log(`⚠️  Empty or bad values (${report.emptyValues.length} total):`);
  for (const { locale, namespace, key, value } of report.emptyValues) {
    console.log(`  [${locale}/${namespace}] ${key} = ${JSON.stringify(value)}`);
  }
  console.log('');
}

// Type mismatches
if (report.typeMismatches.length === 0) {
  console.log('✅ No type mismatches found.\n');
} else {
  console.log(`❌ Type mismatches (${report.typeMismatches.length} total):`);
  for (const { namespace, key, viType, enType } of report.typeMismatches) {
    console.log(`  [${namespace}] ${key}: VI=${viType}, EN=${enType}`);
  }
  console.log('');
}

// Summary
const totalIssues = report.missingInEN.length + report.missingInVI.length + report.emptyValues.length + report.typeMismatches.length;
if (totalIssues === 0) {
  console.log('🎉 ALL CHECKS PASSED — Locale key parity is 100%!\n');
} else {
  console.log(`🔴 TOTAL ISSUES: ${totalIssues}\n`);
}

// Export JSON for report generation
const OUTPUT = {
  timestamp: new Date().toISOString(),
  ...report,
  summary: {
    totalMissingEN: report.missingInEN.length,
    totalMissingVI: report.missingInVI.length,
    totalEmpty: report.emptyValues.length,
    totalTypeMismatch: report.typeMismatches.length,
    totalIssues,
    passed: totalIssues === 0,
  }
};

const outPath = path.resolve(__dirname, '../i18n-audit-result.json');
fs.writeFileSync(outPath, JSON.stringify(OUTPUT, null, 2), 'utf8');
console.log(`📁 Raw results saved to: ${outPath}\n`);
