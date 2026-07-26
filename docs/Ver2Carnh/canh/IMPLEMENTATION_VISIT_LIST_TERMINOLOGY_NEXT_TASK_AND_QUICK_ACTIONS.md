# Visit list — terminology, next task and quick actions

What changed on the visit-request management list, and why. Written for whoever picks this up next, so
the reasoning is here rather than only the diff.

---

## 1. Terminology: "Host" → "Người phụ trách tiếp đón"

### The rule

The UI says **"Người phụ trách tiếp đón"** — always in full, never the bare "Người phụ trách". The short
form collides with five other roles the product already has: coordinator, department assignee, agenda
responsible user, primary contact, and support member. On a screen that shows several of them at once,
"Người phụ trách" is a genuine ambiguity, not a stylistic preference.

### UI matrix

| Before | After | Where |
|---|---|---|
| `Host:` (row label) | `Người phụ trách tiếp đón:` | list row (desktop + mobile), campus accordion |
| `Chờ duyệt & gán host` | `Chưa được phân công` | list row, before a decision |
| `Đã duyệt & gán Host` | `Đã duyệt và phân công` | status labels (list, contribution, photos) |
| `Đã phân công Host` | `Đã phân công người phụ trách` | process summary, invitation detail |
| `Duyệt & gán host` | `Duyệt & phân công người phụ trách` | approve action + modal title/confirm |
| `Tôi làm host chính` | `Tôi nhận phụ trách` | approve modal self-option |
| `Host hiện tại` | `Người phụ trách hiện tại` | approve modal, transfer modal |
| `Chuyển Host` | `Chuyển người phụ trách` | transfer action + menu entry |
| `Chuyển Host phụ trách` | `Chuyển người phụ trách tiếp đón` | transfer modal title |
| `Chọn Host mới` | `Chọn người phụ trách mới` | transfer modal |
| `Tôi là host` (tab) | `Đoàn tôi phụ trách` | tab label |
| `Được giao làm host` | `Bạn phụ trách tiếp đón` | relation badge (now backend-driven) |
| `Cần duyệt & gán host` | `Bạn có quyền duyệt tại cơ sở` | relation badge (now backend-driven) |
| `Đồng thời là host` | `Đồng thời phụ trách tiếp đón` | registrant tab badge |
| `Host đã thay đổi` | `Người phụ trách đã thay đổi` | change badge |
| `Người tiếp đón (Host)` | `Người phụ trách tiếp đón` | detail card |
| `Host chính` / `(Host)` | `Người phụ trách tiếp đón` / `(Phụ trách)` | participants, contribution page |
| `Host ký nhận` / `Host ký trả` | `Người phụ trách ký nhận` / `… ký trả` | logistics handover |
| `Đã hủy bởi Host` | `Đã hủy bởi người phụ trách` | cancelled status |
| `Người phụ trách chuyến thăm` | `Người phụ trách tiếp đón` | visitor detail (the ambiguous short form) |

English locale follows with "reception owner".

### What deliberately did NOT change

Every technical name, because renaming them would be a large, risky diff that buys nothing a reader
can see:

```
DB      current_host_user_id · host_assigned_by · host_assigned_at · is_host
API     TRANSFER_HOST · APPROVE_AND_ASSIGN_HOST · OPEN_HOST_PROCESS · CANCEL_BY_HOST
        POST /v2/visit-instances/{id}/host-transfer · GET /delegations/campuses/{id}/host-candidates
code    HostTransferCommand · VisitHostEligibility · AssignHostModal · VisitHostTransferModal
        hostName · hostUserId · participantRole IC_HOST · notification targetGroup HOST
```

A frontend test pins `VISIT_ALLOWED_ACTIONS.TRANSFER_HOST === 'TRANSFER_HOST'` so a future
find-and-replace of the label cannot quietly take the wire format with it.

### One factual correction found by the sweep

`visitRequest.json → campusProcessing.hostFinalWarning` said the official host **cannot** be changed
after assignment. That stopped being true when the handover shipped. It now states the actual rule:
only the campus Staff Leader may transfer, and only while ≥ 6 hours remain before the start.

