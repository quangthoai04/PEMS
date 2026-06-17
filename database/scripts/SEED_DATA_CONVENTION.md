# PEMS — Quy ước Seed Data chuẩn (Seed Data Convention)

> **Đối tượng:** mọi người chạm vào dữ liệu nền (reference data) của `pems_db` — backend dev,
> người seed DB, người review PR.
> **Mục tiêu:** không ai "seed bừa" dẫn tới lệch giữa **database thật** và **code C#**
> (mismatch permission code, role thiếu quyền, hai catalog song song…).
> **Nguyên tắc tối thượng:** `pems_db` (MySQL) là **nguồn chân lý**. Code chỉ là tầng mapping.

---

## 0. Quy tắc vàng (đọc cái này trước)

1. **Schema sửa bằng SQL thủ công trong MySQL trước** — KHÔNG dùng EF migration, KHÔNG
   `EnsureCreated`, KHÔNG auto-create. Code cập nhật mapping sau cho khớp.
2. **Seed data là dữ liệu có chủ đích, không phải dữ liệu tùy hứng.** Mọi seed phải qua **script SQL
   idempotent** được review, không gõ `INSERT` tay tùy tiện vào DB production/shared.
3. **Mã định danh (`permission_code`, `role_code`, status…) là HỢP ĐỒNG giữa DB và code.** Đổi một
   bên thì PHẢI đổi bên kia **trong cùng một thay đổi**, với chuỗi **giống hệt nhau từng ký tự**.
4. **Auto-seed của app mặc định TẮT** (`Seed:Enabled=false`). Chỉ bật có chủ đích, chạy 1 lần, rồi tắt.
5. **Script seed phải idempotent** (chạy lại nhiều lần không lỗi, không nhân đôi) và **không bao giờ
   `DELETE`/`TRUNCATE`** dữ liệu nền.

---

## 1. Vì sao có tài liệu này (sự cố thực tế)

Trong quá trình rà soát hệ thống đã phát hiện **2 hệ quả điển hình của việc seed/định nghĩa không nhất quán**:

- **Lệch mã permission giữa code và DB → 403 oan.** Bộ lọc phân quyền
  [`RequirePermissionAttribute`](../../backend/PEMS.Api/Filters/PermissionAuthorizeAttribute.cs) so khớp
  **chính xác chuỗi** `permission_code` (xem [`PermissionChecker.cs`](../../backend/PEMS.Infrastructure/Identity/PermissionChecker.cs), dòng `p.PermissionCode == permissionCode`). Nhưng:

  | Code C# mong đợi (`PermissionCodes`) | DB `pems_db` thực tế |
  |---|---|
  | `UC-010.LOGIN_SSO` | `UC-10.LOGIN_VIA_SSO` |
  | `UC-095.VIEW_ACCOUNT_LIST` | `UC-95.VIEW_ACCOUNT_LIST` |
  | `UC-099.SEARCH_FILTER_ACCOUNTS` | `UC-99.SEARCH_AND_FILTER_ACCOUNTS` |
  | `UC-118.CREATE_ROLE` | `UC-118.CREATE_NEW_ROLE` |

  → 5/6 endpoint `AccountsController` và 1/5 `RolesController` **luôn trả 403 dù user có quyền**.

- **Ma trận role→permission không chuẩn.** `ADMIN` chỉ có **30** grant (thiếu cả nhóm Account
  Management), trong khi `STAFF` có **87**. Đây là hậu quả của seed grants thủ công không theo chuẩn.

- **Hai catalog song song.** Bộ seed trong code
  ([`PermissionSeed.cs`](../../backend/PEMS.Infrastructure/Persistence/Seed/PermissionSeed.cs),
  [`PermissionMatrixSeed.cs`](../../backend/PEMS.Infrastructure/Persistence/Seed/PermissionMatrixSeed.cs))
  định nghĩa 18 mã **khác** với 135 mã đang có trong DB. Nếu lỡ bật auto-seed, nó sẽ **chèn thêm 18
  permission trùng nghĩa nhưng khác mã**, làm catalog càng loạn.

Tài liệu này đặt ra luật để các tình huống trên **không tái diễn**.

---

## 2. Nguyên tắc nền tảng — Database-first / Manual schema control

- **`pems_db` là nguồn chân lý** cho cả **schema** lẫn **dữ liệu nền**.
- Schema gốc + seed gốc nằm ở **[`database/scripts/pems_full.sql`](./pems_full.sql)**.
- **CẤM** với schema:
  - `dotnet ef migrations add`, `dotnet ef database update`
  - `context.Database.Migrate()`, `EnsureCreated()`, `EnsureDeleted()`
  - `UseInMemoryDatabase()`
