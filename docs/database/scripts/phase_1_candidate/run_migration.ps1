param (
    [Parameter(Mandatory=$true)]
    [ValidateSet("pems_i_fresh", "pems_i_upgrade", "pems_i_refusal", "pems_i_rollback")]
    [string]$DbName,

    [Parameter(Mandatory=$true)]
    [ValidateSet("Drop", "Restore", "Verify", "Preflight")]
    [string]$Action,

    [Parameter(Mandatory=$false)]
    [switch]$OverrideBlockers
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "PEMS Phase 1 Migration Guard Runner"
Write-Host "========================================"
Write-Host "Target Database: $DbName"
Write-Host "Action: $Action"

if (-not (Get-Command mysql -ErrorAction SilentlyContinue)) {
    Write-Error "mysql client is not available in PATH. Cannot run migration."
    exit 1
}

Write-Host "Checking database existence and connection..."
$dbCheck = mysql -u root -e "SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '$DbName';" --batch --skip-column-names
if ([string]::IsNullOrWhiteSpace($dbCheck)) {
    Write-Error "Database $DbName does not exist or cannot be connected."
    exit 1
}

$scriptFile = ""
$envVars = ""

if ($OverrideBlockers) {
    $envVars += "SET @OVERRIDE_RUNTIME_BLOCKERS=1; "
}

if ($Action -eq "Drop") {
    $scriptFile = "02_guarded_up.sql"
    $envVars += "SET @ENABLE_PHASE_1_DROP=1; "
} elseif ($Action -eq "Restore") {
    $scriptFile = "04_down_restore.sql"
    $envVars += "SET @ENABLE_PHASE_1_RESTORE=1; "
} elseif ($Action -eq "Verify") {
    $scriptFile = "03_verify.sql"
} elseif ($Action -eq "Preflight") {
    $scriptFile = "01_preflight.sql"
}

if (-not (Test-Path $scriptFile)) {
    Write-Error "Script file $scriptFile not found in current directory."
    exit 1
}

Write-Host "Guard checks PASSED. Executing $scriptFile..."
try {
    $command = "$envVars source $scriptFile;"
    $output = Write-Output $command | mysql -u root $DbName 2>&1
    Write-Host $output
    
    if ($output -match "FAIL" -or $output -match "ERROR") {
        Write-Error "Execution failed or validation gate returned FAIL. Output: $output"
        exit 1
    }
    
    Write-Host "Execution of $scriptFile completed successfully."
} catch {
    Write-Error "Failed to execute script: $_"
    exit 1
}
exit 0
