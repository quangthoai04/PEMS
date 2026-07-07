# Tạo database test (`pems_test`) cho Integration Test

> Áp dụng cho Integration Test của UC-63 Create FAQ (và các UC khác dùng chung hạ tầng
> `PemsWebApplicationFactory`). Không dùng Docker/Testcontainers. Không dùng database dev/thật
> (`pems_db`).

## 1. Vì sao cần database riêng

`SessionValidationMiddleware` và các handler thật (CreateFAQCommandHandler, v.v.) đọc/ghi dữ liệu
qua EF Core thật (`ApplicationDbContext`), không mock được ở tầng API. Integration Test phải chạy
với một database MySQL thật, riêng biệt với database dev (`pems_db`), để:

- Không làm hỏng dữ liệu dev khi test tạo/xoá dữ liệu.
- Cho phép test kiểm tra dữ liệu thật sự được lưu/không lưu sau mỗi request.

## 2. Tạo database rỗng

```sql
CREATE DATABASE IF NOT EXISTS pems_test
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;
```

## 3. Import schema — CẢNH BÁO quan trọng

File SQL fresh-create gốc trong `docs/database/scripts/*.sql` chứa các lệnh:

```sql
DROP DATABASE IF EXISTS pems_db;
CREATE DATABASE pems_db;
USE pems_db;
```

**Không import trực tiếp file gốc** bằng `mysql pems_test < script_goc.sql` — MySQL sẽ đọc câu lệnh
`USE pems_db` bên trong file và chuyển sang thao tác trên `pems_db` (database dev thật), bất kể tên
database bạn chỉ định trên command line. Đây chính xác là sự cố đã từng xảy ra với dự án này: `pems_db`
bị `DROP` và tạo lại ngoài ý muốn.

**Quy trình an toàn bắt buộc:**

1. Copy file SQL gốc sang một bản tạm (không sửa file gốc), ví dụ:
   `docs/testing/tmp/fresh_create_for_pems_test.sql`
2. Trong bản copy tạm, thay **mọi** chỗ `pems_db` bằng `pems_test`.
3. Scan lại bản copy để chắc chắn không còn `pems_db` / `DROP DATABASE pems_db` /
   `CREATE DATABASE pems_db` / `USE pems_db` nào sót lại:

   ```bash
   grep -n "pems_db" docs/testing/tmp/fresh_create_for_pems_test.sql
   grep -n "DROP DATABASE\|CREATE DATABASE\|USE " docs/testing/tmp/fresh_create_for_pems_test.sql
   ```

4. Chỉ khi kết quả scan sạch (không còn `pems_db`), mới import bản copy vào `pems_test`:

   ```bash
   mysql -uYOUR_TEST_DB_USER -p pems_test < docs/testing/tmp/fresh_create_for_pems_test.sql
   ```

## 4. Tạo user MySQL riêng cho test (khuyến nghị)

Ưu tiên một user chỉ có quyền trên `pems_test`, không dùng `root`:

```sql
CREATE USER IF NOT EXISTS 'pems_test_user'@'localhost' IDENTIFIED BY 'YOUR_TEST_DB_PASSWORD';
GRANT ALL PRIVILEGES ON pems_test.* TO 'pems_test_user'@'localhost';
FLUSH PRIVILEGES;
```

## 5. Cấu hình kết nối cho Integration Test

1. Copy `backend/PEMS.Api/appsettings.Testing.example.json` thành
   `backend/PEMS.Api/appsettings.Testing.json` (file này **không được commit** — chỉ chứa
   placeholder trong bản `.example`, điền giá trị thật cục bộ vào bản không-example).
2. Điền `ConnectionStrings:DefaultConnection` trỏ tới `pems_test` với user/password ở bước 4.

`PemsWebApplicationFactory` (trong `tests/PEMS.IntegrationTests/TestInfrastructure/`) chỉ đọc
`backend/PEMS.Api/appsettings.Testing.json` — không bao giờ đọc `appsettings.Development.json`.

## 6. Chạy Integration Test

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Trước khi chạy, xác nhận lại:

- `ConnectionStrings:DefaultConnection` trong `appsettings.Testing.json` trỏ đúng `pems_test`
  (không phải `pems_db`).
- Đã import schema an toàn theo mục 3.
