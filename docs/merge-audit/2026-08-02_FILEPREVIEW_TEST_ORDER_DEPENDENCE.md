---
type: merge-audit
feature: dev-into-canh-iter1
status: open
updated: 2026-08-02
links:
  - docs/merge-audit/2026-07-30_DEV_GUARD_LOSS_SCAN.md
---

# FilePreviewDownloadTests — phụ thuộc thứ tự chạy trong Integration suite

> Nợ kỹ thuật **có sẵn**, không phát sinh từ hai commit kết thúc tại `18739bb0`. Ghi lại để không ai
> "sửa" nó bằng cách nới assertion hoặc tăng timeout.

## 0. Vì sao ghi riêng

Cùng một commit `18739bb0`, GitHub Actions chạy **hai** workflow run (một cho `push`, một cho
`pull_request`) trên **cùng một cây mã**:

| Run | Sự kiện | `integration tests` |
|---|---|---|
| `30752604529` | pull_request | ✅ 1364/1364 |
| `30752602490` | push | ❌ **9 đỏ**, 1355 xanh |

Không có một dòng mã nào khác nhau giữa hai lần chạy. Đây là bằng chứng sạch nhất có thể có rằng lỗi
nằm ở bộ test, không ở sản phẩm.

Một suite lúc xanh lúc đỏ mà không đổi dòng code nào là loại lỗi dễ bị xử lý sai nhất — thường bị đóng
lại bằng một `retry`, một timeout dài hơn, hoặc một assertion lỏng hơn. Cả ba đều giấu lỗi chứ không sửa
lỗi.

## 1. Phạm vi trách nhiệm

**Không do hai commit `73a1d493` + `18739bb0` gây ra.**

| Bằng chứng | Nội dung |
|---|---|
| Diff của 2 commit | 20 file, không có `FilePreviewDownloadTests.cs`, `FileDownloadRouteTests.cs`, `GetFileContentQueryHandler`, `FileAccessAuthorizationService`, `LocalFileStorageService` |
| Cùng commit, run khác | PR run xanh 1364/1364, push run đỏ 9 |
| Full Integration cục bộ | 1364/1364 trên đúng cây mã của 2 commit |

## 2. Đây không phải race song song

`tests/PEMS.IntegrationTests/AssemblyInfo.cs:10`

```csharp
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

Toàn assembly chạy tuần tự — không có hai test nào chạy đồng thời, nên không có tranh chấp thời điểm.
Biến duy nhất còn lại giữa lần xanh và lần đỏ là **thứ tự các suite chạy trước** và **trạng thái database
chúng để lại**. Mọi cách sửa dựa trên thời gian chờ đều không chạm tới nguyên nhân.

Thêm một điểm loại trừ: cả 9 test đỏ trong **13 ms trở xuống**. Không có gì bị hết giờ.

## 3. Nguyên nhân trực tiếp — đã chứng minh

Cả 9 test **chết ở bước dựng dữ liệu, chưa gọi một dòng mã sản phẩm nào**. Stack trace giống hệt nhau:

```
MySqlConnector.MySqlException : Cannot delete or update a parent row: a foreign key constraint fails
(`pems_test_run_757498f7…`.`documents`,
 CONSTRAINT `fk_documents_file` FOREIGN KEY (`file_id`) REFERENCES `files` (`file_id`)
 ON DELETE RESTRICT ON UPDATE CASCADE)
   at …RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(…)
   at PEMS.IntegrationTests.Emails.FilePreviewDownloadTests.CleanupRowsAsync(…) : line 231
   at PEMS.IntegrationTests.Emails.FilePreviewDownloadTests.SeedWorldAsync(…)   : line 150
   at PEMS.IntegrationTests.Emails.FilePreviewDownloadTests.<mỗi test>(…)
