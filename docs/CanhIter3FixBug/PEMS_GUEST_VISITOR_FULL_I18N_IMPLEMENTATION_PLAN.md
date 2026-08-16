# PEMS — Kế hoạch triển khai i18n VI/EN 100% cho GUEST và VISITOR

> **Mục tiêu:** hoàn tất song ngữ Việt/Anh cho toàn bộ UI mà **Guest/Anonymous** và người dùng đăng nhập với **role = VISITOR** có thể nhìn thấy hoặc tương tác.
>
> **Repository:** `quangthoai04/PEMS`  
> **Baseline branch:** `Dev`  
> **Baseline commit:** `ceeb4e3b022a5afc350cce9444e9386104488c6e`  
> **Frontend:** `frontend/pems-react/src/`
>
> Tài liệu này được lập từ audit hiện tại + đối chiếu code thực tế. Không coi việc “đã có i18next” hoặc “VI/EN JSON parity” là đồng nghĩa với UI đã dịch 100%.

---

# 1. Mục tiêu hoàn thành

Chỉ được kết luận:

```text
GUEST: PASS 100%
VISITOR: PASS 100%
OVERALL: PASS 100%
```

khi đồng thời thỏa mãn tất cả điều kiện sau:

1. 100% route Guest có thể truy cập đã được audit.
2. 100% route VISITOR có thể truy cập đã được audit.
3. Không còn user-facing string hard-code chỉ tiếng Việt hoặc chỉ tiếng Anh ở scope Guest/VISITOR.
4. Không còn trạng thái/role/enum kỹ thuật hiển thị raw như `VISITOR`, `WAITING_REQUEST_APPROVAL`, `PARTNER_TYPE...`.
5. Không còn date/time/relative-time bị cố định theo tiếng Việt khi đang chọn English.
6. Không còn toast/validation/error/loading/empty/success chỉ có một ngôn ngữ.
7. Không hiển thị trực tiếp backend `message`, `title`, `error.message`, `response.data.message` nếu nội dung đó chưa được localize.
8. Mobile và desktop đều phải dịch đầy đủ.
9. `aria-label`, `title`, tooltip, placeholder, helper text và screen-reader text phải dịch.
10. Đổi ngôn ngữ khi đang đứng trên màn hình phải cập nhật UI đúng.
11. Reload trang phải giữ đúng language đang chọn.
12. VI và EN locale key phải parity, không missing/empty/mojibake.
13. Toàn bộ test gate i18n phải PASS.

---

# 2. Nguyên tắc triển khai

## 2.1. Không dịch các dữ liệu sau

Các giá trị sau có thể giữ nguyên:

- `PEMS`
- `FPT University`, `FPTU`
- email
- URL
- request code như `VR-2026-...`
- campus proper name chính thức nếu backend chỉ lưu một tên chuẩn
- tên cá nhân
- tên tổ chức/đối tác do người dùng nhập
- nội dung do người dùng tự nhập

Các giá trị còn lại nếu là **system-generated user-facing text** thì phải localize.

## 2.2. Không dùng hard-coded fallback khác ngôn ngữ

Không dùng:

```tsx
t('some.key', 'Văn bản tiếng Việt')
```

nếu English có thể rơi xuống defaultValue tiếng Việt.

Phải có key thật trong cả VI và EN.

## 2.3. Không render enum trực tiếp

Không:

```tsx
{user.roleCode}
{partner.partnerType}
{item.instanceStatus}
```

Phải map qua translation key hoặc localized domain-label helper.

## 2.4. Không render raw backend message

Không:

```tsx
toast.success(result.message)
setError(response.data.message)
<p>{item.message}</p>
```

nếu backend chỉ trả một câu cố định.

Ưu tiên:

```text
backend stable code
        ↓
frontend translation key
        ↓
t(...)
```

---

# 3. Kiến trúc i18n mục tiêu

PEMS hiện đã có i18next + react-i18next và hệ thống locale VI/EN. Giữ nguyên nền tảng này.

## 3.1. Namespace nên tái sử dụng

Ưu tiên sử dụng các namespace hiện có:

- `common`
- `publicLayout`
- `visitRequest`
- `visitRequestV2`
- `validation`
- `errors`
- `toast`
- `loginModal`
- `notifications`
- `visitFptu`
- `partners`
- `news`
- `legal`

Nếu số lượng key của Profile/Feedback tăng quá lớn mới cân nhắc thêm:

- `profile`
- `feedback`

Nếu thêm namespace mới bắt buộc:

1. tạo cả `vi/*.json`
2. tạo cả `en/*.json`
3. đăng ký trong `shared/i18n/config.ts`
4. thêm parity test
5. thêm key-resolution test

---

# 4. Phase 0 — Freeze baseline và inventory

Trước khi sửa:

- chốt branch/commit dùng để triển khai;
- đọc lại `App.tsx`;
- đọc `dashboardRouteAccess.ts`;
- chốt route graph Guest;
- chốt route graph Visitor;
- chốt component tree được mount từ từng route;
- đánh dấu route conditional theo ownership/relation/status.

Output bắt buộc:

```text
guest_routes.json / markdown table
visitor_routes.json / markdown table
visitor_conditional_routes.json / markdown table
```

Không bắt đầu sửa từng file ngẫu nhiên trước khi route inventory chốt xong.

---

# 5. Phase 1 — Chuẩn hóa helper dùng chung

Đây là bước nên làm trước để tránh sửa lặp lại hàng chục màn hình.

## 5.1. Localized date/time helper

### Vấn đề hiện tại

Nhiều màn đang dùng:

- `formatVietnamDate`
- `formatVietnamDateTime`
- `toLocaleString('vi-VN')`
- `date-fns/locale/vi`

Điều này khiến English UI vẫn hiển thị format Việt Nam.

### Kế hoạch

Tạo hoặc mở rộng helper dạng:

```ts
formatLocalizedDate(value, language)
formatLocalizedDateTime(value, language)
formatLocalizedRelativeTime(value, language)
```

Yêu cầu:

- timezone nghiệp vụ vẫn có thể giữ `Asia/Ho_Chi_Minh`;
- format hiển thị đổi theo locale:
  - VI → `vi-VN`
  - EN → `en-US` hoặc chuẩn EN mà team chốt;
- không hard-code `vi-VN` trong page/component Visitor-facing.

### File cần rà

- `shared/utils/vietnamTime.ts`
- tất cả caller Guest/VISITOR của helper này.

---

## 5.2. Localized role/status/domain labels

Tạo một hướng thống nhất:

```text
role code
status code
relation code
visit type code
participant role
partner type
media consent
working language
        ↓
translation key
        ↓
localized label
```

Không để mỗi page tự viết một object tiếng Việt riêng.

Các nhóm cần chuẩn hóa:

- User role
- Visit request status
- Campus instance status
- Invitation status
- Relation
- Visit type
- Working language
- Media consent
- Partner type
- Feedback status

---

## 5.3. API error localization

Chuẩn hóa helper dùng cho scope Guest/VISITOR:

```text
errorCode -> errors:api.<CODE>
```

Fallback chỉ dùng generic localized text.

### Quy tắc

Nếu backend có `errorCode`:
- map bằng `errors:api.<CODE>`.

Nếu backend chưa có stable code:
- bổ sung code ở backend cho business error mà Guest/VISITOR có thể gặp.

Nếu là network/timeout:
- dùng `toast/common` localized fallback.

Nếu backend gửi raw Vietnamese message:
- không render trực tiếp ở English.

Nếu backend gửi raw English message:
- không render trực tiếp ở Vietnamese nếu đã có translation key phù hợp.

---

# 6. Phase 2 — Guest P0: các màn chưa dịch nghiêm trọng

## 6.1. `ConfirmEmailPage.tsx`

**File**

```text
frontend/pems-react/src/pages/account/ConfirmEmailPage.tsx
```

### Hiện trạng

Hard-code tiếng Việt ở:

- page title;
- loading;
- confirmed/already confirmed;
- expired;
- invalid;
- server/network error;
- retry;
- back home;
- message bổ sung;
- backend `data.message`.

### Cần làm

