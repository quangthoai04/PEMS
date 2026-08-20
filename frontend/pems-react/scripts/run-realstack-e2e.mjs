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
import { mkdtempSync, openSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import process from 'node:process';

// fileURLToPath (NOT new URL(...).pathname) so a repo path with spaces/drive letter resolves correctly.
const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '..', '..', '..');            // …/PEMS
const API_PROJ = join(REPO, 'backend', 'PEMS.Api', 'PEMS.Api.csproj');
// Resolve the ONE canonical schema script instead of hard-coding a filename: the previously hard-coded
// name was renamed away, leaving this orchestrator pointing at a file that no longer existed. Fail closed
// when it is missing or ambiguous rather than importing "whatever is there".
const SCRIPTS_DIR = join(REPO, 'docs', 'database', 'scripts');
const resolveMaster = () => {
  const candidates = readdirSync(SCRIPTS_DIR).filter((f) => /^PEMS_FULL_.*\.sql$/.test(f));
  if (candidates.length !== 1) {
    throw new Error(
      `Expected exactly one canonical PEMS_FULL_*.sql in ${SCRIPTS_DIR}, found ${candidates.length}` +
      `${candidates.length ? `: ${candidates.join(', ')}` : ''}.`);
  }
  return join(SCRIPTS_DIR, candidates[0]);
};
const MASTER = resolveMaster();

const DB = process.env.PEMS_E2E_DB ?? 'pems_e2e_realstack';
const API_PORT = process.env.PEMS_E2E_API_PORT ?? '5299';
const FE_PORT = process.env.PEMS_E2E_FRONTEND_PORT ?? '3100';
const MYSQL = process.env.MYSQL_BIN ?? 'mysql';
const MYSQL_USER = process.env.MYSQL_USER ?? 'root';
const MYSQL_PW = process.env.MYSQL_PASSWORD ?? '123456';
const MYSQL_HOST = process.env.MYSQL_HOST ?? 'localhost';
const MYSQL_PORT = process.env.MYSQL_PORT ?? '3306';

// Databases that hold real work and must never be the target of this harness.
const PROTECTED_DATABASES = ['pems_db', 'pems_test', 'pems_pr3_test'];

if (PROTECTED_DATABASES.includes(DB)) {
  console.error(`Refusing to use a protected database name: ${DB}`);
  process.exit(2);
}

// State the target out loud, every run. The canonical script carries its own
// `CREATE DATABASE pems_db` / `USE pems_db` header, so "which database is this actually writing to"
// is not answerable by reading the command line — and getting it wrong resets a working database.
console.log(`[e2e] target database : ${DB}`);
console.log(`[e2e] protected (never touched): ${PROTECTED_DATABASES.join(', ')}`);

/**
 * Fail closed if the retarget missed anything.
 *
 * `replaceAll('pems_db', DB)` is only as good as the assumption that every reference is spelled
 * exactly that way. A backtick-quoted variant, a differently-cased one, or a new statement added to
 * the canonical script later would sail through and be executed against the real database — the
 * script's own `USE` wins over whatever database argument mysql was given.
 */
const assertRetargeted = (sqlText) => {
  const leaks = [
    [/CREATE\s+DATABASE[^;]*pems_db/i, 'CREATE DATABASE pems_db'],
    [/\bUSE\s+`?pems_db`?/i, 'USE pems_db'],
    [/\bpems_db\b/i, 'a bare pems_db reference'],
  ].filter(([re]) => re.test(sqlText)).map(([, label]) => label);

  if (leaks.length > 0) {
    console.error(
      `[e2e] ABORT — the retargeted script still references the production database:\n` +
      leaks.map(l => `        • ${l}`).join('\n') +
      `\n      Running it would write to pems_db regardless of the target argument.`);
    process.exit(3);
  }
  console.log('[e2e] retarget verified: no CREATE DATABASE / USE / bare reference to pems_db remains');
};

/** The canonical schema is ~80 tables; anything far below that means the import silently half-ran. */
const MIN_EXPECTED_TABLES = 70;

