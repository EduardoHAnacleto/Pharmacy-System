# Diagnoses the .env at the repository root on Windows.
#
#   .\scripts\env-doctor.ps1
#   .\scripts\env-doctor.ps1 -Path C:\path\to\.env
#
# Reads nothing but the file and docker-compose.yml, changes nothing, and never
# prints a secret - only whether one is set and how long it is, so the output
# can be pasted into a ticket.
#
# It exists because the two ways this file fails most often on Windows both
# produce a file that looks correct in Explorer:
#
#   - saved as .env.txt, because Explorer hides known extensions and Notepad
#     appends one to a name it does not recognise;
#   - saved as UTF-16, because `>` redirection in Windows PowerShell 5.1 writes
#     that encoding, and compose reads the result as binary noise.
#
# In both cases compose finds no values at all and stops on the first `:?`
# guard, naming a variable that is sitting right there in the file.
#
# This file is deliberately pure ASCII; see the note in test-up.ps1.

[CmdletBinding()]
param(
    # The file to inspect. Defaults to the .env beside docker-compose.yml.
    [string]$Path,
    # Skip the final `docker compose config` cross-check.
    [switch]$SkipCompose
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot

$isDefaultPath = [string]::IsNullOrWhiteSpace($Path)
if ($isDefaultPath) { $Path = Join-Path $repoRoot '.env' }

$script:Problems = 0
$script:Warnings = 0

function Write-Problem { param([string]$Text) $script:Problems++; Write-Host "  [problem] $Text" -ForegroundColor Red }
function Write-Warn    { param([string]$Text) $script:Warnings++; Write-Host "  [warning] $Text" -ForegroundColor Yellow }
function Write-Good    { param([string]$Text) Write-Host "  [ok]      $Text" -ForegroundColor Green }
function Write-Info    { param([string]$Text) Write-Host "            $Text" }
function Write-Section { param([string]$Text) Write-Host ''; Write-Host $Text -ForegroundColor Cyan }

# Names are shown inside brackets so a trailing space - which Explorer renders
# as nothing at all - is visible.
function Format-Name { param([string]$Name) return "[$Name]" }

# =========================================================================
# 1. Is the file even there, and is it the file compose will look for?
# =========================================================================

Write-Section '1. The file'

$candidates = @(Get-ChildItem -LiteralPath $repoRoot -Force -Filter '.env*' -File -ErrorAction SilentlyContinue)
if ($candidates.Count -gt 0) {
    Write-Info 'Files matching .env* in the repository root:'
    foreach ($c in $candidates) {
        Write-Info ("  {0,-24} {1,8} bytes" -f (Format-Name $c.Name), $c.Length)
    }
}

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    Write-Problem "There is no file at $Path"

    # The look-alikes, in the order they actually happen.
    $decoys = @($candidates | Where-Object { $_.Name -ne '.env' -and $_.Name -ne '.env.example' })
    foreach ($d in $decoys) {
        if ($d.Name -match '^\.env\.(txt|TXT|Txt)$') {
            Write-Info ''
            Write-Info "$(Format-Name $d.Name) is almost certainly the file you meant to create."
            Write-Info 'Explorer hides known extensions, so Notepad saved .env as .env.txt and'
            Write-Info 'showed it to you as ".env". Rename it:'
            Write-Info "    Rename-Item '$($d.FullName)' '.env'"
        }
        elseif ($d.Name -eq '.env.example') { }
        else {
            Write-Info ''
            Write-Info "$(Format-Name $d.Name) is not a name compose reads. It loads exactly [.env]."
        }
    }
    if ($decoys.Count -eq 0) {
        Write-Info ''
        Write-Info 'Start from the example, then fill in the blanks - copying it is not enough,'
        Write-Info 'the passwords ship empty on purpose:'
        Write-Info '    Copy-Item .env.example .env'
        Write-Info 'The required variables are listed in docs/OPERACOES.md section 1.'
    }
    Write-Host ''
    Write-Host "Found $script:Problems problem(s)." -ForegroundColor Red
    exit 1
}

$item = Get-Item -LiteralPath $Path -Force
Write-Good "$(Format-Name $item.Name) exists, $($item.Length) bytes"
if ($isDefaultPath -and $item.Name -ne '.env') {
    Write-Warn "compose reads [.env]; this file is $(Format-Name $item.Name)"
}

# =========================================================================
# 2. Encoding. This is the failure that hides best.
# =========================================================================

Write-Section '2. Encoding'

$bytes = [System.IO.File]::ReadAllBytes($Path)
if ($bytes.Length -eq 0) {
    Write-Problem 'The file is empty.'
    Write-Host ''
    Write-Host "Found $script:Problems problem(s)." -ForegroundColor Red
    exit 1
}

