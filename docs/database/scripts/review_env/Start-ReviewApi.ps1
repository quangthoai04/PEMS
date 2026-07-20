<#
    Starts the PEMS API against the isolated review database with EVERY outbound integration
    disabled, using process environment variables only.

    WHY THIS SCRIPT EXISTS
    ----------------------
    Pointing the API at a review database is not enough to make it safe. On 2026-07-20 the first
    review start-up sent a REAL email: the reminder background job ran over seed data, and
    appsettings.json ships Smtp:Enabled=true with live Gmail credentials. A review environment
    holds production-shaped data, so every outbound side effect fires against real addresses and
    real third-party APIs unless it is switched off deliberately.

    Choosing the database is one decision; choosing what the process may talk to is a separate
    one, and it has to be made explicitly.

    NOTHING HERE EDITS appsettings.json. Every value below is a process environment variable, so
    production behaviour and the committed configuration are untouched, and no credential is
    removed or rewritten - the integrations are simply never reached.

    ASCII-only (Windows PowerShell 5.1 reads .ps1 as ANSI without a BOM).
#>
param(
    [string]$MysqlUser     = 'pems_review',
    [string]$MysqlPassword = $env:MYSQL_PASSWORD,
    [string]$MysqlHost     = 'localhost',
    [string]$MysqlPort     = '3306',
    [string]$Urls          = 'http://localhost:5299',
    # The frontend origin the review browser runs on. Must be allowed by CORS or every browser API
    # call fails with a Network Error and the app bounces to /login. The committed allowlist covers
    # :5173; a non-default review port is added below via an indexed env override (no appsettings edit).
    [string]$FrontendOrigin = 'http://localhost:5273',
    [string]$BaseOutputPath
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$REVIEW_DB = 'pems_review_v2'

if ([string]::IsNullOrWhiteSpace($MysqlPassword)) {
    throw 'Supply the review DB password via -MysqlPassword or $env:MYSQL_PASSWORD. It is never stored in this script.'
}

$repoRoot = $PSScriptRoot
while ($repoRoot -and -not ((Test-Path (Join-Path $repoRoot 'backend')) -and (Test-Path (Join-Path $repoRoot 'tests')))) {
    $repoRoot = Split-Path -Parent $repoRoot
}
if (-not $repoRoot) { throw "Could not locate the repository root from $PSScriptRoot." }

# ---- database: the review database, via the restricted account -------------
$env:ConnectionStrings__DefaultConnection =
    "server=$MysqlHost;port=$MysqlPort;database=$REVIEW_DB;user=$MysqlUser;Password=$MysqlPassword;AllowUserVariables=True;GuidFormat=None"

# ---- per-campus v2 feature flags: ON for review only -----------------------
# Both default to false in code and are absent from every committed appsettings*.json.
$env:PerCampusFormV2__Enabled      = 'true'
$env:PerCampusFormV2Write__Enabled = 'true'

# ---- outbound kill switches ------------------------------------------------
# SMTP. EmailService reads Smtp:Enabled and, when false, LOGS the message instead of dispatching
# it ("[EmailService-DEV] To:..."). The mail pipeline still runs end to end, so email-driven
# journeys stay reviewable - nothing leaves the machine.
$env:Smtp__Enabled = 'false'

# Google Drive file storage. With this off, uploads stay on local disk.
$env:GoogleDrive__Enabled = 'false'
$env:Storage__Provider    = 'Local'

# Cloudflare Turnstile human verification (already false in the committed config; pinned so a
# future default flip cannot silently start calling out).
$env:Turnstile__Enabled = 'false'

# FeID SSO. An empty base URL leaves the integration inert.
$env:Feid__BaseUrl = ''

# Document AI OCR: the provider is selected by api_code from the api_configurations table, and
# every row in the review database has been set to DISABLED. Blanking the default code as well
# means there is nothing to resolve even if a row were re-enabled by hand.
$env:BusinessCardOcr__DefaultApiCode = ''

# NOTE ON WHAT IS NOT AN ENV SWITCH
# Google Translate and Document AI credentials live in api_configurations rows in the DATABASE,
# not in appsettings. They are neutralised in the review database itself (status = DISABLED),
# which is review data - not production configuration.

# ---- CORS: allow the review frontend origin ------------------------------
# The committed Cors:AllowedOrigins array (indices 0..3) covers localhost:3000/3001/3002/5173.
# Append the review origin at the next index rather than editing appsettings.json. ASP.NET binds
# Cors__AllowedOrigins__4 as a new array element.
$env:Cors__AllowedOrigins__4 = $FrontendOrigin

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS        = $Urls

Write-Host '========================================'
Write-Host 'PEMS review API'
Write-Host '========================================'
Write-Host ("Database : {0} (user {1})" -f $REVIEW_DB, $MysqlUser)
Write-Host ("URLs     : {0}" -f $Urls)
Write-Host ("CORS +   : {0}" -f $FrontendOrigin)
Write-Host 'V2 flags : read=ON write=ON (process only)'
Write-Host 'Outbound : SMTP off, Drive off, Turnstile off, FeID inert, OCR unresolvable'
Write-Host ''

$runArgs = @('run', '--project', (Join-Path $repoRoot 'backend\PEMS.Api'), '--no-launch-profile')
# A running dev server can hold a lock on the normal bin folder; allow redirecting the build.
if ($BaseOutputPath) { $runArgs += "-p:BaseOutputPath=$BaseOutputPath" }

& dotnet @runArgs
