<#
    Builds the isolated PEMS V2 review database from the authoritative master, through the safe
    import path only.

    PREREQUISITE (owner, manual, once): bootstrap_review_db.sql has been run, and the restricted
    account is supplied via MYSQL_USER / MYSQL_PASSWORD. This script never creates a database or a
    user, and never falls back to root.

    Run:
      $env:MYSQL_BIN      = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe'
      $env:MYSQL_USER     = 'pems_review'
      $env:MYSQL_PASSWORD = '<password>'
      .\Build-ReviewDatabase.ps1

    WHAT IT IMPORTS
    ---------------
    The authoritative master already contains BOTH the v1 compatibility columns and the v2
    additive tables (visit_request_campuses, visit_instance_form_details,
    visit_request_pending_forms, the identity-change and amendment tables). That is precisely the
    additive compatibility state a V2 review needs, so the separate percampus_v2_migration chain is
    NOT replayed here - it exists for upgrading an older v1 database, not for a fresh one.

    Nothing in this script drops the ten legacy columns. Review is not contract-drop.

    The master cannot be imported directly: it carries DROP DATABASE / CREATE DATABASE / USE
    pems_db. It is passed through the asserted transformer, which removes exactly those statements
    into a new artifact and re-runs the same guard on its own output.

    ASCII-only (Windows PowerShell 5.1 reads .ps1 as ANSI without a BOM).
#>
param(
    [string]$MasterSql,
    [switch]$ScanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$REVIEW_DB = 'pems_review_v2'

$scriptsRoot = Split-Path -Parent $PSScriptRoot
$importer = Join-Path $scriptsRoot 'phase_1_candidate\import_disposable_fixture.ps1'
if (-not (Test-Path -LiteralPath $importer)) { throw "Safe importer not found at $importer" }

if ([string]::IsNullOrWhiteSpace($MasterSql)) {
    $MasterSql = Join-Path $scriptsRoot 'PEMS_FULL_V11_REMOVED_TTS_19_07_26.sql'
}
if (-not (Test-Path -LiteralPath $MasterSql)) { throw "Master SQL not found at $MasterSql" }

Write-Host '========================================'
Write-Host 'PEMS V2 review database build'
Write-Host '========================================'
Write-Host ("Target : {0}" -f $REVIEW_DB)
Write-Host ("Master : {0}" -f $MasterSql)
Write-Host ''

# The importer performs, in order: exact Review-mode allowlist, statement-aware scan, asserted
# transformation, credential privilege classification, read-only protected fingerprints,
# SELECT DATABASE() before and after the payload, and a protected fingerprint re-check.
# Everything that matters is enforced there rather than duplicated here.
$psArgs = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $importer,
    '-DbName', $REVIEW_DB,
    '-Mode', 'Review',
    '-SqlPath', $MasterSql,
    '-TransformMaster'
)
if ($ScanOnly) { $psArgs += '-ScanOnly' }

& powershell @psArgs
$exit = $LASTEXITCODE

Write-Host ''
if ($exit -ne 0) {
    Write-Host "REVIEW BUILD FAILED (exit $exit). The review database was not modified past the point of failure."
    exit $exit
}

if ($ScanOnly) {
    Write-Host 'ScanOnly: payload validated, no database was touched.'
    exit 0
}

Write-Host 'Review database built.'
Write-Host ''
Write-Host 'NOTE: the master seed inserts no form_schema_version, so every seeded request is v1.'
Write-Host 'The v2 scenarios (single-campus, uniform multi-campus, mixed multi-campus, identity'
Write-Host 'claim, amendment) are created by running the real journeys with the v2 flags on -'
Write-Host 'see PEMS_V2_EXPERIENCE_REVIEW_GUIDE.md. Creating them through the product rather than'
Write-Host 'by seeding SQL is deliberate: hand-written rows can encode states the business flow'
Write-Host 'cannot actually reach, which makes a review look healthier than the product is.'
exit 0
