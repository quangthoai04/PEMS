<#
    Safe SQL import into a DISPOSABLE database.

    This is the only supported way to get SQL into a drill database. Piping a dump straight into
    mysql is what caused the 2026-07-20 incident: the payload's own `USE pems_db;` moved the
    session off the target named on the command line.

    ORDER OF OPERATIONS (each step must pass before the next runs):
      1. exact target allowlist                      - no prefix matching
      2. statement-aware safety scan of the payload  - BEFORE any process is spawned
      3. optional asserted transformation            - for the known master only, into a NEW file
      4. credential privilege classification         - refuse root/global/protected grants
      5. protected-schema fingerprints (read-only)   - captured before the import
      6. SELECT DATABASE() must equal the target     - before the payload runs
      7. run the validated bytes                     - never a re-read of the source path
      8. SELECT DATABASE() must still equal target   - after the payload runs
      9. protected fingerprints must be unchanged    - the import proved it stayed in its lane
     10. temp artifacts removed in finally

    Steps 6 and 8 are a backstop, NOT the guard. By the time a session exists it is already too
    late to discover a hostile payload; step 2 is what actually prevents the incident.

    ASCII-only on purpose (Windows PowerShell 5.1 reads .ps1 as ANSI without a BOM).
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback')]
    [string]$DbName,

    [Parameter(Mandatory = $true)]
    [string]$SqlPath,

    # Treat SqlPath as the known authoritative master and run the asserted header transformation
    # instead of rejecting it outright. Without this, a dump carrying database-control statements
    # is simply refused.
    [switch]$TransformMaster,

    # Validate only: never connect, never spawn mysql. Used by the test suite.
    [switch]$ScanOnly,

    [string]$MysqlExe      = $(if ($env:MYSQL_BIN)  { $env:MYSQL_BIN }  else { 'mysql' }),
    [string]$MysqlUser     = $(if ($env:MYSQL_USER) { $env:MYSQL_USER } else { '' }),
    [string]$MysqlPassword = $env:MYSQL_PASSWORD,
    [string]$MysqlHost     = $(if ($env:MYSQL_HOST) { $env:MYSQL_HOST } else { 'localhost' }),
    [string]$MysqlPort     = $(if ($env:MYSQL_PORT) { $env:MYSQL_PORT } else { '3306' }),

    # Escape hatch for an isolated MySQL instance that legitimately only has a root account and
    # contains NO protected databases. Must be stated explicitly; it is never the default.
    [switch]$AllowPrivilegedCredential
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\SqlSafetyGuard.ps1')

$ALLOWLIST  = @('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback')
$PROTECTED  = @('pems_db', 'pems_test', 'pems_pr3_test')

function Fail([string]$message) {
    Write-Host ''
    Write-Host "REFUSED: $message"
    exit 1
}

Write-Host '========================================'
Write-Host 'PEMS disposable fixture import (safe path)'
Write-Host '========================================'
Write-Host ("Target database : {0}" -f $DbName)
Write-Host ("Payload         : {0}" -f $SqlPath)

# ---- 1. exact allowlist ---------------------------------------------------
# ValidateSet already constrains this; re-checking costs nothing and a destructive tool should
# never depend on a single gate.
if ($ALLOWLIST -notcontains $DbName) { Fail "'$DbName' is not in the exact disposable allowlist." }
if ($PROTECTED -contains $DbName)    { Fail "'$DbName' is a protected database." }

# ---- 2. statement-aware safety scan ---------------------------------------
$scan = Test-SqlFileSafety -Path $SqlPath
Write-Host ("SHA-256         : {0}" -f $scan.Sha256)
Write-Host ("Statements      : {0}" -f $scan.StatementCount)
Write-Host ''

$payloadText = $scan.Content
$payloadLabel = 'source payload'

