---
type: decision-log
status: open
updated: 2026-07-29
links:
  - docs/email-standardization/04-requirement-test-traceability.md
  - docs/email-standardization/07-g11-residual-technical-closure.md
  - docs/permissions/PERMISSION_MATRIX.md
  - docs/use-cases/USE_CASE_LIST.md
---

# Open product decisions — R-104 and R-105

Two residual items from G9 that G11 deliberately did **not** implement. Neither is blocked on effort or
on technique; both are blocked on a decision that belongs to the product owner, and guessing at either
would put an answer in the code that nobody actually gave.

Everything below was re-measured at HEAD `c39e6f04` on 2026-07-29, not copied forward from the G9 report.

---

## R-104 — Three dashboard endpoints with no role contract

**Status: BLOCKED — awaiting owner role / UC-ID / metric decision.**

### What exists in code

| Route | Handler | Authorization |
|---|---|---|
| `GET api/reports/viewdashboardstatistics` | `ViewDashboardStatisticsQueryHandler` — `throw new NotImplementedException(...)` | class-level `[Authorize]` only |
| `POST api/reports/exportstatisticsreport` | `ExportStatisticsReportCommandHandler` — `throw new NotImplementedException(...)` | class-level `[Authorize]` only |
| `GET api/reports/filterdashboardbytime` | `FilterDashboardByTimeQueryHandler` — `throw new NotImplementedException(...)` | class-level `[Authorize]` only |

Every other route on `ReportsController` carries an explicit `[RoleAuthorize(...)]`. These three do not.
They are not anonymous — the controller's `[Authorize]` refuses an unauthenticated caller — but any
authenticated user of any role reaches the handler, which then throws.

There is **no frontend caller** for any of the three: measured across the whole of
`frontend/pems-react/src`, zero occurrences of the three route names.

### Why it cannot be resolved by reading the documents

The three documents that should agree, disagree — about the role list *and* about the UC numbers.

| Source | UC IDs | Roles |
|---|---|---|
| `docs/permissions/PERMISSION_MATRIX.md` §5.11 | **UC-69 / 70 / 71** | HO · Staff Leader · **Department Lead** |
| `docs/use-cases/USE_CASE_LIST.md` | **UC-69 / 70 / 71** | (list only — no roles) |
| `docs/PROJECT_OVERVIEW…md` §5 "Major features" FE-08 | **UC-66 / 67 / 68** | HO · Staff Leader |

And the numbering collides in both directions:

- `PROJECT_OVERVIEW` itself uses **UC-69/70/71** elsewhere for *Calendar* (View My Events, View
  Department Calendar, Switch View Mode).
- `USE_CASE_LIST` uses **UC-66/67/68** for *FAQ* (Update FAQ, Change FAQ Visibility, Search FAQ).

So whichever numbering is adopted, one of the two documents is describing different use cases with the
same identifiers. This is a numbering decision, not a typo to be quietly corrected.

`USE_CASE_LIST.md` line 32 additionally states that `ReportsController` "hiện **không có
authorization** — gọi được ẩn danh". That is no longer true of the code: the class carries `[Authorize]`.
It is noted here as evidence rather than edited, because R-104 is where that whole paragraph gets
settled.

### Decision table

| | Option A | Option B |
|---|---|---|
| **Actors** | HO + Staff Leader | HO + Staff Leader + Department Lead |
| **Scope** | system-wide (HO) / campus (Staff Leader) | system-wide / campus / **department** |
| **Matches** | `PROJECT_OVERVIEW` FE-08 | `PERMISSION_MATRIX` §5.11 + `USE_CASE_LIST` |
| **In its favour** | Least privilege. Every metric these endpoints could return already has a campus-or-wider owner, so no new scoping rule is needed. | The permission matrix is the document the rest of the codebase's `[RoleAuthorize]` attributes were derived from; diverging from it here creates a second source of truth for roles. |
| **Against it** | Contradicts the permission matrix, which is the authority for role questions in every other module. | Requires department-scoped versions of every dashboard metric, which do not exist and are not specified. A Department Lead calling a campus-wide statistic would either over-disclose or need a parallel calculation. |
| **Code affected** | `ReportsController` ×3 attributes | `ReportsController` ×3 attributes, plus scope resolution in all three handlers, plus department variants of every metric |

### What is still missing even after the role decision

Choosing A or B does not make the handlers implementable. Also undecided:

1. **Which metrics** `viewdashboardstatistics` returns — the DTO is a scaffold with no fields agreed.
2. **What "statistics" means** relative to the four report screens that already exist
   (`ho-report-v2`, `staff-leader-report-v2`, `dept-leader-report-v2`, and the three `*-overview`
   routes). If it is a fifth view of the same numbers, the duplication is the decision.
3. **Export format(s)** for `exportstatisticsreport` — the other export routes offer PDF/EXCEL/CSV;
   whether this one does, and whether it archives to Drive like `ReportArchiveService` does, is unstated.
