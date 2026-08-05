/**
 * The 31 rewritten email bodies, rendered by a REAL browser (V4 §16, §17, §24.4).
 *
 * <b>What this covers that nothing else does.</b> Every other test of this content runs in jsdom, which
 * implements no layout at all: it will happily report a 900px-wide table inside a 560px column as fine,
 * because it never computes a width. The failures §17 exists to prevent — a table that breaks out of the
 * frame on a phone, a column crushed to its longest syllable, borders that do not render — are all
 * layout failures, and a browser is the only thing that can see them.
 *
 * <b>The 560px column.</b> Bodies are rendered inside the same constraint the branded shell imposes on a
 * real send (max-width 560px, 32px padding). That measurement is duplicated from
 * EmailComposition.BrandedShell rather than imported, because the shell is C# — stated here so that if
 * the shell's width ever changes, this is a place that has to change with it.
 *
 * The content is read from the shipped defaults resource, so this measures what actually goes out rather
 * than a fixture that can drift away from it.
 */
import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const DEFAULTS = join(
  HERE, '..', '..', '..',
  'backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json');

interface Template {
  templateCode: string;
  subjectVi: string;
  bodyVi: string;
  subjectEn: string;
  bodyEn: string;
}

const templates: Template[] = JSON.parse(readFileSync(DEFAULTS, 'utf8').replace(/^﻿/, ''));

/** The shell's inner column. Duplicated from EmailComposition.BrandedShell — see the file comment. */
const SHELL_WIDTH = 560;
const SHELL_PADDING = 32;

/**
 * Sample values, deliberately LONG.
 *
 * A short value fits anywhere; the layout question is what a real Vietnamese delegation name or a
 * two-line description does to a fixed column. These are the lengths the product actually produces.
 */
const SAMPLES: Record<string, string> = {
  fullName: 'Nguyễn Thị Phương Thảo',
  recipientName: 'Nguyễn Thị Phương Thảo',
  personName: 'Nguyễn Thị Phương Thảo',
  contactFullName: 'Nguyễn Thị Phương Thảo',
  currentContactName: 'Trần Quốc Bảo Nguyên',
  assigneeName: 'Trần Quốc Bảo Nguyên',
  hostName: 'Trần Quốc Bảo Nguyên',
  departmentLeaderName: 'Trần Quốc Bảo Nguyên',
  requesterName: 'Trần Quốc Bảo Nguyên',
  successorName: 'Trần Quốc Bảo Nguyên',
  delegationName: 'Đoàn công tác Đại học Kyoto và Viện Nghiên cứu Công nghệ Thông tin',
  campusName: 'FPT University Thành phố Hồ Chí Minh',
  departmentName: 'Phòng Hành chính Tổng hợp và Quản trị Cơ sở vật chất',
  roleName: 'Cán bộ Hợp tác Quốc tế',
  oldRoleName: 'Cán bộ Hợp tác Quốc tế',
  newRoleName: 'Trưởng nhóm Hợp tác Quốc tế',
  roleLabel: 'Hỗ trợ phiên dịch và dẫn đoàn tham quan',
  scopeLabel: 'nhiệm vụ tiếp khách',
  logisticsTitle: 'Màn hình LED sảnh toà nhà Alpha, kích thước 4x3 mét',
  itemTitle: 'Màn hình LED sảnh toà nhà Alpha',
  logisticsItemType: 'Thiết bị trình chiếu',
  logisticsDescription: 'Chuẩn bị teabreak cho 20 khách, gồm trà, cà phê và nước suối. Bố trí trước giờ họp 15 phút.',
  proposedDescription: 'Đổi sang phòng họp Beta 204 do phòng Alpha đã có lịch trùng.',
  proposalNote: 'Phòng Alpha đã được đặt trước cho một sự kiện khác trong cùng khung giờ.',
  hostMessage: 'Rất mong bạn thu xếp tham gia cùng đoàn, buổi làm việc dự kiến kéo dài khoảng hai giờ.',
  reason: 'Điều chuyển công tác theo quyết định của Ban Giám hiệu ngày 01/08/2026.',
  oldEmailMasked: 'ngu***thao@fpt.edu.vn',
  requestCode: 'VR-2026-0042',
  otpCode: '000000',
  expireMinutes: '10',
  expiresInHours: '24',
  quantity: '20',
  originalQuantity: '20',
  proposedQuantity: '15',
  plannedTime: '09:00 - 11:30 ngày 20/08/2026',
  plannedStart: '09:00 20/08/2026',
  plannedEnd: '11:30 20/08/2026',
  usageStartAt: '08:00 20/08/2026',
  usageEndAt: '12:00 20/08/2026',
  proposedUsageStartAt: '08:30 20/08/2026',
  proposedUsageEndAt: '11:30 20/08/2026',
  dueAt: '17:00 18/08/2026',
  effectiveDate: '01/08/2026',
  periodFrom: '01/07/2026',
  periodTo: '31/07/2026',
  senderName: 'Phạm Minh Hoàng',
  senderRole: 'Chuyên viên Hợp tác Quốc tế',
  senderDepartment: 'Phòng Hợp tác Quốc tế',
  senderEmail: 'hoangpm@fpt.edu.vn',
  senderPhone: '0912 345 678',
  senderCampus: 'FPT University Hà Nội',
};

