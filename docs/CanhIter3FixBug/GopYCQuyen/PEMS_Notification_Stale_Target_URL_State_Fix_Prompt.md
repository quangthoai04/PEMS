# PEMS — Fix Stale Notification Target / URL-State Sync

> Scope: `Canh_iter3_FixBug`  
> Mục tiêu: sửa triệt để lỗi khi click notification A rồi notification B trên cùng route `/dashboard/visit`, URL đã đổi sang target mới nhưng UI vẫn giữ dữ liệu target cũ; đồng thời xử lý các lỗi cùng họ như stale URL-derived state, async race, Back/Forward, tab/filter drift và legacy notification.

---

## Prompt triển khai

```text
@GitHub

Fix triệt để bug stale notification target trong `VisitRequestManagement`.

Bug thực tế đã reproduce:

1. Click notification Nanning → URL:
   `/dashboard/visit?visitRequestId=47028`
   → UI hiển thị Nanning đúng.

2. Không rời trang `/dashboard/visit`, click notification Shinyway → URL đổi:
   `/dashboard/visit?visitRequestId=47027`

3. Nhưng bảng vẫn hiển thị Nanning 47028.

==================================================
1. LOCK SOURCE OF TRUTH
==================================================

Làm việc trên branch:

Canh_iter3_FixBug

Trước khi sửa:
- lấy HEAD mới nhất;
- báo SHA;
- đọc lại code hiện tại;
- không dựa vào line number/snapshot cũ nếu HEAD đã thay đổi.

Không commit/push.

==================================================
2. ROOT CAUSE ĐÃ XÁC ĐỊNH
==================================================

Trong `VisitRequestManagement.tsx` hiện có pattern:

```ts
const [notificationVisitRequestId, setNotificationVisitRequestId] =
  useState(searchParams.get('visitRequestId') || '');
```

Đây là stale derived state.

`useState(...)` chỉ lấy `visitRequestId` từ URL lúc component mount.

Khi React Router điều hướng cùng route:

```text
/dashboard/visit?visitRequestId=47028
→
/dashboard/visit?visitRequestId=47027
```

component không remount.

Kết quả:

```text
searchParams = 47027
notificationVisitRequestId state = 47028
```

`loadDelegations()` tiếp tục dùng ID cũ nên UI vẫn hiển thị Nanning.

Ngoài ra current code còn có các nguy cơ cùng họ:

- `activeTab` init từ `searchParams` một lần;
- `currentPage`;
- `pageSize`;
- `sortOrder`;
- `draftFilters`;
- `appliedFilters`;
- mount-only initial load;
- stale async response có thể overwrite target mới.

==================================================
3. VIẾT REGRESSION TEST TRƯỚC KHI SỬA
==================================================

Bắt buộc thêm test tái hiện chính xác:

N-01:

```text
render VisitRequestManagement
URL target = 47028
API trả Nanning
→ Nanning visible

same component instance
navigate URL target = 47027
API trả Shinyway
→ Shinyway visible
→ Nanning NOT visible
```

Test phải chứng minh:
- same route navigation;
- component không cần remount;
- old code fail;
- fix mới pass.

==================================================
4. URL PHẢI LÀ SOURCE OF TRUTH CHO NOTIFICATION TARGET
==================================================

Không giữ:

```ts
useState(searchParams.get('visitRequestId'))
```

cho notification target.

Ưu tiên:

```ts
const notificationVisitRequestId =
  searchParams.get('visitRequestId') || '';
```

hoặc abstraction tương đương mà URL là source of truth.

Không tạo thêm mirror state nếu không thực sự cần.

Mục tiêu:

```text
URL 47028 → target 47028
URL 47027 → target 47027 ngay render kế tiếp
```

==================================================
5. LOAD PHẢI REACT VỚI URL TARGET CHANGE
==================================================

Current mount-only:

```ts
useEffect(() => {
  loadDelegations(...);
}, []);
```

không đủ.

Thiết kế lại dependency/lifecycle để khi notification target thay đổi:

```text
47028 → 47027
```

thì load đúng target mới.

Phải tránh infinite loop hoặc duplicate fetch.

Mục tiêu:

```text
target changed
→ page/reset state cần thiết
→ fetch new target
→ render new row
```

==================================================
6. KHÔNG CLIENT-FILTER 1000 ROW NẾU BACKEND ĐÃ HỖ TRỢ visitRequestId
==================================================

Current notification filter path đang có kiểu:

```text
fetch list
→ map
→ filter visitRequestId phía frontend
```

Trong khi API hiện đã hỗ trợ:

```text
visitRequestId
```

ở `getVisitRequestManagementList`.

Hãy ưu tiên:

```text
notificationVisitRequestId exists
→ send visitRequestId to backend
→ backend scope/permission filter
→ return exact row(s)
```

Không fetch 1000 records rồi lọc client nếu không cần.

Lợi ích:
- đúng target;
- không phụ thuộc page;
- không bỏ sót record;
- giảm payload;
- permission vẫn do backend quyết định.

==================================================
7. CLEAR STALE UI KHI TARGET ĐỔI
==================================================

Khi chuyển:

```text
Nanning → Shinyway
```

không được để trạng thái:

```text
URL = Shinyway
TABLE = Nanning
```

trong lúc request mới đang load.

Khi notification target đổi:
- set loading;
- clear hoặc invalidate rows cũ;
- reset total;
- render skeleton/loading state;
- sau đó render target mới.

Không để old target nhìn như đang thuộc URL mới.

==================================================
8. STALE REQUEST / RACE CONDITION PROTECTION
==================================================

Phải xử lý case:

```text
click A
request A chậm

