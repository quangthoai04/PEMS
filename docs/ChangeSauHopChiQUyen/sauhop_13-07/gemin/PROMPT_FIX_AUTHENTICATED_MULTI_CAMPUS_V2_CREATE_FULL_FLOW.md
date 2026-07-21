# PEMS — Prompt triển khai hoàn chỉnh luồng tạo đơn V2 liên cơ sở và lựa chọn người xử lý

> **Loại tài liệu:** Prompt bàn giao triển khai tự chứa  
> **Đối tượng sử dụng:** AI coding agent hoặc developer chưa từng đọc source PEMS  
> **Phạm vi:** SQL + Backend + Frontend + Automated Tests + Real-browser verification  
> **Trạng thái xuất phát:** `IN PROGRESS` — chưa được gọi là hoàn thành trải nghiệm V2 trên luồng Dashboard thực tế  
> **Mục tiêu cuối:** Người dùng tạo được đơn một hoặc nhiều campus bằng form V2; mọi dữ liệu thuộc campus được nhập và lưu độc lập; Staff/Staff Leader chọn đúng cách xử lý ngay khi tạo đơn.

---

# 1. Lệnh điều hành dành cho agent tiếp nhận

Bạn là **Senior Software Architect + Senior Full-stack Engineer + Database Engineer + QA Engineer** tiếp tục triển khai PEMS trên repository thật.

Bạn phải tự đọc source, xác minh trạng thái hiện tại, sửa đầy đủ luồng nghiệp vụ và chạy kiểm thử có bằng chứng. Tài liệu này đủ để định hướng người chưa đọc code, nhưng **không được dùng tài liệu để thay thế việc kiểm tra code và SQL tại HEAD**.

Không được:

- chỉ sửa giao diện để trông giống V2 nhưng payload vẫn là V1;
- chỉ mở trực tiếp `/visit-registration/v2` hoặc `/visit/create-v2` để tuyên bố PASS;
- chỉ sửa môi trường review `localhost:5273` trong khi luồng người dùng tại `localhost:3000` vẫn mở popup V1;
- thêm bảng hoặc enum mới khi schema hiện tại đã hỗ trợ nghiệp vụ;
- suy đoán business rule khi code, SQL và tài liệu mâu thuẫn;
- đưa upload ảnh, invitation sau duyệt, OCR, Gallery hoặc contract-drop vào scope này;
- mutation các database được bảo vệ;
- push, merge, rebase hoặc deploy khi chưa được chủ dự án yêu cầu.

Kết quả chỉ được coi là hoàn thành khi người dùng thao tác từ **nút “Tạo đoàn khách” thật trong Dashboard**, form V2 xuất hiện, nhập được dữ liệu khác nhau theo từng campus, lựa chọn xử lý đúng theo vai trò, backend lưu đúng và database chứng minh được kết quả.

---

# 2. Bối cảnh lỗi hiện tại

## 2.1 Hiện tượng người dùng nhìn thấy

Tại ứng dụng đang sử dụng ở `http://localhost:3000`, sau khi đăng nhập và bấm **“Tạo đoàn khách”**, hệ thống vẫn mở popup cũ:

- tiêu đề “Đăng ký tham quan trường”;
- component V1 dạng modal;
- chọn “Liên cơ sở” chỉ tạo nhiều dòng lịch trình;
- `Tên đoàn khách`, `Loại hình thăm/làm việc`, `Mục đích`, `Nội dung làm việc` và các thông tin khác vẫn chỉ có một ô dùng chung.

Form đó không thể biểu diễn trường hợp:

- HN có tên đoàn/mục đích/nội dung làm việc A;
- HCM có tên đoàn/mục đích/nội dung làm việc B;
- operational contact, thành viên, ngôn ngữ hoặc yêu cầu hỗ trợ khác nhau giữa hai campus.

Đây là form V1 có nhiều lịch, **không phải form per-campus V2 hoàn chỉnh**.

## 2.2 Vì sao báo cáo cũ chưa đủ

Một số kiểm thử trước đó chạy trên review stack:

- frontend review: `localhost:5273`;
- API review: `localhost:5299`;
- database: `pems_review_v2`.

Trong khi ảnh thực tế của người dùng là `localhost:3000`. Vì vậy kết luận “V2 EXPERIENCE READY” trước đó chỉ chứng minh một review environment được cấu hình cờ V2, chưa chứng minh entry point và cấu hình của ứng dụng người dùng đang chạy.

## 2.3 Lỗi nghiệp vụ thứ hai

Trong luồng tạo đơn sau khi đăng nhập, logic cũ đã bị thiếu trên form V2:

### Staff thường — `role_code = STAFF`, `sub_role = STAFF`

- Có thể tự nhận làm host cho campus của chính mình khi tạo chính request đó.
- Hoặc gửi request ở trạng thái chờ để Staff Leader campus phân công người xử lý.

### Staff Leader — `role_code = STAFF`, `sub_role = LEADER`

- Có thể tự nhận làm host.
- Có thể giao ngay cho một Staff hợp lệ cùng campus.
- Có thể chưa phân công và để campus chờ xử lý sau.

Các lựa chọn này là logic của **thời điểm tạo request**, không phải chức năng invitation hoặc assignment sau duyệt được kiểm thử ở workstream khác.

---

# 3. Kết quả bắt buộc sau khi sửa

Sau khi hoàn thành:

1. Tất cả entry point tạo đơn dùng chung một resolver capability.
2. Khi V2 ON, Dashboard không bao giờ mở `VisitingFormPopup` V1.
3. Khi V2 ON, public CTA vào form public V2 và authenticated CTA vào form authenticated V2.
4. Khi backend xác nhận V2 OFF, V1 vẫn hoạt động để tương thích rollout.
5. Khi capability lỗi, hiển thị lỗi và Retry; không fallback âm thầm sang V1.
6. Mỗi campus có một snapshot form độc lập.
7. Việc sao chép dữ liệu giữa campus chỉ là deep-copy một lần, không tạo shared state.
8. Staff và Staff Leader nhìn thấy đúng lựa chọn xử lý theo campus scope.
9. Public Visitor không nhìn thấy lựa chọn xử lý nội bộ.
10. Backend tự suy ra decision/host/audit columns; không tin các system fields do client gửi.
11. Toàn bộ parent request, instances, details, members, processing decisions và audit được tạo nhất quán trong một transaction.
12. Database và automated tests chứng minh đủ positive, negative và cross-campus isolation cases.

---

# 4. Nguồn sự thật và thứ tự ưu tiên

Trước khi sửa code, đọc và đối chiếu:

1. Yêu cầu mới nhất của chủ dự án trong tài liệu này.
2. SQL/schema/master/migrations thực tế tại HEAD.
3. Entity, EF mapping, command/service/validator và test đang chạy tại HEAD.
4. `PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT*.md`.
5. `PEMS_CANONICAL_BUSINESS_RULES*.md`.
6. `PEMS_UC_IMPLEMENTATION_RULEBOOK*.md`.
7. `PERMISSION_RULES.md` và `PERMISSION_MATRIX.md`.
8. UI design system và các component đang dùng.
9. Progress report chỉ dùng làm chỉ dẫn; không coi claim trong report là bằng chứng.

