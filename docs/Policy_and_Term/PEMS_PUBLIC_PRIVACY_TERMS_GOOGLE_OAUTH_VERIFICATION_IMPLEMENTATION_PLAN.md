# PEMS – IMPLEMENTATION PLAN: PUBLIC PRIVACY POLICY + TERMS OF SERVICE FOR GOOGLE OAUTH VERIFICATION

## 0. Mục tiêu

Triển khai **2 trang public chính thức** cho PEMS:

- `/privacy` — **Chính sách bảo mật / Privacy Policy**
- `/terms` — **Điều khoản sử dụng / Terms of Service**

Mục tiêu chính:

1. Đáp ứng yêu cầu Google OAuth / Google Auth Platform Verification.
2. Hai trang phải public, mở được khi chưa đăng nhập.
3. Giao diện phải đồng nhất với website public hiện tại của PEMS.
4. Có đầy đủ **VI / EN**.
5. Footer hiện tại phải liên kết đúng sang 2 trang này.
6. Không làm thay đổi luồng Google Drive OAuth / Refresh Token đã triển khai.
7. Không refactor ngoài scope.
8. Ưu tiên tái sử dụng component, layout, i18n và design system hiện có.

---

# 1. Bối cảnh hiện tại

PEMS hiện đã:

- Chuyển Google OAuth app từ `Testing` sang `In production`.
- ADMIN có thể:
  - Kết nối Google Drive.
  - Kết nối lại Google Drive.
  - Google OAuth callback về backend.
  - Backend nhận Refresh Token.
  - Refresh Token được mã hóa và lưu DB.
  - Runtime Google Drive dùng token từ DB.
- Test connection Google Drive thành công.
- Upload file thật lên Google Drive thành công.

Google Auth Platform hiện yêu cầu hoàn thiện:

- Branding
- Privacy Policy URL
- Terms of Service URL
- Authorized domain
- Sau đó mới tiếp tục Verification.

Domain public chính của PEMS:

```text
https://www.pems-fpt.site
```

Hai URL mục tiêu:

```text
https://www.pems-fpt.site/privacy
https://www.pems-fpt.site/terms
```

Authorized domain:

```text
pems-fpt.site
```

---

# 2. Scope implementation

## Trong scope

### Frontend

- Tạo route `/privacy`.
- Tạo route `/terms`.
- Tạo UI public cho cả hai.
- Tái sử dụng header/footer public hiện tại.
- Tái sử dụng cơ chế chuyển VI/EN hiện tại.
- Tạo component layout dùng chung nếu hợp lý.
- Cập nhật Footer:
  - Chính sách bảo mật → `/privacy`
  - Điều khoản sử dụng → `/terms`
- Đảm bảo direct navigation / refresh tại route vẫn hoạt động trên production SPA.
- SEO/title cơ bản cho 2 trang.

### Nội dung

- Privacy Policy đầy đủ VI/EN.
- Terms of Service đầy đủ VI/EN.
- Nội dung phải phản ánh đúng PEMS.
- Privacy Policy phải mô tả rõ Google Drive / Google user data usage.

## Ngoài scope

Không thay đổi:

- Google Drive OAuth flow.
- Refresh Token logic.
- GoogleDriveStorageService.
- Backend OAuth callback.
- Database schema.
- Permission.
- Authentication.
- Role logic.
- Existing API behavior.
- Existing Google Drive scopes.
- Google Cloud Client ID / Secret.
- Railway config.
- Business flow khác.

---

# 3. Nguyên tắc implementation

1. **Không duplicate layout**
   - Nếu PublicHeader/PublicFooter đã tồn tại → dùng lại.
   - Nếu đã có public page container/layout → dùng lại.

2. **Không tạo i18n system mới**
   - Dùng cơ chế VI/EN hiện có.
   - Nếu project dùng translation keys → bổ sung keys.
   - Nếu content dài cần data structure riêng → vẫn hook vào current locale.

3. **Không hardcode style trái design system**
   - Dùng token/class đang có.
   - Giữ FPT blue/orange.
   - Responsive.

4. **Không bắt login**
   - `/privacy`
   - `/terms`
   phải public.

5. **Không redirect về login**
   - Google reviewer phải truy cập được trực tiếp.

6. **Không để footer link là `#`**
   - Phải route thật.

---

# 4. Visual design chung

Hai trang nên cùng một visual language với website public PEMS hiện tại.

## Layout

```text
Public Header
↓
Breadcrumb
↓
Legal Hero
↓
Content area
    ├─ Table of contents
    └─ Main content
↓
Contact card
↓
Public Footer
```

## Desktop

- Max width: theo container public hiện tại.
- Main content:
  - Sidebar TOC khoảng 240–280px.
  - Nội dung chính khoảng 800–900px.
- Sidebar sticky nếu phù hợp.
- Khoảng trắng thoáng, dễ đọc.

