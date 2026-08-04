# PEMS — IMPLEMENTATION PROMPT V2

## Email Template Editor · Contact Capability · Dirty State · UI Cleanup

> Dùng tài liệu này để **triển khai/sửa code thật tại HEAD hiện tại**.
>
> Không dùng tài liệu “status update/closure” làm bằng chứng rằng lỗi đã được sửa. Phải kiểm tra trực tiếp UI, API, source code và test hiện tại.
>
> Tài liệu kế hoạch cũ vẫn là nguồn yêu cầu nền, nhưng prompt này khóa lại các lỗi UI/logic còn phải xác nhận và xử lý dứt điểm.

---

# 1. Vai trò

Bạn là Senior Full-stack Engineer của PEMS, phụ trách màn hình:

```text
Quản lý email → Cấu hình mẫu email → Chỉnh sửa nội dung mẫu email
```

Mục tiêu là sửa đúng yêu cầu với **ít thay đổi nhất**, không refactor ngoài scope, không thêm thư viện và không thay đổi schema database.

---

# 2. Các lỗi bắt buộc phải xử lý

## 2.1. Nhãn sai nghĩa

Nhãn hiện tại:

```text
Lấy đầu mối từ
```

không đúng nghĩa vì phần này cấu hình nguồn lấy **thông tin liên hệ**, không phải chọn “đầu mối” theo cách diễn đạt nội bộ.

Đổi thành:

```text
Nguồn thông tin liên hệ
```

Áp dụng nhất quán ở:

- label trên form;
- tooltip/helper text;
- test snapshot/text assertion;
- bản dịch VI/EN nếu label được i18n hóa.

Không đổi tên API field/domain model nếu không cần thiết.

---

## 2.2. Hai nút cấu hình liên hệ không cùng chiều cao

Hai nút:

```text
Lưu cấu hình liên hệ
Phục hồi về cấu hình mặc định của mẫu
```

đang có chiều cao khác nhau do nút thứ hai xuống dòng.

### UI bắt buộc

Dùng nhãn ngắn, rõ:

```text
Lưu cấu hình liên hệ
Phục hồi mặc định
```

Hai nút phải:

- dùng cùng component/variant hoặc cùng height class;
- cùng `h-11` hoặc `h-12` theo design system hiện tại;
- `inline-flex items-center justify-center`;
- `whitespace-nowrap`;
- không xuống hai dòng;
- căn giữa icon và text;
- cùng chiều cao ở desktop/tablet/mobile.

Ví dụ định hướng, chỉ dùng nếu phù hợp codebase hiện tại:

```tsx
className="inline-flex h-11 items-center justify-center whitespace-nowrap rounded-xl px-4 text-sm font-semibold"
```

Không ép chiều rộng bằng giá trị cứng gây tràn màn hình. Có thể dùng grid hai cột hoặc xếp dọc ở breakpoint nhỏ, nhưng chiều cao từng nút vẫn phải bằng nhau.

---

## 2.3. Mở Edit nhưng tự báo “Nội dung mẫu có thay đổi chưa lưu”

Hiện tại chỉ mở màn hình edit, chưa sửa gì, UI đã hiển thị:

```text
● Nội dung mẫu có thay đổi chưa lưu
```

Đây là lỗi dirty-state/hydration.

### Nguyên tắc bắt buộc

Dirty state phải được tính từ:

```text
normalized current form !== normalized saved baseline
```

Không được đơn giản set dirty trong mọi `onChange`, vì rich-text editor có thể phát `onChange` trong lúc khởi tạo.

### Phải xử lý đủ các nguồn false-positive

- API load xong rồi set form state;
- `undefined`, `null` và `''` khác nhau;
- rich-text editor tự chuẩn hóa HTML;
- `<p>text</p>` và HTML tương đương;
- line ending `\r\n` / `\n`;
- trailing whitespace;
- đổi tab Tiếng Việt/English;
- reset state khi chuyển sang template khác;
- preview render/hydrate;
- backend response sau save có format khác request gửi lên.

### Cách triển khai mong muốn

1. Tạo form đã normalize từ response API.
2. Set current form.
3. Chỉ sau khi hydrate hoàn tất mới gán baseline.
4. Dirty state là giá trị derived bằng deep comparison, không phải flag bật vĩnh viễn.
5. Sau save/restore thành công, cập nhật lại cả current form và baseline từ response mới.
6. Khi người dùng sửa rồi hoàn tác đúng giá trị ban đầu, dirty state phải tự biến mất.

