<#
    PEMS Phase I - FAIL-CLOSED migration runner (disposable databases only).

    NOTE: this file is deliberately ASCII-only. Windows PowerShell 5.1 reads .ps1 files as
    ANSI unless they carry a UTF-8 BOM, and a UTF-8 em dash then decodes to a byte that PS
    treats as a smart closing quote, which breaks parsing.

    Design contract (corrective section 6.1): read-only gate FIRST, DDL payload ONLY after.
      * every path resolves from $PSScriptRoot (never the caller's working directory);
      * connection settings come from parameters/environment, never hardcoded, never logged;
      * the target database must be in the EXACT disposable allowlist (no prefix matching);
      * a destructive action ALWAYS runs 01_preflight.sql first and refuses unless the
        script emits "PHASE1_PREFLIGHT_RESULT: PASS" AND mysql exits 0;
      * the payload is invoked with @PHASE1_PREFLIGHT_OK=1, which the SQL itself requires,
        so the DDL cannot run without a passed gate even if invoked by another tool;
      * nothing prints PASSED before the gates have actually passed;
      * 03_verify.sql runs automatically after UP and after DOWN;
      * any gate failure, non-zero exit code or unparseable output stops with exit code 1.

    Usage:
      .\run_migration.ps1 -DbName pems_i_upgrade  -Action Preflight
      .\run_migration.ps1 -DbName pems_i_upgrade  -Action Up -OverrideBlockers
      .\run_migration.ps1 -DbName pems_i_rollback -Action Down
      .\run_migration.ps1 -DbName pems_i_fresh    -Action Verify -VerifyMode UP

    -OverrideBlockers acknowledges that runtime V1 dependencies still exist. It is valid
    ONLY for a disposable drill; it is never evidence that production is ready.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback')]
    [string]$DbName,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Preflight', 'Up', 'Down', 'Verify')]
    [string]$Action,

    [switch]$OverrideBlockers,

    [ValidateSet('UP', 'DOWN')]
    [string]$VerifyMode = 'UP',

    [string]$MysqlExe      = $(if ($env:MYSQL_BIN)  { $env:MYSQL_BIN }  else { 'mysql' }),
    [string]$MysqlUser     = $(if ($env:MYSQL_USER) { $env:MYSQL_USER } else { 'root' }),
    [string]$MysqlPassword = $env:MYSQL_PASSWORD,
    [string]$MysqlHost     = $(if ($env:MYSQL_HOST) { $env:MYSQL_HOST } else { 'localhost' }),
    [string]$MysqlPort     = $(if ($env:MYSQL_PORT) { $env:MYSQL_PORT } else { '3306' })
)

$ErrorActionPreference = 'Stop'
$ALLOWLIST = @('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback')

function Write-Section($text) { Write-Host ''; Write-Host "== $text" }

# Resolve the mysql client (never assume it is on PATH).
if (-not (Test-Path $MysqlExe)) {
    $onPath = Get-Command $MysqlExe -ErrorAction SilentlyContinue
    if (-not $onPath) {
        Write-Host 'ERROR: mysql client not found. Pass -MysqlExe or set MYSQL_BIN.'
        exit 1
    }
    $MysqlExe = $onPath.Source
}

# Defence in depth: the parameter is already ValidateSet-constrained, but never rely on a
# single gate for a destructive tool, and never match by prefix (for example 'pems_i_%').
if ($ALLOWLIST -notcontains $DbName) {
    Write-Host ("ERROR: '" + $DbName + "' is not in the exact disposable allowlist.")
    exit 1
}

# Executes a .sql file with a variable prelude. Returns the output text and the real exit code.
function Invoke-SqlFile {
    param([string]$File, [string[]]$Prelude)

    $path = Join-Path $PSScriptRoot $File
    if (-not (Test-Path $path)) {
        Write-Host ("ERROR: script not found: " + $path)
        return @{ Output = ''; ExitCode = 1 }
    }

    # Combine prelude + payload into one temp script under a space-free temp dir so the
    # redirect works regardless of where the repository lives. BOM-less UTF-8: a BOM would
    # be sent to mysql as data and break the first statement.
    $tmp  = Join-Path ([System.IO.Path]::GetTempPath()) ('pems_phase1_' + [guid]::NewGuid().ToString('N') + '.sql')
    $body = ($Prelude -join "`n") + "`n" + (Get-Content -Path $path -Raw)
    [System.IO.File]::WriteAllText($tmp, $body, (New-Object System.Text.UTF8Encoding($false)))

    $priorPwd = $env:MYSQL_PWD
    try {
        # Pass the password via MYSQL_PWD rather than -p: it keeps the secret out of the
        # command line (and therefore out of the process list) and avoids the client's
        # "using a password on the command line is insecure" warning.
        if (-not [string]::IsNullOrEmpty($MysqlPassword)) { $env:MYSQL_PWD = $MysqlPassword }

        # stderr is merged INSIDE the cmd string. Using PowerShell's own 2>&1 on a native
        # command wraps each stderr line in a NativeCommandError, which $ErrorActionPreference
        # = 'Stop' would turn into a terminating error.
        $cmd = '"' + $MysqlExe + '" -u' + $MysqlUser + ' -h' + $MysqlHost +
               ' -P' + $MysqlPort + ' --default-character-set=utf8mb4 ' + $DbName +
               ' < "' + $tmp + '" 2>&1'
        $out = & cmd.exe /c $cmd | Out-String
        return @{ Output = $out; ExitCode = $LASTEXITCODE }
    }
    finally {
        # Restore the caller's environment exactly as we found it.
        if ([string]::IsNullOrEmpty($priorPwd)) { Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue }
        else { $env:MYSQL_PWD = $priorPwd }
        Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    }
}

