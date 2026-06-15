# Permission Rules

- **F**: Full Access (Read, Edit, Delete, Admin)
- **E**: Edit Access
- **R**: Read Access
- **O**: Own / Object-level ownership access
- **—**: No Access
# Role & Permission Matrix

## 1. Purpose

This document defines the initial permission matrix for all use cases in the system.
The matrix describes which user roles are allowed to access, view, edit, manage, or perform each use case.

The purpose of this file is to provide a clear permission reference for backend authorization, frontend menu visibility, UI action control, and future use case implementation.

At the current stage, this permission matrix is used as a working draft. Some permissions may need to be reviewed again after each use case is fully specified and validated with business rules.

---

## 2. Permission Legend

| Symbol | Meaning                   | Description                                                                                                                                                              |
| ------ | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| F      | Full Permission           | The role has full authority to perform the main action of the use case, such as create, manage, approve, delete, assign, or configure.                                   |
| E      | Execute / Edit Permission | The role can execute or edit the use case within a limited business scope. This permission is usually used for approval, update, processing, or status-changing actions. |
| R      | Read Permission           | The role can only view, search, filter, or access information. The role cannot change system data through this permission.                                               |
| O      | Own / Personal Permission | The role can perform the action only on its own account, profile, session, email, personal calendar, or personal data.                                                   |
| —      | No Permission             | The role is not allowed to access or perform this use case.                                                                                                              |

---

## 3. Role Description

| Role            | Description                                                                                                                                                                                              |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| HO              | Head Office role responsible for high-level management such as campus management, FAQ management, report viewing, agenda templates, and some system-level configurations.                                |
| Admin           | System administrator responsible for technical administration such as role management, permission configuration, API configuration, API logs, and integration settings.                                  |
| Staff Leader    | Campus or staff management role responsible for reviewing requests, approving news, managing staff-related operations, and supervising delegation-related activities.                                    |
| Staff           | Operational role responsible for creating and updating delegation records, preparing logistics, managing partner information, uploading documents, creating news, and handling daily coordination tasks. |
| Department Lead | Department-level manager responsible for approving resource requests, assigning department tasks, managing department personnel, and monitoring coordination work.                                       |
| Department      | Department member role responsible for reviewing assigned tasks, participating in delegation activities, signing service delivery reports, and supporting department coordination work.                  |
| Student         | Supporting role that may participate in delegation activities, confirm participation, create meeting minutes, submit feedback, upload visit photos, or create news when assigned.                        |
| Visitor         | External guest role mainly used for submitting visit requests and viewing information related to their own delegation or visit process.                                                                  |

---

## 4. General Permission Principles

The system follows role-based access control. Each use case must be checked against the user’s assigned role before allowing access.

Public use cases such as homepage, news, FAQ, contact information, partner information, gallery, and policy pages are available with read-only access.

Authenticated use cases such as notifications, profile management, password change, email actions, personal calendar, and assigned tasks require the user to be logged in.

For use cases marked as **O**, users are only allowed to access or modify their own data. They must not be able to view or change another user’s private information.

For use cases marked as **R**, users can only view, search, or filter data. They must not be allowed to create, update, delete, approve, or change status.

For use cases marked as **E**, users can perform business actions such as approve, update, process, change status, or edit data, but only within their assigned business scope.

For use cases marked as **F**, users have full control over the main action of that use case within the scope of their role and assigned responsibility.

For use cases marked as **—**, the use case must be hidden from the user interface and blocked by backend authorization.

---

## 5. Feature Area Notes

### Common

Common use cases are mostly public-facing functions. These include viewing homepage content, searching public information, viewing contact information, policies, FAQ, news, partners, gallery, and notifications.

Public content must only include published or visible data. Draft, hidden, internal, or restricted data must not be exposed to public users.

### Authentication

Authentication use cases include SSO login, credential login, logout, and forgot password. These use cases are available to all users because every role may need to authenticate.

Authentication actions must include account status checking, secure session handling, token handling, password reset validation, OTP or reset link expiration, and login activity logging.

### Profile Management

Profile management use cases allow authenticated users to view and update their own profile, change password, and manage personal information.

Users must not be allowed to change sensitive fields such as role, campus assignment, account status, or permission level unless they have a separate authorized management use case.

### Delegation Reception Management