## Mobile

- 1 cột.
- TOC chuyển thành:
  - dropdown
  - accordion
  - hoặc compact section navigator.

## Màu sắc

Ưu tiên màu hiện tại của hệ thống:

```text
PEMS/FPT blue: khoảng #004c91 hoặc token hiện có
FPT orange: khoảng #f37021 hoặc token hiện có
Background: white / slate-50 / gray-50 theo UI hiện tại
```

Không tự tạo palette mới.

## Component style

- Card trắng.
- Border nhẹ.
- `rounded-xl` / `rounded-2xl` theo hệ thống.
- Shadow nhẹ hoặc không shadow.
- Không làm kiểu legal page khô, toàn chữ liên tục.

---

# 5. Component structure đề xuất

Tên file thực tế phải theo codebase hiện tại.

Có thể triển khai kiểu:

```text
PublicLegalPageLayout
├── PublicHeader
├── LegalHero
├── LegalTableOfContents
├── LegalSection
├── LegalCallout
├── LegalContactCard
└── PublicFooter
```

Pages:

```text
PrivacyPolicyPage
TermsOfServicePage
```

Nếu codebase hiện tại đã có layout phù hợp hơn thì dùng layout hiện có.

Không tạo abstraction nếu chỉ dùng 1 lần.

---

# 6. Route

Phải có:

```text
/privacy
/terms
```

Direct URL:

```text
https://www.pems-fpt.site/privacy
https://www.pems-fpt.site/terms
```

Cả hai phải:

- trả page public
- không login
- refresh trực tiếp không 404
- hoạt động trên Vercel/hosting frontend hiện tại

Nếu SPA routing hiện tại cần rewrite thì cấu hình theo pattern project hiện có.

---

# 7. Footer

Footer public hiện tại đang có:

```text
Chính sách bảo mật
Điều khoản sử dụng
FAQs
```

Cập nhật:

```text
Chính sách bảo mật → /privacy
Điều khoản sử dụng → /terms
```

English:

```text
Privacy Policy
Terms of Service
FAQs
```

Không đổi layout footer nếu không cần.

---

# 8. Privacy Policy – Header

## VI

### Title

```text
Chính sách bảo mật
```

### Subtitle

```text
Privacy Policy
```

### Description

```text
PEMS tôn trọng quyền riêng tư và cam kết xử lý dữ liệu một cách minh bạch,
an toàn và phù hợp với mục đích vận hành Hệ thống Quản lý Tiếp khách và
Hợp tác Quốc tế.
```

### Last updated

```text
Cập nhật lần cuối: 07/08/2026
```

## EN

### Title

```text
Privacy Policy
```

### Description

```text
PEMS respects your privacy and is committed to handling personal data
transparently and securely for the operation of the Visit Management
and International Cooperation System.
```

### Last updated

```text
Last updated: August 7, 2026
```

Nếu project đã có helper date locale thì dùng helper hiện có.

---

# 9. Privacy Policy – Nội dung đầy đủ

## 9.1 Giới thiệu / Introduction

### VI

PEMS là Hệ thống Quản lý Tiếp khách và Hợp tác Quốc tế được sử dụng để hỗ trợ các hoạt động đăng ký tham quan, tiếp nhận đoàn khách, điều phối lịch trình, quản lý tài liệu, hình ảnh, đối tác và các hoạt động liên quan.

Chính sách bảo mật này giải thích loại dữ liệu PEMS có thể xử lý, mục đích sử dụng dữ liệu, cách dữ liệu được bảo vệ và các quyền liên quan của người dùng.

Việc sử dụng PEMS đồng nghĩa với việc người dùng đã được cung cấp khả năng tiếp cận chính sách này trước hoặc trong quá trình sử dụng các chức năng có liên quan đến dữ liệu cá nhân.

### EN

PEMS is a Visit Management and International Cooperation System used to support visit registration, delegation reception, schedule coordination, document and media management, partner management, and related operational activities.

This Privacy Policy explains the types of data that PEMS may process, the purposes for which such data is used, how the data is protected, and the rights available to users.

Users are provided access to this Privacy Policy before or during their use of features involving personal data.

## 9.2 Dữ liệu PEMS xử lý / Information We Process

### VI

Tùy theo chức năng được sử dụng, PEMS có thể xử lý các thông tin như họ tên, email, số điện thoại nếu được cung cấp, đơn vị hoặc tổ chức, chức vụ, quốc tịch, thông tin đăng ký tham quan, lịch trình làm việc, thành viên đoàn khách, người phụ trách, tài liệu, hình ảnh, báo cáo và các thông tin phục vụ hoạt động tiếp khách.

Đối với người dùng nội bộ, hệ thống có thể xử lý thông tin tài khoản, vai trò, đơn vị công tác, lịch sử thao tác và thông tin cần thiết để xác thực, phân quyền và kiểm toán hệ thống.

