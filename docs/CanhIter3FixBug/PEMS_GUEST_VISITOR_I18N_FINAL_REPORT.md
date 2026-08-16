# Báo cáo cuối — Notification semantic refactor + Guest/Visitor i18n residual sweep

**Branch:** `Canh_iter3_FixBug`
**Baseline (đầu phiên này):** `7f46a7a3` — "test(i18n): add guest visitor localization gates"
**HEAD (cuối phiên này):** `5db58b06` — "test: fix stale and regressed visitor i18n tests"
**5 commit mới, chưa push.** `git log origin/Canh_iter3_FixBug..HEAD` = 8 commit local chưa lên remote (5 của phiên này + 3 của phiên trước).

---

## 1. Kiến trúc Notification — trước/sau

**Trước (phiên trước, hybrid):** backend lưu Title/Message tiếng Việt cố định (nguồn thật) + `messageKey`/`params` cho 7 loại sự kiện đã biết. Frontend: người đọc VI thấy Title/Message thô; người đọc EN được resolve qua `messageKey`. Vấn đề: VI phụ thuộc backend-cố-định (không đi qua i18n), tên `messageKey` gợi ý "khoá của 1 câu" chứ không phải "khoá của 1 sự kiện".

**Sau (phiên này):**

```
backend: eventKey + params (JSON trong metadata_json, cột đã có sẵn, trước đây không dùng)
                    │
                    ▼ (KHÔNG đổi NotificationType — giữ nguyên logic nghiệp vụ khác đang dùng nó)
frontend: resolveNotificationPresentation(input, language, t)
                    │
        eventKey biết  →  t('notifications:events.<KEY>.title/message', params)  — CẢ VI LẪN EN qua i18next
        eventKey lạ/null + language=vi  →  legacyTitle/legacyMessage thô (đúng — vốn dĩ là tiếng Việt)
        eventKey lạ/null + language=en  →  t('notifications:events.unknown.title'), message=null (KHÔNG rò rỉ VI thô)
```

- `backend/PEMS.Application/Notifications/Common/NotificationEventKeys.cs` (mới, thay `NotificationMessageKeys.cs`) — 9 hằng số eventKey + `BuildMetadata()`.
- `frontend/.../resolveNotificationPresentation.ts` (mới, thay `resolveNotificationText.ts`) — điểm render DUY NHẤT cho 4 nơi hiển thị: `NotificationBellButton`, `NotificationsPage`, `NotificationDetailModal`, `VisitorVisitDetailPage` (section `VisitorNotificationsSection`).
- Cùng 1 `notificationId` đổi ngôn ngữ trên UI → không gọi lại API, không tạo dòng mới — vì cả VI lẫn EN đều tính lại từ `metadataJson` có sẵn trong response, không có cache theo ngôn ngữ nào ở backend.

## 2. Inventory đầy đủ 7 sự kiện Visitor-reachable (re-trace phiên này, xác nhận đúng danh sách phiên trước)

| eventKey | Producer | params | Trigger |
|---|---|---|---|
| `CAMPUS_APPROVED` | `CampusApprovalExecutor.cs` | campusName, requestCode, hostName | 1 campus trong đơn được duyệt |
| `CAMPUS_REJECTED` | `RejectCampusInstanceCommandHandler.cs` | campusName, requestCode, reason | 1 campus bị từ chối |
| `FEEDBACK_INVITE_VISITOR` | `CompleteVisitStageCommandHandler.cs` | requestCode | chuyển sang giai đoạn After-visit |
| `VISIT_CLOSED` | `CompleteVisitStageCommandHandler.cs` | requestCode | chuyển sang Closed |
| `VISIT_CANCELLED_BY_HOST` | `CancelVisitRequestCommandHandler.cs` | campusName, requestCode | Host huỷ chuyến |
| `OPCONTACT_TRANSFER_FROM` | `OperationalContactNotifier.cs` | campusLabel, requestCode | đầu mối cũ bị thay |
| `OPCONTACT_TRANSFER_TO` | `OperationalContactNotifier.cs` | campusLabel, requestCode | đầu mối mới được chỉ định |
| `AMENDMENT_APPROVED` | `VisitAmendmentHandlers.cs` | (none) | đề xuất thay đổi được duyệt |
| `AMENDMENT_REJECTED` | `VisitAmendmentHandlers.cs` | (none) | đề xuất thay đổi bị từ chối |

