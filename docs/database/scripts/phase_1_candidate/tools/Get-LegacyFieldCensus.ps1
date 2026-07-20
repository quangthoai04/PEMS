<#
    Reproducible census of the ten Phase I legacy fields across the repository.

    Emits one CSV row per RAW HIT (file + line + field + matched token), so the appendix can be
    grouped later without ever losing the raw count. Grouping that cannot be reconciled back to a
    raw total is how "1172 hits" turns into an unverifiable number.

    Run:
      powershell -ExecutionPolicy Bypass -File .\Get-LegacyFieldCensus.ps1 -OutCsv .\census.csv

    Read-only: never opens a database, never writes outside -OutCsv.
    ASCII-only (Windows PowerShell 5.1 reads .ps1 as ANSI without a BOM).
#>
param(
    [string]$RepoRoot,
    [Parameter(Mandatory = $true)][string]$OutCsv
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# $PSScriptRoot is not bound yet inside a param() default, so resolve here. Walk up to the
# directory that holds both backend/ and tests/ rather than counting '..' segments, which silently
# breaks whenever the script moves.
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $dir = $PSScriptRoot
    while ($dir -and -not ((Test-Path (Join-Path $dir 'backend')) -and (Test-Path (Join-Path $dir 'tests')))) {
        $dir = Split-Path -Parent $dir
    }
    if (-not $dir) { throw "Could not locate the repository root from $PSScriptRoot." }
    $RepoRoot = $dir
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

# PascalCase (C#/TS) paired with snake_case (SQL). Both spellings of the same field are counted
# under one field name so per-field totals mean something.
$fields = [ordered]@{
    'delegation_name'      = @('DelegationName', 'delegation_name')
    'visit_type'           = @('VisitType', 'visit_type')
    'visit_type_other'     = @('VisitTypeOther', 'visit_type_other')
    'purpose'              = @('Purpose', 'purpose')
    'working_content'      = @('WorkingContent', 'working_content')
    'working_language'     = @('WorkingLanguage', 'working_language')
    'transportation_note'  = @('TransportationNote', 'transportation_note')
    'media_consent_status' = @('MediaConsentStatus', 'media_consent_status')
    'media_consent_note'   = @('MediaConsentNote', 'media_consent_note')
    'note_to_fptu'         = @('NoteToFptu', 'note_to_fptu')
}

# Build/vendor output is not source and would make the total unstable between machines.
$excludeDirs = @('\node_modules\', '\bin\', '\obj\', '\dist\', '\.git\', '\.vs\', '\coverage\', '\playwright-report\', '\test-results\')

$includeExt = @('.cs', '.ts', '.tsx', '.js', '.jsx', '.sql', '.json', '.md', '.ps1', '.mjs', '.yml', '.yaml', '.http')

# Paths arrive repo-relative (no leading separator), so the patterns must be anchored at the
# start rather than requiring a leading '/'.
function Get-Area([string]$path) {
    $p = $path.Replace('\', '/')
    if ($p -match '^tests/')                        { return 'test' }
    if ($p -match '^backend/PEMS\.Domain/')         { return 'backend-domain' }
    if ($p -match '^backend/PEMS\.Application/')    { return 'backend-application' }
    if ($p -match '^backend/PEMS\.Infrastructure/') { return 'backend-infrastructure' }
    if ($p -match '^backend/PEMS\.Api/')            { return 'backend-api' }
    if ($p -match '^backend/')                      { return 'backend-other' }
    if ($p -match '^frontend/.*/(tests|__tests__|tests-realstack)/') { return 'frontend-test' }
    if ($p -match '^frontend/')                     { return 'frontend' }
    if ($p -match '^docs/database/')                { return 'sql-script' }
    if ($p -match '^docs/')                         { return 'docs' }
    return 'other'
}

Write-Host ("Scanning {0}" -f $RepoRoot)

$files = Get-ChildItem -Path $RepoRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $includeExt -contains $_.Extension.ToLowerInvariant() }

# Path exclusion is a separate pass: a nested pipeline inside the extension filter is both slower
# and harder to read at this file count.
$files = $files | Where-Object {
    $full = $_.FullName
    $skip = $false
    foreach ($d in $excludeDirs) { if ($full -like "*$d*") { $skip = $true; break } }
    -not $skip
}

Write-Host ("Candidate files: {0}" -f @($files).Count)

$rows = New-Object System.Collections.ArrayList

foreach ($file in $files) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    $rel = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')

    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line.Length -eq 0) { continue }

        foreach ($fieldName in $fields.Keys) {
            foreach ($token in $fields[$fieldName]) {
                # Word-ish boundary so DelegationNameX or my_purpose_note are not counted as hits.
                $pattern = '(?<![A-Za-z0-9_])' + [regex]::Escape($token) + '(?![A-Za-z0-9_])'
                # Not $matches: that is a PowerShell automatic variable and writing to it here
                # would clobber state the -match operator relies on elsewhere.
                $lineHits = [regex]::Matches($line, $pattern)
                if ($lineHits.Count -eq 0) { continue }

                [void]$rows.Add([pscustomobject]@{
                    Field      = $fieldName
                    Token      = $token
                    File       = $rel
                    Line       = $i + 1
                    HitsOnLine = $lineHits.Count
                    Area       = Get-Area $rel
                    Text       = ($line.Trim() -replace '\s+', ' ')
                })
            }
        }
    }
}

$rows = @($rows.ToArray())
$rawHits = ($rows | Measure-Object -Property HitsOnLine -Sum).Sum
$distinctFiles = @($rows | Select-Object -ExpandProperty File -Unique).Count

$rows | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8

Write-Host ''
Write-Host '==== CENSUS SUMMARY ===='
Write-Host ("Raw hits       : {0}" -f $rawHits)
Write-Host ("Matched lines  : {0}" -f $rows.Count)
Write-Host ("Distinct files : {0}" -f $distinctFiles)
Write-Host ("CSV            : {0}" -f (Resolve-Path -LiteralPath $OutCsv).Path)
Write-Host ''
Write-Host 'By area:'
$rows | Group-Object Area | Sort-Object Count -Descending | ForEach-Object {
    $sum = ($_.Group | Measure-Object -Property HitsOnLine -Sum).Sum
    Write-Host ("  {0,-24} {1,6} hits  {2,5} lines  {3,4} files" -f $_.Name, $sum, $_.Count, @($_.Group | Select-Object -ExpandProperty File -Unique).Count)
}
Write-Host ''
Write-Host 'By field:'
$rows | Group-Object Field | Sort-Object Name | ForEach-Object {
    $sum = ($_.Group | Measure-Object -Property HitsOnLine -Sum).Sum
    Write-Host ("  {0,-24} {1,6} hits" -f $_.Name, $sum)
}
