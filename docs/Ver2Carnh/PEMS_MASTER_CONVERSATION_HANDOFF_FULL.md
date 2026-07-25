# PEMS — MASTER CONVERSATION HANDOFF
## Tổng hợp toàn bộ yêu cầu, quyết định, tiến độ, lỗi đã phát hiện, phần đã hoàn thành và việc còn lại

> Tài liệu này tổng hợp toàn bộ nội dung chính trong cuộc hội thoại liên quan đến dự án PEMS, đặc biệt là quá trình chuyển đổi sang Pure V2 per-campus, các phase triển khai, các lỗi frontend/backend/database, chiến lược kiểm thử, Git workflow, và luồng bảo mật tạo tài khoản/xác nhận email.
>
> Mục tiêu là để một kỹ sư, AI Agent, Tech Lead, QA Lead hoặc người mới tiếp quản có thể đọc một lần và hiểu:
>
> - Dự án đang giải quyết vấn đề gì.
> - Kiến trúc và business logic Pure V2 hiện tại là gì.
> - Những phase nào đã hoàn thành.
> - Những lỗi nào đã được sửa.
> - Những lỗi nào vẫn còn.
> - Quyết định nghiệp vụ nào đã được khóa.
> - Quy trình Git/test/evidence phải tuân thủ.
> - Điểm tiếp tục chính xác là gì.
>
> **Lưu ý quan trọng:** tài liệu này được tổng hợp từ nhiều checkpoint theo thời gian. Một số HEAD/commit được báo ở các thời điểm khác nhau. Trước khi tiếp tục code, luôn chạy preflight Git để xác định trạng thái repository thực tế hiện tại, không được giả định HEAD cuối cùng chỉ dựa trên tài liệu này.

---

# MỤC LỤC

1. Tổng quan dự án  
2. Mục tiêu chuyển đổi Pure V2  
3. Mô hình dữ liệu và business logic Pure V2  
4. Vai trò và phạm vi dữ liệu  
5. Luồng tạo đơn, OTP và xác nhận đơn  
6. Luồng Primary Contact, Claim và Transfer  
7. Luồng duyệt/từ chối theo campus  
8. Luồng chỉnh sửa, resubmit, safe edit và amendment  
9. Luồng hủy, invitation, agenda, logistics và closing  
10. Quy tắc scope và bảo mật dữ liệu per-campus  
11. Tiến độ Phase 1 đến Phase 7  
12. Chi tiết các lỗi và fix theo từng phase  
13. Phase 4.5 — Stack, error-state và UI stabilization  
14. Quyết định UI/UX đã khóa  
15. Toast, localization, rich text và mutation feedback  
16. Upload contract đã khóa  
17. Real-stack E2E và evidence policy  
18. P0 Account Email Confirmation & Truthful Email Delivery  
19. Chính sách credential và production security debt  
20. Git workflow, commit policy và stop conditions  
21. Test policy và Definition of Done  
22. Known debts và backlog  
23. Danh sách commit quan trọng đã được báo cáo  
24. Test count theo các checkpoint  
25. Trạng thái hiện tại và điểm tiếp tục  
26. Checklist bàn giao cho người tiếp quản  

---

# 1. TỔNG QUAN DỰ ÁN

## 1.1 Tên và phạm vi

PEMS là hệ thống quản lý tiếp khách/đoàn tham quan nhiều cơ sở của FPT University.

Các chức năng chính:

- Đăng ký đoàn tham quan.
- Tạo đơn công khai qua OTP.
- Quản lý đơn theo nhiều campus.
- Duyệt/từ chối theo từng campus.
- Gán Host.
- Mời participant.
- Quản lý agenda.
- Logistics.
- Tài liệu.
- Biên bản.
- Ảnh đoàn khách.
- Feedback.
- Expense.
- News contribution.
- Primary contact claim/transfer.
- Amendment và revision.
- Audit và notification.
- Quản lý tài khoản HO / Staff Leader.
- Các module phụ như Partner, Gallery, FAQ, Translation.

## 1.2 Công nghệ

- Backend: .NET.
- Database: MySQL.
- Frontend: React/Vite.
- Frontend deployment: Vercel.
- Backend deployment: Railway.
- Local development:
  - Backend thường chạy các port như 5265/5299.
  - Frontend thường chạy 3000/3100/5273 tùy phiên.
- Real-stack test:
  - React → .NET API → MySQL disposable.
  - Playwright/Chromium.
  - Email file-sink, không gửi email thật.

## 1.3 Branch

Branch làm việc chính:

```text
Local: Canh-Iter1
Remote: origin/Cảnh-Iter1
```

Không tự merge vào `Dev` hoặc `main`.

---

# 2. MỤC TIÊU CHUYỂN ĐỔI PURE V2

## 2.1 Vấn đề của V1

V1 coi một request là một form global dùng chung cho mọi campus.

Điều này gây lỗi khi một request đi nhiều campus nhưng:

