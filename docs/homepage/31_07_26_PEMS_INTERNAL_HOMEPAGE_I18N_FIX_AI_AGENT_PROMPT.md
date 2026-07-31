# PEMS INTERNAL HOMEPAGE I18N FIX — AI AGENT IMPLEMENTATION PROMPT

> **Repository:** `quangthoai04/PEMS`  
> **Target branch:** `Dev`  
> **Scope:** Frontend only — Internal Homepage internationalization  
> **Languages:** Vietnamese (`vi`) and English (`en`)  
> **Affected internal role groups:** 7  
> **Do not change backend, database, permissions, routes, business rules, or the Visitor homepage.**

---

# 1. MỤC TIÊU

Hiện tại, khi người dùng đăng nhập bằng các tài khoản nội bộ và chuyển ngôn ngữ trên Header từ tiếng Việt sang tiếng Anh, hầu hết các trang đều được dịch đúng.

Tuy nhiên, riêng trang **Home dành cho internal users** vẫn còn một số khu vực hiển thị tiếng Việt do các chuỗi đang bị hard-code trong component frontend.

Bốn khu vực chưa được dịch đầy đủ gồm:

1. Khu vực đầu trang — Welcome Hero.
2. Khu vực **Truy cập nhanh**.
3. Khu vực **Hướng dẫn quy trình**.
4. Khu vực cuối trang — **Sẵn sàng tiếp tục công việc?**

Tài khoản `VISITOR` đã dịch đầy đủ và **không thuộc phạm vi sửa**.

Mục tiêu của task là:

- Tất cả nội dung tĩnh trong 4 khu vực trên phải dùng hệ thống i18next hiện tại.
- Khi chuyển `VI ↔ EN`, nội dung phải đổi ngay lập tức, không reload trang.
- Phải áp dụng đầy đủ cho 7 nhóm role nội bộ.
- Không được làm thay đổi route, permission, role scope, card visibility hoặc nghiệp vụ hiện có.

---

# 2. 7 NHÓM ROLE PHẢI HỖ TRỢ

Phải kiểm tra đầy đủ các tài khoản sau:

| STT | `role_code` | `sub_role` | Homepage bucket / effective display role |
|---:|---|---|---|
| 1 | `ADMIN` | `NULL` / `NONE` | `ADMIN` |
| 2 | `HO` | `NULL` / `NONE` | `HO` |
| 3 | `STAFF` | `LEADER` | `STAFF_LEADER` |
| 4 | `STAFF` | `STAFF` | `STAFF` |
| 5 | `DEPARTMENT` | `LEADER` | `DEPT_LEADER` / `DEPARTMENT_LEADER` |
| 6 | `DEPARTMENT` | `STAFF` | `DEPT_STAFF` / `DEPARTMENT_STAFF` |
| 7 | `STUDENT` | `NULL` / `NONE` | `STUDENT` |

Lưu ý:

- Runtime role trong hệ thống vẫn là `role_code + sub_role`.
- Không được tự thêm role mới vào backend hoặc database.
- Các tên như `STAFF_LEADER`, `DEPT_LEADER` chỉ là bucket hoặc display key phía frontend nếu code hiện tại đang dùng như vậy.
- Phải giữ nguyên helper resolve role hiện tại; chỉ tái sử dụng nhất quán.

---

# 3. PHẠM VI FILE CẦN RÀ SOÁT VÀ SỬA

Tối thiểu phải kiểm tra các file sau:

```text
frontend/pems-react/src/pages/InternalHomePage.tsx

frontend/pems-react/src/components/home/internal/WelcomeHero.tsx
frontend/pems-react/src/components/home/internal/QuickAccessSection.tsx
frontend/pems-react/src/components/home/internal/GuideStepsSection.tsx
frontend/pems-react/src/components/home/internal/InternalFinalCta.tsx

frontend/pems-react/src/shared/i18n/locales/vi/home.json
frontend/pems-react/src/shared/i18n/locales/en/home.json
```

Ngoài ra phải tìm các file hỗ trợ liên quan:

```text
frontend/pems-react/src/components/home/internal/*
frontend/pems-react/src/pages/HomePage.tsx
frontend/pems-react/src/shared/i18n/config.ts
frontend/pems-react/src/shared/auth/resolveEffectiveRole.ts
frontend/pems-react/src/**/__tests__/*
frontend/pems-react/tests/*
```