4. **What `filterdashboardbytime` adds** over the `preset` + `fromDate`/`toDate` filters the existing
   report queries already accept.
5. Whether any of the three sends email at all. **If a future version of `exportstatisticsreport` mails
   its output, it must be added to the six actions in `07-g11-residual-technical-closure.md` §1.1 and
   declare `IIdempotentEmailSend`** — otherwise it ships with the R-103 defect that G11 just closed.

### What G11 did NOT do, and why

- Did **not** add `[RoleAuthorize]` to any of the three. Picking a role list would settle the
  matrix-versus-overview conflict by implementation, which is the one way a documentation conflict must
  never be resolved.
- Did **not** implement the handlers.
- Did **not** renumber any UC in any document.

---

## R-105 — Two invoice routes with no frontend caller

**Status: BLOCKED — awaiting owner UX decision.**

### What exists in code

| Route | Command | Recipient (resolved by backend) |
|---|---|---|
| `POST api/reports/staff-leader-report-v2/departments/{departmentId}/send-invoice` | `SendStaffLeaderDeptInvoiceCommand` | head of the named department, within the caller's campus |
| `POST api/reports/dept-leader-report-v2/send-invoice` | `SendDeptLeaderInvoiceToStaffLeaderCommand` | Staff Leader of the caller's campus |

Both are fully implemented, authorized, scoped, tested — and unreachable from the product. Both are
**defined** in `frontend/pems-react/src/features/reports/api/reportsApi.ts` and called from nowhere:
measured across the whole of `frontend/pems-react/src`, the only occurrences of either function name are
the definitions themselves.

### Where the UI would go, if it goes anywhere

The panels the actions would belong to already exist:

- **Dept Leader → Staff Leader.** `DeptReportManagement.tsx` §3 "xuất hóa đơn" already loads completed
  logistics lines for a date range (`getDeptLeaderInvoiceItemsV2`), totals them, and exports a PDF
  locally (`exportDeptLeaderInvoicePdf`). "Send this to the Staff Leader" is one button away from a
  panel that already has the data.
- **Staff Leader → department.** `getStaffLeaderDeptInvoiceItems` exists in the API module and has no
  caller either, so this direction has neither a panel nor a button today.

### Decision table

| | Option A — keep API-only | Option B — Dept Leader direction only | Option C — both directions |
|---|---|---|---|
| **What ships** | nothing; the routes stay for integration/manual use | one button in the existing `DeptReportManagement` invoice panel | Option B plus a new invoice panel on the Staff Leader report screen |
| **In its favour** | No new surface, no new decisions. Honest about the fact that nobody has asked for this. | The data, the totals and the PDF are already on screen; the button is the smallest possible addition. | Symmetric — the two directions of the same document behave the same way. |
| **Against it** | Two implemented, tested routes stay dead code, and dead code drifts. | Leaves the Staff Leader direction dead. | Requires a whole panel (department picker, line selection, unit-price entry) that has no design. |
| **Cost** | none | small | medium |

### What the owner must specify for Option B or C

1. **Which page and where on it** — inside the existing "xuất hóa đơn" section, or a separate action?
2. **Which actor sees it** — Department Lead only, or also a delegate?
3. **Enable / disable conditions** — is it available when the line list is empty, when a unit price is
   missing, when the period is still open?
4. **Confirmation** — the action emails a named person; does it show a confirmation naming them first?
   (The backend does not accept a recipient, so the UI can only *display* who it will go to.)
5. **Note and unit prices** — `SendStaffLeaderDeptInvoiceCommand` takes a per-line `unitPrice` that the
   Staff Leader types. Where is that entered, and is a zero price valid?
6. **Success / failure / retry wording** — and specifically what the user sees for the two G11 outcomes
   they can now hit: "đang xử lý" and "chưa xác định được kết quả".

### Idempotency is already handled either way

Both routes were brought under the G11 contract, so a UI added later inherits it rather than repeating
R-103. Concretely: both commands implement `IIdempotentEmailSend`, both `reportsApi` functions already
take a required `idempotencyKey` parameter, and both are covered by
`Every_send_action_replays_instead_of_sending_twice`.

**A UI built for these must use `useIdempotentSend`** — `keyFor(operation, resourceId)` on the way in,
`complete(...)` on a confirmed success, and `attemptIsOver(error)` before retiring the key on failure.
Minting a fresh key per click would re-open R-103 for these two routes only, which is exactly the kind of
per-screen divergence the shared hook exists to prevent.

### What G11 did NOT do, and why

- Did **not** add a button, a page, a menu entry or a route.
- Did **not** invent enable/disable rules or confirmation copy.
- Did **not** delete the routes as dead code — they are correct, tested, and the decision to expose them
  has not been made either way.

---

## 2026-07-30 — G11 final closure changed nothing here

