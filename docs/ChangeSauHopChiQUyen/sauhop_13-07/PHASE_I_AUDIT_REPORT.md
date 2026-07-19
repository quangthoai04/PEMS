# Phase I Zero-Unclassified Audit Report

## Details
| Field | File:line | Category | Read/write | Runtime caller/consumer | V1/V2 behavior | Blocker? | Action before execution |
|---|---|---|---|---|---|---|---|
| Purpose | backend/PEMS.Infrastructure/DependencyInjection.cs:64 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Api/Controllers/FilesController.cs:44 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Api/Email/EmailActionHtmlPages.cs:31 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Api/Email/EmailActionHtmlPages.cs:108 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Api/Email/EmailActionHtmlPages.cs:109 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommand.cs:26 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs:37 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs:41 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs:44 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs:121 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs:189 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs:237 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs:354 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs:480 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/EmailActions/GetEmailActionInfoQuery.cs:34 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs:34 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs:38 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs:41 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs:102 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs:166 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs:226 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Domain/Constants/AuthConstants.cs:93 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Domain/Constants/VisitRequestConstants.cs:21 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Constants/VisitTypes.cs:9 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Enums/NewEnums.cs:9 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Domain/Enums/NewEnums.cs:19 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Domain/Enums/OtpPurpose.cs:3 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/BackgroundJobs/HoUnprocessedCampusAlertHostedService.cs:76 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/BackgroundJobs/HoUnprocessedCampusAlertHostedService.cs:101 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/BackgroundJobs/VisitReminderDispatchHostedService.cs:119 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/BackgroundJobs/VisitReminderDispatchHostedService.cs:167 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Identity/OtpService.cs:73 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Identity/OtpService.cs:93 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Identity/OtpService.cs:125 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Identity/OtpService.cs:188 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Identity/OtpService.cs:231 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Identity/OtpService.cs:309 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Identity/OtpService.cs:363 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Identity/OtpService.cs:402 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:199 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:200 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:201 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:201 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:202 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:203 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:204 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:437 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:438 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:439 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:439 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:440 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:440 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:441 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:442 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs:443 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitContactClaimService.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitContactClaimService.cs:81 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitContactClaimService.cs:115 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitContactClaimService.cs:145 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:149 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:151 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:152 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:152 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:153 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:154 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:159 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:160 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:161 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:162 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestService.cs:163 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:60 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:60 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2Canonical.cs:60 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:145 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:146 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:147 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:147 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:148 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:149 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:154 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:155 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:156 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:157 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:158 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:184 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:185 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:186 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:186 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:187 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:188 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:196 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:197 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:198 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:199 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:200 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:359 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:359 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:359 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:359 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:359 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:361 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:361 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:361 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:361 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs:361 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:99 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:100 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:101 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:101 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:102 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:103 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:109 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:110 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:111 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:112 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:113 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:125 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:126 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:127 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:127 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:128 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:129 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:134 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:135 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:136 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:137 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:138 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:150 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:150 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:150 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:150 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:150 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:152 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:152 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:152 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:152 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:152 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:320 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:321 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:322 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:322 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:323 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:324 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:325 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:326 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:327 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:328 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:329 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:553 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:554 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:555 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:555 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:556 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:557 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:558 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:559 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:560 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:561 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:562 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:734 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:734 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:734 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:734 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:734 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:740 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:740 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:740 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:740 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs:740 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:94 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:99 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:100 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:101 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:102 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:103 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:104 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:105 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:106 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:130 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:288 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:289 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:290 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:290 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:291 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:292 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:293 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:294 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:295 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:296 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:297 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:313 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:313 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:313 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:313 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:313 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:321 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:321 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:321 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:321 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs:322 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Common/AgendaDefaultResolver.cs:25 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Common/AgendaDefaultResolver.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Common/AgendaDefaultResolver.cs:68 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Common/AgendaTemplateContracts.cs:34 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Common/AgendaTemplateContracts.cs:45 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationConstants.cs:7 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationConstants.cs:17 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationConstants.cs:38 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationDtos.cs:16 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs:8 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs:9 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs:11 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs:12 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs:18 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs:29 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Campuses/Common/CampusStatusImpact.cs:18 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Campuses/Common/CampusStatusImpact.cs:106 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs:22 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs:25 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:51 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:51 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:53 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:54 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:65 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:66 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:67 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs:68 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:33 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:34 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:36 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:47 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:49 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:28 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:29 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:29 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:30 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:31 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:44 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:51 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:51 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:52 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs:52 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Common/DTOs/VisitFormV2SafeEditDtos.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Common/DTOs/VisitFormV2SafeEditDtos.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Common/DTOs/VisitFormV2SafeEditDtos.cs:28 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Common/DTOs/VisitFormV2SafeEditDtos.cs:29 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileObjectKeyBuilder.cs:16 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:10 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:54 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:80 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:82 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:83 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:85 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:86 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:87 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:88 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:89 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:90 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:91 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:92 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:93 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:94 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:95 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:96 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:97 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:98 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:99 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:100 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:101 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:102 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:103 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:104 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:105 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:106 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:113 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:115 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:116 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:117 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:118 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:119 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:120 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:121 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:122 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:123 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:124 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:125 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:126 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:127 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:128 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:129 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FilePurpose.cs:130 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileUploadService.cs:51 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileUploadService.cs:62 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileUploadService.cs:73 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileUploadService.cs:113 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:51 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:52 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:53 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:54 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:62 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:63 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:73 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:83 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:92 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:93 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:101 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:113 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationPolicy.cs:131 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Files/FileValidationRule.cs:4 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/Common/Interfaces/IFileObjectKeyBuilder.cs:12 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Interfaces/IFileStorageFolderResolver.cs:6 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/Common/Interfaces/IFileStorageFolderResolver.cs:13 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Interfaces/IFileUploadService.cs:6 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/Common/Interfaces/IFileUploadService.cs:18 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Interfaces/IFileUploadService.cs:32 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Common/Interfaces/IFileValidationPolicy.cs:6 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/Common/Interfaces/IFileValidationPolicy.cs:12 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:20 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:22 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:25 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:34 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:36 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:46 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:55 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:136 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:141 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:141 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:143 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:145 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:150 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs:155 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Common/ScheduleConflictResolver.cs:91 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Common/ScheduleConflictResolver.cs:93 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Common/ScheduleConflictResolver.cs:94 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Common/ScheduleConflictResolver.cs:119 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Common/ScheduleConflictResolver.cs:121 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Common/ScheduleConflictResolver.cs:122 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Common/ScheduleConflictResolver.cs:133 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/News/CreateVisitInstanceNewsCommandHandler.cs:57 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Application/Delegations/News/SubmitVisitInstanceNewsCommandHandler.cs:60 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Application/Delegations/News/UpdateVisitInstanceNewsCommandHandler.cs:63 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:36 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:38 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:39 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:44 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:45 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:46 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:67 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:68 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:69 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:71 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:73 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:74 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:75 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:75 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:76 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:77 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:78 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs:102 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:44 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:46 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:47 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:47 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:99 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:99 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:99 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:104 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:105 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:105 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitRequestFingerprintBuilder.cs:111 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/VisitPhotos/VisitPhotoFolderService.cs:57 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryAreaCoverVideo.cs:17 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryAreaCoverVideo.cs:36 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryContentRules.cs:23 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryCoverImage.cs:34 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryCoverMediaType.cs:18 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryCoverMediaType.cs:21 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryCoverMediaType.cs:33 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryCoverMediaType.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryDetailBuilder.cs:106 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryDetailBuilder.cs:115 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryExternalMediaService.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryExternalMediaService.cs:49 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryItemListQueryExecutor.cs:144 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryItemListQueryExecutor.cs:189 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryItemListQueryExecutor.cs:228 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryLocationDetailBuilder.cs:49 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryLocationDetailBuilder.cs:52 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryMediaClassifier.cs:9 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryMediaClassifier.cs:28 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryMediaClassifier.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryMediaSourceResolver.cs:17 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryMediaSourceResolver.cs:18 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryMediaSourceResolver.cs:20 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Common/GalleryMediaSourceResolver.cs:22 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Entities/AgendaTemplates/AgendaTemplate.cs:23 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Entities/AgendaTemplates/AgendaTemplate.cs:25 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Entities/AgendaTemplates/AgendaTemplateDefault.cs:25 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Entities/AgendaTemplates/AgendaTemplateDefault.cs:27 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Domain/Entities/ApiIntegrations/ApiConfiguration.cs:23 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove before UP |
| DelegationName | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:22 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:25 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:28 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:28 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:31 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:34 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:53 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:56 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:59 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:62 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Domain/Entities/Delegations/VisitInstanceFormDetail.cs:65 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:84 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:90 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:93 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:93 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:95 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:98 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:117 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:123 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:126 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:129 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs:132 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Domain/Entities/Documents/UploadedFile.cs:47 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Domain/Entities/Emails/EmailTemplate.cs:21 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Domain/Entities/Users/OtpToken.cs:23 | EF/schema mapping required before drop | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:10 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:11 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:20 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:28 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:29 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:31 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:40 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:44 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:45 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:46 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:47 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:56 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:57 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:58 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:59 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:60 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:61 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:62 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs:65 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Accounts/Queries/RelatedVisitors/GetRelatedVisitorDetailsQueryHandler.cs:69 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Accounts/Queries/RelatedVisitors/GetRelatedVisitorDetailsQueryHandler.cs:71 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Accounts/Queries/RelatedVisitors/GetRelatedVisitorDetailsQueryHandler.cs:72 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Accounts/Queries/RelatedVisitors/RelatedVisitorAccountDetailDto.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/ApplyAgendaTemplate/ApplyAgendaTemplateCommandHandler.cs:68 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/ApplyAgendaTemplate/ApplyAgendaTemplateCommandHandler.cs:69 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/ApplyAgendaTemplate/ApplyAgendaTemplateCommandHandler.cs:137 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/ApplyAgendaTemplate/ApplyAgendaTemplateCommandHandler.cs:142 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/ApplyAgendaTemplate/ApplyAgendaTemplateResponse.cs:10 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/ApplyAgendaTemplate/ApplyAgendaTemplateResponse.cs:11 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/ApplyAgendaTemplate/ApplyAgendaTemplateResponse.cs:12 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/CreateAgendaTemplate/CreateAgendaTemplateCommand.cs:17 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/CreateAgendaTemplate/CreateAgendaTemplateCommandHandler.cs:49 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/CreateAgendaTemplate/CreateAgendaTemplateCommandHandler.cs:63 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/CreateAgendaTemplate/CreateAgendaTemplateCommandValidator.cs:12 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/CreateAgendaTemplate/CreateAgendaTemplateCommandValidator.cs:13 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/SetAgendaTemplateDefault/SetAgendaTemplateDefaultCommand.cs:10 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/SetAgendaTemplateDefault/SetAgendaTemplateDefaultCommandHandler.cs:45 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/SetAgendaTemplateDefault/SetAgendaTemplateDefaultCommandHandler.cs:53 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/SetAgendaTemplateDefault/SetAgendaTemplateDefaultCommandHandler.cs:62 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/SetAgendaTemplateDefault/SetAgendaTemplateDefaultCommandHandler.cs:91 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/SetAgendaTemplateDefault/SetAgendaTemplateDefaultCommandValidator.cs:10 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/SetAgendaTemplateDefault/SetAgendaTemplateDefaultCommandValidator.cs:11 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/SetAgendaTemplateDefault/SetAgendaTemplateDefaultResponse.cs:7 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/UpdateAgendaTemplate/UpdateAgendaTemplateCommand.cs:15 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/UpdateAgendaTemplate/UpdateAgendaTemplateCommandHandler.cs:60 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/UpdateAgendaTemplate/UpdateAgendaTemplateCommandHandler.cs:73 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/UpdateAgendaTemplate/UpdateAgendaTemplateCommandValidator.cs:13 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Commands/UpdateAgendaTemplate/UpdateAgendaTemplateCommandValidator.cs:14 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetAgendaSetupForInstance/GetAgendaSetupForInstanceDto.cs:12 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetAgendaSetupForInstance/GetAgendaSetupForInstanceDto.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetAgendaSetupForInstance/GetAgendaSetupForInstanceQueryHandler.cs:59 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetAgendaSetupForInstance/GetAgendaSetupForInstanceQueryHandler.cs:64 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetAgendaSetupForInstance/GetAgendaSetupForInstanceQueryHandler.cs:84 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetAgendaSetupForInstance/GetAgendaSetupForInstanceQueryHandler.cs:90 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetDefaultAgendaTemplate/GetDefaultAgendaTemplateQuery.cs:9 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetDefaultAgendaTemplate/GetDefaultAgendaTemplateQueryHandler.cs:27 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/GetDefaultAgendaTemplate/GetDefaultAgendaTemplateQueryHandler.cs:31 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateDefaults/ViewAgendaTemplateDefaultsDto.cs:9 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateDefaults/ViewAgendaTemplateDefaultsQueryHandler.cs:43 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateDefaults/ViewAgendaTemplateDefaultsQueryHandler.cs:48 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateDetail/ViewAgendaTemplateDetailDto.cs:10 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateDetail/ViewAgendaTemplateDetailQueryHandler.cs:54 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateList/ViewAgendaTemplateListQuery.cs:10 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateList/ViewAgendaTemplateListQueryHandler.cs:43 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateList/ViewAgendaTemplateListQueryHandler.cs:44 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateList/ViewAgendaTemplateListQueryHandler.cs:49 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/AgendaTemplates/Queries/ViewAgendaTemplateList/ViewAgendaTemplateListQueryHandler.cs:74 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs:46 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs:47 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs:50 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs:65 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs:102 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs:105 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/UpsertGoogleDocumentAiOcrConfig/UpsertGoogleDocumentAiOcrConfigCommandHandler.cs:52 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/UpsertGoogleDocumentAiOcrConfig/UpsertGoogleDocumentAiOcrConfigCommandHandler.cs:56 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/UpsertGoogleDocumentAiOcrConfig/UpsertGoogleDocumentAiOcrConfigCommandHandler.cs:74 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/ApiIntegrations/Commands/UpsertGoogleTranslationConfig/UpsertGoogleTranslationConfigCommandHandler.cs:54 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/ApiIntegrations/Queries/GetApiIntegrations/GetApiIntegrationsQuery.cs:10 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/ApiIntegrations/Queries/GetApiIntegrations/GetApiIntegrationsQueryHandler.cs:30 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/ApiIntegrations/Queries/GetApiIntegrations/GetApiIntegrationsQueryHandler.cs:31 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Authentication/Commands/ForgotPassword/ForgotPasswordCommandHandler.cs:53 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Authentication/Commands/ResetPassword/ResetPasswordCommandHandler.cs:44 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/BusinessCardOcr/Commands/ScanBusinessCard/ScanBusinessCardCommandHandler.cs:160 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/DepartmentLeaderDashboardSummaryDto.cs:13 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/DepartmentLeaderDashboardSummaryDto.cs:28 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:115 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:117 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:118 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:142 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:144 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:145 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:170 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:172 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:173 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:194 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:196 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetDepartmentLeaderDashboardSummary/GetDepartmentLeaderDashboardSummaryQueryHandler.cs:197 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetHODashboardOverview/GetHODashboardOverviewQueryHandler.cs:84 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetHODashboardOverview/GetHODashboardOverviewQueryHandler.cs:99 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetHODashboardOverview/GetHODashboardOverviewQueryHandler.cs:100 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendar/GetStaffCalendarQueryHandler.cs:114 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendar/GetStaffCalendarQueryHandler.cs:115 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendar/GetStaffCalendarQueryHandler.cs:116 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendar/GetStaffCalendarQueryHandler.cs:156 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendar/GetStaffCalendarQueryHandler.cs:157 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendar/StaffCalendarDtos.cs:51 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:134 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:138 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:138 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:139 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:139 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:140 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:141 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:141 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:142 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:142 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:148 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:152 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:152 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:153 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:153 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:154 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:155 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:155 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:156 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:156 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:196 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:215 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:216 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:217 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:218 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:218 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:220 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:221 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:222 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:223 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/GetStaffCalendarDetailQueryHandler.cs:224 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:28 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:54 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:55 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:56 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:57 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:57 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:59 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:60 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:61 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:63 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Dashboard/Queries/GetStaffCalendarDetail/StaffCalendarDetailDto.cs:64 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Commands/ApproveCampusInstance/ApproveCampusInstanceCommandHandler.cs:216 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/ApproveCampusInstance/ApproveCampusInstanceCommandHandler.cs:221 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs:95 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs:103 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs:110 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:36 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:38 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:39 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:39 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:53 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:54 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:55 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommand.cs:56 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:224 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:226 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:227 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:227 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:282 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:284 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:285 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:285 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:287 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:288 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:293 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:294 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:295 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:296 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:415 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:449 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:467 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:487 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateAuthenticatedVisitRequest/CreateAuthenticatedVisitRequestCommandHandler.cs:527 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:106 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:124 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:126 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:128 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:129 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:129 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:131 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:132 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:134 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:150 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:152 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs:156 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/V2CreateNotifier.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/V2CreateNotifier.cs:62 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:21 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:38 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:39 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:40 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommandHandler.cs:54 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequestV2/InitiateVisitRequestV2Command.cs:65 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequestV2/InitiateVisitRequestV2Command.cs:65 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequestV2/InitiateVisitRequestV2Command.cs:65 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/InitiateVisitRequestV2/InitiateVisitRequestV2CommandHandler.cs:91 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommandHandler.cs:111 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommandHandler.cs:116 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/PrepareVisitLogistics/PrepareVisitLogisticsCommandHandler.cs:234 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/Commands/RecoverVisitRequestOtp/RecoverVisitRequestOtpCommandHandler.cs:55 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/RejectCampusInstance/RejectCampusInstanceCommandHandler.cs:119 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/RejectCampusInstance/RejectCampusInstanceCommandHandler.cs:124 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/Commands/ResendVisitRequestOtp/ResendVisitRequestOtpCommandHandler.cs:35 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:29 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:30 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommand.cs:44 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:228 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:229 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:230 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:230 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:231 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:232 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:236 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:237 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:238 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:239 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:240 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/ResubmitRejectedVisitRequestCommandHandler.cs:334 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequestV2/ResubmitRejectedVisitRequestV2CommandHandler.cs:125 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:25 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:28 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:29 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:40 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommand.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:169 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:171 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:172 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:172 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:173 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:174 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:178 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:179 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:180 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:181 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| NoteToFptu | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:182 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/UpdatePendingVisitRequestCommandHandler.cs:295 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequestV2/UpdatePendingVisitRequestV2CommandHandler.cs:115 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/Commands/UploadVisitPhotos/UploadVisitPhotosCommandHandler.cs:69 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:22 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:25 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:25 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:28 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:39 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:40 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:41 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:92 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:158 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:160 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:161 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:161 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:163 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:164 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:169 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:170 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:171 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:172 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:245 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:266 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:320 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs:333 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequestV2/VerifyAndCreateVisitRequestV2CommandHandler.cs:97 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| TransportationNote | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:34 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:38 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:39 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:40 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:60 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:61 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:64 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:64 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:66 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:67 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:68 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs:69 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VisitContactClaim/GetVisitContactClaimInfoQueryHandler.cs:64 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VisitContactClaim/GetVisitContactClaimInfoQueryHandler.cs:81 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VisitContactClaim/VisitContactClaimContracts.cs:32 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VisitContactTransfer/GetVisitContactTransferInfoQueryHandler.cs:65 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VisitContactTransfer/GetVisitContactTransferInfoQueryHandler.cs:85 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Commands/VisitContactTransfer/VisitContactTransferContracts.cs:107 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetCreateHostCandidates/GetCreateHostCandidatesQueryHandler.cs:115 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetCreateHostCandidates/GetCreateHostCandidatesQueryHandler.cs:117 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetCreateHostCandidates/GetCreateHostCandidatesQueryHandler.cs:118 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetCreateHostCandidates/GetCreateHostCandidatesQueryHandler.cs:138 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:32 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:33 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:34 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:34 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:36 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:45 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:46 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:47 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:48 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/EditableVisitRequestDetailDto.cs:56 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:125 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:126 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:127 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:127 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:128 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:129 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:130 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:131 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:132 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:133 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:134 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:154 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:155 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:156 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:156 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:157 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:158 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:159 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:160 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:161 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:162 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:163 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:185 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:186 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:187 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:187 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:188 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:189 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:196 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:197 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:198 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:199 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/GetEditableVisitRequestDetailQueryHandler.cs:204 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetHostCandidates/GetHostCandidatesQueryHandler.cs:139 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetHostCandidates/GetHostCandidatesQueryHandler.cs:141 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetHostCandidates/GetHostCandidatesQueryHandler.cs:142 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetHostCandidates/GetHostCandidatesQueryHandler.cs:153 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:224 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:225 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:226 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:226 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:227 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:228 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:229 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:230 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:231 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:232 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:233 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:253 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:254 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:255 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:255 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:256 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:257 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:258 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:259 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:260 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:261 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:262 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:378 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:379 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:380 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:380 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:381 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:382 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:401 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:402 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:403 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:404 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs:405 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:36 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:37 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:38 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:39 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:45 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:46 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:47 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/SubmittedVisitRequestFormDetailDto.cs:51 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/ContributionPageDto.cs:63 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:196 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:197 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:197 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:198 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:198 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:199 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:200 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:200 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:201 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:201 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:213 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:214 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:214 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:215 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:215 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:216 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:217 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:217 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:218 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:218 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:240 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:242 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:243 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:243 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:244 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:245 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:246 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:247 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:248 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:249 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:250 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:330 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs:412 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:85 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:86 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:86 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:87 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:87 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:88 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:89 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:89 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:90 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:90 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:102 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:103 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:103 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:104 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:104 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:105 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:106 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:106 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:107 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:107 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:129 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:131 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:132 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:132 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:133 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:134 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:135 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:136 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:137 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:138 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:139 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs:259 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/ProcessSummaryPageDto.cs:39 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitationDetail/GetVisitInvitationDetailQueryHandler.cs:56 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitationDetail/GetVisitInvitationDetailQueryHandler.cs:58 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitationDetail/GetVisitInvitationDetailQueryHandler.cs:63 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitationDetail/GetVisitInvitationDetailQueryHandler.cs:95 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitationDetail/VisitInvitationDetailDto.cs:13 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/GetVisitInvitationsQueryHandler.cs:83 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/GetVisitInvitationsQueryHandler.cs:84 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/GetVisitInvitationsQueryHandler.cs:124 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/GetVisitInvitationsQueryHandler.cs:125 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/GetVisitInvitationsQueryHandler.cs:126 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/GetVisitInvitationsQueryHandler.cs:165 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/InvitationListItemDto.cs:13 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:183 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:184 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:184 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:185 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:185 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:186 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:187 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:187 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:188 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:188 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:200 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:201 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:201 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:202 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:202 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:203 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:204 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:204 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:205 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:205 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:228 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:230 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:231 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:231 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:232 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:233 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:234 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:235 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:236 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:237 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:238 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs:361 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:9 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:81 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:83 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:84 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:84 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:85 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:86 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:87 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:88 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:89 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:91 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/VisitProcessDetailDto.cs:92 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs:11 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs:213 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:216 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:217 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:348 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:349 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:350 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:442 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:448 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:458 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:543 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:544 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:799 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:806 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:821 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs:823 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/GetVisitInvitationByIdQueryHandler.cs:67 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/GetVisitInvitationByIdQueryHandler.cs:69 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/GetVisitInvitationByIdQueryHandler.cs:70 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/GetVisitInvitationByIdQueryHandler.cs:89 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/GetVisitInvitationByIdQueryHandler.cs:90 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/GetVisitInvitationByIdQueryHandler.cs:91 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:79 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:80 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:81 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:83 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:84 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:85 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:86 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:87 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/ViewMyVisitInvitationsQueryHandler.cs:88 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationDto.cs:17 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationDto.cs:31 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationDto.cs:32 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationProjection.cs:28 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationProjection.cs:30 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationProjection.cs:31 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationProjection.cs:61 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationProjection.cs:68 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Queries/ViewMyVisitInvitations/VisitInvitationProjection.cs:69 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:92 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:93 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:94 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:94 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:95 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:96 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:100 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:101 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:102 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:103 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFormRead/ResolvedVisitFormDto.cs:104 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:17 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:18 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:19 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:19 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:20 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:21 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:22 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:25 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitCampusFormContent.cs:26 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:203 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:204 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:205 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:205 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:206 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:207 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:208 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:209 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:210 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:211 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:212 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:240 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:241 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:242 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:242 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:243 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:244 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:245 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:246 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:247 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:248 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:249 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:280 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:281 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:282 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:282 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:283 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:284 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:288 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:289 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:290 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:291 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:292 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:380 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:381 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:382 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:382 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:383 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:384 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:385 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:386 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:387 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:388 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:389 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:444 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:445 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitType | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:446 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| VisitTypeOther | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:446 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:447 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:448 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingLanguage | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:449 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentStatus | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:450 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| MediaConsentNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:451 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| TransportationNote | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:452 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| NoteToFptu | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:453 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitInstanceEffectiveName.cs:33 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitInstanceEffectiveName.cs:34 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitInstanceEffectiveName.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitInstanceEffectiveName.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:45 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:160 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:162 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:163 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:226 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:283 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:285 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:286 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:287 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:289 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:290 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:332 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:339 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs:391 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:101 | Documentation/comment-only | Read | Various | Compatibility | No | None |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:106 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:109 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:110 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:153 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:154 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:162 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:196 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:199 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:200 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar/GetDepartmentCalendarQuery.cs:227 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:42 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:43 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:92 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:93 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:94 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:102 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:103 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:104 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:114 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:132 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail/GetInvitationDetailQuery.cs:133 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:35 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:81 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:82 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:228 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:229 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:230 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:238 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:239 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:240 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:250 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:293 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| WorkingContent | backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail/GetRequestDetailQuery.cs:294 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Documents/Queries/ViewDocumentDetail/ViewDocumentDetailDto.cs:50 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Documents/Queries/ViewDocumentDetail/ViewDocumentDetailQueryHandler.cs:91 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Documents/Queries/ViewDocumentDetail/ViewDocumentDetailQueryHandler.cs:92 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Documents/Queries/ViewDocumentDetail/ViewDocumentDetailQueryHandler.cs:144 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Documents/Queries/ViewDocumentDetail/ViewDocumentDetailQueryHandler.cs:145 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Documents/Queries/ViewDocumentDetail/ViewDocumentDetailQueryHandler.cs:191 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Commands/CreateEmailTemplate/CreateEmailTemplateCommand.cs:10 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Commands/CreateEmailTemplate/CreateEmailTemplateCommandHandler.cs:26 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Emails/Commands/UpdateEmailTemplate/UpdateEmailTemplateCommand.cs:10 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Commands/UpdateEmailTemplate/UpdateEmailTemplateCommandHandler.cs:32 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Emails/Queries/ViewEmailTemplateDetail/ViewEmailTemplateDetailDto.cs:10 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Queries/ViewEmailTemplateDetail/ViewEmailTemplateDetailQueryHandler.cs:35 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Queries/ViewEmailTemplateList/ViewEmailTemplateListDto.cs:18 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Queries/ViewEmailTemplateList/ViewEmailTemplateListQuery.cs:10 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Queries/ViewEmailTemplateList/ViewEmailTemplateListQueryHandler.cs:43 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Queries/ViewEmailTemplateList/ViewEmailTemplateListQueryHandler.cs:45 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Emails/Queries/ViewEmailTemplateList/ViewEmailTemplateListQueryHandler.cs:63 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Commands/SubmitVisitFeedback/SubmitVisitFeedbackCommandHandler.cs:81 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Feedbacks/Commands/SubmitVisitFeedback/SubmitVisitFeedbackCommandHandler.cs:83 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Feedbacks/Commands/SubmitVisitFeedback/SubmitVisitFeedbackCommandHandler.cs:94 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Feedbacks/Commands/SubmitVisitFeedback/SubmitVisitFeedbackCommandHandler.cs:143 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Feedbacks/Commands/SubmitVisitFeedback/SubmitVisitFeedbackCommandHandler.cs:166 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Feedbacks/Commands/SubmitVisitFeedback/SubmitVisitFeedbackCommandHandler.cs:285 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Feedbacks/Commands/SubmitVisitFeedback/SubmitVisitFeedbackCommandHandler.cs:292 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetMyHostFeedback/GetMyHostFeedbackQuery.cs:19 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetMyHostFeedback/GetMyHostFeedbackQueryHandler.cs:61 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetMyHostFeedback/GetMyHostFeedbackQueryHandler.cs:70 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetPendingFeedbackNotifications/GetPendingFeedbackNotificationsQuery.cs:29 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetPendingFeedbackNotifications/GetPendingFeedbackNotificationsQueryHandler.cs:47 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetPendingFeedbackNotifications/GetPendingFeedbackNotificationsQueryHandler.cs:48 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetPendingFeedbackNotifications/GetPendingFeedbackNotificationsQueryHandler.cs:49 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetPendingFeedbackNotifications/GetPendingFeedbackNotificationsQueryHandler.cs:75 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQuery.cs:24 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQueryHandler.cs:55 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQueryHandler.cs:68 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQueryHandler.cs:88 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQueryHandler.cs:89 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQueryHandler.cs:107 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQueryHandler.cs:123 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQueryHandler.cs:145 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitFeedbackTargets/GetVisitFeedbackTargetsQueryHandler.cs:177 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitorFeedback/GetVisitorFeedbackQuery.cs:18 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitorFeedback/GetVisitorFeedbackQueryHandler.cs:72 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/GetVisitorFeedback/GetVisitorFeedbackQueryHandler.cs:81 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/SearchAndFilterFeedback/SearchAndFilterFeedbackQueryHandler.cs:98 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/SearchAndFilterFeedback/SearchAndFilterFeedbackQueryHandler.cs:99 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/ViewFeedbackSummary/ViewFeedbackSummaryQueryHandler.cs:67 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/ViewFeedbackSummary/ViewFeedbackSummaryQueryHandler.cs:68 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Feedbacks/Queries/ViewFeedbackSummary/ViewFeedbackSummaryQueryHandler.cs:113 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Files/Commands/UploadFile/UploadFileCommand.cs:16 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Files/Commands/UploadFile/UploadFileCommandHandler.cs:17 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Files/Commands/UploadFile/UploadFileCommandHandler.cs:68 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Files/Commands/UploadFile/UploadFileCommandHandler.cs:71 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Files/Commands/UploadFile/UploadFileCommandHandler.cs:76 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Files/Commands/UploadFile/UploadFileCommandHandler.cs:92 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Files/Queries/GetFileContent/GetFileContentQueryHandler.cs:39 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Commands/AddGalleryItem/AddGalleryItemCommandHandler.cs:24 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Galleries/Commands/AddGalleryItem/AddGalleryItemCommandHandler.cs:218 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Galleries/Commands/CreateGalleryLocation/CreateGalleryLocationCommandHandler.cs:189 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Galleries/Commands/UpdateGalleryItem/UpdateGalleryItemCommandHandler.cs:318 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Galleries/Public/Common/PublicGalleryMediaFactory.cs:22 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Public/Common/PublicGalleryMediaFactory.cs:27 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Queries/GetGalleryFilterOptions/GetGalleryFilterOptionsQueryHandler.cs:46 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Queries/GetGalleryFilterOptions/GetGalleryFilterOptionsQueryHandler.cs:49 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Queries/GetGalleryFilterOptions/GetGalleryFilterOptionsQueryHandler.cs:51 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Queries/ViewGalleryLocationList/ViewGalleryLocationListQueryHandler.cs:152 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Queries/ViewGalleryLocationList/ViewGalleryLocationListQueryHandler.cs:161 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Queries/ViewGalleryLocationList/ViewGalleryLocationListQueryHandler.cs:163 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/MeetingMinutes/Queries/ExportMinutes/ExportMinutesExcelQueryHandler.cs:40 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/MeetingMinutes/Queries/ExportMinutes/ExportMinutesExcelQueryHandler.cs:56 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/MeetingMinutes/Queries/SearchAndFilterMinutes/SearchAndFilterMinutesQueryHandler.cs:190 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/MeetingMinutes/Queries/SearchAndFilterMinutes/SearchAndFilterMinutesQueryHandler.cs:191 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| MediaConsentStatus | backend/PEMS.Application/News/Commands/CreateNews/CreateNewsCommandHandler.cs:65 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Application/News/Commands/CreateNews/CreateNewsCommandHandler.cs:80 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/News/Commands/UploadNewsCoverImage/UploadNewsCoverImageCommandHandler.cs:53 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| MediaConsentStatus | backend/PEMS.Application/News/Queries/GetEligibleVisitInstancesForNews/GetEligibleVisitInstancesForNewsQueryHandler.cs:65 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Profiles/Commands/UploadProfileAvatar/UploadProfileAvatarCommandHandler.cs:13 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Profiles/Commands/UploadProfileAvatar/UploadProfileAvatarCommandHandler.cs:60 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderInvoice/ExportDeptLeaderInvoiceCommandHandler.cs:67 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderInvoice/ExportDeptLeaderInvoiceCommandHandler.cs:80 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderInvoice/ExportDeptLeaderInvoiceCommandHandler.cs:87 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderInvoice/ExportDeptLeaderInvoiceCommandHandler.cs:146 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderInvoice/ExportDeptLeaderInvoiceCommandHandler.cs:180 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderInvoice/ExportDeptLeaderInvoiceCommandHandler.cs:252 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:221 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:251 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:284 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:290 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:384 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:420 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:455 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:458 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:596 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:675 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportDeptLeaderReport/ExportDeptLeaderReportCommandHandler.cs:683 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommand.cs:19 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:64 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:133 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:196 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:220 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:238 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:240 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:395 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:461 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:514 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:529 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:757 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportHoReport/ExportHoReportCommandHandler.cs:793 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:240 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:240 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:262 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:285 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:291 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:425 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| VisitType | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:425 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:452 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:474 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:477 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:650 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:677 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:709 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/ExportStaffLeaderReport/ExportStaffLeaderReportCommandHandler.cs:718 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderInvoiceToStaffLeader/SendDeptLeaderInvoiceToStaffLeaderCommand.cs:102 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderInvoiceToStaffLeader/SendDeptLeaderInvoiceToStaffLeaderCommand.cs:104 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderInvoiceToStaffLeader/SendDeptLeaderInvoiceToStaffLeaderCommand.cs:105 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderInvoiceToStaffLeader/SendDeptLeaderInvoiceToStaffLeaderCommand.cs:116 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs:82 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs:84 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs:85 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs:102 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs:104 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs:105 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs:113 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs:114 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderDeptInvoice/SendStaffLeaderDeptInvoiceCommand.cs:101 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderDeptInvoice/SendStaffLeaderDeptInvoiceCommand.cs:103 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderDeptInvoice/SendStaffLeaderDeptInvoiceCommand.cs:104 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderDeptInvoice/SendStaffLeaderDeptInvoiceCommand.cs:115 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs:93 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs:95 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs:96 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs:103 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs:119 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs:121 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs:122 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs:128 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderInvoiceData/GetDeptLeaderInvoiceVisitsQuery.cs:22 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderInvoiceData/GetDeptLeaderInvoiceVisitsQueryHandler.cs:48 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderInvoiceData/GetDeptLeaderInvoiceVisitsQueryHandler.cs:50 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderInvoiceData/GetDeptLeaderInvoiceVisitsQueryHandler.cs:51 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/DeptLeaderReportOverviewDto.cs:115 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/DeptLeaderReportOverviewDto.cs:147 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/DeptLeaderReportOverviewDto.cs:183 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:260 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:262 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:263 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:304 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:306 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:307 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:328 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:480 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:656 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:660 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:661 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportOverview/GetDeptLeaderReportOverviewQueryHandler.cs:670 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderInvoiceItemsV2Query.cs:34 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderInvoiceItemsV2Query.cs:93 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderInvoiceItemsV2Query.cs:95 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderInvoiceItemsV2Query.cs:96 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderInvoiceItemsV2Query.cs:147 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderReportV2QueryHandler.cs:62 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderReportV2QueryHandler.cs:64 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderReportV2QueryHandler.cs:65 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderReportV2QueryHandler.cs:87 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderReportV2QueryHandler.cs:89 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetDeptLeaderReportV2/GetDeptLeaderReportV2QueryHandler.cs:90 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQuery.cs:23 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:76 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:86 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:87 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:98 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:99 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:109 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:110 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:120 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:121 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:289 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:291 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:307 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:333 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:335 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:336 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:378 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:424 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:426 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:427 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:434 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:451 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs:741 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/HoReportOverviewDto.cs:42 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/HoReportOverviewDto.cs:143 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/HoReportOverviewDto.cs:159 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetHoReportOverview/HoReportOverviewDto.cs:179 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:233 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:235 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:236 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:254 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:256 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:257 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:335 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:338 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:340 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:342 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:345 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:347 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:373 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:375 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:398 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:400 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:401 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:468 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:492 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/GetStaffLeaderReportOverviewQueryHandler.cs:503 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/StaffLeaderReportOverviewDto.cs:116 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| VisitType | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/StaffLeaderReportOverviewDto.cs:118 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/StaffLeaderReportOverviewDto.cs:131 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportOverview/StaffLeaderReportOverviewDto.cs:159 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderDeptInvoiceItemsQuery.cs:36 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderDeptInvoiceItemsQuery.cs:102 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderDeptInvoiceItemsQuery.cs:104 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderDeptInvoiceItemsQuery.cs:105 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderDeptInvoiceItemsQuery.cs:156 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderReportV2Query.cs:122 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderReportV2QueryHandler.cs:374 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderReportV2QueryHandler.cs:394 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Delegations/VisitPhotos/Commands/UploadVisitInstancePhotos/UploadVisitInstancePhotosCommandHandler.cs:54 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| Purpose | backend/PEMS.Application/Delegations/VisitPhotos/Commands/UploadVisitInstancePhotos/UploadVisitInstancePhotosCommandHandler.cs:67 | Runtime compatibility projection write | Write | Various | Compatibility | Yes | Remove dual-write |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetMyVisitPhotoFolders/GetMyVisitPhotoFoldersQuery.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetMyVisitPhotoFolders/GetMyVisitPhotoFoldersQueryHandler.cs:95 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetMyVisitPhotoFolders/GetMyVisitPhotoFoldersQueryHandler.cs:100 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetMyVisitPhotoFolders/GetMyVisitPhotoFoldersQueryHandler.cs:108 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetMyVisitPhotoFolders/GetMyVisitPhotoFoldersQueryHandler.cs:121 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetVisitInstancePhotos/GetVisitInstancePhotosQueryHandler.cs:34 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetVisitInstancePhotos/GetVisitInstancePhotosQueryHandler.cs:39 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetVisitInstancePhotos/GetVisitInstancePhotosQueryHandler.cs:73 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | backend/PEMS.Application/Delegations/VisitPhotos/Queries/GetVisitInstancePhotos/VisitInstancePhotosDto.cs:23 | Runtime V1 read | Read | Various | Compatibility | Yes | Remove or migrate to V2 |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicCampusNavigation/GetPublicCampusNavigationQueryHandler.cs:89 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicCampusNavigation/GetPublicCampusNavigationQueryHandler.cs:98 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicCampusNavigation/GetPublicCampusNavigationQueryHandler.cs:101 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicCampusNavigation/GetPublicCampusNavigationQueryHandler.cs:103 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicCampusNavigation/GetPublicCampusNavigationQueryHandler.cs:150 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicCampusNavigation/GetPublicCampusNavigationQueryHandler.cs:187 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicGalleryItemAudio/GetPublicGalleryItemAudioQueryHandler.cs:69 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicGalleryItemDetail/GetPublicGalleryItemDetailQueryHandler.cs:76 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicGalleryItemDetail/GetPublicGalleryItemDetailQueryHandler.cs:85 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicGalleryMedia/GetPublicGalleryMediaQueryHandler.cs:49 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicGalleryMediaStream/GetPublicGalleryMediaStreamQueryHandler.cs:45 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicLocationGalleryItem/GetPublicLocationGalleryItemQueryHandler.cs:80 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicLocationGalleryItem/GetPublicLocationGalleryItemQueryHandler.cs:92 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicLocationShowcase/GetPublicLocationShowcaseQueryHandler.cs:114 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| Purpose | backend/PEMS.Application/Galleries/Public/Queries/GetPublicLocationShowcase/GetPublicLocationShowcaseQueryHandler.cs:126 | Runtime dual-read/compatibility read | Read | Various | Compatibility | Yes | Switch to V2 detail read |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:77 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:252 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:269 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:304 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:331 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:369 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:391 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:447 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ActorRelationAuthenticatedCreateApiTests.cs:488 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:95 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:109 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:110 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:124 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:138 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:152 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:153 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:186 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:234 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:234 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:235 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:235 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:238 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:238 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:254 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:255 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:256 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:257 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:260 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/AgendaSetupForInstanceV2Tests.cs:260 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/CreateVisitRequestV2ServiceTests.cs:294 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:90 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:91 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:92 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:109 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:110 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:127 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:142 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:143 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:144 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:159 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:160 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:162 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:227 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:227 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:228 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:228 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:231 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:231 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:247 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:248 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:249 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:250 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:253 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/DeptInvitationDetailV2Tests.cs:253 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:101 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:102 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:118 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:119 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:182 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:182 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:183 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:183 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:186 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:186 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:202 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:203 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:204 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:205 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:208 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/EditableVisitRequestDetailV2Tests.cs:208 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:93 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:94 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:95 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:110 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:111 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:126 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:140 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:141 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:142 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:156 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:157 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:158 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:193 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:238 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:238 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:239 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:239 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:242 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:242 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:258 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:259 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:260 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:261 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:264 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/MyVisitInvitationByIdV2Tests.cs:264 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:73 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:73 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:74 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:74 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:77 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:77 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:100 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:100 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:100 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:101 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:104 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:104 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:231 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:232 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:249 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:250 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:268 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:269 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:288 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:289 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:290 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:291 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:292 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs:293 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PublicInitiateVisitRequestV2Tests.cs:171 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/PublicInitiateVisitRequestV2Tests.cs:192 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/PublicInitiateVisitRequestV2Tests.cs:248 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:89 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:90 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:91 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:109 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:110 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:127 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:142 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:143 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:144 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:159 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:160 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:162 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:227 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:227 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:228 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:228 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:231 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:231 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:247 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:248 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:249 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:250 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:253 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/RequestDetailV2Tests.cs:253 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:114 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:114 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:114 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:114 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:114 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:116 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:116 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:116 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2CommandTests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:69 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:69 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:69 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:69 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:69 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:71 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:71 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:71 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:72 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:159 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs:160 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:99 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:100 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:101 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:102 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:103 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:104 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:121 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:122 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:139 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:154 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:155 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:171 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:172 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:175 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:231 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:279 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:279 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:280 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:280 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:283 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:283 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:299 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:300 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:301 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:302 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:305 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/StaffCalendarDetailV2Tests.cs:305 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:83 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:83 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:84 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:84 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:87 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:87 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:111 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:112 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:113 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:114 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:163 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:164 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:165 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:184 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:204 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:205 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:206 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:221 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:222 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:259 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:260 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/SubmittedVisitRequestFormDetailV2Tests.cs:323 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/Uc17TestData.cs:23 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/Uc17TestData.cs:31 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/Uc17TestData.cs:59 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:115 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:115 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:115 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:115 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:115 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:118 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2CommandTests.cs:227 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:73 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:73 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:73 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:73 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:73 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:75 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:75 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:75 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:76 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:82 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:82 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:82 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:82 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:82 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:84 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:84 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:84 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:85 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:139 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:155 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs:316 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs:240 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs:244 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs:256 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs:257 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs:266 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs:304 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs:345 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:166 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:167 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:168 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:178 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:178 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:178 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:178 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:178 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:179 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:189 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:190 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:191 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:228 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:229 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:240 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:249 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:269 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:301 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:346 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:348 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:505 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:521 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:534 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:557 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:558 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:591 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs:602 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:92 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:93 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:109 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:110 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:126 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:142 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:176 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:234 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:234 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:235 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:235 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:238 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:238 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:261 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:262 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:263 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:264 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:267 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceContributionV2Tests.cs:267 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:91 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:92 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:107 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:108 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:123 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:124 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:140 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:174 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:231 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:231 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:232 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:232 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:235 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:235 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:251 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:252 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:253 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:254 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:257 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitInstanceSummaryV2Tests.cs:257 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:89 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:103 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:104 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:121 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:122 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:203 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:203 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:204 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:204 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:207 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:207 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:223 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:224 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:225 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:226 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:229 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitInvitationDetailV2Tests.cs:229 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:86 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:88 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:89 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:148 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:150 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:151 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:151 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:156 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:157 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:162 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:163 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:164 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:165 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:180 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:182 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:183 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:183 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:188 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:189 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:194 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:195 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:196 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:197 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:244 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:273 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:426 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:452 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitorEditResubmitApiTests.cs:502 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:93 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:94 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:109 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:110 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:125 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:141 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:178 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:236 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:236 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:237 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:237 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:240 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:240 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:256 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:257 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:258 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:259 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:262 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitProcessDetailV2Tests.cs:262 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:161 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:164 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:166 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:169 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:172 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:173 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:215 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:232 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:234 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:328 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs:335 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Campuses/GetCampusStatusImpactQueryHandlerTests.cs:63 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:9 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:15 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:20 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:21 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:27 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:30 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:31 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:35 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:38 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:39 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:40 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:48 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:64 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:77 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Galleries/GalleryAreaCoverVideoTests.cs:95 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/TestInfrastructure/CampusUcTestHarness.cs:299 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/TestInfrastructure/CampusUcTestHarness.cs:300 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/TestInfrastructure/DelegationsTestHarness.cs:301 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/TestInfrastructure/DelegationsTestHarness.cs:302 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:33 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:35 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:36 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:36 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:38 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:39 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:44 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:45 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:46 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:47 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:162 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:164 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/CreateAuthenticatedVisitRequestCommandValidatorTests.cs:165 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:116 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:118 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:119 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:119 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:124 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:125 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:130 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:131 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:132 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.UnitTests/VisitRequests/ResubmitRejectedVisitRequestCommandHandlerTests.cs:133 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:115 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:118 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:118 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:123 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:124 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:129 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:130 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:131 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.UnitTests/VisitRequests/UpdatePendingVisitRequestCommandHandlerTests.cs:132 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:109 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:117 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:121 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:121 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:199 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:201 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:202 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:202 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:204 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:205 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:210 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:211 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:212 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:213 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:224 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:226 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitType | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:227 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| VisitTypeOther | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:227 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:229 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingContent | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:230 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| WorkingLanguage | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:235 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| TransportationNote | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:236 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentStatus | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:237 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| MediaConsentNote | tests/PEMS.UnitTests/VisitRequests/VisitRequestFingerprintBuilderTests.cs:238 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetMyVisitPhotoFoldersQueryHandlerTests.cs:49 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetMyVisitPhotoFoldersQueryHandlerTests.cs:56 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetMyVisitPhotoFoldersQueryHandlerTests.cs:88 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetMyVisitPhotoFoldersQueryHandlerTests.cs:89 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetMyVisitPhotoFoldersQueryHandlerTests.cs:96 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetMyVisitPhotoFoldersQueryHandlerTests.cs:98 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetMyVisitPhotoFoldersQueryHandlerTests.cs:100 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetVisitInstancePhotosQueryHandlerTests.cs:78 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetVisitInstancePhotosQueryHandlerTests.cs:92 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| DelegationName | tests/PEMS.UnitTests/Delegations/VisitPhotos/GetVisitInstancePhotosQueryHandlerTests.cs:97 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Delegations/VisitPhotos/UploadVisitInstancePhotosCommandHandlerTests.cs:60 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Delegations/VisitPhotos/UploadVisitInstancePhotosCommandHandlerTests.cs:133 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Delegations/VisitPhotos/UploadVisitInstancePhotosCommandHandlerTests.cs:270 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Delegations/VisitPhotos/UploadVisitInstancePhotosCommandHandlerTests.cs:279 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Delegations/VisitPhotos/VisitPhotoFolderServiceTests.cs:32 | Test/fixture-only | Read | Various | Compatibility | No | Update test |
| Purpose | tests/PEMS.UnitTests/Delegations/VisitPhotos/VisitPhotoTestSeed.cs:68 | Test/fixture-only | Read | Various | Compatibility | No | Update test |

## Summary
| Category | Occurrences | Unique files | Blocking occurrences |
|---|---:|---:|---:|
| Documentation/comment-only | 13 | 12 | 0 |
| Runtime V1 read | 594 | 101 | 594 |
| Runtime dual-read/compatibility read | 492 | 81 | 492 |
| Runtime compatibility projection write | 301 | 49 | 301 |
| EF/schema mapping required before drop | 29 | 7 | 29 |
| Test/fixture-only | 499 | 38 | 0 |

## Readiness Verdict
- zero runtime reads: FAIL
- zero runtime writes: FAIL
- all persisted requests V2: FAIL
- full backfill: PASS (presumed)
- no old client/draft: FAIL
- V1 fallback retired: FAIL
- flags/cutover state: OFF
- export/restore proof: PASS (Candidate SQL prepared)
- current regression baseline: Targeted V2 IT PASS 45/45