PEMS không yêu cầu người dùng cung cấp dữ liệu không cần thiết cho chức năng đang sử dụng.

### EN

Depending on the features used, PEMS may process information such as name, email address, phone number when provided, organization, job title, nationality, visit registration information, working schedules, delegation members, responsible personnel, documents, images, reports, and other information required for visitor management operations.

For internal users, PEMS may process account information, roles, organizational information, activity history, and information necessary for authentication, authorization, and system auditing.

PEMS does not intentionally request information that is unnecessary for the feature being used.

## 9.3 Google Drive và Google User Data / Google Drive and Google User Data

Đây là section quan trọng nhất cho OAuth verification.

UI nên dùng callout/card riêng để dễ nhìn.

### VI

PEMS sử dụng Google Drive API để cung cấp chức năng lưu trữ và truy xuất tệp phục vụ hoạt động của hệ thống.

Khi Quản trị viên được ủy quyền kết nối tài khoản Google Drive với PEMS, hệ thống yêu cầu quyền truy cập Google Drive nhằm thực hiện các chức năng như tải lên, tải xuống, truy xuất, tổ chức và quản lý các tệp được PEMS sử dụng.

Các tệp có thể bao gồm hình ảnh thư viện, ảnh liên quan đến đoàn khách hoặc chuyến thăm, tài liệu, tài liệu đối tác, biên bản, báo cáo và các tệp nghiệp vụ khác được tạo hoặc quản lý thông qua PEMS.

PEMS chỉ sử dụng quyền truy cập Google Drive để cung cấp và vận hành các chức năng hiển thị rõ trong hệ thống.

PEMS không sử dụng dữ liệu nhận được từ Google Drive cho quảng cáo, xây dựng hồ sơ quảng cáo, bán dữ liệu hoặc cung cấp dữ liệu cho nhà môi giới dữ liệu.

PEMS không sử dụng Google user data cho mục đích không liên quan đến chức năng của hệ thống.

Việc sử dụng thông tin nhận được từ Google APIs tuân thủ Google API Services User Data Policy, bao gồm các yêu cầu về Limited Use.

### EN

PEMS uses the Google Drive API to provide file storage and retrieval capabilities required by the system.

When an authorized administrator connects a Google Drive account to PEMS, the system requests Google Drive access in order to upload, download, retrieve, organize, and manage files used by PEMS.

These files may include gallery media, visit or delegation photos, documents, partner documents, meeting minutes, reports, and other operational files created or managed through PEMS.

PEMS uses Google Drive access only to provide and operate user-facing features that are clearly available within the system.

PEMS does not use data obtained from Google Drive for advertising, advertising profiling, data brokerage, sale of user data, or unrelated purposes.

PEMS does not use Google user data for purposes unrelated to the functionality of the system.

PEMS's use of information received from Google APIs adheres to the Google API Services User Data Policy, including its Limited Use requirements.

## 9.4 Google OAuth Credentials

### VI

PEMS sử dụng OAuth 2.0 để nhận quyền truy cập Google Drive từ tài khoản được quản trị viên chủ động ủy quyền.

PEMS không yêu cầu hoặc lưu trữ mật khẩu tài khoản Google.

Refresh token nhận được thông qua OAuth được bảo vệ trước khi lưu trữ và chỉ được backend của PEMS sử dụng để yêu cầu access token cần thiết cho việc giao tiếp với Google Drive API.

Thông tin xác thực Google không được hiển thị công khai cho người dùng thông thường.

### EN

PEMS uses OAuth 2.0 to obtain Google Drive access from an account explicitly authorized by an administrator.

PEMS does not request or store the password of the connected Google Account.

OAuth refresh tokens are protected before being stored and are used only by the PEMS backend to obtain access tokens required to communicate with the Google Drive API.

Google authentication credentials are not publicly exposed to ordinary system users.

## 9.5 Mục đích sử dụng dữ liệu / How We Use Information

### VI

PEMS sử dụng dữ liệu được xử lý nhằm cung cấp các chức năng của hệ thống; xác thực và phân quyền; tiếp nhận và xử lý yêu cầu tham quan; điều phối hoạt động trước, trong và sau chuyến thăm; quản lý tài liệu, hình ảnh và báo cáo; gửi các thông báo nghiệp vụ; bảo đảm an toàn hệ thống; phát hiện lỗi hoặc hành vi sử dụng không phù hợp; và phục vụ hoạt động kiểm toán khi cần thiết.

### EN

PEMS processes information to provide system functionality; authenticate users and enforce authorization; receive and process visit requests; coordinate activities before, during, and after visits; manage documents, media, and reports; send operational notifications; protect system security; detect errors or misuse; and support auditing when necessary.

## 9.6 Chia sẻ dữ liệu / Sharing of Information

### VI

PEMS không bán dữ liệu cá nhân hoặc Google user data.