## 4.1 Cảnh báo tài liệu stale

Tài liệu cũ có chỗ nói `ASSIGNED` đã bị bỏ, nhưng SQL thực tế được cung cấp vẫn dùng:

- `WAITING_REQUEST_APPROVAL`;
- `ASSIGNED`;
- `BEFORE_VISIT`;
- `DURING_VISIT`;
- `AFTER_VISIT`;
- `CLOSED`;
- `CANCELLED`;
- `REJECTED`.

Không tự sửa lifecycle chỉ dựa trên một dòng tài liệu cũ. Phải ưu tiên schema + entity + tests hiện hành và ghi rõ mọi mâu thuẫn phát hiện được.

Tương tự, các tài liệu cũ về HO duyệt request liên cơ sở có thể đã lỗi thời. Rule hiện tại của chương trình per-campus là:

- HO monitor/read-only;
- mỗi Staff Leader xử lý campus của mình;
- không tái đưa centralized HO approval vào luồng này.

---

# 5. Thuật ngữ và mô hình dữ liệu phải hiểu trước khi code

## 5.1 Request cha

`visit_requests` là aggregate/root request. Một đơn liên cơ sở vẫn chỉ có một request cha.

Request-level giữ những dữ liệu thật sự dùng chung, ví dụ:

- request code;
- submission/idempotency identity;
- registrant user và registrant snapshot;
- primary contact/owner và contact-access state;
- partner nếu có;
- created source;
- `visit_scope`;
- `form_schema_version`;
- `has_mixed_campus_details`;
- aggregate status;
- submit/resubmit/cancel metadata;
- row version và audit metadata.

Không hiểu “một request cha” thành “mọi nội dung form đều dùng chung”.

## 5.2 Campus instance

Mỗi campus tạo một dòng `visit_request_campuses` với:

- `visit_instance_id`;
- `visit_request_id`;
- `campus_id`;
- `planned_start_at`;
- `planned_end_at`;
- campus lifecycle status;
- coordinator/routing;
- decision metadata;
- host metadata;
- row version.

Host và quyết định xử lý thuộc campus instance, không thuộc request tổng.

## 5.3 Form detail theo campus

Mỗi instance có một snapshot `visit_instance_form_details` độc lập, gồm tối thiểu đúng các field canonical hiện hành:

- `delegation_name`;
- `visit_type`;
- `visit_type_other`;
- `purpose`;
- `working_content`;
- `operational_contact_full_name`;
- `operational_contact_organization`;
- `operational_contact_phone`;
- `operational_contact_email`;
- `working_language`;
- `transportation_note`;
- `media_consent_status`;
- `media_consent_note`;
- `note_to_fptu`;
- revision/concurrency fields do backend quản lý.

Nếu schema hiện tại đã đổi tên, thêm hoặc bỏ field, phải lập mapping từ entity/DTO/schema thực tế trước khi code. Không copy danh sách này một cách máy móc nếu HEAD khác.

## 5.4 Thành viên theo campus

Guest/support members của request V2 phải độc lập theo campus:

- mỗi campus có collection riêng;
- copy từ campus A sang B tạo bản sao dữ liệu/ID độc lập theo rule hiện tại;
- sửa hoặc xóa member ở A không thay đổi B;
- không link nhầm member giữa hai request.

## 5.5 Primary contact và operational contact

Không được trộn hai khái niệm:

### Primary contact

- request-level;
- liên quan account VISITOR và ownership/co-edit workflow;
- dùng cho claim/transfer/cancel theo lifecycle.

### Operational contact

- per-campus snapshot;
- chỉ là đầu mối vận hành tại campus;
- có thể khác giữa HN/HCM;
- không tự cấp account hoặc quyền request.

## 5.6 Compatibility projection

Các legacy global fields trên `visit_requests` có thể còn tồn tại tạm thời.

Với `form_schema_version = 2`:

- source of truth là `visit_instance_form_details`;
- legacy columns chỉ là compatibility projection;
- mixed request có thể projection từ campus có `campus_id` nhỏ nhất theo rule hiện hành;
- read/search/report V2 không được coi projection là dữ liệu canonical;
- không drop legacy columns trong nhiệm vụ này.

## 5.7 `has_mixed_campus_details`

Backend tính; frontend không được gửi giá trị quyết định.

So sánh nội dung copyable đã normalize:

- form detail;
- operational contact;
- guest/support member sets;
- additional requirements hiện hành.

Không tính:

- `campus_id`;
- schedule;
- processing/host decision nếu canonical service hiện tại không đưa chúng vào fingerprint mixed.

Kỳ vọng:

- chỉ campus/time khác, nội dung giống nhau → `has_mixed_campus_details = 0`;
- bất kỳ nội dung canonical nào khác → `has_mixed_campus_details = 1`.

---

# 6. Business rules xử lý đơn ngay khi tạo

## 6.1 Các cột và nguồn quyết định đã có

Schema hiện tại đã có các trường trên `visit_request_campuses`:

- `current_host_user_id`;
- `host_assigned_by`;
- `host_assigned_at`;
- `decided_by`;
- `decided_at`;
- `decision_actor_role`;
- `decision_source`;
- `decision_note`.

Các giá trị `decision_source` đã được ghi nhận:

- `STANDARD_CAMPUS_REVIEW`;
- `INTERNAL_SELF_HOST`;
- `INTERNAL_LEADER_ASSIGN`.

Không tạo thêm enum/bảng chỉ để biểu diễn các lựa chọn UI nếu contract hiện tại đã đủ.

## 6.2 Staff thường tự nhận

Chỉ hợp lệ khi đồng thời thỏa:

1. Current user là ACTIVE `STAFF/STAFF`.
2. Đây là request do chính user đó tạo/đăng ký.
3. Campus instance thuộc `currentUser.primary_campus_id`.
4. Quyết định được thực hiện trong transaction tạo request.
5. User không giả mạo ID actor/host từ client.

Hiệu ứng nghiệp vụ dự kiến theo schema hiện tại:

- actor trở thành host chính thức của campus đó;
- decision source là `INTERNAL_SELF_HOST`;
- `decided_by`, `host_assigned_by`, `current_host_user_id` cùng là current user;
- `decision_actor_role = STAFF`;
- timestamps do server tạo;
- lifecycle/aggregate status do backend/DB rule hiện hành tính.

Regular Staff không được dùng self-host để tự duyệt một request pending có sẵn sau thời điểm create.

## 6.3 Staff thường yêu cầu Leader phân công

Đây không cần là một persisted decision enum mới nếu model hiện tại dùng trạng thái pending.

Hiệu ứng:

- instance giữ `WAITING_REQUEST_APPROVAL`;
- route coordinator tới Staff Leader ACTIVE đúng campus theo service hiện hành;
- `current_host_user_id = NULL`;
- `host_assigned_by = NULL`;
- `decided_by = NULL`;
- toàn bộ decision metadata chưa được set;
- Staff Leader xử lý sau bằng standard campus review.