Không được giả định tên file hoặc cấu trúc giống hoàn toàn với tài liệu này. Trước khi sửa, cần dùng search trong repository để xác nhận source thật.

---

# 4. NHỮNG KHU VỰC CẦN FIX

## 4.1. Welcome Hero

Khu vực này hiện hiển thị những nội dung như:

```text
Cổng thông tin nội bộ PEMS
Xin chào, Head Office Coordinator
Head Office
FPT University Hà Nội
```

Các chuỗi tĩnh như:

```text
Cổng thông tin nội bộ PEMS
Xin chào, {{name}}
Quản trị viên
Điều phối viên Head Office
Trưởng phòng Hợp tác Quốc tế
Nhân viên Phòng Hợp tác Quốc tế
Trưởng phòng ban
Nhân viên phòng ban
Sinh viên
```

phải được đưa vào i18n resource.

Các dữ liệu động sau phải giữ nguyên:

```text
user.fullName
user.campusName
user.departmentName
```

Không dịch tự động:

- Tên người dùng.
- Tên campus.
- Tên department.
- Dữ liệu được backend hoặc database trả về.

Chỉ dịch label, title, greeting và role display name.

---

## 4.2. Quick Access

Khu vực này gồm tiêu đề **Truy cập nhanh** và các card theo role, ví dụ với HO:

```text
HO Dashboard
Theo dõi visit và hoạt động theo scope

Quản lý tiếp khách
Theo dõi yêu cầu tham quan liên cơ sở

Quản lý tin tức
Duyệt và quản lý tin tức

Quản lý FAQ
Quản lý câu hỏi thường gặp

Quản lý Campus
Quản lý danh sách campus
```

Phải dịch đầy đủ:

- Section title.
- Card label.
- Card description.
- Tooltip hoặc aria-label nếu có.
- Empty state hoặc supporting text nếu thuộc component này.

Không được thay đổi:

- Route.
- Icon.
- Thứ tự card.
- Card visibility.
- Role condition.
- Permission logic.
- onClick behavior.
- Navigation target.
- Styling.
- Responsive layout.

Phải xử lý đủ card của cả 7 role, không chỉ role HO.

---

## 4.3. Process Guide

Khu vực này gồm:

```text
Hướng dẫn quy trình
```

và danh sách bước theo từng role.

Ví dụ với HO:

```text
1. Theo dõi các yêu cầu tham quan liên cơ sở trong phạm vi phụ trách.
2. Quản lý tin tức và FAQ hiển thị công khai cho đối tác.
3. Cập nhật thông tin campus khi có thay đổi.
4. Giám sát tiến độ tiếp đón qua Dashboard.
```

Phải dịch:

- Section title.
- Tất cả step text.
- Supporting text nếu có.

Không được thay đổi:

- Số lượng bước.
- Thứ tự bước.
- Nội dung nghiệp vụ.
- Role mapping.
- Number badge.
- Layout.
- Style.
- Spacing.

---

## 4.4. Internal Final CTA

Khu vực cuối trang hiện có:

```text
Sẵn sàng tiếp tục công việc?
Vào Dashboard để xử lý các nhiệm vụ và yêu cầu đang chờ bạn.
Vào Dashboard
```

Bản tiếng Anh đề xuất:

```text
Ready to continue working?
Go to the Dashboard to handle your pending tasks and requests.
Go to Dashboard
```

Không được thay đổi:

- Route Dashboard.
- Button icon.
- Button style.
- Background.
- Footer placement.
- Responsive behavior.

---

# 5. CẤU TRÚC I18N ĐỀ XUẤT

Trong namespace `home`, nên tạo nhóm riêng:

```json
{
  "internal": {
    "hero": {},
    "roleLabels": {},
    "quickAccess": {},
    "guide": {},
    "cta": {}
  }
}
```

Không dùng câu tiếng Việt làm key.

Không dùng key rải rác thiếu tổ chức.

Không tạo nhiều namespace mới nếu namespace `home` đang được dùng thống nhất.

---

# 6. RESOURCE TIẾNG VIỆT ĐỀ XUẤT

