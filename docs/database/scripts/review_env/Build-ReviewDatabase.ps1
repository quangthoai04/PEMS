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
    [switch]$ScanOnly,

    # Drop every table/view/trigger INSIDE pems_review_v2 before importing.
    #
    # Needed because the transformed payload no longer carries the dump's own
    # `DROP DATABASE`/`CREATE DATABASE` preamble, so a second import would fail on
    # "table already exists". The reset stays inside the review database: it issues DROP TABLE,
    # never DROP DATABASE, so it cannot escape the schema the restricted account owns.
    [switch]$Reset
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$REVIEW_DB = 'pems_review_v2'

$scriptsRoot = Split-Path -Parent $PSScriptRoot
$importer = Join-Path $scriptsRoot 'phase_1_candidate\import_disposable_fixture.ps1'
if (-not (Test-Path -LiteralPath $importer)) { throw "Safe importer not found at $importer" }

if ([string]::IsNullOrWhiteSpace($MasterSql)) {
    # Resolve the ONE canonical schema script rather than hard-coding a filename that renames break.
    $candidates = @(Get-ChildItem -LiteralPath $scriptsRoot -Filter 'PEMS_FULL_*.sql' -File)
    if ($candidates.Count -ne 1) {
        throw "Expected exactly one canonical PEMS_FULL_*.sql in $scriptsRoot, found $($candidates.Count)."
    }
    $MasterSql = $candidates[0].FullName
}
if (-not (Test-Path -LiteralPath $MasterSql)) { throw "Master SQL not found at $MasterSql" }

Write-Host '========================================'
Write-Host 'PEMS V2 review database build'
Write-Host '========================================'
Write-Host ("Target : {0}" -f $REVIEW_DB)
Write-Host ("Master : {0}" -f $MasterSql)
Write-Host ''

function Invoke-ReviewSql {
    param([string]$Text)

    $exe = if ($env:MYSQL_BIN) { $env:MYSQL_BIN } else { 'mysql' }
    $usr = $env:MYSQL_USER
    $hst = if ($env:MYSQL_HOST) { $env:MYSQL_HOST } else { 'localhost' }
    $prt = if ($env:MYSQL_PORT) { $env:MYSQL_PORT } else { '3306' }
    if ([string]::IsNullOrWhiteSpace($usr)) { throw 'MYSQL_USER is not set. This script never falls back to root.' }

    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('pems_review_' + [guid]::NewGuid().ToString('N') + '.sql')
    [System.IO.File]::WriteAllText($tmp, $Text, (New-Object System.Text.UTF8Encoding($false)))
    $priorPwd = $env:MYSQL_PWD
    try {
        if ($env:MYSQL_PASSWORD) { $env:MYSQL_PWD = $env:MYSQL_PASSWORD }
        # stderr merged inside the cmd string: PowerShell's own 2>&1 on a native command wraps
        # each stderr line in a NativeCommandError, which $ErrorActionPreference='Stop' escalates.
        $cmd = '"' + $exe + '" -u' + $usr + ' -h' + $hst + ' -P' + $prt +
               ' --default-character-set=utf8mb4 -N -B ' + $REVIEW_DB + ' < "' + $tmp + '" 2>&1'
        $out = & cmd.exe /c $cmd | Out-String
        return @{ Output = $out; ExitCode = $LASTEXITCODE }
    }
    finally {
        if ([string]::IsNullOrEmpty($priorPwd)) { Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue }
        else { $env:MYSQL_PWD = $priorPwd }
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    }
}

if ($Reset -and -not $ScanOnly) {
    Write-Host '-- Reset: dropping existing objects inside the review database --'
    # Names come from information_schema for THIS schema only, so the generated DROPs cannot name
    # another database even if the account somehow had wider rights.
    # Read the table list first, then emit one DROP per table. PREPARE executes exactly ONE
    # statement, so building a single semicolon-joined string and preparing it is a syntax error -
    # the drops have to be separate statements in the script.
    $listSql = "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE' ORDER BY table_name;"
    $list = Invoke-ReviewSql -Text $listSql
    if ($list.ExitCode -ne 0) {
        Write-Host $list.Output.Trim()
        Write-Host "RESET FAILED: could not list tables (exit $($list.ExitCode))."
        exit 1
    }

    $tables = @($list.Output -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
    Write-Host ("   existing tables: {0}" -f $tables.Count)

    if ($tables.Count -gt 0) {
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.AppendLine('SET FOREIGN_KEY_CHECKS = 0;')
        foreach ($t in $tables) {
            # Backtick-quote and reject anything that is not a plain identifier, so a hostile
            # table name cannot break out of the generated statement.
            if ($t -notmatch '^[A-Za-z0-9_]+$') {
                Write-Host "RESET FAILED: unexpected table name '$t'."
                exit 1
            }
            [void]$sb.AppendLine('DROP TABLE IF EXISTS `' + $t + '`;')
        }
        [void]$sb.AppendLine('SET FOREIGN_KEY_CHECKS = 1;')
        [void]$sb.AppendLine("SELECT CONCAT('REMAINING_TABLES:', COUNT(*)) FROM information_schema.tables WHERE table_schema = DATABASE();")
        $r = Invoke-ReviewSql -Text $sb.ToString()
    }
    else {
        $r = Invoke-ReviewSql -Text "SELECT CONCAT('REMAINING_TABLES:', COUNT(*)) FROM information_schema.tables WHERE table_schema = DATABASE();"
    }

    Write-Host $r.Output.Trim()
    if ($r.ExitCode -ne 0 -or $r.Output -notmatch 'REMAINING_TABLES:0') {
        Write-Host "RESET FAILED (exit $($r.ExitCode)). Not importing on top of a partial schema."
        exit 1
    }
    Write-Host ''
}

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
    Write-Host "REVIEW BUILD FAILED (exit $exit)."
    Write-Host 'NOTE: MySQL DDL auto-commits, so objects created before the failing statement remain.'
    Write-Host 'Re-run with -Reset once the cause is fixed, so the import does not land on a partial schema.'
    Write-Host ''
    Write-Host 'If the failure was ERROR 1419 (SUPER privilege / binary logging), the master creates'
    Write-Host 'validation TRIGGERS and MySQL blocks that for a non-SUPER account while binlog is on.'
    Write-Host 'The fix is a one-line server setting, applied by the OWNER as root:'
    Write-Host ''
    Write-Host '    SET PERSIST log_bin_trust_function_creators = 1;'
    Write-Host ''
    Write-Host 'Granting the review account SUPER instead would defeat the least-privilege design'
    Write-Host 'and this harness would then refuse the credential. Skipping the triggers is also not'
    Write-Host 'an option: they enforce business invariants, so a review without them would behave'
    Write-Host 'differently from production.'
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
