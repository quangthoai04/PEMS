<#
    Regression tests for the SQL import safety guard (the 2026-07-20 incident class).

    Run:  powershell -ExecutionPolicy Bypass -File .\Test-SqlSafetyGuard.ps1
    Exit: 0 = all passed, 1 = at least one failure.

    No MySQL server, no credentials and no network are required: every assertion here is about
    whether a mysql process would be spawned, which is decided before any connection exists.

    ASCII-only on purpose (Windows PowerShell 5.1 reads .ps1 as ANSI without a BOM).
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\lib\SqlSafetyGuard.ps1')

$script:Pass = 0
$script:Fail = 0

function Assert-Unsafe {
    param([string]$Name, [string]$Sql, [string]$ExpectedKind)

    $v = Test-SqlPayloadSafety -Sql $Sql
    if ($v.IsSafe) {
        Write-Host ("FAIL  {0}: expected UNSAFE, got SAFE" -f $Name) -ForegroundColor Red
        $script:Fail++
        return
    }
    if ($ExpectedKind -and -not ($v.Findings | Where-Object { $_.Kind -eq $ExpectedKind })) {
        $kinds = ($v.Findings | ForEach-Object { $_.Kind }) -join ','
        Write-Host ("FAIL  {0}: expected kind {1}, got {2}" -f $Name, $ExpectedKind, $kinds) -ForegroundColor Red
        $script:Fail++
        return
    }
    Write-Host ("PASS  {0}" -f $Name)
    $script:Pass++
}

function Assert-Safe {
    param([string]$Name, [string]$Sql)

    $v = Test-SqlPayloadSafety -Sql $Sql
    if (-not $v.IsSafe) {
        $detail = ($v.Findings | ForEach-Object { "$($_.Kind):$($_.Detail)" }) -join ' | '
        Write-Host ("FAIL  {0}: expected SAFE, got {1}" -f $Name, $detail) -ForegroundColor Red
        $script:Fail++
        return
    }
    Write-Host ("PASS  {0}" -f $Name)
    $script:Pass++
}

function Assert-True {
    param([string]$Name, [bool]$Condition, [string]$Detail = '')
    if ($Condition) { Write-Host ("PASS  {0}" -f $Name); $script:Pass++ }
    else { Write-Host ("FAIL  {0}: {1}" -f $Name, $Detail) -ForegroundColor Red; $script:Fail++ }
}

Write-Host ''
Write-Host '=== A. The exact incident payload ==='

# This is the shape that overwrote pems_db: the connection said "disposable", the payload said
# otherwise. If only one test in this file ever runs, it should be this one.
Assert-Unsafe 'A1 exact incident (CREATE DATABASE + USE)' @'
CREATE DATABASE IF NOT EXISTS pems_db;
USE pems_db;
DROP TABLE IF EXISTS visit_requests;
'@ 'DATABASE_CONTROL'

Assert-Unsafe 'A2 USE with mixed case, comment and backticks' 'UsE /* incident */ `pems_db`;' 'DATABASE_CONTROL'
Assert-Unsafe 'A3 USE of a non-protected database is still database control' 'USE something_else;' 'DATABASE_CONTROL'

Write-Host ''
Write-Host '=== B. Protected-schema qualified references ==='

Assert-Unsafe 'B1 DROP TABLE `pems_db`.`visit_requests`' 'DROP TABLE `pems_db`.`visit_requests`;' 'PROTECTED_REFERENCE'
Assert-Unsafe 'B2 INSERT INTO pems_test.users' "INSERT INTO pems_test.users VALUES (1,'x');" 'PROTECTED_REFERENCE'
Assert-Unsafe 'B3 SELECT from pems_pr3_test' 'SELECT * FROM pems_pr3_test.visit_requests;' 'PROTECTED_REFERENCE'
Assert-Unsafe 'B4 UPDATE with backticked protected schema' 'UPDATE `pems_db`.`users` SET status = 1;' 'PROTECTED_REFERENCE'