File:

```text
frontend/pems-react/src/shared/i18n/locales/vi/home.json
```

Mẫu cấu trúc:

```json
{
  "internal": {
    "hero": {
      "portalBadge": "Cổng thông tin nội bộ PEMS",
      "greeting": "Xin chào, {{name}}"
    },
    "roleLabels": {
      "ADMIN": "Quản trị viên",
      "HO": "Điều phối viên Head Office",
      "STAFF_LEADER": "Trưởng phòng Hợp tác Quốc tế",
      "STAFF": "Nhân viên Phòng Hợp tác Quốc tế",
      "DEPARTMENT_LEADER": "Trưởng phòng ban",
      "DEPARTMENT_STAFF": "Nhân viên phòng ban",
      "STUDENT": "Sinh viên"
    },
    "quickAccess": {
      "title": "Truy cập nhanh",
      "ADMIN": {},
      "HO": {},
      "STAFF_LEADER": {},
      "STAFF": {},
      "DEPT_LEADER": {},
      "DEPT_STAFF": {},
      "STUDENT": {}
    },
    "guide": {
      "title": "Hướng dẫn quy trình",
      "roles": {
        "ADMIN": {},
        "HO": {},
        "STAFF_LEADER": {},
        "STAFF": {},
        "DEPT_LEADER": {},
        "DEPT_STAFF": {},
        "STUDENT": {}
      }
    },
    "cta": {
      "title": "Sẵn sàng tiếp tục công việc?",
      "description": "Vào Dashboard để xử lý các nhiệm vụ và yêu cầu đang chờ bạn.",
      "button": "Vào Dashboard"
    }
  }
}
```

Không được xóa hoặc đổi cấu trúc các key public homepage hiện có.

---

# 7. RESOURCE TIẾNG ANH ĐỀ XUẤT

File:

```text
frontend/pems-react/src/shared/i18n/locales/en/home.json
```

Mẫu cấu trúc:

```json
{
  "internal": {
    "hero": {
      "portalBadge": "PEMS Internal Portal",
      "greeting": "Welcome, {{name}}"
    },
    "roleLabels": {
      "ADMIN": "Administrator",
      "HO": "Head Office Coordinator",
      "STAFF_LEADER": "International Relations Staff Leader",
      "STAFF": "International Relations Staff",
      "DEPARTMENT_LEADER": "Department Leader",
      "DEPARTMENT_STAFF": "Department Staff",
      "STUDENT": "Student"
    },
    "quickAccess": {
      "title": "Quick Access",
      "ADMIN": {},
      "HO": {},
      "STAFF_LEADER": {},
      "STAFF": {},
      "DEPT_LEADER": {},
      "DEPT_STAFF": {},
      "STUDENT": {}
    },
    "guide": {
      "title": "Process Guide",
      "roles": {
        "ADMIN": {},
        "HO": {},
        "STAFF_LEADER": {},
        "STAFF": {},
        "DEPT_LEADER": {},
        "DEPT_STAFF": {},
        "STUDENT": {}
      }
    },
    "cta": {
      "title": "Ready to continue working?",
      "description": "Go to the Dashboard to handle your pending tasks and requests.",
      "button": "Go to Dashboard"
    }
  }
}
```

Mọi key mới trong `vi/home.json` phải có key tương ứng trong `en/home.json`.

---

# 8. CÁCH SỬA `WelcomeHero.tsx`

Thêm:

```tsx
import { useTranslation } from 'react-i18next';
```

Trong component:

```tsx
const { t } = useTranslation('home');
```

Thay chuỗi hard-code:

```tsx
{t('internal.hero.portalBadge')}
```

Greeting:

```tsx
{t('internal.hero.greeting', {
  name: user.fullName,
})}
```

Role label phải resolve từ role bucket hiện tại.

Ví dụ:

```ts
const roleLabelKeyByBucket = {
  ADMIN: 'internal.roleLabels.ADMIN',
  HO: 'internal.roleLabels.HO',
  STAFF_LEADER: 'internal.roleLabels.STAFF_LEADER',
  STAFF: 'internal.roleLabels.STAFF',
  DEPT_LEADER: 'internal.roleLabels.DEPARTMENT_LEADER',
  DEPT_STAFF: 'internal.roleLabels.DEPARTMENT_STAFF',
  STUDENT: 'internal.roleLabels.STUDENT',
} as const;
```

