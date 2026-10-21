param(
    [Parameter(Mandatory)][string]$MariaDbDirectory,
    [Parameter(Mandatory)][string]$TestDirectory,
    [int]$Port = 33370
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = [IO.Path]::GetFullPath($TestDirectory)
if (Test-Path -LiteralPath $testRoot) { throw 'TestDirectory must be a new directory. Existing data is never deleted.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$password = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$data = Join-Path $testRoot 'data'
& (Join-Path $MariaDbDirectory 'bin/mariadb-install-db.exe') "--datadir=$data" "--password=$password" "--port=$Port"
if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed.' }
$arguments = @('--no-defaults', "--basedir=`"$MariaDbDirectory`"", "--datadir=`"$data`"", "--port=$Port", '--bind-address=127.0.0.1', '--skip-name-resolve', '--innodb-buffer-pool-size=64M', "--log-error=`"$(Join-Path $testRoot 'server.log')`"")
$server = Start-Process -FilePath (Join-Path $MariaDbDirectory 'bin/mariadbd.exe') -ArgumentList $arguments -PassThru -WindowStyle Hidden
$env:MYSQL_PWD = $password
$env:REPAIRDESK_TEST_PASSWORD = $password
$env:REPAIRDESK_TEST_PORT = "$Port"
$settings = @{ Server='127.0.0.1';Port=$Port;Database='repairdesk';User='root';Password=$password }
$connectionPath = Join-Path $testRoot 'connection.local.json'
$settings | ConvertTo-Json | Set-Content -LiteralPath $connectionPath -Encoding utf8
$env:REPAIRDESK_TEST_CONNECTION = $connectionPath
try {
    $ready = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        & (Join-Path $MariaDbDirectory 'bin/mariadb-admin.exe') --host=127.0.0.1 "--port=$Port" --user=root ping 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $ready=$true; break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Database did not start.' }
    Push-Location $projectRoot
    try {
        dotnet run --project tests/RepairDesk.ApiTests -c Release -- --initialize
        if ($LASTEXITCODE -ne 0) { throw 'REST integration tests failed.' }
        dotnet run --project tests/RepairDesk.Tests -c Release -- --integration
        if ($LASTEXITCODE -ne 0) { throw 'Repository integration tests failed.' }
    } finally { Pop-Location }
} finally {
    & (Join-Path $MariaDbDirectory 'bin/mariadb-admin.exe') --host=127.0.0.1 "--port=$Port" --user=root shutdown 2>$null | Out-Null
    if (-not $server.HasExited) { $server.WaitForExit(10000) | Out-Null }
    $env:MYSQL_PWD=$null
    $env:REPAIRDESK_TEST_PASSWORD=$null
    $env:REPAIRDESK_TEST_CONNECTION=$null
    $env:REPAIRDESK_TEST_PORT=$null
}
