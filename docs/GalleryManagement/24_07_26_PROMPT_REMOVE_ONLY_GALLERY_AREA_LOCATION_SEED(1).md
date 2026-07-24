# PROMPT CHO AI AGENT — XÓA SEED `gallery_areas`, `gallery_locations` VÀ USER `215`

## 1. Vai trò

Bạn là **Senior MySQL Database Engineer** phụ trách chỉnh sửa chính xác một file fresh-create SQL của dự án PEMS.

Nhiệm vụ này có phạm vi rất hẹp. Chỉ được thực hiện ba thay đổi nghiệp vụ sau:

1. Xóa toàn bộ seed data của bảng `gallery_areas`.
2. Xóa toàn bộ seed data của bảng `gallery_locations`.
3. Xóa user seed có:

```text
user_id   = 215
full_name = HO Inactive Campus Archive
email     = ho.inactive.archive@fpt.edu.vn
```

Ngoài ra, được phép xóa **duy nhất một dòng phụ thuộc bắt buộc** trong `user_auth_providers` thuộc user `215`, vì nếu giữ dòng này sau khi bỏ user cha thì fresh import sẽ tạo tham chiếu không hợp lệ hoặc thất bại do foreign key/trigger.

Mọi schema, dữ liệu, ID và logic khác phải được giữ nguyên tuyệt đối.

---

## 2. File nguồn phải chỉnh sửa

File SQL mới nhất:

```text
PEMS_FULL_V2_ONLY_CANONICAL_TRANSLATION_GALLERY_FAQ_VISION_GUARD_DIRECT_SEED_NO_STAGING_LATEST (1).sql
```

Không thay thế file bằng bản SQL cũ hơn. Không lấy seed từ file khác để ghi đè. Không tạo lại toàn file từ đầu.

Trước khi sửa, xác nhận đúng file bằng tên và kiểm tra file có các statement sau:

```sql
INSERT INTO users (...)
INSERT INTO user_auth_providers (...)
INSERT INTO gallery_areas (...)
INSERT INTO gallery_locations (...)
```

### Baseline hiện tại cần nhận diện

#### Gallery

- `gallery_areas` đang seed **11 dòng**, `area_id` từ `1` đến `11`.
- `gallery_locations` đang seed **36 dòng**, `location_id` từ `1` đến `36`.
- Không dựa duy nhất vào số dòng; phải nhận diện toàn statement từ `INSERT INTO ... VALUES` đến dấu `;` kết thúc.

#### User cần xóa

Trong `INSERT INTO users (...) VALUES`, có đúng một tuple:

```sql
(215, 'HO Inactive Campus Archive', 'ho.inactive.archive@fpt.edu.vn', ...)
```

Tuple này hiện là **tuple cuối cùng** của statement `INSERT INTO users`, kết thúc bằng dấu `;`.

#### Dòng phụ thuộc bắt buộc

Trong `INSERT INTO user_auth_providers (...) VALUES`, có đúng một tuple liên kết tới user `215`:

```sql
(255, 215, 'GOOGLE_SSO', 'google_sso-subject-215', 'ho.inactive.archive@fpt.edu.vn', TRUE, ...)
```

Ý nghĩa:

```text
auth_provider_id = 255
user_id          = 215
```

Bảng `user_auth_providers.user_id` có foreign key tới `users.user_id` qua `fk_auth_providers_user`.

> `ON DELETE CASCADE` không tự giải quyết trường hợp này, vì task đang xóa tuple khỏi **source seed**, không chạy một lệnh `DELETE` trên row cha đã tồn tại. Nếu vẫn insert auth provider cho user không còn được seed, fresh import có thể lỗi hoặc để lại dữ liệu không hợp lệ tùy trạng thái kiểm tra FK.

---

## 3. Mục tiêu cuối cùng

Sau khi chỉnh sửa và fresh import trên database disposable:

```text
gallery_areas      = 0 dòng
gallery_locations  = 0 dòng
users.user_id 215  = 0 dòng
user_auth_providers.user_id 215 = 0 dòng
```

Đồng thời:

- Hai bảng Gallery vẫn tồn tại đầy đủ.
- Bảng `users` vẫn tồn tại đầy đủ.
- Bảng `user_auth_providers` vẫn tồn tại đầy đủ.
- Mọi user khác vẫn giữ nguyên.
- Mọi auth provider khác vẫn giữ nguyên.
- Không có orphan hoặc foreign-key violation.
- Không có thay đổi ngoài phạm vi được mô tả trong file này.