Pseudo-code định hướng:

```ts
const baselineRef = useRef<NormalizedTemplateForm | null>(null);
const [isHydrated, setIsHydrated] = useState(false);

useEffect(() => {
  if (!detail) return;

  const normalized = normalizeTemplateForm(detail);
  setForm(normalized);
  baselineRef.current = normalized;
  setIsHydrated(true);
}, [detail?.templateCode, detail?.revision]);

const isContentDirty =
  isHydrated &&
  baselineRef.current !== null &&
  !deepEqual(normalizeTemplateForm(form), baselineRef.current);
```

Không copy nguyên pseudo-code nếu architecture hiện tại đã có utility/equality helper phù hợp hơn.

---

## 2.4. Bỏ thông tin giải thích dư thừa

Bỏ khỏi UI các câu sau:

```text
Mã mẫu ACCOUNT_EMAIL_CONFIRMATION do hệ thống quản lý và không thể thay đổi.
```

```text
Ghi chú nội bộ cho người quản trị: mẫu này gửi khi nào, cho ai.
Không hiển thị cho người nhận email.
```

### UI giữ lại

- Label `Mã mẫu`.
- Giá trị code ở trạng thái readonly/disabled.
- Label `Mô tả quản trị`.
- Textarea nhập mô tả.

Readonly style và trạng thái disabled đã đủ để thể hiện mã mẫu không thể sửa. Không cần thêm đoạn giải thích dài.

`Mô tả quản trị` phải:

- full width;
- dùng textarea, không dùng input một dòng;
- wrap đầy đủ;
- tối thiểu 2 dòng;
- không ellipsis trong màn hình edit;
- tuân theo `maxLength` backend hiện có.

---

## 2.5. Template không hỗ trợ contact vẫn hiện form cấu hình

Đây là lỗi quan trọng nhất.

Không được để người dùng:

1. Chọn `Không hiển thị / Tùy chọn / Bắt buộc`.
2. Chọn nguồn thông tin liên hệ.
3. Chọn các trường email/số điện thoại/phòng ban/cơ sở.
4. Chọn Reply-To.
5. Bấm lưu cấu hình.
6. Sau đó mới nhận lỗi rằng template không hỗ trợ `{{contactInformationBlock}}`.

### Hai khái niệm phải tách riêng

```text
Capability cố định của template
→ template có được phép dùng {{contactInformationBlock}} hay không

Policy hiệu lực
→ NONE / OPTIONAL / REQUIRED đang được cấu hình thế nào
```

Không dùng `contactRequirement = NONE` để suy ra template không hỗ trợ contact.

### Contract cần thể hiện rõ

Tên field có thể theo codebase hiện tại, nhưng phải có ý nghĩa tương đương:

```ts
contactSupported: boolean;
contactRequired: boolean;
contactSettingsEditable: boolean;
contactRequirement: 'NONE' | 'OPTIONAL' | 'REQUIRED';
contactReasonCode?: string;
contactReasonText?: string;
```

Frontend không hard-code danh sách template unsupported.

### Baseline audit cần xác nhận lại từ registry hiện tại

Kết quả audit trước đây ghi nhận 4 template không hỗ trợ contact:

```text
ACCOUNT_EMAIL_CONFIRMATION
AUTH_PASSWORD_RESET_OTP
VISIT_REQUEST_OTP
VISIT_REMINDER_HOST
```

Phải kiểm tra lại registry/send point/renderer tại HEAD. Nếu vẫn đúng, capability backend phải trả `contactSupported=false` cho bốn template này.

---

# 3. UI bắt buộc theo capability

## 3.1. Template `contactSupported=false`

Ví dụ `ACCOUNT_EMAIL_CONFIRMATION`.

Card 4 chỉ hiển thị dạng read-only:

```text
4. Thông tin liên hệ

Mẫu này không sử dụng khối thông tin liên hệ vì email chứa
liên kết xác nhận hoặc thông tin dùng một lần.

Không có cấu hình cần chỉnh sửa.
```

Có thể thay câu lý do theo `contactReasonCode`, nhưng phải ngắn và đúng nghiệp vụ.

### Tuyệt đối không hiển thị

