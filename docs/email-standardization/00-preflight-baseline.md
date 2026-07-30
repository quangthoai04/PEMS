# 00 — Preflight và baseline an toàn (Giai đoạn 0)

> Nguồn yêu cầu: `docs/Ver2Carnh/canh/email/PEMS_EMAIL_TEMPLATE_CC_BCC_IMPLEMENTATION_PLAN.md` Mục 9.
> Ngày chạy: 2026-07-26. Người chạy: @Tcanh12 (`nvtcanhwork@gmail.com`).
> Trạng thái: **G0 ĐẠT** (có 1 rủi ro đã kiểm soát — xem Mục 5).

---

## 1. Repository và nhánh

| Mục | Giá trị đo được | Ghi chú |
|---|---|---|
| Remote `origin` | `https://github.com/quangthoai04/PEMS.git` | Khớp repository mục tiêu của kế hoạch |
| Nhánh hiện tại | `Canh-Iter1` | **Khác** nhánh `Dev` mà kế hoạch dự kiến |
| Nhánh mặc định remote | `origin/HEAD → origin/Dev` | |
| Remote của nhánh hiện tại | **không tồn tại** (`origin/Canh-Iter1` chưa được push) | Không tính được ahead/behind |
| HEAD SHA | `06c73b9491b7fb5afb88d20fc64de5ed9a56500c` | |
| HEAD commit | `docs(visit): record the list terminology, next-task and action matrix` (2026-07-26 17:52:10 +0700) | |

**Quyết định nhánh (decision log):** kế hoạch ghi "Nhánh mục tiêu dự kiến: `Dev` — phải xác minh lại tại thời điểm triển khai". Xác minh cho thấy công việc đang diễn ra trên `Canh-Iter1`. Owner đã chọn **tiếp tục làm trên `Canh-Iter1`**, không đổi nhánh, không commit/push (đúng Mục 6.2 và Mục 28 của kế hoạch).

## 2. Trạng thái worktree (bảo toàn)

`git status --short` tại thời điểm bắt đầu:

```
?? docs/Ver2Carnh/canh/email/
```

- Không có file modified/staged.
- Chỉ 1 mục untracked: chính thư mục chứa file kế hoạch.
- **Đã KHÔNG chạy**: `reset`, `rebase`, `clean`, `checkout` đè, `stash drop`. Chỉ chạy `git fetch origin --quiet` (không đổi worktree).

## 3. File SQL canonical

| Mục | Giá trị |
|---|---|
| Đường dẫn | `docs/database/scripts/PEMS_FULL_V2_NO_SEED_DATA_GALLERY.sql` |
| Kích thước | 1 693 226 bytes |
| Commit gần nhất chạm file | `37e3e7ff140dd8680acdcd0b8ed722b8e124847b` (2026-07-26 17:50:51 +0700) |

**Xác nhận đây là bản canonical thật, không phải bản đính kèm.** Bản SQL nêu trong kế hoạch (`PEMS_FULL_V2_NO_SEED_DATA_GALLERY(2).sql`) chỉ khác ở hậu tố `(2)` do trình duyệt đặt khi tải; nội dung tương ứng file trong repository. Đây cũng là file **duy nhất** khớp mẫu `PEMS_FULL_*.sql` ở thư mục gốc `docs/database/scripts/`.

**Ràng buộc quan trọng — hash pin:**
`tests/PEMS.IntegrationTests/TestInfrastructure/CanonicalSqlScript.cs` ghim:

- `FileName = "PEMS_FULL_V2_NO_SEED_DATA_GALLERY.sql"` (không cho phép đổi tên ngầm),
- `ExpectedSha256 = 5ba7daac9667e1b06eee4e6c28c02b120472b4ad37e90732328966f87c8b24ce`,
- và **fail-closed** nếu có hơn một file `PEMS_FULL_*.sql`.

→ Mọi thay đổi seed ở Giai đoạn 7 **bắt buộc** phải bump `ExpectedSha256` trong cùng thay đổi, nếu không toàn bộ integration suite sẽ đỏ.

## 4. Cấu hình email và môi trường kiểm thử

| Cấu hình | `appsettings.json` (Development mặc định) | `appsettings.Testing.json` |
|---|---|---|
| `Smtp.Enabled` | **`true`** | `false` |
| `Smtp.Host` | `smtp.gmail.com:587` | (không đặt) |
| `Smtp.User` | `managementsystemvolunteer@gmail.com` (credential thật) | (không đặt) |
| Connection string | `pems_db` | `pems_pr3_test` |

**Cơ chế an toàn đã có sẵn trong codebase (không phải do kế hoạch này thêm):**

- `EmailService.SendCoreAsync` — khi `Smtp.Enabled=false` hoặc thiếu `Host`: **non-production trả `Skipped`** (không bao giờ báo "sent"), **Production trả `Failed`** (fail-closed). Không log body/OTP/token; địa chỉ bị rút gọn thành `***@domain`.
- `FileSinkEmailService` — sink JSON cho E2E, **double-gated**: chỉ đăng ký khi `ASPNETCORE_ENVIRONMENT=Testing` **và** `PEMS_E2E_TEST_SINK_ENABLED=true` **và** có `PEMS_E2E_TEST_SINK_PATH`. Constructor ném exception nếu thiếu path (fail-closed).
- `CanonicalSqlScript.Retarget` + `AssertSafeToImport` — mỗi lần chạy integration tạo DB dùng-một-lần tên `pems_test_run_<32 hex>`, rewrite **mọi** câu `CREATE/DROP DATABASE`/`USE`, rồi **quét lại** văn bản đã rewrite; từ chối import nếu còn tham chiếu tới `pems_db`.