- Muốn đổi bảng/cột/index/FK: **viết SQL `ALTER`/`CREATE` thủ công, chạy trong MySQL trước**, rồi mới
  cập nhật entity/configuration trong code cho khớp.
- Code C# chỉ chứa **mapping + business logic + API**, không sở hữu schema.

---

## 3. Phân loại dữ liệu seed (3 tầng) — biết loại nào mới biết luật nào

| Tầng | Bảng | Tính chất | Khi nào seed | Đồng bộ với code? |
|---|---|---|---|---|
| **T1 — System / RBAC** | `roles`, `permissions`, `role_permissions` | **Bắt buộc, canonical.** Hệ thống không chạy đúng nếu thiếu/sai. | Mọi môi trường (dev/staging/prod) | **CÓ** — `permission_code`, `role_code`, `permission_level` phải khớp hằng số code |
| **T2 — Tổ chức** | `campuses`, `departments` | Dữ liệu nền theo môi trường. Tương đối ổn định. | Mọi môi trường (giá trị có thể khác nhau) | Một phần — `campus_code`, status enum |
| **T3 — Demo / Test** | `users`, `user_auth_providers`, session… | **Chỉ DEV.** Tài khoản giả để thử nghiệm. | **Chỉ dev**, sau cờ `Seed:DevAccounts` | Không — nhưng password phải hash BCrypt, không plaintext |

> **Luật tầng:** T1 là thiêng liêng nhất — đây là nơi mọi sự cố "seed bừa" xảy ra. T3 **tuyệt đối không
> được lọt vào production**.

---

## 4. Hợp đồng code ↔ DB (Source-of-truth contract)

Các giá trị chuỗi sau là **hợp đồng**. DB là chuẩn; hằng số code phải **soi gương** đúng từng ký tự.

| Khái niệm | Cột DB (nguồn chân lý) | Hằng số code phải khớp | File |
|---|---|---|---|
| Mã quyền | `permissions.permission_code` | `PermissionCodes.*` | [`PermissionConstants.cs`](../../backend/PEMS.Application/Common/Security/PermissionConstants.cs) |
| Mã vai trò | `roles.role_code` (ENUM) | `RoleCodes.*` | [`AuthConstants.cs`](../../backend/PEMS.Domain/Constants/AuthConstants.cs) |
| Mức quyền | `role_permissions.permission_level` (ENUM `F/E/R/O`) | `PermissionLevels.*` | [`AuthConstants.cs`](../../backend/PEMS.Domain/Constants/AuthConstants.cs) |
| Trạng thái user | `users.status` (ENUM) | `UserStatuses.*` | [`AuthConstants.cs`](../../backend/PEMS.Domain/Constants/AuthConstants.cs) |
| Loại provider | `user_auth_providers.provider_type` (ENUM) | `ProviderTypes.*` | [`AuthConstants.cs`](../../backend/PEMS.Domain/Constants/AuthConstants.cs) |
| Cổng đăng nhập | `user_sessions.login_portal` (ENUM) | `LoginPortals.*` | [`AuthConstants.cs`](../../backend/PEMS.Domain/Constants/AuthConstants.cs) |
| `created_via`, OTP purpose, security event… | các ENUM tương ứng | `CreatedViaValues`, `OtpPurposes`, `SecurityEventTypes`… | [`AuthConstants.cs`](../../backend/PEMS.Domain/Constants/AuthConstants.cs) |

### 🔒 Luật "thay đổi nguyên tử" (Atomic change rule)
Khi thêm/sửa một mã thuộc hợp đồng trên, **một thay đổi (PR/commit) phải đồng thời gồm**:
1. Dòng SQL trong script seed canonical (T1) — mã mới/sửa.
2. Hằng số tương ứng trong code — **chuỗi y hệt**.
3. (Nếu là permission) Cập nhật `role_permissions` cấp quyền cho các role phù hợp.

> Nếu chỉ làm 1 bên → chính là lỗi đã xảy ra (`UC-095` ở code vs `UC-95` ở DB).

---

## 5. Quy ước đặt tên (Naming standards)

### 5.1 `permission_code`
**Định dạng chuẩn:** `UC-<N>.<TÊN_VIẾT_HOA_SNAKE>`
- `<N>` = số use case, **zero-pad tới tối thiểu 2 chữ số**: `1→01`, `9→09`, `10→10`, `99→99`, `100→100`, `135→135`.
  (Đây là quy ước **đang dùng trong `pems_db`** — nguồn chân lý. Ví dụ đúng: `UC-01`, `UC-95`, `UC-135`.)
