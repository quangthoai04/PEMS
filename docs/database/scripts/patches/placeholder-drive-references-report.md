# Placeholder Google Drive file references — full classification

> **Read-only report.** Nothing here changed a row. Produced 2026-08-02 against the working `pems_db`
> by `docs/database/scripts/patches/audit_unusable_drive_file_references.sql` plus the per-row query
> reproduced in §5. Re-run that query to regenerate; do not hand-edit the table.

## 1. Why these rows exist

Traced from a live failure on **visit process 3107** (`visit_request` 2004). Preparing the
"Cập nhật công tác chuẩn bị" email died with *"Không tìm thấy tệp đính kèm trên Google Drive"*. No
attachment was involved: the delegation's partner (SeoulTech, `partner_id` 1) carries
`logo_file_id = 1`, whose `external_file_id` is `drv-logo-seoultech` — a seed placeholder Drive has
never held. Drive answered 404 and the report render let it escape.

The **code** side is fixed (commit `fix(storage): recover missing report artifacts and align attachments`):
a partner logo is best-effort, and the mandatory Schedule Report is probed before a draft is reused or
a message is sent. These rows therefore break nothing today. They are catalogued because a row that
points at no file will mislead the next person who reads it.

## 2. How a placeholder is recognised

Google Drive file ids are 25+ characters of `[A-Za-z0-9_-]` (28–44 in practice). The filter flags ids
too **short** to be real, so a genuine id is never caught.

> **Limitation, stated plainly:** the filter can still MISS a fabricated id that happens to be long
> enough. A clean result means "no obvious placeholders", never "every id resolves". Only an actual
> Drive read proves that.

## 3. Totals — reconciled

Snapshot of `files` on the working `pems_db`, 2026-08-02:

| Bucket | Rows |
|---|---:|
| GOOGLE_DRIVE, id looks real | 68 |
| **GOOGLE_DRIVE, id unusable** | **161** |
| LOCAL | 16 |
| S3 / AZURE / GCS / OTHER | 2 / 1 / 2 / 1 |
| **Total `files`** | **251** |

`68 + 161 + 16 + 6 = 251` ✓

> Only the **161** is stable. The "id looks real" bucket grows every time somebody uploads through the
> application — it moved 62 → 68 during the afternoon this report was written, from ordinary use of a
> running dev server. Re-run §1 of the audit script for a current number; the placeholder count is what
> this report is about, and nothing creates new placeholders.

Breakdown of the 161, by whether anything points at them:

| Link state | Role | Rows |
|---|---|---:|
| referenced | decorative | 11 |
| referenced | **MANDATORY** | **0** |
| orphan | n/a | 150 |
| **Total** | | **161** |

`11 + 0 + 150 = 161` ✓

**Role** is decided by the referencing column, not by guesswork:
`documents.file_id` / `email_draft_attachments.file_id` / `sent_email_attachments.file_id` are
**MANDATORY** — a user is shown or sent that exact file. `partners.logo_file_id` /
`partners.cover_file_id` are **decorative** — the UI and the PDF both have a defined look without them.

**No mandatory reference is affected.** In particular there is no broken `SCHEDULE_REPORT`: every
archived report on this database resolves.

The 150 orphans are referenced by nothing at all — 119 `image/jpeg`, 24 `video/mp4`, 4
`application/pdf`, 2 `image/png`, 1 `.xlsx`. They are seed fixtures for gallery/photo screens that
read through other tables. **Not deleted in this batch** (see §6).

## 4. Every affected row

`ref_count` is how many rows across all five referencing columns point at that `file_id`.

