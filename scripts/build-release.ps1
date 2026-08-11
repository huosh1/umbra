param(
    [string]$Version = "1.0.0",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"
$installerOutput = Join-Path $artifacts "installer"

if (Test-Path -LiteralPath $artifacts) {
    Remove-Item -LiteralPath $artifacts -Recurse -Force
}
New-Item -ItemType Directory -Path $publish, $installerOutput -Force | Out-Null

if (-not $SkipTests) {
    dotnet test (Join-Path $root "Umbra.Tests\Umbra.Tests.csproj") -c Release -p:Platform=AnyCPU
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
}

dotnet publish (Join-Path $root "Umbra.App\Umbra.App.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:SkipBrowserHostCopy=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "Umbra publish failed." }

# Publish the native messaging host into the same self-contained directory so
# it shares the runtime files already emitted for Umbra instead of duplicating
# an entire .NET runtime in a nested directory.
dotnet publish (Join-Path $root "Umbra.BrowserHost\Umbra.BrowserHost.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "Browser host publish failed." }

$extensionManifest = Get-Content -Raw -LiteralPath (Join-Path $root "Umbra.App\browser-extension\manifest.json") | ConvertFrom-Json
$extensionVersion = $extensionManifest.version
$extensionZip = Join-Path $artifacts "Umbra-Extension-$extensionVersion.zip"
Compress-Archive -Path (Join-Path $root "Umbra.App\browser-extension\*") -DestinationPath $extensionZip -CompressionLevel Optimal

# Chrome Web Store rejects a developer-defined manifest key. Keep the regular
# archive for manual/unpacked installs (where the key preserves the extension
# ID expected by the native host), and create a second Store-only archive with
# that field removed. Google assigns the definitive Store item ID on upload.
$storeExtensionDirectory = Join-Path $artifacts "store-extension"
Copy-Item -LiteralPath (Join-Path $root "Umbra.App\browser-extension") `
    -Destination $storeExtensionDirectory -Recurse
$storeManifestPath = Join-Path $storeExtensionDirectory "manifest.json"
$storeManifest = Get-Content -Raw -LiteralPath $storeManifestPath | ConvertFrom-Json
$storeManifest.PSObject.Properties.Remove("key")
$storeManifestJson = $storeManifest | ConvertTo-Json -Depth 20
[IO.File]::WriteAllText($storeManifestPath, $storeManifestJson, [Text.UTF8Encoding]::new($false))
$storeExtensionZip = Join-Path $artifacts "Umbra-Extension-Chrome-Web-Store-$extensionVersion.zip"
Compress-Archive -Path (Join-Path $storeExtensionDirectory "*") `
    -DestinationPath $storeExtensionZip -CompressionLevel Optimal

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 is required. Install it with: winget install JRSoftware.InnoSetup"
}

& $iscc "/DAppVersion=$Version" (Join-Path $root "installer\Umbra.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed." }

$checksums = @(Get-ChildItem $installerOutput -File)
$checksums += @(Get-Item $extensionZip)
$checksums += @(Get-Item $storeExtensionZip)
$checksumPath = Join-Path $artifacts "SHA256SUMS.txt"
$checksums | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content -LiteralPath $checksumPath -Encoding ASCII

Write-Host "Release artifacts created in $artifacts"
