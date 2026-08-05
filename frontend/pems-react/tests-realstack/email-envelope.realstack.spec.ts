/**
 * REAL-STACK — G11-H §4: the three mandatory email journeys, end to end.
 *
 *   real Chromium → real React (Vite) → real .NET API (Testing, fail-closed E2E auth)
 *     → disposable MySQL → real dispatcher (file-sink or SMTP pickup) → real persistence/history
 *
 * NO network mocking, no real SMTP, no real mail.
 *
 * <b>Journey A</b> compose → preview → back to edit → preview again → send.
 * <b>Journey B</b> Reply.
 * <b>Journey C</b> Reply All.
 *
 * <b>There is no draft here, and that is the current design.</b> These journeys used to go
 * compose → autosave → reopen → edit → send, and the reopen was the point: it proved each address came
 * back in the group it was entered in. `bc4c6a54` removed drafts entirely — a composer opened on a
 * draft id that no longer existed showed a form full of generated text that looked exactly like a loaded
 * draft, and sending from it quietly created a NEW draft to send from. A composed message now lives in
 * the browser until it is sent.
 *
 * So the round trip these journeys exercise is the one that still exists: <b>preview</b>. It is a server
 * call that returns the sanitised message and the three groups as the send will use them, and the walk
 * back through "Quay lại sửa" and forward again is what proves a second look does not merge them. The
 * assertions about grouping, casing, duplicates and blind copies are unchanged — only the mechanism they
 * ride on moved, because the old one is gone.
 *
 * <b>Why a browser, when the API-level suites already pass.</b> Every property here is a property of a
 * HAND-OFF: the screen holds three groups, the draft endpoint stores which group each address came
 * from, reopening puts each address back where it was, and the dispatcher receives them still apart. An
 * integration test that posts a well-formed payload proves the server half and assumes the half where
 * the mistakes actually happen — a CC collected by the UI and flattened into TO on the way out is
 * invisible to it, because the payload it posts was never flattened.
 *
 * <b>The BCC address is the assertion.</b> `facilities.leader.hn@fpt.edu.vn` is blind-copied on the
 * original in Journey A and then hunted for everywhere it must not be: not in a header, not in the
 * body, not in what a TO or CC recipient can read back, and above all not in a Reply All. It is
 * addressed exactly once, and every later appearance would be a leak.
 */
import { test, expect, type Browser } from '@playwright/test';
import { authedPage, meUser } from './realstackHelpers';
import { asStoredUser } from './departmentRealstackHelpers';
import {
  DEPT_LEADER, FACILITIES_LEADER, HO, LEADER_HN, MODE,
  allEnvelopes, chipsIn, dbRecipients, dispatchedCount, expectAbsentFromMessage,
  expectChips, expectRecipientRefused, previewGroup, queryDb, scalar, sorted,
  typeBody, typeRecipient, waitForMessage,
} from './emailRealstackHelpers';

/** Run-unique marker stamped into every subject, so no journey can claim another's mail. */
const marker = () => `G11H-${Date.now().toString(36).toUpperCase()}-${Math.floor(Math.random() * 1e4)}`;

/** Opens Email Management as a profile and returns the page plus its context for cleanup. */
async function emailScreen(browser: Browser, request: Parameters<typeof meUser>[0], profileKey: string) {
  const me = await meUser(request, profileKey);
  const { context, page } = await authedPage(browser, profileKey, asStoredUser(me));
  await page.goto('/dashboard/email');
  return { context, page };
}

/** Every history row this run's marker produced, oldest first. */
const historyFor = (tag: string) =>
  queryDb(`SELECT sent_email_id, subject FROM sent_emails WHERE subject LIKE '%${tag}%' ORDER BY sent_email_id`);

/** The id of the ORIGINAL message this run sent, read back from the database by its subject marker. */
function sentEmailIdFor(tag: string): number {
  const rows = historyFor(tag);
  expect(rows.length, `no sent_emails row whose subject contains ${tag}`).toBeGreaterThan(0);
  return Number(rows[0][0]);
}