const workDir = mkdtempSync(join(tmpdir(), 'pems-e2e-'));
const publishDir = join(workDir, 'api');
const inbox = join(workDir, 'inbox.jsonl');
/**
 * Opt-in SMTP PICKUP mode (`PEMS_E2E_SMTP_PICKUP=1`).
 *
 * The default FileSink records the three envelope groups separately, which is the only way to show a
 * BCC address was ADDRESSED — no MIME message records that, by design. Pickup mode is its opposite
 * number: the real `EmailService` serialises each message to a real `.eml`, so the run can show a BCC
 * address appears in NO header. Both are needed, and no single artefact can carry both properties.
 *
 * `SmtpDeliveryMethod.SpecifiedPickupDirectory` writes files and never opens a connection — this is
 * still "no real SMTP, no real mail". Host/User/Password are blanked as well so that remains true even
 * if the dispatch path is changed later.
 */
const smtpPickup = process.env.PEMS_E2E_SMTP_PICKUP === '1';
const pickupDir = join(workDir, 'pickup');
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
  // Replace the seed db name and stream the master into mysql. The retargeted text is verified
  // BEFORE it reaches the server — after that it is too late to be careful.
  const sqlText = readFileSync(MASTER, 'utf8').replaceAll('pems_db', DB);
  assertRetargeted(sqlText);
  const r = spawnSync(MYSQL, [`-u${MYSQL_USER}`, `-p${MYSQL_PW}`, `-h${MYSQL_HOST}`, `-P${MYSQL_PORT}`,
    '--default-character-set=utf8mb4', DB], { input: sqlText, encoding: 'utf8', maxBuffer: 1 << 28 });
  if (r.status !== 0) throw new Error(`master import failed: ${r.stderr}`);
};

/**
 * Prove the import landed where it was supposed to, before a single test runs.
 *
 * A zero-table target is the signature of the script having executed somewhere else entirely: the
 * canonical file used to redirect itself with `USE pems_db`, so the target could be created, left
 * empty, and every table written to the production database instead. Counting tables here turns
 * that into an immediate abort rather than a confusing wall of test failures.
 */
const assertTargetPopulated = () => {
  const q = spawnSync(MYSQL, [`-u${MYSQL_USER}`, `-p${MYSQL_PW}`, `-h${MYSQL_HOST}`, `-P${MYSQL_PORT}`,
    '-N', '-B', '-e',
    `SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '${DB}'`],
    { encoding: 'utf8' });
  const tables = Number((q.stdout ?? '').trim());
  if (!Number.isFinite(tables) || tables < MIN_EXPECTED_TABLES) {
    throw new Error(
      `import verification failed: ${DB} has ${tables || 0} tables (expected >= ${MIN_EXPECTED_TABLES}). ` +
      `The script may have written somewhere else — check for an un-retargeted USE statement.`);
  }
  console.log(`[e2e] import verified: ${DB} has ${tables} tables`);
};

