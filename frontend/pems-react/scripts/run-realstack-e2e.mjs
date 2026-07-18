#!/usr/bin/env node
/**
 * Reproducible REAL-STACK E2E orchestrator (H-4). Boots the full stack against a DISPOSABLE MySQL and runs
 * the real-stack Playwright specs, then tears everything down — even on failure.
 *
 *   real Chromium → real React (Vite) → real .NET API (Testing, both v2 flags ON) → disposable MySQL
 *                                                            + Testing-only FileSink OTP inbox
 *
 * Steps: create `pems_e2e_realstack` from the fixed master SQL → publish + start the backend on a dedicated
 * port with env overrides (never edits appsettings) → wait healthy → `playwright test` (starts Vite, points
 * it at the backend) → stop backend, drop the DB, delete the inbox. NEVER touches pems_db / pems_test /
 * pems_pr3_test. Requires the `mysql` CLI and `dotnet` on PATH.
 *
 *   npm run test:e2e:realstack
 *
 * Env overrides (all optional): PEMS_E2E_DB, PEMS_E2E_API_PORT, PEMS_E2E_FRONTEND_PORT, MYSQL_BIN,
 * MYSQL_USER, MYSQL_PASSWORD, MYSQL_HOST, MYSQL_PORT.
 */
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import process from 'node:process';

const HERE = resolve(new URL('.', import.meta.url).pathname);
const REPO = resolve(HERE, '..', '..', '..');            // …/PEMS
const API_PROJ = join(REPO, 'backend', 'PEMS.Api', 'PEMS.Api.csproj');
const MASTER = join(REPO, 'docs', 'database', 'scripts', 'pems_full_v10_TTS_Gallery_FULL_UPDATED_NOTIFICATIONS_FIXED.sql');

const DB = process.env.PEMS_E2E_DB ?? 'pems_e2e_realstack';
const API_PORT = process.env.PEMS_E2E_API_PORT ?? '5299';
const FE_PORT = process.env.PEMS_E2E_FRONTEND_PORT ?? '3100';
const MYSQL = process.env.MYSQL_BIN ?? 'mysql';
const MYSQL_USER = process.env.MYSQL_USER ?? 'root';
const MYSQL_PW = process.env.MYSQL_PASSWORD ?? '123456';
const MYSQL_HOST = process.env.MYSQL_HOST ?? 'localhost';
const MYSQL_PORT = process.env.MYSQL_PORT ?? '3306';

if (['pems_db', 'pems_test', 'pems_pr3_test'].includes(DB)) {
  console.error(`Refusing to use a protected database name: ${DB}`);
  process.exit(2);
}

const workDir = mkdtempSync(join(tmpdir(), 'pems-e2e-'));
const publishDir = join(workDir, 'api');
const inbox = join(workDir, 'inbox.jsonl');
const conn = `server=${MYSQL_HOST};port=${MYSQL_PORT};database=${DB};user=${MYSQL_USER};password=${MYSQL_PW};AllowUserVariables=True;GuidFormat=None`;
let backend = null;

const mysql = (sql, db) => spawnSync(
  MYSQL, [`-u${MYSQL_USER}`, `-p${MYSQL_PW}`, `-h${MYSQL_HOST}`, `-P${MYSQL_PORT}`,
    '--default-character-set=utf8mb4', ...(db ? [db] : []), '-e', sql],
  { encoding: 'utf8' });

const importMaster = () => {
  // Replace the seed db name and stream the master into mysql.
  const sqlText = readFileSync(MASTER, 'utf8').replaceAll('pems_db', DB);
  const r = spawnSync(MYSQL, [`-u${MYSQL_USER}`, `-p${MYSQL_PW}`, `-h${MYSQL_HOST}`, `-P${MYSQL_PORT}`,
    '--default-character-set=utf8mb4', DB], { input: sqlText, encoding: 'utf8', maxBuffer: 1 << 28 });
  if (r.status !== 0) throw new Error(`master import failed: ${r.stderr}`);
};

const waitHealthy = async (url, timeoutMs) => {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(url);
      if (res.ok) return true;
    } catch { /* not up yet */ }
    await new Promise(r => setTimeout(r, 1000));
  }
  return false;
};

const run = (cmd, args, opts = {}) => new Promise((res, rej) => {
  const p = spawn(cmd, args, { stdio: 'inherit', shell: process.platform === 'win32', ...opts });
  p.on('exit', code => (code === 0 ? res() : rej(new Error(`${cmd} exited ${code}`))));
  p.on('error', rej);
});

function cleanup() {
  try { if (backend && !backend.killed) backend.kill(); } catch { /* ignore */ }
  mysql(`DROP DATABASE IF EXISTS \`${DB}\``);
  try { rmSync(workDir, { recursive: true, force: true }); } catch { /* ignore */ }
}

process.on('SIGINT', () => { cleanup(); process.exit(130); });

let exitCode = 1;
try {
  console.log(`[e2e] create disposable DB ${DB} from master`);
  mysql(`DROP DATABASE IF EXISTS \`${DB}\`; CREATE DATABASE \`${DB}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`);
  importMaster();

  console.log('[e2e] publish backend');
  await run('dotnet', ['publish', API_PROJ, '-c', 'Debug', '-o', publishDir, '--nologo', '-v', 'q']);

  console.log(`[e2e] start backend on :${API_PORT} (Testing, flags ON, sink)`);
  writeFileSync(inbox, '');
  backend = spawn('dotnet', [join(publishDir, 'PEMS.Api.dll')], {
    cwd: publishDir,
    stdio: 'ignore',
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Testing',
      ASPNETCORE_URLS: `http://localhost:${API_PORT}`,
      ConnectionStrings__DefaultConnection: conn,
      PerCampusFormV2__Enabled: 'true',
      PerCampusFormV2Write__Enabled: 'true',
      Cors__AllowedOrigins__0: `http://localhost:${FE_PORT}`,
      Smtp__Enabled: 'false',
      PEMS_E2E_TEST_SINK_ENABLED: 'true',
      PEMS_E2E_TEST_SINK_PATH: inbox,
    },
  });

  const healthy = await waitHealthy(`http://localhost:${API_PORT}/api/campuses/available-for-registration`, 120_000);
  if (!healthy) throw new Error('backend did not become healthy within 120s');

  console.log('[e2e] run real-stack Playwright specs');
  await run('npx', ['playwright', 'test', '--config', 'playwright.realstack.config.ts'], {
    cwd: join(REPO, 'frontend', 'pems-react'),
    env: {
      ...process.env,
      PEMS_E2E_TEST_SINK_PATH: inbox,
      PEMS_E2E_API_BASE: `http://localhost:${API_PORT}/api`,
      PEMS_E2E_FRONTEND_PORT: FE_PORT,
    },
  });
  exitCode = 0;
  console.log('[e2e] PASS');
} catch (err) {
  console.error(`[e2e] FAIL: ${err.message}`);
} finally {
  cleanup();
}
process.exit(exitCode);
