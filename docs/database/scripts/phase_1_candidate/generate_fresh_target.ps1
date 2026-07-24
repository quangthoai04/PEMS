# Resolve the ONE canonical schema script; a hard-coded filename silently rots when the script is renamed.
$scriptsDir = "docs\database\scripts"
$candidates = @(Get-ChildItem -LiteralPath $scriptsDir -Filter 'PEMS_FULL_*.sql' -File)
if ($candidates.Count -ne 1) {
    throw "Expected exactly one canonical PEMS_FULL_*.sql in $scriptsDir, found $($candidates.Count)."
}
$sourcePath = $candidates[0].FullName
$targetPath = "docs\database\scripts\phase_1_candidate\00_fresh_target.sql"

$content = Get-Content $sourcePath -Raw

# Remove the 10 legacy columns from visit_requests
$content = $content -replace "(?m)^\s*delegation_name VARCHAR\(200\)[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*visit_scope ENUM[^,]*,?\r?\n?", "  visit_scope ENUM('SINGLE_CAMPUS','MULTI_CAMPUS') NOT NULL DEFAULT 'SINGLE_CAMPUS' COMMENT 'SINGLE_CAMPUS/MULTI_CAMPUS chỉ mô tả số campus được chọn; cả hai đều route từng campus instance tới Staff Leader của campus tương ứng.',`n"
$content = $content -replace "(?m)^\s*visit_type ENUM[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*visit_type_other VARCHAR\(255\)[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*purpose TEXT[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*working_content TEXT[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*working_language ENUM[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*transportation_note TEXT[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*media_consent_status ENUM[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*media_consent_note TEXT[^,]*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*note_to_fptu TEXT[^,]*,?\r?\n?", ""

# Remove dependent indexes
$content = $content -replace "(?m)^\s*KEY idx_visit_requests_visit_type.*,?\r?\n?", ""
$content = $content -replace "(?m)^\s*KEY idx_visit_requests_media_consent.*,?\r?\n?", ""

# Update FULLTEXT key ft_visit_requests_frontend_search (remove delegation_name)
$oldFullText = "FULLTEXT KEY ft_visit_requests_frontend_search (request_code, delegation_name, registrant_full_name, registrant_organization, registrant_email, contact_person_full_name, contact_person_organization, contact_person_email)"
$newFullText = "FULLTEXT KEY ft_visit_requests_frontend_search (request_code, registrant_full_name, registrant_organization, registrant_email, contact_person_full_name, contact_person_organization, contact_person_email)"
$content = $content.Replace($oldFullText, $newFullText)

# Remove the CHECK constraint for visit_type
$content = $content -replace "(?m)^\s*CHECK \(visit_type <> 'OTHER'.*,?\r?\n?", ""

Set-Content -Path $targetPath -Value $content
Write-Host "00_fresh_target.sql created successfully."
