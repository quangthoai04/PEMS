# UC-95 + UC-99 — Account List / Search / Filter

> Implements **UC-95 View Account List** and **UC-99 Search and Filter Accounts** end-to-end
> (backend query + scope + paging + filter + sort, and the Account Management page wired to the
> real API). Built per `docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY.md`.

## 1. Summary

- `GET /api/accounts/viewaccountlist` (UC-95) and `GET /api/accounts/searchandfilteraccounts` (UC-99)
  now run for real — no more `NotImplementedException`.
- Both endpoints share **one** read model (`AccountListQueryExecutor`) so there is no duplicated
  query logic; UC-99 is UC-95 with all filters supplied.
- Returns a paged envelope `PaginatedResult<AccountListItemDto>` with no sensitive columns.
- Caller scope is enforced server-side (never trust the client `campusId`).
- The Account Management page (`pages/dashboard/accounts/AccountManagement.tsx`) loads the real list
  with server-side search / role / status / campus filters + pagination, with loading / empty / error states.

## 2. Backend files changed / added

| File | Change |
|---|---|
| `PEMS.Application/Common/Models/PaginatedResult.cs` | **New** — generic paged envelope + `Create()` factory |
| `PEMS.Application/Accounts/Common/AccountListItemDto.cs` | **New** — list row DTO (no sensitive fields) |
| `PEMS.Application/Accounts/Common/IAccountListCriteria.cs` | **New** — shared paging/filter/sort inputs |
| `PEMS.Application/Accounts/Common/AccountListQueryExecutor.cs` | **New** — scope + filter + sort + page + project |
| `PEMS.Application/Accounts/Common/AccountListCriteriaRules.cs` | **New** — shared FluentValidation rules |
| `PEMS.Application/Accounts/Common/AccountErrorCodes.cs` | **New** — stable error codes |
| `PEMS.Application/Accounts/Queries/ViewAccountList/ViewAccountListQuery.cs` | **Rewritten** — full criteria, returns `PaginatedResult<AccountListItemDto>` |
| `…/ViewAccountList/ViewAccountListQueryHandler.cs` | **Rewritten** — delegates to executor |
| `…/ViewAccountList/ViewAccountListQueryValidator.cs` | **New** |
| `…/ViewAccountList/ViewAccountListDto.cs` | **Removed** (scaffold) |
| `…/SearchandFilterAccounts/SearchandFilterAccountsQuery.cs` | **Rewritten** — same criteria |
| `…/SearchandFilterAccounts/SearchandFilterAccountsQueryHandler.cs` | **Rewritten** — delegates to executor |
| `…/SearchandFilterAccounts/SearchandFilterAccountsQueryValidator.cs` | **New** |
| `…/SearchandFilterAccounts/SearchandFilterAccountsDto.cs` | **Removed** (scaffold) |

`AccountsController` was already wired (`viewaccountlist` → `[RequirePermission(UC-95.VIEW_ACCOUNT_LIST)]`,
`searchandfilteraccounts` → `[RequirePermission(UC-99.SEARCH_AND_FILTER_ACCOUNTS)]`) — no change needed.

## 3. Frontend files changed / added

| File | Change |
|---|---|
| `features/account-management/types/accountManagement.types.ts` | **+** `AccountListItem`, `AccountListQueryParams`, `PaginatedResult<T>`, `ActiveCampusOption` |
| `features/account-management/api/accountManagementApi.ts` | **+** `getAccounts`, `searchAccounts`, `getActiveCampuses` (params cleaned of empty values) |
| `features/account-management/api/accountError.ts` | **New** — `getAccountErrorMessage` (errorCode → localized VN message) |
| `features/account-management/hooks/useAccountList.ts` | **New** — fetch on param change, ignore stale responses |
| `shared/hooks/useDebounce.ts` | **Fixed** — was an empty stub `() => ({})`; now a real `useDebounce<T>(value, delay)` |
| `pages/dashboard/accounts/AccountManagement.tsx` | **Wired** — "all" tab now server-driven (search/filter/campus/pagination), loading/empty/error states |

## 4. API contract

`GET /api/accounts/viewaccountlist` (UC-95) and `GET /api/accounts/searchandfilteraccounts` (UC-99).

Query params (all optional): `page` (default 1), `pageSize` (default 20, **1–100**), `keyword` (≤100),
`roleCode`, `subRole`, `status`, `campusId`, `departmentId`, `providerType`, `createdVia`,
`accountType` (`ALL|INTERNAL|VISITOR`), `hasCampus`, `fromDate`, `toDate`, `lastLoginFrom`, `lastLoginTo`,
`sortBy`, `sortDirection` (`asc|desc`).

Response `200`:

```json
{
  "items": [
    {
      "userId": "…", "email": "…", "fullName": "…",
      "roleCode": "STAFF", "roleName": "Staff", "subRole": "Leader",
      "campusId": "…", "campusCode": "HN", "campusName": "FPT University Hà Nội",
      "departmentId": null, "departmentName": null,
      "status": "ACTIVE", "createdVia": "MANUAL_CREATED",
      "providers": ["GOOGLE_SSO"],
      "lastLoginAt": "2026-06-18T10:00:00Z", "createdAt": "2026-06-01T10:00:00Z", "updatedAt": null,
      "canViewDetails": true, "canUpdateRole": true, "canManageStatus": true
    }
  ],
  "page": 1, "pageSize": 20, "totalItems": 1, "totalPages": 1,
  "hasNextPage": false, "hasPreviousPage": false
}
```

Errors (via `ExceptionHandlingMiddleware` → `{ success:false, errorCode?, message, traceId }`):

| Code | HTTP | When |
|---|---|---|
| `ACCOUNT_LIST_FORBIDDEN` | 403 | authenticated but no list/search permission |
| `CAMPUS_SCOPE_FORBIDDEN` | 403 | campus-scoped caller requests another campus' `campusId` |
| `UNSUPPORTED_SORT_COLUMN` | 400 | `sortBy` not in the whitelist |
| (validation) | 400 | `pageSize`>100 / <1, `page`<1, keyword>100, bad date range, bad `sortDirection`/`accountType` |
| (auth) | 401 | no/expired token (also auto-refresh on the client) |
| `RATE_LIMIT_EXCEEDED` | 429 | per-user request rate exceeded (ADMIN/HO 60/min, others 30/min) |

**Never returned:** `passwordHash`, `providerSubject`, tokens, security stamps.

## 5. Filters / sort supported

- **Search keyword** (contains): email, full name, role code/name, campus name/code, department name, phone, student code. Sensitive columns are never searched.
- **Filters:** roleCode, subRole, status (`ACTIVE|INACTIVE|LOCKED`), campusId, departmentId, providerType (`LOCAL_PASSWORD|GOOGLE_SSO|FEID`), createdVia, accountType (`ALL|INTERNAL|VISITOR`), hasCampus, created-at range, last-login range.
- **Sort whitelist:** `createdAt`(default), `updatedAt`, `lastLoginAt`, `email`, `fullName`, `role`, `status`, `campus`; tie-break by `userId`; default direction `desc`. Sort is a `switch`, never raw SQL.

## 6. Scope / security rules

Source of truth = seeded RBAC (`role_permissions`). Account list/search is granted to **HO** (system-wide)
and **STAFF Leader** (campus-scoped). Scope applied in `AccountListQueryExecutor`:

- **ADMIN / HO (privileged):** no campus restriction; an optional `campusId` is applied as a plain filter.
- **STAFF Leader (campus-scoped):** own-campus accounts only, **plus** Visitor accounts **only when a keyword
  is supplied** (never dumps all campus-less visitors — supports UC-100 convert-by-email). Requesting another
  campus' `campusId` → `CAMPUS_SCOPE_FORBIDDEN`.
- **Other campus-scoped roles:** strictly own campus, no visitor dump. No campus assigned → empty result.
- **DEPT / STUDENT / VISITOR:** not granted UC-95/UC-99 → blocked at `[RequirePermission]` with 403.

Per-row `canViewDetails` / `canUpdateRole` / `canManageStatus` = (caller holds UC-98 / UC-100 / UC-97) **and**
the row is in action scope (privileged → any row; campus-scoped → own-campus row or a Visitor, never an ADMIN/HO row).

> **Note (Permission Matrix — by design):** per the current Permission Matrix, account list/search is granted to
> **HO** and **STAFF Leader** only. **ADMIN is not granted UC-95/UC-99**, so an ADMIN call to these endpoints
> returns **403 — this is the expected, correct behavior, not a bug.** We do **not** modify `role_permissions` /
> seed here: changing who may access accounts is a Permission-Matrix decision, out of scope for UC-95/UC-99.
> (The handler still treats ADMIN as privileged *if* the matrix ever grants it — no code change needed then.)

## 7. Anti-spam / performance

- **Rate limit (runtime):** the account read endpoints carry `[EnableRateLimiting("accounts-read")]`. The policy is
  the .NET 8 built-in limiter, configured in `Program.cs` + enforced by `app.UseRateLimiter()`. It is a per-user
  fixed window (1 min): **ADMIN/HO 60 req/min, other roles 30 req/min** (partitioned by user id, fallback IP).
  Over-limit → **429** `{ success:false, errorCode:"RATE_LIMIT_EXCEEDED", message:… }` + `Retry-After` header.
  - **Scope/blast radius:** the policy only applies where the attribute is present (the two account endpoints).
    `UseRateLimiter()` does **not** limit any endpoint without an attached policy and there is **no** global limiter,
    so the rest of the system is unaffected.
  - Note: `RateLimitMiddleware.cs` / `RateLimitingExtensions.cs` were empty stub classes (never implemented); this
    UC uses the framework limiter directly instead of those stubs.