1. Import `useTranslation`.
2. Chuyển toàn bộ text sang namespace phù hợp, đề xuất `loginModal` hoặc `common/errors`.
3. Không render `data.message` trực tiếp.
4. Map response `status`:
   - `CONFIRMED`
   - `ALREADY_CONFIRMED`
   - `INVALID`
   - `EXPIRED`
   → localized key.
5. Network/server state dùng `errors`/`toast`.
6. Test VI.
7. Test EN.
8. Test switch language khi đang ở trang.
9. Test missing token.
10. Test expired/invalid/already-confirmed.

### Definition of Done

Không còn user-facing literal Vietnamese trong file.

---

## 6.2. `VisitContactInvitationPage.tsx`

**File**

```text
frontend/pems-react/src/pages/identity/VisitContactInvitationPage.tsx
```

### Hiện trạng

Gần như toàn bộ page hard-code VI:

- claim title;
- transfer title;
- intro;
- accept CTA;
- decline CTA;
- expiration note;
- request code;
- delegation;
- invited email;
- campus;
- expires at;
- status messages;
- signed-in explanation;
- anonymous explanation;
- login Google CTA;
- decline reason;
- submit/loading/error/success;
- `toLocaleString('vi-VN')`;
- raw backend `result.message`;
- raw `response.data.message`.

### Cần làm

1. Dùng `useTranslation(['visitRequestV2', 'errors'])`.
2. Tạo nhóm key:

```text
visitRequestV2:contactInvitation.claim.*
visitRequestV2:contactInvitation.transfer.*
visitRequestV2:contactInvitation.fields.*
visitRequestV2:contactInvitation.status.*
visitRequestV2:contactInvitation.actions.*
visitRequestV2:contactInvitation.auth.*
visitRequestV2:contactInvitation.errors.*
```

3. `effectiveKind` chỉ quyết định key, không quyết định literal.
4. Status map `APPLIED`, `DECLINED`, `EXPIRED`, `CANCELLED`, `SUPERSEDED`, `INVALID` → i18n.
5. Dùng localized datetime helper.
6. Success/error action map bằng code hoặc action-local success text.
7. Không render backend message trực tiếp.
8. Test anonymous claim.
9. Test anonymous transfer.
10. Test authenticated Visitor.
11. Test expired.
12. Test superseded.
13. Test invalid.
14. Test accept.
15. Test decline.
16. Test mobile.

---

# 7. Phase 3 — Guest Public residual issues

## 7.1. OTP rate-limit

**File**

```text
features/visit-request/components/OtpVerificationModal.tsx
```

Đang gọi `visitRequest:otp.rateLimited.title` và `.desc` nhưng fallback là Vietnamese.

### Cần làm

Thêm VI và EN thật:

```text
visitRequest:otp.rateLimited.title
visitRequest:otp.rateLimited.desc
```

Không dùng hard-coded defaultValue.

---

## 7.2. Campus card collapse/expand

**File**

```text
features/visit-request/components/v2/CampusVisitCard.tsx
```

Đảm bảo có key thật cho:

```text
visitRequestV2:card.collapse
visitRequestV2:card.expand
```

và bỏ Vietnamese defaultValue.

Rà luôn:

- `aria-label`
- `title`
- tooltip
- screen reader text

---

## 7.3. Partners

**Files**

```text
pages/PartnersPage.tsx
pages/PartnerDetailPage.tsx
```

### Cần sửa

Không render:

```tsx
{partner.partnerType}
```

Tạo map:

```text
partners:types.<CODE>
```

Rà cả list card, detail, filter, badge, empty state, search và API error.

---

## 7.4. Visit FPTU campus detail

**File**

```text
pages/CampusDetailVisitPage.tsx
```

### Cần sửa

1. `CAMPUS_FALLBACK` description:
   - đưa sang `visitFptu.json`, hoặc
   - lưu bilingual từ backend.
2. Stats:
   - `n nội dung`
   - `n hình ảnh`
   - `n video`
   - `n hỗn hợp`
   → i18n plural-aware.
3. Rà breadcrumb / media kind / aria.
4. Fallback title/description phải theo ngôn ngữ.

---

## 7.5. News

**Files**

```text
pages/NewsPage.tsx
pages/NewsDetailPage.tsx
```

