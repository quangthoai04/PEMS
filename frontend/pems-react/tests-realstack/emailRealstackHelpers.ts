/**
 * Helpers for the EMAIL real-stack journeys (G11-H): compose → preview → draft → reopen → send,
 * Reply, and Reply All.
 *
 * Same contract as the other real-stack helpers — real Chromium → real React → real .NET API →
 * disposable MySQL → a real dispatcher. NO network mocking, no real SMTP, no real mail.
 *
 * <b>Two dispatcher modes, because one file cannot answer both questions.</b>
 *
 *  - <b>sink</b> (default): `FileSinkEmailService` writes a JSON record per message carrying the three
 *    envelope groups separately. This is the only witness that can say a BCC address was actually
 *    ADDRESSED — a property no MIME message records, by definition.
 *  - <b>pickup</b> (`PEMS_E2E_PICKUP_DIR` set): the real `EmailService` serialises each message to a
 *    real `.eml` through `SmtpDeliveryMethod.SpecifiedPickupDirectory`. Those are the exact bytes an
 *    SMTP server would have received, which is the only witness that can say a BCC address appears in
 *    NO header. It never opens a connection.
 *
 * Running the same browser journeys in both modes is deliberate: "addressed" and "invisible" are
 * opposite properties, and a single artefact that showed both would prove neither.
 */
import { spawnSync } from 'node:child_process';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { expect, type Page } from '@playwright/test';

// ── Seeded identities used by the email journeys ───────────────────────────────────────────────
//
// Four distinct mailboxes are needed, not three: TO, CC and BCC must be different people, and the
// replier must be someone OTHER than the sender — `SentEmailAccess.CanOfferReply` refuses a reply to
// your own message, so a journey that composed and replied as one account would prove nothing.

export const HO = { key: 'ho_viewer', email: 'ho@fpt.edu.vn' } as const;
export const LEADER_HN = { key: 'campus_leader_hn', email: 'staff.leader.hn@fpt.edu.vn' } as const;
export const DEPT_LEADER = { key: 'dept_leader_hn', email: 'dept.leader.hn@fpt.edu.vn' } as const;
export const FACILITIES_LEADER = { key: 'facilities_leader_hn', email: 'facilities.leader.hn@fpt.edu.vn' } as const;

// ── Read-only SQL against the disposable database ──────────────────────────────────────────────

const PROTECTED_DATABASES = ['pems_db', 'pems_test', 'pems_pr3_test'];
const DB = process.env.PEMS_E2E_DB ?? 'pems_e2e_realstack';

/** Read-only query against the disposable database; refuses a protected name and refuses non-SELECT. */
export function queryDb(sql: string): string[][] {
  if (PROTECTED_DATABASES.includes(DB))
    throw new Error(`Refusing to query a protected database: ${DB}`);
  if (!/^\s*select\b/i.test(sql))
    throw new Error(`queryDb is read-only; refusing: ${sql.slice(0, 60)}`);

  const r = spawnSync(
    process.env.MYSQL_BIN ?? 'mysql',
    [
      `-u${process.env.MYSQL_USER ?? 'root'}`,
      `-p${process.env.MYSQL_PASSWORD ?? '123456'}`,
      `-h${process.env.MYSQL_HOST ?? 'localhost'}`,
      `-P${process.env.MYSQL_PORT ?? '3306'}`,
      '-N', '-B', '--default-character-set=utf8mb4', DB, '-e', sql,
    ],
    { encoding: 'utf8' },
  );
  if (r.status !== 0) throw new Error(`queryDb failed: ${r.stderr}`);
  // Split on \r?\n, not \n. mysql.exe ends every line with CRLF, so splitting on \n alone leaves a
  // stray \r on each row's LAST column — and `.trim()` on the whole output only cleans the final row.
  // The result is a helper that is exact for a one-row answer and silently wrong for a two-row one:
  // `recipient_type` came back as "TO\r" on every row but the last, so filtering for "TO" found
  // nothing while the data was perfectly correct.
  return (r.stdout ?? '').trim().split(/\r?\n/).filter(Boolean).map(line => line.split('\t'));
}

export function scalar(sql: string): string | null {
  const rows = queryDb(sql);
  return rows.length ? rows[0][0] : null;
}