Dữ liệu chỉ được xử lý hoặc truyền cho các dịch vụ cần thiết để cung cấp chức năng của PEMS, trong phạm vi cần thiết cho chức năng đó.

Dữ liệu cũng có thể được cung cấp khi cần thiết để đáp ứng yêu cầu pháp lý hợp lệ, bảo vệ an toàn hệ thống hoặc điều tra hành vi lạm dụng.

### EN

PEMS does not sell personal data or Google user data.

Information may be processed by or transmitted to services necessary to provide PEMS functionality, only to the extent required for that functionality.

Information may also be disclosed where necessary to comply with valid legal obligations, protect system security, or investigate abuse.

## 9.7 Bảo mật / Data Security

### VI

PEMS áp dụng các biện pháp kỹ thuật và tổ chức phù hợp nhằm bảo vệ dữ liệu khỏi truy cập, sử dụng, thay đổi hoặc tiết lộ trái phép.

Các biện pháp có thể bao gồm kiểm soát truy cập theo vai trò, xác thực người dùng, mã hóa hoặc bảo vệ thông tin xác thực nhạy cảm, giới hạn quyền truy cập, ghi nhật ký kiểm toán và truyền dữ liệu qua kết nối bảo mật.

Không có hệ thống nào có thể bảo đảm an toàn tuyệt đối; tuy nhiên PEMS được thiết kế nhằm giảm thiểu các rủi ro truy cập trái phép và lộ dữ liệu.

### EN

PEMS applies appropriate technical and organizational safeguards to protect information against unauthorized access, use, alteration, or disclosure.

These safeguards may include role-based access control, user authentication, encryption or protection of sensitive credentials, access restrictions, audit logging, and transmission over secure connections.

No system can guarantee absolute security; however, PEMS is designed to reduce the risks of unauthorized access and data exposure.

## 9.8 Lưu trữ dữ liệu / Data Retention

### VI

PEMS lưu dữ liệu trong thời gian cần thiết để cung cấp chức năng nghiệp vụ, duy trì hồ sơ hoạt động và đáp ứng các yêu cầu vận hành phù hợp.

Thời gian lưu cụ thể có thể phụ thuộc vào loại dữ liệu và mục đích nghiệp vụ.

Khi dữ liệu không còn cần thiết, dữ liệu có thể được xóa, ẩn danh hoặc lưu trữ theo chính sách quản lý dữ liệu áp dụng.

### EN

PEMS retains information for as long as reasonably necessary to provide its operational functions, maintain relevant records, and meet applicable operational requirements.

The retention period may vary depending on the type of information and its business purpose.

When information is no longer required, it may be deleted, anonymized, or archived in accordance with applicable data management practices.

## 9.9 Thu hồi quyền Google / Revoking Google Access

### VI

Quản trị viên có thể ngừng sử dụng kết nối Google Drive của PEMS bằng chức năng quản lý tích hợp trong hệ thống hoặc bằng cách thu hồi quyền truy cập của PEMS trong phần quản lý ứng dụng bên thứ ba của Google Account.

Sau khi quyền truy cập bị thu hồi, PEMS sẽ không thể tiếp tục sử dụng refresh token đó để truy cập Google Drive.

### EN

An administrator may stop using the PEMS Google Drive integration through the integration management features provided by PEMS or by revoking PEMS access from the third-party application settings of the connected Google Account.

After access is revoked, PEMS will no longer be able to use the corresponding refresh token to access Google Drive.

## 9.10 Yêu cầu người dùng / User Requests

### VI

Người dùng có thể liên hệ với đơn vị vận hành PEMS để yêu cầu hỗ trợ về dữ liệu cá nhân, bao gồm yêu cầu cập nhật, chỉnh sửa hoặc xóa dữ liệu khi phù hợp với quyền hạn và quy định áp dụng.

Một số dữ liệu có thể cần được giữ lại trong thời gian cần thiết để bảo đảm tính toàn vẹn của hồ sơ nghiệp vụ, kiểm toán hoặc nghĩa vụ liên quan.

### EN

Users may contact the PEMS operating team for assistance regarding their personal information, including requests to update, correct, or delete information where appropriate and permitted.

Certain information may need to be retained where necessary to preserve operational records, auditing requirements, or other applicable obligations.

## 9.11 Dịch vụ bên thứ ba / Third-party Services

### VI

PEMS có thể sử dụng các dịch vụ bên thứ ba để cung cấp một số chức năng, chẳng hạn Google Drive để lưu trữ tệp, các dịch vụ Google Cloud phục vụ chức năng xử lý dữ liệu được cấu hình trong hệ thống, và nhà cung cấp email để gửi thông báo.

Các dịch vụ này có thể có chính sách bảo mật và điều khoản riêng.

### EN

PEMS may use third-party services to provide certain functionality, such as Google Drive for file storage, configured Google Cloud services for supported data-processing functions, and email delivery providers for system notifications.

