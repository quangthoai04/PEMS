> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **ASSIGNED is removed.** Approving a request now requires assigning a host immediately.
> - **New statuses:** `PARTIALLY_APPROVED` (request level) and `REJECTED` (campus level) are added. 
> - **Cancel logic:** Visitors can cancel requests in `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** `transportation_note` and `transportation_note` are replaced by `transportation_note`.
> Please refer to the latest codebase and SQL schema for the current implementation.

# PROMPT_FIX_VISIT_MANAGEMENT_STATUS_SORT_SEARCH_HOST_RULES_PEMS

## 0. Context

Current DB version used for testing:

```text
pems_full_role_status_coverage_v7_aligned.sql
```

Current screen:

```text
VisitRequestManagement.tsx
AssignHostModal.tsx
visitRequestFilterConfig.ts
ViewGuestDelegationListQueryHandler.cs
ViewGuestDelegationListDto.cs
DelegationsController.cs
```

The screen currently has several incorrect behaviors around Staff Leader temporary host assignment, status filtering, cancellation labels, realtime search, and date sorting.

Important SQL rule:

```text
Do not automatically update database from code.
Do not embed SQL patch into DbSeeder/Program.cs.
Only create standalone SQL script for user to review and run manually.
```

---

# 1. Required business rules

## 1.1. Staff Leader is never the official Host

A Staff Leader is:

```text
role_code = STAFF
sub_role = Leader
```

Staff Leader can approve a single-campus request and assign a normal Staff as official Host.

Staff Leader can also receive a multi-campus campus instance temporarily after HO approves, but this is only a temporary responsibility to select an official Host.

Therefore:

```text
Staff Leader must not be treated as official Host.
Staff Leader must not have badge "Được giao làm host".
Staff Leader must not have CANCEL_BY_HOST.
Staff Leader must not prepare/operate the visit as Host.
Staff Leader only selects/assigns official Host to a Staff user.
```

Official Host must be:

```text
role_code = STAFF
sub_role = Staff
active
same campus
```

---

## 1.2. Temporary Staff Leader assignment after HO approval

For MULTI_CAMPUS after HO approval:

```text
visit_request_campuses.current_host_user_id = StaffLeaderId
host_assignment_source = AUTO_STAFF_LEADER
```

means:

```text
Staff Leader is temporary campus receiver.
Staff Leader must select official Host.
```

Frontend label:

```text
Cần chọn Host chính thức
```

or:

```text
Chờ bổ nhiệm Host
```

Do not display:

```text
Được giao làm host
```

Backend relation should be:

```text
PENDING_HOST_ASSIGNMENT
```

or:

```text
TEMP_CAMPUS_RESPONSIBLE
```

---

## 1.3. When can "Cần chọn Host chính thức" appear?

`Cần chọn Host chính thức` must only appear when the visit has not entered operation.

Valid cases:

```text
request_status = APPROVED
visit_scope = MULTI_CAMPUS
host_assignment_source = AUTO_STAFF_LEADER
current_host_user_id = current Staff Leader
campus_status IN ('ASSIGNED', 'BEFORE_VISIT')
request_status NOT IN ('REJECTED', 'CANCELLED')
campus_status NOT IN ('DURING_VISIT', 'AFTER_VISIT', 'CLOSED', 'CANCELLED')
```

Invalid cases:

```text
DURING_VISIT must not show "Cần chọn Host chính thức".
AFTER_VISIT must not show "Cần chọn Host chính thức".
CLOSED must not show "Cần chọn Host chính thức".
CANCELLED must not show "Cần chọn Host chính thức".
REJECTED must not show "Cần chọn Host chính thức".
```

If a row is already:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
```

then the campus must already have an official Host, not only an AUTO_STAFF_LEADER temporary assignment.

---

## 1.4. Cancelled labels must show who cancelled

Current generic label:

```text
Đã hủy
```

is not enough.

Need distinguish:

```text
Khách đã hủy
Host đã hủy
Hệ thống đã hủy
Đã hủy
```

Mapping:

```text
cancellation_actor_type = VISITOR
=> Khách đã hủy

cancellation_actor_type = HOST
=> Host đã hủy

cancellation_actor_type = SYSTEM
=> Hệ thống đã hủy

else
=> Đã hủy
```