```

Dòng 231 là câu lệnh đầu tiên của bộ dọn:

```csharp
$"DELETE FROM files WHERE uploaded_by BETWEEN {Base} AND {Base + 100}"   // 991_700 … 991_800
```

`fk_documents_file` là `ON DELETE RESTRICT`. Nên **chỉ cần một hàng `documents` còn sót trỏ vào một hàng
`files` trong dải đó là toàn bộ class chết ngay ở setup** — đúng 9/11 test gọi `SeedWorldAsync`.

**Khiếm khuyết thật nằm ở đây:** `CleanupRowsAsync` xóa `files` mà **không xóa trước những bảng tham
chiếu tới `files`**. Với `RESTRICT`, bộ dọn chỉ hoạt động khi database vốn đã sạch — tức là nó không
phải bộ dọn, nó là một phép thử may rủi. Điều này đúng bất kể suite nào để sót hàng.

`documents` chỉ là bảng vấp phải hôm nay. Truy vấn `information_schema` trên schema thật cho **11 cột
`RESTRICT`** cùng chặn được câu lệnh đó:

```
business_card_ocr_jobs.scanned_card_file_id   gallery_item_media.file_id
documents.file_id                             gallery_locations.cover_file_id
email_draft_attachments.file_id               news_section_files.file_id
gallery_areas.cover_file_id                   sent_email_attachments.file_id
gallery_item_contents.audio_en_file_id        visit_photos.file_id
gallery_item_contents.audio_vi_file_id
```

Sửa riêng `documents` sẽ chỉ dời lỗi sang bảng kế tiếp.

## 4. Bộ dọn chạy trước khi seed, không chạy sau khi xong

`FilePreviewDownloadTests` **không có teardown database**. `Dispose()` (dòng 60–64) chỉ xóa thư mục temp:

```csharp
public void Dispose()
{
    try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); }
    catch (IOException) { /* a temp dir left behind must never fail a test run */ }
}
```

`CleanupRowsAsync` được gọi ở **đầu** `SeedWorldAsync` (dòng 149) — dọn *trước khi seed*, không dọn *sau
khi chạy xong*. Sau test cuối cùng, các hàng `campuses` / `departments` / `users` / `files` trong dải
`991_700…991_800` **nằm lại** trong database cho bất kỳ thứ gì chạy sau.

Ba suite họ file dọn ba kiểu khác nhau — không suite nào dọn `documents`:

| Bảng | FilePreviewDownload (231–236) | FileDownloadRoute (158–166) | FileDownloadAuthorization (328–346) |
|---|---|---|---|
| `files` / `users` / `departments` / `campuses` | ✅ | ✅ | ✅ |
| `sent_emails` / `user_sessions` | ❌ | ✅ | một phần |
| `visit_photos` / `visit_photo_folders` | ❌ | ❌ | ✅ |
| **`documents`** | ❌ | ❌ | ❌ (chỉ xóa lẻ trong thân test, dòng 646) |

## 5. Va chạm dải ID

Hai suite khai báo **cùng một `Base`**:

| Suite | Dòng | `Base` | Số test |
|---|---|---|---|
| `Emails/FilePreviewDownloadTests.cs` | 50 | `991_700` | 11 |
| `Emails/FileDownloadRouteTests.cs` | 35 | `991_700` | 7 |

Trùng ở đúng những khóa chính giống hệt: `CampusId` = 991701, `IcDeptId` = 991702, và hai user 991710 /
991711 (`OwnerA`/`OutsiderB` so với `SenderA`/`RecipientB`). Hai suite chèn hai bộ dữ liệu khác nhau vào
cùng một tập khóa chính.

Comment ở `Emails/EmailSendIdempotencyTests.cs:54` cho thấy quy ước "mỗi suite một dải ID riêng" là có
chủ ý và các suite email khác nằm trong `990_900 … 991_700`. Hai suite trên cùng lấy đúng biên trên của
dải — đây là va chạm do sơ ý.

## 6. Điều CHƯA xác định

**Chưa tìm ra suite nào để sót hàng `documents` đó.** Đã rà toàn bộ nơi ghi vào `documents` trong
`tests/PEMS.IntegrationTests`:

| Nơi ghi | Người upload file | Có trong dải 991_700–991_800? |
|---|---|---|
| `FileDownloadAuthorizationTests:641` (`document_id` cứng = 991_560) | `SenderA` = 991_510 | ❌ |
| `DocumentSearchScopeV2Tests:91` | `LeaderHn` = 3 | ❌ |
| `DocumentVisitOwnerContextV2Tests:146/220/261/285` | `Registrant` = 8 | ❌ |

Không endpoint nào trong test tạo `documents` (`FileDownloadRouteTests` chỉ gọi `GET /api/files/…`).

Tức là **đọc mã tĩnh không giải thích được** vì sao có hàng `documents` trỏ vào file của user 991_7xx.
Người sửa cần một phép đo lúc chạy, không nên đoán tiếp:

```sql
SELECT d.document_id, d.file_id, f.uploaded_by, f.object_key, f.file_purpose
FROM documents d JOIN files f ON f.file_id = d.file_id
WHERE f.uploaded_by BETWEEN 991700 AND 991800;
```

Chèn truy vấn này vào ngay trước dòng 231 (hoặc bắt `MySqlException` rồi in ra) và chạy full suite tới
khi đỏ. Hàng trả về sẽ chỉ đích danh thủ phạm.

Đáng ngờ nhất: `FileDownloadAuthorizationTests:633-647` dọn hàng `documents` **trong thân test, không đặt
trong `try/finally`** — khác với hai suite V2 vốn dùng `finally`. Test đó fail giữa chừng là hàng
`documents` ở lại vĩnh viễn. Đây là **giả thuyết**, chưa phải kết luận: dải ID của nó không khớp.

## 7. Vì sao vá từng bảng không phải lời giải

Bản vá đầu tiên liệt kê tay 11 cột `RESTRICT` của `files` và dọn chúng trước. Nó **đúng phần nó phủ** —
lỗi tại dòng 231 không tái phát nữa — nhưng lần chạy full kế tiếp cho **35 test đỏ**:

| Suite | Số đỏ | Chết ở | FK chặn |
|---|---|---|---|
| `ReportInvoiceRouteTests` | 26 | `CleanupAsync` dòng 273 | `visit_guest_members.visit_request_id → visit_requests` |
| `FilePreviewDownloadTests` | 9 | `CleanupRowsAsync` **dòng 285** | `visit_participants.user_id → users` |

Điểm chết chỉ **dịch xuống một dòng**: từ `DELETE FROM files` sang `DELETE FROM users`. Cùng một phương
thức xóa bốn bảng cha, và mỗi bảng có tập referrer riêng:

| Bảng cha bị xóa | referrer `RESTRICT` |
|---|---|
| `users` | 12 |
| `files` | 11 |
| `campuses` | 6 |
| `departments` | 3 |

Không ai giữ đúng bằng tay bốn danh sách như vậy, và **thêm một FK mới là cả bốn sai lặng lẽ**.

## 8. Đã sửa — teardown suy ra từ schema

`tests/PEMS.IntegrationTests/TestInfrastructure/FixtureCleanup.cs`. Suite khai báo **root** — những hàng
nó sở hữu — rồi helper đọc `information_schema` lúc chạy và xóa từ sâu nhất lên:

```csharp
FixtureCleanup.For(db)
    .Root("files", $"uploaded_by BETWEEN {Base} AND {Base + 100}")
    .Root("users", $"user_id BETWEEN {Base} AND {Base + 100}")
    .Root("departments", $"department_id = {IcDeptId}")
    .Root("campuses", $"campus_id = {CampusId}")
    .RunAsync();