These third-party services may be subject to their own privacy policies and terms.

## 9.12 Dữ liệu người chưa thành niên / Information Relating to Minors

### VI

PEMS được thiết kế để phục vụ hoạt động quản lý tiếp khách và hợp tác của tổ chức. Việc cung cấp dữ liệu về người chưa thành niên, nếu phát sinh trong hoạt động nghiệp vụ, cần được thực hiện theo quy trình và căn cứ phù hợp của đơn vị quản lý.

### EN

PEMS is designed to support organizational visitor management and cooperation activities. Where information relating to minors is processed as part of legitimate operational activities, such information should be handled in accordance with the applicable procedures and authority of the responsible organization.

## 9.13 Thay đổi chính sách / Changes to This Privacy Policy

### VI

PEMS có thể cập nhật Chính sách bảo mật này khi chức năng hoặc phương thức xử lý dữ liệu thay đổi.

Phiên bản cập nhật sẽ được công bố trên trang này cùng ngày cập nhật gần nhất.

Nếu có thay đổi đáng kể về cách PEMS sử dụng Google user data, người dùng sẽ được thông báo hoặc yêu cầu xác nhận lại khi cần thiết trước khi dữ liệu được sử dụng cho mục đích mới.

### EN

PEMS may update this Privacy Policy when system functionality or data-processing practices change.

The updated version will be published on this page together with the most recent update date.

If there is a material change to the way PEMS uses Google user data, users will be notified or asked to provide renewed consent where appropriate before the data is used for a new purpose.

## 9.14 Liên hệ / Contact

### VI

```text
Phòng Hợp tác Quốc tế – FPT University
Email: international.fptu@fpt.edu.vn
Điện thoại: 024 6680 5912
Website: https://www.pems-fpt.site
```

### EN

```text
International Cooperation Department – FPT University
Email: international.fptu@fpt.edu.vn
Phone: 024 6680 5912
Website: https://www.pems-fpt.site
```

Nếu codebase hiện có contact data từ config/constants, tái sử dụng thay vì hardcode.

---

# 10. Terms of Service – Header

## VI

```text
Điều khoản sử dụng
Terms of Service
```

Description:

```text
Điều khoản này quy định việc truy cập và sử dụng Hệ thống Quản lý Tiếp khách
và Hợp tác Quốc tế PEMS.
```

## EN

```text
Terms of Service
```

Description:

```text
These Terms govern access to and use of the PEMS Visit Management and
International Cooperation System.
```

Last updated giống Privacy Policy.

---

# 11. Terms of Service – Nội dung đầy đủ

## 11.1 Chấp nhận điều khoản / Acceptance of Terms

### VI

Bằng việc truy cập hoặc sử dụng PEMS, người dùng đồng ý sử dụng hệ thống phù hợp với các điều khoản này, các quy định áp dụng của đơn vị vận hành và các hướng dẫn được cung cấp trong hệ thống.

Nếu người dùng không đồng ý với các điều khoản áp dụng, người dùng nên ngừng sử dụng các chức năng tương ứng.

### EN

By accessing or using PEMS, users agree to use the system in accordance with these Terms, applicable operational policies, and instructions provided through the system.

Users who do not agree with the applicable terms should discontinue use of the relevant features.

## 11.2 Mục đích hệ thống / Purpose of the System

### VI

PEMS được cung cấp nhằm hỗ trợ đăng ký tham quan, quản lý đoàn khách, điều phối hoạt động tiếp khách, quản lý lịch trình, tài liệu, hình ảnh, đối tác, báo cáo và các chức năng liên quan.

### EN

PEMS is provided to support visit registration, delegation management, visitor coordination, schedules, documents, media, partners, reports, and related operational functions.

## 11.3 Tài khoản / Accounts

### VI

Người dùng chịu trách nhiệm bảo vệ thông tin đăng nhập của mình và không được chia sẻ tài khoản cho người không có thẩm quyền.

Mọi thao tác thực hiện bằng tài khoản được xác thực có thể được ghi nhận nhằm phục vụ vận hành và kiểm toán hệ thống.

### EN

Users are responsible for protecting their authentication credentials and must not share accounts with unauthorized persons.

Actions performed through authenticated accounts may be logged for operational and auditing purposes.

## 11.4 Sử dụng được phép / Authorized Use

### VI

Người dùng chỉ được sử dụng PEMS cho các mục đích phù hợp với chức năng và quyền được cấp.

Không được cố gắng truy cập dữ liệu, chức năng hoặc tài nguyên mà tài khoản không được phép sử dụng.

### EN

Users may use PEMS only for purposes consistent with the system's functionality and their assigned permissions.

Users must not attempt to access data, functionality, or resources for which they are not authorized.

## 11.5 Nội dung người dùng cung cấp / User Content

### VI