Required DTO fields if not already available:

```text
CancellationActorType
CancellationSource
CancellationReason
CancelledBy
CancelledAt
```

Frontend must show cancellation reason in detail/reason modal if available.

---

## 1.5. Staff Leader cannot cancel

For Staff Leader:

```text
role_code = STAFF
sub_role = Leader
```

Backend must never return:

```text
CANCEL_BY_HOST
```

even if:

```text
current_host_user_id = currentUser.id
```

because that can happen only as temporary AUTO_STAFF_LEADER assignment.

Frontend can defensively hide cancel, but backend must enforce the rule.

---

## 1.6. Hide assign/transfer host icon after official Host is already assigned

If official Host is already assigned:

```text
host_assignment_source IN ('MANUAL_APPROVAL', 'TRANSFERRED')
current_host_user_id points to STAFF + sub_role = Staff
```

then Staff Leader must not see the assign/transfer icon.

The `TRANSFER_HOST` / `SELECT_OFFICIAL_HOST` action is only valid for:

```text
PENDING_HOST_ASSIGNMENT
```

before official Host is selected.

If business later allows reassigning Host, implement as a separate permission/action:

```text
REASSIGN_OFFICIAL_HOST
```

Do not reuse the current "Chọn Host chính thức" action for already-assigned cases.

---

# 2. Current issues to fix

## 2.1. Filter "Cần chọn Host chính thức" returns wrong rows

Current behavior returns rows such as:

```text
DURING_VISIT
CLOSED
CANCELLED
single-campus rows
old historical rows where Staff Leader is wrongly seeded as host
```

Expected behavior:

```text
Only MULTI_CAMPUS + APPROVED + AUTO_STAFF_LEADER + campus_status ASSIGNED/BEFORE_VISIT.
No DURING_VISIT.
No AFTER_VISIT.
No CLOSED.
No CANCELLED.
No SINGLE_CAMPUS.
No official-host rows.
```

Backend must enforce this, not only frontend.

---

## 2.2. "Trong tiếp khách" filter showing "Cần chọn Host chính thức"

This is invalid.

Reason:

```text
A visit cannot be DURING_VISIT if no official Host has been selected.
```

Fix both:

```text
1. SQL seed data: do not seed DURING_VISIT with current_host_user_id = Staff Leader and host_assignment_source = AUTO_STAFF_LEADER.
2. Backend relation logic: do not return PENDING_HOST_ASSIGNMENT for DURING_VISIT.
3. Frontend badge logic: do not render "Cần chọn Host chính thức" for DURING_VISIT.
```

---

## 2.3. "Đã hủy" rows showing "Cần chọn Host chính thức"

This is invalid.

For CANCELLED rows, the main status/badge should be cancellation-related:

```text
Khách đã hủy
Host đã hủy
Hệ thống đã hủy
Đã hủy
```

Do not show pending host assignment badge on cancelled rows.

---

## 2.4. "Tất cả đơn đã duyệt" needs clearer display

When filtering:

```text
Tất cả đơn đã duyệt
```

rows should still clearly explain their sub-progress.

Instead of only showing:

```text
Đã phân công Host
Đang chuẩn bị
Đang tiếp khách
Chờ đóng đoàn
Đã đóng đoàn
```

show a clearer combined label or sublabel:

```text
Đã duyệt · Chờ chọn Host chính thức
Đã duyệt · Đã phân công Host
Đã duyệt · Đang chuẩn bị
Đã duyệt · Đang tiếp khách
Đã duyệt · Chờ đóng đoàn
Đã duyệt · Đã đóng đoàn
```

Implementation options:

Option A:

```text
Status badge text = progress label.
Add small sublabel/badge = Đã duyệt.
```

Option B:

```text
Status badge text = Đã duyệt · <progress>.
```

Use whichever is cleaner in UI.

---

# 3. Backend changes

## 3.1. Update DTO

In `ViewGuestDelegationListDto.cs`, ensure these fields exist:

```csharp
public string? CurrentUserRelation { get; set; }
public bool IsReadOnly { get; set; }
public string? TabType { get; set; }
public string? HostAssignmentSource { get; set; }
public long? CurrentHostUserId { get; set; }
public string? CancellationActorType { get; set; }
public string? CancellationSource { get; set; }
public string? CancellationReason { get; set; }
public long? CancelledBy { get; set; }
public DateTime? CancelledAt { get; set; }
public string? DisplayStatusLabel { get; set; }
public string? DisplayProgressLabel { get; set; }
```

If naming style differs, use project convention.

---

## 3.2. Update relation calculation

In `ViewGuestDelegationListQueryHandler.cs`, calculate `PENDING_HOST_ASSIGNMENT` only when:

```csharp
currentUser.RoleCode == "STAFF"
&& currentUser.SubRole == "Leader"
&& item.VisitScope == "MULTI_CAMPUS"
&& item.RequestStatus == "APPROVED"
&& item.HostAssignmentSource == "AUTO_STAFF_LEADER"
&& item.CurrentHostUserId == currentUser.UserId
&& (item.CampusStatus == "ASSIGNED" || item.CampusStatus == "BEFORE_VISIT")
```

Do not return `PENDING_HOST_ASSIGNMENT` when:

```csharp
item.RequestStatus == "REJECTED"
|| item.RequestStatus == "CANCELLED"
|| item.CampusStatus == "DURING_VISIT"
|| item.CampusStatus == "AFTER_VISIT"
|| item.CampusStatus == "CLOSED"
|| item.CampusStatus == "CANCELLED"
```

If the row has:

```text
host_assignment_source = AUTO_STAFF_LEADER
campus_status IN DURING_VISIT/AFTER_VISIT/CLOSED
```

treat as bad seed/data and return a safe readonly relation such as:

```text
STAFF_LEADER_SCOPE_READONLY
```

or fix via SQL seed patch.

---

## 3.3. Update allowedActions

For Staff Leader:

```csharp
// Never return this for Staff Leader:
CANCEL_BY_HOST
```

For pending host assignment:

```csharp
if (relation == "PENDING_HOST_ASSIGNMENT")
{
    allowedActions = ["VIEW_DETAIL", "TRANSFER_HOST"]; // frontend labels as "Chọn Host chính thức"
}
```

For official Host already assigned:

```csharp
if (hostAssignmentSource == "TRANSFERRED" || hostAssignmentSource == "MANUAL_APPROVAL")
{
    // Do not show TRANSFER_HOST unless there is a separate explicit business permission.
    allowedActions must not include TRANSFER_HOST.
}
```

For Staff official Host:

```csharp
role_code = STAFF
sub_role = Staff
current_host_user_id = currentUser.id
```

then allowed actions may include:

```text
VIEW_DETAIL
PREPARE_VISIT
CANCEL_BY_HOST if business rules and time allow
```

---

## 3.4. Update filtering params

Add or confirm support for:

```http
sortBy=plannedStartAt
sortDirection=asc|desc
keyword=<text>
```

For Staff Leader filter:

```text
status=PENDING_HOST_ASSIGNMENT
```

or:

```text
relation=PENDING_HOST_ASSIGNMENT
```

must map to:

```sql
visit_scope = 'MULTI_CAMPUS'
AND request_status = 'APPROVED'
AND host_assignment_source = 'AUTO_STAFF_LEADER'
AND campus_status IN ('ASSIGNED', 'BEFORE_VISIT')
AND campus_status NOT IN ('DURING_VISIT', 'AFTER_VISIT', 'CLOSED', 'CANCELLED')
```

If existing query only filters `relation=PENDING_HOST_ASSIGNMENT`, tighten it.

---

## 3.5. Realtime keyword search support

Backend already accepts `keyword`. Ensure it is safe and paginated.

Frontend will debounce and call API automatically after typing. Backend does not need special realtime endpoint.

---

# 4. Frontend changes

## 4.1. Realtime search

In `VisitRequestManagement.tsx`:

Current behavior:

```text
User types keyword.
Nothing changes until clicking Apply or pressing Enter.
```

Required behavior:

```text
User types keyword.
After debounce 300–500ms, page resets to 1 and list reloads.
```

Implementation:

```tsx
const [debouncedKeyword, setDebouncedKeyword] = useState(draftFilters.keyword);

useEffect(() => {
  const timer = setTimeout(() => {
    setDebouncedKeyword(draftFilters.keyword.trim());
  }, 400);
  return () => clearTimeout(timer);
}, [draftFilters.keyword]);

useEffect(() => {
  const nextFilters = { ...appliedFilters, keyword: debouncedKeyword };
  setAppliedFilters(nextFilters);
  setCurrentPage(1);
  loadDelegations(activeTab, 1, pageSize, nextFilters);
}, [debouncedKeyword]);
```

Avoid duplicate calls when Apply is clicked.

Status/date/scope filters may still use Apply button unless product wants all filters realtime.

---

## 4.2. Sort by "Lịch tiếp" header

Do not put date sort inside filter panel.

Add sorting directly to table header:

```text
Lịch tiếp ↑
Lịch tiếp ↓
```

Click behavior:

```text
First click: sort plannedStartAt descending/newest first.
Second click: sort plannedStartAt ascending/oldest first.
Third click optional: reset default.
```

State:

```tsx
const [sortBy, setSortBy] = useState<'plannedStartAt' | null>('plannedStartAt');
const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
```

Or default `desc` if product wants newest first.

Send to backend:

```ts
params.sortBy = 'plannedStartAt';
params.sortDirection = sortDirection;
```

Update pagination when sorting:

```text
When sort changes, reset currentPage = 1.
```

The `Lịch tiếp` table header should be a clickable button.

---

## 4.3. Fix badge "Cần chọn Host chính thức"

In `renderBadges(row)`:

Current logic is too broad:

```tsx
row.currentUserRelation === 'PENDING_HOST_ASSIGNMENT'
|| row.currentUserRelation === 'TEMP_CAMPUS_RESPONSIBLE'
|| (isStaffLeader && row.hostAssignmentSource === 'AUTO_STAFF_LEADER' && row.currentUserIsHost)
```

Replace with a stricter helper:

```tsx
const isPendingHostAssignment =
  isStaffLeader &&
  row.visitScope === 'MULTI_CAMPUS' &&
  row.requestStatus === 'APPROVED' &&
  row.hostAssignmentSource === 'AUTO_STAFF_LEADER' &&
  (row.currentUserRelation === 'PENDING_HOST_ASSIGNMENT' ||
   row.currentUserRelation === 'TEMP_CAMPUS_RESPONSIBLE') &&
  (row.campusStatus === 'ASSIGNED' || row.campusStatus === 'BEFORE_VISIT');
```

Also explicitly exclude:

```tsx
const isCancelledOrRejected =
  row.requestStatus === 'CANCELLED' ||
  row.campusStatus === 'CANCELLED' ||
  row.requestStatus === 'REJECTED';

const isOperationalOrFinished =
  row.campusStatus === 'DURING_VISIT' ||
  row.campusStatus === 'AFTER_VISIT' ||
  row.campusStatus === 'CLOSED';
```

Then:

```tsx
if (isPendingHostAssignment && !isCancelledOrRejected && !isOperationalOrFinished) {
  show "Cần chọn Host chính thức";
}
```

Do not show `Được giao làm host` for Staff Leader.

---

## 4.4. Fix status badge computation

In `getStatusBadge(row)`:

Do not prioritize `PENDING_HOST_ASSIGNMENT` before terminal/operational statuses.

Order must be:

```text
1. CANCELLED
2. REJECTED
3. PENDING_APPROVAL
4. APPROVED + campus progress
5. pending host assignment only when campusStatus ASSIGNED/BEFORE_VISIT
```

Recommended:

```tsx
if (row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED') {
  return cancellation label;
}

if (row.requestStatus === 'REJECTED') {
  return 'Từ chối';
}

if (row.requestStatus === 'PENDING_APPROVAL') {
  return 'Chờ duyệt';
}

if (row.requestStatus === 'APPROVED') {
  if (isPendingHostAssignment(row)) return 'Đã duyệt · Chờ chọn Host';
  if (row.campusStatus === 'ASSIGNED') return 'Đã duyệt · Đã phân công Host';
  if (row.campusStatus === 'BEFORE_VISIT') return 'Đã duyệt · Đang chuẩn bị';
  if (row.campusStatus === 'DURING_VISIT') return 'Đã duyệt · Đang tiếp khách';
  if (row.campusStatus === 'AFTER_VISIT') return 'Đã duyệt · Chờ đóng đoàn';
  if (row.campusStatus === 'CLOSED') return 'Đã duyệt · Đã đóng đoàn';
}
```

