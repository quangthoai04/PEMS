# Permission Matrix

## Purpose
The purpose of this document is to help AI tools, developers, testers, and stakeholders understand the system permission matrix clearly. It outlines the Role-Based Access Control (RBAC) rules for the system, mapping each use case (UC) to the specific permissions granted to different user roles.

## How to Read This Matrix
- The matrix uses roles as columns and feature areas/actions as rows.
- Each row represents a specific action or Use Case (UC) within the system.
- The intersecting cell between an action and a role indicates the level of access that role has for that action.
- The matrix is grouped by **Feature Area** to make it easier to read.

## Permission Code Legend
* 🟢 **F** = Full Access: user can create, read, update, delete, approve, configure, or fully manage the feature depending on the action.
* 🟡 **E** = Edit Access: user can edit, update, approve, process, or modify the feature but may not have full management control.
* 🔵 **R** = Read Access: user can only view, search, filter, or read information.
* 🟣 **O** = Own Access: user can only access or manage their own data, such as their own profile, email, event, or account-related action.
* ⚪ **—** = No Access: user is not allowed to access this action.

## Role Descriptions
1. **HO**: Head Office
2. **Admin**: System Administrator
3. **Staff Leader**: Leader of staff members
4. **Staff**: General staff member
5. **Department Lead**: Head of a specific department
6. **Department**: Department member
7. **Student**: Enrolled student
8. **Visitor**: Guest or unauthenticated user

## Permission Matrix

### Common
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-01 | View Homepage | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-02 | Search Information | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-03 | View Contact Info | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-04 | View Policy & Terms | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-05 | View FAQ | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-06 | View News | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-07 | View Partners | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-08 | View Gallery | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-09 | View Notifications | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |

### Authentication
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-10 | Login via SSO | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |
| UC-11 | Login via Credentials | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |
| UC-12 | Logout | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |
| UC-13 | Forgot Password | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |

### Profile management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-14 | View Profile | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |
| UC-15 | Update Profile | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |
| UC-16 | Change Password | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |

### Delegation Reception Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-17 | Submit Visit Request | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🟢 F |
| UC-18 | Approve Cross-Campus Request | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-19 | View Guest Delegation Details | 🔵 R | ⚪ — | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-20 | View Guest Delegation List | 🔵 R | ⚪ — | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-21 | Search Delegations | 🔵 R | ⚪ — | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-22 | Process Visit Request | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-23 | Create Guest Delegation | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-24 | Update Guest Delegation | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-25 | Prepare Visit Logistics | ⚪ — | ⚪ — | 🔵 R | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-26 | Update Visit Logistics | ⚪ — | ⚪ — | 🔵 R | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-27 | Confirm Participation | ⚪ — | ⚪ — | ⚪ — | 🟡 E | 🟡 E | 🟡 E | 🟡 E | ⚪ — |
| UC-28 | Approve Resource Request | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — |
| UC-29 | Propose Resource Modification | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🟢 F | 🟢 F | ⚪ — | ⚪ — |
| UC-30 | Confirm The Change Proposal | ⚪ — | ⚪ — | ⚪ — | 🟡 E | 🔵 R | 🔵 R | ⚪ — | ⚪ — |
| UC-31 | Create Meeting Minutes | ⚪ — | ⚪ — | ⚪ — | 🟢 F | 🟢 F | 🟢 F | 🟢 F | ⚪ — |
| UC-32 | Edit Meeting Minutes | ⚪ — | ⚪ — | ⚪ — | 🟢 F | 🟢 F | 🟢 F | 🟢 F | ⚪ — |
| UC-33 | View Meeting Minutes Details | 🔵 R | ⚪ — | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | ⚪ — |
| UC-34 | Submit Delegation Feedback | ⚪ — | ⚪ — | ⚪ — | 🟢 F | 🟢 F | 🟢 F | 🟢 F | ⚪ — |
| UC-35 | Scan Business Card | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-36 | Create Partner Profile | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-37 | Upload Attached Documents | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-38 | Upload Visit Photos | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | 🟢 F | ⚪ — |
| UC-39 | Tag Faces on Photos | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-40 | Create News Article | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | 🟢 F | ⚪ — |
| UC-41 | Close Delegation | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-42 | Configure Agenda Templates | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### Email Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-43 | Config Email Templates | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-44 | Edit Email Content | 🟣 O | ⚪ — | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |
| UC-45 | Send Email | 🟣 O | ⚪ — | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |
| UC-46 | View Email | 🔵 R | ⚪ — | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R |
| UC-47 | Reply to Email | 🟣 O | ⚪ — | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O | 🟣 O |