Người dùng chịu trách nhiệm về tính phù hợp và chính xác của dữ liệu, tài liệu hoặc nội dung mà mình gửi lên PEMS.

Người dùng không được tải lên nội dung trái pháp luật, độc hại, vi phạm quyền của bên khác hoặc không liên quan đến mục đích nghiệp vụ của hệ thống.

### EN

Users are responsible for the appropriateness and accuracy of information, documents, and content they submit to PEMS.

Users must not upload unlawful, harmful, rights-infringing, or operationally irrelevant content.

## 11.6 Google Drive Integration

### VI

Một số chức năng lưu trữ tệp của PEMS được cung cấp thông qua Google Drive API.

Việc kết nối Google Drive chỉ được thực hiện bởi tài khoản hoặc quản trị viên có thẩm quyền.

Việc sử dụng Google Drive thông qua PEMS đồng thời chịu sự điều chỉnh của các điều khoản và chính sách áp dụng của Google.

### EN

Certain PEMS file-storage features are provided through the Google Drive API.

Google Drive connections may only be established by appropriately authorized accounts or administrators.

Use of Google Drive through PEMS is also subject to applicable Google terms and policies.

## 11.7 Tính sẵn sàng của dịch vụ / Availability

### VI

PEMS hướng tới duy trì khả năng hoạt động ổn định nhưng không cam kết dịch vụ luôn hoạt động không gián đoạn.

Hệ thống có thể tạm ngừng để bảo trì, cập nhật, xử lý sự cố hoặc vì nguyên nhân nằm ngoài khả năng kiểm soát hợp lý.

### EN

PEMS aims to provide reliable service but does not guarantee uninterrupted availability.

The system may be temporarily unavailable due to maintenance, updates, incident response, or circumstances beyond reasonable control.

## 11.8 Bảo mật và hành vi bị cấm / Security and Prohibited Conduct

### VI

Người dùng không được thực hiện hành vi cố gắng vượt qua cơ chế bảo mật, khai thác lỗ hổng, gây gián đoạn dịch vụ, phát tán mã độc hoặc truy cập trái phép.

### EN

Users must not attempt to bypass security controls, exploit vulnerabilities, disrupt services, distribute malicious software, or obtain unauthorized access.

## 11.9 Hạn chế hoặc tạm dừng quyền truy cập / Suspension

### VI

Quyền sử dụng PEMS có thể bị hạn chế hoặc tạm dừng khi tài khoản vi phạm điều khoản sử dụng, không còn thẩm quyền sử dụng hệ thống hoặc khi việc hạn chế là cần thiết để bảo vệ hệ thống và dữ liệu.

### EN

Access to PEMS may be restricted or suspended where an account violates these Terms, is no longer authorized to use the system, or where restriction is necessary to protect the system and its data.

## 11.10 Quyền sở hữu trí tuệ / Intellectual Property

### VI

Phần mềm, giao diện, thiết kế, tài liệu hệ thống và các thành phần thuộc PEMS được bảo vệ theo quyền sở hữu và quyền sử dụng tương ứng của các bên có liên quan.

Việc sử dụng PEMS không mặc nhiên chuyển giao quyền sở hữu trí tuệ cho người dùng.

### EN

Software, interfaces, designs, system documentation, and other PEMS components remain subject to the applicable ownership and usage rights of their respective owners.

Use of PEMS does not transfer intellectual property rights to users.

## 11.11 Dịch vụ bên thứ ba / Third-party Services

### VI

Một số chức năng của PEMS phụ thuộc vào dịch vụ bên thứ ba như Google APIs hoặc dịch vụ gửi email.

PEMS không kiểm soát hoàn toàn tính sẵn sàng hoặc thay đổi chính sách của các dịch vụ bên thứ ba đó.

### EN

Certain PEMS features depend on third-party services such as Google APIs or email delivery services.

PEMS does not have complete control over the availability or policy changes of those third-party services.

## 11.12 Quyền riêng tư / Privacy

### VI

Việc xử lý dữ liệu cá nhân khi sử dụng PEMS được mô tả chi tiết trong Chính sách bảo mật.

Link:

```text
Xem Chính sách bảo mật → /privacy
```

### EN

The processing of personal information in connection with PEMS is described in the Privacy Policy.

Link:

```text
View Privacy Policy → /privacy
```

## 11.13 Thay đổi điều khoản / Changes to These Terms

### VI

PEMS có thể cập nhật các Điều khoản này khi chức năng, chính sách hoặc yêu cầu vận hành thay đổi.

Phiên bản mới sẽ được công bố trên trang này cùng ngày cập nhật gần nhất.

### EN

PEMS may update these Terms when functionality, policies, or operational requirements change.

The revised Terms will be published on this page together with the most recent update date.

## 11.14 Liên hệ / Contact

Dùng cùng contact data với Privacy Policy.

---

# 12. Table of Contents

## Privacy