/**
 * The id of the reply produced for this marker.
 *
 * Asserts there is EXACTLY one original and one reply. Reaching for "the newest row that looks like a
 * reply" would pass just as happily on two replies, which is the shape a double-send takes — so the
 * count is part of what this proves, not a precondition for finding the row.
 */
/**
 * Every recipient row this marker produced, rendered for an assertion message.
 *
 * A recipient assertion that fails with `[]` says only "not what I expected" — it cannot distinguish
 * "the reply addressed the wrong people" from "I looked up the wrong message". Naming the whole set
 * makes the failure diagnose itself.
 */
const recipientDump = (tag: string) =>
  queryDb(
    `SELECT e.sent_email_id, e.subject, r.recipient_type, r.recipient_email
       FROM sent_emails e LEFT JOIN sent_email_recipients r ON r.sent_email_id = e.sent_email_id
      WHERE e.subject LIKE '%${tag}%' ORDER BY e.sent_email_id, r.sent_email_recipient_id`,
  ).map(([id, subject, type, email]) => `#${id} "${subject}" ${type}:${email}`).join(' | ') || '(no rows)';

function replyIdFor(tag: string): number {
  const rows = historyFor(tag);
  const summary = rows.map(([id, subject]) => `#${id} "${subject}"`).join(' | ') || '(none)';
  expect(rows.length, `expected exactly one original + one reply for ${tag}; got ${summary}`).toBe(2);
  expect(rows[1][1].startsWith('Re: '), `the second message must be the reply; got ${summary}`).toBe(true);
  return Number(rows[1][0]);
}

// ── Journey A — compose → preview → edit → preview → send ──────────────────────────────────────