**Kết luận an toàn:** mọi thao tác của kế hoạch này chạy ở `Testing`. Không dùng `pems_db`. Không gửi email thật.

## 5. Rủi ro đã ghi nhận

| # | Rủi ro | Mức | Biện pháp |
|---|---|---|---|
| R-0.1 | `appsettings.json` chứa **credential SMTP Gmail thật** (app password) đã commit vào repository | Cao (lộ bí mật) | Nằm ngoài phạm vi kế hoạch này để sửa, nhưng **phải báo owner**: nên rotate app password và chuyển sang user-secrets/biến môi trường. Trong suốt kế hoạch này không chạy backend ở Development. |
| R-0.2 | Chạy nhầm ở Development sẽ gửi mail thật | Cao | Chỉ chạy `dotnet test` (dùng `Testing`) và build. Không `dotnet run`. |
| R-0.3 | Sửa canonical SQL làm đỏ toàn bộ integration suite do hash pin | Trung bình | Bump `ExpectedSha256` cùng lúc, có ghi lý do theo đúng nếp comment sẵn có trong file. |

## 6. Baseline đo được TRƯỚC khi sửa

Tất cả build ghi ra `BaseOutputPath` tạm (`.tmp-build/…`) để tránh khoá file của dev-server đang chạy.

| Hạng mục | Lệnh | Kết quả | Thời lượng |
|---|---|---|---|
| Backend build | `dotnet build backend/PEMS.Api/PEMS.Api.csproj -c Debug` | **0 error**, 158 warning | 23,8 s |
| Unit tests | `dotnet test tests/PEMS.UnitTests` | **1158 pass / 0 fail / 0 skip** | 10 s |
| Architecture tests | `dotnet test tests/PEMS.ArchitectureTests` | **14 pass / 0 fail / 0 skip** | 10 s |
| Integration tests | `dotnet test tests/PEMS.IntegrationTests` | **665 pass / 0 fail / 0 skip** | 2 m 47 s |
| Frontend build | `npm run build` (`frontend/pems-react`) | **thành công** (cảnh báo chunk > 500 kB, đã có từ trước) | 33,6 s |
| Frontend unit tests | `npx vitest run` (`frontend/pems-react`) | **752 pass / 0 fail**, 57 test file | 27,6 s |

**Không có test nào fail từ trước.** Đây là mốc so sánh cho Gate G9: mọi con số trên phải giữ nguyên hoặc tăng, không được giảm.

Ghi chú kỹ thuật:

- `dotnet test` với `-p:BaseOutputPath` **không được** có dấu `\` ở cuối chuỗi (dấu `\"` bị shell hiểu là escape, làm đường dẫn output méo — vô hại nhưng gây khó đọc log).
- `npx vitest run --reporter=basic` **hỏng** ở phiên bản vitest hiện tại (reporter `basic` đã bị gỡ; vitest ném lỗi `loadCustomReporterModule` trước khi chạy test nào). Dùng reporter mặc định. Đây là lỗi công cụ, không phải test đỏ.

## 7. Fresh import SQL

Không chạy import thủ công riêng. Integration suite đã tự làm việc này ở mỗi lần chạy: tạo DB dùng-một-lần, import canonical SQL đã retarget, chạy 665 test, rồi drop. **665 pass ⇒ fresh import canonical SQL hiện tại thành công.**

## 8. Số lượng dữ liệu email trong seed canonical

Đếm trực tiếp trên file canonical SQL:

| Bảng | Khối `INSERT` | Ghi chú |
|---|---|---|
| `email_templates` | 2 khối (dòng 5393 và 5403) | tổng **16 hàng**, `email_template_id` cứng **1..16**, tất cả `status='ACTIVE'` |
| `email_templates` — khối `UPDATE` theo numeric ID | **16 khối** (dòng 9752 → 10097) | mỗi khối `WHERE email_template_id = N` |
| `email_templates` — khối patch theo `template_code` | 1 khối (dòng 11459) | `CASE template_code` gồm cả mã **không tồn tại** |
| `sent_emails` | 4 khối (5414, 8477, 9571, 9688) | có tham chiếu `email_template_id` bằng số |
| `sent_email_recipients` | 4 khối (5422, 8482, 9581, 9698) | |
| `email_drafts` | **0 khối** | không có seed draft |

Chi tiết phân loại từng template ở `01-email-caller-template-audit.md`.

## 9. Gate G0 — kết luận

| Điều kiện G0 | Trạng thái |
|---|---|
| Không có nguy cơ ghi vào database production/dev thật | ✅ chỉ dùng disposable DB, có kiểm chứng bằng `AssertSafeToImport` |
| Không có nguy cơ gửi email thật | ✅ `Testing` có `Smtp.Enabled=false`; `EmailService` trả `Skipped`, không gửi |
| Worktree hiện hữu được bảo toàn | ✅ không chạy lệnh phá huỷ; chỉ thêm file mới dưới `docs/email-standardization/` |
| Baseline được ghi lại | ✅ Mục 6 |
| File SQL canonical đã được xác định | ✅ Mục 3, kèm ràng buộc hash pin |

**G0 ĐẠT.** Được phép sang Giai đoạn 1.