Đối chiếu lại toàn bộ `RecipientUserId:` trong ~40 file producer (grep, không tin lại báo cáo cũ): xác nhận đây là ĐỦ — không có producer thứ 8 nào gán `RecipientUserId` cho user role VISITOR mà chưa được liệt kê.

## 3. Dữ liệu lịch sử (Phase 10-11) — đã backfill thật trên DB local

Kết nối trực tiếp `pems_db` (viết C# console tool tạm dùng `MySql.Data`, không có mysql/python CLI trên máy). Kết quả điều tra:

- **36 dòng notification tổng.** Chỉ **5 dòng có recipient role = VISITOR** (join thật `users`→`roles`, không đoán qua tên `notification_type`).
- Trong 5 dòng đó:
  - **2 dòng FULLY_RECONSTRUCTABLE** (`notification_id` 99019, 99022, type `VISIT_REQUEST_APPROVED`): Title/Message khớp đúng producer hiện tại, `visit_request_id`/`campus_id`/`actor_user_id` đều resolve ra dòng còn tồn tại. → **Đã backfill** qua `docs/database/scripts/patches/pems_patch_backfill_visitor_notification_metadata.sql` (UPDATE thật đã chạy: 2 dòng cập nhật; chạy lại xác nhận 0 dòng — idempotent).
  - **3 dòng NON_RECONSTRUCTABLE** (`notification_id` 7, 99014, 99015): mọi cột FK ngữ nghĩa (visit_request_id/visit_instance_id/campus_id/actor_user_id) đều NULL, Title/Message là text placeholder chung chung của seed/fixture — không còn dữ liệu quan hệ nào để dựng lại params. Không bịa. 3 dòng này sẽ hiện fallback chung khi đọc bằng EN, và vẫn hiện đúng VI gốc khi đọc bằng VI (không mất thông tin, không rò rỉ).

Backfill counts chính xác: **total 36 / Visitor-recipient 5 / reconstructable-và-đã-backfill 2 / non-reconstructable 3 / failed 0**.

## 4. Các lỗ hổng i18n thật khác tìm thấy + đã sửa phiên này (Phase 22)

| File | Vấn đề | Sửa |
|---|---|---|
| `VisitAmendmentPanel.tsx` | Không có `useTranslation` — 100% tiếng Việt cứng; đây là panel duyệt/từ chối/rút đề xuất thay đổi của chính registrant, vào từ `VisitRequestV2DetailView.tsx` (đã trong scope). | Viết lại toàn bộ sang i18n key; đồng thời KHÔNG còn render `result.message` thô từ backend nữa — thay bằng câu cố định đã localize theo từng action. |
| `profileApi.ts` (`validateAvatarFile`) | Trả về string lỗi tiếng Việt thô. | Đổi sang trả `'INVALID_TYPE' \| 'TOO_LARGE'`, localize tại `Profile.tsx`. |
| `HelpTooltip.tsx` | `aria-label` mặc định hardcode `'Trợ giúp'`. | `t('common:help')`. |
| `useVisitEntryCta.tsx` | Toast lỗi/loading của CTA (dùng chung ở trang chủ, FAQ, Partners, dashboard) hardcode tiếng Việt. | `i18n.t('common:visitEntryCta.*')`. |

## 5. Củng cố cổng kiểm tra hardcode-scan (Phase 20)

Vấn đề gốc: `SCOPED_FILES` là danh sách tay ~30 file, không có gì phát hiện khi 1 file Guest/Visitor-reachable mới được thêm mà quên đưa vào danh sách.

**Giải pháp — `scopedFileListFreshness.test.ts` (mới):** tự động suy ra closure import thật từ `App.tsx` (route Guest công khai + route dashboard có `VISITOR` trong `allowedRoles` của `dashboardRouteAccess.ts`), BFS theo các câu `import`/`import()` tương đối, dừng lại ở các file trong `ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS` (không đi tiếp vào cây con của chúng — đây là các file route-guard cho phép VISITOR về mặt kỹ thuật nhưng có bằng chứng code cụ thể là không bao giờ thực sự render cho Visitor). Assert: closure suy ra được **phải là tập con** của `SCOPED_FILES ∪ ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS`.

`SCOPED_FILES` tăng từ ~30 → 185+ file (152 file "sạch" gộp batch + 15 file xét riêng, sửa 4 lỗ hổng thật ở Mục 4). `ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS` tăng từ 4 → 10, mỗi dòng có lý do kèm bằng chứng code cụ thể (ternary trong `App.tsx`, doc comment, hoặc đối chiếu chéo với inventory notification Mục 2).

## 6. Test coverage cho sự kiện notification (Phase 14, mới)

`notificationEventCoverage.test.ts` (mới, 38 assertion): mỗi trong 9 eventKey ở Mục 2 phải có title+message VI và EN không rỗng, phải dùng đúng — không thiếu không thừa — mọi `{{param}}` đã khai báo; fallback `events.unknown.title` phải có ở cả 2 ngôn ngữ; danh sách `KNOWN_EVENT_KEYS` trong `resolveNotificationPresentation.ts` phải khớp chính xác danh sách eventKey của test này (bắt drift nếu ai thêm eventKey ở 1 phía mà quên phía kia).

## 7. Sửa 2 test bị lỗi — root cause thật, không dán nhãn "pre-existing"

- **`VisitContactInvitationPage.test.tsx`, 4 test.** Nguyên nhân gốc: `src/test/setup.ts` cố tình làm jsdom báo `navigator.language = en-US`, nên MỌI test trong bộ này mặc định render tiếng Anh. 4 test này assert chuỗi tiếng Việt (`/Đồng ý làm đầu mối/i`...) trong khi component đã được song ngữ hoá từ phiên trước — test cũ, không đồng bộ. Sửa: đổi các chuỗi query/assert sang tiếng Anh tương ứng.
- **`VisitRequestV2DetailView.test.tsx`, 1 test.** Đây là **regression thật do chính việc viết lại `VisitAmendmentPanel.tsx`** (xác nhận bằng `git stash`/`git stash pop` so với HEAD sạch: test pass trên baseline, fail trước khi sửa). Nguyên nhân: component cũ 100% tiếng Việt "vô tình" pass chỉ vì môi trường test mặc định EN không ảnh hưởng tới nó; sau khi i18n hoá đúng, test cần assert tiếng Anh. Sửa thêm: đổi `getByRole` đồng bộ → `findByRole` bất đồng bộ cho nút cần thêm 1 tick render để tên accessible ổn định sau khi resolve i18n.

## 8. Xác nhận lại hạ tầng test backend (Phase 19)

Báo cáo phiên trước "không có backend test project" là **sai** — do chỉ tìm trong `backend/` và glob `**/*.sln` (bỏ sót `.slnx`). Xác nhận lại: `PEMS.slnx` ở root đăng ký `tests/PEMS.UnitTests`, `tests/PEMS.ArchitectureTests`, `tests/PEMS.IntegrationTests`. `tests/PEMS.ApplicationTests` (139 file) không có `.csproj`, không nằm trong solution — mồ côi, không chạy được (đã ghi vào memory, không ảnh hưởng phiên này).

## 9. Kết quả gate cuối (Phase 27)

| Gate | Kết quả |
|---|---|
| `npm run lint` (tsc --noEmit) | **0 lỗi** |
| `npm run build` (vite) | **Thành công** (cảnh báo chunk-size >500kB là pre-existing, không liên quan) |
| `npx vitest run` (toàn bộ, không filter path) | **153/153 file, 3676/3676 test pass** — lần đầu tiên đạt 100% xanh trong suốt chuỗi phiên làm việc này |
| `dotnet build PEMS.slnx` | **0 Error** (238 warning, toàn bộ pre-existing, không nằm trong file phiên này sửa) |
| `dotnet test tests/PEMS.UnitTests` | **2738/2738 pass** (chạy với `BaseOutputPath` tương đối TRONG repo — dùng path ngoài repo gây 17 fail ảo do các test tự quét source theo repo-root; đã xác nhận lại bằng cách so 2 lần chạy, khớp với hazard đã ghi trong memory) |
| `dotnet test tests/PEMS.ArchitectureTests` | **28/28 pass** |
| `dotnet test tests/PEMS.IntegrationTests` | **Không chạy được trong sandbox này** — cần Docker (Testcontainers.MySql), máy không có Docker. Giới hạn môi trường, không phải lỗi code. |

Một test lẻ (`visitRequestV2ContactEmailRejected.test.tsx`, không liên quan gì tới thay đổi phiên này) timeout khi chạy chung batch lớn do IO contention — chạy lại riêng lẻ pass 12/12 ngay; đã xác nhận đây là hiện tượng đã biết (Vitest dưới tải IO cao), không phải regression.

## 10. Việc CHƯA làm trong phiên này (thành thật, không tự nhận đã xong)

- **Phase 15-16 — Producer-level contract test (backend xUnit) cho từng producer** (đúng `NotificationType`, đúng shape params, đúng recipient, không phá dedupe/action-url): **CHƯA viết**. Có xác minh thủ công qua backfill DB thật (Mục 3) rằng shape `metadata_json` hoạt động đúng end-to-end, nhưng đây không thay thế test tự động lặp lại được.
- **Phase 17 — Re-audit luồng feedback** cho phần dynamic text còn sót: **KHÔNG re-touch phiên này** (đã làm ở phiên trước, không re-verify lại phiên này).
- **Phase 24 — Kiểm chứng runtime/browser thật** (chuyển VI↔EN không reload, mobile+desktop, mọi state): **CHƯA làm** — không có dev server chạy trong phiên này, không có browser-level walkthrough.
- **Phase 25 — Test "notification sống" bằng DB fixture thật** khẳng định 1 `notificationId` chuyển VI→EN→VI không tạo lại từ backend: **CHƯA viết thành automated test.** Đã làm tương đương thủ công (backfill thật + re-verify DB), nhưng không có test lặp lại được trong CI.
- **Phase 26 — Test "notification lịch sử"** (seed dòng cũ metadata NULL, xác nhận VI hiện thô/EN hiện fallback, dòng đã backfill chuyển đúng): **CHƯA viết thành automated test.**

## 11. Kết luận cuối cùng

> **CHƯA ĐỦ CƠ SỞ ĐỂ XÁC NHẬN 100%.**

Lý do: Phase 15-16 (producer contract test), 24 (runtime/browser thật), 25-26 (notification live-test + historical-test dạng automated) chưa hoàn thành trong phiên này — đây là các mục **user yêu cầu tường minh**, không phải suy diễn thêm. Những gì ĐÃ đạt 100% xanh và có bằng chứng cụ thể: kiến trúc semantic notification (Mục 1-2), backfill dữ liệu lịch sử thật trên DB (Mục 3), toàn bộ gate lint/build/frontend-test/backend-unit-test/backend-architecture-test (Mục 9), 2 test bị lỗi đã root-cause và sửa dứt điểm (Mục 7), cổng hardcode-scan đã tự-drift-detect (Mục 5), coverage test cho notification event (Mục 6).

**Không có commit nào được push.** 5 commit mới (`ccc82ad4`..`5db58b06`) cộng 3 commit của phiên trước (`421e3d92`..`7f46a7a3`) đang chờ ở local, tổng 8 commit trước `origin/Canh_iter3_FixBug`.
