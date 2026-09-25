$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\Tts7BalconApi\Tts7BalconApi.csproj'
$release = Join-Path $PSScriptRoot 'release'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Instala el SDK de .NET 10 en Windows y vuelve a ejecutar build.ps1.'
}

dotnet publish $project -c Release -r win-x86 --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=false -o $release
if ($LASTEXITCODE -ne 0) { throw 'Fallo dotnet publish.' }

New-Item -ItemType Directory -Force (Join-Path $release 'tools\balcon') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'tools\balcon\README.txt') (Join-Path $release 'tools\balcon\README.txt') -Force
Copy-Item (Join-Path $PSScriptRoot 'START_API.cmd') (Join-Path $release 'START_API.cmd') -Force
Write-Host "API lista en: $release"