---

## 4. Phạm vi chỉnh sửa được phép

### 4.1. Xóa toàn bộ statement seed `gallery_areas`

Tìm statement bắt đầu bằng:

```sql
INSERT INTO gallery_areas (area_id, campus_id, area_name, area_key, status, display_order, created_at, created_by, updated_at, updated_by) VALUES
```

Xóa toàn bộ statement này, bao gồm:

- Dòng `INSERT INTO ... VALUES`;
- Toàn bộ 11 tuple từ `area_id = 1` đến `area_id = 11`;
- Dấu `;` kết thúc statement.

Không xóa hoặc sửa `CREATE TABLE gallery_areas`.

### 4.2. Xóa toàn bộ statement seed `gallery_locations`

Tìm statement bắt đầu bằng:

```sql
INSERT INTO gallery_locations (location_id, area_id, location_name, location_key, status, display_order, created_at, created_by, updated_at, updated_by) VALUES
```

Xóa toàn bộ statement này, bao gồm:

- Dòng `INSERT INTO ... VALUES`;
- Toàn bộ 36 tuple từ `location_id = 1` đến `location_id = 36`;
- Dấu `;` kết thúc statement.

Không xóa hoặc sửa `CREATE TABLE gallery_locations`.

### 4.3. Xóa đúng tuple user `215`

Trong statement:

```sql
INSERT INTO users (user_id, full_name, email, ... ) VALUES
```

chỉ xóa tuple thỏa mãn đồng thời cả ba điều kiện:

```text
user_id   = 215
full_name = 'HO Inactive Campus Archive'
email     = 'ho.inactive.archive@fpt.edu.vn'
```

Không xóa user chỉ dựa trên một số `215` xuất hiện trong file.

Vì tuple user `215` hiện là tuple cuối statement, sau khi xóa phải sửa kết thúc tuple user `214` từ:

```sql
(... user_id 214 ...),
```

thành:

```sql
(... user_id 214 ...);
```

Đây là thay đổi cú pháp bắt buộc để statement `INSERT INTO users` vẫn hợp lệ.

Không được:

- Xóa toàn statement `INSERT INTO users`;
- Đổi ID của user khác;
- Dồn lại ID;
- Đổi `AUTO_INCREMENT`;
- Đổi email, role, campus hoặc trạng thái của user khác;
- Thêm user thay thế cho ID `215`.

### 4.4. Xóa đúng auth provider phụ thuộc của user `215`

Trong statement:

```sql
INSERT INTO user_auth_providers (auth_provider_id, user_id, provider_type, provider_subject, provider_email, is_enabled, linked_at, last_used_at) VALUES
```

xóa đúng tuple:

```sql
(255, 215, 'GOOGLE_SSO', 'google_sso-subject-215', 'ho.inactive.archive@fpt.edu.vn', TRUE, ...)
```

Không xóa tuple có:

```sql
(215, 125, 'GOOGLE_SSO', 'google_sso-subject-125', ...)
```

Bởi vì ở tuple trên:

```text
auth_provider_id = 215
user_id          = 125
```

Nó thuộc user `125`, hoàn toàn không phải auth provider của user `215`.

Sau khi xóa tuple `(255, 215, ...)`, giữ nguyên tuple `254` và tuple `256`; chỉ bảo đảm dấu phẩy giữa các tuple còn lại hợp lệ.

### 4.5. Comment được phép sửa tối thiểu

Chỉ được điều chỉnh comment nằm ngay tại block seed Gallery vừa xóa để nội dung không còn khẳng định area/location seed vẫn được giữ.

Có thể thay block seed đã xóa bằng:

```sql
-- gallery_areas seed intentionally omitted.
-- gallery_locations seed intentionally omitted.
```

Nếu có comment gần đó như:

```text
(Areas/locations + their covers above are kept.)
```

chỉ sửa câu này thành:

```text
(Areas/locations are intentionally not seeded; schema remains available for UI-created data.)
```

Không cần thêm comment vào block `users` hoặc `user_auth_providers`, trừ khi comment hiện hữu trực tiếp trở nên sai sau khi xóa user `215`.

---

## 5. Cảnh báo đặc biệt về số `215`

Trong file SQL có nhiều giá trị `215` không liên quan đến `users.user_id = 215`, ví dụ có thể là:

- `auth_provider_id = 215` của user `125`;
- `file_id = 215`;
- `cover_file_id = 215`;
- `document.file_id = 215`;
- `news_section_files.file_id = 215`;
- `view_count = 215`;
- Một ID hoặc số liệu của module khác.

