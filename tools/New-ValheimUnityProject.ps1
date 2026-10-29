param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [string]$Game = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$gamePath = [IO.Path]::GetFullPath($Game)
$project = [IO.Path]::GetFullPath($Destination)
$data = Join-Path $gamePath 'valheim_Data'
$managed = Join-Path $data 'Managed'
$manifest = Join-Path $data 'StreamingAssets\SoftRef\manifest_extended'
$bundles = Join-Path $data 'StreamingAssets\SoftRef\Bundles'
$native = Join-Path $data 'Plugins\x86_64'

foreach ($file in @($Unity, $manifest, (Join-Path $data 'ScriptingAssemblies.json'))) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing: $file" }
}
foreach ($folder in @($managed, $bundles)) {
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { throw "Missing: $folder" }
}
if ((Test-Path -LiteralPath $project) -and
    (Get-ChildItem -LiteralPath $project -Force | Select-Object -First 1)) {
    throw "Destination must be empty: $project"
}
if ([IO.Path]::GetPathRoot($project) -ne [IO.Path]::GetPathRoot($managed)) {
    throw 'Project and game must be on the same volume for hard links without copies.'
}

$createLog = Join-Path $env:TEMP ('ValheimUnity-create-' + [guid]::NewGuid().ToString('N') + '.log')
$args = '-batchmode -nographics -quit -createProject "' + $project +
    '" -logFile "' + $createLog + '"'
$run = Start-Process -FilePath $Unity -ArgumentList $args -WindowStyle Hidden -PassThru -Wait
if ($run.ExitCode -ne 0) { throw "Unity project creation failed: $createLog" }

$pluginFolder = Join-Path $project 'Assets\Plugins\Valheim'
New-Item -ItemType Directory -Path $pluginFolder -Force | Out-Null
$linked = New-Object System.Collections.Generic.List[string]
foreach ($dll in Get-ChildItem -LiteralPath $managed -Filter '*.dll' -File) {
    if ($dll.Name -match '^(System\.|mscorlib|netstandard|UnityEngine\.)') {
        if ($dll.Name -notin @('UnityEngine.UI.dll',
            'UnityEngine.SpatialTracking.dll', 'UnityEngine.XR.LegacyInputHelpers.dll')) {
            continue
        }
    }
    $target = Join-Path $pluginFolder $dll.Name
    New-Item -ItemType HardLink -Path $target -Target $dll.FullName | Out-Null
    $linked.Add($dll.Name)
}

$nativeFolder = Join-Path $project 'ValheimDependencies\Native'
$nativeLinks = New-Object System.Collections.Generic.List[string]
if (Test-Path -LiteralPath $native) {
    New-Item -ItemType Directory -Path $nativeFolder -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $native -Filter '*.dll' -File) {
        New-Item -ItemType HardLink -Path (Join-Path $nativeFolder $file.Name) `
            -Target $file.FullName | Out-Null
        $nativeLinks.Add($file.Name)
    }
}

$entries = New-Object System.Collections.Generic.List[object]
$bundleId = ''
foreach ($line in [IO.File]::ReadLines($manifest)) {
    $value = $line.Trim()
    if ($value.StartsWith('bundle: ')) { $bundleId = $value.Substring(8) }
    if ($value -notmatch '^path in bundle: (.+\.shader)$') { continue }
    $path = $Matches[1]
    $bundleFile = Join-Path $bundles $bundleId
    if (-not (Test-Path -LiteralPath $bundleFile -PathType Leaf)) {
        throw "Shader bundle missing: $bundleFile"
    }
    $entries.Add([ordered]@{ asset = $path; bundle = $bundleId })
}
$index = [ordered]@{
    gameData = $data
    unityVersion = '6000.0.75f1'
    managedHardLinks = $linked.ToArray()
    nativeHardLinks = $nativeLinks.ToArray()
    compiledShaders = $entries.ToArray()
}
$settings = Join-Path $project 'ValheimDependencies'
New-Item -ItemType Directory -Path $settings -Force | Out-Null
$index | ConvertTo-Json -Depth 6 | Set-Content `
    -LiteralPath (Join-Path $settings 'index.json') -Encoding UTF8

$editor = Join-Path $project 'Assets\Editor'
New-Item -ItemType Directory -Path $editor -Force | Out-Null
$librarySource = Join-Path $PSScriptRoot 'ValheimShaderLibrary.cs'
$bundleSource = Join-Path $PSScriptRoot 'ValheimBundleLibrary.cs'
$linkedPrefabSource = Join-Path $PSScriptRoot 'ValheimLinkedPrefab.cs'
$menuSource = Join-Path $PSScriptRoot 'ValheimShaderMenu.cs'
$prefabSource = Join-Path $PSScriptRoot 'ValheimPrefabMenu.cs'
$browserSource = Join-Path $PSScriptRoot 'ValheimPrefabBrowser.cs'
foreach ($source in @($bundleSource, $librarySource, $linkedPrefabSource, $menuSource, $prefabSource, $browserSource)) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Missing companion script: $source"
    }
}
Copy-Item -LiteralPath $librarySource -Destination (Join-Path $project 'Assets\ValheimShaderLibrary.cs')
Copy-Item -LiteralPath $bundleSource -Destination (Join-Path $project 'Assets\ValheimBundleLibrary.cs')
Copy-Item -LiteralPath $linkedPrefabSource -Destination (Join-Path $project 'Assets\ValheimLinkedPrefab.cs')
Copy-Item -LiteralPath $menuSource -Destination (Join-Path $editor 'ValheimShaderMenu.cs')
Copy-Item -LiteralPath $prefabSource -Destination (Join-Path $editor 'ValheimPrefabMenu.cs')
Copy-Item -LiteralPath $browserSource -Destination (Join-Path $editor 'ValheimPrefabBrowser.cs')

$validateLog = Join-Path $env:TEMP ('ValheimUnity-validate-' + [guid]::NewGuid().ToString('N') + '.log')
$args = '-batchmode -nographics -quit -projectPath "' + $project +
    '" -logFile "' + $validateLog + '"'
$run = Start-Process -FilePath $Unity -ArgumentList $args -WindowStyle Hidden -PassThru -Wait
if ($run.ExitCode -ne 0) { throw "Unity import failed: $validateLog" }
$errors = Select-String -LiteralPath $validateLog -Pattern @(
    'error CS\d+', 'Assembly .* will not be loaded due to errors:', 'Shader error')
if ($errors) { throw "Unity reported dependency errors: $validateLog" }

Write-Output "Project: $project"
Write-Output "Managed DLL hard links: $($linked.Count)"
Write-Output "Native DLL hard links: $($nativeLinks.Count)"
Write-Output "Game shaders indexed: $($entries.Count)"
Write-Output "Unity validation log: $validateLog"