### Cần sửa

Thay formatter cố định Vietnam bằng localized date helper.

Test:

- VI date;
- EN date;
- published date;
- empty date;
- mobile.

---

# 8. Phase 4 — Shared shell khi VISITOR đăng nhập

Đây là blocker diện rộng vì shell xuất hiện trên nhiều route.

## 8.1. `DashboardLayout.tsx`

**File**

```text
components/layout/DashboardLayout.tsx
```

### Cần sửa

- `Dashboard`
- `Mở menu điều hướng`
- mọi `aria-label`
- mọi responsive label

Dùng `common` / `publicLayout`.

---

## 8.2. `Sidebar.tsx`

**File**

```text
components/dashboard/Sidebar.tsx
```

### Cần sửa

- `"Khách"`
- `"Không rõ"`
- `"GUEST"`
- raw `roleCode`
- `"Đóng menu"`
- `"Mở rộng menu"`
- `"Thu gọn menu"`
- `"Về trang chủ"`
- `title`
- `aria-label`

### Role label

Không hiển thị `VISITOR`.

Phải hiển thị:

```text
VI: Khách tham quan
EN: Visitor
```

hoặc wording chính thức team chốt.

---

## 8.3. Public `Header.tsx` sau login

**File**

```text
components/layout/Header.tsx
```

Đặc biệt mobile drawer: không render raw `user.role`. Dùng role-label helper.

Test anonymous/Visitor trên desktop + mobile, cả VI và EN.

---

# 9. Phase 5 — Visitor core journey

## 9.1. Profile

**File**

```text
pages/dashboard/profile/Profile.tsx
```

### Cần dịch toàn bộ

- title;
- labels;
- placeholders;
- buttons;
- gender `MALE/FEMALE/OTHER`;
- nationality fallback;
- validation full name/phone;
- loading;
- error;
- save/cancel;
- avatar upload/replace/cancel/uploading;
- success toast;
- error toast.

`resolveNationalityLabel()` không được ép luôn về tên tiếng Việt. Dùng country localized name theo current language.

---

## 9.2. Visitor visit detail

**File**

```text
pages/dashboard/visit/VisitorVisitDetailPage.tsx
```

### Cần dịch

- loading;
- no host;
- breadcrumb;
- status;
- visitor hero;
- host information;
- time;
- next step;
- agenda;
- submitted form;
- operational contact;
- guest list;
- support list;
- campus information;
- feedback block;
- notifications section;
- public news section;
- cancelled banner;
- all field labels;
- empty states;
- date/time.

Status code phải map → translation key. Bỏ `date-fns/locale/vi` cố định.

---

## 9.3. Feedback modal

**File**

```text
features/feedbacks/components/VisitFeedbackModal.tsx
```

### Cần dịch

- modal title;
- status labels;
- close aria;
- loading;
- load error;
- already submitted;
- submit hint fallback;
- rating progress;
- “N mục sẽ được gửi”;
- “Chấm sao để gửi đánh giá”;
- submit button;
- submitting;
- success toast;
- failure toast.

Rà child components:

```text
FeedbackGroupSection.tsx
rating/comment components
hooks/useVisitFeedback*
```

Nếu backend trả group/target label bằng một ngôn ngữ thì phải localize hoặc trả key/code.

---

## 9.4. Feedback deep-link page

**File**

```text
pages/dashboard/visit/VisitFeedbackPage.tsx
```

Không copy literal riêng. Ưu tiên tái sử dụng key với `VisitFeedbackModal`.

---

# 10. Phase 6 — Visitor secondary/conditional routes

Các route này chỉ bắt buộc nếu route inventory + backend scope chứng minh VISITOR có thể tới.

## 10.1. `CreateVisitRequestEntry.tsx`

Dịch loading sr-only, capability error và retry.

## 10.2. `VisitParticipantInvitationDetail.tsx`

Dịch:

- load error;
- decline validation;
- accept/decline;
- action error;
- invitation status;
- secondary visit status;
- breadcrumb;
- page title;
- request form CTA;
- contribution CTA;
- rejection modal;
- placeholders;
- reason character rules;
- raw backend message.