- `<TÊN>` = `A-Z`, `0-9`, `_`; viết hoa, dùng `_` ngăn từ. Phải mô tả đúng use case.
- **Ví dụ chuẩn:** `UC-10.LOGIN_VIA_SSO`, `UC-95.VIEW_ACCOUNT_LIST`, `UC-118.CREATE_NEW_ROLE`.
- **❌ Sai (không theo chuẩn DB):** `UC-010.LOGIN_SSO` (pad 3 số), `UC-099.SEARCH_FILTER_ACCOUNTS` (vừa pad sai vừa khác tên).

> ⚠️ **Hiện trạng cần chuẩn hóa:** các hằng số trong `PermissionCodes.cs` đang dùng pad-3-số và vài tên
> khác DB. Theo luật database-first, **sửa code cho khớp DB** (xem §10 Remediation), không đổi DB.

### 5.2 `role_code`
- Tập đóng, viết hoa, khớp ENUM trong `roles` và `RoleCodes`: `ADMIN`, `HO`, `STAFF`, `DEPT`, `STUDENT`, `VISITOR`.
- Thêm role mới = đổi **ENUM cột `roles.role_code` (SQL thủ công)** + thêm `RoleCodes.*` + thêm grants.

### 5.3 `permission_level` (cột `role_permissions.permission_level`, ENUM)
| Mã | Ý nghĩa | Rank (so sánh "tối thiểu") |
|---|---|---|
| `F` | Full | 4 |
| `E` | Execute/Edit | 3 |
| `O` | Own (chỉ tài nguyên của chính mình) | 2 |
| `R` | Read | 1 |

So sánh dùng `PermissionLevels.Satisfies(actual, required)` (rank ≥). Quyền auth/profile thường cấp mức `O`.

### 5.4 `permission_group`
- Chuỗi mô tả nhóm, **khớp đúng** giá trị đang có trong DB (ví dụ: `Account Management`,
  `Role & Permission Management`, `Profile Management`, `Authentication`, `Common`, `API Management`,
  `Department Management`, `Delegation Reception Management`…). Không tự bịa nhóm mới khi đã có nhóm phù hợp.

### 5.5 Khóa chính / UUID
- Tất cả ID nghiệp vụ là **`CHAR(36)`** (UUID dạng chuỗi), sinh bằng **`UUID()` của MySQL** trong seed
  (đồng bộ với dữ liệu hiện có). Cột BIGINT auto-increment chỉ dùng cho bảng log (`*_logs`, `security_events`).
- **❌ KHÔNG hardcode UUID cố định** trong script seed (sẽ khác nhau giữa các môi trường và dễ trùng/lệch).
  Luôn resolve quan hệ bằng **khóa tự nhiên** (`role_code`, `permission_code`, `campus_code`).

---

## 6. Quy tắc viết script seed (idempotent SQL)

Mọi script seed T1/T2 **phải**:
1. Bắt đầu bằng `USE pems_db;` và bọc trong `START TRANSACTION; … COMMIT;`.
2. **Idempotent** — chạy lại không lỗi, không nhân đôi. Dùng `INSERT … ON DUPLICATE KEY UPDATE` (theo
   unique key/PK) hoặc `INSERT … SELECT … WHERE NOT EXISTS`.
3. **Resolve FK bằng khóa tự nhiên**, không hardcode UUID. Ví dụ JOIN `roles`/`permissions` theo code.
4. **Không** `DELETE`/`TRUNCATE`/`DROP` dữ liệu nền. Muốn gỡ quyền → câu lệnh có điều kiện rõ ràng, review kỹ.
5. Kết thúc bằng vài câu **`SELECT` kiểm chứng** (đếm số dòng kỳ vọng).
6. Đặt tại `database/scripts/`, tên rõ nghĩa: `seed_<đối_tượng>.sql` (vd `seed_rbac.sql`). Ghi comment đầu file: mục đích, nguồn chân lý, ngày.