VI:

```text
1. Giới thiệu
2. Dữ liệu PEMS xử lý
3. Google Drive và Google User Data
4. Xác thực và thông tin ủy quyền Google
5. Mục đích sử dụng dữ liệu
6. Chia sẻ dữ liệu
7. Bảo mật
8. Lưu trữ dữ liệu
9. Thu hồi quyền Google
10. Yêu cầu người dùng
11. Dịch vụ bên thứ ba
12. Dữ liệu người chưa thành niên
13. Thay đổi chính sách
14. Liên hệ
```

## Terms

VI:

```text
1. Chấp nhận điều khoản
2. Mục đích hệ thống
3. Tài khoản
4. Sử dụng được phép
5. Nội dung người dùng cung cấp
6. Google Drive Integration
7. Tính sẵn sàng của dịch vụ
8. Bảo mật và hành vi bị cấm
9. Hạn chế hoặc tạm dừng quyền truy cập
10. Quyền sở hữu trí tuệ
11. Dịch vụ bên thứ ba
12. Quyền riêng tư
13. Thay đổi điều khoản
14. Liên hệ
```

---

# 13. Contact card cuối trang

## VI

```text
Bạn có câu hỏi về Chính sách bảo mật / Điều khoản sử dụng?

Phòng Hợp tác Quốc tế – FPT University
international.fptu@fpt.edu.vn
024 6680 5912

[ Liên hệ chúng tôi ]
```

## EN

```text
Have questions about our Privacy Policy / Terms of Service?

International Cooperation Department – FPT University
international.fptu@fpt.edu.vn
024 6680 5912

[ Contact us ]
```

Nếu public website đã có contact route/modal thì dùng lại.

---

# 14. i18n

Phải hỗ trợ VI/EN theo cách hiện tại của project.

Yêu cầu:

- Chọn VI → toàn bộ title, TOC, section, buttons, footer labels sang VI.
- Chọn EN → toàn bộ sang EN.
- Không còn sót tiếng Việt trong English mode.
- Không tạo local language state riêng nếu app đã có language provider/store.

Nếu project dùng translation JSON:

```text
privacy.*
terms.*
legal.*
```

Nếu content quá dài và codebase hiện có pattern content object thì dùng pattern đó.

---

# 15. SEO / metadata

Tối thiểu:

```text
Chính sách bảo mật | PEMS - FPT University
Privacy Policy | PEMS - FPT University
Điều khoản sử dụng | PEMS - FPT University
Terms of Service | PEMS - FPT University
```

Nếu project đã có SEO helper / Helmet → dùng lại.

Không thêm thư viện mới chỉ để set title nếu không cần.

---

# 16. Accessibility

- Heading hierarchy đúng.
- `h1` duy nhất.
- Section dùng `h2`.
- Anchor TOC có focus state.
- Contrast dễ đọc.
- Mobile font không quá nhỏ.
- External link có accessible label nếu cần.

---

# 17. Responsive behavior

## Desktop

- Sidebar TOC sticky.
- Main content readable width.
- Không để line quá dài.

## Tablet

- Sidebar có thể thu gọn.
- Không overlap header.

## Mobile

- TOC dạng accordion/dropdown.
- Legal content 1 cột.
- Contact card full width.
- Không horizontal scroll.

---

# 18. Google Branding sau khi deploy

Sau khi 2 page deploy và public:

```text
Application home page
https://www.pems-fpt.site/

Application privacy policy link
https://www.pems-fpt.site/privacy

Application terms of service link
https://www.pems-fpt.site/terms

Authorized domain
pems-fpt.site
```

Hai route phải truy cập được ở incognito, chưa login.

---

# 19. Acceptance criteria

## Routing

- [ ] `/privacy` mở được public.
- [ ] `/terms` mở được public.
- [ ] Refresh trực tiếp route không 404.
- [ ] Không redirect login.

## UI

- [ ] Dùng Public Header hiện tại.
- [ ] Dùng Public Footer hiện tại.
- [ ] Giao diện đồng nhất với PEMS.
- [ ] Responsive desktop/tablet/mobile.
- [ ] TOC hoạt động.

## Footer

- [ ] `Chính sách bảo mật` → `/privacy`
- [ ] `Điều khoản sử dụng` → `/terms`
- [ ] English labels đúng.

## i18n

- [ ] Privacy VI full.
- [ ] Privacy EN full.
- [ ] Terms VI full.
- [ ] Terms EN full.
- [ ] Không sót text VI ở English mode.

## Privacy

- [ ] Có disclosure Google Drive.
- [ ] Có Google user data usage.
- [ ] Có OAuth credential explanation.
- [ ] Có no advertising / no sale statement.
- [ ] Có Limited Use statement.
- [ ] Có revoke Google access.
- [ ] Có retention.
- [ ] Có contact.

## Terms

