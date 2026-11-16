param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "win-arm64")]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [string] $PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string] $PortableDirectory,

    [switch] $RequireReduction
)

$ErrorActionPreference = "Stop"

function Get-DirectoryBytes([string] $Path) {
    $files = Get-ChildItem -LiteralPath $Path -File -Recurse
    return [long](($files | Measure-Object -Property Length -Sum).Sum)
}

if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
    throw "RID publish output does not exist: $PublishDirectory"
}
if (-not (Test-Path -LiteralPath $PortableDirectory -PathType Container)) {
    throw "Portable renderer output does not exist: $PortableDirectory"
}
$portableFullPath = [IO.Path]::GetFullPath($PortableDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
$publishFullPath = [IO.Path]::GetFullPath($PublishDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if ($publishFullPath.StartsWith($portableFullPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "RID publish output must not be nested in portable output: $PublishDirectory"
}

$requiredFiles = @(
    "VisualWeb.Renderer.dll",
    "VisualWeb.Renderer.deps.json",
    "VisualWeb.Renderer.runtimeconfig.json"
)
foreach ($name in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory $name) -PathType Leaf)) {
        throw "RID publish output is missing required file: $name"
    }
}

$depsPath = Join-Path $PublishDirectory "VisualWeb.Renderer.deps.json"
$deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json
$targetName = [string]$deps.runtimeTarget.name
if (-not $targetName.EndsWith("/$RuntimeIdentifier", [StringComparison]::OrdinalIgnoreCase)) {
    throw "Expected $RuntimeIdentifier in deps runtime target, found '$targetName'."
}
if ($deps.runtimeOptions.selfContained -eq $true) {
    throw "RID renderer publish must remain framework-dependent."
}

$target = $deps.targets.PSObject.Properties[$targetName]
if ($null -eq $target) {
    throw "RID deps target '$targetName' is missing."
}

$runtimeConfig = Get-Content -LiteralPath (Join-Path $PublishDirectory "VisualWeb.Renderer.runtimeconfig.json") -Raw |
    ConvertFrom-Json
if ($null -eq $runtimeConfig.runtimeOptions.framework -and
    $null -eq $runtimeConfig.runtimeOptions.frameworks) {
    throw "RID publish is not framework-dependent: runtimeconfig has no framework reference."
}

$nativeAssets = @()
foreach ($library in $target.Value.PSObject.Properties) {
    foreach ($asset in $library.Value.native.PSObject.Properties) {
        $rid = ""
        if ($asset.Name -match "^runtimes/([^/]+)/native/") {
            $rid = $Matches[1]
        }
        $nativeAssets += [pscustomobject]@{
            Name = [string]$asset.Name
            Rid = $rid
        }
    }
    foreach ($asset in $library.Value.runtimeTargets.PSObject.Properties) {
        if ($asset.Value.assetType -eq "native") {
            $nativeAssets += [pscustomobject]@{
                Name = [string]$asset.Name
                Rid = [string]$asset.Value.rid
            }
        }
    }
}

$expectedNativeNames = @(
    "ClearScriptV8.$RuntimeIdentifier.dll",
    "libHarfBuzzSharp.dll",
    "libSkiaSharp.dll"
)
$publishFiles = @(Get-ChildItem -LiteralPath $PublishDirectory -File -Recurse)
foreach ($name in $expectedNativeNames) {
    if (-not ($nativeAssets | Where-Object {
        $_.Rid -ceq $RuntimeIdentifier -and
        $_.Name.EndsWith("/$name", [StringComparison]::OrdinalIgnoreCase)
    })) {
        throw "RID deps target does not declare '$name' for active RID '$RuntimeIdentifier'."
    }
    if (-not ($publishFiles | Where-Object { $_.Name -ceq $name })) {
        throw "RID publish output is missing required native asset '$name'."
    }
}

$compatibleNativeRids = @($RuntimeIdentifier, "win", "any")
foreach ($asset in $nativeAssets) {
    if (-not ($compatibleNativeRids -ccontains $asset.Rid)) {
        throw "RID publish retained incompatible native asset '$($asset.Name)' for '$($asset.Rid)'."
    }
}
foreach ($file in $publishFiles) {
    $relativePath = [IO.Path]::GetRelativePath($PublishDirectory, $file.FullName).Replace("\", "/")
    if ($relativePath -match "^runtimes/([^/]+)/native/") {
        $rid = $Matches[1]
        if (-not ($compatibleNativeRids -ccontains $rid)) {
            throw "RID publish output contains incompatible native payload '$relativePath'."
        }
    }
    if ($file.Name -match "^ClearScriptV8\.(win(?:-[a-z0-9]+)*|any)\.dll$") {
        $rid = $Matches[1]
        if (-not ($compatibleNativeRids -ccontains $rid)) {
            throw "RID publish output contains incompatible native payload '$relativePath'."
        }
    }
}

$symbolNames = @("libHarfBuzzSharp.pdb", "libSkiaSharp.pdb")
$assetsPath = Join-Path $PSScriptRoot "../src/Apps/VisualWeb.Renderer/obj/project.assets.json"
if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
    throw "Restored renderer package assets are missing: $assetsPath"
}
$assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
$symbolPackages = @{
    "libHarfBuzzSharp.pdb" = "HarfBuzzSharp.NativeAssets.Win32"
    "libSkiaSharp.pdb" = "SkiaSharp.NativeAssets.Win32"
}
foreach ($name in $symbolNames) {
    $packageName = $symbolPackages[$name]
    $package = $assets.libraries.PSObject.Properties |
        Where-Object { $_.Name.StartsWith("$packageName/", [StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1
    if ($null -eq $package) {
        throw "Restored baseline package is missing for native symbol '$name'."
    }

    $relativeSymbolPath = "runtimes/$RuntimeIdentifier/native/$name"
    $baselineSymbol = $null
    foreach ($packageFolder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path (Join-Path $packageFolder $package.Value.path) $relativeSymbolPath
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $baselineSymbol = $candidate
            break
        }
    }
    if ($null -eq $baselineSymbol) {
        throw "Restored baseline package has no '$relativeSymbolPath' symbol."
    }

    $publishedSymbol = $publishFiles | Where-Object { $_.Name -ceq $name } | Select-Object -First 1
    if ($null -eq $publishedSymbol) {
        throw "RID publish output did not retain baseline native symbol '$name'."
    }
    $baselineHash = (Get-FileHash -LiteralPath $baselineSymbol -Algorithm SHA256).Hash
    $publishedHash = (Get-FileHash -LiteralPath $publishedSymbol.FullName -Algorithm SHA256).Hash
    if ($publishedHash -cne $baselineHash) {
        throw "RID publish symbol '$name' differs from its restored baseline native asset."
    }
}

$portableBytes = Get-DirectoryBytes $PortableDirectory
$publishBytes = Get-DirectoryBytes $PublishDirectory
if ($RequireReduction -and $publishBytes -ge $portableBytes) {
    throw "RID publish is not smaller than portable output ($publishBytes bytes vs $portableBytes bytes)."
}

Write-Host "PASS: $RuntimeIdentifier framework-dependent publish is structurally valid."
Write-Host "Payload bytes: portable=$portableBytes; RID=$publishBytes; difference=$($portableBytes - $publishBytes) bytes."
Write-Host "Retained baseline native symbols (SHA-256 verified): $($symbolNames -join ', ')."