Render:

```tsx
const roleLabelKey = roleLabelKeyByBucket[roleBucket];

const roleLabel = roleLabelKey
  ? t(roleLabelKey)
  : user.roleName ?? '';
```

Không được dùng `user.roleName` làm nguồn chính nếu đây là chuỗi cố định từ backend.

---

# 9. CÁCH SỬA `QuickAccessSection.tsx`

Nếu cấu hình hiện tại đang lưu trực tiếp text:

```ts
{
  label: 'Quản lý tiếp khách',
  description: 'Theo dõi yêu cầu tham quan liên cơ sở',
  route: '/dashboard/visit'
}
```

thì phải đổi thành key:

```ts
{
  labelKey: 'internal.quickAccess.HO.visitManagement.label',
  descriptionKey: 'internal.quickAccess.HO.visitManagement.description',
  route: '/dashboard/visit'
}
```

Render:

```tsx
<h3>{t(item.labelKey)}</h3>
<p>{t(item.descriptionKey)}</p>
```

Giữ nguyên toàn bộ metadata ngoài text:

```ts
route
icon
allowedRoles
visibility
sort/order
onClick
```

Không được tạo 2 bộ cấu hình riêng cho VI và EN.

Không dùng:

```tsx
i18n.language === 'en' ? '...' : '...'
```

---

# 10. CÁCH SỬA `GuideStepsSection.tsx`

Không để chuỗi tiếng Việt trực tiếp trong constant.

Sai:

```ts
const GUIDE_STEPS = {
  HO: [
    'Theo dõi các yêu cầu tham quan liên cơ sở...',
    'Quản lý tin tức và FAQ...'
  ]
};
```

Đúng:

```ts
const GUIDE_STEP_KEYS = {
  HO: [
    'internal.guide.roles.HO.step1',
    'internal.guide.roles.HO.step2',
    'internal.guide.roles.HO.step3',
    'internal.guide.roles.HO.step4'
  ]
};
```

Render:

```tsx
{stepKeys.map((stepKey, index) => (
  <GuideStep
    key={stepKey}
    number={index + 1}
    text={t(stepKey)}
  />
))}
```

Phải tạo key đầy đủ cho 7 role.

---

# 11. CÁCH SỬA `InternalFinalCta.tsx`

Thêm:

```tsx
const { t } = useTranslation('home');
```

Thay:

```tsx
<h2>{t('internal.cta.title')}</h2>
<p>{t('internal.cta.description')}</p>
<span>{t('internal.cta.button')}</span>
```

Không sửa route hoặc style.

---

# 12. KIỂM TRA `InternalHomePage.tsx`

Phải đảm bảo tất cả component nhận cùng một role bucket:

```tsx
<WelcomeHero roleBucket={roleBucket} />
<QuickAccessSection roleBucket={roleBucket} />
<GuideStepsSection roleBucket={roleBucket} />
```

Không để mỗi component tự resolve role bằng logic khác nhau.

Không gọi `i18n.t()` ở module scope.

Sai:

```ts
const cards = [
  {
    label: i18n.t('internal.quickAccess...')
  }
];
```

Đúng:

```ts
const cards = [
  {
    labelKey: 'internal.quickAccess...'
  }
];
```

sau đó gọi `t()` trong component.

---

# 13. FALLBACK VÀ FAIL-SAFE

Không để UI hiện raw key:

```text
internal.quickAccess.HO.visitManagement.label
```

Có thể dùng `defaultValue` cho production fallback:

```tsx
t(key, { defaultValue: fallback })
```

Tuy nhiên:

- Không dùng fallback để che việc thiếu key.
- Test phải fail nếu thiếu key.
- Cả VI và EN phải có cùng cấu trúc key.

---

# 14. TEST TỰ ĐỘNG BẮT BUỘC

## 14.1. Welcome Hero

Test cho đủ 7 role, mỗi role 2 ngôn ngữ:

```text
ADMIN — VI / EN
HO — VI / EN
STAFF_LEADER — VI / EN
STAFF — VI / EN
DEPT_LEADER — VI / EN
DEPT_STAFF — VI / EN
STUDENT — VI / EN
```