```

Bốn điểm thiết kế đáng ghi lại, vì mỗi điểm là một lỗi đã gặp thật:

1. **Chỉ đi theo chiều tham chiếu vào fixture.** Không chạm bảng cha dùng chung mà fixture chỉ trỏ tới.
2. **Bỏ qua cạnh `SET NULL`.** Database tự làm trống cột đó, và hàng đang trỏ tới là của người khác — một
   hàng `partners` có logo là file của fixture phải **sống sót với logo rỗng**, không được xóa.
3. **Thứ tự root có ý nghĩa.** `files.uploaded_by` trỏ `users` kiểu `SET NULL`, nên xóa `users` trước sẽ
   làm trống đúng cột mà root `files` dùng để nhận diện — hàng file ở lại, vô chủ, không ai thấy.
4. **Id của root được chốt trước khi duyệt.** Root `sent_emails` nhận diện qua `sent_email_recipients`;
   nếu đánh giá lại vị từ ở cuối thì recipient đã bị xóa, vị từ khớp 0 hàng và root sống sót im lặng.

Đi theo **cột mà FK chỉ đích danh**, không đòi khóa chính — vài bảng nối (`visit_instance_guest_members`)
có khóa phức hợp và không có id đơn để bám.

| Yêu cầu | Cách đạt |
|---|---|
| Idempotent | Toàn bộ là `DELETE … WHERE`; chạy lần hai xóa 0 hàng và thành công (có test) |
| Không tắt FK | Không có `SET FOREIGN_KEY_CHECKS`; ràng buộc có hiệu lực suốt quá trình |
| Không nuốt exception | Chu kỳ FK hoặc root thiếu khóa → ném lỗi nêu tên bảng và chuỗi FK, không tự nới rộng |
| Không xóa ngoài phạm vi | Mọi câu lệnh giới hạn theo giá trị khóa của fixture, không bao giờ theo cả bảng |
| Chỉ chạy trên DB dùng-một-lần | Guard theo mẫu `pems_test_run_<32hex>`, kiểm tra **trước** mọi câu lệnh |

Kiểm chứng ở `FixtureCleanupTests.cs`: hai test dựng lại đúng hai hàng đã hạ CI (`documents` trỏ file
fixture, `visit_participants` trỏ user fixture), cộng idempotency, dải rỗng, và bằng chứng hàng của
người khác trong **cùng bảng** vẫn còn nguyên.

**Chi phí:** không đáng kể. `FilePreviewDownloadTests` vẫn **16 s** (trong đó 15 s là import canonical),
`ReportInvoiceRouteTests` **26 s**. So với phương án cấp DB sạch mỗi test — đã đo, ~15 s/lần import × 52
test ≈ **+13 phút** — thì đây gần như bằng 0.

**Còn nợ lại:**

1. **Teardown thật** — bộ dọn vẫn chỉ chạy trước khi seed. Suite vẫn để lại hàng cho thứ chạy sau, dù giờ
   thứ chạy sau không còn chết vì chuyện đó.
2. **Tách dải ID** — `FileDownloadRouteTests` vẫn dùng chung `Base = 991_700` (Mục 5).
3. **Hai suite họ file còn lại chưa chuyển sang helper.** `FileDownloadAuthorizationTests:342` và
   `FileDownloadRouteTests:162` vẫn xóa `files` bằng danh sách tay. Suite thứ nhất đáng ngại hơn: chính
   nó tạo hàng `documents` (dòng 641) và dọn **ngoài** `try/finally`, nên test fail giữa chừng là hàng ở
   lại vĩnh viễn.

## 9. Lỗi thứ hai, không liên quan — hộp thư file-sink

Cùng đợt chạy còn lộ một test đỏ khác, `VisitReminderDispatchIdempotencyTests`. Nó **không cùng loại** và
được ghi ở đây để không ai gộp hai thứ làm một:

```
Attempt 5 of 20 produced 0 messages, expected exactly 1.
```

Trước dòng đó, hai khẳng định về idempotency **đã pass**: `reminder.Status == SENT`, và đúng **1** hàng
`sent_email_recipients`. Database nói đã gửi đúng một lần; đĩa chưa thấy file.

Quyết định nằm ở **số 0**, không phải 2. Chốt chặn idempotency hỏng thì sinh ra **hai** thư, không thể ra
không. Số 0 nghĩa là đọc thư mục trước khi file kịp hiện — `EmailEvidenceHarness.Messages()` chỉ là
`Directory.GetFiles(...)`, không chờ gì cả.

Sửa ở harness, **không đụng idempotency sản phẩm**: `AwaitMessagesAsync` poll tới đúng số thư mong đợi,
có giới hạn thời gian, **và có khoảng ổn định** sau khi đạt số — vì thư trùng mà một bài kiểm tra chống
trùng cần bắt chính là thư đến **sau**. Trả về ngay khi đủ số sẽ khiến "đúng một thư" không thể bị bác
bỏ. Vượt số thì hỏng ngay lập tức. Thông báo lỗi kèm expected/actual, recipient, thời gian đã chờ và
danh sách file quan sát được.

### 9.1. Thủ phạm thật: hosted service chạy trong test host

Waiter mới **không** làm test hết đỏ — nó làm thông báo đủ rõ để tìm ra nguyên nhân. Lần đỏ tiếp theo in:

```
timed out waiting for messages.  expected 1 · actual 0 · elapsed 10006 ms · files: (none)
```

Chờ đủ 10 giây mà hộp thư **rỗng hoàn toàn** thì không thể là độ trễ ghi file. Phân loại "file-sink write
latency" ban đầu là **sai** và đã bị bác bỏ bằng chính bằng chứng này.

Nguyên nhân thật, chứng minh từ mã nguồn:

| Mắt xích | Vị trí |
|---|---|
| `AddHostedService<VisitReminderDispatchHostedService>()` — **không có điều kiện môi trường** | `PEMS.Infrastructure/DependencyInjection.cs:172` |
| `PemsWebApplicationFactory` chỉ `UseEnvironment("Testing")`, **không gỡ hosted service** | factory cũ |
| Poll mỗi **60 s** (không có `Reminders:PollSeconds` trong config Testing), trễ đầu 10 s | `VisitReminderDispatchHostedService` |
| `DispatchDueAsync` quét **toàn database**, claim bất kỳ reminder PENDING đến hạn nào | `VisitReminderDispatchService:73` |

Nên mỗi class test dùng `PemsWebApplicationFactory` đều dựng một bộ đếm giờ nền, và nó **giành mất**
reminder của suite kia. Claim đẩy hàng sang `SENT` **trước khi** gửi, còn host của test có
`Smtp:Enabled = false`, nên không file `.eml` nào được ghi ở đâu cả.

Khớp toàn bộ triệu chứng: `SENT` ✓ · hộp thư của test rỗng ✓ · không lỗi nào được ghi ✓ · chỉ xảy ra
trong full run ✓ · chạy riêng luôn xanh (6/6) ✓.

**Sửa:** gỡ **đúng một** descriptor trong `PemsWebApplicationFactory` — tìm `ServiceType ==
typeof(IHostedService)` và `ImplementationType == typeof(VisitReminderDispatchHostedService)` rồi
`services.Remove(...)`. **Chỉ test host**; đăng ký production giữ nguyên, và mọi job nền khác vẫn chạy
trong test.

Cố ý **không** dùng `RemoveAll<IHostedService>()`: lệnh đó gỡ luôn mọi job nền khác, kể cả job mà một
integration test sau này có thể cần, và sẽ hỏng lặng lẽ. Test nào cần một nhịp reminder thì gọi thẳng
scoped service — đó cũng là cách duy nhất biết mình đang quan sát **nhịp nào**.

Thêm `AssertReadyToDispatchAsync` chạy ngay trước mỗi dispatch: kiểm `current_host_user_id` khác NULL,
đúng host fixture tạo ra, user còn tồn tại, email đúng marker, reminder trỏ đúng instance và đúng trạng
thái đầu vào. Hỏng tiền đề thì đỏ **tại tiền đề**, kèm instance id / reminder id / host id / email —
không phải "0 thư" sau mười giây.

### 9.2. Nợ nghiệp vụ — ngữ nghĩa reminder không có người nhận

> `ResolveRecipientsAsync` trả rỗng dẫn tới `ReminderDispatchOutcome.Nothing` với `SafeError = null`;
> `Succeeded => SafeError is null` nên `Succeeded = true`, khiến reminder giữ `SENT` dù không có
> `sent_emails` lẫn file-sink, và **không ghi lỗi nào**.
>
> Cần quyết định nghiệp vụ riêng: `SENT`, `FAILED`, hay không claim ngay từ đầu.

Phát hiện **ngoài phạm vi** đợt này. Không sửa, và **cố ý không viết test khóa** hành vi "0 người nhận ⇒
SENT" là đúng — chưa ai quyết định điều đó.

**Cấm:**

- ❌ Nới assertion cho vừa kết quả lúc đỏ. Ở đây còn vô nghĩa hơn bình thường: test chết ở setup, chưa
  chạm assertion nào.
- ❌ Chỉ tăng timeout hoặc thêm `retry`. Mục 2 đã loại trừ yếu tố thời gian; 9 test đỏ trong ≤13 ms.
- ❌ Đánh dấu `Skip` để CI xanh. Suite này kiểm tra việc phân biệt bốn loại từ chối khác nhau khi đọc tệp
  đính kèm — bỏ nó là mất đúng phần bảo vệ đó.

## 10. Trạng thái

| Mục | Trạng thái |
|---|---|
| Nguyên nhân trực tiếp | ✅ đã chứng minh (Mục 3) |
| Khiếm khuyết bộ dọn | ✅ đã sửa bằng teardown suy ra từ schema (Mục 8) |
| Lỗi FK của `ReportInvoiceRouteTests` | ✅ đã sửa — cùng helper (Mục 8) |
| Race hộp thư file-sink | ✅ đã sửa ở harness (Mục 9) |
| Nguồn để sót hàng `documents` | ❌ chưa tìm ra (Mục 6) — helper làm nó vô hại, không làm nó biến mất |
| Teardown + tách dải ID + 2 suite họ file còn lại | ❌ chưa (Mục 8) |
| Có chặn `18739bb0` không | **Không** — PR run xanh 1364/1364, 4 job còn lại xanh cả hai run |