---

## 2. The approval note

`visit_request_campuses.decision_note` holds a **human's sentence**, typed into the approve dialog and
stored verbatim. Nothing on the read path generates, appends to, or reformats it.

It was displayed under one shared label, `Lý do / Ghi chú`, for all three outcomes. That reads as an
admin field, and it puts a rejection reason under a heading that says "note". The label now follows the
outcome:

| Campus status | Label |
|---|---|
| REJECTED | `Lý do từ chối` |
| CANCELLED | `Lý do hủy` |
| otherwise (APPROVED and beyond) | `Ghi chú phê duyệt` |

### Seed wording

15 approved-campus rows in the canonical script carried:

> Đã đối chiếu lịch tiếp đón, thành phần và nguồn lực campus {name} cho {delegation}; campus được phê
> duyệt và host được xác nhận trong cùng quyết định.

That reads as if the system had assessed the campus's resources — it is a person's approval note. Now:

> Campus {name} xác nhận tiếp nhận đoàn. Người phụ trách tiếp đón đã được phân công.

Seed text only: no DDL, no trigger, no row count changed. `CanonicalSqlScript.ExpectedSha256` re-pinned
to `5ba7daac…` with that reason recorded next to it. **No runtime data was rewritten** — this affects
the next fresh build only.

---

## 3. Three layers on a row

A row answers three different questions, and one badge used to try to answer all three. "Chờ xử lý tại
cơ sở" was simultaneously a status, an instruction to the Staff Leader, and noise to the visitor who
could do nothing about it.

| Layer | Field | Source |
|---|---|---|
| Where the request **is** | `statusLabel` | backend, from the campus status (aggregate only for a summary row) |
| What the reader **is** to it | `relationLabel` | backend, from `currentUserRelation` |
| What the reader should **do** | `nextTask` | backend, per reader — see below |

The status badge, the relation chip and the "Việc cần làm" line are three separate elements. The change
badge (`changeSummary`) remains a fourth, additive signal beside the status — never a replacement for it.

---

## 4. Next-task contract

```ts
interface VisitNextTaskDto {
  code: 'REVIEW_AND_ASSIGN' | 'COMPLETE_PREPARATION' | 'CONFIRM_PREPARATION' | 'REVIEW_AMENDMENT'
      | 'ACCEPT_HOST_HANDOVER' | 'RUN_RECEPTION' | 'COMPLETE_POST_VISIT' | 'CLOSE_VISIT' | 'NONE';
  label: string;              // Vietnamese, ready to render
  requiresAction: boolean;    // is THIS reader the one being waited on
  scope: 'REQUEST' | 'INSTANCE';
  visitInstanceId?: number;
  dueAt?: string;             // planned start for pre-visit work, planned end after
  actionCode?: string;        // the allowedActions entry that performs it, when the list can
  disabledReason?: string;
}
```

`NONE` is a real answer ("Không có nhiệm vụ cần xử lý"), not an absent field — an absent field would
leave the client to invent one from the status, which is the thing this contract exists to stop.

### Priority

1. an action the backend already granted (`APPROVE_AND_ASSIGN_HOST`) — someone is demonstrably waiting;
2. a pending amendment on a campus this caller leads;
3. a Host handover that arrived here and has not been acknowledged;
4. the operational stage the Host is standing in;
5. `NONE`.

### Mapping

| Condition | Code | Label |
|---|---|---|
| WAITING + APPROVE_AND_ASSIGN_HOST granted | `REVIEW_AND_ASSIGN` | Duyệt hoặc từ chối và phân công người phụ trách |
| pending amendment on a campus I lead | `REVIEW_AMENDMENT` | Duyệt đề xuất thay đổi |
| I am the Host and an unread action-required handover notice exists | `ACCEPT_HOST_HANDOVER` | Tiếp nhận bàn giao từ người phụ trách cũ |
| Host, ASSIGNED/BEFORE_VISIT, preparation demonstrably unfinished | `COMPLETE_PREPARATION` | Hoàn thiện lịch trình và công tác chuẩn bị |
| Host, ASSIGNED/BEFORE_VISIT, nothing blocking | `CONFIRM_PREPARATION` | Xác nhận hoàn thành chuẩn bị |
| Host, DURING_VISIT | `RUN_RECEPTION` | Theo dõi và cập nhật quá trình tiếp đón |
| Host, AFTER_VISIT, a close condition still fails | `COMPLETE_POST_VISIT` | Hoàn thiện biên bản và hồ sơ |
| Host, AFTER_VISIT, every close condition met | `CLOSE_VISIT` | Kiểm tra và đóng đoàn |
| anything else, incl. visitor / HO / read-only tabs | `NONE` | Không có nhiệm vụ cần xử lý |

