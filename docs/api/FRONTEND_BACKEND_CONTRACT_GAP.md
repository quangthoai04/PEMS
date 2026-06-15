# Frontend & Backend Contract Gap Analysis

| Frontend Page | Frontend API Function | Expected Endpoint | Backend Endpoint Exists | Request Type | Response Type | Gap |
| --- | --- | --- | --- | --- | --- | --- |
| Dashboard Home | `getDelegationsStats` | `/api/reports/viewdashboardstatistics` | Yes | Query | DTO | Backend scaffold only |
| Visit Requests | `submitVisitRequest` | `/api/delegations/submitvisitrequest` | Yes | Command | Response | Backend scaffold only |
| Delegation List | `getGuestDelegationList` | `/api/delegations/viewguestdelegationlist` | Yes | Query | DTO | Backend scaffold only |
| Create Account | `createAccount` | `/api/accounts/createaccount` | Yes | Command | Response | Backend scaffold only |
| Account List | `getAccountList` | `/api/accounts/viewaccountlist` | Yes | Query | DTO | Backend scaffold only |
| Login Page | `login` | `/api/authentication/loginviacredentials` | Yes | Command | Response | Backend scaffold only |
| Partner List | `getPartnerLists` | `/api/partners/viewpartnerlists` | Yes | Query | DTO | Backend scaffold only |

*Note: All 135 use cases have been scaffolded in the backend. The gap currently is that the backend only contains the scaffolding (routes, MediatR handlers) and needs the business logic implemented to match the frontend payloads.*