UI có thể hiển thị nhãn “Yêu cầu Staff Leader phân công người xử lý”. Backend phải map vào pending state hiện hành, không lưu một trạng thái giả.

## 6.4 Staff Leader tự nhận

Chỉ hợp lệ cho campus trùng `primary_campus_id` của Leader.

Hiệu ứng:

- Leader là người quyết định và là host;
- server sử dụng đúng decision source đã có cho create-time leader processing;
- `decided_by` và `host_assigned_by` là Leader;
- `current_host_user_id` là chính Leader;
- `decision_actor_role = STAFF_LEADER`;
- không cho Leader thao tác sibling campus.

Agent phải kiểm tra command/service/tests hiện tại để dùng đúng source (`INTERNAL_LEADER_ASSIGN` hoặc mapping canonical đang tồn tại), không tự tạo tên khác.

## 6.5 Staff Leader giao cho Staff khác

Chỉ cho chọn candidate thỏa điều kiện hiện hành, tối thiểu:

- ACTIVE;
- `role_code = STAFF`;
- `sub_role = STAFF`;
- cùng campus;
- thuộc nhóm/department IC nếu code hiện hành yêu cầu;
- không vi phạm rule eligibility hiện tại.

Nếu API host-candidate hiện trả conflict lịch, hiển thị thông tin để Leader quyết định theo contract đang có. Không tự biến warning thành hard block hoặc bỏ hard block nếu business rule chưa nói vậy.

Hiệu ứng:

- selected Staff là `current_host_user_id`;
- Leader là `decided_by` và `host_assigned_by`;
- `decision_actor_role = STAFF_LEADER`;
- dùng source create-time leader assignment hiện hành;
- backend re-query và re-authorize candidate; không tin dropdown client.

## 6.6 Staff Leader để xử lý sau

Đây là điểm dễ code sai nhất.

“Để xử lý sau” **không phải**:

- approve nhưng không có host;
- status `ASSIGNED` với host NULL;
- tạo decision row giả;
- tự gán coordinator làm host.

Nó phải là:

- instance ở `WAITING_REQUEST_APPROVAL`;
- coordinator được route đúng Leader/campus nếu model dùng coordinator;
- host fields NULL;
- decision fields NULL;
- sau đó xử lý qua `STANDARD_CAMPUS_REVIEW`.

## 6.7 Đơn nhiều campus

Processing phải là per-campus, không phải một lựa chọn request-level.

Ví dụ user là Staff HN tạo HN + HCM:

- card HN: cho chọn tự nhận hoặc yêu cầu Leader HN phân công;
- card HCM: user HN không có quyền tự nhận/giao; HCM tự động pending và route Staff Leader HCM;
- không hiển thị dropdown candidate HCM cho Staff HN;
- không dùng lựa chọn HN để set host cho HCM.

Ví dụ user là Staff Leader HN tạo HN + HCM:

- card HN: tự nhận/giao Staff HN/để xử lý sau;
- card HCM: pending và route Staff Leader HCM;
- Leader HN không được quyết định host HCM.

Nếu request không chứa primary campus của current user, toàn bộ campuses nằm ngoài scope quyết định của họ và phải pending/routed theo rule hiện hành.

## 6.8 Public Visitor

Public form không hiển thị processing controls.

Mọi campus instance sau submit:

- pending đúng lifecycle;
- route Staff Leader đúng campus;
- không có host/decision giả.

---

# 7. Contract khái niệm

Contract mục tiêu có dạng khái niệm:

```text
VisitRequestFormV2
  submissionId
  registrant
  primaryContact
  partnerId?
  campusVisits[]

CampusVisit
  clientKey
  visitInstanceId?          // edit only
  campusId
  startDatetime
  endDatetime
  delegationName
  visitType
  visitTypeOther?
  purpose
  workingContent?
  visitors[]
  supportMembers[]
  operationalContact
  workingLanguage
  transportationNote?
  mediaConsentStatus
  mediaConsentNote?
  noteToFptu?
  processing?               // authenticated create only, per-campus
  rowVersion?               // edit only
```

Client có thể gửi một processing intent tối thiểu, ví dụ về mặt khái niệm:

```text
processing.mode
processing.hostUserId?
```

Tên mode thực tế phải lấy từ code hiện tại. Không tạo các string mới chỉ vì tài liệu dùng các nhãn khái niệm “SELF_HOST”, “ASK_LEADER”, “ASSIGN_STAFF”, “DEFER”.

Client không được gửi và backend không được tin:

- `formSchemaVersion`;
- `visitScope`;
- `hasMixedCampusDetails`;
- aggregate/campus status;
- `decidedBy`;
- `decisionActorRole`;
- `decisionSource`;
- `hostAssignedBy`;
- audit timestamps;
- coordinator ID;
- arbitrary registrant user ID;
- arbitrary `sameForAll` flag.

Backend derive toàn bộ system fields từ current user, selected campuses và server-side policy.

---

# 8. Frontend — yêu cầu triển khai chi tiết

## 8.1 Lập bản đồ code trước khi sửa

Dùng `rg --files` và `rg -n` để xác nhận đường dẫn tại HEAD. Các file/candidate đã từng tồn tại:

```text
frontend/pems-react/src/App.tsx
frontend/pems-react/src/pages/visit/VisitRequestV2Page.tsx
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestFormV2.tsx
frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx
frontend/pems-react/src/features/visit-request/api/visitRequestV2Api.ts
frontend/pems-react/src/shared/features/perCampusV2Capability.tsx
frontend/pems-react/src/shared/features/perCampusV2Entry.ts
frontend/pems-react/src/components/.../VisitingFormPopup.tsx
frontend/pems-react/src/features/.../CreateVisitRequest.tsx
```

Tìm tất cả entry points:

```bash
rg -n "setIsVisitorFormOpen\(true\)|VisitingFormPopup|visit-registration/v2|visit/create-v2|resolveVisitEntryOutcome|useVisitEntryCta|Tạo đoàn khách|Đăng ký tham quan"
```

Không dừng ở Home/FAQ/Partners. Phải bao phủ:

- Home hero;
- final CTA;
- FAQ;
- Partners;
- navbar/header nếu có;
- Dashboard create button;
- mobile menus;
- any shortcut/card/action menu;
- direct routes và legacy redirects.

## 8.2 Một resolver capability duy nhất

Tất cả entry points phải gọi cùng một decision function/hook.

Ma trận bắt buộc:

| Capability result | Public CTA | Authenticated Dashboard CTA |
|---|---|---|
| V2 read + write enabled | `/visit-registration/v2` | `/visit/create-v2` |
| Backend xác nhận V2 OFF | V1 popup | V1 authenticated behavior hiện hành |
| Loading | disable/progress, không double click | disable/progress |
| Error/timeout | error toast + Retry | error toast + Retry |

Không fallback V1 khi HTTP error, parse error hoặc network error.

Provider phải expose `retry()` và resolver không được cache lỗi vĩnh viễn.

## 8.3 Sửa đúng môi trường người dùng

Phải xác định:

1. `localhost:3000` được start bằng script nào.
2. Frontend này đang gọi API base URL nào.
3. API đó trả capability gì.
4. Hai flag thực tế là gì và lấy từ đâu.
5. Vì sao review launcher bật được nhưng normal launcher không bật.

Không bật production mặc định nếu rollout policy chưa cho phép. Tuy nhiên phải cung cấp một cách **chuẩn, lặp lại được và có tài liệu** để normal local experience tại `localhost:3000` chạy V2, ví dụ launcher/env development được quản lý đúng convention dự án.

Không commit password, token hoặc credential.

## 8.4 Không nhân bản form

Không xây một form V2 thứ hai riêng cho Dashboard.

Phải tái sử dụng chung:

- schema/type;
- field components;
- campus state model;
- validation;
- payload mapper;
- per-campus card;
- error focus;
- API client.

Public và authenticated có thể khác shell/route và authenticated processing panel, nhưng core campus form phải dùng chung.

Nếu sản phẩm muốn giao diện modal, có thể render `VisitRequestFormV2` bên trong modal shell. Không được tiếp tục mở rộng `VisitingFormPopup` V1 thành một implementation song song dễ drift.

## 8.5 Bố cục form đề xuất

### Phần A — thông tin dùng chung

- registrant identity/snapshot;
- primary contact;
- partner hoặc request-level fields khác theo contract hiện tại.

### Phần B — chọn campus

- thêm/xóa campus;
- không chọn trùng campus;
- hiển thị rõ số campus;
- mỗi campus có stable `clientKey`.

### Phần C — card/tab cho từng campus

Mỗi card chứa:

- campus name;
- schedule;
- đầy đủ form-detail fields;
- operational contact;
- guest/support members;
- additional requirements;
- processing panel nếu authenticated và current user có quyền ở campus đó;
- validation summary của chính card.

### Phần D — review trước submit

- common request data một lần;
- từng campus thành section riêng;
- badge “giống campus X” hoặc “có dữ liệu riêng” nếu hữu ích;
- processing choice hiển thị rõ;
- không collapse mixed content thành một dòng global.

## 8.6 Global default và deep copy

Để nhập nhanh, UI có thể hỗ trợ:

- “Sao chép từ campus trước”;
- “Áp dụng nội dung hiện tại cho các campus đã chọn” với confirm rõ ràng;
- tạo campus mới từ một snapshot mặc định.

Nhưng mỗi thao tác phải deep-copy một lần.

Sau copy:

- đổi `delegationName` HCM không đổi HN;
- thêm member HCM không thêm vào HN;
- đổi operational contact HCM không đổi HN;
- nested arrays/objects không share reference;
- processing không được copy xuyên campus nếu actor không có scope.

Không dùng hidden continuous synchronization giữa campuses.

## 8.7 State identity

Không dùng array index làm identity duy nhất.

Dùng:

- `clientKey` ổn định cho campus chưa persist;
- `visitInstanceId` khi đã persist;
- `campusId` để enforce unique selection.

Khi xóa/reorder/switch tab:

- dữ liệu không nhảy sang campus khác;
- error không gắn nhầm card;
- dirty state không mất;
- focus hợp lệ.

## 8.8 Validation frontend

Phải đồng bộ với backend canonical rules:

- campus required và unique;
- end > start;
- duration tối thiểu 30 phút;
- `visitTypeOther` required khi type OTHER;
- required/optional fields đúng schema;
- phone/email dùng cùng policy đã quyết định hoặc giữ issue `NEEDS-BUSINESS-DECISION` nếu chưa chốt;
- max lengths theo backend/DB;
- collection limits;
- processing mode và candidate ID hợp lệ về shape.

Error path phải chỉ đúng campus, ví dụ:

```text
campusVisits[clientKey].purpose
campusVisits[clientKey].operationalContact.email
campusVisits[clientKey].processing.hostUserId
```

## 8.9 Processing panel theo role

### Public/Visitor

- không render.

### Staff thường — own primary campus

- “Tôi sẽ xử lý đoàn tại campus này”.
- “Yêu cầu Staff Leader phân công”.

### Staff Leader — own primary campus

- “Tôi sẽ xử lý”.
- “Giao cho nhân sự khác”.
- “Để xử lý sau”.

Nếu chọn giao người khác:

- gọi API candidates đúng campus;
- hiển thị only eligible Staff;
- loading/empty/error states rõ;
- không dùng candidate cached từ campus khác;
- khi đổi mode phải clear stale `hostUserId`.

### Sibling campus ngoài scope

- hiển thị read-only notice “Campus này sẽ được chuyển tới Staff Leader của cơ sở tương ứng”.
- không render candidate picker;
- không gửi intent giả.

## 8.10 Accessibility và responsive

- keyboard navigation cho tabs/cards/radio;
- label và error association;
- focus campus đầu tiên lỗi sau submit;
- không chỉ dùng màu để phân biệt campus;
- responsive tối thiểu 390px;
- tiếng Việt đúng, không để raw enum/error code;
- giữ design system hiện hành.

---

# 9. Backend — yêu cầu triển khai chi tiết

## 9.1 Khảo sát code

Tìm và đọc đầy đủ:

- controller route V1 và V2;
- authenticated create command/handler;
- public initiate/verify V2;
- V2 aggregate creation service;
- DTO/request models;
- FluentValidation;
- host candidate query;
- current-user service;
- Staff Leader routing service;
- entity methods của `VisitRequest` và campus instance;
- EF mapping/converters cho status/source;
- audit/revision writer;
- idempotency/fingerprint code;
- tests liên quan create/auth/self-host/leader assign.

Các tên từng được ghi nhận, nhưng phải xác minh tại HEAD:

```text
CreateAuthenticatedVisitRequest
CreateAuthenticatedVisitRequestCommandHandler
POST /api/v2/visit-requests
CreateV2Async
GetHostCandidates
PerCampusFormV2
PerCampusFormV2Write
```

Tìm bằng:

```bash
rg -n "CreateAuthenticatedVisitRequest|CreateV2Async|INTERNAL_SELF_HOST|INTERNAL_LEADER_ASSIGN|processing|hostUserId|current_host_user_id|GetHostCandidates|PerCampusFormV2"
```

## 9.2 Endpoint strategy

Không đổi shape âm thầm trên route V1.

Giữ:

- V1 route cho explicit V2 OFF;
- V2 public initiate/verify flow;
- V2 authenticated create route/command.

Nếu public và authenticated đang dùng cùng create service, giữ một canonical domain/application service và truyền authenticated context rõ ràng. Không copy-paste transaction logic.

## 9.3 Structural validator

Validate request shape trước khi vào service:

- `submissionId` hợp lệ;
- registrant/primary-contact shape;
- campus array non-empty;
- campus unique;
- stable field paths;
- schedule/duration;
- all nested form details;
- members;
- processing discriminated shape;
- không nhận system-derived fields.

Service vẫn revalidate business/auth/data state trong transaction.

## 9.4 Derive actor context

Backend lấy current user từ auth context.

Không dùng:

- user ID do client tự nhập;
- role/subRole do client gửi;
- primary campus do client gửi;
- host-assigned-by do client gửi.