Assertion:

- Badge đổi đúng.
- Greeting đổi đúng.
- Role label đổi đúng.
- Tên người dùng giữ nguyên.
- Campus/department giữ nguyên.
- Không xuất hiện raw translation key.

---

## 14.2. Quick Access

Với từng role:

- Số card VI bằng số card EN.
- Route từng card không đổi.
- Icon không đổi.
- Label đổi đúng.
- Description đổi đúng.
- Không lộ card của role khác.
- Không có chuỗi tiếng Việt ở chế độ EN.

---

## 14.3. Process Guide

Với từng role:

- Title đổi từ `Hướng dẫn quy trình` sang `Process Guide`.
- Số bước không đổi.
- Thứ tự không đổi.
- Step text đổi đúng.
- Không hiện raw key.

---

## 14.4. Final CTA

Kiểm tra:

```text
VI:
Sẵn sàng tiếp tục công việc?
Vào Dashboard để xử lý các nhiệm vụ và yêu cầu đang chờ bạn.
Vào Dashboard

EN:
Ready to continue working?
Go to the Dashboard to handle your pending tasks and requests.
Go to Dashboard
```

Nút vẫn điều hướng đúng.

---

## 14.5. Language Switch Runtime Test

Phải có ít nhất một test mô phỏng:

1. Render internal homepage với `vi`.
2. Assert text tiếng Việt.
3. Gọi `i18n.changeLanguage('en')`.
4. Không unmount component.
5. Assert cả 4 khu vực đổi sang tiếng Anh.

Mục tiêu là chứng minh component subscribe đúng với i18next.

---

## 14.6. Translation Key Parity Test

Nên thêm test hoặc script đệ quy để bảo đảm:

- Mọi key trong `vi/home.json` tồn tại trong `en/home.json`.
- Mọi key trong `en/home.json` tồn tại trong `vi/home.json`.
- Không có object/leaf mismatch.
- Không có chuỗi rỗng.
- Không có raw key bị render.

---

# 15. KIỂM THỬ THỦ CÔNG

Phải đăng nhập lần lượt bằng đủ 7 role:

```text
ADMIN
HO
STAFF / LEADER
STAFF / STAFF
DEPARTMENT / LEADER
DEPARTMENT / STAFF
STUDENT
```

Với mỗi role:

1. Đăng nhập.
2. Mở homepage `/`.
3. Chuyển `VI → EN`.
4. Kiểm tra Welcome Hero.
5. Kiểm tra Quick Access.
6. Kiểm tra Process Guide.
7. Cuộn xuống Final CTA.
8. Click một số Quick Access card quan trọng.
9. Click `Go to Dashboard`.
10. Refresh trang.
11. Kiểm tra EN vẫn được lưu.
12. Chuyển `EN → VI`.
13. Kiểm tra text quay lại tiếng Việt.

Tổng số trạng thái tối thiểu:

```text
7 role × 2 ngôn ngữ = 14 trạng thái
```

Phải kiểm tra thêm 1 tài khoản `VISITOR` để bảo đảm không regression.

---

# 16. LỆNH KIỂM TRA BẮT BUỘC

Chạy tại:

```text
frontend/pems-react
```

Các lệnh:

```bash
npm run lint
npm run build
npm run test:unit
```

Nếu test E2E liên quan homepage tồn tại và có thể chạy:

```bash
npm run test:e2e
```

Báo cáo phải ghi:

```text
Command
Exit code
Passed
Failed
Skipped
Not run / blocked reason
```

Không được chỉ ghi “all green” mà không có bằng chứng.

---

# 17. NHỮNG ĐIỀU TUYỆT ĐỐI KHÔNG ĐƯỢC LÀM

Không được:

- Sửa backend.
- Sửa database.
- Sửa role schema.
- Sửa permission.
- Sửa authorization.
- Sửa route.
- Thêm role runtime mới.
- Thêm card mới.
- Xóa card cũ.
- Đổi thứ tự card.
- Đổi action card.
- Đổi Process Guide nghiệp vụ.
- Dịch tên người dùng.
- Dịch tên campus.
- Dịch tên department.
- Dùng Google Translate API cho UI label.
- Lưu UI translation trong database.
- Tạo hai component riêng VI/EN.
- Dùng ternary ngôn ngữ hard-code rải rác.
- Reload trang để text đổi ngôn ngữ.
- Làm ảnh hưởng Visitor homepage.
- Refactor ngoài phạm vi.
- Thay đổi layout hoặc visual design nếu không cần thiết.
- Chỉ dịch title mà bỏ sót card description hoặc guide step.

---

# 18. DEFINITION OF DONE

Task chỉ hoàn thành khi đạt đủ:

1. Welcome Hero dịch đúng cho 7 role.
2. Quick Access dịch đúng toàn bộ card cho 7 role.
3. Process Guide dịch đúng toàn bộ step cho 7 role.
4. Final CTA dịch đúng.
5. `VI ↔ EN` đổi ngay không reload.
6. Role label đổi theo ngôn ngữ.
7. Tên người dùng, campus, department giữ nguyên.
8. Route/card visibility/permission không đổi.
9. Không còn chuỗi hard-code tiếng Việt trong 4 component thuộc phạm vi.
10. Không hiện raw translation key.
11. `vi/home.json` và `en/home.json` có key parity.
12. Visitor homepage không regression.
13. `npm run lint` đạt.
14. `npm run build` đạt.
15. `npm run test:unit` đạt.
16. Manual verification đủ 7 role.
17. Báo cáo cuối liệt kê chính xác file đã sửa.
18. Báo cáo cuối liệt kê test command và kết quả thực tế.

---

# 19. FORMAT BÁO CÁO CUỐI CỦA AI AGENT

AI Agent phải trả báo cáo theo mẫu:

```text
1. Preflight
- Repository:
- Branch:
- HEAD:
- Working tree:
- Files reviewed:

2. Root cause
- Components containing hard-coded strings:
- Why Visitor was unaffected:
- Role bucket mapping:

3. Files changed
- file path
- exact purpose
- important implementation notes

4. Translation resources added
- new VI keys
- new EN keys
- key parity result

5. Role coverage
- ADMIN
- HO
- STAFF/LEADER
- STAFF/STAFF
- DEPARTMENT/LEADER
- DEPARTMENT/STAFF
- STUDENT
- VISITOR regression

6. Automated test results
- npm run lint
- npm run build
- npm run test:unit
- npm run test:e2e

7. Manual verification
- role
- VI result
- EN result
- routes preserved
- issues found

8. Remaining risks
- none / exact unresolved issue

9. Final verdict
- COMPLETE / PARTIAL / BLOCKED
```

Không được tuyên bố `COMPLETE` nếu chưa chạy các gate bắt buộc hoặc chưa kiểm tra đủ 7 role.

---

# 20. PROMPT THỰC THI NGẮN GỌN CHO AI AGENT

Bạn đang làm việc trên repository `quangthoai04/PEMS`, nhánh `Dev`.

Hãy sửa lỗi i18n của Internal Homepage cho 7 nhóm role:

- ADMIN
- HO
- STAFF / LEADER
- STAFF / STAFF
- DEPARTMENT / LEADER
- DEPARTMENT / STAFF
- STUDENT

Visitor homepage đã dịch đúng và không thuộc phạm vi sửa.

Bốn khu vực cần fix:

1. Welcome Hero.
2. Quick Access.
3. Process Guide.
4. Final CTA.

Yêu cầu:

- Đọc source thật trước khi sửa.
- Dùng i18next namespace `home`.
- Chuyển toàn bộ text tĩnh sang translation keys.
- Bổ sung đầy đủ `vi/home.json` và `en/home.json`.
- Giữ nguyên route, icon, card order, visibility, permission, role logic, business flow và layout.
- Không sửa backend/database.
- Không dịch dữ liệu động như fullName, campusName, departmentName.
- Không dùng ternary ngôn ngữ hard-code.
- Không reload trang để đổi ngôn ngữ.
- Test đủ 7 role và Visitor regression.
- Chạy `npm run lint`, `npm run build`, `npm run test:unit`.
- Chỉ tuyên bố hoàn thành khi tất cả gate đạt và báo cáo đầy đủ bằng chứng.
