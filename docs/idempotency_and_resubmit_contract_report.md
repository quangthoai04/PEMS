# Idempotency and Resubmit Contract Report

## 1. Idempotency Contract

### 1.1 Duplication Window & Keys
The system identifies duplicate visit requests (idempotency) based on a 15-minute window for the same core business keys:
- `registrant_email`
- `registrant_phone`
- `working_content_hash` (or raw working content/title)
- `campus`
- `date_window` (start time / end time)

If an anonymous or authenticated user submits a payload matching these keys within 15 minutes of an existing request, the system treats it as a duplicate to prevent accidental double-bookings or spam.

### 1.2 Concurrency & Race Conditions
- **Application Level Check:** The idempotency check is primarily enforced at the application level (MediatR handler `VerifyAndCreateVisitRequestV2Command`). The handler queries the database to see if a matching request exists within the time window.
- **Race Condition Risk:** Because this is an application-level `SELECT` followed by an `INSERT`, a race condition exists. If two identical requests arrive at the exact same millisecond, both handlers might execute the `SELECT` simultaneously, find no duplicate, and both proceed to `INSERT`.
- **Database Level:** There is currently **NO** database-level `UNIQUE` constraint on `(registrant_email, campus, working_content_hash, created_at)` in the schema that strictly prevents this. Therefore, a pure race condition can bypass the idempotency check. It is recommended to implement a short-lived distributed lock (e.g., Redis) or a unique hash column for strict concurrency protection if traffic spikes are expected.

## 2. Resubmit Contract & Identity Spoofing Protection

### 2.1 Resubmit Rules
The Resubmit action (`POST /api/v2/visit-requests/{id}/resubmit`) allows an applicant to address rejection reasons and submit a revised payload.

### 2.2 Identity Spoofing Protection
- **Rule:** Resubmit is **strictly prohibited** if the identity fields (`registrant_email`, `registrant_phone`) change. 
- **Reason:** To prevent identity spoofing. A user cannot initiate a request under one identity, get it reviewed, and then resubmit the payload substituting a completely different person's email or phone number.
- **Enforcement:** The backend application validates that the original registrant identity matches the resubmitted identity. If it detects a mismatch, it will fail closed and reject the operation. The original session identity (or authenticated user identity) must also match the owner of the request.