Nếu cuối cùng chứng minh Visitor không reachable → đánh dấu `N/A for Visitor`, không tự suy đoán.

## 10.3. `VisitRequestDetail.tsx`

Dịch breadcrumb, task type, field labels, status map, visit type map, working-language map, media-consent map, loading/error, date/time.

## 10.4. `VisitProcessSummaryPage.tsx`

Dịch permission states, loading, denied, empty, section titles, workspace status, status text, buttons, breadcrumb, timeline, date/time.

## 10.5. `VisitContributionPage.tsx`

Dịch instance status, relation, participant role/status, logistics status, page title, breadcrumb, denied/error/retry, section names, empty states, workspace placeholders, date/time.

Rà child components:

```text
MinutesContributionSection
MediaContributionSection
NewsContributionSection
```

## 10.6. `VisitProcess.tsx` — chỉ phần Visitor có thể thấy

Không cần dịch toàn bộ staff workflow nếu ngoài scope.

Phải xác định chính xác block render khi `isVisitor === true`, gồm các phần như:

- Album ảnh
- Bài tin tức
- Visitor-specific notice
- CTA/back/navigation
- empty/error/loading states

---

# 11. Phase 7 — VisitRequestManagement: hoàn thiện phần còn sót

**File**

```text
pages/dashboard/visit/VisitRequestManagement.tsx
```

> File này đã có i18n cho Visitor ở nhiều phần, không được xem là 0%.

Rà toàn bộ nhánh chạy với `isVisitor === true`, tập trung:

- cancel validation;
- cancel confirm;
- cancel success;
- cancel error;
- status fallback;
- empty/fallback row text;
- `Không có tên`;
- action menu;
- campus accordion;
- reject/cancel reason;
- feedback entry;
- responsive mobile labels;
- tooltip/title/aria;
- date/time.

Nếu literal chỉ dành cho Staff/HO/Department và không bao giờ render cho Visitor → ghi `out of scope`.

Nếu shared component mà Visitor có thể render → bắt buộc localize.

---

# 12. Phase 8 — Notifications: xử lý dynamic content từ backend

## 12.1. Frontend

**File**

```text
pages/notifications/NotificationsPage.tsx
```

UI chrome đã dùng `t()` khá tốt, nhưng:

```text
item.title
item.message
item.timeAgoText
```

đang render trực tiếp.

## 12.2. Backend

**File chính cần rà**

```text
backend/PEMS.Application/Notifications/Queries/GetMyNotifications/GetMyNotificationsQueryHandler.cs
```

Backend hiện tạo Vietnamese relative-time như:

```text
Vừa xong
x phút trước
x giờ trước
x ngày trước
```

### Relative time

Visitor UI mới chỉ dùng `CreatedAt`, frontend tính bằng `formatLocalizedRelativeTime`.

Có thể giữ `TimeAgoText` cho client cũ nhưng không dùng ở Guest/VISITOR bilingual UI.

### Notification title/message

Mục tiêu tốt nhất:

```json
{
  "messageKey": "notifications:events.visitApproved",
  "messageParams": {
    "requestCode": "VR-..."
  }
}
```

Frontend:

```tsx
t(messageKey, messageParams)
```

Nếu chưa refactor schema toàn bộ, map `notificationType/category/actionType` → translation key cho tất cả notification Visitor có thể nhận.

Audit cả:

- Notification bell;
- Notification detail modal;
- Notifications page;
- deep-link labels;
- pending-feedback synthetic notification.

---

# 13. Phase 9 — My Contact Invitations

**File**

```text
pages/dashboard/visit/MyContactInvitationsPage.tsx
```

Phần chính đã i18n. Còn:

1. `formatVietnamDateTime` → localized date/time.
2. `showSuccessToast(result.message)` → localized success key.
3. `AnsweredOutcome.message` không phụ thuộc raw backend sentence.
4. rà decline/accept dynamic messages.
5. test VI/EN.

---

# 14. Phase 10 — V2 Create / Detail / Edit residual audit

Các màn này đã có i18n tương đối tốt, không làm lại từ đầu.

Audit residual trên:

```text
VisitRequestV2Page
VisitRequestFormV2
VisitRequestV2DetailPage
VisitRequestV2DetailView
EditVisitRequestV2Page
EditPendingCampusV2Page
CampusVisitCard
CampusVisitDetailCard
VisitHistoryTimeline
VisitHistoryDetailDrawer
VisitAmendment*
VisitSafeEdit*
ContactLinkPromptDialog
Excel import components
```

Tìm:

- hardcoded Vietnamese;
- hardcoded English;
- defaultValue Vietnamese;
- date locale;
- raw API message;
- raw enum;
- aria/title;
- validation;
- toast;
- modal;
- status;
- empty/loading/error.

Không coi file có `useTranslation()` là đã PASS.

---

# 15. Backend/API message leak remediation

Grep toàn frontend Guest/VISITOR dependency graph:

```text
response.data.message
response?.data?.message
data.message
result.message
error.message
e.message
response.data.title
toast.success(...)
toast.error(...)
setError(...)
setMessage(...)
setOutcome(...)
```

Phân loại mỗi occurrence:

```text
A. technical/log only → OK
B. user-facing + stable errorCode mapping → PASS
C. user-facing raw backend text → FAIL
D. user-entered content → N/A
```

Mục tiêu Guest/VISITOR business errors:

```text
stable code -> i18n key -> localized UI
```

---

# 16. Hard-code audit sau khi sửa

Scan toàn bộ dependency graph Guest/VISITOR.

Tìm cả Vietnamese và English literals:

```text
>Văn bản<
placeholder="..."
title="..."
aria-label="..."
alt="..."
window.alert(...)
window.confirm(...)
window.prompt(...)
toast.*
setError(...)
setMessage(...)
return '...'
label: '...'
```

Không được chỉ scan ký tự có dấu, vì English hard-code như `Dashboard`, `Close`, `Back`, `Retry`, `Submit`, `Loading`, `Visitor` cũng là lỗi bilingual.

---

# 17. Locale key audit

## 17.1. Parity

Với mọi namespace:

```text
VI keys == EN keys
```

## 17.2. Empty values

Không có:

```json
"key": ""
```

## 17.3. Missing literal key

Mọi `t('namespace:key')` phải resolve.

## 17.4. Pluralization

Phải hiểu đúng i18next:

```text
key_one
key_other
```

Không báo false-positive rằng `t('key', {count})` missing khi suffix tồn tại.

---

# 18. Language switching test

Mỗi nhóm route quan trọng:

1. mở bằng VI;
2. chuyển EN;
3. không reload;
4. toàn màn đổi EN;
5. validation hiện tại đổi đúng;
6. toast mới theo EN;
7. modal mới theo EN;
8. điều hướng route con vẫn EN;
9. refresh vẫn EN;
10. đổi lại VI;
11. toàn bộ quay lại VI.

Đặc biệt test:

- Header;
- Sidebar;
- Profile;
- Visit list;
- Visit detail;
- Feedback;
- Notification;
- Public registration;
- Operational Contact invitation.

---

# 19. Automated test plan

## 19.1. Locale parity test

Fail nếu có:

```text
missing VI
missing EN
empty
mojibake
```

## 19.2. Translation-key resolution test

Scan:

```text
t('...')
i18n.t('...')
<Trans i18nKey="...">
```

và hỗ trợ plural suffix.

## 19.3. Guest route smoke test

Chạy toàn Guest route inventory ở VI và EN. Check render, không lộ key, không mixed language, không raw enum.

## 19.4. Visitor route smoke test

Đăng nhập fixture `VISITOR`.

Bao gồm state fixtures:

- pending contact confirmation;
- pending approval;
- approved;
- rejected;
- cancelled;
- before visit;
- during visit;
- after visit;
- closed;
- feedback pending/submitted.

## 19.5. Responsive test

Ít nhất desktop + mobile cho:

- Header mobile drawer;
- Dashboard mobile app bar;
- Sidebar;
- Visit list mobile card;
- modal/bottom sheet.

## 19.6. Backend-message leak test

Mock backend trả:

```text
Vietnamese raw message
English raw message
unknown error code
known error code
```

Kỳ vọng:

- known code → localized text;
- unknown → localized generic fallback;
- không leak raw language sai.

---

# 20. Implementation batches / commit plan

## Batch 1 — Shared i18n infrastructure

- localized date/time;
- role/status/domain mapping;
- API error mapping;
- tests.

## Batch 2 — Guest critical

- ConfirmEmailPage;
- VisitContactInvitationPage;
- OTP keys.

## Batch 3 — Guest public residual

- Partners;
- Campus Detail Visit FPTU;
- News date;
- accessibility residual.

## Batch 4 — Visitor shared shell

- DashboardLayout;
- Sidebar;
- Header.

## Batch 5 — Visitor core

- Profile;
- VisitorVisitDetailPage;
- Feedback modal/page.

## Batch 6 — Visitor conditional routes

- RequestDetail;
- ProcessSummary;
- Contribution;
- ParticipantInvitation;
- CreateEntry;
- Visitor-visible VisitProcess blocks.

## Batch 7 — Dynamic/backend content

- Notifications;
- MyContactInvitations;
- API message leakage.

## Batch 8 — VisitRequestManagement + V2 residual

- remaining hardcodes;
- aria/title;
- date/time;
- dynamic state.

## Batch 9 — Full audit gate

- route coverage;
- locale parity;
- key resolution;
- no hardcode;
- language switching;
- responsive;
- final report.

---

# 21. Priority order

## P0 — bắt buộc sửa trước

1. `ConfirmEmailPage.tsx`
2. `VisitContactInvitationPage.tsx`
3. `Profile.tsx`
4. `VisitorVisitDetailPage.tsx`
5. `VisitFeedbackModal.tsx`
6. `VisitFeedbackPage.tsx`
7. `DashboardLayout.tsx`
8. `Sidebar.tsx`
9. Visitor role label trong `Header.tsx`
10. Notifications raw title/message/time

## P1 — Visitor journey

11. `VisitRequestDetail.tsx`
12. `VisitProcessSummaryPage.tsx`
13. `VisitContributionPage.tsx`
14. `VisitParticipantInvitationDetail.tsx`
15. `CreateVisitRequestEntry.tsx`
16. Visitor-visible block trong `VisitProcess.tsx`
17. `MyContactInvitationsPage.tsx`
18. residual trong `VisitRequestManagement.tsx`

## P2 — Public residual

19. OTP rate-limit key
20. CampusVisitCard collapse/expand key
21. Partner type
22. Campus fallback description
23. Campus gallery stats
24. News date locale
25. final accessibility/hardcode scan

---

# 22. Per-file Definition of Done

Một file/page chỉ PASS khi:

- [ ] Có translator ở đúng component scope.
- [ ] Không có user-facing hard-code chỉ VI.
- [ ] Không có user-facing hard-code chỉ EN.
- [ ] Không có raw role/status/enum.
- [ ] Không có Vietnamese fixed date locale.
- [ ] Không leak backend message.
- [ ] Validation bilingual.
- [ ] Toast bilingual.
- [ ] Loading bilingual.
- [ ] Empty bilingual.
- [ ] Error bilingual.
- [ ] Success bilingual.
- [ ] Modal bilingual.
- [ ] Tooltip bilingual.
- [ ] `aria-label` bilingual.
- [ ] `title` bilingual.
- [ ] Mobile bilingual.
- [ ] Desktop bilingual.
- [ ] Switch language tại chỗ hoạt động.
- [ ] VI test PASS.
- [ ] EN test PASS.

---

# 23. Final audit checklist

## Guest

- [ ] Home
- [ ] Header
- [ ] Footer
- [ ] Login modal
- [ ] News list
- [ ] News detail
- [ ] Partners list
- [ ] Partner detail
- [ ] Visit FPTU
- [ ] Campus detail/gallery
- [ ] FAQ
- [ ] Privacy
- [ ] Terms
- [ ] Forgot password
- [ ] Reset password
- [ ] Confirm email
- [ ] Operational Contact claim
- [ ] Operational Contact transfer
- [ ] Public Visit Request V2
- [ ] OTP
- [ ] Not Found
- [ ] Forbidden/invalid-account nếu reachable