---

## 4.5. Fix action icon "Chọn Host chính thức"

Only show transfer/select official host action when:

```tsx
can('TRANSFER_HOST') && isPendingHostAssignment(row)
```

Do not show it when:

```text
campusStatus = ASSIGNED with official Host already assigned
campusStatus = DURING_VISIT
campusStatus = AFTER_VISIT
campusStatus = CLOSED
campusStatus = CANCELLED
requestStatus = REJECTED
requestStatus = CANCELLED
```

If backend still returns `TRANSFER_HOST` incorrectly, frontend should hide it defensively for Staff Leader unless `isPendingHostAssignment(row)` is true.

---

## 4.6. Fix host text label for Staff Leader temporary rows

For pending host assignment rows, do not display:

```text
Host: IC Staff Leader (HN)
```

Instead display:

```text
Người tiếp nhận tạm: IC Staff Leader (HN)
```

or:

```text
Chờ chọn Host chính thức
```

For official host rows, display:

```text
Host: <Staff name>
```

This avoids making Staff Leader look like official Host.

---

## 4.7. Fix Staff attending tab

Regular Staff:

```text
role_code = STAFF
sub_role = Staff
```

must have:

```text
Tab 1: Đơn phụ trách
Tab 2: Đơn mời tham dự
```

Do not remove Tab 2.

Department/Student:

```text
DEPARTMENT/DEPT
STUDENT
```

can default to `attending`.

Student must not show `responsible`.

---

# 5. Frontend filter config updates

## 5.1. Staff Leader status options

Update `visitRequestFilterConfig.ts`.

For Staff Leader:

```ts
{ 
  value: 'PENDING_HOST_ASSIGNMENT',
  label: 'Cần chọn Host chính thức',
  requestStatus: 'APPROVED',
  visitScopes: ['MULTI_CAMPUS'],
  campusStatuses: ['ASSIGNED', 'BEFORE_VISIT'],
  relation: 'PENDING_HOST_ASSIGNMENT',
  description: 'Đơn liên cơ sở đã được HO duyệt, đang chờ Staff Leader chọn Host chính thức.'
}
```

Need to add `campusStatuses?: string[]` type if it does not exist.

Remove any behavior where this filter only sends relation without status constraints.

---

## 5.2. Approved filter explanation

For HO/Staff Leader/Visitor:

```text
Tất cả đơn đã duyệt
```

should still show progress substatus in the list:

```text
Đã duyệt · Đã phân công Host
Đã duyệt · Đang chuẩn bị
Đã duyệt · Đang tiếp khách
...
```

---

# 6. SQL seed alignment required

Do not update DB automatically. Only create a standalone SQL script.

## 6.1. Fix invalid original seed rows

The current full SQL still contains old seed rows such as:

```text
Đoàn SeoulTech tham quan Hà Nội và TP.HCM
Đoàn SeoulTech trao đổi học thuật tại Hà Nội
Seoul Future University - chuyến thăm campus HN
GreenTech Asia Pte. Ltd. - chuyến thăm campus HN
```

Some of these rows assign Staff Leader as current_host_user_id for operational states.

This is invalid.

Fix rules:

```text
SINGLE_CAMPUS approved/operational/closed/cancelled after approval:
- current_host_user_id must be STAFF + sub_role Staff.
- host_assignment_source = MANUAL_APPROVAL.
- host_assigned_by = Staff Leader.

MULTI_CAMPUS AUTO_STAFF_LEADER temporary:
- allowed only before official host assignment.
- campus_status must be ASSIGNED or BEFORE_VISIT.
- not DURING_VISIT / AFTER_VISIT / CLOSED / CANCELLED.

MULTI_CAMPUS DURING_VISIT / AFTER_VISIT / CLOSED:
- must have official Host = STAFF + sub_role Staff.
- host_assignment_source should be TRANSFERRED if originally released to Staff Leader.
- host_transferred_by = Staff Leader.
- host_transferred_at is not null.
```

