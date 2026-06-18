# PEMS Auth Error Codes

Mọi lỗi nghiệp vụ auth trả về body:

```json
{ "success": false, "errorCode": "<CODE>", "message": "<thông điệp hiển thị>" }
```

Nguồn sự thật: `backend/PEMS.Application/Common/Security/AuthErrorCodes.cs`.
Frontend map code → message tiếng Việt tại
`frontend/pems-react/src/features/authentication/api/authError.ts` (`AUTH_ERROR_MESSAGES`).
Hai file này phải luôn đồng bộ.

| errorCode | HTTP | Khi nào | Ghi chú |
|---|---|---|---|
| `INVALID_CREDENTIALS` | 401 | Sai email hoặc password | Không tiết lộ email có tồn tại hay không |
| `PASSWORD_LOGIN_DISABLED` | 403 | Password login bị tắt (ProductionSsoOnly hoặc provider bị disable) | |
| `CAMPUS_REQUIRED` | 400 | Internal portal nhưng thiếu `selectedCampusId` | |
| `CAMPUS_MISMATCH` | 403 | Campus chọn ≠ `PrimaryCampusId` của user | |
| `WRONG_PORTAL_VISITOR_ACCOUNT` | 403 | Account VISITOR đăng nhập cổng INTERNAL | |
| `WRONG_PORTAL_INTERNAL_ACCOUNT` | 403 | Account internal đăng nhập cổng VISITOR | |
| `INTERNAL_ACCOUNT_NOT_FOUND` | 403 | Internal portal, email chưa tồn tại | KHÔNG auto-create |
| `ACCOUNT_INACTIVE` | 403 | User/role không ACTIVE | |
| `ACCOUNT_LOCKED` | 403 | `locked_until` còn hiệu lực (vượt số lần sai) | |
| `SSO_DISABLED` | 403 | `AllowGoogleSso = false` | |
| `EXTERNAL_AUTH_FAILED` | 401/403 | Google token không hợp lệ / provider bị disable / subject mismatch | Message generic |
| `VISITOR_PROVISION_DISABLED` | 403 | Visitor portal SSO lần đầu nhưng `AutoCreateVisitorOnExternalLogin=false` | |
| `FEID_DISABLED` | 403 | `AllowFeid = false` | |
| `FEID_NOT_CONFIGURED` | 403 | FEID login nhưng chưa cấu hình provider thật | Không fake login |
| `FEID_NOT_ELIGIBLE` | 403 | FEID identity hợp lệ nhưng không đủ điều kiện (vd cohort < `StudentFeidMinCohort`) | Chỉ reachable khi FEID có provider thật |

## Quy tắc status code
- **400**: thiếu input/format sai (`CAMPUS_REQUIRED`, validation).
- **401**: sai credential / token invalid / token-session hết hạn ở protected endpoint.
- **403**: wrong portal, campus mismatch, provider disabled, account inactive/locked, FEID not configured.
- **500**: lỗi bất ngờ (không lộ secret).

> Quan trọng: KHÔNG dùng `401` cho wrong portal / campus mismatch — frontend interceptor có thể hiểu
> nhầm là token hết hạn rồi refresh/logout sai.