**Tuyệt đối không dùng tìm-thay-thế toàn cục để xóa mọi dòng chứa `215`.**

Chỉ xóa dựa trên đúng bảng, đúng danh sách cột và đúng tuple đã mô tả ở mục 4.3–4.4.

---

## 6. Những phần tuyệt đối không được động vào

### 6.1. Không đổi schema Gallery

Giữ nguyên toàn bộ schema của:

```text
gallery_areas
gallery_locations
gallery_items
gallery_item_media
gallery_item_contents
photo_face_tags
```

Giữ nguyên mọi `CREATE TABLE`, cột dịch, constraint, index và foreign key.

### 6.2. Không đổi schema account/authentication

Giữ nguyên:

```text
CREATE TABLE users
CREATE TABLE user_auth_providers
CREATE TABLE user_sessions
```

Giữ nguyên:

- Primary key;
- Unique key;
- Foreign key;
- Trigger validation;
- Role/sub-role rules;
- Campus/department rules;
- SSO/session logic;
- `AUTO_INCREMENT` và cấu hình bảng.

Không sửa `fk_auth_providers_user` hay chuyển thành `SET NULL` để né dependency.

### 6.3. Không xóa seed của bảng khác

Ngoại trừ bốn mục được phép:

1. Statement seed `gallery_areas`;
2. Statement seed `gallery_locations`;
3. Tuple user `215` trong `users`;
4. Tuple auth provider `(auth_provider_id=255, user_id=215)` trong `user_auth_providers`;

không được xóa hoặc sửa seed của bất kỳ bảng nào khác.

Đặc biệt giữ nguyên:

```text
campuses
roles
departments
mọi user khác
mọi user_auth_provider khác
user_sessions
files
photo_face_tags
partners
faqs
news
notifications
calendar_events
visit_requests
visit_request_campuses
và toàn bộ bảng còn lại
```

Không xóa `files.file_id = 215`. Đây là file News cover và không phải user `215`.

### 6.4. Không xóa các câu lệnh cập nhật metadata Gallery

Giữ nguyên các statement:

```sql
UPDATE gallery_areas ...
UPDATE gallery_locations ...
```

Bao gồm:

- Chuẩn hóa `area_name`;
- Chuẩn hóa `location_name`;
- Tính `translation_source_hash`;
- Cập nhật `translation_status`;
- Cập nhật `translated_at`.

Các statement này có thể update `0 rows` trên fresh seed mới; đó là kết quả hợp lệ.

### 6.5. Không sửa verification schema/i18n

Giữ nguyên các truy vấn kiểm tra như:

```text
gallery_i18n_columns_present
```

Kiểm tra này xác minh schema, không yêu cầu có seed Gallery.

### 6.6. Không thay đổi logic dịch

Giữ nguyên quy tắc:

- Area/Location có dữ liệu nguồn tiếng Việt khi được tạo;
- Bản tiếng Anh được lưu trong DB;
- Translation API chỉ dùng trong create/update/backfill;
- Public read và chuyển ngôn ngữ chỉ đọc dữ liệu đã lưu;
- Không thêm bảng `translation_memory`;
- Không gọi Translation API khi render public page hoặc đổi header language.

### 6.7. Không thay đổi phần khác của file

Không được:

- Đổi tên database;
- Đổi `USE pems_db`;
- Đổi thứ tự tạo bảng;
- Đổi `FOREIGN_KEY_CHECKS`;
- Đổi delimiter;
- Đổi trigger/procedure;
- Đổi ID seed khác;
- Reformat toàn file;
- Chuẩn hóa quote/indent toàn file;
- Thay line ending toàn file;
- Xóa khoảng trắng hàng loạt;
- Thêm bảng, cột, view, function, event hoặc staging table;
- Tạo patch `DELETE FROM ...` ở cuối file để che seed cũ;
- Renumber user/auth provider/file hoặc bất kỳ ID nào.

Phải xóa trực tiếp các tuple/statement nguồn, không seed rồi mới xóa.

---

## 7. Cách thực hiện an toàn

### Bước 1 — Preflight

1. Xác nhận đúng file SQL mới nhất.
2. Ghi lại checksum hoặc `git diff --stat` baseline nếu file nằm trong repository.
3. Tìm các block Gallery:

```bash
rg -n "INSERT INTO gallery_areas|INSERT INTO gallery_locations|CREATE TABLE gallery_areas|CREATE TABLE gallery_locations" "<SQL_FILE>"
```

