# Brings up the throwaway test stack on Windows.
#
#   .\scripts\test-up.ps1
#   .\scripts\test-up.ps1 -StorefrontPort 8081 -ApiPort 5002
#
# Generates .env.test with random credentials on first run, validates the
# configuration, then starts the stack. Nothing to fill in by hand - the point
# is to see the project running without touching .env at all.
#
# .env.test is generated, never committed: .gitignore already covers .env.*
#
# This file is deliberately pure ASCII. Windows PowerShell 5.1 decodes a .ps1
# that carries no BOM using the system ANSI codepage, so a non-ASCII character
# would arrive mojibake on any machine whose codepage is not UTF-8 - and the
# smart quotes a word processor produces are real string delimiters to the
# PowerShell parser, not decoration.

[CmdletBinding()]
param(
    # The host ports the stack publishes on 127.0.0.1. Windows hands 8080 to
    # IIS and reserves scattered ranges for Hyper-V and WSL2, so a machine that
    # cannot offer these needs somewhere else to put them.
    [ValidateRange(1, 65535)][int]$StorefrontPort = 8080,
    [ValidateRange(1, 65535)][int]$ApiPort = 5001
)

$ErrorActionPreference = 'Stop'

if ($StorefrontPort -eq $ApiPort) {
    Write-Host "The storefront and the API cannot share port $StorefrontPort." -ForegroundColor Red
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot

$envFile = Join-Path $repoRoot '.env.test'
$composeArgs = @(
    'compose',
    '--env-file', $envFile,
    '-f', 'docker-compose.yml',
    '-f', 'docker-compose.test.yml'
)

# --- Running native commands safely ----------------------------------------

# Windows PowerShell 5.1 turns a native command's stderr into error records the
# moment that stream is redirected (`2>`, `*>`, `2>&1`). Under
# $ErrorActionPreference = 'Stop' those records are terminating errors, so a
# docker that merely printed a warning would be indistinguishable from a docker
# that failed. Nothing here redirects a native stderr: it goes straight to the
# console, where the user can read it, and success is judged by the exit code
# alone. Only stdout is ever discarded.
#
# The exit code is left in $LASTEXITCODE for the caller to read rather than
# returned: docker's own stdout already flows into this function's output
# stream, so a returned code would arrive appended to it, and `-ne 0` against
# that array filters instead of comparing - every command that printed anything
# would look like a failure.
function Invoke-Docker {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string[]]$DockerArgs,
        # Discard stdout. Errors still reach the console.
        [switch]$Quiet
    )

    # A native command that never launches leaves the previous command's exit
    # code in place; start from a known value so a stale 0 cannot read as
    # success.
    $global:LASTEXITCODE = 0
    if ($Quiet) { & docker @DockerArgs | Out-Null }
    else { & docker @DockerArgs }
}

# --- Docker must be installed, running, and recent enough ------------------

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host 'Could not find docker on PATH.' -ForegroundColor Red
    Write-Host 'Install Docker Desktop - or, if you installed it after opening this'
    Write-Host 'terminal, open a new one so it picks up the updated PATH.'
    exit 1
}

Invoke-Docker @('info', '--format', '{{.ServerVersion}}') -Quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Docker is installed but the daemon did not answer.' -ForegroundColor Red
    Write-Host 'Start Docker Desktop, wait until it reports Running, and try again.'
    exit 1
}

Invoke-Docker @('compose', 'version') -Quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host 'This Docker has no `docker compose` command.' -ForegroundColor Red
    Write-Host 'The stack needs Compose v2. Update Docker Desktop; the standalone'
    Write-Host 'docker-compose.exe from v1 cannot read these files.'
    exit 1
}

# docker-compose.test.yml uses the `!override` tag to replace the frontend's
# published port instead of appending to it. Compose added that tag in 2.24;
# before it the file fails with a bare YAML tag error naming neither the
# feature nor the file.
$global:LASTEXITCODE = 0
$composeVersion = (@(& docker compose version --short) -join '').Trim()
if ($LASTEXITCODE -eq 0 -and $composeVersion -match '(\d+)\.(\d+)') {
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    if ($major -lt 2 -or ($major -eq 2 -and $minor -lt 24)) {
        Write-Host "Compose $composeVersion is too old for this stack." -ForegroundColor Red
        Write-Host 'docker-compose.test.yml needs the !override tag, added in Compose 2.24.'
        Write-Host 'Update Docker Desktop and try again.'
        exit 1
    }
}

# --- Writing .env.test ------------------------------------------------------