**R-104 and R-105 remain BLOCKED**, awaiting the same owner decisions as before. Nothing in this round
touched them, and no part of it was allowed to imply an answer.

Two decisions were made, and both were the owner's, recorded rather than invented:

- **Restore Default is HO-only and belongs to UC-44**, not a new use case. It is the return path of the
  same right — "change a template's content" — and the fixed catalog is precisely why a return path has to
  exist: an operator who breaks a template can neither create a replacement nor delete it.
- **Two deprecated duplicate routes on `EmailsController` were gated, not removed.** They were a live
  authorization bypass and that had to stop today; whether the routes themselves should exist is a
  different question, and it is the owner's. See `10-g11-final-closure.md` §5.1.

One thing deliberately NOT decided: whether a **Reply All button** should appear for every role that can
reply. It is offered on exactly the same right as Reply, because the server applies the identical
authorization check to both — extending it further, or restricting it, would be a product decision nobody
has asked for.

---

## 2026-07-30 (b) — R-104 / R-105 có chặn G12 và G10 không?

Chủ dự án yêu cầu làm rõ, và nói rõ **không được tự coi là non-blocking**. Mục này trình bày dữ kiện và
lựa chọn; **quyết định là của chủ dự án**, và cho tới khi có quyết định thì cả hai mang trạng thái
`BLOCKED — awaiting owner disposition`, còn `G10 readiness` giữ **NOT READY**.

### Với G12 (contact guard) — có ảnh hưởng gì không

Không có đường nối kỹ thuật nào. G12 là trigger/ràng buộc mức database trên `visit_contacts`; R-104 là ba
route dashboard trên `ReportsController`, R-105 là hai route invoice cũng ở đó. Không dùng chung bảng,
handler, hay đường gửi email. Đây là quan sát về **cấu trúc code**, không phải một phán quyết rằng G12
"được phép" đóng — việc kết luận vẫn thuộc chủ dự án.

### Với G10 (deploy) — dữ kiện

**R-104.** Ba route (`viewdashboardstatistics`, `exportstatisticsreport`, `filterdashboardbytime`):

| Điều | Trạng thái |
|---|---|
| Handler | cả ba `throw new NotImplementedException(...)` |
| Phân quyền | chỉ `[Authorize]` cấp class — **mọi user đã đăng nhập, mọi vai trò** đều tới được handler |
| Caller frontend | không có (đo trên toàn `src/`) |
| Lộ dữ liệu | **không** — ném trước khi đọc bất cứ thứ gì |
| Hệ quả khi deploy | ba route trả **500** cho bất kỳ ai gọi; không có hợp đồng vai trò; nhiễu cho giám sát lỗi |

Nghĩa là: không rò rỉ, không hỏng luồng người dùng (không ai gọi), nhưng **có** bề mặt 500 và **có** một
khoảng trống phân quyền đi vào production.

**R-105.** Hai route invoice: cài đặt đầy đủ, có `[RoleAuthorize]`, có scope, có test, **không có UI**.
Không có gì hỏng khi deploy; câu hỏi là tính năng có được coi là hoàn chỉnh khi thiếu nút bấm hay không.

### Lựa chọn để chủ dự án chốt

**R-104** — chọn một:

| | A — chặn G10 | B — không chặn, nhưng chặn cửa trước | C — không chặn, deploy nguyên trạng |
|---|---|---|---|
| Việc phải làm | chốt vai trò + đánh số UC (mâu thuẫn `PERMISSION_MATRIX §5.11` ↔ `PROJECT_OVERVIEW` FE-08), rồi mới deploy | trả **501 Not Implemented** kèm mã lỗi ổn định cho **mọi** vai trò, chưa chốt vai trò nào | để nguyên |
| Ưu | giải quyết dứt điểm | **không** chốt danh sách vai trò nên không giải quyết mâu thuẫn tài liệu bằng cách cài đặt; hết 500 | không tốn gì |
| Nhược | chặn release vì việc chưa ai đặt hàng | vẫn còn nợ hợp đồng vai trò | 500 trong production, khoảng trống phân quyền |

> Em **chưa** làm B. B chỉ đúng nếu nó thật sự không chốt vai trò nào — trả 501 đồng loạt thì đạt điều đó,
> nhưng đây vẫn là thay đổi hành vi API lúc sắp deploy, nên chờ chủ dự án.

**R-105** — A (giữ API-only) / B (chỉ chiều Dept Leader) / C (cả hai chiều), như bảng ở mục R-105 phía trên.
Không lựa chọn nào là điều kiện kỹ thuật để deploy; nó quyết định **phạm vi release**.

### Cần chủ dự án trả lời

1. R-104 chọn A, B hay C?
2. R-105 chọn A, B hay C?
3. Với mỗi khoản: **có chặn G10 hay không** — ghi rõ, để lần sau không phải suy diễn lại.
