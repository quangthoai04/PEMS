<#
    Statement-aware safety scanner for SQL that is about to be fed to a mysql client.

    WHY THIS EXISTS
    ---------------
    On 2026-07-20 a master dump was piped into mysql with an intended target of a disposable
    database. The dump contained top-level `CREATE DATABASE IF NOT EXISTS pems_db;` followed by
    `USE pems_db;`. `USE` re-pointed the session, so every following DROP/CREATE/INSERT landed in
    the protected `pems_db` while the intended target stayed empty.

    The lesson is NOT "read the header first". It is that selecting the database on the mysql
    command line is only a DEFAULT: the payload can move the session anywhere it likes. Any guard
    that lives outside the payload (a connection-string allowlist, a -DbName ValidateSet) is
    therefore not a guard at all. The payload itself has to be proven safe BEFORE a mysql process
    is spawned.

    NOTE: this file is deliberately ASCII-only. Windows PowerShell 5.1 reads .ps1 files as ANSI
    unless they carry a UTF-8 BOM, so a UTF-8 dash decodes to a byte PS treats as a smart quote
    and the file stops parsing.

    DESIGN
    ------
    * A real tokenizer, not a regex sweep. Regex cannot tell `USE pems_db` from the same bytes
      inside a string literal, a comment, or a routine body, and both false directions are
      dangerous: a missed statement destroys data, a false positive on a comment blocks safe work
      and pushes people back to raw imports.
    * Fail CLOSED on anything the scanner cannot classify. A statement that is not understood is
      reported, not waved through because it failed to match a denylist.
    * Reports findings; never edits, rewrites, or "sanitises" the input. Silently stripping a
      `USE` would produce a file nobody reviewed that still runs with full privileges.
#>

Set-StrictMode -Version 2.0

$script:ProtectedSchemas = @('pems_db', 'pems_test', 'pems_pr3_test')

# Statement prefixes that move or destroy a whole schema. Matched on the normalized statement.
$script:DatabaseControlPatterns = @(
    @{ Name = 'USE';             Pattern = '^USE(\s|`|$)' },
    @{ Name = 'CREATE DATABASE'; Pattern = '^CREATE\s+(DATABASE|SCHEMA)(\s|`|$)' },
    @{ Name = 'ALTER DATABASE';  Pattern = '^ALTER\s+(DATABASE|SCHEMA)(\s|`|$)' },
    @{ Name = 'DROP DATABASE';   Pattern = '^DROP\s+(DATABASE|SCHEMA)(\s|`|$)' }
)

# Server/global/admin statements. A disposable fixture import never legitimately needs these, and
# several of them (RESET MASTER, PURGE BINARY LOGS) would destroy the very binlogs that make an
# incident recoverable.
$script:AdminPatterns = @(
    @{ Name = 'CREATE USER';        Pattern = '^CREATE\s+USER(\s|$)' },
    @{ Name = 'ALTER USER';         Pattern = '^ALTER\s+USER(\s|$)' },
    @{ Name = 'DROP USER';          Pattern = '^DROP\s+USER(\s|$)' },
    @{ Name = 'RENAME USER';        Pattern = '^RENAME\s+USER(\s|$)' },
    @{ Name = 'GRANT';              Pattern = '^GRANT(\s|$)' },
    @{ Name = 'REVOKE';             Pattern = '^REVOKE(\s|$)' },
    @{ Name = 'SET GLOBAL';         Pattern = '^SET\s+GLOBAL(\s|$)' },
    @{ Name = 'SET PERSIST';        Pattern = '^SET\s+PERSIST(_ONLY)?(\s|$)' },
    @{ Name = 'RESET MASTER';       Pattern = '^RESET\s+(MASTER|REPLICA|SLAVE)(\s|$)' },
    @{ Name = 'PURGE BINARY LOGS';  Pattern = '^PURGE\s+(BINARY|MASTER)\s+LOGS(\s|$)' },
    @{ Name = 'SHUTDOWN';           Pattern = '^SHUTDOWN(\s|$)' },
    @{ Name = 'INSTALL PLUGIN';     Pattern = '^(INSTALL|UNINSTALL)\s+(PLUGIN|COMPONENT)(\s|$)' },
    @{ Name = 'CHANGE REPLICATION'; Pattern = '^CHANGE\s+(MASTER|REPLICATION)(\s|$)' },
    @{ Name = 'START REPLICA';      Pattern = '^(START|STOP)\s+(SLAVE|REPLICA|GROUP_REPLICATION)(\s|$)' },
    @{ Name = 'CREATE ROLE';        Pattern = '^(CREATE|DROP)\s+ROLE(\s|$)' },
    @{ Name = 'FLUSH PRIVILEGES';   Pattern = '^FLUSH\s+.*PRIVILEGES(\s|$)' }
)