click B
request B nhanh

B trả trước
→ UI = B

A trả sau
→ không được overwrite UI thành A
```

Dùng một trong:

A. `AbortController` nếu API layer hỗ trợ.

hoặc

B. monotonic request sequence:

```ts
const requestVersionRef = useRef(0);

const version = ++requestVersionRef.current;

const response = await ...

if (version !== requestVersionRef.current) return;
```

Chỉ latest request được phép gọi:

```text
setRows
setTotal
setSummaryStats
setListError
```

==================================================
9. EXTERNAL URL → ACTIVE TAB SYNC
==================================================

Audit:

```ts
const [activeTab, setActiveTab] = useState(defaultTab);
```

Current `defaultTab` đọc từ URL nhưng chỉ lúc mount.

Phải đảm bảo external navigation:

```text
?tab=all
→ notification
?tab=attending
```

thì UI thật sự chuyển:

```text
activeTab = attending
```

Không chỉ URL đổi.

Đặc biệt pin case:

PARTICIPATION_INVITED
→ tab=attending
→ đúng attending data/action.

==================================================
10. AUDIT URL-DERIVED STATE TRONG VisitRequestManagement
==================================================

Rà toàn bộ:

```text
visitRequestId
tab
page
pageSize
sortOrder
keyword
status
visitScope
relation
fromDate
toDate
campusId
```

Nguyên tắc:

Không để một query param vừa tồn tại trong URL vừa có một state mirror chỉ được initialize một lần.

Với mỗi field phải chọn rõ:

A. URL is source of truth

hoặc

B. local state is source of truth + explicit bidirectional sync

Không dùng hybrid không đồng bộ.

==================================================
11. FILTER CONFLICT
==================================================

Case cần hỗ trợ:

User đang có filter:

```text
status=CLOSED
keyword=abc
page=5
```

rồi click notification của request đang:

```text
WAITING_REQUEST_APPROVAL
```

Notification target phải thắng filter cũ.

Phải:
- reset page về 1;
- không để keyword/status cũ làm target biến mất;
- hoặc dùng exact `visitRequestId` backend query độc lập khỏi normal list filters.

Sau khi user bấm "Xem tất cả":
- notification target mode được xóa;
- normal filters hoạt động bình thường.

==================================================
12. BACK/FORWARD SUPPORT
==================================================

Test:

```text
A → B → Back
```

UI phải quay về A nếu URL quay về A.

```text
A → B → Back → Forward
```

UI phải quay lại B.

Không được URL một nơi, state một nơi.

==================================================
13. NO-PERMISSION / DELETED TARGET
==================================================

Case:

```text
notification A visible
→ click B
→ backend trả 0 items vì B đã bị xóa / mất quyền
```

UI phải:

- clear A;
- không tiếp tục hiển thị A;
- show đúng error:
  "Không tìm thấy đoàn được nhắc... hoặc không còn quyền"
- URL/state không bị kẹt ở A.

==================================================
14. LEGACY NOTIFICATION AUDIT
==================================================

Ảnh production hiện vẫn cho thấy URL:

```text
/dashboard/visit?visitRequestId=...
```

trong khi current Bell code có logic rewrite sang:

```text
openVisitRequestId
```

Hãy kiểm tra payload thật của đúng notification Nanning/Shinyway.

Capture:

```text
notificationId
eventKey
visitRequestId
visitInstanceId
actionType
targetUrl
metadataJson
```

Nếu:

```text
visitRequestId == null
```

nhưng targetUrl có:

```text
/dashboard/visit?visitRequestId=47027
```

thì resolver legacy phải có safe fallback:

- parse structured query param từ targetUrl;
- chuyển sang canonical notification command;
- KHÔNG parse title/message.

Nếu deployment chưa chứa latest notification resolver:
ghi rõ DEPLOYMENT_MISMATCH, không nhầm với app logic.

==================================================
15. CANONICAL NOTIFICATION TARGET CONTRACT
==================================================

Sau fix, tránh tồn tại song song quá nhiều semantics:

```text
visitRequestId
openVisitRequestId
notificationIntent
```

Hãy document rõ:

Persistent list-filter:
`visitRequestId`
= hiển thị một target trong list cho user chủ động "Xem tất cả".

One-shot navigation command:
`openVisitRequestId`
= resolve/open target rồi consume.

Nếu notification semantic hiện tại đã dùng one-shot command:
ưu tiên notification click dùng one-shot.

Không được notification cùng loại lúc thì persistent filter, lúc thì one-shot nếu không có lý do backward compatibility rõ ràng.

==================================================
16. SECOND CLICK REGRESSION PHẢI GIỮ
==================================================

Không làm hỏng fix trước:

```text
click same notification
→ open
→ close
→ click same notification again
→ open again
```

StrictMode:
→ không double-open.

==================================================
17. RAPID DIFFERENT-NOTIFICATION REGRESSION
==================================================

Test:

```text
click A
50ms later click B