test('Journey A — compose, preview, edit and send keeps TO/CC/BCC apart', async ({ browser, request }) => {
  const tag = marker();
  const { context, page } = await emailScreen(browser, request, HO.key);

  try {
    const before = dispatchedCount();
    await page.getByRole('button', { name: 'Soạn email' }).click();

    // ── A malformed address is refused at the field, and no chip is created for it.
    //
    //    Checked FIRST, on a clean field. `RecipientChipInput` resolves `externalError ?? localError`,
    //    so a form-level refusal parked on this group outranks the per-address one — asserting the
    //    per-address message after a failed submit would be reading a message the screen deliberately
    //    subordinates, not the one under test.
    await expectRecipientRefused(page, 'Đến', 'TO', 'khong-phai-email', 'không hợp lệ', []);

    // ── Whitespace and casing are normalised: this is typed padded and shouty, and must come out as
    //    one ordinary address — the same mailbox, recorded once.
    await typeRecipient(page, 'Đến', `   ${LEADER_HN.email.toUpperCase()}   `);
    await expectChips(page, 'TO', [LEADER_HN.email]);

    // ── The same mailbox again in a different case is a duplicate, not a second recipient.
    await expectRecipientRefused(
      page, 'Đến', 'TO', LEADER_HN.email.replace('staff', 'STAFF'), 'bị lặp', [LEADER_HN.email]);

    // ── CC, and the cross-group rule: one person belongs to exactly one group.
    await page.getByRole('button', { name: 'Thêm CC' }).click();
    await expectRecipientRefused(
      page, 'CC', 'CC', LEADER_HN.email, 'chỉ được thuộc một mục', []);
    await typeRecipient(page, 'CC', DEPT_LEADER.email);
    await expectChips(page, 'CC', [DEPT_LEADER.email]);

    // ── BCC.
    await page.getByRole('button', { name: 'Thêm BCC' }).click();
    await typeRecipient(page, 'BCC', FACILITIES_LEADER.email);
    await expectChips(page, 'BCC', [FACILITIES_LEADER.email]);

    // ── At least one TO — proven by taking the only TO away and being refused. CC and BCC are still
    //    populated here on purpose: "some recipients" must not be mistaken for "a valid envelope".
    await page.getByTestId('chip-TO').getByRole('button').click();
    await page.getByRole('button', { name: 'Xem trước' }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'ít nhất một người nhận' })).toBeVisible();
    await expect(page.getByText('Xem trước email'), 'a refused envelope must not reach preview').toHaveCount(0);
    // Re-entered SHOUTING, so the send below can show where casing is folded and where it is kept.
    await typeRecipient(page, 'Đến', LEADER_HN.email.toUpperCase());
    await expectChips(page, 'TO', [LEADER_HN.email]);

    await page.getByPlaceholder('Tiêu đề email…').fill(`${tag} thu moi hop`);
    await typeBody(page, `Noi dung ${tag}. Vui long xac nhan.`);

    // ── PREVIEW. A server call, not a local render: it returns the sanitised message and the three
    //    groups as the send will use them. This is the round trip the journey now turns on — the draft
    //    that used to provide one is gone.
    await page.getByRole('button', { name: 'Xem trước' }).click();
    await expect(page.getByText('Xem trước email')).toBeVisible();
    expect(await previewGroup(page, 'TO')).toEqual([LEADER_HN.email]);
    expect(await previewGroup(page, 'CC')).toEqual([DEPT_LEADER.email]);
    expect(await previewGroup(page, 'BCC')).toEqual([FACILITIES_LEADER.email]);

    // The body previewed is the body composed. The preview is what the author approves, so a preview
    // showing something else is the one failure this stage cannot be allowed to have.
    await expect(page.getByTestId('compose-preview-body')).toContainText(`Noi dung ${tag}`);

    // ── BACK TO EDIT. Nothing may be lost or merged by looking and returning: the groups the composer
    //    holds after a preview must still be the three it held before one.
    await page.getByRole('button', { name: 'Quay lại sửa' }).click();
    await expectChips(page, 'TO', [LEADER_HN.email], 'TO must survive a look at the preview');
    await expectChips(page, 'CC', [DEPT_LEADER.email], 'a CC must not come back as a TO');
    await expectChips(page, 'BCC', [FACILITIES_LEADER.email], 'a BCC must not come back as a CC');

    // ── Change the message, and preview again. The second preview must show the CHANGE — a cached
    //    first answer would let an author send one message while approving another.
    await page.getByPlaceholder('Tiêu đề email…').fill(`${tag} thu moi hop (da sua)`);

    await page.getByRole('button', { name: 'Xem trước' }).click();
    await expect(page.getByText('Xem trước email')).toBeVisible();
    await expect(page.getByTestId('compose-preview')).toContainText('da sua');
    expect(await previewGroup(page, 'BCC'), 'the blind copy survives a second preview')
      .toEqual([FACILITIES_LEADER.email]);

    // ── SEND, from the preview the author is looking at.
    await page.getByRole('button', { name: 'Xác nhận gửi' }).click();
    await page.getByRole('button', { name: 'Xác nhận', exact: true }).click();

    // ── What the dispatcher actually received.
    const message = await waitForMessage(tag, before);
    expect(message.subject).toContain('da sua');
    expect(sorted(message.to)).toEqual([LEADER_HN.email]);
    expect(sorted(message.cc)).toEqual([DEPT_LEADER.email]);

    // Both witnesses answer "was the blind copy addressed?" — the sink from the validated envelope,
    // pickup from the transport recipients minus everyone named in a header.
    expect(sorted(message.bcc), 'the BCC must have been addressed').toEqual([FACILITIES_LEADER.email]);
    expect(message.envelopeRcpt.includes(FACILITIES_LEADER.email), 'delivered to the blind copy').toBe(true);

    if (MODE === 'pickup') {
      // …and only the serialised message can answer "can the others see it?".
      expect(message.raw, 'pickup mode must produce a serialised message').not.toBeNull();
      expect(message.raw!.toLowerCase()).not.toContain('bcc:');
      expectAbsentFromMessage(
        message, FACILITIES_LEADER.email,
        'a blind copy must appear in no header, body or attachment of the message the others receive');
    }

    // ── History: one row, three typed recipients, and a status that is not invented.
    const sentEmailId = sentEmailIdFor(tag);
    const persisted = dbRecipients(sentEmailId);
    // Comparison is case-insensitive; storage is not. The address was entered SHOUTING and is kept
    // that way — "Ha.Nguyen@fpt.edu.vn" is the same mailbox as the lower-case form but is not the same
    // thing to show a person. What gets folded is the copy handed to the transport.
    expect(persisted.find(r => r.type === 'TO')!.email).toBe(LEADER_HN.email.toUpperCase());
    expect(sorted(persisted.filter(r => r.type === 'TO').map(r => r.email))).toEqual([LEADER_HN.email]);
    expect(sorted(persisted.filter(r => r.type === 'CC').map(r => r.email))).toEqual([DEPT_LEADER.email]);
    expect(sorted(persisted.filter(r => r.type === 'BCC').map(r => r.email))).toEqual([FACILITIES_LEADER.email]);

    // `delivered_at` must stay NULL: acceptance by a dispatcher is not delivery to a mailbox, and PEMS
    // has no webhook that could tell it otherwise.
    expect(scalar(`SELECT COUNT(*) FROM sent_emails WHERE sent_email_id = ${sentEmailId} AND delivered_at IS NOT NULL`))
      .toBe('0');
    expect(scalar(`SELECT COUNT(*) FROM sent_email_recipients WHERE sent_email_id = ${sentEmailId} AND delivery_status = 'DELIVERED'`))
      .toBe('0');

    // ── One message, not two. Sending used to go through a draft row, and the failure that removal was
    //    meant to end was a send creating a second record of itself; a run that produced two sent_emails
    //    for one press would satisfy every assertion above.
    expect(historyFor(tag), 'one press of send produces exactly one message').toHaveLength(1);
  } finally {
    await context.close();
  }
});