Normalize role rules:

- Staff thường: `STAFF/STAFF`;
- Staff Leader: `STAFF/LEADER`;
- role khác không có internal processing rights trừ khi canonical rule nói rõ.

## 9.5 Validate processing theo từng campus

Cho mỗi `campusVisit`:

1. Load campus và active Staff Leader routing.
2. Xác định campus có thuộc primary campus của actor hay không.
3. Xác định actor là regular Staff, Staff Leader hay không có internal rights.
4. Validate processing intent phù hợp.
5. Nếu outside scope, reject tampered processing hoặc ignore chỉ khi contract đã quy định rõ; ưu tiên fail-closed với stable error.
6. Derive database decision/host fields.

Không validate một lần ở request level rồi áp dụng cho mọi campus.

## 9.6 Mapping processing intent

Mapping phải tập trung tại một service/method có unit tests.

| Actor/action | Host | Decision actor | Source | Trạng thái ý nghĩa |
|---|---|---|---|---|
| Staff self-host own campus | current user | current user/STAFF | `INTERNAL_SELF_HOST` | xử lý ngay |
| Staff asks Leader | NULL | NULL | NULL | pending |
| Leader self-host own campus | current Leader | current Leader/STAFF_LEADER | source create-time hiện hành | xử lý ngay |
| Leader assigns Staff | selected Staff | current Leader/STAFF_LEADER | `INTERNAL_LEADER_ASSIGN` hoặc exact current constant | xử lý ngay |
| Leader defers | NULL | NULL | NULL | pending |
| Outside-scope campus | NULL | NULL | NULL | pending + correct routing |
| Public | NULL | NULL | NULL | pending + correct routing |

Không xem tên action khái niệm trong bảng là yêu cầu tạo enum frontend/backend mới. Tái sử dụng exact constants hiện có.

## 9.7 Host candidate security

Khi Leader chọn host:

- query lại user trong backend;
- ACTIVE;
- regular Staff;
- same campus;
- department/IC rule đúng current implementation;
- không chấp nhận Leader khác nếu rule chỉ cho Leader tự host chính mình;
- không chấp nhận ID từ campus khác;
- không chấp nhận disabled/deleted user;
- xử lý race user bị disable sau lúc UI load;
- trả error code ổn định, không leak dữ liệu ngoài scope.

## 9.8 Transaction invariant

Một create V2 thành công phải commit đồng thời:

- 1 parent `visit_requests`;
- N `visit_request_campuses`;
- N `visit_instance_form_details`;
- member rows/links theo campus;
- routing/coordinator;
- processing decision/host cho campus hợp lệ;
- baseline revisions/history;
- audit;
- identity INITIAL_CLAIM nếu applicable;
- compatibility projection;
- derived scope/mixed/fingerprint.

Bất kỳ lỗi nào phải rollback toàn bộ.

Không gửi external notification trước commit. Nhiệm vụ này không yêu cầu thay notification workflow, nhưng test environment không được gửi thật.

## 9.9 Idempotency

Giữ `submissionId` idempotency:

- retry cùng submission không tạo request thứ hai;
- concurrent unique race trả winner theo convention hiện hành;
- processing/members/details không duplicate;
- payload khác với submission đã dùng phải xử lý theo contract hiện hành, không overwrite mù.

## 9.10 Derived aggregate status

Không để client gửi aggregate status.

Backend/DB phải derive đúng khi:

- tất cả campus pending;
- một campus xử lý ngay, sibling pending;
- tất cả campus xử lý ngay;
- các tình huống reject/approve sau create.

Đặc biệt, một instance được self-host không được tự động gán cùng host cho sibling campus.

## 9.11 Audit và revision

Audit phải ghi:

- actor thực;
- request/instance scope;
- source chính xác;
- host nếu có;
- correlation/submission ID;
- field paths theo campus;
- không log PII/token/password không cần thiết.

Audit failure làm transaction failure theo invariant hiện hành.

## 9.12 Error contract

Tái sử dụng stable error envelope. Bổ sung error codes chỉ khi chưa có equivalent.

Cần phân biệt tối thiểu:

- V2 read/write flags không tương thích;
- duplicate/invalid campus;
- campus outside processing scope;
- self-host không hợp lệ;
- candidate không hợp lệ;
- candidate khác campus;
- missing Staff Leader routing;
- invalid processing mode;
- form validation error theo campus;
- concurrency/idempotency conflict.

Không trả raw SQL exception hoặc enum parse error ra UI.

---

# 10. SQL và persistence

## 10.1 Kỳ vọng ban đầu

Schema đã có host/decision fields và decision sources cần thiết. Vì vậy **không mặc định tạo migration mới**.

Trước khi thay SQL:

1. So sánh current master, additive migration, fresh-target và EF mapping.
2. Kiểm tra `SHOW CREATE TABLE visit_request_campuses` trên disposable review DB.
3. Kiểm tra trigger insert/update hiện hành.
4. Kiểm tra exact enum values.
5. Xác nhận current code đang map các values này thế nào.

Chỉ sửa SQL nếu có drift thật.

## 10.2 Invariants phải giữ

### Pending instance

Khi `status = WAITING_REQUEST_APPROVAL`:

- host fields NULL;
- decision fields NULL;
- không có fake approval.

### Regular Staff self-host

- chỉ create-time;
- exact registrant/current actor;
- same campus;
- active regular Staff;
- `decided_by = host_assigned_by = current_host_user_id`;
- source/actor role đúng.

### Leader decision

- same-campus Staff Leader;
- selected host hợp lệ;
- `decided_by` và `host_assigned_by` đúng Leader;
- Leader self-host chỉ khi `current_host_user_id` là chính Leader;
- post-create decision dùng standard review theo trigger hiện hành.

### Official host immutability

Không phá rule host không được đổi tùy ý sau first assignment nếu current schema đang enforce.

## 10.3 Đồng bộ schema sources

Nếu thật sự cần SQL change, cập nhật đồng bộ:

- additive/upgrade script;
- current master fresh-create;
- deterministic fresh target/generator nếu có;
- rollback/verify/preflight;
- EF entity configuration/converter;
- schema drift tests;
- documentation trong cùng functional commit.

Không sửa chỉ một master file rồi để migration drift.

## 10.4 Không contract-drop

Nhiệm vụ này không được:

- drop 10 legacy columns;
- chạy Phase I destructive payload;
- tuyên bố contract-drop ready;
- thay compatibility projection thành source of truth.

---

# 11. Luồng end-to-end phải hoạt động

## 11.1 Public single-campus

1. Người dùng bấm CTA thật.
2. Capability ON route V2.
3. Điền request-level + một campus detail.
4. Initiate OTP.
5. Verify OTP.
6. Backend tạo request V2.
7. Campus pending, routed đúng Leader, không host giả.

## 11.2 Public multi-campus mixed

1. Chọn HN và HCM.
2. Nhập detail HN.
3. Copy sang HCM nếu muốn.
4. Sửa HCM thành nội dung khác.
5. Quay lại HN, dữ liệu không đổi.
6. Submit/OTP.
7. DB có hai instances + hai details khác nhau.
8. `has_mixed_campus_details = 1`.
9. Cả hai pending/routed đúng campus.

