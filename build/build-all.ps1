# ------------------------------------------------------------------------------
# Script de Build & Publicação - BacateTagAssist
# ------------------------------------------------------------------------------

param(
    [string]$Version = "1.0.0",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
Set-Location $root

Write-Host "=================================================" -ForegroundColor Green
Write-Host " BacateTagAssist - Compilação da Release v$Version" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# 1. Testes Unitários
if (-not $SkipTests) {
    Write-Host "`n[1/4] Executando testes unitários..." -ForegroundColor Cyan
    dotnet test "$root\tests\BacateTagAssist.Tests" -c Release -nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Falha nos testes unitários!"
    }
}

$distDir = "$root\dist"
if (Test-Path $distDir) { Remove-Item -Recurse -Force $distDir }
New-Item -ItemType Directory -Force $distDir | Out-Null

# 2. Publicação Windows Desktop (Self-Contained Single File)
Write-Host "`n[2/4] Compilando Windows Desktop (Self-Contained x64)..." -ForegroundColor Cyan
$winOut = "$distDir\windows-desktop"
dotnet publish "$root\src\BacateTagAssist.Desktop\BacateTagAssist.Desktop.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version `
    -o $winOut

# Copia MediaInfo oficial para a pasta do executável Windows
$toolsMediaInfo = "$root\.tools\mediainfo\MediaInfo.exe"
if (Test-Path $toolsMediaInfo) {
    Write-Host "  Embutindo MediaInfo CLI oficial..." -ForegroundColor Gray
    $miDest = "$winOut\mediainfo"
    New-Item -ItemType Directory -Force $miDest | Out-Null
    Copy-Item $toolsMediaInfo "$miDest\MediaInfo.exe"
}

# Cria ZIP para distribuição no Windows
$winZip = "$distDir\BacateTagAssist-v$Version-Windows-x64.zip"
Write-Host "  Compactando $winZip..." -ForegroundColor Gray
Compress-Archive -Path "$winOut\*" -DestinationPath $winZip -Force

# 3. Publicação Servidor Linux (Para Docker / OMV / NAS)
Write-Host "`n[3/4] Compilando Servidor Linux x64..." -ForegroundColor Cyan
$linuxOut = "$distDir\server-linux-x64"
dotnet publish "$root\src\BacateTagAssist.Server\BacateTagAssist.Server.csproj" `
    -c Release `
    -r linux-x64 `
    --self-contained false `
    -p:Version=$Version `
    -o $linuxOut

$linuxZip = "$distDir\BacateTagAssist-v$Version-Linux-x64.zip"
Write-Host "  Compactando $linuxZip..." -ForegroundColor Gray
Compress-Archive -Path "$linuxOut\*" -DestinationPath $linuxZip -Force

Write-Host "`n[4/4] Concluído com sucesso!" -ForegroundColor Green
Write-Host "Pacotes gerados na pasta dist/:" -ForegroundColor Green
Get-ChildItem $distDir -Filter *.zip | Select-Object Name, Length
