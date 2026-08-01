# Seed policy — never write a storage id that addresses nothing

> **Scope: the canonical seed and every future seed/demo batch.** This is a rule for how `files` rows
> and their referrers are seeded, not a migration. It changes no existing data. The repair for data
> already in a database is a separate, still-unapproved decision — see
> [`audit_unusable_drive_file_references.sql`](audit_unusable_drive_file_references.sql) §6.

## The failure this prevents

`PEMS_FULL_VS_31_07_NEW.sql` seeds `files` rows with invented `external_file_id` values
(`drv-logo-seoultech`, `ext-file-205`, `drv-asset-211`…). Google Drive has never held any of them, so
every read answers 404.

That stayed invisible until a real feature depended on one. On **visit process 3107**, preparing the
setup-progress email died with *"Không tìm thấy tệp đính kèm trên Google Drive"* — a partner **logo**
placeholder taking down the whole Schedule Report, under a message that named an attachment. See
[`placeholder-drive-references-report.md`](placeholder-drive-references-report.md) for the full
classification: **161 of 239** `files` rows are affected.

The seed did not lie about a file's *content*. It lied about a file's *existence*, and there is no way
to spot that by reading the SQL — only by asking Drive.

## The four rules

### 1. Never seed a fabricated `external_file_id`

A `files` row with `storage_provider = 'GOOGLE_DRIVE'` may only carry an `external_file_id` that was
returned by an actual upload. There is no acceptable placeholder value, including "obviously fake"
ones — obvious to a reader is not obvious to code, and 404 is 404.

If a row is needed for FK shape and no upload happened, the row should not exist.

### 2. Optional images with no real file: `NULL` + fallback

`partners.logo_file_id`, `partners.cover_file_id` and every other optional media column are **nullable
by design**, and the product already renders without them:

- The Schedule Report PDF draws a single centred FPT logo when there is no partner logo — the same
  layout a delegation with no partner at all produces (`ScheduleReportPdfRenderer`).
- The Partner screens fall back to their placeholder treatment.

`NULL` is therefore the *correct* seeded state, not a degraded one. A fabricated id is strictly worse
than `NULL`: it produces a broken read instead of a defined fallback, and it asserts something untrue.

### 3. Mandatory documents: no metadata without bytes

`documents`, `email_draft_attachments` and `sent_email_attachments` are **mandatory** references — a
user is shown, downloads, or is mailed that exact file. Seeding metadata for bytes that do not exist
creates a record the product must honour and cannot.

So: **do not seed these rows at all** unless the bytes were genuinely uploaded. A delegation with no
archived Schedule Report is a truthful demo state; one whose report cannot be opened is not.

> The current database happens to have **0** broken mandatory references. That is luck, not design —
> the seed simply did not fabricate any. This rule is what keeps it that way.

### 4. Demo images go in through the application, after seeding

An environment that needs real logos, covers or gallery media gets them by **uploading through the
application** (Partner screen, Gallery admin, Visit documents), which runs the normal path:
`FileUploadService` → Drive upload → `files` row written **from the upload result** → business row
linked.

That path cannot produce this defect, because the id is never authored — it is returned. A post-seed
script that drives the same API is fine. Writing Drive ids into SQL is not.

## Checklist for a seed/demo batch

- [ ] No `INSERT INTO files` with `storage_provider='GOOGLE_DRIVE'` and a hand-written `external_file_id`.
- [ ] Optional media columns are `NULL` unless a real upload backs them.
- [ ] No `documents` / `*_attachments` row without real bytes behind it.
- [ ] Demo media is listed as a **post-seed upload step**, not as SQL.
- [ ] After loading, `audit_unusable_drive_file_references.sql` §1–2 reports **0** unusable rows.

Last check is the honest one: it is the only step that can catch a violation the reviewer's eye missed.
Note its stated limitation — the filter flags ids too *short* to be real, so a long fabricated id can
still slip past. Only a real Drive read proves an id resolves.

## Status

**Not yet applied to the canonical seed.** The 161 rows described here still exist, still break
nothing (the code degrades correctly as of
`fix(storage): recover missing report artifacts and align attachments`), and are still awaiting a
data-owner decision. This document defines what a corrected seed must look like when that decision is
taken; it does not itself change any file.