/** `email → TYPE` for one sent message, straight out of `sent_email_recipients`. */
export function dbRecipients(sentEmailId: number): Array<{ email: string; type: string }> {
  return queryDb(
    `SELECT recipient_email, recipient_type FROM sent_email_recipients
      WHERE sent_email_id = ${sentEmailId} ORDER BY sent_email_recipient_id`,
  ).map(([email, type]) => ({ email, type }));
}

/** `email → TYPE` for one draft, straight out of `email_draft_recipients`. */
export function dbDraftRecipients(draftId: number): Array<{ email: string; type: string }> {
  return queryDb(
    `SELECT recipient_email, recipient_type FROM email_draft_recipients
      WHERE email_draft_id = ${draftId} ORDER BY display_order`,
  ).map(([email, type]) => ({ email, type }));
}

// ── Dispatcher witnesses ───────────────────────────────────────────────────────────────────────

export const PICKUP_DIR = process.env.PEMS_E2E_PICKUP_DIR;
export const SINK_PATH = process.env.PEMS_E2E_TEST_SINK_PATH;

/** Which dispatcher this run is exercising. Every mode-specific assertion is gated on it. */
export const MODE: 'pickup' | 'sink' = PICKUP_DIR ? 'pickup' : 'sink';

export interface Envelope {
  to: string[];
  cc: string[];
  bcc: string[];
  subject: string;
  body: string;
  /**
   * The serialised RFC 5322 message — headers and body as a recipient receives them (pickup mode
   * only). In pickup mode this deliberately EXCLUDES the `X-Sender`/`X-Receiver` preamble, which is
   * transport envelope and not part of the message. See {@link readPickupEnvelopes}.
   */
  raw: string | null;
  /** Transport-level recipients (pickup mode only): every address the message is being delivered to. */
  envelopeRcpt: string[];
}

interface SinkLine {
  to?: Array<{ email?: string }>;
  cc?: Array<{ email?: string }>;
  bcc?: Array<{ email?: string }>;
  subject?: string;
  body?: string;
}

const lower = (s: string) => s.trim().toLowerCase();

function readSinkEnvelopes(): Envelope[] {
  if (!SINK_PATH) throw new Error('PEMS_E2E_TEST_SINK_PATH is not set — the harness must provide it.');
  let lines: string[] = [];
  try { lines = readFileSync(SINK_PATH, 'utf8').split('\n').filter(Boolean); } catch { /* not written yet */ }

  const out: Envelope[] = [];
  for (const line of lines) {
    let parsed: SinkLine;
    try { parsed = JSON.parse(line) as SinkLine; } catch { continue; }
    const group = (g?: Array<{ email?: string }>) => (g ?? []).map(a => lower(a.email ?? '')).filter(Boolean);
    out.push({
      to: group(parsed.to),
      cc: group(parsed.cc),
      bcc: group(parsed.bcc),
      subject: parsed.subject ?? '',
      body: parsed.body ?? '',
      raw: null,
      envelopeRcpt: [...group(parsed.to), ...group(parsed.cc), ...group(parsed.bcc)],
    });
  }
  return out;
}

/**
 * Reads one header from a serialised message, honouring RFC 5322 folding — a continuation line starts
 * with whitespace and belongs to the header above it. Without unfolding, a long `Cc:` split across two
 * lines would read as a short one and the addresses on the continuation would silently go unasserted.
 */
function headerValue(raw: string, name: string): string | null {
  const headerBlock = raw.split(/\r?\n\r?\n/)[0] ?? '';
  const unfolded = headerBlock.replace(/\r?\n[ \t]+/g, ' ');
  for (const line of unfolded.split(/\r?\n/)) {
    const at = line.indexOf(':');
    if (at < 0) continue;
    if (line.slice(0, at).trim().toLowerCase() === name.toLowerCase()) return line.slice(at + 1).trim();
  }
  return null;
}

