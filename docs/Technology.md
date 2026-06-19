<!-- =====================================================================
PEMS DOC UPDATE v8.2-full-preserved-cancel-delegation-no-external-note
Generated: 2026-06-19
Mode: PRESERVE ORIGINAL CONTENT + APPEND ADDENDUM.
No original section below has been removed or compressed.
The addendum section at the end is the authoritative update for cancellation UC-136.
===================================================================== -->

Project name: Partnership Engagement Management System  
Main function:

Technology used:

| Category | Tools / Infrastructure |
| ----- | ----- |
| **Frontend**  | ReactJS |
| **Backend**  | RESTful API, C\#, .NET |
| **Database**  | MySQL 8 |
| **IDEs / Editors**  | Visual Studio Code 1.89 (Frontend), Visual Studio 2026 (Backend) |
| **Diagramming**  | Visual Paradigm 18.0, Draw.io |
| **Documentation**  | MS Word / Google Docs, Google Sheets / MS Excel  |
| **Version Control – Code**  | GitHub (Gitflow branching: main / develop / feature / hotfix)  |
| **Version Control – Docs**  | Google Drive (shared team folder, version naming convention)  |
| **Project Management**  | GitHub Projects (tasks, milestones)  |
| **Testing**  | JUnit 5 (unit), Postman (API), Selenium (UI automation)  |
| **Deployment**  | Docker \+ Railway / Render (staging); domain TBD (production)  |
| **Communication**  | Google Meet, Zalo  |

Installation/Usage Instructions:

---

# Addendum — Implementation note for UC-136

UC-136 Cancel Visit Request vẫn dùng cùng stack công nghệ hiện tại:

| Layer | Update |
|---|---|
| Frontend ReactJS | Thêm Cancel modal trong Delegation detail/list, validate reason theo cancellation source |
| Backend C#/.NET RESTful API | Thêm command `CancelVisitRequest` trong Delegations feature, dùng MediatR + FluentValidation |
| Database MySQL 8 | Dùng các cột `cancelled_by`, `cancelled_at`, `cancellation_actor_type`, `cancellation_source`, `cancellation_reason` |
| Testing Postman/Selenium | Bổ sung test case Visitor cancel, Host cancel by external confirmation, Staff Leader scope, HO multi-campus, Admin forbidden |

Không cần công nghệ mới cho UC này.