// Resolve the seeded identities (by stable email) and write the opaque-key → identity profile file. The
// browser only ever sends a profile KEY + the run secret; role/campus come from HERE, never a header.
const writeAuthProfiles = () => {
  // department_id is selected because the department flows (personnel management, logistics) resolve
  // scope from the DepartmentId claim — a profile without it authenticates but has no department, and
  // every department-scoped endpoint refuses it for the wrong reason.
  const q = spawnSync(MYSQL, [`-u${MYSQL_USER}`, `-p${MYSQL_PW}`, `-h${MYSQL_HOST}`, `-P${MYSQL_PORT}`,
    '-N', '-B', '--default-character-set=utf8mb4', DB, '-e',
    "SELECT u.user_id, u.email, r.role_code, COALESCE(u.sub_role,''), COALESCE(u.primary_campus_id,''), " +
    "COALESCE(u.department_id,'') " +
    "FROM users u JOIN roles r ON r.role_id = u.role_id WHERE u.email IN " +
    "('ho@fpt.edu.vn','staff.leader.hn@fpt.edu.vn','staff.leader.hcm@fpt.edu.vn','kim.minjae@seoultech.example'," +
    "'dept.leader.hn@fpt.edu.vn','dept.hn@fpt.edu.vn'," +
    "'facilities.leader.hn@fpt.edu.vn','facilities.staff.hn@fpt.edu.vn','staff.hn@fpt.edu.vn')"],
    { encoding: 'utf8' });
  if (q.status !== 0) throw new Error(`auth profile seed query failed: ${q.stderr}`);
  const byEmail = {};
  for (const line of q.stdout.trim().split('\n').filter(Boolean)) {
    const [userId, email, roleCode, subRole, campus, dept] = line.split('\t');
    byEmail[email] = {
      userId: Number(userId), email, roleCode,
      subRole: subRole || null, primaryCampusId: campus ? Number(campus) : null,
      departmentId: dept ? Number(dept) : null,
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
    pick('visitor_owner', 'kim.minjae@seoultech.example'),
    // Two GENERAL departments on campus 1, each with its own Leader and Staff. Two are needed rather
    // than one: cross-department refusal is only a real test when the other department actually exists
    // and is populated, otherwise "no data" and "correctly denied" look identical from the browser.
    pick('dept_leader_hn', 'dept.leader.hn@fpt.edu.vn'),           // Đào tạo HN
    pick('dept_staff_hn', 'dept.hn@fpt.edu.vn'),
    pick('facilities_leader_hn', 'facilities.leader.hn@fpt.edu.vn'), // Cơ sở vật chất HN
    pick('facilities_staff_hn', 'facilities.staff.hn@fpt.edu.vn'),
    // A second HN IC Staff member distinct from the campus Staff Leader -- needed for host-transfer
    // journeys: TransferVisitHostCommandHandler suppresses the notification to whichever side IS the
    // actor ("you already know"), and only the Staff Leader may call it, so a real incoming/outgoing
    // notification can only ever land on a DIFFERENT IC Staff member's own account.
    pick('staff_hn', 'staff.hn@fpt.edu.vn'),
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
  return profiles;
};

/**
 * Seat each GENERAL department's Leader in `departments.head_user_id`.
 *
 * The canonical seed leaves `head_user_id` NULL everywhere, but a Department Leader's authority is not
 * taken from their sub-role — `DepartmentLeaderPersonnelScopeService` re-reads the department and
 * refuses anyone who is not the *seated* head (403 DEPARTMENT_SCOPE_FORBIDDEN). Without this the
 * personnel journeys would all fail on authorization and prove nothing about the screens.
 *
 * This is ordinary application state, not a shortcut: a department with a seated head is what
 * /transfer-leadership produces. It is applied through UPDATE rather than by editing the canonical
 * script, so the schema file stays byte-identical to the one the SHA guard pins.
 */
const seedDepartmentHeads = (profiles) => {
  const heads = profiles.filter(p => p.roleCode === 'DEPARTMENT' && p.subRole === 'LEADER' && p.departmentId);
  for (const h of heads) {
    const r = mysql(
      `UPDATE departments SET head_user_id = ${h.userId} WHERE department_id = ${h.departmentId}`, DB);
    if (r.status !== 0) throw new Error(`department head seed failed for ${h.email}: ${r.stderr}`);
  }
  console.log(`[e2e] seated ${heads.length} department head(s)`);
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
  // Re-check the target at the moment of dropping. DB is a constant today, but a DROP is the one
  // statement here that cannot be taken back, and it should not depend on a check made 200 lines up.
  if (PROTECTED_DATABASES.includes(DB)) {
    console.error(`[e2e] refusing to drop a protected database: ${DB}`);
  } else {
    console.log(`[e2e] cleanup: dropping ${DB}`);
    mysql(`DROP DATABASE IF EXISTS \`${DB}\``);
  }
  try { rmSync(workDir, { recursive: true, force: true }); } catch { /* ignore */ }
}

process.on('SIGINT', () => { cleanup(); process.exit(130); });

let exitCode = 1;
try {
  console.log(`[e2e] create disposable DB ${DB} from master`);
  mysql(`DROP DATABASE IF EXISTS \`${DB}\`; CREATE DATABASE \`${DB}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`);
  importMaster();
  assertTargetPopulated();

  console.log('[e2e] write server-side auth profiles from actual seeded IDs');
  seedDepartmentHeads(writeAuthProfiles());

  console.log('[e2e] publish backend');
  // Build intermediates to a temp BaseOutputPath so a running dev server holding the repo bin/ lock never
  // blocks the publish (lets the harness run alongside `dotnet run` on another port).
  await run('dotnet', ['publish', API_PROJ, '-c', 'Debug', '-o', publishDir, '--nologo', '-v', 'q',
    `-p:BaseOutputPath=${join(workDir, 'binout')}\\`]);

  console.log(`[e2e] start backend on :${API_PORT} (Testing, flags ON, ` +
    `${smtpPickup ? `SMTP pickup -> ${pickupDir}` : 'FileSink'})`);
  writeFileSync(inbox, '');
  // The API's own output is discarded by default: it is noisy and the specs assert on the sink, not on
  // logs. Set PEMS_E2E_API_LOG to a path to keep it instead — without that, a 500 from the running host
  // shows up in a spec only as "INTERNAL_SERVER_ERROR" plus a trace id, and the stack trace that would
  // name the cause is thrown away with the process.
  const apiLog = process.env.PEMS_E2E_API_LOG;
  const apiLogFd = apiLog ? openSync(apiLog, 'w') : null;
  if (apiLog) console.log(`[e2e] backend log -> ${apiLog}`);

  backend = spawn('dotnet', [join(publishDir, 'PEMS.Api.dll')], {
    cwd: publishDir,
    stdio: apiLogFd === null ? 'ignore' : ['ignore', apiLogFd, apiLogFd],
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Testing',
      ASPNETCORE_URLS: `http://localhost:${API_PORT}`,
      ConnectionStrings__DefaultConnection: conn,
      PerCampusFormV2__Enabled: 'true',
      PerCampusFormV2Write__Enabled: 'true',
      Cors__AllowedOrigins__0: `http://localhost:${FE_PORT}`,
      ...(smtpPickup
        ? {
            // Real EmailService → real MIME on disk. No sink, no network: pickup returns before any
            // SmtpClient is constructed, and the credentials it would have used are blanked anyway.
            Smtp__Enabled: 'true',
            Smtp__PickupDirectory: pickupDir,
            // Host stays EMPTY so there is no server to reach even in principle. User/Password are
            // placeholders, not credentials: `SecretConfigurationValidator` requires both to be present
            // whenever SMTP is enabled, and `DispatchAsync` returns at the pickup branch before an
            // SmtpClient is ever constructed, so nothing here is read by anything.
            Smtp__Host: '',
            Smtp__User: 'pickup-mode-no-login',
            Smtp__Password: 'pickup-mode-no-login',
            Smtp__FromEmail: 'no-reply@pems.test',
            Smtp__FromName: 'PEMS',
          }
        : {
            Smtp__Enabled: 'false',
            PEMS_E2E_TEST_SINK_ENABLED: 'true',
          }),
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
  // Optional PEMS_E2E_GREP narrows the run to matching titles (debugging a subset); default = full suite.
  const grepArgs = process.env.PEMS_E2E_GREP ? ['--grep', process.env.PEMS_E2E_GREP] : [];
  // Extra Playwright flags, space-separated (e.g. PEMS_E2E_PW_ARGS="--repeat-each=3"). Without this the
  // only way to run a stability check was to edit the config, so "it passed three times" was never
  // something a reviewer could reproduce from a command line.
  const extraArgs = (process.env.PEMS_E2E_PW_ARGS ?? '').split(' ').filter(Boolean);
  await run('npx', ['playwright', 'test', '--config', 'playwright.realstack.config.ts', ...grepArgs, ...extraArgs], {
    cwd: join(REPO, 'frontend', 'pems-react'),
    env: {
      ...process.env,
      PEMS_E2E_TEST_SINK_PATH: inbox,
      PEMS_E2E_API_BASE: `http://localhost:${API_PORT}/api`,
      PEMS_E2E_FRONTEND_PORT: FE_PORT,
      // Set ONLY in pickup mode: the email specs read it to decide which witness they can assert on.
      ...(smtpPickup ? { PEMS_E2E_PICKUP_DIR: pickupDir } : {}),
      // Named explicitly so a read-only verification query can never be aimed at a different database
      // than the one this run created.
      PEMS_E2E_DB: DB,
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