function Test-Bom {
    param([byte[]]$Data, [byte[]]$Bom)
    if ($Data.Length -lt $Bom.Length) { return $false }
    for ($i = 0; $i -lt $Bom.Length; $i++) { if ($Data[$i] -ne $Bom[$i]) { return $false } }
    return $true
}

$isUtf32Le = Test-Bom $bytes @(0xFF, 0xFE, 0x00, 0x00)
$isUtf16Le = (-not $isUtf32Le) -and (Test-Bom $bytes @(0xFF, 0xFE))
$isUtf16Be = Test-Bom $bytes @(0xFE, 0xFF)
$isUtf8Bom = Test-Bom $bytes @(0xEF, 0xBB, 0xBF)
$hasNul = $false
foreach ($b in $bytes) { if ($b -eq 0) { $hasNul = $true; break } }

if ($isUtf16Le -or $isUtf16Be -or $isUtf32Le -or $hasNul) {
    $what = if ($isUtf32Le) { 'UTF-32' } elseif ($isUtf16Be) { 'UTF-16 big-endian' }
            elseif ($isUtf16Le) { 'UTF-16 little-endian' } else { 'UTF-16 (no byte-order mark)' }
    # Get-Content only sniffs an encoding when there is a byte-order mark to
    # sniff; on a bare UTF-16 file it falls back to the ANSI codepage and hands
    # back every character separated by a NUL. The re-encode has to be told.
    $psEncoding = if ($isUtf32Le) { 'UTF32' } elseif ($isUtf16Be) { 'BigEndianUnicode' } else { 'Unicode' }
    Write-Problem "The file is $what. Compose cannot read it."
    Write-Info ''
    Write-Info 'Every variable comes out unset, so compose stops on the first `:?` guard'
    Write-Info 'and names a variable that is plainly there in the file. Its own message'
    Write-Info 'mentions an unexpected "\x00" character rather than the encoding.'
    Write-Info ''
    Write-Info 'This is what `>` redirection produces in Windows PowerShell 5.1.'
    Write-Info 'Rewrite it as UTF-8, keeping a copy first:'
    Write-Info "    Copy-Item '$Path' '$Path.bak'"
    Write-Info "    `$text = Get-Content -LiteralPath '$Path' -Raw -Encoding $psEncoding"
    Write-Info "    [System.IO.File]::WriteAllText('$Path', `$text, (New-Object System.Text.UTF8Encoding(`$false)))"
    Write-Info ''
    Write-Info 'Then run this script again. Nothing below could be checked.'
    Write-Host ''
    Write-Host "Found $script:Problems problem(s)." -ForegroundColor Red
    exit 1
}

if ($isUtf8Bom) {
    Write-Good 'UTF-8 with a byte-order mark. Compose handles this.'
}
else {
    Write-Good 'UTF-8 (or plain ASCII), no byte-order mark.'
}

$text = [System.Text.Encoding]::UTF8.GetString($bytes)
if ($isUtf8Bom) { $text = $text.TrimStart([char]0xFEFF) }
$crlf = ([regex]::Matches($text, "`r`n")).Count
if ($crlf -gt 0) { Write-Good "Windows line endings ($crlf). Compose handles these." }

# =========================================================================
# 3. Parse
# =========================================================================

Write-Section '3. Contents'

$lines = $text -split "`r`n|`n|`r"
$entries = @()
$lineNo = 0
foreach ($raw in $lines) {
    $lineNo++
    $t = $raw.Trim()
    if ($t.Length -eq 0 -or $t.StartsWith('#')) { continue }
    # compose accepts a leading `export`, the way a shell script would write it.
    if ($t -match '^export\s+(.*)$') { $t = $Matches[1].Trim() }
    $eq = $t.IndexOf('=')
    if ($eq -lt 1) {
        Write-Problem "Line ${lineNo}: not a KEY=VALUE assignment and not a comment."
        continue
    }
    $key = $t.Substring(0, $eq).Trim()
    $value = $t.Substring($eq + 1).Trim()
    $quote = 'none'
    if ($value.Length -ge 2 -and $value.StartsWith("'") -and $value.EndsWith("'")) {
        $quote = 'single'; $value = $value.Substring(1, $value.Length - 2)
    }
    elseif ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
        $quote = 'double'; $value = $value.Substring(1, $value.Length - 2)
    }
    if ($key -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
        Write-Problem "Line ${lineNo}: $(Format-Name $key) is not a usable variable name."
        continue
    }
    # Whitespace followed by `#` starts a comment in an unquoted value; a `#`
    # glued to the value does not. Cut it here, so every length reported below
    # is the length compose will actually pass to the container rather than the
    # length that is sitting in the file.
    $hasComment = $false
    if ($quote -eq 'none' -and $value -match '\s#') {
        $hasComment = $true
        $value = ($value -replace '\s#.*$', '').TrimEnd()
    }
    # `$` starts an interpolation unless doubled or wrapped in single quotes.
    # Double quotes do NOT protect it. What the value becomes depends on the
    # environment, so nothing below reports a length for one of these.
    $hasDollar = ($quote -ne 'single') -and (($value -replace '\$\$', '').Contains('$'))
    $entries += [pscustomobject]@{
        Line = $lineNo; Key = $key; Value = $value; Quote = $quote
        HasComment = $hasComment; HasDollar = $hasDollar
    }
}

