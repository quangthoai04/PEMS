/**
 * The canonical-schema hashing rule, in JavaScript.
 *
 * This is the SECOND implementation of the rule; the first is
 * `CanonicalSqlScript.ComputeNormalizedSha256` in tests/PEMS.IntegrationTests. The integration suite does
 * NOT call this file — it must be able to run with no Node present — so the two are genuinely independent,
 * and a shared fixture with a pinned hash is what keeps them honest:
 *
 *   • C#: CanonicalSqlHashTests.Cross_language_fixture_matches_the_value_the_workflow_asserts
 *   • CI: the "Hashing rule agrees with the C# implementation" step of merge-validation.yml
 *
 * Both assert b8fa13ef… for the same fixture, so changing one implementation alone turns something red.
 *
 * WHY normalise at all: `.gitattributes` declares `* text=auto`, so git stores the schema with LF and a
 * Windows worktree holds it with CRLF. A raw-byte hash of that file therefore has two correct answers and
 * a pin can satisfy only one platform at a time — which is how this repository's first CI run went red on
 * a branch whose local gates were all green, with the schema entirely unchanged.
 *
 * WHY normalise no further: only a leading BOM and line endings are folded away. No trimming, no
 * whitespace collapsing, no Unicode normalisation — each of those would also hide a real edit, such as a
 * dropped space in a SIGNAL message or a changed indent inside a trigger body, where a single space is
 * content.
 *
 * Self-test: `node scripts/normalized-sql-hash.cjs --self-test`
 */
'use strict';

const crypto = require('crypto');

/** The shared fixture: leading BOM, CRLF, a lone CR, a plain LF, and multi-byte Vietnamese text. */
const FIXTURE =
  '\uFEFF' +
  'CREATE TABLE v\u00ED_d\u1EE5 (\r\n' +
  '  id BIGINT,\r' +
  '  t\u00EAn VARCHAR(255),\n' +
  '  ghi_ch\u00FA TEXT -- Ti\u1EBFng Vi\u1EC7t: \u0111\u1EE7 d\u1EA5u\r\n' +
  ');\n';

/** The hash the C# side pins for FIXTURE. */
const FIXTURE_SHA256 = 'b8fa13eff82815dedb6c3f17cccc92f8026c1be5584e84d46151b2dd2e937d26';

/**
 * Strips exactly one leading BOM and folds CRLF and lone CR to LF. Nothing else.
 * @param {string} text
 * @returns {string}
 */
function normalize(text) {
  if (text.charCodeAt(0) === 0xfeff) text = text.slice(1);
  return text.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
}

/**
 * Lower-case hex SHA-256 of the normalised text, encoded UTF-8 without a BOM.
 * @param {string} text
 * @returns {string}
 */
function normalizedSha256(text) {
  return crypto.createHash('sha256').update(Buffer.from(normalize(text), 'utf8')).digest('hex');
}

module.exports = { normalize, normalizedSha256, FIXTURE, FIXTURE_SHA256 };

// ── Self-test ────────────────────────────────────────────────────────────────────────────────────

if (require.main === module) {
  const args = process.argv.slice(2);

  if (args[0] === '--self-test') {
    const lf = 'CREATE TABLE example (\n  id BIGINT NOT NULL\n);\n';
    const checks = [
      ['LF === CRLF', normalizedSha256(lf) === normalizedSha256(lf.replace(/\n/g, '\r\n'))],
      ['LF === lone CR', normalizedSha256(lf) === normalizedSha256(lf.replace(/\n/g, '\r'))],
      ['BOM === no BOM', normalizedSha256(lf) === normalizedSha256('\uFEFF' + lf)],
      ['content change differs', normalizedSha256(lf) !== normalizedSha256(lf.replace('BIGINT', 'INT'))],
      ['inner whitespace differs', normalizedSha256(lf) !== normalizedSha256(lf.replace('id BIGINT', 'id  BIGINT'))],
      ['trailing newline differs', normalizedSha256(lf) !== normalizedSha256(lf + '\n')],
      ['fixture matches the C# pin', normalizedSha256(FIXTURE) === FIXTURE_SHA256],
    ];

    let failed = 0;
    for (const [name, ok] of checks) {
      console.log(`${ok ? 'ok  ' : 'FAIL'}  ${name}`);
      if (!ok) failed++;
    }
    console.log(failed === 0 ? `\nall ${checks.length} checks passed` : `\n${failed} check(s) FAILED`);
    process.exit(failed === 0 ? 0 : 1);
  }

  const file = args[0];
  if (!file) {
    console.error('usage: node scripts/normalized-sql-hash.cjs <file.sql> | --self-test');
    process.exit(2);
  }
  console.log(normalizedSha256(require('fs').readFileSync(file, 'utf8')));
}