| file_id | external_file_id | mime_type | referenced by (table.column) | ref_count | link state | role |
|---:|---|---|---|---:|---|---|
| 4 | `drv-green-brochure` | application/pdf | (orphan) | 0 | orphan | n/a (orphan) |
| 5 | `drv-minutes-seoultech` | application/pdf | (orphan) | 0 | orphan | n/a (orphan) |
| 6 | `drv-service-room` | application/pdf | (orphan) | 0 | orphan | n/a (orphan) |
| 7 | `drv-gallery-hn-hero` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 8 | `drv-gallery-hn-lab` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 9 | `drv-gallery-hcm-walkway` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 10 | `drv-news-global-mobility` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 11 | `drv-news-student-buddy` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 12 | `drv-card-aoi` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 13 | `drv-report-june` | application/pdf | (orphan) | 0 | orphan | n/a (orphan) |
| 14 | `drv-gallery-dn-lab` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 15 | `drv-gallery-ct-workshop` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 16 | `drv-gallery-qn-coastal` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 212 | `drv-asset-212` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 214 | `drv-asset-214` | image/png | (orphan) | 0 | orphan | n/a (orphan) |
| 216 | `drv-asset-216` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 218 | `drv-asset-218` | application/vnd.openxmlformats-officedocument.spreadsheetml.sheet | (orphan) | 0 | orphan | n/a (orphan) |
| 220 | `drv-asset-220` | image/png | (orphan) | 0 | orphan | n/a (orphan) |
| 301 | `drv-wide-gallery-301` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 302 | `drv-wide-gallery-302` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 303 | `drv-wide-gallery-303` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 304 | `drv-wide-gallery-304` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 305 | `drv-wide-gallery-305` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 306 | `drv-wide-gallery-306` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 307 | `drv-wide-gallery-307` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 308 | `drv-wide-gallery-308` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 309 | `drv-wide-gallery-309` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 310 | `drv-wide-gallery-310` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 311 | `drv-wide-gallery-311` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 312 | `drv-wide-gallery-312` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 313 | `drv-wide-gallery-313` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 314 | `drv-wide-gallery-314` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 315 | `drv-wide-gallery-315` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 316 | `drv-wide-gallery-316` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 317 | `drv-wide-gallery-317` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 318 | `drv-wide-gallery-318` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 319 | `drv-wide-gallery-319` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 320 | `drv-wide-gallery-320` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 321 | `drv-wide-gallery-321` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 322 | `drv-wide-gallery-322` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 323 | `drv-wide-gallery-323` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 324 | `drv-wide-gallery-324` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 325 | `drv-wide-gallery-325` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 326 | `drv-wide-gallery-326` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 327 | `drv-wide-gallery-327` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 328 | `drv-wide-gallery-328` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 329 | `drv-wide-gallery-329` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 330 | `drv-wide-gallery-330` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 331 | `drv-wide-gallery-331` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 332 | `drv-wide-gallery-332` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 333 | `drv-wide-gallery-333` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 334 | `drv-wide-gallery-334` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 335 | `drv-wide-gallery-335` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 336 | `drv-wide-gallery-336` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 337 | `drv-wide-gallery-337` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 338 | `drv-wide-gallery-338` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 339 | `drv-wide-gallery-339` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 340 | `drv-wide-gallery-340` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 341 | `drv-wide-gallery-341` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 342 | `drv-wide-gallery-342` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 343 | `drv-wide-gallery-343` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 344 | `drv-wide-gallery-344` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 345 | `drv-wide-gallery-345` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 346 | `drv-wide-gallery-346` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 347 | `drv-wide-gallery-347` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 348 | `drv-wide-gallery-348` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 349 | `drv-wide-gallery-349` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 350 | `drv-wide-gallery-350` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 351 | `drv-wide-gallery-351` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 352 | `drv-wide-gallery-352` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 353 | `drv-wide-gallery-353` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 354 | `drv-wide-gallery-354` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 355 | `drv-wide-gallery-355` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 356 | `drv-wide-gallery-356` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 357 | `drv-wide-gallery-357` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 358 | `drv-wide-gallery-358` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 359 | `drv-wide-gallery-359` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 360 | `drv-wide-gallery-360` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 361 | `drv-wide-gallery-361` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 362 | `drv-wide-gallery-362` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 363 | `drv-wide-gallery-363` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 364 | `drv-wide-gallery-364` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 365 | `drv-wide-gallery-365` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 366 | `drv-wide-gallery-366` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 367 | `drv-wide-gallery-367` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 368 | `drv-wide-gallery-368` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 369 | `drv-wide-gallery-369` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 370 | `drv-wide-gallery-370` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 371 | `drv-wide-gallery-371` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 372 | `drv-wide-gallery-372` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 373 | `drv-wide-gallery-373` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 374 | `drv-wide-gallery-374` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 375 | `drv-wide-gallery-375` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 376 | `drv-wide-gallery-376` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 377 | `drv-wide-gallery-377` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 378 | `drv-wide-gallery-378` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 379 | `drv-wide-gallery-379` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 380 | `drv-wide-gallery-380` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 381 | `drv-wide-gallery-381` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 382 | `drv-wide-gallery-382` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 383 | `drv-wide-gallery-383` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 384 | `drv-wide-gallery-384` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 385 | `drv-wide-gallery-385` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 386 | `drv-wide-gallery-386` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 387 | `drv-wide-gallery-387` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 388 | `drv-wide-gallery-388` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 389 | `drv-wide-gallery-389` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 390 | `drv-wide-gallery-390` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 391 | `drv-wide-gallery-391` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 392 | `drv-wide-gallery-392` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 393 | `drv-wide-gallery-393` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 394 | `drv-wide-gallery-394` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 395 | `drv-wide-gallery-395` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 396 | `drv-wide-gallery-396` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 397 | `drv-wide-gallery-397` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 398 | `drv-wide-gallery-398` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 399 | `drv-wide-gallery-399` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 400 | `drv-wide-gallery-400` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 401 | `drv-wide-gallery-401` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 402 | `drv-wide-gallery-402` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 403 | `drv-wide-gallery-403` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 404 | `drv-wide-gallery-404` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 405 | `drv-wide-gallery-405` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 406 | `drv-wide-gallery-406` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 407 | `drv-wide-gallery-407` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 408 | `drv-wide-gallery-408` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 409 | `drv-wide-gallery-409` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 410 | `drv-wide-gallery-410` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 411 | `drv-wide-gallery-411` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 412 | `drv-wide-gallery-412` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 413 | `drv-wide-gallery-413` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 414 | `drv-wide-gallery-414` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 415 | `drv-wide-gallery-415` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 416 | `drv-wide-gallery-416` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 417 | `drv-wide-gallery-417` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 418 | `drv-wide-gallery-418` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 419 | `drv-wide-gallery-419` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 420 | `drv-wide-gallery-420` | video/mp4 | (orphan) | 0 | orphan | n/a (orphan) |
| 18001 | `seed-drive-cover-17001` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18002 | `seed-drive-cover-17002` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18003 | `seed-drive-cover-17003` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18004 | `seed-drive-cover-17004` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18005 | `seed-drive-cover-17005` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18006 | `seed-drive-cover-17006` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18007 | `seed-drive-cover-17007` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18008 | `seed-drive-cover-17008` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18009 | `seed-drive-cover-17009` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18010 | `seed-drive-cover-17010` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18011 | `seed-drive-cover-17011` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 18012 | `seed-drive-cover-17012` | image/jpeg | (orphan) | 0 | orphan | n/a (orphan) |
| 1 | `drv-logo-seoultech` | image/png | partners.logo_file_id | 1 | referenced | decorative |
| 2 | `drv-cover-seoultech` | image/jpeg | partners.cover_file_id | 1 | referenced | decorative |
| 3 | `drv-logo-kyoto` | image/svg+xml | partners.logo_file_id | 1 | referenced | decorative |
| 205 | `ext-file-205` | application/octet-stream | partners.logo_file_id | 1 | referenced | decorative |
| 207 | `drv-partner-logo-207` | image/png | partners.logo_file_id | 1 | referenced | decorative |
| 210 | `drv-partner-logo-210` | image/svg+xml | partners.logo_file_id | 1 | referenced | decorative |
| 211 | `drv-asset-211` | application/pdf | partners.cover_file_id | 1 | referenced | decorative |
| 213 | `drv-asset-213` | application/pdf | partners.cover_file_id | 1 | referenced | decorative |
| 215 | `drv-asset-215` | image/jpeg | partners.cover_file_id | 1 | referenced | decorative |
| 217 | `drv-asset-217` | text/csv | partners.cover_file_id | 1 | referenced | decorative |
| 219 | `drv-asset-219` | application/json | partners.cover_file_id | 1 | referenced | decorative |

