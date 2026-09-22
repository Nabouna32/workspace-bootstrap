[CmdletBinding()]
param()

$ErrorActionPreference="Stop"
$root=Split-Path $PSScriptRoot -Parent
$windowsRoot=Join-Path $root "bootstrap\windows"
$componentsRoot=Join-Path $windowsRoot "components"

. (Join-Path $windowsRoot "engine\Component.ps1")

$catalog=@(Get-ComponentCatalog $componentsRoot)
$map=@{}
$errors=[System.Collections.Generic.List[string]]::new()
$validPolicies=@("latest-stable","latest-stable-compatible","pinned-compatible","stable-compatible","minimum","platform")
$validChecks=@("command","command-version","winget-package","file-any","wsl-distro","vs-required")
$profileIds=@{}

# Validate every declarative component manifest.
$manifestFiles=@(Get-ChildItem $componentsRoot -Filter "component.json" -Recurse -File)
$manifestIds=@{}
foreach($manifestFile in $manifestFiles){
    try {
        $manifest=Get-Content $manifestFile.FullName -Raw | ConvertFrom-Json
    } catch {
        [void]$errors.Add("Manifest $($manifestFile.FullName): invalid JSON: $($_.Exception.Message)")
        continue
    }

    $id=[string]$manifest.id
    if([string]::IsNullOrWhiteSpace($id)){
        [void]$errors.Add("Manifest $($manifestFile.FullName): missing id.")
        continue
    }
    if($id -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$'){
        [void]$errors.Add("${id}: id must use lowercase kebab-case.")
    }
    if($manifestIds.ContainsKey($id)){
        [void]$errors.Add("Duplicate manifest id: $id")
    } else {
        $manifestIds[$id]=$manifestFile.FullName
    }

    $platforms=@($manifest.platforms)
    if($platforms.Count -eq 0){
        [void]$errors.Add("${id}: platforms must contain at least one target.")
    } elseif(@($platforms | Where-Object {$_ -notin @("windows","wsl","linux")}).Count -gt 0){
        [void]$errors.Add("${id}: manifest contains an unsupported platform.")
    }

    $installer=[string]$manifest.installer
    if([string]::IsNullOrWhiteSpace($installer)){
        [void]$errors.Add("${id}: manifest installer is required.")
    } elseif([System.IO.Path]::IsPathRooted($installer)){
        [void]$errors.Add("${id}: installer must be repository-relative.")
    }

    $dependencies=@($manifest.dependencies)
    if(@($dependencies | Where-Object {$_ -eq $id}).Count -gt 0){
        [void]$errors.Add("${id}: component cannot depend on itself.")
    }
    if(@($dependencies | Sort-Object -Unique).Count -ne $dependencies.Count){
        [void]$errors.Add("${id}: duplicate dependency declared.")
    }

    $manifestProfiles=@($manifest.profiles)
    if(@($manifestProfiles | Sort-Object -Unique).Count -ne $manifestProfiles.Count){
        [void]$errors.Add("${id}: duplicate profile declared.")
    }
}

foreach($component in $catalog){
    $id=[string]$component.id
    if([string]::IsNullOrWhiteSpace($id)){
        [void]$errors.Add("Component without id.")
        continue
    }
    if($map.ContainsKey($id)){
        [void]$errors.Add("Duplicate component id: $id")
    } else {
        $map[$id]=$component
    }

    if([string]::IsNullOrWhiteSpace([string]$component.installer)){
        [void]$errors.Add("${id}: missing installer.")
    } elseif([string]$component.installer -eq "Install-WingetPackage.ps1"){
        $parameters=@($component.installerParameters)
        $packageId=[string]$component.packageId
        if([string]::IsNullOrWhiteSpace($packageId)){
            [void]$errors.Add("${id}: shared WinGet installer requires packageId.")
        } elseif($parameters.Count -ne 2 -or [string]$parameters[0] -ne "-PackageId" -or [string]$parameters[1] -ne $packageId){
            [void]$errors.Add("${id}: shared WinGet installer parameters must be '-PackageId', packageId.")
        }
    }

    $installerPath=Join-Path $windowsRoot ([string]$component.installer)
    if(-not (Test-Path -LiteralPath $installerPath -PathType Leaf)){
        [void]$errors.Add("${id}: installer not found: $installerPath")
    }

    if($component.PSObject.Properties.Name -contains "installerParameters"){
        if($component.installerParameters -isnot [System.Array]){
            [void]$errors.Add("${id}: installerParameters must be an array.")
        }
        foreach($parameter in @($component.installerParameters)){
            if($null -eq $parameter -or [string]::IsNullOrWhiteSpace([string]$parameter)){
                [void]$errors.Add("${id}: installerParameters contains an empty value.")
            }
        }
    }

    if([string]$component.versionPolicy -notin $validPolicies){
        [void]$errors.Add("${id}: unsupported versionPolicy '$($component.versionPolicy)'.")
    }

    foreach($platform in @($component.platforms)){
        if($platform -notin @("windows","wsl","linux")){
            [void]$errors.Add("${id}: unsupported platform '$platform'.")
        }
    }

    foreach($requirement in @(if($component.PSObject.Properties.Name -contains "prerequisites"){$component.prerequisites}else{@()})){
        if([string]$requirement.type -notin @("admin","windows-build","command","component","file")){
            [void]$errors.Add("${id}: unknown prerequisite type '$($requirement.type)'.")
        }
    }

    foreach($check in @(if($component.PSObject.Properties.Name -contains "checks"){$component.checks}else{@()})){
        if([string]$check.type -notin $validChecks){
            [void]$errors.Add("${id}: unknown check type '$($check.type)'.")
        }
    }
    foreach($check in @(if($component.PSObject.Properties.Name -contains "configurationChecks"){$component.configurationChecks}else{@()})){
        if([string]$check.type -notin $validChecks){
            [void]$errors.Add("${id}: unknown configuration check type '$($check.type)'.")
        }
    }
}