## 11.3 Staff single-campus self-host

1. Login regular Staff.
2. Bấm Dashboard “Tạo đoàn khách”.
3. Form authenticated V2 xuất hiện.
4. Chọn own campus.
5. Chọn tự nhận.
6. Submit.
7. DB chứng minh actor/host/source/status đúng.

## 11.4 Staff single-campus ask Leader

1. Login regular Staff.
2. Chọn own campus.
3. Chọn yêu cầu Leader phân công.
4. Submit.
5. Instance pending, host/decision NULL, coordinator/routing đúng.

## 11.5 Staff multi-campus

1. Staff HN tạo HN + HCM.
2. HN cho chọn self/ask Leader.
3. HCM không cho Staff HN tự nhận hoặc chọn host.
4. Submit.
5. Hai campus lưu form độc lập.
6. HN processing đúng lựa chọn.
7. HCM pending/routed Staff Leader HCM.

## 11.6 Staff Leader self-host

1. Login Staff Leader HN.
2. Tạo request có HN.
3. Chọn tự nhận.
4. Submit.
5. Leader là decider/assigner/host đúng schema.

## 11.7 Staff Leader assign another

1. Login Staff Leader HN.
2. Chọn giao nhân sự khác.
3. Candidate list chỉ có eligible Staff HN.
4. Chọn candidate và submit.
5. Selected Staff là host; Leader là decider/assigner.

## 11.8 Staff Leader defer

1. Login Staff Leader HN.
2. Chọn để xử lý sau.
3. Submit.
4. HN vẫn pending.
5. Host/decision NULL.
6. Không tạo trạng thái “approved without host”.

## 11.9 Staff Leader multi-campus

1. Leader HN tạo HN + HCM.
2. HN có ba lựa chọn.
3. HCM read-only processing notice.
4. Leader HN không nhìn thấy candidates HCM.
5. HCM pending/routed Leader HCM.

---

# 12. Kế hoạch triển khai theo thứ tự

## Phase 0 — Snapshot và audit

1. `git status --short --branch`.
2. Ghi HEAD, remote HEAD, merge-base, ahead/behind.
3. Ghi nhận untracked/user changes; không đụng file không liên quan.
4. Audit commits gần nhất liên quan V2 entry/form/processing.
5. Lập bảng current route → component → API → flags.
6. Lập bảng exact DTO fields và processing modes.
7. Lập bảng SQL/EF mappings.

Reported historical commits để định hướng, không được tin mù:

- `e683d756`: centralize V2 public entry CTA behavior;
- `9aa3da28`: documents enum fix;
- `382d50fc`: later review/test work.

Actual HEAD có thể đã đổi; không rewrite các commit đã push/shared.

## Phase 1 — Reproduce đúng lỗi

1. Start app bằng normal documented command dẫn đến `localhost:3000`.
2. Ghi network/API base URL.
3. Bấm Dashboard CTA thật.
4. Xác nhận V1 popup xuất hiện.
5. Capture capability response.
6. Xác nhận authenticated V2 form có/không có processing controls.
7. Viết failing regression test trước hoặc đồng thời với fix.

## Phase 2 — Entry point/capability cutover

1. Gom tất cả CTA vào resolver chung.
2. Sửa Dashboard authenticated outcome.
3. Bảo đảm normal local stack nhận đúng V2 flags.
4. Error + Retry; no fallback.
5. Test ON/OFF/error/loading.
6. Real-browser bấm từ Dashboard.

## Phase 3 — Per-campus frontend model

1. Loại bỏ global ownership của per-campus fields trong V2 form.
2. Đưa fields vào `campusVisits[]`.
3. Dùng shared `CampusVisitCard`.
4. Implement deep copy.
5. Implement nested validation/error focus.
6. Review screen per campus.
7. Mapper gửi resolved snapshots.
8. Unit/component tests.

## Phase 4 — Authenticated processing UI

1. Lấy current actor role/subRole/campus từ trusted auth profile.
2. Render processing panel theo role/scope.
3. Wire host candidate API.
4. Clear stale state khi đổi mode.
5. Sibling campus read-only processing.
6. Tests cho role matrix.

## Phase 5 — Backend processing parity

1. Xác nhận DTO field `processing`.
2. Centralize mapping/authorization.
3. Reuse create aggregate service.
4. Derive decision/host fields.
5. Transaction/audit/revision/idempotency.
6. Negative tests và cross-campus tests.
7. Không thêm schema nếu không cần.

## Phase 6 — Full verification

1. Unit tests backend.
2. Architecture tests.
3. Relevant integration tests.
4. C1/C2 regression group nếu liên quan vẫn chạy xanh.
5. Frontend TypeScript/lint/unit/build.
6. Real Chromium từ actual CTA.
7. DB assertions.
8. Feature flag matrix.
9. Safety/fingerprint evidence.

## Phase 7 — Documentation và commit

1. Cập nhật guide/report cho đúng thực tế.
2. Thu hồi claim cũ nếu chưa đạt DoD.
3. Gom code + tests + docs theo functional slice.
4. Verify commit metadata.
5. Không push.

---

# 13. Automated test matrix bắt buộc

## 13.1 Entry resolver

| ID | Case | Expected |
|---|---|---|
| E01 | Public, flags ON | public V2 route |
| E02 | Authenticated Dashboard, flags ON | authenticated V2 route |
| E03 | Backend explicit OFF | V1 behavior |
| E04 | capability 500 | error + Retry, no V1 |
| E05 | network error | error + Retry, no V1 |
| E06 | malformed response | fail closed |
| E07 | double click while loading | one navigation/open action |
| E08 | retry succeeds | navigates V2 |
| E09 | every CTA | same resolver outcome |

## 13.2 Per-campus frontend state

| ID | Case | Expected |
|---|---|---|
| F01 | Add HN, fill all fields | HN snapshot retained |
| F02 | Add HCM | HN unchanged |
| F03 | Copy HN → HCM | values equal, object references independent |
| F04 | Edit HCM nested contact | HN contact unchanged |
| F05 | Add HCM member | HN members unchanged |
| F06 | Remove HN | HCM data remains HCM |
| F07 | Reorder/switch tab | no data/error swap |
| F08 | Duplicate campus | validation error |
| F09 | 29m59s | rejected |
| F10 | 30m00s | accepted |
| F11 | OTHER without detail | field error in correct campus |
| F12 | invalid field in second campus | focus second campus/card |
| F13 | same values | payload contains two full snapshots |
| F14 | mixed values | payload preserves distinct snapshots |

## 13.3 Processing frontend

| ID | Actor/campus | Expected options |
|---|---|---|
| P01 | Public | none |
| P02 | Visitor authenticated if allowed create | no internal processing unless code has explicit rule |
| P03 | regular Staff, own campus | self-host / ask Leader |
| P04 | regular Staff, other campus | read-only routed notice |
| P05 | Staff Leader, own campus | self / assign / defer |
| P06 | Staff Leader, other campus | read-only routed notice |
| P07 | Leader chooses assign | eligible candidate required |
| P08 | change assign → defer | stale host ID cleared |
| P09 | change assign → self | selected Staff ID cleared |
| P10 | candidate API error | clear error/retry; cannot submit invalid assignment |