- [ ] Có authorized use.
- [ ] Có account responsibility.
- [ ] Có prohibited conduct.
- [ ] Có Google Drive section.
- [ ] Có Privacy link.
- [ ] Có third-party services.
- [ ] Có contact.

## Google verification readiness

- [ ] Homepage public.
- [ ] Privacy public.
- [ ] Terms public.
- [ ] Cùng domain `pems-fpt.site`.
- [ ] Footer link thật.
- [ ] URLs không phải `#`.

---

# 20. Test cases

## Functional

1. Mở `/privacy` chưa login.
2. Mở `/terms` chưa login.
3. Refresh browser tại `/privacy`.
4. Refresh browser tại `/terms`.
5. Footer → Privacy.
6. Footer → Terms.
7. Terms → link Privacy.

## Language

1. VI → Privacy toàn VI.
2. EN → Privacy toàn EN.
3. VI → Terms toàn VI.
4. EN → Terms toàn EN.
5. Đổi language giữa page không reset route sai.

## Responsive

- 375px
- 768px
- 1024px
- Desktop lớn

## Google reviewer simulation

Incognito:

```text
https://www.pems-fpt.site/
https://www.pems-fpt.site/privacy
https://www.pems-fpt.site/terms
```

Tất cả phải load mà không cần tài khoản PEMS.

---

# 21. File impact – AI Agent phải tự map codebase

Trước khi code, Agent phải đọc:

- public router hiện tại
- PublicHeader
- PublicFooter
- public layout
- i18n implementation
- route config
- existing legal/footer placeholders
- contact info source
- design system / Tailwind classes
- deployment SPA routing config

Sau đó báo ngắn:

```text
File → thay đổi gì → lý do
```

Không được đoán path nếu chưa đọc codebase.

---

# 22. Thứ tự triển khai

1. Audit public routing + layout + i18n.
2. Xác định component có thể reuse.
3. Tạo shared legal layout tối thiểu nếu cần.
4. Tạo `/privacy`.
5. Tạo `/terms`.
6. Thêm nội dung VI.
7. Thêm nội dung EN.
8. Cập nhật footer links.
9. Kiểm tra direct refresh.
10. Kiểm tra responsive.
11. Kiểm tra VI/EN.
12. Build/lint/test frontend.
13. Báo file changed + test result.

---

# 23. Những điều AI Agent KHÔNG được làm

- Không sửa backend OAuth.
- Không sửa Google Drive scope.
- Không sửa DB.
- Không sửa permissions.
- Không thay architecture.
- Không tạo legal CMS mới.
- Không thêm dependency mới nếu không cần.
- Không đổi homepage design.
- Không redesign footer ngoài link.
- Không thêm business logic khác.
- Không thay route login.
- Không tự tạo privacy/terms backend endpoint.
- Không hardcode secret.
- Không viết nội dung trái với behavior thực tế của PEMS.

---

# 24. Definition of Done

Task chỉ hoàn thành khi:

```text
/public homepage               ✅
/privacy public                ✅
/terms public                  ✅
VI                             ✅
EN                             ✅
Footer links                   ✅
Direct refresh                 ✅
Responsive                     ✅
Google Drive disclosure        ✅
Google Limited Use statement   ✅
Contact                        ✅
Frontend build                 ✅
Frontend lint/test relevant    ✅
```

Sau đó người vận hành có thể điền Google Auth Platform:

```text
Home page:
https://www.pems-fpt.site/

Privacy:
https://www.pems-fpt.site/privacy

Terms:
https://www.pems-fpt.site/terms

Authorized domain:
pems-fpt.site
```

và tiếp tục:

```text
Google Auth Platform
→ Branding
→ Verify branding
→ Publish branding
→ Verification Center
→ Prepare for verification
```

---

# 25. Output report bắt buộc của AI Agent

Sau khi code xong, báo đúng format:

## Files changed

```text
file/path
- thay đổi
```

## Routes

```text
/privacy
/terms
```

## Reused components

```text
component/path
```

## i18n

```text
VI: done
EN: done
```

## Verification

```text
public route test: PASS/FAIL
direct refresh: PASS/FAIL
footer links: PASS/FAIL
responsive: PASS/FAIL
build: PASS/FAIL
lint: PASS/FAIL
tests: PASS/FAIL
```

## Notes

Chỉ báo issue ngoài scope nếu có khả năng làm task fail thật.

---

# 26. Lưu ý pháp lý

Nội dung trong tài liệu này được thiết kế để:

- phù hợp với hành vi hiện tại của PEMS;
- hỗ trợ Google OAuth verification;
- phản ánh cách PEMS đang sử dụng Google Drive và Google user data.

Đây không phải là tư vấn pháp lý chính thức.

Nếu FPT University có quy trình phê duyệt pháp lý/nội bộ, nội dung cuối cùng nên được rà soát theo quy trình đó trước khi công bố chính thức.