### Mẫu chuẩn (template)
```sql
USE pems_db;
START TRANSACTION;

-- 1) Permissions: idempotent theo unique key uq_permissions_code
INSERT INTO permissions (permission_id, permission_code, name, permission_group, is_system, created_at)
VALUES
  (UUID(), 'UC-95.VIEW_ACCOUNT_LIST', 'View Account List', 'Account Management', 1, NOW())
  -- … các dòng khác …
ON DUPLICATE KEY UPDATE
  name = VALUES(name), permission_group = VALUES(permission_group), is_system = VALUES(is_system);
  -- LƯU Ý: không cập nhật permission_id → giữ nguyên ID cũ.

-- 2) Role-permission: resolve role_id/permission_id qua khóa tự nhiên; idempotent theo PK (role_id, permission_id)
INSERT INTO role_permissions (role_id, permission_id, permission_level, granted_at)
SELECT r.role_id, p.permission_id, g.level, NOW()
FROM ( SELECT 'ADMIN' AS role_code, 'UC-95.VIEW_ACCOUNT_LIST' AS permission_code, 'F' AS level
       -- … UNION ALL các grant khác …
     ) AS g
JOIN roles r       ON r.role_code = g.role_code AND r.deleted_at IS NULL
JOIN permissions p ON p.permission_code = g.permission_code
ON DUPLICATE KEY UPDATE permission_level = VALUES(permission_level);

COMMIT;

-- 3) Kiểm chứng
SELECT COUNT(*) AS permissions FROM permissions;
SELECT r.role_code, COUNT(*) AS grants FROM role_permissions rp
  JOIN roles r ON r.role_id = rp.role_id GROUP BY r.role_code ORDER BY r.role_code;
```

---

## 7. Quy trình thêm/sửa seed data (checklist bắt buộc)

### 7.1 Thêm 1 permission mới
- [ ] Chọn mã theo §5.1 (`UC-<N>.<TÊN>`, pad 2 số), **chưa trùng** mã/ý nghĩa có sẵn.
- [ ] Thêm dòng vào script seed canonical (`INSERT … ON DUPLICATE KEY UPDATE`).
- [ ] Thêm hằng số `PermissionCodes.<X> = "UC-<N>.<TÊN>"` — **chuỗi y hệt**.
- [ ] Thêm `role_permissions` cấp quyền cho các role cần (kèm `permission_level`).
- [ ] Chạy script trong MySQL → kiểm `SELECT` đếm.
- [ ] Chạy app, gọi endpoint dùng quyền đó → xác nhận không 403 oan.

### 7.2 Thêm 1 role mới
- [ ] `ALTER` ENUM `roles.role_code` (SQL thủ công) + `INSERT` role.
- [ ] Thêm `RoleCodes.<X>`.
- [ ] Bổ sung `role_permissions` cho role mới.
- [ ] Kiểm tra mọi nơi `switch`/mapping theo role có xử lý role mới.

### 7.3 Cấp/sửa quyền cho role (ma trận)
- [ ] Cập nhật script `role_permissions` (idempotent), **không** sửa tay từng dòng trên DB shared.
- [ ] Re-run script; kiểm đếm grants từng role.

### 7.4 Tài khoản demo (T3 — chỉ dev)
- [ ] Password **luôn hash BCrypt** (work factor khớp `PasswordHasher.WorkFactor = 12`), không plaintext.
- [ ] Chỉ seed khi `Seed:Enabled=true` **và** `Seed:DevAccounts=true`. Không commit cờ bật.
- [ ] Không bao giờ đưa account demo lên staging/prod.

---

## 8. Auto-seed của app (`DatabaseSeeder`) — chính sách

- Vị trí: [`DatabaseSeeder.cs`](../../backend/PEMS.Infrastructure/Persistence/Seed/DatabaseSeeder.cs);
  điều khiển ở [`Program.cs`](../../backend/PEMS.Api/Program.cs).
- **Mặc định TẮT.** Chỉ chạy khi `appsettings → Seed:Enabled = true` (kể cả môi trường Development).
  Dev account chỉ chạy khi `Seed:DevAccounts = true`.
- Quy trình dùng có chủ đích: bật cờ → chạy 1 lần → **tắt lại** → không commit cờ bật.
- ⚠️ **Cảnh báo hiện tại:** catalog trong code seeder (`PermissionSeed` / `PermissionMatrixSeed`) đang
  **lệch** với 135 permission thật trong `pems_db` (mã kiểu `UC-010`, tên cũ). **KHÔNG bật seeder này lên
  `pems_db`** cho tới khi nó được đồng bộ lại với catalog chuẩn (xem §10), nếu không sẽ tạo permission trùng nghĩa khác mã.
- Nguồn seed chính thức hiện nay là **SQL thủ công** trong `database/scripts/`, không phải app seeder.

---

## 9. Điều CẤM (anti-patterns đã/có thể gây lệch)