## 13.4 Backend role and authorization

| ID | Case | Expected |
|---|---|---|
| B01 | regular Staff self-host own request/own campus | success |
| B02 | regular Staff self-host other campus | forbidden |
| B03 | regular Staff self-host request of another user | forbidden |
| B04 | inactive Staff self-host | forbidden |
| B05 | Staff asks Leader | pending/null decision/host |
| B06 | Leader self-host own campus | success |
| B07 | Leader self-host other campus | forbidden |
| B08 | Leader assigns eligible same-campus Staff | success |
| B09 | Leader assigns other-campus Staff | forbidden |
| B10 | Leader assigns inactive Staff | forbidden |
| B11 | Leader assigns another Leader when unsupported | forbidden |
| B12 | Leader defers | pending/null decision/host |
| B13 | public sends forged processing | rejected/ignored exactly per fail-closed contract |
| B14 | client sends forged decidedBy/source | binding rejected or fields ignored and server derives |
| B15 | multi-campus processing differs | stored independently |
| B16 | one invalid campus processing | full transaction rollback |
| B17 | duplicate submission retry | one aggregate only |
| B18 | candidate disabled during race | fail/rollback |

## 13.5 Persistence and mixed semantics

| ID | Case | Expected |
|---|---|---|
| D01 | one campus | 1 parent/1 instance/1 detail |
| D02 | two campuses same form | 1/2/2, mixed=0 |
| D03 | two campuses different form | 1/2/2, mixed=1 |
| D04 | schedule only differs | mixed=0 |
| D05 | operational contact differs | mixed=1 |
| D06 | member set differs | mixed=1 |
| D07 | Staff self-host HN + pending HCM | HN decision only, HCM null |
| D08 | Leader assigns HN + pending HCM | no host leak to HCM |
| D09 | compatibility projection | deterministic, not canonical read source |
| D10 | audit/revision | baseline rows and actor/source correct |

## 13.6 Compatibility

| ID | Case | Expected |
|---|---|---|
| C01 | read/write V2 ON | V2 create |
| C02 | both OFF | V1 byte-compatible behavior |
| C03 | write ON/read OFF | reject with current stable error |
| C04 | existing V1 request | old read/edit behavior unchanged |
| C05 | V2 mixed request on flat legacy read | current guarded behavior, no silent projection |

---

# 14. Real-browser verification bắt buộc

Automated API calls không thay thế browser evidence.

Phải chạy Chromium/Playwright hoặc browser harness tương đương qua DOM thật:

1. Mở `localhost:3000` bằng standard local startup.
2. Login Staff.
3. Bấm Dashboard “Tạo đoàn khách”.
4. Chứng minh V1 popup không xuất hiện khi V2 ON.
5. Tạo HN + HCM.
6. Điền full HN data.
7. Copy/thêm HCM và sửa thành data khác.
8. Chuyển qua lại, chứng minh state độc lập.
9. Chọn processing ở own campus.
10. Submit qua UI.
11. Kiểm tra API success.
12. Query review DB để đối chiếu exact rows.
13. Lặp lại với Staff Leader cho self/assign/defer.
14. Thử tampering/cross-campus negative case.

Không được tính là PASS nếu:

- nhập trực tiếp route V2;
- chỉ nhìn page HTTP 200;
- API call thực tế bị CORS/401/500;
- browser request được tạo nhưng response thất bại;
- DB assertion không khớp UI;
- test fixture dùng format mà UI thật không thể tạo.

---

# 15. Database evidence cần xuất

Trên `pems_review_v2`, sau mỗi case quan trọng phải query và báo cáo:

```text
visit_requests
  visit_request_id
  request_code
  form_schema_version
  visit_scope
  has_mixed_campus_details
  registrant_user_id
  created_source
  status

visit_request_campuses
  visit_instance_id
  campus_id
  status
  coordinator_user_id
  current_host_user_id
  host_assigned_by
  decided_by
  decision_actor_role
  decision_source

visit_instance_form_details
  all canonical form fields
  form_revision
  approval_revision

member/link tables
  campus ownership and independent IDs

audit/revision tables
  actor/source/target/correlation
```

Không dùng `sent_emails.status = SENT` làm bằng chứng email đã thực sự ra ngoài. Nhiệm vụ này không yêu cầu gửi email thật.

---

# 16. Safety bắt buộc

## 16.1 Database

Chỉ được mutation:

- `pems_review_v2`;
- bằng account hạn chế `pems_review`;
- hoặc disposable database được chủ dự án cho phép rõ ràng.

Tuyệt đối không mutation:

- `pems_db`;
- `pems_test`;
- `pems_pr3_test`;
- database không nằm trong exact allowlist.

Không chạy raw master dump có `CREATE DATABASE`, `DROP DATABASE`, `USE pems_db` trực tiếp. Dùng safe-import harness đã có và assert `DATABASE()` trước/sau.

## 16.2 Outbound

Không gửi email thật hoặc gọi integration bên ngoài trong automated/review testing.

Phải bảo đảm test/review process không phát sinh:

- SMTP;
- Google Drive;
- OCR/Document AI;
- Translate;
- Turnstile/FeID hoặc HTTP integrations khác.

Không sửa hoặc xóa credential production để đạt mục tiêu. Dùng process-local/test configuration theo harness hiện hành.

## 16.3 Source control

- Preserve unrelated changes.
- Không `git reset --hard`.
- Không checkout/restore file của người dùng.
- Không rewrite shared history.
- Không push/merge/deploy.

---

# 17. Phạm vi ngoài nhiệm vụ

Không làm các phần sau trừ khi chúng trực tiếp bị regression bởi code vừa sửa:

- photo upload;
- student contribution;
- invitation/accept/decline sau khi host đã được gán;
- department task assignment;
- Google SSO claim accept;
- OCR/Translate/Drive;
- Gallery;
- email templates;
- report/invoice migration;
- R6 full census;
- F1/F5/F7;
- Phase I contract-drop;
- drop legacy fields.

Lưu ý: **processing choice ngay lúc authenticated create vẫn nằm trong scope**, dù từ “assign” xuất hiện trong UI. Nó khác với participant invitation hoặc assignment journey sau duyệt.

---

# 18. Commit policy

Không chia commit theo từng file.

Ưu tiên tối đa 2–3 semantic commits:

## Commit 1 — Entry cutover + per-campus frontend

Gom:

- capability resolver/entry points;
- Dashboard route;
- shared V2 form state;
- CampusVisitCard;
- deep-copy/validation;
- frontend tests;
- guide liên quan.

Ví dụ message:

```text
fix(visit): use per-campus v2 form from all create entry points
```

## Commit 2 — Authenticated processing parity

Gom:

- DTO/validator;
- processing mapper/service;
- authorization;
- persistence/audit;
- backend/integration tests;
- SQL sync nếu thật sự cần;
- docs liên quan.