### "Preparation complete" is never guessed

Completeness is answered by the **same conditions `CompleteVisitStage` enforces**, so the list cannot
promise a step the command would then refuse:

- *preparation* — an agenda exists AND no invitation is still awaiting a response;
- *closing* — the planned end has passed, no logistics item is still open, every handover is signed by
  both sides, no minute action item is outstanding, and there is either a published article or a Host
  waiver.

All of it is batched over the page; per-row queries would mean six round-trips per row.

### Role mapping

| Reader | Relation label | Typical task |
|---|---|---|
| Campus Staff Leader, own campus pending | Bạn có quyền duyệt tại cơ sở | REVIEW_AND_ASSIGN |
| Host of the campus | Bạn phụ trách tiếp đón | prepare → confirm → run → close |
| Visitor (contact owner) | Bạn là đầu mối chính | NONE |
| Registrant (read-only tab) | Bạn là người đăng ký | NONE |
| HO | Chỉ theo dõi | NONE |
| Invited participant | Bạn được mời tham dự | NONE |
| Department assignee | Bạn được giao nhiệm vụ | NONE |

---

## 5. Actions: two visible, the rest in "⋯"

A row keeps **Xem form**, **Mở quy trình**, and at most **one** next action. Everything else moved into
a `⋯` menu with text labels (which is also what makes it usable on a phone, where an unlabelled icon is
a guess).

### Primary action

| Situation | Button |
|---|---|
| APPROVE_AND_ASSIGN_HOST granted | Duyệt & phân công người phụ trách |
| next task is REVIEW_AMENDMENT | Duyệt đề xuất thay đổi → opens the detail screen |
| ACCEPT_INVITATION | Xác nhận tham gia |
| ASSIGN_TO_DEPARTMENT_STAFF | Giao việc cho Staff |
| EDIT_PENDING_REQUEST | Sửa đơn |
| RESUBMIT_REJECTED_REQUEST | Sửa & gửi lại đơn |

### Menu matrix

| Entry | Shown when | Disabled with a reason when |
|---|---|---|
| Chuyển người phụ trách | a `TRANSFER_HOST` verdict exists for the row/campus | the verdict refused it (cutoff, or no owner yet) |
| Sửa đơn | `EDIT_PENDING_REQUEST` | — |
| Sửa & gửi lại đơn | `RESUBMIT_REJECTED_REQUEST` | — |
| Từ chối cơ sở này | `CAMPUS_REJECT` | — |
| Từ chối lời mời | `DECLINE_INVITATION` | — |
| Hủy lịch thăm | `CANCEL_BY_VISITOR` or `CANCEL_BY_HOST` | — |
| Xem lý do từ chối | request REJECTED with a note | — |
| Xem lý do hủy | request/campus cancelled | — |
| Xem lịch sử thay đổi | `canViewRequestDetail` | — |
| Đánh giá chuyến thăm | a pending feedback target | — |

