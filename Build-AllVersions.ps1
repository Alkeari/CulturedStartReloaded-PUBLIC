<#
Builds one implementation per game version and merges them into a single module folder.

The BUTR loader reads an AssemblyMetadataAttribute("GameVersion") from each DLL beside the module's
loader, matches the running game exactly, and on a miss takes the HIGHEST version present without
regard to what the game can actually run. So a module folder must hold either exactly one
implementation, or one for every version it claims. This produces the latter.

Each version is built into its own scratch folder, because the SDK's RenameModuleFolder target
replaces the module folder wholesale and a shared target would leave only the last build. The DLLs
are then merged into one module, which is what ships.

Usage:
  .\Build-AllVersions.ps1 -Out <staging folder> [-Configuration Release] [-Versions v1.4.8,v1.3.15]
#>
param(
    [Parameter(Mandatory)][string]$Out,
    [string]$Configuration = 'Release',
    [string[]]$Versions,
    [string]$ModuleName = 'Cultured Start Reloaded',
    [string]$Project = 'Cultured Start Reloaded.csproj'
)
$ErrorActionPreference = 'Stop'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe'
$root = $PSScriptRoot

if (-not $Versions) {
    $Versions = (Get-Content (Join-Path $root 'supported-game-versions.txt')) |
        ForEach-Object { $_.Trim() } | Where-Object { $_ }
}

$scratch = Join-Path ([IO.Path]::GetTempPath()) ("csr-allver-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$merged = Join-Path $Out $ModuleName
if (Test-Path $merged) { Remove-Item $merged -Recurse -Force }

# The manifest's DependedModuleMetadata version is the only ENFORCED declaration and it is a FLOOR,
# so the merged module must take its SubModule.xml from the LOWEST version built.
# Sorted ascending rather than merely reversed, so the floor is correct whatever order they arrive in.
# pwsh -File passes a comma list as one string, so split before sorting
$Versions = @($Versions) | ForEach-Object { $_ -split ',' } | Where-Object { $_ } |
    Sort-Object { [version]($_.Trim() -replace '^v', '') }

$built = @()
foreach ($v in $Versions) {
    $target = Join-Path $scratch $v
    # The SDK skips the copy entirely, with only a warning, when GameFolder does not exist
    New-Item -ItemType Directory -Force -Path (Join-Path $target 'Modules') | Out-Null
    Write-Host "building $v ..." -NoNewline
    & $msbuild (Join-Path $root $Project) -p:Configuration=$Configuration -p:Platform=x64 -restore `
        -p:OverrideGameVersion=$v -p:GameFolder=$target -v:quiet -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "build failed for $v" }

    $src = Join-Path $target "Modules\$ModuleName"
    if (-not (Test-Path $src)) { throw "no module produced for $v at $src" }

    # The first build supplies the whole module: manifest, GUI, ModuleData, loader.
    if (-not (Test-Path $merged)) {
        Copy-Item $src $merged -Recurse -Force
        # Debug symbols are not shipped
        Get-ChildItem $merged -Recurse -Filter '*.pdb' | Remove-Item -Force
    }

    # Every build contributes only its own implementation DLLs, per platform bin folder.
    foreach ($bin in (Get-ChildItem (Join-Path $src 'bin') -Directory)) {
        $dest = Join-Path $merged "bin\$($bin.Name)"
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        Get-ChildItem $bin.FullName -Filter '*.dll' |
            Where-Object { $_.Name -notlike '*ModuleLoader*' } |
            ForEach-Object { Copy-Item $_.FullName (Join-Path $dest $_.Name) -Force }
    }
    $built += $v
    Write-Host " ok"
}
Remove-Item $scratch -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`nmerged module: $merged"
foreach ($bin in (Get-ChildItem (Join-Path $merged 'bin') -Directory)) {
    $impl = Get-ChildItem $bin.FullName -Filter '*.dll' | Where-Object { $_.Name -notlike '*ModuleLoader*' }
    Write-Host ("  {0,-34} {1} implementations" -f $bin.Name, $impl.Count)
}
Write-Host ("  versions: {0}" -f ($built -join ' '))