- lịch khác nhau;
- mục đích khác nhau;
- nội dung làm việc khác nhau;
- đầu mối vận hành khác nhau;
- danh sách khách/hỗ trợ khác nhau;
- media consent khác nhau;
- Host/agenda/logistics khác nhau.

V1 còn tồn tại các dạng fallback:

- lấy dữ liệu global trên `visit_requests`;
- lấy campus đầu tiên;
- lấy campus có ID nhỏ nhất làm projection;
- dùng `form_schema_version` để rẽ V1/V2;
- dual-read/fallback.

## 2.2 Mục tiêu Pure V2

Pure V2 yêu cầu:

```text
1 Visit Request
→ N Visit/Campus Instances
→ mỗi instance có dữ liệu form riêng
```

Không còn:

- request-level form projection;
- V1 fallback;
- `FormSchemaVersion` runtime branch;
- campus đại diện;
- smallest-campus projection;
- first-campus fallback.

---

# 3. MÔ HÌNH DỮ LIỆU VÀ BUSINESS LOGIC PURE V2

## 3.1 Request cha

`visit_requests` chỉ giữ thông tin cấp request:

- request identity;
- request code;
- registrant;
- primary contact relation;
- source;
- lifecycle tổng;
- scope;
- mixed flag;
- fingerprint;
- row version;
- audit metadata.

Không giữ nội dung form per-campus.

## 3.2 Campus/Visit Instance

Mỗi campus có một instance riêng:

```text
Visit Request
├── HN Instance
├── HCM Instance
└── ĐN Instance
```

Mỗi instance có:

- campus;
- start/end;
- status riêng;
- Host riêng;
- decision riêng;
- agenda riêng;
- logistics riêng;
- documents/minutes/photos/expense/news riêng.

## 3.3 Form detail per instance

Dữ liệu form thật nằm tại `visit_instance_form_details`, bao gồm:

- delegation name;
- visit type;
- purpose;
- working content;
- operational contact;
- working language;
- transportation note;
- media consent;
- campus-specific notes.

## 3.4 Guest/support

Guest và support phải được link đúng instance:

- guest master record;
- instance guest membership;
- support membership per campus.

Không lấy guest/support từ sibling campus.

---

# 4. VAI TRÒ VÀ PHẠM VI DỮ LIỆU

## 4.1 Người đăng ký

Người trực tiếp tạo/gửi form.

Có thể là:

- Visitor.
- Staff.
- Staff Leader.
- User nội bộ tạo hộ.

Không tự động là Primary Contact nếu email khác.

## 4.2 Primary Contact

Người đại diện chính cấp request.

Có quyền sau khi relation được xác nhận:

- xem request;
- chỉnh sửa hợp lệ;
- gửi amendment;
- theo dõi các campus;
- thực hiện owner actions theo lifecycle.

## 4.3 Operational Contact

Đầu mối phối hợp tại từng campus.

- là snapshot trong form;
- không nhất thiết có tài khoản;
- không tự có quyền login;
- không tự duyệt;
- không tự gán Host;
- không phải owner request.

## 4.4 Host

Nhân sự nội bộ phụ trách một campus instance.

Host:

- thuộc đúng campus;
- đúng role/sub-role;
- account ACTIVE;
- được gán cho instance cụ thể;
- điều khiển process stages của instance.

## 4.5 HO

- xem toàn bộ request/campus;
- read-only/monitoring theo thiết kế hiện tại;
- không phải centralized approver.

## 4.6 Staff Leader

- chỉ xử lý campus thuộc phạm vi;
- duyệt/từ chối campus;
- gán Host;
- không thấy dữ liệu sibling campus nếu không có scope.

## 4.7 Staff/Participant/Student

Chỉ thấy instance có relation:

- assigned;
- invited;
- participant;
- department task;
- host relation.

## 4.8 Admin

Theo permission matrix hiện tại:

- không có business access trực tiếp với Visit Request;
- chỉ quản trị hệ thống/audit nếu được phân quyền riêng.

---

# 5. LUỒNG TẠO ĐƠN, OTP VÀ XÁC NHẬN ĐƠN

## 5.1 Authenticated create

Backend phải:

1. Validate ít nhất một campus.
2. Validate schedule.
3. Validate campus không trùng.
4. Validate detail cho từng campus.
5. Validate guest/support membership.
6. Tính scope.
7. Tính mixed flag.
8. Tính fingerprint.
9. Tạo request/instances/details/members/revisions/audit trong transaction.
10. Gửi notification sau commit.
11. Idempotency không tạo duplicate.

## 5.2 Public OTP create

Luồng chuẩn:

```text
Nhập form
→ gửi OTP
→ lưu pending snapshot
→ nhập OTP
→ verify
→ tạo request thật
```

Pending snapshot phải chứa đủ toàn bộ payload per-campus.

## 5.3 Contract khi verify OTP

Chỉ success khi:

- HTTP success;
- có requestId;
- request tồn tại;
- instances tồn tại;
- form details đầy đủ;
- transaction commit.

Nếu fail:

- modal vẫn mở;
- form không mất;
- campus không mất;
- không navigate;
- không reset;
- lỗi rõ;
- retry/resend phù hợp.

## 5.4 Idempotency

- verify lại không tạo request thứ hai;
- không gửi notification lần hai;
- timeout sau commit, retry phải trả request cũ;
- không partial data.

---

# 6. PRIMARY CONTACT, CLAIM VÀ TRANSFER

## 6.1 Initial claim

Nếu registrant và primary contact cùng email:

- relation ACTIVE ngay.

Nếu khác email:

```text
visitor_user_id = null
primary_contact_access_status = PENDING_CONFIRMATION
```

Tạo token claim 72 giờ.

Chỉ khi accept:

- đúng email;
- đúng token;
- chưa expiry;
- transaction accept;
- mới gắn owner relation.

## 6.2 Transfer

- token 24 giờ;
- owner cũ giữ quyền khi pending;
- owner mới chỉ có quyền sau accept;
- resend vô hiệu token cũ;
- decline/cancel không đổi owner;
- replay không apply lần hai.

---

# 7. DUYỆT/TỪ CHỐI THEO CAMPUS

Không còn HO duyệt tập trung toàn request.

Luồng:

```text
Submit
→ mỗi campus gửi tới Staff Leader campus đó
→ Staff Leader duyệt/từ chối độc lập
```

Ví dụ:

| Campus | Kết quả |
|---|---|
| HN | Approved |
| HCM | Rejected |
| ĐN | Pending |

Request-level status chỉ là trạng thái tổng hợp.

Audit decision phải có:

- request_id;
- campus_id;
- visit_instance_id;
- actor;
- source type;
- reason;
- timestamp.

---

# 8. EDIT, RESUBMIT, SAFE EDIT VÀ AMENDMENT

## 8.1 Pending edit

- kiểm tra row version;
- cập nhật đúng campus;
- không ảnh hưởng sibling;
- recalculate scope/mixed/fingerprint;
- revision;
- conflict rõ.

## 8.2 Resubmit

- chỉ campus rejected;
- sửa detail campus đó;
- sibling approved không reset;
- lifecycle đúng;
- revision/audit đầy đủ.

## 8.3 Safe edit

Thay đổi ít ảnh hưởng có thể apply ngay:

- registrant/contact metadata;
- note;
- transportation note;
- một số thông tin không ảnh hưởng approval.

## 8.4 Amendment

Thay đổi ảnh hưởng approval phải tạo amendment:

- delegation name;
- purpose;
- working content;
- guest/support;
- language;
- operational contact quan trọng;
- schedule approved;
- logistics-sensitive data.

Snapshot cũ vẫn có hiệu lực cho tới khi amendment được duyệt.

## 8.5 Media withdrawal

Media consent chuyển sang DECLINED:

- áp dụng khẩn cấp;
- không chờ approval;
- notification/audit rõ.

---

# 9. HỦY, INVITATION, AGENDA, LOGISTICS VÀ CLOSING

## 9.1 Cancel

Phân biệt REJECTED và CANCELLED.

- trước approval: reject;
- owner có thể cancel theo lifecycle;
- sau approval: actor/time/status guard;
- không cancel khi DURING/AFTER/CLOSED nếu contract cấm;
- sibling campus không tự bị cancel nếu chỉ target một campus.

## 9.2 Invitation

- gắn đúng instance;
- recipient đúng;
- token single-use;
- ACCEPT/DECLINE;
- replay trả already responded;
- audit đầy đủ campus/request/instance.

## 9.3 Agenda

- thuộc đúng instance;
- Host scope;
- deterministic ordering;
- template apply không ghi sibling;
- report dùng đúng target agenda.

## 9.4 Logistics

Lifecycle:

```text
REQUESTED
→ ASSIGNED
→ ACCEPTED
→ IN_PROGRESS
→ DONE
```

Các nhánh:

- CHANGE_PROPOSED;
- REJECTED;
- DECLINED;
- CANCELLED.

Asset handover:

- borrow/return riêng;
- hai chữ ký;
- close-stage dependency.

## 9.5 Closing

Host điều khiển:

```text
BEFORE_VISIT
→ DURING_VISIT
→ AFTER_VISIT
→ CLOSED
```

CLOSE chỉ khi:

- qua end time;
- logistics terminal;
- handover đủ chữ ký;
- minutes action items done/cancelled;
- có published news hoặc xác nhận không cần.

---

# 10. SCOPE VÀ BẢO MẬT DỮ LIỆU PER-CAMPUS

Thứ tự query đúng:

```text
base query
→ actor scope
→ campus/department/participant restriction
→ keyword/filter
→ projection
→ sort/pagination
```

Không được:

```text
search toàn hệ thống
→ match sibling campus
→ rồi mới lọc scope
```

Vì có thể lộ:

- existence;
- keyword;
- total count;
- pagination;
- sibling data.

Backend phải scope trước projection.

Không gửi toàn bộ sibling data rồi ẩn bằng frontend.

---

# 11. TIẾN ĐỘ PHASE 1 ĐẾN PHASE 7

## Phase 1 — VERIFIED

Đã hoàn thành:

- Pure V2 production violation = 0.
- Dead compatibility projection removed.
- Guard `CampusVisits` covered.
- XML-doc/comment aligned.
- Integration test isolation fixed.
- Baseline development ready.

## Phase 2 — VERIFIED

Đã đóng core write/read flows:

- create;
- OTP;
- pending edit;
- resubmit;
- safe edit;
- amendment;
- claim;
- transfer;
- cancel;
- approve/reject;
- assign host;
- invitation response.

Đã sửa audit context per-campus.

## Phase 3 — VERIFIED

Đã lập Query Consumer Matrix.

Đã kiểm tra:

- list/detail/search;
- scope-before-keyword;
- minutes query N+1;
- feedback/news/doc-search/minutes export;
- cross-campus leakage.

## Phase 4 — Code gates green

Đã sửa:

- feature flags mặc định false làm V2 chết ngoài Testing;
- frontend routing theo `formSchemaVersion`;
- capability/dead routing.

Frontend lint/unit/build xanh.

Critical E2E được defer.

## Phase 5 — VERIFIED

Visit-adjacent modules:

- face-tag anti-IDOR;
- expense scope;
- agenda;
- minutes mutation;
- feedback notification LEFT JOIN;
- logistics cancel;
- news consent.

## Phase 4.5 — IN PROGRESS

Đây là stabilization phase bổ sung trước Phase 6.

Đã hoàn thành phần lớn:

- readiness;
- error toolkit;
- dependency gating;
- HO dashboard infinite-loading;
- remove After-tab mocks;
- live mock removal;
- upload contract;
- real-stack partial.

Chưa hoàn tất hoàn toàn do browser suite/targeted specs và UI consistency/toast work.

## Phase 6 — PAUSED

Chưa được phép bắt đầu final SQL/E2E gate cho tới khi Phase 4.5 VERIFIED.

## Phase 7 — Chưa bắt đầu

Cleanup:

- orphan tests;
- FormSchemaVersions test references;
- compatibility symbols;
- final release gate.

---

# 12. CHI TIẾT CÁC LỖI VÀ FIX THEO PHASE

## 12.1 Dead projection

Đã xóa ở:

- Create service.
- Edit/resubmit service.

Không còn smallest-campus projection.

## 12.2 Test isolation

Lỗi FK ngẫu nhiên ở UpdateDepartmentApiTests.

Root cause:

- test user chọn department bằng FirstOrDefault không order;
- đôi khi bám vào department test sắp xóa.

Fix:

- chọn seed department deterministic;
- exclude `[IT-`;
- cleanup reassign user trước delete;
- thêm 3 regression tests;
- 5 full integration runs liên tiếp xanh.

## 12.3 Feature flag severe defect

Hai flag mặc định false:

- Development/Production không có config;
- V2 endpoint 404;
- không V1 fallback.

Fix:

- mặc định true;
- deprecated kill-switch;
- tests.

## 12.4 Frontend route severe defect

Frontend rẽ theo `formSchemaVersion`, trong khi backend không emit.

Kết quả:

- route sang `/unsupported-version`;
- route không tồn tại.

Fix:

- route luôn V2;
- remove formSchemaVersion khỏi types;
- modal 409-driven.

## 12.5 Minutes N+1

Từng có ~90 query/page.

Fix:

- query cố định;
- scope-before-keyword;
- query-count test.

## 12.6 Document owner context

Trước đây lấy schedule từ `CampusInstances.FirstOrDefault()`.

Fix:

- instance document → target instance date;
- request document → earliest start / latest end.

## 12.7 Expense leak

Hai expense read endpoint chỉ `[Authorize]`, không scope.

Fix:

- VisitExpenseAccessScope;
- cross-campus read leak closed;
- generated total contract tests.

## 12.8 Face-tag anti-IDOR

- guest phải thuộc scan instance;
- sibling/foreign request bị từ chối;
- replay conflict;
- stranger forbidden.

## 12.9 Feedback notification LEFT JOIN

Owner-WIP được:

- stash backup;
- audit;
- test real MySQL;
- commit riêng;
- stash backup chưa drop.

---

# 13. PHASE 4.5 — STACK, ERROR-STATE VÀ UI STABILIZATION

## 13.1 Pure V2 DB readiness

Đã thêm:

```text
GET /api/health/live
GET /api/health/readiness
```

Readiness kiểm tra:

- bảng/cột Pure V2;
- reintroduced V1 columns;
- DB name;
- environment;
- Production hides detail;
- no secret leak.