## 6.2. Staff Leader must not be IC_HOST participant

Delete or prevent rows:

```sql
visit_participants.user_id = StaffLeaderId
participant_role = 'IC_HOST'
is_host = TRUE
```

for Staff Leader.

Official Host participant should be normal Staff only:

```text
role_code = STAFF
sub_role = Staff
participant_role = IC_HOST
is_host = TRUE
status = ASSIGNED
```

## 6.3. Cancelled seed labels

Seed at least two cancellation cases:

```text
VISITOR cancellation:
cancellation_actor_type = VISITOR
cancellation_source = SELF_SERVICE

HOST cancellation:
cancellation_actor_type = HOST
cancellation_source = HOST_ASSISTED
```

Staff Leader must not be the cancelling Host.

---

# 7. SQL diagnostics required

The SQL patch must include diagnostics only, not auto-run in application.

After user manually runs the patch, these checks should return `invalid_count = 0`:

```sql
-- Staff Leader official host invalid
SELECT COUNT(*) AS invalid_count
FROM visit_request_campuses vrc
JOIN users u ON u.user_id = vrc.current_host_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'STAFF'
  AND u.sub_role = 'Leader'
  AND (
    vrc.host_assignment_source <> 'AUTO_STAFF_LEADER'
    OR vrc.status IN ('DURING_VISIT', 'AFTER_VISIT', 'CLOSED')
  );

-- Pending host assignment invalid status
SELECT COUNT(*) AS invalid_count
FROM visit_request_campuses vrc
WHERE vrc.host_assignment_source = 'AUTO_STAFF_LEADER'
  AND vrc.status NOT IN ('ASSIGNED', 'BEFORE_VISIT', 'CANCELLED');

-- Staff Leader IC_HOST participant invalid
SELECT COUNT(*) AS invalid_count
FROM visit_participants vp
JOIN users u ON u.user_id = vp.user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'STAFF'
  AND u.sub_role = 'Leader'
  AND vp.participant_role = 'IC_HOST';

-- Staff Leader CANCEL_BY_HOST must be impossible by data + backend
-- This is mostly a code test, not pure SQL.
```

---

# 8. Manual test cases

## 8.1. Staff Leader

Login:

```text
staff.leader.hn@fpt.edu.vn
```

Expected:

```text
- No Tab Đơn mời tham dự.
- No CANCEL_BY_HOST anywhere.
- "Cần chọn Host chính thức" filter shows only MULTI_CAMPUS approved rows in ASSIGNED/BEFORE_VISIT with AUTO_STAFF_LEADER.
- It does not show DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED.
- Already assigned official Host rows do not show assign/transfer icon.
- Pending host rows show button "Chọn Host chính thức".
- Operational rows show read-only or no host assignment action.
```

## 8.2. Staff regular

Login:

```text
staff.hn@fpt.edu.vn
```

Expected:

```text
- Has both Đơn phụ trách and Đơn mời tham dự tabs.
- Host rows show Host actions only when current user is official Host.
- Attending rows show invitation/participant rows.
```

## 8.3. HO

Login:

```text
ho@fpt.edu.vn
```

Expected:

```text
- No "Loại xử lý" filter.
- Has status/scope/date filters.
- Can see MULTI_CAMPUS and SINGLE_CAMPUS read-only.
- SINGLE_CAMPUS has VIEW_DETAIL only.
```

## 8.4. Date sorting

Click table header:

```text
Lịch tiếp
```

Expected:

```text
- Toggle newest/oldest.
- API receives sortBy=plannedStartAt and sortDirection=asc/desc.
- Pagination remains correct.
```

## 8.5. Realtime search

Typing in search box should reload the list after debounce without pressing Apply.

---

# 9. Build requirements

Run:

```bash
dotnet build
npm run build
```

If available:

```bash
dotnet test
npm run lint
npm run typecheck
```

Report exactly which commands were run.

---

# 10. Output requirements

When SQL changes are needed:

```text
Do not run SQL automatically.
Do not embed patch into DbSeeder.
Create standalone SQL script only.
Explain affected tables.
Explain rollback.
Provide verification SELECT queries.
Wait for user to run manually.
```