# Parity with the chmod 600 in test-up.sh. The file holds the database root
# password, the JWT signing key and the admin password; on a shared machine the
# inherited ACLs would leave all three readable by every local user. Best
# effort: failing to tighten an ACL is worth a warning, not a dead stack.
function Protect-File {
    param([Parameter(Mandatory)][string]$Path)

    # $IsWindows does not exist before PowerShell 6, where the answer is always
    # yes; -or short-circuits, so it is never evaluated there.
    $onWindows = ($PSVersionTable.PSVersion.Major -lt 6) -or $IsWindows
    if (-not $onWindows) { return }

    # icacls, not Get-Acl/Set-Acl. Set-Acl writes the whole security descriptor,
    # and once this file's DACL is protected - which is exactly what the first
    # run makes it - writing it back asks for SeSecurityPrivilege, a privilege
    # an ordinary user does not hold. So the first run succeeded and every run
    # after it warned about a file that was already correct, while a DACL that
    # really had drifted could never be repaired. icacls edits the DACL alone,
    # needs no privilege, and is idempotent.
    #
    # /inheritance:r drops the inherited entries, which on a file this script
    # just created are all of them; /grant:r then leaves the current user as the
    # only entry. The SID rather than the account name, so nothing depends on
    # how the account is spelled or localised.
    #
    # icacls' stderr is not redirected, for the reason spelled out above
    # Invoke-Docker: only its "processed file" chatter on stdout is discarded,
    # and the exit code decides.
    $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $global:LASTEXITCODE = 0
    try {
        & icacls.exe $Path /inheritance:r /grant:r "*${sid}:(F)" | Out-Null
    }
    catch {
        Write-Host "  note: could not restrict permissions on $Path" -ForegroundColor Yellow
        Write-Host "  ($($_.Exception.Message))" -ForegroundColor Yellow
        return
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  note: could not restrict permissions on $Path" -ForegroundColor Yellow
        Write-Host "  (icacls exited $LASTEXITCODE - the file holds the database root" -ForegroundColor Yellow
        Write-Host "  password, the JWT signing key and the admin password)" -ForegroundColor Yellow
    }
}

# UTF-8 without a BOM, written through .NET rather than Set-Content or `>`:
# PowerShell 5.1 writes UTF-16 for `>` redirection and defaults Set-Content to
# the ANSI codepage. Compose can parse neither - it reads the file as binary
# noise and every variable comes out unset, which surfaces much later as an
# empty MYSQL_ROOT_PASSWORD and an unhealthy database container.
function Write-EnvFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        # The blank separator lines in .env.test are empty strings, which a
        # mandatory [string[]] rejects by default.
        [Parameter(Mandatory)][AllowEmptyCollection()][AllowEmptyString()][string[]]$Lines
    )
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, ($Lines -join "`n") + "`n", $utf8NoBom)
    Protect-File -Path $Path
}

# Replace KEY=... in place, or append it when the file predates the key.
function Set-EnvLine {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value
    )
    $prefix = "$Key="
    $found = $false
    $result = @(foreach ($line in $Lines) {
        if ($line.StartsWith($prefix, [StringComparison]::Ordinal)) {
            $found = $true
            "$prefix$Value"
        }
        else { $line }
    })
    if (-not $found) { $result += "$prefix$Value" }
    return $result
}

# --- Credentials -----------------------------------------------------------