## 13.2 Error toolkit

Đã có:

- `normalizeApiError`;
- LoadingState;
- EmptyState;
- ErrorState;
- StaleDataBanner.

Phân loại:

- forbidden;
- notFound;
- conflict;
- validation;
- server;
- network;
- timeout;
- unknown.

Riêng:

- `VISIT_FORM_DETAIL_MISSING`.

## 13.3 Dependency gating

Reminder và agenda candidates trước đây nuốt lỗi thành default.

Fix:

- track failure;
- stale banner;
- retry;
- block Save/Cancel/assign;
- không mutation bằng `[]` giả.

## 13.4 HO Dashboard infinite-loading

Root cause:

- fetch fail chỉ console.error;
- `data=null`;
- condition `loading || !data` giữ spinner mãi.

Fix:

- explicit error state;
- retry;
- spinner finite;
- 3 component tests.

## 13.5 After-Visit News mocks

Đã xác minh News workflow thật đã tồn tại.

Đã xóa:

- Tokyo mock;
- Monash mock;
- Unsplash preview;
- hardcoded folder ID;
- fake generator.

## 13.6 Live mock removal

Đã sửa:

- VisitRequestDetail hardcoded Tokyo/2023.
- VisitDuringTab Kenji/Tokyo/rating/notes/action items.
- Business Card default Takahiro Sato.
- localStorage doc/contact lists.

## 13.7 Upload contract

Đã chốt và implement Visit Photo:

```text
JPEG/PNG/WebP
5 MB/file
10 files/request
no video/PDF
magic-byte check
```

Frontend/backed đã đồng bộ theo báo cáo.

---

# 14. QUYẾT ĐỊNH UI/UX ĐÃ KHÓA

## 14.1 Visit detail

Nên mở từ list bằng modal/drawer lớn nhưng vẫn:

- route/deep link;
- refresh;
- browser back;
- giữ filter/page/scroll;
- dùng chung detail component.

## 14.2 Guest/support table

Chuẩn:

| STT | Họ và tên | Chức vụ | Đơn vị công tác | Quốc tịch |

Mobile phải đủ cùng dữ liệu.

## 14.3 Status localization

Không hiển thị raw:

- DRAFT;
- SAVED;
- PRESENT;
- EXCUSED;
- GENERAL;
- VISIT;
- COVERAGE_GENERAL;
- CANCELLED;
- AFTER_VISIT;
- PENDING_REVIEW.

Phải mapping tiếng Việt tập trung.

## 14.4 Audit/history

Không hiển thị:

```text
source=CREATE;approvalRevision=1
SOURCE_TYPE=CAMPUS_DECISION
Seed migration
```

Phải chuyển thành UI:

- hành động;
- actor;
- time;
- reason;
- before/after;
- version;
- campus.

## 14.5 Date locale

Hiển thị:

```text
dd/MM/yyyy
```

Không dùng `mm/dd/yyyy` ở UI tiếng Việt.

---

# 15. TOAST, LOCALIZATION, RICH TEXT VÀ MUTATION FEEDBACK

## 15.1 Rich text Minutes

Lỗi:

- raw `<h2>`, `<p>`, `<strong>` xuất hiện.

Yêu cầu:

- sanitize;
- render HTML an toàn;
- list preview dùng plain-text excerpt;
- chặn script/event handler/unsafe URL.

## 15.2 Toast policy

Mọi mutation chủ động phải có:

- toast success;
- toast failure;
- inline validation giữ nguyên;
- modal không đóng khi fail.

Bao gồm:

- create;
- save;
- edit;
- delete;
- cancel;
- approve;
- reject;
- restore;
- submit;
- assign;
- transfer;
- status change;
- lock/unlock;
- upload.

Không bắt toast cho:

- mark notification read;
- autosave;
- acquire/release lock;
- debounce translation;
- polling.

## 15.3 Một helper toast

Dùng:

```text
shared/utils/toast.ts
```

Không tiếp tục tạo:

- pushToast;
- showToast;
- setToast;
- alert();
- local toast systems.

## 15.4 Mutation gaps đã phát hiện

- Edit request / Save changes.
- Quick edit.
- Cancel.
- Approve/assign host.
- Reject.
- Amendment submit/approve/reject/withdraw.
- Primary contact transfer/resend/cancel.
- Invitation accept/decline.
- Department assignment dùng alert.
- Account lock/unlock/status.
- Department create/update/status.
- Replace Staff Leader.
- FAQ visibility toggle.
- Delete email draft.
- Translation/preview silent fail.

---

# 16. UPLOAD CONTRACT ĐÃ KHÓA

## Visit Photos

```text
Allowed:
- image/jpeg
- image/png
- image/webp

Max:
- 5 MB/file
- 10 files/request

Not allowed:
- video
- PDF
```

Backend source of truth.

Kiểm tra:

- MIME;
- extension;
- MIME-extension match;
- magic bytes;
- size;
- count.

