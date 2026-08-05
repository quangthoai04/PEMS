/**
 * REAL-STACK — what the eye icon and the final preview actually put on screen, in a real browser.
 *
 *   real Chromium → real .NET API (Testing, fail-closed E2E auth) → disposable MySQL
 *
 * <b>Why a browser, when the integration suite already compares these strings.</b> Everything asserted
 * here is a property of RENDERED output: whether the buttons sit inside the message or after its footer,
 * whether they can be pressed, whether the branded card holds together at phone width. jsdom computes no
 * layout and follows no link, so it can report a message that overflows its frame, or an action area
 * that navigates, as perfectly fine.
 *
 * <b>The HTML is fetched from the running API, never rebuilt here.</b> A fixture would drift, and a
 * second copy of the assembly logic in this file would be the exact defect the shared composer was
 * written to end — a preview assembled by rules the send does not use.
 *
 * The four flows named in the plan: logistics request (teabreak), participant invitation, department
 * staff assignment, and the setup-progress update — which has no action area at all, and is here to
 * prove the composer does not invent one.
 */
import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { API_BASE, hdr } from './realstackHelpers';
import { HO } from './emailRealstackHelpers';

/** The shell's inner column, duplicated from EmailComposition.BrandedShell (C#, cannot be imported). */
const SHELL_WIDTH = 560;

/** A phone. The narrowest thing that reads this mail. */
const PHONE = { width: 375, height: 900 };

interface Preview {
  templateCode: string;
  subject: string;
  editableBodyHtml: string;
  initialFinalPreviewHtml: string;
  isActionTemplate: boolean;
  replyToEmail?: string | null;
  runtimeEditable: boolean;
  previewToken?: string | null;
}

/**
 * Real variable values for each flow, in the lengths the product actually produces.
 *
 * Sample mode is deliberately NOT used: a preview that quietly fills its own gaps is right for an
 * operator editing wording and wrong here, where the point is to look at a message somebody would send.
 */
const FLOWS: Array<{ label: string; templateCode: string; context: Record<string, string> }> = [
  {
    label: 'logistics request (teabreak)',
    templateCode: 'LOGISTICS_REQUEST_TO_DEPARTMENT',
    context: {
      departmentLeaderName: 'Nguyễn Thị Phương Thảo',
      requesterName: 'Trần Quốc Bảo Nguyên',
      logisticsTitle: 'Teabreak cho 20 khách tại sảnh toà nhà Alpha',
      logisticsItemType: 'Dịch vụ ăn uống',
      quantity: '20 phần',
      usageStartAt: '08:45 12/08/2026',
      usageEndAt: '09:15 12/08/2026',
      logisticsDescription:
        'Chuẩn bị teabreak cho 20 khách, gồm trà, cà phê và nước suối. Bố trí trước giờ họp 15 phút.',
    },
  },
  {
    label: 'participant invitation',
    templateCode: 'VISIT_PARTICIPANT_INVITATION',
    context: {
      recipientName: 'Nguyễn Thị Phương Thảo',
      delegationName: 'Đoàn công tác Đại học Kyoto và Viện Nghiên cứu Công nghệ Thông tin',
      campusName: 'FPT University Thành phố Hồ Chí Minh',
      plannedTime: '09:00 12/08/2026 - 11:30 12/08/2026',
      hostName: 'Trần Quốc Bảo Nguyên',
      roleLabel: 'Hỗ trợ phiên dịch và dẫn đoàn tham quan',
      hostMessage: 'Nhờ anh/chị hỗ trợ phần đón tiếp và dẫn đoàn tham quan khuôn viên.',
    },
  },
  {
    label: 'department staff assignment',
    templateCode: 'VISIT_DEPARTMENT_STAFF_ASSIGNMENT',
    context: {
      recipientName: 'Nguyễn Thị Phương Thảo',
      delegationName: 'Đoàn công tác Đại học Kyoto và Viện Nghiên cứu Công nghệ Thông tin',
      campusName: 'FPT University Thành phố Hồ Chí Minh',
      plannedTime: '09:00 12/08/2026 - 11:30 12/08/2026',
      departmentName: 'Phòng Hành chính Tổng hợp và Quản trị Cơ sở vật chất',
    },
  },
];

