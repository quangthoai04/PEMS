$disallowedFolders = @("Application", "Domain", "Infrastructure", "Pems_WebAPI", "Scaffold", "Scaffolder")
$root = (Get-Item $PSScriptRoot).Parent.FullName

$failed = $false

foreach ($folder in $disallowedFolders) {
    if (Test-Path "$root\$folder") {
        Write-Error "Project structure violation: Root directory '$folder' is not allowed. The correct structure requires these to be inside 'backend/PEMS.*'. Please delete '$folder' before proceeding."
        $failed = $true
    }
}

if ($failed) {
    exit 1
}

Write-Output "Project structure is valid."
exit 0