- `AsNoTracking()` + projection to DTO; no `Include` bloat; child `providers` projected in the same query (no N+1).
- `pageSize` hard-capped 1–100 (validator) — no `all=true` / unbounded page; `keyword` capped at 100.
- Existing DB indexes already cover the query (`idx_users_status`, `idx_users_campus_role_status`,
  `idx_users_email_status`, `idx_users_role_sub_role`, `idx_users_created_via`, `idx_users_last_login`, …) →
  **no index patch needed**.
- Frontend debounces the keyword (450ms) and ignores stale responses (last-request-wins).

## 8. Manual test checklist

Backend (Swagger/Postman) — acceptance per the current Permission Matrix:

- [ ] No token → 401.
- [ ] **HO (has UC-95/UC-99) → 200**, system-wide.
- [ ] **STAFF Leader (has UC-95/UC-99) → 200**, only own-campus accounts (campus scope).
- [ ] **ADMIN (NOT granted UC-95/UC-99) → 403 — expected by design, NOT a bug.**
- [ ] **VISITOR / STUDENT / DEPT (no permission) → 403.**
- [ ] STAFF Leader filter `campusId`=other campus → 403 `CAMPUS_SCOPE_FORBIDDEN`.
- [ ] Over rate limit (HO/ADMIN-test >60/min, others >30/min) → 429 `RATE_LIMIT_EXCEEDED`.
- [ ] STAFF Leader keyword = exact visitor email → visitor appears; no keyword → no visitors dumped.
- [ ] keyword search returns matching rows; no match → 200 empty, `totalItems=0`.
- [ ] `roleCode=VISITOR` / `status=ACTIVE` / `accountType=INTERNAL` / `providerType=GOOGLE_SSO` filter correctly.
- [ ] `sortBy=email&sortDirection=asc` ordered; `sortBy=passwordHash` → 400 `UNSUPPORTED_SORT_COLUMN`.
- [ ] `pageSize=9999` → 400; `fromDate>toDate` → 400.
- [ ] Response never contains passwordHash / providerSubject / tokens.

Frontend:

- [ ] Account Management loads real data (no mock) on the "Tất cả tài khoản" tab.
- [ ] Typing in search → debounced API call; role/status/campus filters → API call; pagination → API call.
- [ ] Loading / empty / error (401/403) states render.
- [ ] Create/Edit/Status buttons don't crash (those flows remain out of scope).

## 9. Build result

- Backend: `dotnet build PEMS.Api/PEMS.Api.csproj` → **success** into real `bin` (Domain, Application,
  Infrastructure, Api). Note: if the API is running it locks `PEMS.Api.dll/.exe` — stop the process, rebuild,
  restart (done during verification).
- Frontend: `npm run build` (vite) → **success**.

### Runtime verification (DevMixed, dev seed accounts, password `Admin@123`)

Ran against a freshly rebuilt API. Permissions (`GET /api/accounts/viewaccountlist`):

| Caller | Result | Notes |
|---|---|---|
| No token | **401** | |
| HO (`ho@fpt.edu.vn`) | **200** | system-wide |
| STAFF Leader (`staff.leader.hn@fpt.edu.vn`) | **200** | `totalItems=15`, **all rows campusCode=HN**; no Visitors (no keyword) |
| ADMIN (`admin@fpt.edu.vn`) | **403** | by design (not granted UC-95) |
| VISITOR (`visitor@example.com`) | **403** | |

Rate limit:

- STAFF Leader: 30 × 200 then **429** (`first_429` at the 30th request in the 1-min window).
- HO: 60 × 200 then **429** (`first_429` at the 60th request) — confirms the higher HO limit.
- 429 body: `{"success":false,"errorCode":"RATE_LIMIT_EXCEEDED","message":"Bạn thao tác quá nhanh. Vui lòng thử lại sau."}`
  with header **`Retry-After: 60`**.

## 10. Known limitations / TODO next phase

- Stat widgets: the **total** card uses the server `totalItems`; the per-status / per-campus breakdown cards
  reflect the **currently loaded page** (no aggregate endpoint yet). TODO: dedicated counts endpoint.
- "Chưa từng đăng nhập" (never-logged-in) is not a server filter yet (no `hasLoggedIn` param).
- The "pending approval" tab and the view/edit drawer & create modal remain mock/out of scope (UC-96/97/98/100).
- ADMIN access is **intentionally** not granted (see §6) — no seed change unless the Permission Matrix changes.
- Rate limiting is enforced per-endpoint for UC-95/UC-99 (see §7). A broader, system-wide rate-limit policy (all
  endpoints, Redis-backed store) is deferred to the **security-hardening phase**.