# A table whose name merely STARTS with a protected name is a different object. Flagging it would
# be a false positive that pushes people back to raw imports.
Assert-Safe 'B5 similarly-named but distinct schema is not flagged' 'SELECT * FROM pems_db_archive.t;'

Write-Host ''
Write-Host '=== C. Database lifecycle statements ==='

Assert-Unsafe 'C1 DROP DATABASE'  'DROP DATABASE pems_db;'                    'DATABASE_CONTROL'
Assert-Unsafe 'C2 ALTER DATABASE' 'ALTER DATABASE pems_db CHARACTER SET utf8;' 'DATABASE_CONTROL'
Assert-Unsafe 'C3 CREATE SCHEMA'  'CREATE SCHEMA whatever;'                    'DATABASE_CONTROL'

Write-Host ''
Write-Host '=== D. Server / admin / client statements ==='

Assert-Unsafe 'D1 SET GLOBAL'          'SET GLOBAL max_connections = 500;'                'ADMIN_STATEMENT'
Assert-Unsafe 'D2 GRANT'               'GRANT ALL PRIVILEGES ON *.* TO x@localhost;'      'ADMIN_STATEMENT'
Assert-Unsafe 'D3 CREATE USER'         "CREATE USER a@localhost IDENTIFIED BY 'p';"       'ADMIN_STATEMENT'
Assert-Unsafe 'D4 RESET MASTER'        'RESET MASTER;'                                    'ADMIN_STATEMENT'
Assert-Unsafe 'D5 PURGE BINARY LOGS'   "PURGE BINARY LOGS BEFORE '2026-01-01';"           'ADMIN_STATEMENT'
Assert-Unsafe 'D6 SHUTDOWN'            'SHUTDOWN;'                                        'ADMIN_STATEMENT'
Assert-Unsafe 'D7 client SOURCE'       'SOURCE other.sql;'                                'CLIENT_COMMAND'
Assert-Unsafe 'D8 client backslash-dot' '\. other.sql'                                    'CLIENT_COMMAND'

Write-Host ''
Write-Host '=== E. Lexical traps: the tokenizer must not be fooled either way ==='

# False NEGATIVE trap: hazard buried deep in an otherwise ordinary file.
$buried = (1..60 | ForEach-Object { "INSERT INTO t VALUES ($_);" }) -join "`n"
$buried += "`nUSE pems_db;`n"
$buried += (1..20 | ForEach-Object { "INSERT INTO t VALUES ($_);" }) -join "`n"
Assert-Unsafe 'E1 USE hidden after 60 harmless statements' $buried 'DATABASE_CONTROL'

# False POSITIVE traps: the same words as data or prose must NOT block a safe import.
Assert-Safe 'E2 protected name inside a string literal' "INSERT INTO notes (body) VALUES ('run USE pems_db; to switch');"
Assert-Safe 'E3 protected name inside a line comment'   "-- remember: USE pems_db; was the incident`nSELECT 1;"
Assert-Safe 'E4 protected name inside a block comment'  "/* USE pems_db; DROP DATABASE pems_db; */`nSELECT 1;"
Assert-Safe 'E5 double-dash without space is an operator, not a comment' 'UPDATE t SET n = n--1;'
Assert-Safe 'E6 escaped quotes inside literals'          "INSERT INTO t VALUES ('it''s fine', 'a\'b');"

# Versioned comments ARE executed by MySQL, so their contents must be scanned as live SQL.
Assert-Unsafe 'E7 hazard inside an executable versioned comment' '/*!40101 USE pems_db */;' 'DATABASE_CONTROL'

Write-Host ''
Write-Host '=== F. Routine bodies and DELIMITER ==='

$routine = @'
DELIMITER $$
CREATE PROCEDURE p()
BEGIN
  INSERT INTO t VALUES (1);
  INSERT INTO t VALUES (2);
END$$
DELIMITER ;
SELECT 1;
'@
Assert-Safe 'F1 routine body with custom delimiter is not split mid-body' $routine