A **refused** action is shown disabled with its sentence ("Thao tác này chỉ được thực hiện ít nhất 6 giờ
trước khi chuyến thăm bắt đầu") — a rule you can read beats a button that silently disappears. An action
the caller was never granted is absent entirely.

### Kept on the detail screen

Amendment approval with many fields, before/after comparison, agenda and logistics editing, complex
participant management, minutes, and the full history diff. The list points at them; it does not embed
them.

---

## 6. Handover scoping

`TRANSFER_HOST` is **INSTANCE-scoped**, always, and comes from the same `VisitMutationPolicy` the
transfer command re-checks inside its transaction.

- **Single campus** (a row with its own `visitInstanceId`) — offered in the row's `⋯` menu; the modal
  opens on that instance, shows the current owner, the eligible successors, the 6-hour deadline, and
  requires a reason.
- **Multi-campus summary row** (`visitInstanceId === null`) — **never**. The handover picks a campus and
  a summary row cannot say which. The backend refuses to attach the verdict there at all, so the UI has
  nothing to render even by accident.
- **Inside the accordion** — each campus carries its own verdict, measured on its own start and status.
  A campus the caller does not lead comes back with no verdict, so no menu appears for it. Transferring
  one campus leaves the sibling's owner and `rowVersion` untouched.

The row carries the campus instance's `rowVersion`, so a handover started from the list echoes the
concurrency token back and gets a clean 409 instead of clobbering a concurrent change.

### Campus accordion slots (§12)

| Slot | Content |
|---|---|
| 1 | change indicator for this campus (dot; orange when it needs the reader) |
| 2 | view detail |
| 3 | the campus's own `⋯` menu (handover, reject reason) |
| 4 | cancel / cancel reason / feedback |

---

## 7. Accessibility and mobile

- `⋯` trigger: `aria-haspopup="menu"`, `aria-expanded`; panel `role="menu"` with `role="menuitem"` items.
- Keyboard: ↑ ↓ Home End move between enabled items, Escape closes and **returns focus to the trigger**,
  Tab lets focus leave naturally. Click-outside closes without stealing focus.
- Disabled entries carry their reason on `title` and as a visible sub-line.
- The menu renders through a portal because the list container clips its overflow.
- Every row action carries a text label for the mobile breakpoint, not only an icon.
- The transfer modal handles Escape, focuses itself on open, and restores focus to whatever opened it.
- Both layouts (desktop table, mobile cards) are in the DOM at once, so element ids are suffixed with
  the layout (`row-menu-desktop-…` / `row-menu-mobile-…`).

---

## 8. Also fixed on the way

Instance-level rows never received a change summary — `AttachChangeSummariesAsync` ran only on the
request-level query. The one audience whose job is to react to a change (the campus Staff Leader looking
at their own campus) was the only audience never told that something had changed. Instance rows now get
one, scoped to their own instance, so it cannot reveal a sibling.

---

## 9. Tests

| Level | File | Covers |
|---|---|---|
| Unit | `tests/PEMS.UnitTests/Delegations/VisitRowLabelsTests.cs` | status/relation labels; no label contains "Host" |
| Integration | `tests/PEMS.IntegrationTests/VisitRequests/V2ListNextTaskAndTransferTests.cs` | next task per role, handover scoping, the 6-hour refusal, amendment priority, verbatim decision note |
| Frontend | `frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementActions.test.tsx` | terminology, the three layers, capability-driven menu, single vs multi-campus scoping, Escape/focus, mobile labels |
| Real-stack | `frontend/pems-react/tests-realstack/list-terminology-and-actions.realstack.spec.ts` | §17.1–§17.4 through the real browser and API |

---

## 10. Known limitations

1. **`Sửa nhanh` and `Đề xuất thay đổi` are not list actions.** §9.2 lists them as menu candidates, but
   their verdicts (safe edit, amendment) are requester-side and depend on the primary contact's access
   status, which the list DTO does not carry. Offering them from a row would mean promising something the
   handler might refuse — the opposite of what the capability contract is for. They stay on the detail
   screen, which §10 explicitly permits, and the menu's `Xem lịch sử thay đổi` is the route there.
2. **`ACCEPT_HOST_HANDOVER` depends on the notification staying unread.** It is derived from the
   action-required notice the transfer writes to the incoming owner; once they read it, the task falls
   back to the stage task. Nothing else in the schema records "this is new to you".
3. **A multi-campus accordion is only reachable for a Visitor or HO row today**, because campus actors
   see one row per campus. The per-campus verdicts are computed correctly regardless, so the accordion is
   right for whoever reaches it — but the enabled handover a Staff Leader actually uses is the one on
   their own row.