## Business Card

Riêng:

```text
JPEG/PNG/WebP/PDF
10 MB
1 file
```

## News/Gallery/Documents

Giữ contract riêng, không ép giống Visit Photos.

---

# 17. REAL-STACK E2E VÀ EVIDENCE POLICY

## 17.1 Stack

```text
MySQL disposable
→ .NET API
→ Vite
→ Chromium
```

## 17.2 Safety

- không dùng pems_db;
- không Railway/Production;
- không real email;
- không kill process lạ;
- port khác nếu bị chiếm;
- file-sink email.

## 17.3 Real-stack drift đã phát hiện

1. `workingContent: null` trong seed.
2. locator `input[name="registerInfo.organization"]` stale vì PartnerOrgCombobox.

Fix đã chuẩn bị:

- workingContent hợp lệ;
- placeholder/combobox locator thật.

## 17.4 Targeted specs cần có

- Dashboard HO;
- Visit Process;
- Visit Photos;
- Minutes;
- Feedback;
- VisitRequestDetail;
- VisitDuringTab;
- Upload;
- OTP;
- mutation toast;
- localized statuses;
- rich text.

## 17.5 Evidence package

Phải cùng một HEAD:

- git head/status;
- frontend/backend URL;
- DB name;
- readiness;
- backend/frontend logs;
- TRX;
- Playwright report;
- traces;
- screenshots;
- HAR;
- OTP DB state;
- timestamps.

---

# 18. P0 ACCOUNT EMAIL CONFIRMATION & TRUTHFUL EMAIL DELIVERY

Đây là một stream công việc bảo mật mới, hiện **IN PROGRESS**.

## 18.1 Root cause P0 #1

`CreateAccountCommandHandler` tạo `ACTIVE` ngay.

Rủi ro:

- nhập nhầm email;
- người nhận nhầm SSO-login;
- role/head authority cấp trước khi verify email.

## 18.2 Root cause P0 #2

`AddDepartmentPersonnel`:

- path song song;
- thiếu auth;
- tạo ACTIVE;
- hardcode URL;
- bypass shared provisioning.

## 18.3 Root cause P0 #3

SMTP disabled:

- trước đây log body/OTP/token;
- trả success;
- caller ghi SENT.

## 18.4 Thiết kế đã khóa

### Status mới

```text
PENDING_EMAIL_CONFIRMATION
```

Không dùng INACTIVE.

### Authority timing

```text
Reserve slot at creation
Activate authority at confirmation
```

Pending Head:

- giữ slot;
- không có effective authority.

### Confirmation table

```text
account_email_confirmations
```

Tối thiểu:

- confirmation_id;
- user_id;
- target_email;
- token_hash;
- status;
- expires_at;
- resend_count;
- created_at;
- updated_at;
- confirmed_at;
- cancelled_at.

Không lưu plaintext token.

## 18.5 Truthful email delivery

Result:

```text
Sent
Failed
Skipped
```

Rules:

- Dev/Test SMTP disabled → Skipped.
- Production disabled → Failed.
- Provider accepted → Sent.
- Provider exception → Failed.

Không ghi SENT khi Skipped.

## 18.6 Account creation

- create pending;
- reserve Head slot;
- no effective authority;
- create confirmation;
- commit;
- send after commit;
- record truthful delivery.

## 18.7 Confirmation transaction

- hash token;
- lock row;
- status pending;
- not expired;
- target email matches;
- account pending;
- reservation valid;
- user → ACTIVE;
- confirmation → CONFIRMED;
- revoke others;
- audit;
- commit.

## 18.8 Resend/Edit email

- old token invalidated;
- new token hash;
- cooldown/rate limit;
- target email update;
- old email cannot confirm.

## 18.9 Cancel/Expire/Delete

- confirmation cancelled/expired;
- release Head reservation;
- no authority leak.

## 18.10 Frontend

- pending badge;
- confirm page;
- resend;
- edit email;
- cancel pending;
- pending excluded from candidates;
- create-account message reflects Sent/Skipped/Failed.

## 18.11 P0 order

```text
1. Sensitive log fix
2. AddDepartmentPersonnel containment
3. Truthful delivery
4. Pending confirmation flow
5. Shared provisioning
6. Full P0 verification
7. Then P1
```

---

# 19. CREDENTIAL POLICY VÀ PRODUCTION SECURITY DEBT

## 19.1 Current policy

```text
Shared development credentials intentionally tracked
Production security hardening deferred
```

Không tự:

- remove dev credentials;
- replace placeholder;
- rewrite history;
- rotate shared dev credentials.

## 19.2 Debt

Base appsettings có non-blank dev secrets.

Rủi ro:

- Production thiếu env override vẫn dùng dev key.

Phải xử lý trước production:

- production-safe base config;
- secret manager/env;
- fail-fast khi thiếu secret;
- rotate production/personal credential;
- không reuse dev credentials.