- radio mức hiển thị;
- `Nguồn thông tin liên hệ`;
- checkbox các trường;
- tiêu đề khối VI/EN;
- Reply-To;
- `Lưu cấu hình liên hệ`;
- `Phục hồi mặc định` cho contact;
- trạng thái dirty của contact;
- form contact disabled giả.

Không render một form bị disable toàn bộ. Hãy dùng một card thông báo ngắn để người dùng hiểu rằng template này không có phần cần cấu hình.

## 3.2. Template `contactSupported=true`

Hiển thị form cấu hình bình thường.

Nếu `contactRequired=true`:

- không cho chọn `NONE`;
- body VI/EN phải có `{{contactInformationBlock}}` theo contract;
- UI giải thích ngắn tại vị trí mức hiển thị;
- backend vẫn fail-closed.

Nếu `contactRequired=false`:

- chỉ hiển thị các lựa chọn policy được backend cho phép;
- không tự suy luận từ tên template.

---

# 4. Xử lý `{{contactInformationBlock}}` không hợp lệ

Nếu template `contactSupported=false` nhưng body hiện tại vẫn chứa:

```text
{{contactInformationBlock}}
```

thì:

1. Form contact vẫn phải bị ẩn.
2. Hiện lỗi inline ngay dưới editor/body tương ứng:

```text
Khối thông tin liên hệ không được hỗ trợ ở mẫu này.
Hãy xóa {{contactInformationBlock}} khỏi nội dung.
```

3. Có nút:

```text
Xóa khối không hợp lệ
```

4. Nút chỉ xóa block khỏi body VI/EN đang chứa nó.
5. Không tự lưu ngay; sau khi xóa, content dirty đúng nghĩa và người dùng bấm `Lưu thay đổi mẫu`.
6. Không tự xóa nội dung khác quanh block.

### Dữ liệu mặc định/canonical

Audit shipped defaults, seed/canonical SQL và registry body của các template unsupported.

Nếu default của hệ thống đang chứa block không hợp lệ:

- sửa nguồn mặc định/canonical tương ứng;
- không thêm bảng;
- không đổi schema;
- không fresh-import database thật;
- nếu cần data correction cho môi trường hiện tại, tạo patch/update an toàn, idempotent và báo rõ trước khi chạy.

---

# 5. Backend logic bắt buộc

## 5.1. Capability là nguồn sự thật

Capability phải được xác định tập trung từ registry/contract hiện có, không rải logic ở nhiều handler và không hard-code frontend.

`EmailTemplateContracts.For(...)` hoặc service tương đương phải trả capability thật của template.

## 5.2. Policy phải đọc giá trị đã lưu

Không được dùng shipped default để thay thế policy đã lưu sau khi người dùng cập nhật.

Phải bảo đảm đồng nhất giữa:

- GET template contract/detail;
- GET contact settings;
- PUT contact settings;
- restore default;
- content validator;
- preview renderer;
- runtime renderer/send path.

Case phải hoạt động đúng:

```text
SUPPORTED + đổi REQUIRED → OPTIONAL
→ xóa block được nếu policy OPTIONAL cho phép
→ save, preview và runtime đều dùng OPTIONAL mới
```

```text
UNSUPPORTED
→ GET contract contactSupported=false
→ PUT/restore contact settings bị từ chối
→ preview không render contact block
→ runtime không cố resolve contact
```

## 5.3. Endpoint behavior

Với template unsupported:

- GET có thể trả capability/reason nhưng không trả một edit model giả khiến UI hiểu là editable;
- PUT contact settings trả `422` với mã rõ ràng, ví dụ:
  `EMAIL_TEMPLATE_CONTACT_NOT_SUPPORTED`;
- restore contact settings cũng trả cùng nhóm lỗi;
- validator body vẫn chặn block không hợp lệ;
- preview không thay block bằng text rỗng một cách âm thầm nếu điều đó che lỗi save; save phải báo lỗi rõ.

Backend vẫn là lớp bảo vệ cuối cùng ngay cả khi frontend đã ẩn form.

---

# 6. Save/restore UX

## 6.1. Hai phạm vi lưu độc lập

Nút cuối màn hình:

```text
Lưu thay đổi mẫu
```

chỉ lưu:

- tên mẫu;
- mô tả quản trị;
- subject VI;
- body VI;
- subject EN;
- body EN.

Trong Card 4:

```text
Lưu cấu hình liên hệ
```

chỉ lưu:

- requirement;
- nguồn thông tin liên hệ;
- visibility fields;
- heading VI/EN;
- Reply-To.

Không thêm `Lưu tất cả` nếu backend chưa có transaction chung.

## 6.2. Restore độc lập

```text
Phục hồi nội dung mẫu
```

không thay đổi contact settings.

```text
Phục hồi mặc định
```

trong Card 4 chỉ phục hồi contact settings và chỉ hiển thị khi:

```text
contactSupported=true && contactSettingsEditable=true
```

Không hiển thị nút contact restore cho template unsupported.

---

# 7. File/area cần audit trước khi sửa

Không giả định đường dẫn tuyệt đối nếu repository đã đổi. Dùng `rg --files` và `rg` để tìm.

Tối thiểu audit:

```text
TemplateManagement.tsx
ContactSettingsPanel.tsx
email template contract/types
email template API client
EmailTemplateContracts
EmailContactPolicyDefaults
contact settings GET/PUT/restore handlers
content validator
preview renderer
runtime renderer/dispatcher
TemplateManagement tests
contact settings unit/integration tests
canonical SQL/default template content
```

Tìm toàn repo:

```text
Lấy đầu mối từ
Ghi chú nội bộ cho người quản trị
Mã mẫu .* do hệ thống quản lý
Nội dung mẫu có thay đổi chưa lưu
contactInformationBlock
contactSupported
contactRequired
contactSettingsEditable
EmailContactPolicyDefaults
EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED
EMAIL_TEMPLATE_CONTACT_NOT_SUPPORTED
```

Ưu tiên tái sử dụng utility/component/service hiện có. Không tạo abstraction mới nếu chỉ dùng một nơi.

---

# 8. Kế hoạch triển khai

## Bước 0 — Preflight

```bash
git status --short
git branch --show-current
git log --oneline -10
git diff --check
```

Không reset/rebase/amend/squash/push nếu chưa có yêu cầu.

## Bước 1 — Reproduce

Chạy backend/frontend hiện tại và ghi bằng chứng trước khi sửa:

1. Mở `ACCOUNT_EMAIL_CONFIRMATION`.
2. Không sửa gì, kiểm tra dirty state có xuất hiện không.
3. Kiểm tra Card 4 có form contact không.
4. Kiểm tra label hiện tại.
5. Kiểm tra chiều cao hai nút trên template supported.
6. Kiểm tra body VI/EN có stale `{{contactInformationBlock}}` không.
7. Gọi contract/contact-settings API và lưu response.

Nếu source đã sửa nhưng UI vẫn cũ, xác nhận API/frontend đang chạy binary/bundle cũ trước khi thay đổi code tiếp.

## Bước 2 — Backend capability/policy

- Tách capability khỏi saved policy.
- Đồng bộ GET/PUT/restore/validator/preview/runtime.
- Thêm/điều chỉnh error code rõ ràng.
- Không đổi schema.

## Bước 3 — Frontend capability rendering

- Ẩn toàn bộ form contact với unsupported template.
- Hiện card reason read-only.
- Đổi label thành `Nguồn thông tin liên hệ`.
- Đồng bộ chiều cao/nội dung hai nút.
- Bỏ helper text dư.

## Bước 4 — Dirty state

- Normalize form.
- Baseline sau hydrate.
- Derived comparison.
- Reset baseline sau save/restore.
- Guard khi đóng chỉ xuất hiện khi dirty thật.

## Bước 5 — Stale block/default data

- Sửa shipped defaults/canonical source nếu sai.
- Hiện nút xóa block không hợp lệ cho dữ liệu tùy chỉnh cũ.
- Không tự sửa DB thật không báo trước.

## Bước 6 — Tests và runtime

Chạy targeted trước, sau đó gate cần thiết.

---

# 9. Tests bắt buộc

## 9.1. Frontend

### Initial dirty state

- Mở edit, không sửa → không hiện dirty.
- API hydrate → không dirty.
- Editor mount/onChange khởi tạo → không dirty.
- Đổi VI/EN tab → không dirty.
- Sửa rồi undo về giá trị ban đầu → không dirty.
- Save thành công → dirty biến mất.
- Chuyển template → baseline không bị giữ từ template trước.

### Unsupported contact