Write-Info "$($entries.Count) assignment(s) read."

# A repeated key is not an error to compose - the last one silently wins - but
# it is almost always an edit that did not land where its author thought.
foreach ($group in ($entries | Group-Object Key | Where-Object { $_.Count -gt 1 })) {
    $where = ($group.Group | ForEach-Object { $_.Line }) -join ', '
    Write-Warn "$($group.Name) is set more than once (lines $where). The last one wins."
}

$byKey = @{}
foreach ($e in $entries) { $byKey[$e.Key] = $e }

# =========================================================================
# 4. Values that compose will quietly rewrite
# =========================================================================

Write-Section '4. Characters compose treats as syntax'

$syntaxIssues = 0
foreach ($e in $entries) {
    if ($e.HasDollar) {
        $syntaxIssues++
        Write-Problem "Line $($e.Line): $($e.Key) contains a single `$, which compose expands."
        Write-Info "          Everything from the `$ onwards is replaced, usually by nothing."
        Write-Info "          Write it as `$`$, or wrap the whole value in single quotes -"
        Write-Info '          double quotes do not protect it.'
    }
    if ($e.HasComment) {
        $syntaxIssues++
        Write-Problem "Line $($e.Line): $($e.Key) contains a space followed by #, which starts a comment."
        Write-Info '          Compose keeps only what is before it. Remove the space, or quote'
        Write-Info "          the value. It reaches the stack as $($e.Value.Length) character(s), not"
        Write-Info '          what the line looks like.'
    }
}
if ($syntaxIssues -eq 0) { Write-Good 'No value contains a character compose would reinterpret.' }

# =========================================================================
# 5. The variables the stack refuses to start without
# =========================================================================

Write-Section '5. Required variables'