Ví dụ:

```text
fix(visit): restore authenticated per-campus host decisions on create
```

Không tạo docs-only/test-count-only/fixup-only commit nếu có thể amend/gom vào functional slice chưa push.

Commit metadata:

- dùng `Tcanh12 <canhnvthe186121@fpt.edu.vn>` nếu local repository hiện xác nhận đúng identity này;
- không tự đổi global git config;
- không có `Claude`, `AI`, `Generated by`, `Co-Authored-By` hoặc attribution tương tự;
- verify author/committer/message sau commit.

---

# 19. Quality gates

Agent phải khám phá exact commands từ solution/package scripts tại HEAD, không bịa command.

Tối thiểu chạy:

- backend build;
- backend UnitTests;
- ArchitectureTests;
- relevant IntegrationTests trên DB được phép;
- existing C1/C2 regression group;
- SQL safety guard tests nếu SQL/harness bị sửa;
- TypeScript check;
- frontend lint nếu configured;
- Vitest/component tests;
- Vite production build;
- targeted real-browser E2E;
- database assertions.

Báo cáo exact:

- discovered;
- passed;
- failed;
- skipped/not run;
- exit code;
- local hay CI.

Không ghi “all tests pass” nếu chỉ chạy targeted subset.

## 19.1 Regression proof

Đối với các behavior quan trọng, chứng minh test bắt lỗi bằng một trong các cách an toàn:

- chạy test trên parent/pre-fix commit;
- tạm revert patch trong working tree rồi chạy targeted test, sau đó phục hồi patch an toàn;
- mutation test nhỏ không commit.

Không phá hoặc rewrite commit để tạo bằng chứng.

---

# 20. Definition of Done

Chỉ được kết luận `AUTHENTICATED MULTI-CAMPUS V2 CREATE READY` khi đủ tất cả:

- [ ] Standard app tại `localhost:3000` được kiểm chứng, không chỉ review `:5273`.
- [ ] Bấm CTA thật từ Dashboard mở authenticated V2 khi flags ON.
- [ ] V1 popup không render/open khi flags ON.
- [ ] Explicit flags OFF vẫn cho V1 theo rollout contract.
- [ ] Capability error không fallback V1.
- [ ] Public và authenticated dùng shared per-campus form core.
- [ ] Mỗi campus có full independent canonical detail.
- [ ] Deep-copy không shared nested state.
- [ ] Members và operational contacts độc lập.
- [ ] Staff own-campus self-host hoạt động.
- [ ] Staff ask-Leader tạo pending instance đúng.
- [ ] Staff Leader self-host hoạt động.
- [ ] Staff Leader assign same-campus eligible Staff hoạt động.
- [ ] Staff Leader defer giữ pending/null decision/host.
- [ ] Cross-campus tampering bị chặn backend.
- [ ] Multi-campus processing không leak sang sibling.
- [ ] DB chứng minh parent/instances/details/decisions đúng.
- [ ] `has_mixed_campus_details` đúng cho uniform/mixed.
- [ ] Transaction/idempotency/audit/revision đúng.
- [ ] V1 compatibility không regression.
- [ ] Automated quality gates xanh theo phạm vi khai báo.
- [ ] Real-browser journeys chạy qua DOM thật.
- [ ] Protected DB fingerprints không đổi.
- [ ] Không có outbound dispatch thật.
- [ ] Commits được gom semantic, không AI attribution.
- [ ] Không push/merge/deploy.

Nếu thiếu bất kỳ mục nào, kết luận phải là:

```text
IN PROGRESS — <exact blocker hoặc phần chưa kiểm chứng>
```

Không dùng lại `V2 EXPERIENCE READY` chỉ vì direct V2 route và API create chạy được.

---

# 21. Deliverables cuối session

Báo cáo theo đúng cấu trúc:

1. **Git:** start HEAD, end HEAD, remote, merge-base, ahead/behind, divergence.
2. **Audit commits:** commit nào đã có, behavior nào thật sự được code/test.
3. **Root cause:** vì sao `localhost:3000` mở V1; vì sao processing UI thiếu.
4. **Entry-point census:** mọi CTA và outcome sau sửa.
5. **Feature flags:** exact config source và response ON/OFF/error.
6. **Files changed:** nhóm Frontend/Backend/SQL/Tests/Docs.
7. **Data ownership:** request-level vs per-campus mapping cuối.
8. **Processing matrix:** role × campus scope × action × persisted result.
9. **Frontend evidence:** deep-copy/state/validation/role UI.
10. **Backend evidence:** authorization/mapping/transaction/idempotency.
11. **SQL evidence:** schema/trigger/EF drift; nêu rõ nếu không cần SQL change.
12. **Browser journeys:** từng journey PASS/FAIL/NOT RUN với evidence.
13. **DB evidence:** request IDs và assertions cho uniform/mixed/processing.
14. **Negative tests:** cross-campus/forged actor/candidate/inactive/rollback.
15. **Compatibility:** flags OFF và V1 regression.
16. **Quality gates:** exact counts và exit codes.
17. **Safety ledger:** databases/processes/outbound touched.
18. **Open business decisions:** chỉ những điểm thật sự chưa có nguồn authoritative.
19. **Git status:** exact output.
20. **Commits:** hash, message, author; xác nhận không AI markers.
21. **No push/merge/deploy:** xác nhận rõ.
22. **Final status:** READY hoặc IN PROGRESS theo Definition of Done.

Không dùng mô tả chung chung như “đã tối ưu”, “đã kiểm tra kỹ”, “full pass” nếu không kèm evidence có thể đối chiếu.

---

# 22. Lệnh bắt đầu ngay cho agent

Thực hiện liên tục theo thứ tự sau:

1. Verify git/branch/HEAD/status và preserve changes hiện có.
2. Đọc source-of-truth docs, SQL, entity, DTO, create handlers và tests.
3. Reproduce bằng cách bấm Dashboard CTA trên `localhost:3000`.
4. Chứng minh capability/config mismatch bằng network + endpoint evidence.
5. Lập exact field map request-level/per-campus và exact processing constants.
6. Viết failing tests cho Dashboard entry, per-campus independence và role processing.
7. Sửa entry resolver/normal local V2 configuration.
8. Hoàn thiện shared V2 form với full per-campus state.
9. Khôi phục authenticated processing UI.
10. Hoàn thiện backend authorization/persistence nếu field đang bị ignore/chưa wire.
11. Chỉ sửa SQL khi drift được chứng minh.
12. Chạy automated matrix và real-browser journeys.
13. Query `pems_review_v2` để đối chiếu.
14. Cập nhật guide/report trung thực.
15. Gom 1–3 semantic commits, verify metadata.
16. Không push; trả deliverables mục 21.

Không dừng sau khi chỉ sửa route. Không dừng sau khi chỉ làm UI. Không dừng sau khi API trả 200. Kết thúc khi toàn bộ entry → form state → payload → authorization → transaction → database → readback đã được kiểm chứng, hoặc khi có hard blocker thật sự được mô tả chính xác.