// ── Journey A′ — redaction, read through the real API as three different viewers ────────────────

test('Journey A2 — history shows a blind copy to its sender and to nobody else', async ({ browser, request }) => {
  const tag = marker();
  const { context, page } = await emailScreen(browser, request, HO.key);

  try {
    const before = dispatchedCount();
    await page.getByRole('button', { name: 'Soạn email' }).click();
    await typeRecipient(page, 'Đến', LEADER_HN.email);
    await page.getByRole('button', { name: 'Thêm CC' }).click();
    await typeRecipient(page, 'CC', DEPT_LEADER.email);
    await page.getByRole('button', { name: 'Thêm BCC' }).click();
    await typeRecipient(page, 'BCC', FACILITIES_LEADER.email);
    await page.getByPlaceholder('Tiêu đề email…').fill(`${tag} kiem tra che do an`);
    await typeBody(page, `Noi dung ${tag}.`);
    await page.getByRole('button', { name: 'Xem trước' }).click();
    // Waiting on the preview rather than on a stamp: it is a server call, so its arrival is the signal
    // that the message reached the API — which is what the removed autosave stamp used to report.
    await expect(page.getByTestId('compose-preview')).toBeVisible({ timeout: 15_000 });
    await page.getByRole('button', { name: 'Xác nhận gửi' }).click();
    await page.getByRole('button', { name: 'Xác nhận', exact: true }).click();

    await waitForMessage(tag, before);
    const id = sentEmailIdFor(tag);

    const detailAs = async (profileKey: string) => {
      const res = await request.get(
        `${process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api'}/Emails/viewemail?id=${id}&sourceType=ALL`,
        { headers: { 'X-E2E-Profile': profileKey, 'X-E2E-Secret': process.env.PEMS_E2E_AUTH_SECRET ?? '' } });
      return { status: res.status(), body: res.ok() ? await res.json() : null, text: await res.text().catch(() => '') };
    };

    // The sender chose the blind copy, so hiding it would hide their own act.
    const asSender = await detailAs(HO.key);
    expect(asSender.status).toBe(200);
    expect(asSender.body.recipients.map((r: any) => r.recipientEmail.toLowerCase()).sort())
      .toEqual(sorted([LEADER_HN.email, DEPT_LEADER.email, FACILITIES_LEADER.email]));

    // A TO recipient sees exactly the headers their own copy carried — and no hint that more exist.
    const asTo = await detailAs(LEADER_HN.key);
    expect(asTo.status).toBe(200);
    const toSees = asTo.body.recipients.map((r: any) => r.recipientEmail.toLowerCase());
    expect(sorted(toSees)).toEqual(sorted([LEADER_HN.email, DEPT_LEADER.email]));
    expect(JSON.stringify(asTo.body).toLowerCase(), 'no count, flag or address may betray the blind copy')
      .not.toContain(FACILITIES_LEADER.email);

    // The blind recipient sees the visible envelope plus their own entry, never another's.
    const asBcc = await detailAs(FACILITIES_LEADER.key);
    expect(asBcc.status).toBe(200);
    expect(sorted(asBcc.body.recipients.map((r: any) => r.recipientEmail.toLowerCase())))
      .toEqual(sorted([LEADER_HN.email, DEPT_LEADER.email, FACILITIES_LEADER.email]));
    expect(asBcc.body.recipients.find(
      (r: any) => r.recipientEmail.toLowerCase() === FACILITIES_LEADER.email)?.recipientType).toBe('BCC');

    // Someone with no relation to the message at all cannot open it by guessing its id.
    //
    // The stranger is the HCM campus leader on purpose. The refusal has to come from the RELATION —
    // not sender, not addressed, no linked object — so the viewer must first get PAST the controller's
    // role gate; a VISITOR would be refused for having the wrong role and prove nothing about this
    // rule. It is also the one seeded profile no other real-stack suite mutates: the department and
    // facilities accounts are the subject of the personnel journeys, which legitimately revoke their
    // sessions, and a 401 from a revoked session is a refusal that does not test authorization at all.
    const asStranger = await detailAs('campus_leader_hcm');
    expect(asStranger.status, `a stranger must be refused; body: ${asStranger.text}`).toBe(403);
  } finally {
    await context.close();
  }
});