Với `ACCOUNT_EMAIL_CONFIRMATION`:

- không có radio requirement;
- không có label nguồn liên hệ;
- không có checkbox field;
- không có Reply-To;
- không có save/restore contact;
- có reason read-only;
- nếu body có stale block, có warning và nút xóa block.

### Supported contact

- label là `Nguồn thông tin liên hệ`;
- hai nút cùng chiều cao;
- nhãn không xuống dòng ở desktop;
- required template không có lựa chọn NONE;
- save contact không reset content dirty;
- save content không reset contact dirty.

### UI cleanup

Không còn các chuỗi:

```text
Mã mẫu ... do hệ thống quản lý và không thể thay đổi.
Ghi chú nội bộ cho người quản trị...
Lấy đầu mối từ
```

## 9.2. Backend unit/integration

- Contract unsupported trả `contactSupported=false`.
- Contract supported/required trả đúng capability và saved policy.
- PUT unsupported → 422 `EMAIL_TEMPLATE_CONTACT_NOT_SUPPORTED`.
- Restore unsupported → 422 tương ứng.
- Validator chặn unsupported body block.
- Saved policy được renderer/validator/preview đọc đúng, không quay về default.
- Restore default chỉ thay contact settings.
- Preview unsupported không chứa contact block/data.
- Runtime send path không resolve contact cho unsupported template.

## 9.3. Runtime smoke

Chạy với:

```text
Smtp__Enabled=false
```

Kiểm tra tối thiểu:

### `ACCOUNT_EMAIL_CONFIRMATION`

- mở edit không dirty;
- chỉ có một action hint đúng;
- Card 4 không có form;
- preview có đúng nút xác nhận;
- không có contact block;
- không có URL/token thật.

### `VISIT_PARTICIPANT_INVITATION`

- Card 4 editable;
- label đúng;
- hai nút bằng chiều cao;
- required policy không cho NONE;
- save/mở lại đúng.

### Một template supported nhưng không required

- có thể chọn policy hợp lệ;
- save/mở lại/preview dùng policy vừa lưu.

---

# 10. Gate hoàn thành

Chỉ báo hoàn thành khi đạt tất cả:

- [ ] `Lấy đầu mối từ` đã đổi thành `Nguồn thông tin liên hệ`.
- [ ] Hai nút contact cùng chiều cao và không xuống dòng sai.
- [ ] Mở edit không tự báo dirty.
- [ ] Dirty state biến mất khi undo về baseline.
- [ ] Bỏ hai đoạn helper text dư.
- [ ] `Mô tả quản trị` full-width textarea.
- [ ] Unsupported template không render form contact.
- [ ] Unsupported template không có save/restore contact.
- [ ] Capability không hard-code ở frontend.
- [ ] Capability và saved policy được tách riêng.
- [ ] GET/PUT/restore/validator/preview/runtime đồng nhất.
- [ ] Stale unsupported block được xử lý rõ ràng.
- [ ] Không thêm bảng, không đổi schema.
- [ ] Backend build xanh.
- [ ] Email targeted unit/integration xanh.
- [ ] Frontend typecheck xanh.
- [ ] Frontend targeted tests xanh.
- [ ] Frontend build xanh.
- [ ] Runtime smoke đạt với SMTP tắt.
- [ ] `git diff --check` xanh.
- [ ] Không push nếu chưa được yêu cầu.

---

# 11. Báo cáo cuối bắt buộc

```text
ROOT CAUSE
- Initial dirty state:
- Unsupported contact rendering:
- Capability vs saved policy:
- Button height:
- Redundant helper text:

FILES CHANGED
- path:
- path:

BEHAVIOR AFTER FIX
- ACCOUNT_EMAIL_CONFIRMATION:
- VISIT_PARTICIPANT_INVITATION:
- Supported optional template:

TESTS
- Backend build:
- Unit targeted:
- Integration targeted/full:
- Frontend typecheck:
- Frontend targeted:
- Frontend build:
- Runtime smoke:

SAFETY
- SMTP enabled/disabled:
- Schema changed:
- DB fresh-imported:
- Existing WIP preserved:
- Pushed:

FINAL VERDICT
- All acceptance criteria met:
- Remaining issue, if any:
```

Không báo `DONE` chỉ vì code compile. Phải kèm bằng chứng UI/API/runtime cho lỗi dirty state và unsupported contact form.