A response artificially delayed 1000ms
B response 100ms

final UI MUST be B
```

Đây là test bắt buộc cho stale-response guard.

==================================================
18. REVERSE ORDER REGRESSION
==================================================

Test cả:

```text
Nanning 47028 → Shinyway 47027
```

và:

```text
Shinyway 47027 → Nanning 47028
```

Không fix phụ thuộc thứ tự.

==================================================
19. ATTENDING REGRESSION
==================================================

Test:

```text
currently tab=all
click PARTICIPATION_INVITED notification
URL tab=attending
→ activeTab attending
→ correct participant row
→ Accept/Decline visible nếu current state INVITED
```

==================================================
20. NEWS / PARTNER NON-REGRESSION
==================================================

News/Partner hiện derive notification ID trực tiếp:

```text
searchParams.get(...)
```

và fetch phụ thuộc ID.

Không làm regress behavior này.

Add/keep smoke tests:

```text
newsId A → newsId B
→ B visible

partnerId A → partnerId B
→ B visible
```

==================================================
21. REPO-WIDE AUDIT TƯƠNG TỰ
==================================================

Search frontend:

```text
useState(searchParams.get(
useState(getUrlFilters(
```

và các pattern tương đương.

Triage các page có deep-link/query navigation.

Đặc biệt:

- VisitRequestManagement
- AccountManagement
- Department task/dashboard
- PostVisitTasks
- Calendar/task deep-link
- News
- Partner
- Notifications
- Reports/detail pages nếu có query-driven record focus

Phân loại:

SAFE_KEEP
FIX_REQUIRED

Không mass-refactor page không liên quan.

==================================================
22. TEST MATRIX BẮT BUỘC
==================================================

N-01:
A → B same route, B visible.

N-02:
B → A.

N-03:
same notification second click.

N-04:
rapid A → B, A finishes last.

N-05:
Back.

N-06:
Forward.

N-07:
tab=attending external notification.

N-08:
existing filters conflict.

N-09:
target missing/no permission clears old row.

N-10:
legacy targetUrl structured-ID fallback.

N-11:
StrictMode no double command.

N-12:
normal filter change after notification does not replay old target.

==================================================
23. TEST GATES
==================================================

Sau fix chạy:

```bash
npm run lint
npm run test:unit
npm run build
```

Chạy relevant tests:

```text
VisitRequestManagementNotificationDeepLink
notification destination resolver
notification semantic
URL state sync tests
race-condition tests
```

Nếu environment cho phép:

```bash
npm run test:e2e
```

và test trực tiếp Nanning/Shinyway flow.

==================================================
24. CHROME SAFETY
==================================================

Khi chạy Playwright:

CẤM:

```text
taskkill /F /IM chrome.exe
Stop-Process -Name chrome
pkill chrome
killall chrome
```

Không được tắt Chrome cá nhân.

Chỉ cleanup process chắc chắn thuộc Playwright.

==================================================
25. BUSINESS LOGIC PRESERVATION
==================================================

Task này là URL/state/deep-link lifecycle.

Không đổi:

- API business rules;
- status transition;
- approval logic;
- permission/allowedActions;
- role semantics;
- payload mutation;
- Visit workflow.

Nếu cần sửa production logic ngoài navigation/state sync:
báo trước bằng evidence.

==================================================
26. FINAL REPORT
==================================================

Báo:

HEAD:

CONFIRMED ROOT CAUSE:

Files changed:

URL/state contract before:

URL/state contract after:

Race protection:

Legacy notification handling:

Nanning→Shinyway result:

Shinyway→Nanning result:

Back/Forward:

Attending notification:

Filter conflict:

No-permission behavior:

Repo-wide similar-pattern audit:

Tests:
- lint
- unit
- build
- relevant notification/deep-link
- E2E nếu chạy

Business logic impact:

Known remaining bugs:

NOT VERIFIED:

==================================================
27. DEFINITION OF DONE
==================================================

Không báo DONE nếu còn bất kỳ case nào:

- URL target B nhưng UI vẫn A;
- old async response có thể overwrite new target;
- external tab URL không sync activeTab;
- filter cũ làm notification target biến mất;
- missing target vẫn giữ row cũ;
- same-notification second click regression quay lại;
- legacy structured targetUrl vẫn bypass canonical flow mà không có lý do;
- Back/Forward làm URL và UI lệch nhau.

Bắt đầu ngay bằng:
1. Lock HEAD.
2. Viết Nanning→Shinyway failing regression test.
3. Fix URL-derived stale state.
4. Add stale-response guard.
5. Audit tab/filter URL sync.
6. Audit legacy payload.
7. Run full relevant verification.
```