if (-not $scan.IsSafe) {
    Write-SafetyFindings -Verdict $scan -Label 'source payload'

    if (-not $TransformMaster) {
        Fail 'the payload contains database-control, admin, client or protected-schema statements. mysql was NOT invoked; zero database mutation.'
    }

    # ---- 3. asserted transformation (known master only) --------------------
    # This is NOT "strip the header". Every removal is asserted: exactly the expected
    # database-control statements may be removed, nothing else, and the result must then pass the
    # SAME guard that rejected the original.
    Write-Host ''
    Write-Host '-- TransformMaster: attempting asserted header transformation --'

    $statements = Split-SqlStatements -Sql $payloadText
    $removable = @($statements | Where-Object {
        $_.Normalized -match '^USE(\s|`|$)' -or $_.Normalized -match '^CREATE\s+(DATABASE|SCHEMA)(\s|`|$)'
    })
    $other = @($scan.Findings | Where-Object { $_.Kind -ne 'DATABASE_CONTROL' })

    if ($other.Count -gt 0) {
        Fail "transformation refused: the payload also contains $($other.Count) non-database-control finding(s) (admin/client/protected/dynamic). Those are never transformed away."
    }
    if ($removable.Count -eq 0) {
        Fail 'transformation refused: no USE / CREATE DATABASE statement was found, so the source does not match the expected master shape.'
    }

    Write-Host ("   removing {0} database-control statement(s):" -f $removable.Count)
    foreach ($r in $removable) {
        Write-Host ("     line {0}: {1}" -f $r.Line, (($r.Raw -replace '\s+', ' ')))
    }

    # Rebuild by removing exactly those statements' raw text. Everything else - including string
    # contents, comments and data values - is preserved byte-for-byte.
    $transformed = $payloadText
    foreach ($r in $removable) {
        $needle = $r.Raw
        $idx = $transformed.IndexOf($needle, [System.StringComparison]::Ordinal)
        if ($idx -lt 0) {
            Fail "transformation refused: could not locate statement #$($r.Index) verbatim; the source does not match what was scanned."
        }
        $transformed = $transformed.Remove($idx, $needle.Length)
    }

    $recheck = Test-SqlPayloadSafety -Sql $transformed
    if (-not $recheck.IsSafe) {
        Write-SafetyFindings -Verdict $recheck -Label 'transformed payload'
        Fail 'transformation produced an artifact that still fails the guard. No output was published; mysql was NOT invoked.'
    }

    $outBytes = [System.Text.Encoding]::UTF8.GetBytes($transformed)
    $outHash = [System.BitConverter]::ToString(
        [System.Security.Cryptography.SHA256]::Create().ComputeHash($outBytes)).Replace('-', '').ToLowerInvariant()

    Write-Host ''
    Write-Host '   TRANSFORM MANIFEST'
    Write-Host ("     source sha256      : {0}" -f $scan.Sha256)
    Write-Host ("     source statements  : {0}" -f $scan.StatementCount)
    Write-Host ("     removed statements : {0}" -f $removable.Count)
    Write-Host ("     output statements  : {0}" -f $recheck.StatementCount)
    Write-Host ("     output sha256      : {0}" -f $outHash)
    Write-Host ("     target database    : {0}" -f $DbName)

    $payloadText = $transformed
    $payloadLabel = 'transformed payload'
}
else {
    Write-SafetyFindings -Verdict $scan -Label 'source payload'
}

Write-Host ''
Write-Host ("Safety gate     : PASS ({0})" -f $payloadLabel)

if ($ScanOnly) {
    Write-Host 'ScanOnly: no connection attempted, no mysql process spawned.'
    exit 0
}

# ---- resolve the client ---------------------------------------------------
if (-not (Test-Path -LiteralPath $MysqlExe)) {
    $onPath = Get-Command $MysqlExe -ErrorAction SilentlyContinue
    if (-not $onPath) { Fail "mysql client not found. Pass -MysqlExe or set MYSQL_BIN." }
    $MysqlExe = $onPath.Source
}
if ([string]::IsNullOrWhiteSpace($MysqlUser)) {
    Fail 'no MySQL user supplied. Set MYSQL_USER (a restricted drill account) or pass -MysqlUser. This tool does not default to root.'
}

# Runs SQL text against the target and returns the output plus the real exit code. The password
# travels via MYSQL_PWD so it stays out of the process list, and stderr is merged INSIDE the cmd
# string because PowerShell's own 2>&1 wraps native stderr in NativeCommandError.
function Invoke-Sql {
    param([string]$Text, [switch]$NoDatabase)

    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('pems_import_' + [guid]::NewGuid().ToString('N') + '.sql')
    [System.IO.File]::WriteAllText($tmp, $Text, (New-Object System.Text.UTF8Encoding($false)))

    $priorPwd = $env:MYSQL_PWD
    try {
        if (-not [string]::IsNullOrEmpty($MysqlPassword)) { $env:MYSQL_PWD = $MysqlPassword }
        $dbArg = if ($NoDatabase) { '' } else { " $DbName" }
        $cmd = '"' + $MysqlExe + '" -u' + $MysqlUser + ' -h' + $MysqlHost + ' -P' + $MysqlPort +
               ' --default-character-set=utf8mb4 -N -B' + $dbArg + ' < "' + $tmp + '" 2>&1'
        $out = & cmd.exe /c $cmd | Out-String
        return @{ Output = $out; ExitCode = $LASTEXITCODE }
    }
    finally {
        if ([string]::IsNullOrEmpty($priorPwd)) { Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue }
        else { $env:MYSQL_PWD = $priorPwd }
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    }
}

# ---- 4. credential privilege classification -------------------------------
# A drill account that can write to pems_db makes every other control advisory. Refuse before
# touching anything.
Write-Host ''
Write-Host '-- Credential check --'
$who = Invoke-Sql -Text 'SELECT CURRENT_USER();' -NoDatabase
if ($who.ExitCode -ne 0) { Fail "could not connect (exit $($who.ExitCode)). Output: $($who.Output.Trim())" }
$currentUser = $who.Output.Trim()
Write-Host ("Current user    : {0}" -f $currentUser)