// ── Journey B — Reply ──────────────────────────────────────────────────────────────────────────

test('Journey B — Reply answers the sender only and inherits nothing from the original', async ({ browser, request }) => {
  const tag = marker();

  // Precondition: a message with all three groups, composed through the screen in Journey A's shape.
  const composer = await emailScreen(browser, request, HO.key);
  const before = dispatchedCount();
  try {
    await composer.page.getByRole('button', { name: 'Soạn email' }).click();
    await typeRecipient(composer.page, 'Đến', LEADER_HN.email);
    await composer.page.getByRole('button', { name: 'Thêm CC' }).click();
    await typeRecipient(composer.page, 'CC', DEPT_LEADER.email);
    await composer.page.getByRole('button', { name: 'Thêm BCC' }).click();
    await typeRecipient(composer.page, 'BCC', FACILITIES_LEADER.email);
    await composer.page.getByPlaceholder('Tiêu đề email…').fill(`${tag} ban goc`);
    await typeBody(composer.page, `Noi dung goc ${tag}.`);
    await composer.page.getByRole('button', { name: 'Xem trước' }).click();
    await expect(composer.page.getByTestId('compose-preview')).toBeVisible({ timeout: 15_000 });
    await composer.page.getByRole('button', { name: 'Xác nhận gửi' }).click();
    await composer.page.getByRole('button', { name: 'Xác nhận', exact: true }).click();
    await waitForMessage(tag, before);
  } finally {
    await composer.context.close();
  }

  const originalId = sentEmailIdFor(tag);
  const afterOriginal = dispatchedCount();

  // The TO recipient replies — through their own browser, on their own screen.
  const me = await meUser(request, LEADER_HN.key);
  const { context, page } = await authedPage(browser, LEADER_HN.key, asStoredUser(me));
  try {
    await page.goto(`/dashboard/email/detail/RECEIVED/${originalId}`);
    // Waiting on the affordance itself, not on a caption: "the reply button is offered" is the
    // precondition this journey actually needs, and `canReply` is a server decision.
    await expect(page.getByRole('button', { name: 'Phản hồi', exact: true })).toBeVisible();

    // The screen a TO recipient sees must not mention the blind copy anywhere on it.
    expect((await page.locator('body').innerText()).toLowerCase())
      .not.toContain(FACILITIES_LEADER.email);

    await page.getByRole('button', { name: 'Phản hồi', exact: true }).click();

    // TO is the server's decision, shown and not editable — there is no TO field to type into.
    await expect(page.getByText('hệ thống xác định')).toBeVisible();
    expect(await page.getByLabel('Đến', { exact: true }).count(), 'a reply has no editable TO').toBe(0);

    await typeBody(page, `Phan hoi ${tag}.`);
    await page.getByRole('button', { name: 'Gửi', exact: true }).click();

    const reply = await waitForMessage(tag, afterOriginal);
    expect(reply.subject.startsWith('Re: ')).toBe(true);

    // Addressed to the original sender, and to nobody the original happened to carry.
    expect(sorted(reply.to)).toEqual([HO.email]);
    expect(reply.cc, 'a plain Reply must not inherit the original CC').toEqual([]);
    expectAbsentFromMessage(
      reply, FACILITIES_LEADER.email, 'a reply must never resurrect the original blind copy');

    const replyId = replyIdFor(tag);
    const rows = dbRecipients(replyId);
    expect(sorted(rows.map(r => r.email)), 'exactly one recipient row').toEqual([HO.email]);
    expect(rows[0].type).toBe('TO');

    // The reply points at its parent, so the thread is a fact rather than a guess.
    expect(scalar(`SELECT COUNT(*) FROM sent_emails WHERE sent_email_id = ${replyId} AND provider_thread_id IS NOT NULL`))
      .toBe('1');
  } finally {
    await context.close();
  }
});