## 5. The query behind §4

```sql
SET @u := '^[A-Za-z0-9_-]{25,}$';
SET SESSION group_concat_max_len = 4096;

SELECT
  f.file_id, f.external_file_id, COALESCE(f.mime_type,'(null)') AS mime_type,
  COALESCE(r.refs,'(orphan)') AS referenced_by, COALESCE(r.ref_count,0) AS ref_count,
  CASE WHEN r.ref_count IS NULL THEN 'orphan' ELSE 'referenced' END AS link_state,
  CASE WHEN r.refs IS NULL THEN 'n/a (orphan)'
       WHEN r.refs LIKE '%documents%' OR r.refs LIKE '%email_draft_attachments%'
         OR r.refs LIKE '%sent_email_attachments%' THEN 'MANDATORY'
       ELSE 'decorative' END AS role
FROM files f
LEFT JOIN (
  SELECT file_id, GROUP_CONCAT(DISTINCT ref ORDER BY ref SEPARATOR ' + ') AS refs, SUM(n) AS ref_count
  FROM (
    SELECT d.file_id, 'documents.file_id' AS ref, COUNT(*) n FROM documents d GROUP BY d.file_id
    UNION ALL SELECT a.file_id,'email_draft_attachments.file_id',COUNT(*) FROM email_draft_attachments a GROUP BY a.file_id
    UNION ALL SELECT s.file_id,'sent_email_attachments.file_id', COUNT(*) FROM sent_email_attachments s  GROUP BY s.file_id
    UNION ALL SELECT p.logo_file_id, 'partners.logo_file_id', COUNT(*) FROM partners p WHERE p.logo_file_id  IS NOT NULL GROUP BY p.logo_file_id
    UNION ALL SELECT p.cover_file_id,'partners.cover_file_id',COUNT(*) FROM partners p WHERE p.cover_file_id IS NOT NULL GROUP BY p.cover_file_id
  ) x GROUP BY file_id
) r ON r.file_id = f.file_id
WHERE f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @u)
ORDER BY link_state, role, f.file_id;
```