/** Pulls the bare addresses out of a header value, handling `Name <a@b>` and comma lists. */
function addressesIn(headerLine: string | null): string[] {
  if (!headerLine) return [];
  return Array.from(headerLine.matchAll(/[\w.!#$%&'*+/=?^`{|}~-]+@[\w-]+(?:\.[\w-]+)+/g))
    .map(m => lower(m[0]));
}

function readPickupEnvelopes(): Envelope[] {
  if (!PICKUP_DIR) throw new Error('PEMS_E2E_PICKUP_DIR is not set.');
  let files: string[] = [];
  try {
    // Ordered by write time, NOT by name: .NET names pickup files with a GUID, so a name sort is
    // arbitrary. Every "consider only messages after #n" assertion depends on this order being the
    // order they were sent in, and a name sort would quietly hand back a different run's message.
    files = readdirSync(PICKUP_DIR)
      .filter(f => f.toLowerCase().endsWith('.eml'))
      .map(f => ({ f, at: statSync(join(PICKUP_DIR, f)).mtimeMs }))
      .sort((a, b) => a.at - b.at || a.f.localeCompare(b.f))
      .map(x => x.f);
  } catch { /* directory not created until the first send */ }

  return files.map(f => {
    const file = readFileSync(join(PICKUP_DIR, f), 'utf8');

    // ── Split the transport envelope off the message ──────────────────────────────────────────
    //
    // Over a live connection the envelope travels in `MAIL FROM` / `RCPT TO` and never enters the
    // message. Pickup delivery has no connection, so .NET writes that same envelope as `X-Sender` and
    // one `X-Receiver` per recipient at the very top of the file, for the pickup service to consume
    // and strip before transmission.
    //
    // Those lines are therefore NOT something a recipient ever sees, and a blind copy appearing among
    // them is correct — it is the pickup-mode spelling of `RCPT TO`, and its absence would mean the
    // BCC was never delivered at all. Conflating it with a header leak would fail a system that is
    // behaving properly; ignoring the distinction the other way would let a genuine `Bcc:` header pass.
    const lines = file.split(/\r?\n/);
    let firstMessageLine = 0;
    const envelopeRcpt: string[] = [];
    while (firstMessageLine < lines.length && /^X-(Sender|Receiver):/i.test(lines[firstMessageLine])) {
      if (/^X-Receiver:/i.test(lines[firstMessageLine]))
        envelopeRcpt.push(...addressesIn(lines[firstMessageLine]));
      firstMessageLine++;
    }

    const raw = lines.slice(firstMessageLine).join('\n');
    const body = raw.split(/\r?\n\r?\n/).slice(1).join('\n\n');
    const to = addressesIn(headerValue(raw, 'To'));
    const cc = addressesIn(headerValue(raw, 'Cc'));
    const visible = new Set([...to, ...cc]);

    return {
      to,
      cc,
      // Derived exactly as a receiving server could: addressed by the transport, named in no header.
      // That is what "blind copy" means, and it is a stronger statement than reading a Bcc field —
      // there is no Bcc field to read.
      bcc: envelopeRcpt.filter(a => !visible.has(a)),
      subject: headerValue(raw, 'Subject') ?? '',
      body,
      raw,
      envelopeRcpt,
    };
  });
}

/** Every message the dispatcher has produced so far, oldest first. */
export const allEnvelopes = (): Envelope[] =>
  MODE === 'pickup' ? readPickupEnvelopes() : readSinkEnvelopes();

/** How many messages exist right now — take this BEFORE an action to scope the search after it. */
export const dispatchedCount = () => allEnvelopes().length;

/**
 * Waits for a message whose subject contains `marker`, considering only messages produced after
 * `since`. Every journey stamps its own run-unique marker into the subject, so two journeys (or two
 * runs against a re-used account) can never claim each other's mail.
 */
export async function waitForMessage(marker: string, since = 0, timeoutMs = 20_000): Promise<Envelope> {
  const deadline = Date.now() + timeoutMs;
  let seen: string[] = [];
  while (Date.now() < deadline) {
    const produced = allEnvelopes().slice(since);
    const hit = produced.find(e => e.subject.includes(marker));
    if (hit) return hit;
    seen = produced.map(e => e.subject);
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(
    `No message whose subject contains "${marker}" within ${timeoutMs}ms ` +
    `(mode=${MODE}, since=#${since}). Seen: ${seen.join(' | ') || '(nothing)'}`);
}

// ── Compose-modal driving ──────────────────────────────────────────────────────────────────────

/**
 * Types one address into a recipient field and commits it with Enter.
 *
 * The field is found by its visible label — 'Đến', 'CC', 'BCC' — which is what `RecipientChipInput`
 * renders through `RECIPIENT_GROUP_LABELS`. Deliberately NOT a test id: these journeys exist to prove
 * the screen a person uses keeps the three groups apart, and a label is what that person reads.
 */
export async function typeRecipient(page: Page, label: 'Đến' | 'CC' | 'BCC', value: string) {
  const field = page.getByLabel(label, { exact: true });
  await field.click();
  await field.fill(value);
  await field.press('Enter');
}

/**
 * Types an address that must be REFUSED, asserts the message and that no chip appeared, then clears
 * the box.
 *
 * Clearing is not tidying — it is what the person does. A refused address deliberately stays in the
 * box so it can be corrected rather than vanishing, and the field commits on blur; leaving it there
 * means the next click somewhere else re-offers it, and if the reason it was refused has gone by then
 * (the duplicate it clashed with was removed, say) it is accepted after all. That is correct
 * behaviour, and a test that walks away from a rejected value silently gets a recipient it never
 * asked for.
 */
export async function expectRecipientRefused(
  page: Page, label: 'Đến' | 'CC' | 'BCC', group: 'TO' | 'CC' | 'BCC',
  value: string, messageFragment: string, chipsBefore: string[],
) {
  await typeRecipient(page, label, value);
  await expect(page.getByRole('alert').filter({ hasText: messageFragment })).toBeVisible();
  await expectChips(page, group, chipsBefore, `"${value}" was refused and must not become a chip`);
  await page.getByLabel(label, { exact: true }).fill('');
}

/** The chips currently shown in one group, lower-cased. */
export async function chipsIn(page: Page, group: 'TO' | 'CC' | 'BCC'): Promise<string[]> {
  const chips = page.getByTestId(`chip-${group}`);
  const count = await chips.count();
  const out: string[] = [];
  for (let i = 0; i < count; i++) out.push(lower((await chips.nth(i).innerText()).replace(/×\s*$/, '')));
  return out;
}

/**
 * Asserts a group's chips, polling until they settle.
 *
 * A one-shot read races the render that follows Enter: it would report the list as it was an instant
 * before the commit, which fails as "the address was refused" — the opposite of what happened.
 */
export async function expectChips(page: Page, group: 'TO' | 'CC' | 'BCC', expected: string[], why?: string) {
  await expect
    .poll(async () => sorted(await chipsIn(page, group)), { message: why, timeout: 10_000 })
    .toEqual(sorted(expected));
}

/** The addresses the preview screen shows for one group, lower-cased. */
export async function previewGroup(page: Page, group: 'TO' | 'CC' | 'BCC'): Promise<string[]> {
  const block = page.getByTestId(`preview-${group}`);
  if (await block.count() === 0) return [];
  return (await block.innerText()).split('\n').map(lower).filter(Boolean);
}

/** Types the Quill body. Quill owns a contenteditable, so this types into it rather than filling it. */
export async function typeBody(page: Page, text: string) {
  const editor = page.locator('.ql-editor').first();
  await editor.click();
  await editor.fill(text);
}

/** Sorted lower-case copy, so an assertion never depends on the order a group happens to be built in. */
export const sorted = (values: string[]) => [...values].map(lower).sort();

/** Asserts an address appears in no part of a serialised message — headers, body, attachments alike. */
export function expectAbsentFromMessage(envelope: Envelope, address: string, why: string) {
  const source = envelope.raw ?? `${envelope.subject}\n${envelope.body}`;
  const needle = lower(address);
  // Name the offending lines. "expected false, received true" cannot distinguish a leak in a header
  // from one in the body, and those are different bugs with different fixes.
  const offenders = source
    .split(/\r?\n/)
    .map((line, i) => ({ line, i }))
    .filter(({ line }) => line.toLowerCase().includes(needle))
    .map(({ line, i }) => `line ${i + 1}: ${line.trim().slice(0, 160)}`);

  expect(offenders, `${why} — found ${address} at: ${offenders.join(' || ') || '(nowhere)'}`).toEqual([]);
}