## Visitor

- [ ] Public Header khi logged-in
- [ ] Dashboard shell
- [ ] Sidebar
- [ ] Profile
- [ ] Notifications
- [ ] Visit list
- [ ] Create Visit V2
- [ ] Visit detail V2
- [ ] Edit pending
- [ ] Resubmit
- [ ] Campus edit
- [ ] Visitor reception/detail
- [ ] Feedback modal
- [ ] Feedback deep-link
- [ ] My Contact Invitations
- [ ] Request Detail
- [ ] Process Summary nếu reachable
- [ ] Contribution nếu reachable
- [ ] Participant Invitation nếu reachable
- [ ] Visitor-visible sections trong VisitProcess
- [ ] Cancel/reject/history/reason modals reachable by Visitor
- [ ] responsive mobile states
- [ ] all success/error/loading/empty states

---

# 24. Coverage metrics để chốt

```text
Guest Route Coverage
= audited reachable Guest routes / total reachable Guest routes * 100

Visitor Route Coverage
= audited reachable Visitor routes / total reachable Visitor routes * 100

Page Translation Pass Rate
= pages PASS / audited pages * 100

VI/EN Key Coverage
= valid required bilingual keys / total required keys * 100
```

Chỉ kết luận 100% nếu cả route coverage và translation coverage đều 100%.

---

# 25. Final release gate

Không merge/release nếu còn bất kỳ điều kiện nào:

```text
missing translation key
empty VI/EN value
hard-coded user-facing VI/EN
raw enum
mixed language
fixed vi-VN rendering in English
raw backend message leak
untranslated toast
untranslated validation
untranslated modal
untranslated accessibility label
language switch stale UI
Guest route chưa audit
Visitor route chưa audit
conditional Visitor route chưa xác minh
```

---

# 26. Kết quả mong đợi sau implementation

```text
Repository: quangthoai04/PEMS
Branch: <final branch>
Commit SHA: <final SHA>

GUEST
Reachable routes: X
Audited routes: X
Route coverage: 100%
PASS pages: X/X
Translation pass rate: 100%

VISITOR
Reachable routes: Y
Audited routes: Y
Conditional routes verified: Z
Route coverage: 100%
PASS pages: Y/Y
Translation pass rate: 100%

Locale parity:
VI -> EN missing: 0
EN -> VI missing: 0
Empty values: 0
Broken literal t() keys: 0

Hard-coded user-facing strings in Guest/VISITOR scope: 0
Raw enum/status leaks: 0
Backend/API message leaks: 0
Mixed-language issues: 0
Language-switch issues: 0

GUEST: PASS 100%
VISITOR: PASS 100%
OVERALL: PASS 100%
Blocking issues before 100%: 0
```

---

# 27. Lưu ý quan trọng khi triển khai

- Không sửa lan sang toàn bộ role Staff/HO/Department nếu text đó không thể xuất hiện với Visitor.
- Shared component mà Visitor dùng thì phải dịch đầy đủ cho nhánh Visitor.
- Không chỉ grep page file; phải đi xuống child component.
- Không chỉ kiểm tra happy path; phải kiểm tra error/loading/empty/success/validation.
- Không chỉ kiểm tra desktop; phải kiểm tra mobile.
- Không chỉ kiểm tra text; phải kiểm tra date/status/role/enum/API dynamic content.
- Không coi locale JSON parity là chứng minh UI đã bilingual 100%.
- Không kết luận PASS 100% chỉ vì đã thêm `useTranslation()`.
- Sau mỗi batch phải chạy lại automated i18n gate để tránh regression.

---

# 28. Thứ tự triển khai khuyến nghị ngắn gọn

```text
1. Shared locale/date/error helpers
2. Confirm Email
3. Operational Contact Invitation
4. DashboardLayout + Sidebar + Header
5. Profile
6. Visitor Visit Detail
7. Feedback
8. Notifications
9. Visitor conditional detail/process/contribution screens
10. My Contact Invitations
11. VisitRequestManagement residual
12. Public residuals
13. V2 residual scan
14. Full Guest + Visitor route audit
15. Automated gate
16. Final PASS 100% report
```