### Partner Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-48 | Process Partner Creation Request | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-49 | Edit Partner Information | ⚪ — | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-50 | View Partner Lists | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-51 | Search Partners | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-52 | View Partner Details | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### Document Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-53 | View Document List | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-54 | Search Documents | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### Gallery Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-55 | View Gallery Item List | ⚪ — | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-56 | Search Gallerys Items | ⚪ — | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-57 | Add Gallery Item | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-58 | Update Gallery Item | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-59 | Delete Gallery Item | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### Minutes Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-60 | View Minutes List | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-61 | Search/Filter Minutes | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### FAQ Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-62 | Create FAQ | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-63 | Update FAQ | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-64 | Change FAQ Visibility | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-65 | Search FAQ | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### Report Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-66 | View Dashboard Statistics | 🔵 R | ⚪ — | 🔵 R | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — |
| UC-67 | Export Statistics Report | 🟡 E | ⚪ — | 🟡 E | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — |
| UC-68 | Filter Dashboard By Time | 🔵 R | ⚪ — | 🔵 R | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — |

### Calendar Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-69 | View My Events | ⚪ — | ⚪ — | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | ⚪ — |
| UC-70 | View Department Calendar | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-71 | Switch View Mode | ⚪ — | ⚪ — | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | ⚪ — |
| UC-72 | Add Personal Event | ⚪ — | ⚪ — | 🟣 O | 🟣 O | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-73 | Delete Personal Event | ⚪ — | ⚪ — | 🟣 O | 🟣 O | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-74 | Update Personal Event | ⚪ — | ⚪ — | 🟣 O | 🟣 O | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-75 | View Event Details | ⚪ — | ⚪ — | 🔵 R | 🔵 R | 🔵 R | 🔵 R | 🔵 R | ⚪ — |

### Feedback Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-76 | Search/Filter Feedback | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-77 | View Feedback Summary | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### Campus Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-78 | Add New Campus | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-79 | View Campus List | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-80 | Search and Filter Campus | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-81 | View Campus Details | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-82 | Update Campus | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-83 | Manage Campus Status | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-84 | Assign Campus Lead | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### News Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-85 | Approve News | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-86 | Publish News | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | 🟢 F | ⚪ — |
| UC-87 | View News List | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | 🔵 R | ⚪ — |
| UC-88 | View News Details | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — | 🔵 R | ⚪ — |
| UC-89 | Add Multilingual News | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | 🟢 F | ⚪ — |
| UC-90 | Manage News Visibility | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-91 | Edit News | ⚪ — | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | 🟡 E | ⚪ — |

### Account Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-92 | View Account List | 🔵 R | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-93 | Create Account | 🟢 F | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-94 | Manage Account Status | 🟡 E | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-95 | View Account Details | 🔵 R | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-96 | Search and Filter Accounts | 🔵 R | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-97 | Update Account Role | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### Department Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-98 | Add New Department | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-99 | Update Department | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-100 | Search and Filter Departments | ⚪ — | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-101 | View Department List | ⚪ — | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-102 | View Department Details | ⚪ — | ⚪ — | 🔵 R | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — |
| UC-103 | Manage Department Status | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-104 | Add Department Personnel | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — |
| UC-105 | View Personnel Details | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — |
| UC-106 | Search Personnel | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — |
| UC-107 | Review Assigned Tasks | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🟡 E | ⚪ — | ⚪ — |
| UC-108 | Assign Tasks | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — |
| UC-109 | Sign The Service Delivery Report | ⚪ — | ⚪ — | ⚪ — | 🟡 E | 🟡 E | 🟡 E | ⚪ — | ⚪ — |
| UC-110 | Remove Personnel | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — |
| UC-111 | View Coordination Tasks | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — |
| UC-112 | Search Coordination Tasks | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🔵 R | 🔵 R | ⚪ — | ⚪ — |
| UC-113 | Reassign Department Lead | ⚪ — | ⚪ — | ⚪ — | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — |

### Role & Permission Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-114 | View Role List | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-115 | Create New Role | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-116 | Configure Role Permissions | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-117 | Update Role Details | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-118 | Disable/Delete Role | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

### API Management
| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | Visitor |
|---|---|---|---|---|---|---|---|---|---|
| UC-119 | View API Configuration | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-120 | Create API Configuration | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-121 | Update API Configuration | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-122 | Delete API Configuration | ⚪ — | 🟡 E | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-123 | Test API Connection | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-124 | Manage API Status | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-125 | Configure Request Limit | ⚪ — | 🟢 F | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-126 | View API Logs | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |
| UC-127 | Search API Logs | ⚪ — | 🔵 R | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — | ⚪ — |

## Access Control Notes
- **Usage**: The permission matrix is used to define and govern the access rights of various user roles across different features of the system.
- **Frontend Usage**: Frontend developers should use this matrix to show or hide buttons, menu items, forms, and specific actions to improve the user experience.
- **Backend Validation**: Frontend hiding is not enough; the backend must still strictly validate all permissions to ensure security.
- **Checking Strategy**: All permission checks should be based on a combination of **role**, **action**, and **ownership** rules.
- **"Own Access" (O) Meaning**: This means the system must verify that the requested resource belongs to the current user (e.g., viewing their own profile, editing their own event) before allowing the action.

## Data Quality Notes
- **Missing Permissions**: The original table contained empty cells for `HO` and `Admin` in the `UC-86 Publish News` row. These have been updated to `⚪ —` upon confirmation.
- **Consistency**: Roles "Department " and "VISITOR" from the raw data have been standardized to "Department" and "Visitor" for clarity and consistency.
- **UC IDs**: Ensure that the UC IDs are continuous from UC-01 to UC-127 as expected. Currently, there are 127 mapped use cases.
- **Role Validation**: Check whether `Visitor` and `Student` permissions are intentionally different (e.g., Visitor can "Submit Visit Request", but Students cannot, yet Students can "Upload Visit Photos" and "Create News Article" but Visitors cannot). This should be confirmed with the product owner.

## Implementation Notes for Developers
- Each UC ID should map to one permission rule.
- Each protected API endpoint should check the related UC permission.
- Actions with `—` must return access denied.
- Actions with `R` should not allow create/update/delete.
- Actions with `E` should allow modification but not necessarily full control.
- Actions with `F` should allow full management for that feature.
- Actions with `O` must check whether the resource belongs to the current user.
- The backend should be the source of truth for authorization.
- The frontend should only use this matrix for UI visibility and better user experience.

## QA / Testing Checklist
- [ ] Verify that each role can access only allowed actions.
- [ ] Verify that users with `—` cannot access the feature directly through URL or API.
- [ ] Verify that `R` users cannot edit or delete data.
- [ ] Verify that `E` users can edit only the allowed action.
- [ ] Verify that `F` users can fully manage the feature.
- [ ] Verify that `O` users can only access their own data.
- [ ] Verify that Visitor can only access public or guest-related actions.
- [ ] Verify that Admin can manage Role & Permission Management and API Management.
- [ ] Verify that HO can manage campus, FAQ, agenda template, and cross-campus approval actions.
- [ ] Verify that Staff and Staff Leader permissions are separated correctly.