# Read the guard list out of docker-compose.yml rather than repeating it here,
# so this check cannot drift from the file that enforces it.
$required = @()
$composeFile = Join-Path $repoRoot 'docker-compose.yml'
if (Test-Path -LiteralPath $composeFile) {
    $composeText = [System.IO.File]::ReadAllText($composeFile)
    $required = @([regex]::Matches($composeText, '\$\{([A-Za-z_][A-Za-z0-9_]*):\?') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}
if ($required.Count -eq 0) {
    Write-Warn 'Could not read the guards from docker-compose.yml; using the documented list.'
    $required = @('CORS_ALLOWED_ORIGINS', 'JWT_SIGNING_KEY', 'MYSQL_DATABASE',
                  'MYSQL_PASSWORD', 'MYSQL_ROOT_PASSWORD', 'MYSQL_USER', 'REDIS_PASSWORD')
}

foreach ($name in $required) {
    if (-not $byKey.ContainsKey($name)) {
        Write-Problem "$name is not in the file. Compose stops before starting anything."
    }
    elseif ([string]::IsNullOrWhiteSpace($byKey[$name].Value)) {
        Write-Problem "$name is present but empty. A `:?` guard treats that the same as absent."
    }
    elseif ($byKey[$name].HasDollar) {
        Write-Warn "$name contains a `$; what the stack receives cannot be read off the file."
    }
    else {
        Write-Good ("{0,-22} set ({1} characters)" -f $name, $byKey[$name].Value.Length)
    }
}

# =========================================================================
# 6. Values that satisfy compose and then break something later
# =========================================================================

Write-Section '6. Values compose accepts but the application does not'

# The API validates this itself at startup and exits if it is too short, well
# after compose has reported success.
if ($byKey.ContainsKey('JWT_SIGNING_KEY') -and -not $byKey['JWT_SIGNING_KEY'].HasDollar) {
    $jwtLen = $byKey['JWT_SIGNING_KEY'].Value.Length
    if ($jwtLen -gt 0 -and $jwtLen -lt 32) {
        Write-Problem "JWT_SIGNING_KEY is $jwtLen characters; the API requires at least 32 and exits on less."
        Write-Info '          The database and Redis come up, then the backend container stops.'
    }
    elseif ($jwtLen -ge 32) {
        Write-Good "JWT_SIGNING_KEY is long enough ($jwtLen characters, minimum 32)."
    }
}

# The MySQL entrypoint creates MYSQL_USER itself and refuses to create root.
if ($byKey.ContainsKey('MYSQL_USER') -and $byKey['MYSQL_USER'].Value -eq 'root') {
    Write-Problem 'MYSQL_USER is root. The MySQL entrypoint refuses to create that account.'
    Write-Info '          Use an application user, for example storefront.'
}

if ($byKey.ContainsKey('MYSQL_PASSWORD') -and $byKey.ContainsKey('MYSQL_ROOT_PASSWORD') -and
    $byKey['MYSQL_PASSWORD'].Value.Length -gt 0 -and
    $byKey['MYSQL_PASSWORD'].Value -eq $byKey['MYSQL_ROOT_PASSWORD'].Value) {
    Write-Warn 'MYSQL_PASSWORD is the same as MYSQL_ROOT_PASSWORD; they are meant to differ.'
}

# Deliberately unguarded in docker-compose.yml, because a redeploy over a
# database that already has its admin does not need them. On a first install
# an empty pair means no account is created and there is no way to log in.
$seedUser = if ($byKey.ContainsKey('ADMIN_SEED_USERNAME')) { $byKey['ADMIN_SEED_USERNAME'].Value } else { '' }
$seedPass = if ($byKey.ContainsKey('ADMIN_SEED_PASSWORD')) { $byKey['ADMIN_SEED_PASSWORD'].Value } else { '' }
if ([string]::IsNullOrWhiteSpace($seedUser) -or [string]::IsNullOrWhiteSpace($seedPass)) {
    Write-Warn 'ADMIN_SEED_USERNAME/ADMIN_SEED_PASSWORD are not both set.'
    Write-Info '          The stack will start, but on a new database no admin account is'
    Write-Info '          created and there is no way to log in. Harmless if this database'
    Write-Info '          already has its admin - the pair is only read while users is empty.'
}
else {
    Write-Good 'ADMIN_SEED_USERNAME/ADMIN_SEED_PASSWORD are both set.'
}

# =========================================================================
# 7. It must not be committed
# =========================================================================

# `ls-files -- .env` lists the path when it is tracked and prints nothing when
# it is not. `--error-unmatch` would report the ordinary, healthy case on stderr,
# and stderr must not be redirected here - see the note in section 8.
if ((Test-Path -LiteralPath (Join-Path $repoRoot '.git')) -and
    (Get-Command git -ErrorAction SilentlyContinue)) {
    $tracked = @(git ls-files -- .env)
    if ($tracked.Count -gt 0) {
        Write-Section '7. Version control'
        Write-Problem '.env is tracked by git. It holds secrets and must not be.'
        Write-Info '          git rm --cached .env'
        Write-Info '          Then rotate everything in it: the history still has the old values.'
    }
}

# =========================================================================
# 8. Let compose have the last word
# =========================================================================

if (-not $SkipCompose) {
    Write-Section '8. Cross-check with compose'
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Info 'docker is not on PATH; skipping.'
    }
    else {
        # --quiet keeps the resolved configuration - passwords included - out of
        # the scrollback. Compose still prints the reason on failure.
        # Nothing redirects docker's stderr: under Windows PowerShell 5.1 a
        # redirected native stderr becomes a terminating error.
        $global:LASTEXITCODE = 0
        if ($isDefaultPath) { docker compose config --quiet }
        else { docker compose --env-file $Path config --quiet }

        if ($LASTEXITCODE -eq 0) {
            Write-Good 'docker compose config resolved the whole file.'
        }
        else {
            Write-Problem 'docker compose config failed; its reason is printed above.'
        }
    }
}

# =========================================================================

Write-Host ''
if ($script:Problems -gt 0) {
    Write-Host "$script:Problems problem(s), $script:Warnings warning(s)." -ForegroundColor Red
    Write-Host 'The stack will not come up until the problems are fixed.'
    exit 1
}
if ($script:Warnings -gt 0) {
    Write-Host "No problems, $script:Warnings warning(s)." -ForegroundColor Yellow
    Write-Host 'The stack should come up. Read the warnings before trusting it.'
    exit 0
}
Write-Host 'No problems found.' -ForegroundColor Green
Write-Host 'If the stack still does not start, the cause is not this file:'
Write-Host '  docker compose up -d --build'
Write-Host '  docker compose logs --tail 50'
exit 0