/** Stands in for the blocks the backend injects; shaped like the real ones, with no live link. */
const BLOCKS: Record<string, string> = {
  actionBlock:
    '<div style="text-align:center;margin:24px 0">'
    + '<span style="display:inline-block;background:#10b981;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px">Chấp nhận</span>'
    + '<span style="display:inline-block;background:#ef4444;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px">Từ chối</span>'
    + '</div>',
  setupSummaryBlock:
    '<table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%"'
    + ' style="border-collapse:collapse;width:100%;table-layout:fixed;font-size:13px;border:1px solid #374151">'
    + '<colgroup><col style="width:34%"/><col style="width:33%"/><col style="width:33%"/></colgroup><tbody>'
    + '<tr><td style="border:1px solid #374151;padding:8px">Họ tên</td>'
    + '<td style="border:1px solid #374151;padding:8px">Đơn vị</td>'
    + '<td style="border:1px solid #374151;padding:8px">Vai trò</td></tr>'
    + '<tr><td style="border:1px solid #374151;padding:8px">Tanaka Hiroshi Yamamoto</td>'
    + '<td style="border:1px solid #374151;padding:8px">Đại học Kyoto</td>'
    + '<td style="border:1px solid #374151;padding:8px">Trưởng đoàn</td></tr>'
    + '</tbody></table>',
};

function render(body: string): string {
  let html = body;
  for (const [name, value] of Object.entries({ ...SAMPLES, ...BLOCKS })) {
    html = html.split(`{{${name}}}`).join(value);
  }
  return html;
}

/** The shell's inner column, and nothing else — no app CSS, exactly as a mail client sees it. */
async function mount(page: Page, body: string, viewport: { width: number; height: number }) {
  await page.setViewportSize(viewport);
  await page.setContent(
    '<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">'
    + '<meta name="viewport" content="width=device-width,initial-scale=1"></head>'
    + '<body style="font-family:Arial,sans-serif;background:#f4f6f9;margin:0;padding:20px">'
    + `<div id="shell" style="max-width:${SHELL_WIDTH}px;margin:auto;background:#fff;border-radius:12px;overflow:hidden">`
    + `<div id="content" style="padding:${SHELL_PADDING}px;color:#374151;font-size:14px">${render(body)}</div>`
    + '</div></body></html>',
    { waitUntil: 'domcontentloaded' });
}

const MOBILE = { width: 375, height: 900 };
const DESKTOP = { width: 1024, height: 900 };