# Runs the read-only gate and returns $true ONLY on an explicit PASS verdict + exit code 0.
function Test-Preflight {
    Write-Section ('Preflight gate (read-only) on ' + $DbName)
    $prelude = @()
    if ($OverrideBlockers) { $prelude += 'SET @OVERRIDE_RUNTIME_BLOCKERS = 1;' }
    $r = Invoke-SqlFile -File '01_preflight.sql' -Prelude $prelude
    Write-Host $r.Output.TrimEnd()

    if ($r.ExitCode -ne 0) {
        Write-Host ('Preflight FAILED: mysql exit code ' + $r.ExitCode + '.')
        return $false
    }
    if ($r.Output -notmatch 'PHASE1_PREFLIGHT_RESULT:\s*(PASS|FAIL)') {
        Write-Host 'Preflight FAILED: verdict token not found (output not parseable).'
        return $false
    }
    if ($Matches[1] -ne 'PASS') {
        Write-Host 'Preflight FAILED: at least one gate returned FAIL.'
        return $false
    }
    return $true
}

function Invoke-Verify {
    param([string]$Mode)
    Write-Section ('Verify (' + $Mode + ') on ' + $DbName)
    $r = Invoke-SqlFile -File '03_verify.sql' -Prelude @("SET @PHASE1_VERIFY_MODE = '$Mode';")
    Write-Host $r.Output.TrimEnd()

    if ($r.ExitCode -ne 0) {
        Write-Host ('Verify FAILED: mysql exit code ' + $r.ExitCode + '.')
        return $false
    }
    if ($r.Output -notmatch 'PHASE1_VERIFY_RESULT:\s*(PASS|FAIL)') {
        Write-Host 'Verify FAILED: verdict token not found.'
        return $false
    }
    return ($Matches[1] -eq 'PASS')
}

Write-Host '========================================'
Write-Host 'PEMS Phase I migration runner (fail-closed)'
Write-Host '========================================'
Write-Host ('Target database  : ' + $DbName)
Write-Host ('Action           : ' + $Action)
Write-Host ('Blocker override : ' + [bool]$OverrideBlockers)

if ($Action -eq 'Preflight') {
    if (Test-Preflight) { Write-Host 'Preflight gate: PASS'; exit 0 }
    Write-Host 'Preflight gate: FAIL'
    exit 1
}

if ($Action -eq 'Up') {
    # Destructive. The gate ALWAYS runs first and the payload is skipped unless it passes.
    if (-not (Test-Preflight)) {
        Write-Host ''
        Write-Host 'REFUSED: preflight did not pass - the UP payload was NOT executed (zero mutation).'
        exit 1
    }
    Write-Host 'Preflight gate: PASS - proceeding to the guarded UP payload.'

    Write-Section ('Guarded UP on ' + $DbName)
    $r = Invoke-SqlFile -File '02_guarded_up.sql' -Prelude @(
        'SET @ENABLE_PHASE_1_DROP = 1;',
        'SET @PHASE1_PREFLIGHT_OK = 1;'
    )
    Write-Host $r.Output.TrimEnd()
    if ($r.ExitCode -ne 0 -or $r.Output -notmatch 'PHASE1_UP_RESULT:\s*DONE') {
        Write-Host ('UP FAILED (exit ' + $r.ExitCode + '). NOTE: MySQL DDL auto-commits - inspect the schema before retrying.')
        exit 1
    }

    if (Invoke-Verify -Mode 'UP') { Write-Host ''; Write-Host 'UP + verify: PASS'; exit 0 }
    Write-Host ''
    Write-Host 'UP applied but verify FAILED.'
    exit 1
}

if ($Action -eq 'Down') {
    Write-Section ('Guarded DOWN / restore on ' + $DbName)
    $r = Invoke-SqlFile -File '04_down_restore.sql' -Prelude @('SET @ENABLE_PHASE_1_RESTORE = 1;')
    Write-Host $r.Output.TrimEnd()
    if ($r.ExitCode -ne 0 -or $r.Output -notmatch 'PHASE1_DOWN_RESULT:\s*DONE') {
        Write-Host ('DOWN FAILED (exit ' + $r.ExitCode + '). The restore refuses to fabricate NOT NULL values; inspect the backfill.')
        exit 1
    }

    if (Invoke-Verify -Mode 'DOWN') { Write-Host ''; Write-Host 'DOWN + verify: PASS'; exit 0 }
    Write-Host ''
    Write-Host 'DOWN applied but verify FAILED.'
    exit 1
}

if ($Action -eq 'Verify') {
    if (Invoke-Verify -Mode $VerifyMode) { Write-Host ('Verify (' + $VerifyMode + '): PASS'); exit 0 }
    Write-Host ('Verify (' + $VerifyMode + '): FAIL')
    exit 1
}