# mysql CLIENT meta-commands. These are interpreted by the client, not the server, so they never
# appear in a server parse and are easy to overlook. SOURCE pulls in an arbitrary second file,
# which would bypass every check performed on the file we actually scanned.
$script:ClientCommandPatterns = @(
    @{ Name = 'SOURCE';        Pattern = '^SOURCE(\s|$)' },
    @{ Name = 'BACKSLASH-DOT'; Pattern = '^\\\.' },
    @{ Name = 'SYSTEM';        Pattern = '^(SYSTEM|\\!)(\s|$)' },
    @{ Name = 'CONNECT';       Pattern = '^(CONNECT|\\r)(\s|$)' }
)

<#
    Splits SQL into top-level statements while tracking the lexical state that makes naive
    scanning wrong.

    Returns objects with:
      Index      - 1-based statement number
      Line       - 1-based line where the statement starts
      Raw        - the statement as written
      Normalized - comments removed, whitespace collapsed, upper-cased OUTSIDE string/identifier
                   literals, so keyword matching is safe but literal contents are never folded
      Literals   - the string/backtick literal contents, kept separately so a scanner can tell
                   "the text pems_db appeared in a comment" from "the statement targets pems_db"

    Content inside MySQL versioned comments (the executable form) is emitted as normal SQL,
    because the server DOES run it.