# Cross-check catalog/manifests and dependency references.
foreach($manifestId in @($manifestIds.Keys)){
    $manifest=Get-Content $manifestIds[$manifestId] -Raw | ConvertFrom-Json
    if(@($manifest.platforms) -contains "windows" -and -not $map.ContainsKey($manifestId)){
        [void]$errors.Add("Windows component manifest '$manifestId' is missing from catalog.json.")
    }
}
foreach($catalogId in @($map.Keys)){
    if(-not $manifestIds.ContainsKey([string]$catalogId)){
        [void]$errors.Add("Catalog component '$catalogId' has no component.json manifest.")
    }
}
foreach($component in $catalog){
    foreach($dependency in @($component.dependencies)){
        if($dependency -and -not $map.ContainsKey([string]$dependency)){
            [void]$errors.Add("$($component.id): unknown dependency '$dependency'.")
        }
    }
}

$forbiddenWindowsComponents=@("windows-sdk","android-sdk","android-emulator")
foreach($forbidden in $forbiddenWindowsComponents){
    if($map.ContainsKey($forbidden)){
        [void]$errors.Add("Windows catalog must not own WSL-only component '$forbidden'.")
    }
}

# Visual Studio Build Tools owns the Windows SDK.
$vsConfigPath=Join-Path $root "config\windows\buildtools.vsconfig"
if(-not (Test-Path -LiteralPath $vsConfigPath -PathType Leaf)){
    [void]$errors.Add("Visual Studio Build Tools config is missing: $vsConfigPath")
} else {
    $vsConfig=Get-Content $vsConfigPath -Raw | ConvertFrom-Json
    $requiredVsComponents=@(
        "Microsoft.VisualStudio.Workload.VCTools",
        "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
        "Microsoft.VisualStudio.Component.VC.CMake.Project",
        "Microsoft.VisualStudio.Component.Windows11SDK.26100"
    )
    foreach($requiredVsComponent in $requiredVsComponents){
        if($vsConfig.components -notcontains $requiredVsComponent){
            [void]$errors.Add("Visual Studio config is missing required component '$requiredVsComponent'.")
        }
    }
}

$profilesRoot=Join-Path $windowsRoot "profiles"
foreach($profileFile in @(Get-ChildItem $profilesRoot -Filter "*.json" -File)){
    $profile=Get-Content $profileFile.FullName -Raw | ConvertFrom-Json
    $id=[string]$profile.id
    if([string]::IsNullOrWhiteSpace($id)){
        [void]$errors.Add("Profile $($profileFile.Name): missing id.")
    } elseif($profileIds.ContainsKey($id)){
        [void]$errors.Add("Duplicate profile id: $id")
    } else {
        $profileIds[$id]=$true
    }
    if(-not $profile.components){
        [void]$errors.Add("Profile $($profileFile.Name): no components.")
    }
    foreach($componentId in @($profile.components)){
        if($componentId -and -not $map.ContainsKey([string]$componentId)){
            [void]$errors.Add("Profile ${id}: unknown component '$componentId'.")
        }
    }
}

foreach($component in $catalog){
    foreach($profileId in @($component.profiles)){
        if($profileId -and -not $profileIds.ContainsKey([string]$profileId)){
            [void]$errors.Add("$($component.id): unknown profile '$profileId'.")
        }
    }
}

# Exercise dependency resolution failure paths.
try {
    [void](Resolve-ComponentPlan $catalog @("__unknown__"))
    [void]$errors.Add("Dependency resolver accepted an unknown component.")
} catch {}

try {
    $cycleA=[pscustomobject]@{id="__cycle-a";dependencies=@("__cycle-b")}
    $cycleB=[pscustomobject]@{id="__cycle-b";dependencies=@("__cycle-a")}
    [void](Resolve-ComponentPlan @($cycleA,$cycleB) @("__cycle-a"))
    [void]$errors.Add("Dependency resolver accepted a dependency cycle.")
} catch {}

if($errors.Count){
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Windows component catalog validation: PASS"
Write-Host "Components: $($catalog.Count)"
Write-Host "Profiles: $(@(Get-ChildItem $profilesRoot -Filter '*.json' -File).Count)"
exit 0