## 6. What was deliberately NOT done

- **No repair was run.** The repair block in `audit_unusable_drive_file_references.sql` stays commented out.
- **No `logo_file_id` / `cover_file_id` was set to NULL** on this database.
- **No orphan was deleted.** They are referenced by nothing today, but "nothing in these five columns"
  is not "nothing anywhere" — checksum and audit history are not enumerated here, and deleting them
  buys nothing once the reference question is settled.
- **No Drive id was invented.** There is no correct value to write: the bytes were never uploaded.

## 7. The only real repair, for the 11 referenced rows

Re-upload the real images **through the Partner screen**, which writes a genuine Drive id via the normal
upload path and needs no SQL. Until an image exists, the correct state is `NULL` + the built-in
fallback — and setting that NULL is a data-owner decision, not a migration.

### 5 logos to re-upload

| # | partner_id | Partner | file_id | placeholder id | mime |
|---:|---:|---|---:|---|---|
| 1 | 1 | SeoulTech Global Engagement Center | 1 | `drv-logo-seoultech` | image/png |
| 2 | 2 | Kyoto Robotics Collaboration Lab | 3 | `drv-logo-kyoto` | image/svg+xml |
| 3 | 104 | Nordic Green Campus Alliance | 205 | `ext-file-205` | application/octet-stream |
| 4 | 106 | Gulf Innovation Fund for Education | 207 | `drv-partner-logo-207` | image/png |
| 5 | 109 | Andes University Exchange Office | 210 | `drv-partner-logo-210` | image/svg+xml |

### 6 covers to re-upload

| # | partner_id | Partner | file_id | placeholder id | mime |
|---:|---:|---|---:|---|---|
| 1 | 1 | SeoulTech Global Engagement Center | 2 | `drv-cover-seoultech` | image/jpeg |
| 2 | 102 | Politecnico di Milano Mobility Lab | 213 | `drv-asset-213` | application/pdf |
| 3 | 104 | Nordic Green Campus Alliance | 215 | `drv-asset-215` | image/jpeg |
| 4 | 106 | Gulf Innovation Fund for Education | 217 | `drv-asset-217` | text/csv |
| 5 | 108 | Lagos Tech Bridge Initiative | 219 | `drv-asset-219` | application/json |
| 6 | 110 | Singapore Applied AI Consortium | 211 | `drv-asset-211` | application/pdf |

> **Two notes before anyone collects 11 images.**
>
> Partners **104** (Nordic Green Campus Alliance) and **106** (Gulf Innovation Fund for Education)
> appear in BOTH tables — each needs a logo *and* a cover. The 11 files therefore belong to
> **9 distinct partners**, not 11.
>
> Rows 205, 211, 213, 217 and 219 carry mime types that are not images at all
> (`application/octet-stream`, `application/pdf`, `text/csv`, `application/json`). They were coverage
> fixtures, never pictures — so for those the honest state is "no logo/cover", and re-uploading only
> makes sense if a real image actually exists to upload.