#>
function Split-SqlStatements {
    param([Parameter(Mandatory = $true)][string]$Sql)

    $statements = New-Object System.Collections.ArrayList
    $delimiter = ';'

    $rawBuf  = New-Object System.Text.StringBuilder
    $normBuf = New-Object System.Text.StringBuilder
    $literals = New-Object System.Collections.ArrayList

    $i = 0
    $len = $Sql.Length
    $line = 1
    $startLine = 1
    $index = 0

    # Tracks whether the normalized buffer currently ends with a space, to collapse runs.
    function Add-Norm([System.Text.StringBuilder]$buf, [string]$text) {
        if ($text -eq ' ') {
            if ($buf.Length -eq 0) { return }
            if ($buf[$buf.Length - 1] -eq ' ') { return }
        }
        [void]$buf.Append($text)
    }

    while ($i -lt $len) {
        $ch = $Sql[$i]
        $next = if ($i + 1 -lt $len) { $Sql[$i + 1] } else { [char]0 }

        # ---- line comments -------------------------------------------------
        # "--" only starts a comment when followed by whitespace/EOL (MySQL rule); "--x" is an
        # operator sequence. Getting this wrong would silently swallow real statements.
        $isDashComment = ($ch -eq '-' -and $next -eq '-' -and
            ($i + 2 -ge $len -or $Sql[$i + 2] -eq ' ' -or $Sql[$i + 2] -eq "`t" -or $Sql[$i + 2] -eq "`r" -or $Sql[$i + 2] -eq "`n"))
        if ($isDashComment -or $ch -eq '#') {
            while ($i -lt $len -and $Sql[$i] -ne "`n") { [void]$rawBuf.Append($Sql[$i]); $i++ }
            Add-Norm $normBuf ' '
            continue
        }

        # ---- block comments ------------------------------------------------
        if ($ch -eq '/' -and $next -eq '*') {
            $isVersioned = ($i + 2 -lt $len -and $Sql[$i + 2] -eq '!')
            $close = $Sql.IndexOf('*/', $i + 2)
            if ($close -lt 0) {
                # Unterminated comment: everything after it is un-analysable. Fail closed by
                # surfacing it as a statement the caller cannot classify.
                $body = $Sql.Substring($i)
                [void]$rawBuf.Append($body)
                $line += @($body.ToCharArray() | Where-Object { $_ -eq "`n" }).Count
                $i = $len
                Add-Norm $normBuf ' '
                continue
            }

            $body = $Sql.Substring($i, $close + 2 - $i)
            [void]$rawBuf.Append($body)
            $line += @($body.ToCharArray() | Where-Object { $_ -eq "`n" }).Count

            if ($isVersioned) {
                # /*!40101 ... */ IS executed by MySQL. Re-scan its payload as live SQL: skip the
                # "/*!" plus the version digits, drop the trailing "*/".
                $inner = $body.Substring(3)
                $inner = $inner -replace '^\d+', ''
                $inner = $inner.Substring(0, $inner.Length - 2)
                foreach ($sub in (Split-SqlStatements -Sql ($inner + ';'))) {
                    [void]$statements.Add([pscustomobject]@{
                        Index = 0; Line = $startLine; Raw = $sub.Raw
                        Normalized = $sub.Normalized; Literals = $sub.Literals
                    })
                }
            }

            Add-Norm $normBuf ' '
            $i = $close + 2
            continue
        }

        # ---- quoted strings and quoted identifiers -------------------------
        if ($ch -eq "'" -or $ch -eq '"' -or $ch -eq '`') {
            $quote = $ch
            [void]$rawBuf.Append($ch)
            $i++
            $lit = New-Object System.Text.StringBuilder
            while ($i -lt $len) {
                $c = $Sql[$i]
                if ($c -eq '\' -and $quote -ne '`' -and $i + 1 -lt $len) {
                    # Backslash escapes apply to strings but not to backtick identifiers.
                    [void]$rawBuf.Append($c); [void]$rawBuf.Append($Sql[$i + 1])
                    [void]$lit.Append($Sql[$i + 1])
                    $i += 2
                    continue
                }
                if ($c -eq $quote) {
                    if ($i + 1 -lt $len -and $Sql[$i + 1] -eq $quote) {
                        # Doubled quote is an escaped quote, not the end of the literal.
                        [void]$rawBuf.Append($c); [void]$rawBuf.Append($c)
                        [void]$lit.Append($c)
                        $i += 2
                        continue
                    }
                    [void]$rawBuf.Append($c)
                    $i++
                    break
                }
                if ($c -eq "`n") { $line++ }
                [void]$rawBuf.Append($c); [void]$lit.Append($c)
                $i++
            }

            [void]$literals.Add($lit.ToString())
            if ($quote -eq '`') {
                # A backtick identifier is part of the statement's target, so it MUST stay in the
                # normalized text - that is how `pems_db`.`t` is detected.
                Add-Norm $normBuf ('`' + $lit.ToString().ToUpperInvariant() + '`')
            } else {
                # String contents are replaced by a placeholder: their bytes are data, and folding
                # them into the normalized text is exactly how a comment or a message body would
                # be misread as a statement.
                Add-Norm $normBuf "'#'"
            }
            continue
        }

        # ---- DELIMITER (a client command that changes statement termination)
        if (($ch -eq 'd' -or $ch -eq 'D') -and $normBuf.Length -eq 0) {
            $rest = $Sql.Substring($i)
            $m = [regex]::Match($rest, '^DELIMITER[ \t]+(\S+)[ \t]*(\r?\n|$)', 'IgnoreCase')
            if ($m.Success) {
                $delimiter = $m.Groups[1].Value
                $i += $m.Length
                $line++
                $rawBuf.Clear() | Out-Null
                $normBuf.Clear() | Out-Null
                $literals.Clear()
                $startLine = $line
                continue
            }
        }

        # ---- statement terminator -----------------------------------------
        if ($delimiter.Length -gt 0 -and $i + $delimiter.Length -le $len -and
            $Sql.Substring($i, $delimiter.Length) -eq $delimiter) {

            $normalized = $normBuf.ToString().Trim()
            if ($normalized.Length -gt 0) {
                $index++
                [void]$statements.Add([pscustomobject]@{
                    Index = $index; Line = $startLine; Raw = $rawBuf.ToString().Trim()
                    Normalized = $normalized; Literals = @($literals.ToArray())
                })
            }
            $i += $delimiter.Length
            $rawBuf.Clear() | Out-Null
            $normBuf.Clear() | Out-Null
            $literals.Clear()
            $startLine = $line
            continue
        }

        if ($ch -eq "`n") { $line++; [void]$rawBuf.Append($ch); Add-Norm $normBuf ' '; $i++; continue }
        if ($ch -eq "`r" -or $ch -eq "`t" -or $ch -eq ' ') { [void]$rawBuf.Append($ch); Add-Norm $normBuf ' '; $i++; continue }

        [void]$rawBuf.Append($ch)
        Add-Norm $normBuf ([string]$ch).ToUpperInvariant()
        $i++
    }

    # Trailing statement without a terminator still executes when the client hits EOF.
    $normalized = $normBuf.ToString().Trim()
    if ($normalized.Length -gt 0) {
        $index++
        [void]$statements.Add([pscustomobject]@{
            Index = $index; Line = $startLine; Raw = $rawBuf.ToString().Trim()
            Normalized = $normalized; Literals = @($literals.ToArray())
        })
    }

    return @($statements.ToArray())
}

<#
    Scans SQL text and returns a verdict object:
      IsSafe   - $true only when NOTHING was found
      Findings - one entry per problem (Statement index, Line, Kind, Detail, Excerpt)

    Never throws on hostile input and never touches the filesystem or a database.
#>
function Test-SqlPayloadSafety {
    param(
        [Parameter(Mandatory = $true)][string]$Sql,
        [string[]]$ProtectedSchemas = $script:ProtectedSchemas
    )

    $findings = New-Object System.Collections.ArrayList

    function Add-Finding($stmt, $kind, $detail) {
        $excerpt = $stmt.Raw -replace '\s+', ' '
        if ($excerpt.Length -gt 120) { $excerpt = $excerpt.Substring(0, 120) + '...' }
        [void]$findings.Add([pscustomobject]@{
            Statement = $stmt.Index; Line = $stmt.Line; Kind = $kind
            Detail = $detail; Excerpt = $excerpt
        })
    }

    # @() matters: PowerShell unrolls a single-element array on return, and StrictMode then makes
    # .Count on the resulting scalar a hard error.
    $statements = @(Split-SqlStatements -Sql $Sql)

    foreach ($stmt in $statements) {
        $norm = $stmt.Normalized

        foreach ($rule in $script:DatabaseControlPatterns) {
            if ($norm -match $rule.Pattern) {
                Add-Finding $stmt 'DATABASE_CONTROL' ("$($rule.Name) changes or destroys a schema; the session target is no longer the one the caller selected")
            }
        }

        foreach ($rule in $script:AdminPatterns) {
            if ($norm -match $rule.Pattern) {
                Add-Finding $stmt 'ADMIN_STATEMENT' ("$($rule.Name) is a server/global operation and is never part of a disposable fixture import")
            }
        }

        foreach ($rule in $script:ClientCommandPatterns) {
            if ($norm -match $rule.Pattern) {
                Add-Finding $stmt 'CLIENT_COMMAND' ("$($rule.Name) is interpreted by the mysql client and can pull in unscanned input")
            }
        }

        # Fully-qualified references to a protected schema. Checked against the NORMALIZED text so
        # the same characters inside a string literal or a comment cannot trigger it, while
        # `pems_db`.`t` (backticks kept in the normalized form) still does.
        foreach ($schema in $ProtectedSchemas) {
            $up = $schema.ToUpperInvariant()
            $pattern = '(^|[^A-Z0-9_$])(`' + $up + '`|' + [regex]::Escape($up) + ')\s*\.'
            if ($norm -match $pattern) {
                Add-Finding $stmt 'PROTECTED_REFERENCE' "statement references the protected schema '$schema' by name"
            }
        }

        # Dynamic SQL: PREPARE from a value we cannot evaluate. Fail closed rather than guess.
        if ($norm -match '^PREPARE(\s|$)' -or $norm -match '(^|\s)EXECUTE\s+IMMEDIATE(\s|$)') {
            $mentions = $false
            foreach ($schema in $ProtectedSchemas) {
                foreach ($litText in $stmt.Literals) {
                    if ($litText -match [regex]::Escape($schema)) { $mentions = $true }
                }
            }
            if ($mentions) {
                Add-Finding $stmt 'DYNAMIC_SQL' 'dynamic SQL builds a statement mentioning a protected schema'
            } else {
                Add-Finding $stmt 'DYNAMIC_SQL' 'dynamic SQL cannot be statically proven safe; refusing rather than guessing'
            }
        }
    }

    return [pscustomobject]@{
        IsSafe         = ($findings.Count -eq 0)
        Findings       = @($findings.ToArray())
        StatementCount = $statements.Count
    }
}

<#
    File-level wrapper. Returns the verdict PLUS the SHA-256 and the exact bytes that were scanned.

    The caller MUST import the returned Content, not re-read the path: re-reading opens a
    time-of-check/time-of-use window where the file changes between the scan and the import.
#>
function Test-SqlFileSafety {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "SQL file not found: $Path"
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $sha = [System.BitConverter]::ToString(
        [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()

    # Decode as UTF-8 and drop a BOM if present, so a BOM cannot hide the first statement.
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }

    $verdict = Test-SqlPayloadSafety -Sql $text

    return [pscustomobject]@{
        Path           = (Resolve-Path -LiteralPath $Path).Path
        Sha256         = $sha
        SizeBytes      = $bytes.Length
        Content        = $text
        IsSafe         = $verdict.IsSafe
        Findings       = $verdict.Findings
        StatementCount = $verdict.StatementCount
    }
}

function Write-SafetyFindings {
    param([Parameter(Mandatory = $true)]$Verdict, [string]$Label = 'payload')

    if ($Verdict.IsSafe) {
        Write-Host ("SAFE: {0} - {1} statement(s), no database-control, admin, client or protected reference found." -f $Label, $Verdict.StatementCount)
        return
    }

    Write-Host ("UNSAFE: {0} - {1} finding(s). mysql was NOT invoked." -f $Label, $Verdict.Findings.Count)
    foreach ($f in $Verdict.Findings) {
        Write-Host ("  [{0}] statement #{1} line {2}: {3}" -f $f.Kind, $f.Statement, $f.Line, $f.Detail)
        Write-Host ("      {0}" -f $f.Excerpt)
    }
}