---

# 20. GIT WORKFLOW, COMMIT POLICY VÀ STOP CONDITIONS

## 20.1 Preflight

Luôn chạy:

```bash
git status --short --branch
git branch --show-current
git rev-parse HEAD
git fetch origin Cảnh-Iter1
git rev-parse origin/Cảnh-Iter1
git rev-list --left-right --count origin/Cảnh-Iter1...HEAD
git log -15 --oneline --decorate
git stash list --format="%gd %H %s"
git diff --check
```

## 20.2 Stop conditions

Nếu:

- remote đổi;
- working tree có unknown change;
- teammate WIP;
- destructive DB;
- production credential/deploy;
- platform limit;

thì:

- không merge;
- không rebase;
- không reset;
- không push;
- báo và dừng.

## 20.3 Commit policy

Commit theo functional slice.

Không:

- mỗi file một commit;
- `fix`;
- `fix again`;
- report-only;
- test-count-only;
- amend pushed commits;
- AI names;
- `git add .`.

## 20.4 Push policy

- push sau slice green;
- fetch trước push;
- remote unchanged;
- no force push.

---

# 21. TEST POLICY VÀ DEFINITION OF DONE

## 21.1 Khi fail

Giữ lần fail đầu:

- TRX;
- logs;
- screenshot;
- trace;
- HAR;
- DB name;
- stack trace;
- cleanup state.

Không rerun đè.

## 21.2 Không được

- skip test;
- disable test;
- giảm assertion;
- tắt FK;
- tắt trigger;
- FOREIGN_KEY_CHECKS=0;
- delay/retry che race;
- sửa production chỉ để fixture pass.

## 21.3 Gate

Backend:

- build;
- Architecture;
- Unit;
- Integration.

Frontend:

- lint;
- unit;
- build.

Browser:

- existing realstack;
- targeted specs.

Database:

- disposable;
- readiness;
- cleanup;
- protected DB untouched.

---

# 22. KNOWN DEBTS VÀ BACKLOG

## P2-F1

`tests/PEMS.ApplicationTests/`:

- khoảng 139 `.cs`;
- không `.csproj`;
- không solution;
- chưa compile.

Phase 7 quyết định restore/delete.

## P2-F2

`FormSchemaVersions`:

- production consumer 0;
- khoảng 98 references trong test harness.

Phase 7 cleanup.

## Security debt

- base appsettings non-blank secrets;
- pre-production.

## Browser debt

- real-stack suite cần repair;
- targeted specs chưa hoàn tất.

## Toast debt

- Mutation Feedback Matrix chưa 100% verified.

---

# 23. CÁC COMMIT QUAN TRỌNG ĐÃ ĐƯỢC BÁO CÁO

> Danh sách dưới đây là lịch sử được báo trong hội thoại, không thay thế `git log` thực tế.

## Phase 1

- `779c4bcb`
- `59ab4aaf`
- `522b8294`
- `56cda915`
- `c750e324`
- `3de5ebdc`
- `047083aa`
- `954cdb29`

## Phase 2

- `f8c2092c`
- `8528e309`
- `9edc1aa0`
- `3d26f8b1`

## Phase 3

- `3e6a06be`
- `7141e1c3`
- `4a55a2ea`

## Phase 4

- `0f8733ad`
- `edaa5014`

## Owner/merge events

- `70926992`
- `903bbabe`

## Phase 5

- `00e6a2d6`
- `2a9a52af`
- `1c06b131`
- `58b9c100`
- `1ebf88e5`
- `35070d99`
- `d473577e`

## Phase 4.5

- `b58e6a5b`
- `703996a7`
- `7f711122`
- `f483b86d`
- `63f8ba25`
- `2819bebb`

---

# 24. TEST COUNT THEO CHECKPOINT

Các số đã được báo ở nhiều thời điểm:

| Checkpoint | Architecture | Unit | Integration | Frontend Unit |
|---|---:|---:|---:|---:|
| Phase 1 đầu | 14 | 951 | 517 | — |
| Sau guard tests | 14 | 955 | 519/522 | — |
| Phase 3 | 14 | 955 | 552 | — |
| Phase 4 | 14 | 958 | 552 | 383 |
| Phase 5 | 14 | 958 | 569 | 383 |
| Phase 4.5 readiness | 14 | 958 | 576 | 406 |
| Phase 4.5 later | 14 | 964 | 576 | 414 |

Số hiện tại phải lấy từ test run mới, không ép về số cũ.

---

# 25. TRẠNG THÁI HIỆN TẠI VÀ ĐIỂM TIẾP TỤC

Có hai stream đang tồn tại:

## 25.1 Phase 4.5 closure stream

Checkpoint cuối được báo:

```text
HEAD 2819bebb
sync 0/0
stash 8/8
owner-WIP f27b0853
```

Working tree tại checkpoint:

- 2 realstack files đang sửa;
- prompt files untracked.