Kỳ vọng trước sửa:

- Đúng 1 `INSERT INTO gallery_areas`;
- Đúng 1 `INSERT INTO gallery_locations`;
- Đúng 1 `CREATE TABLE gallery_areas`;
- Đúng 1 `CREATE TABLE gallery_locations`.

4. Tìm user mục tiêu và auth provider phụ thuộc:

```bash
rg -n "HO Inactive Campus Archive|ho\.inactive\.archive@fpt\.edu\.vn|google_sso-subject-215" "<SQL_FILE>"
```

Kỳ vọng baseline:

- `HO Inactive Campus Archive`: 1 occurrence trong tuple `users`;
- `ho.inactive.archive@fpt.edu.vn`: 2 occurrence, gồm `users` và `user_auth_providers`;
- `google_sso-subject-215`: 1 occurrence trong `user_auth_providers`.

5. Xác nhận đúng dependency bằng cột, không bằng vị trí số:

```bash
rg -n "INSERT INTO users|INSERT INTO user_auth_providers|fk_auth_providers_user" "<SQL_FILE>"
```

6. Tìm mọi tham chiếu trực tiếp tới user `215` trong các cột FK/user relation. Không coi `file_id=215`, `view_count=215` hoặc các ID khác là user reference.

Baseline hiện tại đã rà soát thấy dependency seed trực tiếp duy nhất ngoài row `users` là:

```text
user_auth_providers.auth_provider_id = 255
user_auth_providers.user_id          = 215
```

Không có `user_sessions` seed dùng `user_id = 215` hoặc `auth_provider_id = 255` trong baseline hiện tại. Tuy nhiên Agent vẫn phải xác nhận lại trên file thực tế trước khi sửa.

Nếu phát hiện thêm một foreign-key reference thật tới `users.user_id = 215`, **dừng và báo cáo**, không tự ý xóa thêm ngoài phạm vi.

### Bước 2 — Chỉnh sửa tối thiểu

1. Xóa statement seed `gallery_areas`.
2. Xóa statement seed `gallery_locations`.
3. Xóa tuple user `215`.
4. Đổi dấu kết thúc tuple user `214` từ `,` thành `;`.
5. Xóa tuple auth provider `(255, 215, ...)`.
6. Chỉ sửa comment Gallery trực tiếp nếu cần.
7. Không chạy formatter toàn file.

### Bước 3 — Static verification

Xác nhận không còn Gallery seed:

```bash
rg -n "INSERT INTO gallery_areas|INSERT INTO gallery_locations" "<SQL_FILE>"
```

Kỳ vọng: **0 kết quả**.

Xác nhận schema Gallery vẫn còn:

```bash
rg -n "CREATE TABLE gallery_areas|CREATE TABLE gallery_locations" "<SQL_FILE>"
```

Kỳ vọng: mỗi bảng đúng 1 kết quả.

Xác nhận metadata update vẫn còn:

```bash
rg -n "UPDATE gallery_areas|UPDATE gallery_locations" "<SQL_FILE>"
```

Kỳ vọng: các statement hiện hữu vẫn nguyên vẹn.

Xác nhận user mục tiêu và provider của user mục tiêu đã biến mất:

```bash
rg -n "HO Inactive Campus Archive|ho\.inactive\.archive@fpt\.edu\.vn|google_sso-subject-215" "<SQL_FILE>"
```

Kỳ vọng: **0 kết quả**.

Xác nhận auth provider ID `215` của user `125` vẫn còn:

```bash
rg -n "\(215, 125, 'GOOGLE_SSO', 'google_sso-subject-125'" "<SQL_FILE>"
```

Kỳ vọng: **1 kết quả**.

Xác nhận file ID `215` và các dữ liệu module khác không bị xóa chỉ vì cùng số:

```bash
rg -n "\(215, 'GOOGLE_DRIVE'.*news-innovation-cover" "<SQL_FILE>"
```

Kỳ vọng: **1 kết quả**.

### Bước 4 — Diff review

Chạy:

```bash
git diff -- "<SQL_FILE>"
```

Diff hợp lệ chỉ được chứa:

- Xóa statement seed `gallery_areas` gồm 11 tuple;
- Xóa statement seed `gallery_locations` gồm 36 tuple;
- Xóa tuple user `215`;
- Đổi terminator của tuple user `214` từ `,` thành `;`;
- Xóa tuple auth provider `(255, 215, ...)`;
- Tối đa một vài dòng comment Gallery trực tiếp để phản ánh trạng thái không seed.

