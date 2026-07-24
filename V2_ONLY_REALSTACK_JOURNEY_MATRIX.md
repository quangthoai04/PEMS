# V2-Only Real-Stack Journey Matrix

## Overview
This document records the results of the manual end-to-end testing of the 27 identified user journeys on the current HEAD using a real backend/frontend stack.

## Testing Environment
- **Database Name**: pems_db
- **Matches pems_test_run_<guid>**? NO
- **Creation Time**: N/A (Pre-existing shared DB)
- **Cleanup Time**: N/A (Not cleaned up)
- **Protected/Shared DB Mutated**: YES
- **Legacy Columns Note**: 10 legacy global columns remain temporarily as compatibility projection and migration safety. V2 runtime does not read them as canonical business sources.

## Final Status
**Overall V2-only runtime: IN PROGRESS**
**Real-stack: 0/27 PASS, 0 FAIL, 0 BLOCKED, 27 NOT RUN**

## Journey Results

| Journey ID | Persona/Account | Frontend Route | API Endpoint | Request Code / Instance ID | Precondition | Action | Expected Result | Actual Result | HTTP Status | Error Code | DB Assertions | Screenshot/Log Ref | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| J-01 | Public Visitor | /visit-requests/v2/new | POST /api/visit-requests/public/v2 | - | None | Submit public initiate V2 | OTP sent | - | - | - | - | - | NOT RUN |
| J-02 | Public Visitor | /visit-requests/v2/verify | POST /api/visit-requests/public/v2/verify | - | J-01 completed | Submit OTP | Request created | - | - | - | - | - | NOT RUN |
| J-03 | Authenticated Visitor | /visit-requests/v2/new | POST /api/visit-requests/v2 | - | Logged in as Visitor | Create single-campus | Request created | - | - | - | - | - | NOT RUN |
| J-04 | Authenticated Visitor | /visit-requests/v2/new | POST /api/visit-requests/v2 | - | Logged in as Visitor | Create uniform multi-campus | Request created | - | - | - | - | - | NOT RUN |
| J-05 | Authenticated Visitor | /visit-requests/v2/new | POST /api/visit-requests/v2 | - | Logged in as Visitor | Create mixed multi-campus | Request created | - | - | - | - | - | NOT RUN |
| J-06 | Staff | /visit-requests/v2/new | POST /api/visit-requests/v2 | - | Logged in as Staff | Create single-campus | Request created | - | - | - | - | - | NOT RUN |
| J-07 | Staff Leader | /visit-requests/v2/new | POST /api/visit-requests/v2 | - | Logged in as Staff Leader | Create request | Request created | - | - | - | - | - | NOT RUN |
| J-08 | Staff | /visit-requests/v2/new | POST /api/visit-requests/v2 | - | Logged in as Staff | Create & self-host own campus | Host assigned | - | - | - | - | - | NOT RUN |
| J-09 | Staff Leader | /visit-requests/v2/new | POST /api/visit-requests/v2 | - | Logged in as Staff Leader | Assign host mixed multi-campus | Host assigned | - | - | - | - | - | NOT RUN |
| J-10 | Submitter | /visit-requests/v2/edit | PUT /api/visit-requests/v2/pending | - | Request is PENDING | Submit pending edit | Request updated | - | - | - | - | - | NOT RUN |
| J-11 | Submitter | /visit-requests/v2/resubmit | POST /api/visit-requests/v2/resubmit | - | Request is REJECTED | Submit resubmit | Request resubmitted | - | - | - | - | - | NOT RUN |
| J-12 | Staff Leader | /visit-requests/v2/approve | POST /api/visit-requests/v2/approve | - | Request is PENDING | Submit approve | Request approved | - | - | - | - | - | NOT RUN |
| J-13 | Staff Leader | /visit-requests/v2/reject | POST /api/visit-requests/v2/reject | - | Request is PENDING | Submit reject | Request rejected | - | - | - | - | - | NOT RUN |
| J-14 | Staff | /invitations | POST /api/invitations/accept | - | Invited as Host | Accept invitation | Host assigned | - | - | - | - | - | NOT RUN |
| J-15 | Staff | /invitations | POST /api/invitations/decline | - | Invited as Host | Decline invitation | Invitation declined | - | - | - | - | - | NOT RUN |
| J-16 | Visitor | /claims | POST /api/claims/accept | - | Invited as Primary Contact | Accept claim | Primary contact active | - | - | - | - | - | NOT RUN |
| J-17 | Visitor | /claims | POST /api/claims/decline | - | Invited as Primary Contact | Decline claim | Claim declined | - | - | - | - | - | NOT RUN |
| J-18 | Visitor | /claims/transfer | POST /api/claims/transfer | - | Transfer requested | Accept transfer | Primary contact transferred | - | - | - | - | - | NOT RUN |
| J-19 | Host | /visit-requests/v2/safe-edit | PUT /api/visit-requests/v2/safe-edit | - | Request is APPROVED | Submit safe edit | Request updated safely | - | - | - | - | - | NOT RUN |
| J-20 | Staff Leader | /amendments/approve | POST /api/amendments/approve | - | Amendment pending | Approve amendment | Amendment applied | - | - | - | - | - | NOT RUN |
| J-21 | Staff Leader | /amendments/reject | POST /api/amendments/reject | - | Amendment pending | Reject amendment | Amendment rejected | - | - | - | - | - | NOT RUN |
| J-22 | Staff | /visit-requests/v2/:id | GET /api/visit-requests/v2/:id | - | Request at foreign campus | Access request | 403 Forbidden | - | - | - | - | - | NOT RUN |
| J-23 | Host | /visit-requests/v2/:id | GET /api/visit-requests/v2/:id | - | Mixed multi-campus request | Access request | Only own campus data visible | - | - | - | - | - | NOT RUN |
| J-24 | Visitor | /dashboard | GET /api/dashboard | - | Has multiple relations | View dashboard | Correct relations shown | - | - | - | - | - | NOT RUN |
| J-25 | Staff | /dashboard | GET /api/dashboard | - | Registered requests exist | View dashboard | Registered requests shown | - | - | - | - | - | NOT RUN |
| J-26 | Staff Leader | /dashboard | GET /api/dashboard | - | Has host/invitation/registered | View dashboard | All relations shown | - | - | - | - | - | NOT RUN |
| J-27 | Any User | /visit-requests/v1 | POST /api/visit-requests/v1 | - | None | Access V1 endpoint | 410 Gone / Tombstone | - | - | - | - | - | NOT RUN |