// ── Journey C — Reply All ──────────────────────────────────────────────────────────────────────

test('Journey C — Reply All reaches the visible recipients and never the blind ones', async ({ browser, request }) => {
  const tag = marker();

  const composer = await emailScreen(browser, request, HO.key);
  const before = dispatchedCount();
  try {
    await composer.page.getByRole('button', { name: 'Soạn email' }).click();
    await typeRecipient(composer.page, 'Đến', LEADER_HN.email);
    await composer.page.getByRole('button', { name: 'Thêm CC' }).click();
    await typeRecipient(composer.page, 'CC', DEPT_LEADER.email);
    await composer.page.getByRole('button', { name: 'Thêm BCC' }).click();
    await typeRecipient(composer.page, 'BCC', FACILITIES_LEADER.email);
    await composer.page.getByPlaceholder('Tiêu đề email…').fill(`${tag} ban goc tra loi tat ca`);
    await typeBody(composer.page, `Noi dung goc ${tag}.`);
    await composer.page.getByRole('button', { name: 'Xem trước' }).click();
    await expect(composer.page.getByTestId('compose-preview')).toBeVisible({ timeout: 15_000 });
    await composer.page.getByRole('button', { name: 'Xác nhận gửi' }).click();
    await composer.page.getByRole('button', { name: 'Xác nhận', exact: true }).click();
    await waitForMessage(tag, before);
  } finally {
    await composer.context.close();
  }

  const originalId = sentEmailIdFor(tag);
  const afterOriginal = dispatchedCount();

  const me = await meUser(request, LEADER_HN.key);
  const { context, page } = await authedPage(browser, LEADER_HN.key, asStoredUser(me));
  try {
    await page.goto(`/dashboard/email/detail/RECEIVED/${originalId}`);

    // Watch what the browser actually posts. The claim being tested is not only "the right people were
    // addressed" but "the client never named them" — a client that could name recipients could name one
    // who had been on BCC, and the server would have no way to tell that from a CC.
    let posted: any = null;
    let postedUrl = '';
    page.on('request', req => {
      if (req.method() === 'POST' && /replyalltoemail/i.test(req.url())) {
        postedUrl = req.url();
        try { posted = JSON.parse(req.postData() ?? '{}'); } catch { posted = {}; }
      }
    });

    await page.getByTestId('reply-all').click();
    await typeBody(page, `Phan hoi tat ca ${tag}.`);
    await page.getByRole('button', { name: 'Gửi', exact: true }).click();

    const reply = await waitForMessage(tag, afterOriginal);

    // The wire: a mode expressed by the ROUTE, and no recipient list of any kind.
    expect(postedUrl, 'Reply All must use its own route').toContain('replyalltoemail');
    expect(posted, 'the reply-all request must have been observed').not.toBeNull();
    expect(posted.to, 'the client must not name a TO').toBeUndefined();
    expect(posted.cc ?? [], 'the client sent no CC of its own here').toEqual([]);
    expect(posted.bcc ?? [], 'the client sent no BCC of its own here').toEqual([]);

    // The server's plan: the original sender, plus the original's VISIBLE recipients, minus the replier.
    expect(sorted(reply.to), 'sender plus original TO, self excluded').toEqual([HO.email]);
    expect(sorted(reply.cc), "the original's CC is carried; its BCC is not").toEqual([DEPT_LEADER.email]);
    expect(reply.to.includes(LEADER_HN.email), 'the replier must not be addressed').toBe(false);
    expect(reply.cc.includes(LEADER_HN.email), 'the replier must not be copied').toBe(false);

    expectAbsentFromMessage(
      reply, FACILITIES_LEADER.email,
      'Reply All must never expose who was blind-copied on the message it answers');

    const replyId = replyIdFor(tag);
    const rows = dbRecipients(replyId);
    const dump = `reply #${replyId} of ${recipientDump(tag)}`;
    expect(sorted(rows.filter(r => r.type === 'TO').map(r => r.email)), dump).toEqual([HO.email]);
    expect(sorted(rows.filter(r => r.type === 'CC').map(r => r.email)), dump).toEqual([DEPT_LEADER.email]);
    expect(rows.filter(r => r.type === 'BCC'), 'a Reply All writes no blind copies of its own').toHaveLength(0);
    expect(
      rows.some(r => r.email.toLowerCase() === FACILITIES_LEADER.email),
      'the blind copy must not reach the reply history either').toBe(false);

    // Nothing anywhere in the run's history rows re-addresses the blind copy after the original.
    const everywhere = queryDb(
      `SELECT r.recipient_email, r.recipient_type, e.subject FROM sent_email_recipients r
         JOIN sent_emails e ON e.sent_email_id = r.sent_email_id
        WHERE e.subject LIKE '%${tag}%'`);
    const blindRows = everywhere.filter(([email]) => email.toLowerCase() === FACILITIES_LEADER.email);
    expect(blindRows, 'the blind copy is addressed exactly once, on the original').toHaveLength(1);
    expect(blindRows[0][1]).toBe('BCC');
    expect(blindRows[0][2]).not.toContain('Re: ');
  } finally {
    await context.close();
  }
});

// ── Cross-cutting: what the dispatcher never emitted ───────────────────────────────────────────

test('No message produced by this run carries a Bcc header', async () => {
  test.skip(MODE !== 'pickup', 'only a serialised message can be searched for headers');

  const produced = allEnvelopes();
  expect(produced.length, 'the run must have produced messages to inspect').toBeGreaterThan(0);
  for (const message of produced) {
    // Proves the preamble was recognised rather than the whole file being read as "message" — a parse
    // that silently found no envelope would make the assertion below pass for the wrong reason.
    expect(message.envelopeRcpt.length, 'every pickup file carries its transport envelope')
      .toBeGreaterThan(0);
    expect(message.raw!.toLowerCase().split(/\r?\n\r?\n/)[0], 'no header block may contain Bcc:')
      .not.toContain('bcc:');
  }
});