Nếu diff có thay đổi ở seed hoặc schema khác, phải hoàn tác phần ngoài phạm vi trước khi tiếp tục.

---

## 8. Runtime verification trên database dùng một lần

Không được chạy file chỉnh sửa trên các database được bảo vệ hoặc đang dùng, bao gồm tối thiểu:

```text
pems_db
pems_test
pems_pr3_test
```

Chỉ import vào database disposable/fresh riêng.

Sau khi import thành công, chạy:

```sql
SELECT COUNT(*) AS gallery_area_count
FROM gallery_areas;

SELECT COUNT(*) AS gallery_location_count
FROM gallery_locations;

SELECT COUNT(*) AS removed_user_count
FROM users
WHERE user_id = 215
   OR full_name = 'HO Inactive Campus Archive'
   OR email = 'ho.inactive.archive@fpt.edu.vn';

SELECT COUNT(*) AS removed_user_auth_provider_count
FROM user_auth_providers
WHERE user_id = 215
   OR provider_subject = 'google_sso-subject-215'
   OR provider_email = 'ho.inactive.archive@fpt.edu.vn';
```

Kết quả bắt buộc:

```text
gallery_area_count                = 0
gallery_location_count            = 0
removed_user_count                = 0
removed_user_auth_provider_count  = 0
```

Xác nhận user `214` vẫn còn và statement users không bị cắt sai:

```sql
SELECT COUNT(*) AS user_214_count
FROM users
WHERE user_id = 214;
```

Kỳ vọng:

```text
user_214_count = 1
```

Xác nhận auth provider ID `215` của user `125` vẫn còn:

```sql
SELECT COUNT(*) AS provider_215_for_user_125_count
FROM user_auth_providers
WHERE auth_provider_id = 215
  AND user_id = 125
  AND provider_subject = 'google_sso-subject-125';
```

Kỳ vọng:

```text
provider_215_for_user_125_count = 1
```

Xác nhận không còn orphan authentication relation:

```sql
SELECT COUNT(*) AS orphan_auth_provider_count
FROM user_auth_providers ap
LEFT JOIN users u ON u.user_id = ap.user_id
WHERE u.user_id IS NULL;

SELECT COUNT(*) AS orphan_session_user_count
FROM user_sessions s
LEFT JOIN users u ON u.user_id = s.user_id
WHERE u.user_id IS NULL;

SELECT COUNT(*) AS orphan_session_provider_count
FROM user_sessions s
LEFT JOIN user_auth_providers ap ON ap.auth_provider_id = s.auth_provider_id
WHERE s.auth_provider_id IS NOT NULL
  AND ap.auth_provider_id IS NULL;
```

Kết quả bắt buộc:

```text
orphan_auth_provider_count   = 0
orphan_session_user_count    = 0
orphan_session_provider_count = 0
```

Xác nhận bảng Gallery vẫn tồn tại:

```sql
SHOW CREATE TABLE gallery_areas;
SHOW CREATE TABLE gallery_locations;
```

Xác nhận không có orphan Gallery:

```sql
SELECT COUNT(*) AS orphan_location_count
FROM gallery_locations gl
LEFT JOIN gallery_areas ga ON ga.area_id = gl.area_id
WHERE ga.area_id IS NULL;

SELECT COUNT(*) AS orphan_item_count
FROM gallery_items gi
LEFT JOIN gallery_locations gl ON gl.location_id = gi.location_id
WHERE gl.location_id IS NULL;
```

Kết quả bắt buộc:

```text
orphan_location_count = 0
orphan_item_count      = 0
```

Chạy toàn bộ verification cuối file. Mọi query `issue_count` vẫn phải bằng `0`.

---

## 9. Kiểm tra “còn lại giữ nguyên”

Nếu có thể tạo hai disposable database:

1. Import file gốc vào DB A.
2. Import file đã sửa vào DB B.
3. So sánh row count của toàn bộ bảng.

Chênh lệch duy nhất được phép:

```text
gallery_areas:        DB B ít hơn DB A đúng 11 dòng
gallery_locations:    DB B ít hơn DB A đúng 36 dòng
users:                DB B ít hơn DB A đúng 1 dòng
user_auth_providers:  DB B ít hơn DB A đúng 1 dòng
```

Mọi bảng còn lại phải có row count giống nhau.

Ngoài row count, kiểm tra các khóa mục tiêu:

```sql
-- DB B phải không có:
users.user_id = 215
user_auth_providers.auth_provider_id = 255 AND user_id = 215

-- DB B vẫn phải có:
users.user_id = 214
user_auth_providers.auth_provider_id = 215 AND user_id = 125
files.file_id = 215
```

Không coi việc `UPDATE gallery_areas` hoặc `UPDATE gallery_locations` ảnh hưởng `0 rows` là lỗi.

---

## 10. Điều kiện hoàn thành

Task chỉ được coi là hoàn thành khi đáp ứng đủ:

- [ ] Không còn `INSERT INTO gallery_areas`.
- [ ] Không còn `INSERT INTO gallery_locations`.
- [ ] `CREATE TABLE gallery_areas` vẫn nguyên vẹn.
- [ ] `CREATE TABLE gallery_locations` vẫn nguyên vẹn.
- [ ] Các `UPDATE gallery_areas` và `UPDATE gallery_locations` vẫn còn.
- [ ] Không còn user `215` / `HO Inactive Campus Archive` / `ho.inactive.archive@fpt.edu.vn`.
- [ ] Không còn auth provider `(auth_provider_id=255, user_id=215)`.
- [ ] Tuple user `214` kết thúc statement `users` đúng bằng `;`.
- [ ] Auth provider `(auth_provider_id=215, user_id=125)` vẫn còn.
- [ ] `files.file_id=215` và các dữ liệu module khác mang số `215` vẫn còn.
- [ ] Không xóa user hoặc auth provider nào khác.
- [ ] Không có orphan auth provider/session.
- [ ] Không có orphan Gallery.
- [ ] Fresh import trên disposable DB thành công.
- [ ] `gallery_areas = 0`.
- [ ] `gallery_locations = 0`.
- [ ] User mục tiêu count = `0`.
- [ ] Auth provider của user mục tiêu count = `0`.
- [ ] Mọi `issue_count = 0`.
- [ ] Diff không có thay đổi ngoài phạm vi.
- [ ] Không commit, push, merge hoặc deploy nếu người dùng chưa yêu cầu rõ.

---

## 11. Báo cáo cuối cùng bắt buộc

Báo cáo ngắn nhưng có bằng chứng theo mẫu:

```text
KẾT QUẢ: PASS / FAIL / NOT VERIFIED

File đã sửa:
- <đường dẫn file>

Đã xóa:
- gallery_areas seed: 11 dòng, area_id 1..11
- gallery_locations seed: 36 dòng, location_id 1..36
- users seed: user_id 215, HO Inactive Campus Archive
- user_auth_providers seed: auth_provider_id 255, user_id 215

Đã giữ nguyên:
- Schema gallery_areas/gallery_locations
- Schema users/user_auth_providers/user_sessions
- User 214 và toàn bộ user khác
- Auth provider ID 215 của user 125 và toàn bộ provider khác
- File ID 215 và toàn bộ dữ liệu module khác có số 215
- Toàn bộ seed và logic ngoài phạm vi
- Các update metadata dịch
- Verification cuối file

Static checks:
- INSERT gallery_areas còn lại: 0
- INSERT gallery_locations còn lại: 0
- CREATE TABLE gallery_areas: còn
- CREATE TABLE gallery_locations: còn
- User 215/name/email occurrence: 0
- Auth provider subject google_sso-subject-215: 0
- Auth provider ID 215 của user 125: còn
- File ID 215: còn

Disposable DB verification:
- Import: PASS/FAIL/NOT RUN
- gallery_areas count: <n>
- gallery_locations count: <n>
- removed_user_count: <n>
- removed_user_auth_provider_count: <n>
- user_214_count: <n>
- provider_215_for_user_125_count: <n>
- orphan_auth_provider_count: <n>
- orphan_session_user_count: <n>
- orphan_session_provider_count: <n>
- orphan_location_count: <n>
- orphan_item_count: <n>
- verification issue_count: PASS/FAIL/NOT RUN

Diff scope:
- Chỉ các seed mục tiêu + terminator user 214 + comment Gallery trực tiếp: YES/NO

Git actions:
- Commit: không thực hiện
- Push: không thực hiện
- Merge/deploy: không thực hiện
```

Không được báo `PASS` nếu chưa có bằng chứng fresh import thành công trên disposable MySQL và toàn bộ count bắt buộc đều đúng. Nếu chỉ kiểm tra tĩnh, kết luận phải là `NOT VERIFIED`.