/** The one flow with no action area, kept separate because its expectations are the opposite. */
const NO_ACTION_FLOW = {
  label: 'setup-progress update',
  templateCode: 'VISIT_SETUP_PROGRESS_UPDATE',
  context: {
    delegationName: 'Đoàn công tác Đại học Kyoto và Viện Nghiên cứu Công nghệ Thông tin',
    campusName: 'FPT University Thành phố Hồ Chí Minh',
    plannedStart: '09:00 12/08/2026',
    plannedEnd: '11:30 12/08/2026',
    hostName: 'Trần Quốc Bảo Nguyên',
  },
};

async function prepare(
  request: APIRequestContext, templateCode: string, context: Record<string, string>,
): Promise<Preview> {
  const res = await request.post(`${API_BASE}/email-templates/preview`, {
    headers: hdr(HO.key),
    data: { templateCode, context, language: 'VI', visitInstanceId: 1 },
  });
  expect(res.ok(), `preview ${templateCode} failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

/**
 * Loads assembled email HTML into a real page.
 *
 * `document.write` rather than `page.setContent`, because the assembled message is a WHOLE document —
 * doctype, head, body — and setContent would nest it inside one Playwright supplies.
 */
async function show(page: Page, html: string) {
  await page.setViewportSize(PHONE);
  await page.goto('about:blank');
  await page.evaluate((markup) => {
    document.open();
    document.write(markup);
    document.close();
  }, html);
}

/** The rendered rectangle of the first element whose text matches, or null. */
const boxOf = (page: Page, text: string) =>
  page.locator(`text=${text}`).first().boundingBox();

for (const flow of FLOWS) {
  test(`Initial VIEW — ${flow.label} renders as a whole email with its buttons in place`, async ({ page, request }) => {
    const preview = await prepare(request, flow.templateCode, flow.context);

    expect(preview.isActionTemplate, `${flow.templateCode} should carry an action area`).toBe(true);
    expect(preview.initialFinalPreviewHtml, 'the first preview must arrive assembled').toBeTruthy();

    await show(page, preview.initialFinalPreviewHtml);

    // ── The branded shell is there, and it is the message — not a bare body.
    await expect(page.getByRole('heading', { name: /PEMS — Campus Visit/ })).toBeVisible();
    await expect(page.locator('body')).toContainText('FPT University');
    await expect(page.locator('body')).toContainText('Không trả lời email này');

    // ── The action buttons are INSIDE the message: above the footer, and after the greeting.
    const buttons = page.locator('span', { hasText: /^(Chấp nhận|Từ chối|Xác nhận|Xem chi tiết|Mở yêu cầu|Đã chuẩn bị xong)/ });
    expect(await buttons.count(), 'no action buttons were rendered').toBeGreaterThan(0);

    const firstButton = await buttons.first().boundingBox();
    const footer = await boxOf(page, 'Không trả lời email này');
    expect(firstButton, 'the action buttons have no box, so they are not displayed').not.toBeNull();
    expect(footer).not.toBeNull();
    expect(firstButton!.y, 'the buttons must sit inside the message, above its footer')
      .toBeLessThan(footer!.y);

    // ── Nothing here is pressable. A preview that navigates is a preview a sender can answer their own
    //    message with; a preview holding a real token is a credential in every screenshot of it.
    expect(await page.locator('a').count(), 'a preview must contain no links at all').toBe(0);
    const html = await page.content();
    expect(html).not.toContain('/public/email-actions/');

    // Clicking one really does nothing — asserted by clicking, not by reasoning about spans.
    const before = page.url();
    await buttons.first().click();
    await page.waitForTimeout(150);
    expect(page.url(), 'pressing a preview button must not navigate').toBe(before);

    // ── It holds together on a phone: nothing may spill out of the frame sideways.
    const overflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, 'the message overflows a 375px screen horizontally').toBeLessThanOrEqual(1);

    // ── And the card itself keeps the shell's column on a wide screen.
    await page.setViewportSize({ width: 1280, height: 900 });
    const card = await page.locator('div[style*="max-width:560px"]').first().boundingBox();
    expect(card, 'the branded card is missing').not.toBeNull();
    expect(Math.round(card!.width)).toBeLessThanOrEqual(SHELL_WIDTH);
  });

  test(`Final preview — ${flow.label} keeps the buttons where the sender moved them`, async ({ page, request }) => {
    const preview = await prepare(request, flow.templateCode, flow.context);
    test.skip(!preview.runtimeEditable || !preview.previewToken,
      `${flow.templateCode} is not runtime-editable, so it has no final preview`);

    // The sender writes around the node, leaving it in the MIDDLE — the position the assembled preview
    // then has to honour.
    const edited =
      '<p>MO-DAU xin anh chi chon mot phuong an ben duoi</p>'
      + '<div data-system-block="action"></div>'
      + '<p>KY-TEN Tran Quoc Bao Nguyen</p>';

    const res = await request.post(`${API_BASE}/email-templates/final-preview`, {
      headers: hdr(HO.key),
      data: {
        previewToken: preview.previewToken,
        subject: preview.subject,
        editableBodyHtml: edited,
        language: 'VI',
      },
    });
    expect(res.ok(), `final-preview failed: ${res.status()} ${await res.text()}`).toBeTruthy();
    const final = await res.json();

    await show(page, final.finalPreviewHtml);

    const intro = await boxOf(page, 'MO-DAU');
    const signature = await boxOf(page, 'KY-TEN');
    const buttons = page.locator('span', { hasText: /^(Chấp nhận|Từ chối|Xác nhận|Xem chi tiết|Mở yêu cầu|Đã chuẩn bị xong)/ });
    const block = await buttons.first().boundingBox();

    expect(intro, 'the sender opening sentence is missing').not.toBeNull();
    expect(signature, 'the sender signature is missing').not.toBeNull();
    expect(block, 'the action block is missing').not.toBeNull();

    // Vertical order on a real page, not string offsets: this is the claim a reader can check.
    expect(intro!.y).toBeLessThan(block!.y);
    expect(block!.y).toBeLessThan(signature!.y);

    // Still inert, still no credential — an edit must not turn the preview live.
    expect(await page.locator('a').count()).toBe(0);
    expect(await page.content()).not.toContain('/public/email-actions/');
  });
}

/**
 * A message with no action area gets none invented for it.
 *
 * This is the failure a composer that appends buttons "just in case" would ship: two dead spans at the
 * end of a progress update that never had any, which every other assertion in this file would pass.
 */
test(`Initial VIEW — ${NO_ACTION_FLOW.label} shows no action area at all`, async ({ page, request }) => {
  const preview = await prepare(request, NO_ACTION_FLOW.templateCode, NO_ACTION_FLOW.context);

  expect(preview.isActionTemplate, `${NO_ACTION_FLOW.templateCode} should have no action area`).toBe(false);

  await show(page, preview.initialFinalPreviewHtml);

  await expect(page.getByRole('heading', { name: /PEMS — Campus Visit/ })).toBeVisible();
  await expect(page.locator('body')).toContainText('Không trả lời email này');

  const html = await page.content();
  expect(html).not.toContain('PEMS_ACTION_BLOCK');
  expect(html).not.toContain('Chấp nhận');
  expect(html).not.toContain('Từ chối');
  expect(await page.locator('a').count()).toBe(0);

  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
});