$badRoutine = @'
DELIMITER $$
CREATE PROCEDURE p()
BEGIN
  INSERT INTO `pems_db`.`t` VALUES (1);
END$$
DELIMITER ;
'@
Assert-Unsafe 'F2 protected reference inside a routine body' $badRoutine 'PROTECTED_REFERENCE'

Write-Host ''
Write-Host '=== G. Dynamic SQL fails closed ==='

Assert-Unsafe 'G1 PREPARE from a literal naming a protected schema' @'
SET @s = 'DROP TABLE pems_db.visit_requests';
PREPARE st FROM @s;
'@ 'DYNAMIC_SQL'

Assert-Unsafe 'G2 PREPARE from an opaque variable is refused, not guessed' @'
PREPARE st FROM @whatever;
'@ 'DYNAMIC_SQL'

Write-Host ''
Write-Host '=== H. Safe payloads are still allowed ==='

Assert-Safe 'H1 unqualified DDL/DML (the wrapper selects the target)' @'
DROP TABLE IF EXISTS visit_requests;
CREATE TABLE visit_requests (id BIGINT UNSIGNED NOT NULL PRIMARY KEY);
INSERT INTO visit_requests (id) VALUES (1);
'@

Assert-Safe 'H2 SET SESSION and normal client-side settings' @'
SET NAMES utf8mb4;
SET SESSION sql_mode = 'STRICT_ALL_TABLES';
SET FOREIGN_KEY_CHECKS = 0;
SELECT COUNT(*) FROM visit_requests;
SET FOREIGN_KEY_CHECKS = 1;
'@

Write-Host ''
Write-Host '=== I. Encoding and line endings ==='

$tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ('pems_guard_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmpDir | Out-Null
try {
    # BOM + CRLF must not hide the first statement.
    $bomPath = Join-Path $tmpDir 'bom_crlf.sql'
    $bomBytes = [byte[]](0xEF, 0xBB, 0xBF) + [System.Text.Encoding]::UTF8.GetBytes("USE pems_db;`r`nSELECT 1;`r`n")
    [System.IO.File]::WriteAllBytes($bomPath, $bomBytes)

    $v = Test-SqlFileSafety -Path $bomPath
    Assert-True 'I1 UTF-8 BOM + CRLF file is still detected as unsafe' (-not $v.IsSafe) 'BOM hid the first statement'
    Assert-True 'I2 file scan reports a SHA-256' ($v.Sha256 -match '^[0-9a-f]{64}$') "got '$($v.Sha256)'"

    # TOCTOU: the caller must import the bytes that were SCANNED, not re-read the path.
    $toctouPath = Join-Path $tmpDir 'toctou.sql'
    Set-Content -LiteralPath $toctouPath -Value 'SELECT 1;' -Encoding ASCII
    $scan = Test-SqlFileSafety -Path $toctouPath
    Set-Content -LiteralPath $toctouPath -Value 'USE pems_db;' -Encoding ASCII

    Assert-True 'I3 scan result carries the validated content (TOCTOU-safe)' ($scan.Content -match 'SELECT 1') 'Content missing'
    $rescan = Test-SqlFileSafety -Path $toctouPath
    Assert-True 'I4 re-reading the changed path would have been unsafe' (-not $rescan.IsSafe) 'swap not detected'
}
finally {
    Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host '=== J. End-to-end: the runner must not spawn mysql for unsafe input ==='

# A fake mysql that appends one line per invocation. If the guard is wired correctly the spy log
# never appears for an unsafe payload - this is the "mysql invocation count = 0" evidence.
$spyDir = Join-Path ([System.IO.Path]::GetTempPath()) ('pems_spy_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $spyDir | Out-Null
try {
    $spyLog = Join-Path $spyDir 'invocations.log'
    $spyExe = Join-Path $spyDir 'mysql.cmd'
    Set-Content -LiteralPath $spyExe -Value ("@echo off`r`necho INVOKED %* >> `"$spyLog`"`r`nexit /b 0") -Encoding ASCII

    $unsafeSql = Join-Path $spyDir 'unsafe.sql'
    Set-Content -LiteralPath $unsafeSql -Value "CREATE DATABASE IF NOT EXISTS pems_db;`r`nUSE pems_db;`r`nDROP TABLE visit_requests;" -Encoding ASCII

    $importer = Join-Path $PSScriptRoot '..\import_disposable_fixture.ps1'
    Assert-True 'J0 importer script exists' (Test-Path -LiteralPath $importer) "missing: $importer"

    if (Test-Path -LiteralPath $importer) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $importer `
            -DbName pems_i_refusal -SqlPath $unsafeSql -MysqlExe $spyExe -ScanOnly 2>&1 | Out-Null
        $exit = $LASTEXITCODE

        Assert-True 'J1 importer exits nonzero for the incident payload' ($exit -ne 0) "exit was $exit"
        Assert-True 'J2 mysql invocation count is 0' (-not (Test-Path -LiteralPath $spyLog)) 'the spy was invoked'
    }
}
finally {
    Remove-Item -LiteralPath $spyDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host '=== L. Asserted transformation (-TransformMaster) ==='

# The transformer is the ONLY sanctioned way to turn a dump that carries database-control
# statements into something importable. It must remove exactly the asserted statements, must
# re-run the same guard on its own output, and must refuse rather than publish a half-safe file.
$txDir = Join-Path ([System.IO.Path]::GetTempPath()) ('pems_tx_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $txDir | Out-Null
try {
    $spyLog = Join-Path $txDir 'invocations.log'
    $spyExe = Join-Path $txDir 'mysql.cmd'
    Set-Content -LiteralPath $spyExe -Value ("@echo off`r`necho INVOKED %* >> `"$spyLog`"`r`nexit /b 0") -Encoding ASCII
    $importer = Join-Path $PSScriptRoot '..\import_disposable_fixture.ps1'

    function Invoke-Importer {
        param([string]$Sql, [switch]$Transform)
        $p = Join-Path $txDir ('case_' + [guid]::NewGuid().ToString('N') + '.sql')
        Set-Content -LiteralPath $p -Value $Sql -Encoding UTF8
        # Not $args: that is a PowerShell automatic variable and assigning to it here would
        # clobber the caller's argument array.
        $psArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $importer,
                    '-DbName', 'pems_i_refusal', '-SqlPath', $p, '-MysqlExe', $spyExe, '-ScanOnly')
        if ($Transform) { $psArgs += '-TransformMaster' }
        $out = & powershell @psArgs 2>&1 | Out-String
        return @{ Output = $out; ExitCode = $LASTEXITCODE }
    }

    $goodMaster = "CREATE DATABASE IF NOT EXISTS pems_db;`nUSE pems_db;`nCREATE TABLE t (id INT);`nINSERT INTO t VALUES (1);"

    $r = Invoke-Importer -Sql $goodMaster -Transform
    Assert-True 'L1 master-shaped header transforms successfully' ($r.ExitCode -eq 0) ("exit $($r.ExitCode): " + $r.Output)
    Assert-True 'L2 transform emits a manifest with an output hash' ($r.Output -match 'output sha256\s+:\s+[0-9a-f]{64}') 'no output hash in manifest'
    Assert-True 'L3 transform reports the source hash' ($r.Output -match 'source sha256\s+:\s+[0-9a-f]{64}') 'no source hash'

    # Reproducibility: same input, same output hash.
    $r2 = Invoke-Importer -Sql $goodMaster -Transform
    $h1 = ([regex]::Match($r.Output,  'output sha256\s+:\s+([0-9a-f]{64})')).Groups[1].Value
    $h2 = ([regex]::Match($r2.Output, 'output sha256\s+:\s+([0-9a-f]{64})')).Groups[1].Value
    Assert-True 'L4 transformation is reproducible (same output hash)' ($h1 -eq $h2 -and $h1.Length -eq 64) "h1=$h1 h2=$h2"

    # Without the switch the same file is simply refused - transformation is never implicit.
    $r3 = Invoke-Importer -Sql $goodMaster
    Assert-True 'L5 without -TransformMaster the same file is refused' ($r3.ExitCode -ne 0) 'was accepted'

    # A dump that ALSO carries an admin statement must not be "rescued" by the transformer.
    $r4 = Invoke-Importer -Sql "USE pems_db;`nGRANT ALL PRIVILEGES ON *.* TO x@localhost;`nCREATE TABLE t (id INT);" -Transform
    Assert-True 'L6 transformer refuses when admin statements are also present' ($r4.ExitCode -ne 0) 'admin statement was transformed away'

    # A leftover fully-qualified protected reference must survive header removal and fail.
    $r5 = Invoke-Importer -Sql "USE pems_db;`nINSERT INTO pems_db.t VALUES (1);" -Transform
    Assert-True 'L7 transformer refuses a leftover protected reference' ($r5.ExitCode -ne 0) 'protected reference slipped through'

    # Nothing to transform means the source is not the expected shape.
    $r6 = Invoke-Importer -Sql "SELECT 1;" -Transform
    Assert-True 'L8 transform of an already-safe file is a no-op success' ($r6.ExitCode -eq 0) ("exit $($r6.ExitCode)")

    # A fresh-create dump recreates its own schema first. All three preamble statements are
    # removable; the output is still re-scanned, so this is not a blanket exemption.
    $freshCreate = "DROP DATABASE IF EXISTS pems_db;`nCREATE DATABASE pems_db;`nUSE pems_db;`nCREATE TABLE t (id INT);"
    $r7 = Invoke-Importer -Sql $freshCreate -Transform
    Assert-True 'L10 fresh-create preamble (DROP+CREATE+USE) transforms' ($r7.ExitCode -eq 0) ("exit $($r7.ExitCode): " + $r7.Output)

    # A DROP DATABASE aimed at something the preamble did not create must still not be laundered
    # into a "safe" artifact just because -TransformMaster was passed... it is removed, but the
    # re-scan is what decides, and a leftover protected reference still fails.
    $r8 = Invoke-Importer -Sql "DROP DATABASE pems_test;`nSELECT * FROM pems_test.users;" -Transform
    Assert-True 'L11 leftover protected reference still fails after preamble removal' ($r8.ExitCode -ne 0) 'protected reference laundered'

    Assert-True 'L9 no mysql process was spawned in any transform case' (-not (Test-Path -LiteralPath $spyLog)) 'the spy was invoked'
}
finally {
    Remove-Item -LiteralPath $txDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host '=== M. Review mode is a separate allowlist, not a widened one ==='

# The review database is long-lived application data; the drill databases get dropped and rebuilt
# by destructive migrations. Merging the two lists would let a review fixture land on a database
# mid-migration, or hand the review database to the Phase I destructive runner. -Mode keeps them
# apart, and these cases prove the separation actually holds rather than being a comment.
$rmDir = Join-Path ([System.IO.Path]::GetTempPath()) ('pems_rm_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $rmDir | Out-Null
try {
    $spyLog = Join-Path $rmDir 'invocations.log'
    $spyExe = Join-Path $rmDir 'mysql.cmd'
    Set-Content -LiteralPath $spyExe -Value ("@echo off`r`necho INVOKED %* >> `"$spyLog`"`r`nexit /b 0") -Encoding ASCII
    $importer = Join-Path $PSScriptRoot '..\import_disposable_fixture.ps1'

    $safeSql = Join-Path $rmDir 'safe.sql'
    Set-Content -LiteralPath $safeSql -Value "CREATE TABLE t (id INT);" -Encoding ASCII
    $unsafeSql = Join-Path $rmDir 'unsafe.sql'
    Set-Content -LiteralPath $unsafeSql -Value "USE pems_db;`r`nDROP TABLE t;" -Encoding ASCII

    function Invoke-Mode {
        param([string]$Db, [string]$ModeName, [string]$Sql)
        $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $importer `
            -DbName $Db -Mode $ModeName -SqlPath $Sql -MysqlExe $spyExe -ScanOnly 2>&1 | Out-String
        return @{ Output = $out; ExitCode = $LASTEXITCODE }
    }

    $m1 = Invoke-Mode 'pems_review_v2' 'Review' $safeSql
    Assert-True 'M1 review target accepted in Review mode' ($m1.ExitCode -eq 0) ("exit $($m1.ExitCode): " + $m1.Output)

    $m2 = Invoke-Mode 'pems_review_v2' 'Drill' $safeSql
    Assert-True 'M2 review target REFUSED in Drill mode' ($m2.ExitCode -ne 0) 'review db reachable from drill mode'

    $m3 = Invoke-Mode 'pems_i_upgrade' 'Review' $safeSql
    Assert-True 'M3 drill target REFUSED in Review mode' ($m3.ExitCode -ne 0) 'drill db reachable from review mode'

    $m4 = Invoke-Mode 'pems_i_upgrade' 'Drill' $safeSql
    Assert-True 'M4 drill target accepted in Drill mode' ($m4.ExitCode -eq 0) ("exit $($m4.ExitCode)")

    # Review mode must not weaken the denylist in any way.
    $m5 = Invoke-Mode 'pems_review_v2' 'Review' $unsafeSql
    Assert-True 'M5 review mode still refuses a protected USE' ($m5.ExitCode -ne 0) 'review mode weakened the denylist'

    Assert-True 'M6 no mysql process was spawned in any review-mode case' (-not (Test-Path -LiteralPath $spyLog)) 'the spy was invoked'
}
finally {
    Remove-Item -LiteralPath $rmDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host '=== N. The Phase I destructive runner allowlist is unchanged ==='

# Adding the review database to the importer must NOT leak into the destructive runner, which
# drops columns and rebuilds indexes. Assert on its actual ValidateSet.
$runner = Join-Path $PSScriptRoot '..\run_migration.ps1'
if (Test-Path -LiteralPath $runner) {
    $runnerText = Get-Content -LiteralPath $runner -Raw
    Assert-True 'N1 destructive runner still lists exactly the four drill databases' `
        ($runnerText -match "ValidateSet\('pems_i_fresh',\s*'pems_i_upgrade',\s*'pems_i_refusal',\s*'pems_i_rollback'\)") `
        'runner allowlist changed'
    Assert-True 'N2 destructive runner does not mention the review database' `
        (-not ($runnerText -match 'pems_review_v2')) 'review db leaked into the destructive runner'
} else {
    Write-Host 'SKIP  N: run_migration.ps1 not found'
}

Write-Host ''
Write-Host '=== K. The real master dump must be rejected by the raw guard ==='

# Resolve the ONE canonical schema script instead of a hard-coded (renameable) filename.
$masterCandidates = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..\..') -Filter 'PEMS_FULL_*.sql' -File -ErrorAction SilentlyContinue)
$master = if ($masterCandidates.Count -eq 1) { $masterCandidates[0].FullName } else { $null }
if ($master -and (Test-Path -LiteralPath $master)) {
    $mv = Test-SqlFileSafety -Path $master
    Assert-True 'K1 authoritative master is NOT safe for direct import' (-not $mv.IsSafe) 'master scanned as safe'
    $kinds = @($mv.Findings | ForEach-Object { $_.Kind } | Sort-Object -Unique)
    Assert-True 'K2 master is flagged for database control' ($kinds -contains 'DATABASE_CONTROL') ("kinds: " + ($kinds -join ','))
    Write-Host ("      master sha256 = {0}" -f $mv.Sha256)
    Write-Host ("      master findings = {0}" -f $mv.Findings.Count)
} else {
    Write-Host 'SKIP  K: authoritative master not present at the expected path'
}

Write-Host ''
Write-Host ('==== {0} passed, {1} failed ====' -f $script:Pass, $script:Fail)
if ($script:Fail -gt 0) { exit 1 }
exit 0