for (const t of templates) {
  for (const [lang, body] of [['VI', t.bodyVi], ['EN', t.bodyEn]] as const) {

    test(`${t.templateCode} ${lang} fits a phone`, async ({ page }) => {
      await mount(page, body, MOBILE);

      // The page must not scroll sideways. This is the §17 failure that matters most and the one no
      // jsdom test can see: a table wider than its column pushes the whole message out of frame, and the
      // reader gets a horizontal scrollbar over an email.
      const overflow = await page.evaluate(() =>
        document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow, `${lang} body overflows the viewport by ${overflow}px`).toBeLessThanOrEqual(0);

      // Nothing sticks out of the shell either — an element can overflow its container without making
      // the document scroll, and it is just as broken.
      const widest = await page.evaluate(() => {
        const content = document.getElementById('content')!;
        const limit = content.getBoundingClientRect().right;
        let worst = 0;
        for (const el of Array.from(content.querySelectorAll('*'))) {
          worst = Math.max(worst, el.getBoundingClientRect().right - limit);
        }
        return Math.round(worst);
      });
      expect(widest, `an element sticks ${widest}px past the content column`).toBeLessThanOrEqual(1);
    });

    test(`${t.templateCode} ${lang} renders mail-safe markup`, async ({ page }) => {
      await mount(page, body, DESKTOP);

      const report = await page.evaluate(() => {
        const content = document.getElementById('content')!;
        const problems: string[] = [];

        for (const table of Array.from(content.querySelectorAll('table'))) {
          const style = getComputedStyle(table);
          // §17 — without collapsed borders a mail client draws a double rule between every cell.
          if (style.borderCollapse !== 'collapse') problems.push('a table does not collapse its borders');
          // A table an email can use is nested in nothing and nests nothing.
          if (table.querySelector('table')) problems.push('a table contains another table');
        }

        // §17 — no flex or grid for layout: Outlook renders neither, and the message falls back to a
        // single stacked column with none of the spacing the design assumed.
        for (const el of Array.from(content.querySelectorAll('*'))) {
          const display = getComputedStyle(el).display;
          if (display === 'flex' || display === 'grid' || display === 'inline-flex') {
            problems.push(`an element uses display:${display}`);
          }
        }

        // Nothing hidden: a sender approving a preview must be approving everything the mail carries.
        for (const el of Array.from(content.querySelectorAll('*'))) {
          const s = getComputedStyle(el);
          if (s.display === 'none' || s.visibility === 'hidden' || Number(s.opacity) === 0) {
            problems.push('an element is invisible to the reader');
          }
        }

        // No placeholder survived substitution — one that did would ship as literal braces.
        //
        // Named rather than counted: "a placeholder remains" sends the reader through 31 templates
        // looking for it, and the usual cause is a missing entry in SAMPLES above, not a defect in
        // the content.
        const left = content.textContent?.match(/\{\{[a-zA-Z]+\}\}/g) ?? [];
        for (const name of new Set(left)) problems.push(`no value was substituted for ${name}`);

        return [...new Set(problems)];
      });

      expect(report, report.join('; ')).toEqual([]);
    });
  }
}

test('every summary table actually draws its borders', async ({ page }) => {
  // One representative body rather than all 31: this asserts the §16.4 table STYLE resolves in a
  // browser, which is the same markup everywhere. A border declared but not rendered is how the earlier
  // defect looked — the cells were all there and the grid had no lines.
  const t = templates.find((x) => x.templateCode === 'VISIT_PARTICIPANT_INVITATION')!;
  await mount(page, t.bodyVi, DESKTOP);

  const cells = await page.evaluate(() => {
    const rows = Array.from(document.querySelectorAll('#content table tr'));
    return rows.slice(0, -1).map((tr) => {
      const td = tr.querySelector('td')!;
      const s = getComputedStyle(td);
      return { bottom: s.borderBottomWidth, padding: s.paddingTop };
    });
  });

  expect(cells.length).toBeGreaterThan(0);
  for (const c of cells) {
    expect(c.bottom).not.toBe('0px');
    expect(c.padding).not.toBe('0px');
  }
});