# Alphanumeric only, so no value can collide with the ways compose reads a
# .env file: `$` starts an interpolation and `#` starts a comment. The modulo
# below is very slightly biased; for a credential that exists only inside a
# localhost-only throwaway stack that does not matter.
function New-Secret {
    param([int]$Length = 32)
    $alphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    $bytes = New-Object 'byte[]' $Length
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

if (-not (Test-Path -LiteralPath $envFile)) {
    Write-Host 'Generating .env.test with random credentials...' -ForegroundColor Cyan

    $lines = @(
        '# Generated by scripts/test-up.ps1 for the local test stack.',
        '# Throwaway credentials for a localhost-only stack. Never reuse these,',
        '# and never copy this file to .env on a server.',
        '',
        "MYSQL_ROOT_PASSWORD=$(New-Secret 32)",
        'MYSQL_DATABASE=storefront_test',
        'MYSQL_USER=storefront',
        "MYSQL_PASSWORD=$(New-Secret 32)",
        "REDIS_PASSWORD=$(New-Secret 32)",
        '',
        '# The published ports, and the origins the API will accept. These three',
        '# move together: the browser calls the API from the storefront origin,',
        '# so a port changed without its CORS entry blocks every request.',
        "TEST_STOREFRONT_PORT=$StorefrontPort",
        "TEST_API_PORT=$ApiPort",
        "CORS_ALLOWED_ORIGINS=http://localhost:$StorefrontPort,http://127.0.0.1:$StorefrontPort",
        "JWT_SIGNING_KEY=$(New-Secret 64)",
        '',
        'ADMIN_SEED_USERNAME=admin',
        "ADMIN_SEED_PASSWORD=$(New-Secret 20)",
        'ADMIN_SEED_EMAIL=',
        '',
        'STORE_NAME=Loja de Teste',
        'STORE_CURRENCY=BRL',
        'STORE_LOCALE=pt-BR',
        'STORE_COUNTRY_CODE=BR',
        'STORE_TIME_ZONE=America/Sao_Paulo',
        'STORE_LOGO_URL=/logoFarma.png',
        'STORE_WHATSAPP_NUMBER=',
        '',
        'ANALYTICS_RAW_RETENTION_DAYS=90',
        'OTLP_ENDPOINT=',
        'OTEL_SERVICE_NAME=storefront-test'
    )

    Write-EnvFile -Path $envFile -Lines $lines
}
else {
    Write-Host 'Reusing the existing .env.test' -ForegroundColor Cyan

    # Reconcile the ports, so -StorefrontPort works on the second run too. Only
    # these three lines are touched: regenerating the credentials would not
    # change the admin account, which is seeded once while the users table is
    # empty, and would leave the printed password wrong.
    $lines = @(Get-Content -LiteralPath $envFile)
    $before = $lines -join "`n"
    $lines = Set-EnvLine $lines 'TEST_STOREFRONT_PORT' "$StorefrontPort"
    $lines = Set-EnvLine $lines 'TEST_API_PORT' "$ApiPort"
    $lines = Set-EnvLine $lines 'CORS_ALLOWED_ORIGINS' "http://localhost:$StorefrontPort,http://127.0.0.1:$StorefrontPort"
    if (($lines -join "`n") -ne $before) {
        Write-Host "  ports set to storefront $StorefrontPort, API $ApiPort" -ForegroundColor Cyan
        Write-EnvFile -Path $envFile -Lines $lines
    }
}

# --- Validate before starting anything -------------------------------------

# --quiet keeps the resolved YAML - every generated password included - out of
# the terminal scrollback. Compose still prints the reason on failure.
Write-Host 'Validating the configuration...' -ForegroundColor Cyan
Invoke-Docker ($composeArgs + @('config', '--quiet'))
if ($LASTEXITCODE -ne 0) {
    Write-Host 'The configuration is invalid; nothing was started.' -ForegroundColor Red
    Write-Host 'The line above names what compose could not resolve. If .env.test was'
    Write-Host 'hand-edited, deleting it and re-running regenerates a working one -'
    Write-Host 'but tear the stack down with -v first, or the new database password'
    Write-Host 'will not match the volume that already exists.'
    exit 1
}

# --- Ports must be free ----------------------------------------------------

# There are two questions here, and binding only answers the first.
#
# Can the port be bound? Binding is the only honest test for the ranges Windows
# reserves for Hyper-V and WSL2, which `netstat` does not show as occupied but
# Docker still cannot bind.
#
# Will the URL this script prints actually reach the stack? That is a separate
# question, and on Windows the answer can be no even when the bind succeeds.
# Windows allows a bind to 127.0.0.1 while another process already serves the
# same port on 0.0.0.0 - the more specific bind simply wins for that one
# address - and `localhost` resolves to ::1 before 127.0.0.1. So the stack can
# take 127.0.0.1:8081 cleanly, report success, and still hand the browser a
# different project's server on ::1:8081. Expo's dev server defaults to exactly
# that port and binds both wildcards, which is how this was found.
#
# Rejecting the port in both cases is right: what the user needs is a port that
# is theirs on every address `localhost` can resolve to.
function Test-PortFree {
    param([Parameter(Mandatory)][int]$Port)

    # A listener on any of these either refuses our bind or shadows the URL.
    $shadowing = @(
        [System.Net.IPAddress]::Any,           # 0.0.0.0
        [System.Net.IPAddress]::IPv6Any,       # ::
        [System.Net.IPAddress]::Loopback,      # 127.0.0.1
        [System.Net.IPAddress]::IPv6Loopback   # ::1
    )
    try {
        $active = [System.Net.NetworkInformation.IPGlobalProperties]::
            GetIPGlobalProperties().GetActiveTcpListeners()
        foreach ($endpoint in $active) {
            if ($endpoint.Port -eq $Port -and $shadowing -contains $endpoint.Address) {
                return $false
            }
        }
    }
    catch {
        # No listener table is a reason to fall through to the bind, not to
        # call the port taken.
    }

    $listener = $null
    try {
        $listener = New-Object System.Net.Sockets.TcpListener(
            [System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        return $true
    }
    catch { return $false }
    finally { if ($null -ne $listener) { try { $listener.Stop() } catch { } } }
}

# Skip the check when the stack already holds the ports itself: compose will
# recreate those containers, and complaining about our own listener would make
# a second run impossible. This runs after validation on purpose - `ps` has to
# interpolate the same files, so on a broken .env.test it would print the very
# same error a second time.
$global:LASTEXITCODE = 0
$running = @(& docker @composeArgs ps --quiet)
$stackAlreadyUp = ($LASTEXITCODE -eq 0 -and $running.Count -gt 0)

if (-not $stackAlreadyUp) {
    $wanted = @(
        @{ Name = 'storefront'; Port = $StorefrontPort; Flag = '-StorefrontPort' },
        @{ Name = 'API';        Port = $ApiPort;        Flag = '-ApiPort' }
    )
    $blocked = @($wanted | Where-Object { -not (Test-PortFree $_.Port) })
    if ($blocked.Count -gt 0) {
        Write-Host 'Nothing was started: a port the stack needs is unavailable.' -ForegroundColor Red
        foreach ($p in $blocked) {
            Write-Host "  127.0.0.1:$($p.Port) ($($p.Name)) cannot be bound."
        }
        Write-Host ''
        Write-Host 'Find what holds it:'
        foreach ($p in $blocked) { Write-Host "  netstat -ano | findstr :$($p.Port)" }
        Write-Host 'A listener on 0.0.0.0 or [::] counts, even though binding'
        Write-Host '127.0.0.1 would still succeed next to it: `localhost` resolves to ::1'
        Write-Host 'first on Windows, so that process would answer the browser instead of'
        Write-Host 'this stack. Node and Expo dev servers default to 8081.'
        Write-Host 'Windows also reserves ranges for Hyper-V and WSL2, which show as free:'
        Write-Host '  netsh int ipv4 show excludedportrange protocol=tcp'
        Write-Host 'Or move the stack somewhere else, for example:'
        Write-Host "  .\scripts\test-up.ps1 $(($blocked | ForEach-Object { "$($_.Flag) $($_.Port + 1)" }) -join ' ')"
        exit 1
    }
}

# --- Up --------------------------------------------------------------------

Write-Host 'Building and starting (first run pulls images and can take a few minutes)...' -ForegroundColor Cyan
Invoke-Docker ($composeArgs + @('up', '--build', '-d'))
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host 'The stack did not come up. Logs:' -ForegroundColor Red
    Write-Host "  docker $($composeArgs -join ' ') logs"
    exit 1
}

# Read back rather than reuse the generated values, so the credentials shown
# are the ones the stack actually booted with when .env.test already existed.
function Get-EnvValue {
    param([string]$Key)
    $line = Get-Content -LiteralPath $envFile |
        Where-Object { $_ -match "^$([regex]::Escape($Key))=" } |
        Select-Object -First 1
    if ($null -eq $line) { return '(not set)' }
    $value = $line.Substring($Key.Length + 1)
    if ([string]::IsNullOrWhiteSpace($value)) { return '(empty)' }
    return $value
}

$adminUser = Get-EnvValue 'ADMIN_SEED_USERNAME'
$adminPass = Get-EnvValue 'ADMIN_SEED_PASSWORD'

Write-Host ''
Write-Host 'Test stack is up.' -ForegroundColor Green
Write-Host ''
# 127.0.0.1, not localhost. Compose publishes these ports on 127.0.0.1, which
# is IPv4 only, while Windows resolves `localhost` to ::1 first. With nothing
# on ::1 the browser just falls back to IPv4 and the difference is invisible,
# but anything else holding the port on ::1 or :: answers instead - silently,
# and with its own error page. The literal address is the one that is always
# this stack. CORS_ALLOWED_ORIGINS covers both spellings, so either works once
# the port is genuinely free.
Write-Host "  Storefront   http://127.0.0.1:$StorefrontPort"
Write-Host "  Admin        http://127.0.0.1:$StorefrontPort/login"
Write-Host "  Swagger      http://127.0.0.1:$ApiPort/swagger"
Write-Host "  Health       http://127.0.0.1:$ApiPort/health"
Write-Host ''
Write-Host "  user  $adminUser"
Write-Host "  pass  $adminPass"
Write-Host ''
Write-Host 'The credentials are in .env.test. Keep the file: the admin account is'
Write-Host 'seeded once, while the users table is empty, so deleting .env.test and'
Write-Host 'regenerating it will NOT change the password of an account that already'
Write-Host 'exists. To start over from scratch, tear down with -v first.'
Write-Host ''
Write-Host 'To follow the logs:'
Write-Host "  docker $($composeArgs -join ' ') logs -f"
Write-Host 'To tear it down, volumes included:'
Write-Host "  docker $($composeArgs -join ' ') down -v"
