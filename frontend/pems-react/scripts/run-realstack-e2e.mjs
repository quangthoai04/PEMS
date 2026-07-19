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
import { randomBytes } from 'node:crypto';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import process from 'node:process';

// fileURLToPath (NOT new URL(...).pathname) so a repo path with spaces/drive letter resolves correctly.
const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '..', '..', '..');            // …/PEMS
const API_PROJ = join(REPO, 'backend', 'PEMS.Api', 'PEMS.Api.csproj');
const MASTER = join(REPO, 'docs', 'database', 'scripts', 'PEMS_FULL_V11_EXPENSE_COMPATIBILITY_FIXED_V3.sql');

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
// Fail-closed E2E test-auth: a fresh run-scoped secret (never on disk, never logged) + a server-side profile
// file (opaque keys → seeded identities, NO secret) written under the temp workDir and deleted in cleanup.
const authSecret = randomBytes(32).toString('hex');
const profileFile = join(workDir, 'e2e-auth-profiles.json');
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

// Resolve the seeded identities (by stable email) and write the opaque-key → identity profile file. The
// browser only ever sends a profile KEY + the run secret; role/campus come from HERE, never a header.
const writeAuthProfiles = () => {
  const q = spawnSync(MYSQL, [`-u${MYSQL_USER}`, `-p${MYSQL_PW}`, `-h${MYSQL_HOST}`, `-P${MYSQL_PORT}`,
    '-N', '-B', '--default-character-set=utf8mb4', DB, '-e',
    "SELECT u.user_id, u.email, r.role_code, COALESCE(u.sub_role,''), COALESCE(u.primary_campus_id,'') " +
    "FROM users u JOIN roles r ON r.role_id = u.role_id WHERE u.email IN " +
    "('ho@fpt.edu.vn','staff.leader.hn@fpt.edu.vn','staff.leader.hcm@fpt.edu.vn','visitor@example.com')"],
    { encoding: 'utf8' });
  if (q.status !== 0) throw new Error(`auth profile seed query failed: ${q.stderr}`);
  const byEmail = {};
  for (const line of q.stdout.trim().split('\n').filter(Boolean)) {
    const [userId, email, roleCode, subRole, campus] = line.split('\t');
    byEmail[email] = {
      userId: Number(userId), email, roleCode,
      subRole: subRole || null, primaryCampusId: campus ? Number(campus) : null,
    };
  }
  const pick = (key, email) => {
    const p = byEmail[email];
    if (!p) throw new Error(`E2E seed user not found (fail-closed): ${email}`);
    return { key, ...p };
  };
  const profiles = [
    pick('ho_viewer', 'ho@fpt.edu.vn'),
    pick('campus_leader_hn', 'staff.leader.hn@fpt.edu.vn'),
    pick('campus_leader_hcm', 'staff.leader.hcm@fpt.edu.vn'),
    pick('visitor_owner', 'visitor@example.com'),
  ];
  // Seed an ACTIVE session per profile so the real SessionValidationMiddleware accepts the E2E actor exactly
  // like a logged-in user (no production middleware bypass). login_portal follows the account kind.
  for (const p of profiles) {
    const portal = p.roleCode === 'VISITOR' ? 'VISITOR' : 'INTERNAL';
    const ins = mysql(
      `INSERT INTO user_sessions (user_id, login_portal, expires_at, created_at) ` +
      `VALUES (${p.userId}, '${portal}', DATE_ADD(NOW(), INTERVAL 1 DAY), NOW())`, DB);
    if (ins.status !== 0) throw new Error(`session seed failed for ${p.email}: ${ins.stderr}`);
    const s = spawnSync(MYSQL, [`-u${MYSQL_USER}`, `-p${MYSQL_PW}`, `-h${MYSQL_HOST}`, `-P${MYSQL_PORT}`,
      '-N', '-B', DB, '-e', `SELECT session_id FROM user_sessions WHERE user_id = ${p.userId} ORDER BY session_id DESC LIMIT 1`],
      { encoding: 'utf8' });
    p.sessionId = Number(s.stdout.trim());
  }
  writeFileSync(profileFile, JSON.stringify(profiles));
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
  // On Windows we need shell:true to resolve npx/dotnet (.cmd), but then args with spaces (a repo path like
  // "SUMMER 2026 Final") must be quoted or MSBuild/npx splits them into separate switches.
  const useShell = process.platform === 'win32';
  const finalArgs = useShell ? args.map(a => (/\s/.test(a) ? `"${a}"` : a)) : args;
  const p = spawn(cmd, finalArgs, { stdio: 'inherit', shell: useShell, ...opts });
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

  console.log('[e2e] write server-side auth profiles from actual seeded IDs');
  writeAuthProfiles();

  console.log('[e2e] publish backend');
  // Build intermediates to a temp BaseOutputPath so a running dev server holding the repo bin/ lock never
  // blocks the publish (lets the harness run alongside `dotnet run` on another port).
  await run('dotnet', ['publish', API_PROJ, '-c', 'Debug', '-o', publishDir, '--nologo', '-v', 'q',
    `-p:BaseOutputPath=${join(workDir, 'binout')}\\`]);

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
      // Fail-closed test-auth (four gates): Testing env + explicit flag + run secret + profile file.
      PEMS_E2E_TEST_AUTH_ENABLED: 'true',
      PEMS_E2E_TEST_AUTH_SECRET: authSecret,
      PEMS_E2E_TEST_AUTH_PROFILES: profileFile,
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
      // The specs inject the run secret + a profile key on backend requests (never persisted; never logged).
      PEMS_E2E_AUTH_SECRET: authSecret,
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