Việc đang làm:

1. Repair real-stack seed `workingContent`.
2. Repair PartnerOrgCombobox locator.
3. Run all 17 real-stack specs.
4. Commit `test(e2e): repair Pure V2 real-stack browser flows`.
5. Localization/status.
6. Rich text sanitize.
7. Toast standardization.
8. Targeted browser specs.
9. Evidence package.
10. Phase 4.5 verdict.

## 25.2 P0 Account Email Confirmation stream

Checkpoint được báo riêng:

```text
HEAD bc6f62cd
P0 EMAIL/ACCOUNT FLOW IN PROGRESS
```

P0 #3a đã sửa logging nhưng chưa commit tại checkpoint đó.

Việc tiếp:

1. Commit sensitive logging fix.
2. Contain AddDepartmentPersonnel.
3. Truthful delivery.
4. Pending email confirmation.
5. Shared provisioning.
6. Frontend confirmation.
7. File-sink E2E.
8. P0 VERIFIED.
9. Sau đó mới P1/P2.

## 25.3 Cảnh báo về hai HEAD

Hai checkpoint `2819bebb` và `bc6f62cd` xuất hiện ở hai nhánh tiến trình khác nhau trong hội thoại.

Người tiếp quản phải:

- chạy `git log`;
- xác định commit ancestry;
- xác định stream nào đang ở working tree;
- không reset về một HEAD chỉ vì tài liệu ghi;
- không trộn WIP của hai stream.

---

# 26. CHECKLIST BÀN GIAO CHO NGƯỜI TIẾP QUẢN

## A. Git

- [ ] Đúng branch.
- [ ] Fetch remote.
- [ ] Ahead/behind rõ.
- [ ] Stash đủ.
- [ ] Owner-WIP hash còn nguyên.
- [ ] Prompt files không add.
- [ ] Unknown changes được phân loại.
- [ ] Không force-push.

## B. Pure V2

- [ ] Không global projection.
- [ ] Không FormSchemaVersion runtime.
- [ ] Không first/smallest campus.
- [ ] Scope trước projection.
- [ ] Missing detail fail-closed.
- [ ] Cross-campus leak = 0.

## C. Frontend stabilization

- [ ] Error khác empty.
- [ ] Dashboard không loading vô hạn.
- [ ] Dependency fail block action.
- [ ] Status tiếng Việt.
- [ ] Rich text sanitize.
- [ ] Modal/deep link.
- [ ] Tables có STT.
- [ ] Desktop/mobile parity.
- [ ] Toast success/failure.

## D. OTP Visit Request

- [ ] Verify success tạo request.
- [ ] Fail giữ form.
- [ ] Replay idempotent.
- [ ] Mixed campus đủ detail.
- [ ] Evidence DB.

## E. Upload

- [ ] Visit Photo 5 MB.
- [ ] 10 files.
- [ ] JPEG/PNG/WebP.
- [ ] Magic bytes.
- [ ] FE/BE aligned.

## F. Account confirmation

- [ ] Pending status.
- [ ] Login/SSO/refresh blocked.
- [ ] Truthful email outcome.
- [ ] Token hash only.
- [ ] Confirm POST.
- [ ] Resend/edit revoke old token.
- [ ] Reservation Head.
- [ ] No effective authority before confirm.
- [ ] Cancel/expire releases slot.
- [ ] AddDepartmentPersonnel shared flow.
- [ ] Frontend confirm page.
- [ ] File-sink E2E.

## G. Evidence

- [ ] Same HEAD.
- [ ] Logs.
- [ ] TRX.
- [ ] Playwright report.
- [ ] Trace/screenshots.
- [ ] HAR.
- [ ] DB name/state.
- [ ] No secret leak.
- [ ] Cleanup 0.

---

# KẾT LUẬN

Dự án PEMS đã hoàn thành phần lớn quá trình chuyển đổi Pure V2:

- Core per-campus model đã ổn định.
- Phase 1–5 đã được báo VERIFIED.
- Nhiều lỗi scope, audit, routing, readiness, expense, minutes, document, photo và test isolation đã được sửa.
- Phase 4.5 đang hoàn thiện UI/error/browser consistency.
- Một stream bảo mật P0 mới về email/account confirmation đang được triển khai và phải hoàn tất trước P1/P2.
- Phase 6 SQL canonical + final real-stack E2E chưa được phép đóng.
- Phase 7 cleanup chưa bắt đầu.

Trạng thái trung thực:

```text
Pure V2 core: VERIFIED
Phase 1–5: VERIFIED
Phase 4.5: IN PROGRESS
P0 Account Email Confirmation: IN PROGRESS
Phase 6: PAUSED
Phase 7: NOT STARTED
Project: NOT YET FINAL
```

Người tiếp quản không được chỉ dựa vào báo cáo để kết luận hoàn thành. Phải xác minh source thật, Git thật, test thật và evidence cùng một HEAD.