Delegation reception management is the core operational area of the system. It covers visit request submission, approval, delegation creation, logistics preparation, participation confirmation, resource approval, meeting minutes, documents, feedback, photos, partner creation, news creation, and delegation closing.

Access to delegation data must depend on role, assigned campus, assigned department, participation status, and delegation ownership.

### Email Management

Email management covers template management, email drafting, sending, viewing, and replying.

Email templates are controlled by authorized management roles. Normal users may send or reply to emails only within their permitted scope. Email history should be logged and access should be restricted to relevant senders, recipients, or linked delegation records.

### Partner Management

Partner management covers partner creation request processing, partner editing, partner list viewing, partner search, and partner details.

Partner records should be protected from unauthorized modification. Duplicate organization or contact information should be checked before creating or approving partner profiles.

### Document Management

Document management covers viewing and searching uploaded documents.

Users should only see documents that they are allowed to access based on delegation, campus, role, or assigned responsibility. Restricted documents must not appear in search results for unauthorized users.

### Gallery Management

Gallery management controls the virtual campus gallery content.

Only authorized users should be able to add, update, or delete gallery items. Public users should only see gallery items that are marked as visible or published.

### Minutes Management

Minutes management covers the archive of meeting minutes.

Meeting minutes should be linked to the correct delegation and should only be visible to users with proper access. Closed delegation records should be protected from unauthorized editing.

### FAQ Management

FAQ management allows authorized users to create, update, search, and control FAQ visibility.

Only visible or published FAQ items should appear on the public homepage. Draft, hidden, or deleted FAQ items must not be shown publicly.

### Report Management

Report management provides dashboard statistics, report export, and time-based filtering.

Statistics should follow role scope, campus scope, and reporting permission. Exported reports must not include data outside the user’s authorized access range.

### Calendar Management

Calendar management includes personal events, department calendar, view mode switching, and event details.

Users may view events related to their role or assignment. Personal event create, update, and delete actions must only apply to events owned by the current user.

### Feedback Management

Feedback management includes searching feedback and viewing feedback summaries.

Feedback data should be filtered by role and access scope. Sensitive feedback details should be hidden if required by business rules.

### Campus Management

Campus management is controlled by HO.

Campus records are master data and may affect account assignment, delegation routing, department structure, and reporting. Changes to campus status should be handled carefully.

### News Management

News management controls news approval, publishing, visibility, list viewing, details viewing, multilingual news creation, and editing.

Only approved news should be published publicly. Draft, rejected, hidden, or pending news should not appear on the public homepage.

### Account Management

Account management controls user account listing, creation, status management, details viewing, search, filtering, and role update.

Sensitive data such as passwords, tokens, reset codes, and authentication secrets must never be displayed. Account status changes should revoke active sessions if needed.

### Department Management

Department management controls department creation, update, search, status, personnel, task assignment, task review, service delivery report signing, and department lead reassignment.

Department operations must respect campus scope and department ownership. Removing personnel should not delete the user account; it should only remove the department relationship.

### Role & Permission Management

Role and permission management is controlled by Admin.

This area defines system roles and their permission matrix. Changes to permissions must be logged carefully because they directly affect system access control.

### API Management

API management is controlled by Admin.

API configuration includes external service settings, connection testing, request limits, status management, and API logs. Secrets, tokens, and credentials must be encrypted or masked and must not be exposed in logs.

### Agenda Templates Management

Agenda template management is controlled by HO.

Agenda templates are reusable schedule structures for delegation preparation. Updating or deleting a template should not automatically modify existing delegation agendas unless explicitly confirmed by business rules.

---

## 6. Implementation Notes

Frontend permission checking should be used to hide menus, buttons, and pages that the user cannot access.

Backend permission checking is mandatory and must be the final source of truth. Even if a button is hidden on the frontend, the backend must still validate permission before processing any request.

Each protected API should check:

* whether the user is authenticated;
* whether the user’s role has permission for the use case;
* whether the user has access to the specific data scope;
* whether the action is valid for the current business status;
* whether audit logging is required.

All create, update, delete, approve, reject, assign, publish, close, or status-changing actions should be recorded in audit logs.

This permission matrix is a draft baseline and should be updated whenever use case details, role responsibilities, business rules, or security requirements change.
 