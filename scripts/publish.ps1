param([string]$MariaDbDirectory,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path (Split-Path -Parent $projectRoot) 'RepairDesk-Portable' }
if (-not $MariaDbDirectory -or -not (Test-Path -LiteralPath (Join-Path $MariaDbDirectory 'bin\mariadbd.exe'))) { throw 'Specify -MariaDbDirectory for the extracted official MariaDB Windows ZIP.' }
dotnet publish (Join-Path $projectRoot 'src\RepairDesk.Desktop\RepairDesk.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $OutputDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$serverOutput = Join-Path $OutputDirectory 'server'
New-Item -ItemType Directory -Force -Path $serverOutput | Out-Null
Get-ChildItem -LiteralPath $MariaDbDirectory | Copy-Item -Destination $serverOutput -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\QUICKSTART.md') -Destination (Join-Path $OutputDirectory 'START-HERE.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\THIRD-PARTY.md') -Destination $OutputDirectory -Force
Write-Output "Ready: $OutputDirectory"