- ❌ Chạy EF migration / `database update` / `Migrate()` / `EnsureCreated()` trên `pems_db`.
- ❌ Bật auto-seed mặc định; commit `Seed:Enabled=true`.
- ❌ Gõ `INSERT/UPDATE/DELETE` tay tùy hứng vào DB shared mà không qua script review.
- ❌ Thêm permission ở code mà quên DB (hoặc ngược lại) → **hai catalog song song**.
- ❌ Đặt mã sai chuẩn (`UC-010` thay vì `UC-10`; tên khác giữa code và DB).
- ❌ Hardcode UUID trong seed; resolve quan hệ bằng UUID cố định.
- ❌ `DELETE`/`TRUNCATE` `permissions`/`roles`/`role_permissions`.
- ❌ Lưu password demo dạng plaintext; đưa account demo lên prod.

---

## 10. Phát hiện trôi dạt (drift detection) & Việc cần làm để chuẩn hóa hiện trạng

### 10.1 Cách phát hiện lệch code ↔ DB
- Liệt kê toàn bộ mã trong DB:
  ```sql
  SELECT permission_group, permission_code FROM permissions ORDER BY permission_group, permission_code;
  ```
- Đối chiếu với hằng số trong `PermissionConstants.cs`. **Mọi `PermissionCodes.*` phải tồn tại y hệt
  trong cột `permission_code`.** Nếu một mã code không có trong DB → endpoint dùng nó sẽ 403.
- Kiểm role thiếu quyền:
  ```sql
  SELECT r.role_code, COUNT(*) FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id
  GROUP BY r.role_code ORDER BY r.role_code;
  ```

### 10.2 Việc cần làm để đưa hiện trạng về chuẩn (đề xuất — bạn quyết)
1. **Đồng bộ `PermissionCodes` theo DB** (database-first; sửa code, không sửa DB). 18 ánh xạ cần đổi:
   `UC-010.LOGIN_SSO→UC-10.LOGIN_VIA_SSO`, `UC-011→UC-11.LOGIN_VIA_CREDENTIALS`, `UC-012→UC-12.LOGOUT`,
   `UC-013→UC-13.FORGOT_PASSWORD`, `UC-014→UC-14.VIEW_PROFILE`, `UC-015→UC-15.UPDATE_PROFILE`,
   `UC-016→UC-16.CHANGE_PASSWORD`, `UC-095→UC-95.VIEW_ACCOUNT_LIST`, `UC-096→UC-96.CREATE_ACCOUNT`,
   `UC-097→UC-97.MANAGE_ACCOUNT_STATUS`, `UC-098→UC-98.VIEW_ACCOUNT_DETAILS`,
   `UC-099.SEARCH_FILTER_ACCOUNTS→UC-99.SEARCH_AND_FILTER_ACCOUNTS`, `UC-100`✓, `UC-117`✓,
   `UC-118.CREATE_ROLE→UC-118.CREATE_NEW_ROLE`, `UC-119`✓, `UC-120`✓, `UC-121`✓.
2. **Chuẩn hóa ma trận `role_permissions`** bằng 1 script canonical idempotent (đặc biệt `ADMIN` đang
   thiếu Account Management). Cần bạn chốt chính sách: `ADMIN`=full toàn bộ? các role khác giữ nguyên hay định nghĩa lại?
3. **Hòa giải hoặc khai tử catalog trong code seeder** (`PermissionSeed`/`PermissionMatrixSeed`): hoặc
   cập nhật cho khớp 135 mã DB, hoặc đánh dấu rõ "không dùng để seed pems_db".

> Khi bạn chốt §10.2 mục 2 (chính sách ma trận), tôi sẽ sinh script `seed_rbac.sql` chuẩn theo đúng
> template §6, kèm phần đồng bộ `PermissionCodes`.

---

## 11. Phụ lục — Danh mục hiện tại trong `pems_db`

- **Roles (6):** `ADMIN`, `HO`, `STAFF`, `DEPT`, `STUDENT`, `VISITOR`.
- **Permissions:** 135 mã, `UC-01` … `UC-135`, trải trên các nhóm: Common, Authentication,
  Profile Management, Delegation Reception Management, Email Management, Partner Management,
  Document Management, Gallery Management, Minutes Management, FAQ Management, Report Management,
  Calendar Management, Feedback Management, Campus Management, News Management, Account Management,
  Department Management, Role & Permission Management, API Management, Agenda Templates Management.
- **Permission levels:** `F` / `E` / `O` / `R`.
- **Bảng Auth/RBAC liên quan:** `users`, `roles`, `permissions`, `role_permissions`,
  `user_auth_providers`, `user_sessions`, `otp_tokens`, `login_logs`, `security_events`, `audit_logs`,
  `campuses`, `departments`.

---

*Tài liệu này là một phần của quy ước database-first của PEMS. Mọi thay đổi seed data phải tuân thủ.
Khi sửa, cập nhật cả phần §10 cho khớp hiện trạng.*