$grants = Invoke-Sql -Text 'SHOW GRANTS FOR CURRENT_USER();' -NoDatabase
if ($grants.ExitCode -ne 0) { Fail "could not read grants (exit $($grants.ExitCode))." }

$grantText = $grants.Output
$privilegeProblems = New-Object System.Collections.ArrayList
if ($grantText -match 'ALL PRIVILEGES ON \*\.\*' -or $grantText -match 'GRANT ALL PRIVILEGES ON `?\*`?\.\*') {
    [void]$privilegeProblems.Add('holds ALL PRIVILEGES ON *.*')
}
if ($grantText -match 'SUPER|GRANT OPTION') {
    [void]$privilegeProblems.Add('holds SUPER and/or GRANT OPTION')
}
foreach ($p in $PROTECTED) {
    if ($grantText -match ('ON `?' + [regex]::Escape($p) + '`?\.')) {
        [void]$privilegeProblems.Add("holds grants on the protected schema $p")
    }
}
if ($currentUser -match '^root@') { [void]$privilegeProblems.Add('is the root account') }

if ($privilegeProblems.Count -gt 0) {
    Write-Host 'Privilege classification: PRIVILEGED'
    foreach ($p in $privilegeProblems) { Write-Host ("  - {0}" -f $p) }
    if (-not $AllowPrivilegedCredential) {
        Fail ("this credential can reach protected databases. Create a restricted drill account " +
              "(see restricted_drill_user.sql) or run against an isolated instance. " +
              "mysql was connected but the payload was NOT executed; zero mutation.")
    }
    Write-Host 'WARNING: -AllowPrivilegedCredential was supplied. This is only valid on an isolated'
    Write-Host '         instance that contains no protected databases. It is NOT evidence of safety.'
}
else {
    Write-Host 'Privilege classification: RESTRICTED (no global, no protected-schema grants)'
}

# ---- 5. protected fingerprints (read-only) --------------------------------
function Get-ProtectedFingerprint {
    $sql = @"
SELECT IFNULL(GROUP_CONCAT(CONCAT(table_schema,'.',table_name,':',IFNULL(table_rows,0)) ORDER BY table_schema,table_name SEPARATOR '|'),'<none>')
FROM information_schema.tables
WHERE table_schema IN ('pems_db','pems_test','pems_pr3_test');
"@
    $r = Invoke-Sql -Text $sql -NoDatabase
    if ($r.ExitCode -ne 0) { return $null }
    $raw = $r.Output.Trim()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($raw)
    return [System.BitConverter]::ToString(
        [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
}

$fpBefore = Get-ProtectedFingerprint
Write-Host ''
Write-Host ("Protected FP before : {0}" -f $fpBefore)

# ---- 6. target assertion before the payload -------------------------------
$dbCheck = Invoke-Sql -Text 'SELECT DATABASE();'
if ($dbCheck.ExitCode -ne 0) { Fail "could not select the target database (exit $($dbCheck.ExitCode)). Does '$DbName' exist?" }
$actual = $dbCheck.Output.Trim()
if ($actual -ne $DbName) { Fail "session default is '$actual', expected '$DbName'." }
Write-Host ("Pre-import DATABASE(): {0}" -f $actual)

# ---- 7 + 8. run the validated bytes, then re-assert the target ------------
# The payload is followed by a DATABASE() probe in the SAME session, so a `USE` that somehow
# survived the guard would still be caught before the result is reported as success.
Write-Host ''
Write-Host '-- Importing --'
$result = Invoke-Sql -Text ($payloadText + "`nSELECT CONCAT('POST_IMPORT_DATABASE:', DATABASE());`n")

if ($result.ExitCode -ne 0) {
    Write-Host $result.Output.Trim()
    Fail "import failed (mysql exit $($result.ExitCode))."
}

if ($result.Output -notmatch 'POST_IMPORT_DATABASE:(\S*)') {
    Fail 'could not read the post-import session database; refusing to report success.'
}
$postDb = $Matches[1]
if ($postDb -ne $DbName) {
    Fail "the payload moved the session to '$postDb'. The guard must be fixed before any further import."
}
Write-Host ("Post-import DATABASE(): {0}" -f $postDb)

# ---- 9. protected fingerprints unchanged ----------------------------------
$fpAfter = Get-ProtectedFingerprint
Write-Host ("Protected FP after  : {0}" -f $fpAfter)
if ($fpBefore -ne $fpAfter) {
    Fail 'protected-database fingerprint CHANGED during the import. Treat this as an incident.'
}

Write-Host ''
Write-Host ("IMPORT OK: {0} imported into {1}; protected fingerprints unchanged." -f $payloadLabel, $DbName)
exit 0
