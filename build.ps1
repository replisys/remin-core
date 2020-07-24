[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]
    $IsoPath,

    [Parameter(Mandatory = $true)]
    [string]
    $SaveRoot,

    [Parameter(Mandatory = $true)]
    [string]
    $WorkRoot
)

$ErrorActionPreference = 'Stop'

$env:PSModulePath += ";$PSScriptRoot\libs\eps"
Import-Module EPS

$SourceBuild = "10.0.19041.1"
$Arch = "amd64"

[Reflection.Assembly]::LoadFile("$PSScriptRoot\Interop.Cmi20.dll") | Out-Null
[Reflection.Assembly]::LoadFile("$PSScriptRoot\tools\borderline\bin\x64\Release\netcoreapp3.1\publish\borderline.dll") | Out-Null

$cmiFactory = New-Object Cmi20.CmiFactoryClass
$serializer = $cmiFactory.CreateObject([Cmi20.CmiObjectType]::cmiSerializer)

function Get-Keyform {
    param (
        [Parameter()]
        [object]
        $Assembly
    )

    return [string]([borderline.SxSExtract]::CreateAssemblyID(
        $Assembly.Id.Name,
        $Assembly.Id.PublicKeyToken,
        $Assembly.Id.ProcessorArchitecture,
        $Assembly.Id.Language,
        $Assembly.Id.Version.Value,
        $Assembly.Id.VersionScope
    ))
}

New-Item -Force -ItemType Directory $WorkRoot\Packages | Out-Null

# https://stackoverflow.com/a/43481862
function Naive-Convert-HashToByteArray {
    [CmdletBinding()]
    Param ( [Parameter(Mandatory = $True, ValueFromPipeline = $True)] [String] $String )
    $String -split '([A-F0-9]{2})' | foreach-object { if ($_) {[System.Convert]::ToByte($_,16)}}
}

function Convert-Manifest {
    param (
        [Parameter()]
        [string]
        $Manifest
    )

    $assembly = $serializer.Deserialize($Manifest)
    $assembly.Id.Version.Value = $SourceBuild

    if ($assembly.Id.Name -eq "Microsoft-OneCore-ReverseForwarders") {
        #$assembly.Id.Version.Value = "10.0.19041.264"
    }

    $keyform = (Get-Keyform $assembly)
    $fileRoot = "$WorkRoot\Packages\$keyform"

    $environment = $cmiFactory.CreateObject([Cmi20.CmiObjectType]::cmiEnvironment)
    $environment.Item("`$(runtime.windows)") = "Windows\"
    $environment.Item("`$(runtime.system32)") = "Windows\System32\"

    foreach ($file in $assembly.Files) {
        New-Item -Force -ItemType Directory $fileRoot | Out-Null

        $destPath = $environment.Expand($file.DestinationPath)
        $destName = $file.DestinationName
        $filePath = "$PSScriptRoot\pkgs\src\$($assembly.Id.Name)\$destName"

        if (!(Test-Path $filePath)) {
            $filePath = "$WorkRoot\Docker\Files\$destPath\$destName"
            if (!(Test-Path $filePath)) {
                $filePath = "$WorkRoot\SourceOS\1\$destPath\$destName"

                if (!(Test-Path $filePath)) {
                    throw "Can't find $destPath"
                }
            }
        }

        $hash = (Get-FileHash -Algorithm SHA256 -Path $filePath).Hash
        $file.CustomInformations.Item(0).SubElements.Item(2).InnerContent = [System.Convert]::ToBase64String((Naive-Convert-HashToByteArray $hash))

        $srcName = $file.SourceName
        Copy-Item -Force $filePath -Destination $fileRoot\$srcName | Out-Null
    }

    foreach ($dependency in $assembly.Dependencies) {
        $dependency.SubObject.DependencyId.Version.Value = $SourceBuild

        if ($dependency.SubObject.DependencyId.Name -eq "Microsoft-OneCore-ReverseForwarders") {
            #$dependency.SubObject.DependencyId.Version.Value = "10.0.19041.264"
        }
    }

    $outPath = "$WorkRoot\Packages\$keyform.manifest"
    $serializer.Serialize($assembly, $outPath)

    # rewrite as UTF8
    $a = (Get-Content $outPath) -replace "encoding=`"UTF-16", "encoding=`"UTF-8"
    $a | Out-File -Encoding utf8NoBOM $outPath
}

if (Test-Path $WorkRoot) {
    #Remove-Item -Recurse -Force $WorkRoot
}

New-Item -Force -ItemType Directory $WorkRoot

$OSPackages = @(
    "Microsoft-Windows-ServicingStack-OneCore-Package",
    "Microsoft-Windows-Servicing-Core-Package",
    "Microsoft-Windows-ServicingStack-OneCore-Package",
    "Microsoft-Windows-ServicingStack-OneCoreadmin-Package",
    <#"Microsoft-Windows-ServicingStack-OneCore-Package",
    "Microsoft-Windows-Servicing-Core-Package",
    "Microsoft-Windows-ServicingStack-OneCore-Package",
    "Microsoft-Windows-ServicingStack-OneCoreadmin-Package",

    "Microsoft-Windows-Foundation-Group-merged-Package",
    "en-US\Microsoft-Windows-Foundation-Group-merged-Package",
    "Microsoft-Windows-BootEnvironment-BootManagers-Package",
    "en-US\Microsoft-Windows-BootEnvironment-BootManagers-Package",
    "Microsoft-Windows-Common-DriverClasses-Package",
    "en-US\Microsoft-Windows-Common-DriverClasses-Package",
    "Microsoft-Windows-Server-Minimal-Drivers-Package",
    "en-US\Microsoft-Windows-Server-Minimal-Drivers-Package",
    "Microsoft-Windows-ServerCore-Drivers-Package",
    "en-US\Microsoft-Windows-ServerCore-Drivers-Package",
    "Microsoft-Windows-CoreSystem-merged-Package",
    "en-US\Microsoft-Windows-CoreSystem-merged-Package",
    "Microsoft-OneCore-Wer-merged-Package",
    "en-US\Microsoft-OneCore-Wer-merged-Package",
    "Microsoft-Windows-Online-Setup-State-Full-Package",
    "en-US\Microsoft-Windows-Online-Setup-State-Full-Package",
    "Microsoft-OneCore-CoreSystem-Core-Package",
    "en-US\Microsoft-OneCore-CoreSystem-Core-Package",
    "Microsoft-OneCore-EnterpriseNetworking-Package",
    "en-US\Microsoft-OneCore-EnterpriseNetworking-Package",
    "Microsoft-OneCore-Pnp-Full-Package",
    "en-US\Microsoft-OneCore-Pnp-Full-Package",
    "Microsoft-Windows-Network-Security-Core-Package",
    "en-US\Microsoft-Windows-Network-Security-Core-Package",
    "Microsoft-Windows-CoreSystem-RemoteFS-Package",
    "en-US\Microsoft-Windows-CoreSystem-RemoteFS-Package",
    "Microsoft-Windows-RemoteFS-Legacy-Package",
    "Microsoft-OneCore-Console-Host-Package",
    "en-US\Microsoft-OneCore-Console-Host-Package",
    "Microsoft-NanoServer-Edition-Core-Package",
    "en-US\Microsoft-NanoServer-Edition-Core-Package",
    "Microsoft-Windows-HyperV-Guest-Package",
    "en-US\Microsoft-Windows-HyperV-Guest-Package",
    "Remin-SKU-Foundation-Package",
    "en-US\Remin-SKU-Foundation-Package"#>

    "Microsoft-Windows-Common-DriverClasses-Package",
    "en-US\Microsoft-Windows-Common-DriverClasses-Package",
    "Microsoft-Windows-Server-Minimal-Drivers-Package",
    "en-US\Microsoft-Windows-Server-Minimal-Drivers-Package",
    "Microsoft-Windows-ServerCore-Drivers-Package",
    "en-US\Microsoft-Windows-ServerCore-Drivers-Package",

    "Microsoft-OneCore-EnterpriseNetworking-Package",
    "en-US\Microsoft-OneCore-EnterpriseNetworking-Package",
    "Microsoft-OneCore-Pnp-Full-Package",
    "en-US\Microsoft-OneCore-Pnp-Full-Package",

    "Microsoft-Windows-CoreSystem-RemoteFS-Package",
    "en-US\Microsoft-Windows-CoreSystem-RemoteFS-Package",
    "Microsoft-Windows-RemoteFS-Legacy-Package",

    "Microsoft-Windows-ServerDatacenterNanoEdition",
    "en-US\Microsoft-Windows-ServerDatacenterNanoEdition-LP",

    "Microsoft-Windows-Online-Setup-State-Full-Package",
    "en-US\Microsoft-Windows-Online-Setup-State-Full-Package",

    "Microsoft-OneCore-Transactions-Package",
    "en-US\Microsoft-OneCore-Transactions-Package",

    "Remin-SKU-Foundation-Package",
    "en-US\Remin-SKU-Foundation-Package",

    "Microsoft-Windows-HyperV-Guest-Package",
    "en-US\Microsoft-Windows-HyperV-Guest-Package"
)

function Resolve-PackageName {
    param (
        [Parameter()]
        [string]
        $PackageName
    )

    $Parts = $PackageName -split '\',2,'SimpleMatch'

    $RootName = $Parts[0]
    $Lang = ""
    if ($Parts.Length -eq 2) {
        $Lang = $Parts[0]
        $RootName = $Parts[1]
    }

    return "$RootName~31bf3856ad364e35~$Arch~$Lang~$SourceBuild"
}

function Get-HasOSPackages {
    foreach ($package in $OSPackages) {
        if (!(Test-Path "$SaveRoot\packages\$SourceBuild\$package.cab")) {
            return $false
        }
    }

    return $true
}

function Get-DockerContainer {
    # TODO: version mapping
    $object = (curl.exe -sL -H "Accept: application/vnd.docker.distribution.manifest.v2+json" `
        https://mcr.microsoft.com/v2/windows/nanoserver/manifests/10.0.19041.264-amd64) | ConvertFrom-Json

    & $env:windir\system32\curl.exe -Lo $WorkRoot\docker.tar.gz $object.layers[0].urls[0]

    New-Item -ItemType Directory -Force $WorkRoot\Docker | Out-Null
    & $env:windir\system32\tar.exe -C $WorkRoot\Docker -xf $WorkRoot\docker.tar.gz
}

function Build-CustomPackages {
    $manifests = (Get-ChildItem $PSScriptRoot\pkgs\remin-sku-foundation-package\*.manifest)

    foreach ($manifest in $manifests) {
        Convert-Manifest $manifest
    }
}

function Build-OSPackages {
    Dismount-DiskImage -ImagePath $IsoPath

    $iso = Mount-DiskImage -ImagePath $IsoPath
    try {
        $isoRoot = ($iso | Get-Volume).DriveLetter + ":"

        New-Item -Force -ItemType Directory $WorkRoot\SourceOS

        #Invoke-Expression "dism /apply-image /imagefile:$isoRoot\sources\install.wim /index:1 /applydir:$WorkRoot\SourceOS"

        # enable
        Invoke-Expression "7z x -o$WorkRoot\SourceOS $isoRoot\sources\install.wim 1"

        foreach ($manifest in Get-ChildItem $WorkRoot\Packages\*.manifest) {
            Copy-Item -Force $manifest.FullName "$WorkRoot\SourceOS\1\Windows\WinSxS\Manifests\${$manifest.Name}"
        }

        foreach ($file in Get-ChildItem -Directory $WorkRoot\Packages\) {
            Copy-Item -Recurse -Force $file.FullName $WorkRoot\SourceOS\1\Windows\WinSxS\
        }

        foreach ($file in Get-ChildItem $PSScriptRoot\pkgs\remin-sku-foundation-package\*.mum) {
            $data = (Invoke-EpsTemplate -Path $file.FullName -Safe -Binding @{ Version = $SourceBuild })
            $data | Out-File -Encoding utf8NoBOM -FilePath "$env:TEMP\manifest.txt"

            $assembly = $serializer.Deserialize("$env:TEMP\manifest.txt")
            $pn = $assembly.Id.Name

            if ($assembly.Id.Language -ne "neutral") {
                $pn = "en-US\$pn"
            }

            $pn = Resolve-PackageName $pn
            $pc = $pn + ".cat"
            $pn += ".mum"
            $data | Out-File -Encoding utf8NoBOM -FilePath $WorkRoot\SourceOS\1\Windows\Servicing\Packages\$pn

            Copy-Item -Force $WorkRoot\SourceOS\1\Windows\Servicing\Packages\Windows-Defender-Server-Core-Group-Package~31bf3856ad364e35~amd64~en-US~$SourceBuild.cat `
                $WorkRoot\SourceOS\1\Windows\Servicing\Packages\$pc
        }

        New-Item -Force -ItemType Directory $SaveRoot
        New-Item -Force -ItemType Directory $SaveRoot\packages
        New-Item -Force -ItemType Directory $SaveRoot\packages\$SourceBuild
        New-Item -Force -ItemType Directory $SaveRoot\packages\$SourceBuild\en-US

        foreach ($Package in $OSPackages) {
            $FileName = Resolve-PackageName $Package
            $FullName = "$WorkRoot\SourceOS\1\Windows\Servicing\Packages\$FileName.mum"

            $FullName | Write-Host
            $OutName = "$SaveRoot\packages\$SourceBuild\$Package.cab"

            if ((Test-Path $FullName) -and !(Test-Path $OutName)) {
                Push-Location $SaveRoot
                Invoke-Expression "dotnet $PSScriptRoot\tools\borderline\bin\x64\Release\netcoreapp3.1\publish\borderline.dll $FullName $WorkRoot\SourceOS\1\Windows $OutName"
                Pop-Location
            }
        }
    } finally {
        Dismount-DiskImage -InputObject $iso
    }
}

if (!(Get-HasOSPackages)) {
    Get-DockerContainer
    Build-CustomPackages
    Build-OSPackages
}

$components = @(
    "Microsoft-Windows-Deployment-Image-Servicing-Management",
    "Microsoft-Windows-PackageManager",
    "Microsoft-Windows-PantherEngine",
    "Microsoft-Windows-Deployment-Image-Servicing-Management-WinProviders",
    "Microsoft-Windows-Deployment-Image-Servicing-Management-API",
    "Microsoft-Windows-Deployment-Image-Servicing-Management-Core",
    "Microsoft-Windows-ServicingStack"
)

$mfDir = "$WorkRoot\servicing\manifests"
New-Item -ItemType Directory -Force $mfDir | Out-Null

foreach ($component in $components) {
    $keyform = [borderline.SxSExtract]::CreateAssemblyID(
        $component,
        "31bf3856ad364e35",
        "amd64",
        "neutral",
        $SourceBuild,
        "nonSxS")

    $manifest = "$WorkRoot\SourceOS\1\Windows\WinSxS\Manifests\$keyform.manifest"
    $tempPath = "$env:TEMP\\temp.manifest"
    [LibSxS.Delta.DeltaAPI]::wcpBasePath = "$PSScriptRoot\tools\borderline\manifest.bin"
    [System.IO.File]::WriteAllBytes($tempPath, [LibSxS.Delta.DeltaAPI]::LoadManifest($manifest))

    $assembly = $serializer.Deserialize($tempPath)

    foreach ($file in $assembly.Files) {
        $source = "$WorkRoot\SourceOS\1\Windows\WinSxS\$keyform\" + $file.SourceName

        Copy-Item -Force $source "$mfDir\${$file.SourceName}"
    }

    # we don't have eventsinstaller, remove it
    [xml]$text = Get-Content $tempPath
    $n = $text.SelectSingleNode("//*[local-name() = 'instrumentation']", $mgr)
    if ($n) {
        $n.ParentNode.RemoveChild($n) | Out-Null
    }
    $text.Save("$mfDir\$component.manifest")
}

Copy-Item -Force $PSScriptRoot\wcp.dll $mfDir\wcp.dll

$dismDeployment = @"
<?xml version="1.0" encoding="utf-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v3" manifestVersion="1.0" copyright="Copyright (c) Microsoft Corporation. All Rights Reserved.">
  <assemblyIdentity name="Microsoft-Windows-CoreSystem-DISM-Deployment" version="$SourceBuild" processorArchitecture="amd64" language="neutral" buildType="release" publicKeyToken="31bf3856ad364e35" versionScope="nonSxS" />
  <deployment xmlns="urn:schemas-microsoft-com:asm.v3" />
  <dependency discoverable="yes">
    <dependentAssembly dependencyType="install">
      <assemblyIdentity name="Microsoft-Windows-Deployment-Image-Servicing-Management" version="$SourceBuild" processorArchitecture="amd64" language="neutral" buildType="release" publicKeyToken="31bf3856ad364e35" versionScope="nonSxS" />
    </dependentAssembly>
  </dependency>
  <dependency discoverable="yes">
    <dependentAssembly dependencyType="install">
      <assemblyIdentity name="Microsoft-Windows-Deployment-Image-Servicing-Management-API" version="$SourceBuild" processorArchitecture="amd64" language="neutral" buildType="release" publicKeyToken="31bf3856ad364e35" versionScope="nonSxS" />
    </dependentAssembly>
  </dependency>
  <!--<dependency discoverable="yes">
    <dependentAssembly dependencyType="install">
      <assemblyIdentity name="Microsoft-Windows-Deployment-Image-Servicing-Management-API-ETW" version="$SourceBuild" processorArchitecture="amd64" language="neutral" buildType="release" publicKeyToken="31bf3856ad364e35" versionScope="nonSxS" />
    </dependentAssembly>
  </dependency>-->
  <dependency discoverable="yes">
    <dependentAssembly dependencyType="install">
      <assemblyIdentity name="Microsoft-Windows-Deployment-Image-Servicing-Management-Core" version="$SourceBuild" processorArchitecture="amd64" language="neutral" buildType="release" publicKeyToken="31bf3856ad364e35" versionScope="nonSxS" />
    </dependentAssembly>
  </dependency>
  <dependency discoverable="yes">
    <dependentAssembly dependencyType="install">
      <assemblyIdentity name="Microsoft-Windows-Deployment-Image-Servicing-Management-WinProviders" version="$SourceBuild" processorArchitecture="amd64" language="neutral" buildType="release" publicKeyToken="31bf3856ad364e35" versionScope="nonSxS" />
    </dependentAssembly>
  </dependency>
</assembly>
"@

$reminDeployment = @"
<?xml version="1.0" encoding="utf-8" standalone="yes"?>

<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v3">
  <assemblyIdentity name="remin-deployment" version="$SourceBuild" processorArchitecture="amd64" language="neutral" publicKeyToken="31bf3856ad364e35" buildType="release" versionScope="nonSxS" />
  <deployment xmlns="urn:schemas-microsoft-com:asm.v3" />
  <dependency>
    <dependentAssembly dependencyType="install">
      <assemblyIdentity name="Microsoft-Windows-ServicingStack" version="$SourceBuild" processorArchitecture="amd64" language="neutral" publicKeyToken="31bf3856ad364e35" buildType="release" versionScope="nonSxS" />
    </dependentAssembly>
  </dependency>
  <dependency>
    <dependentAssembly dependencyType="install">
      <assemblyIdentity name="Microsoft-Windows-PackageManager" version="$SourceBuild" processorArchitecture="amd64" language="neutral" publicKeyToken="31bf3856ad364e35" buildType="release" versionScope="nonSxS" />
    </dependentAssembly>
  </dependency>
  <dependency>
    <dependentAssembly dependencyType="install">
      <assemblyIdentity name="Microsoft-Windows-PantherEngine" version="$SourceBuild" processorArchitecture="amd64" language="neutral" publicKeyToken="31bf3856ad364e35" buildType="release" versionScope="nonSxS" />
    </dependentAssembly>
  </dependency>
</assembly>
"@

$dismDeployment | Out-File -Encoding utf8NoBOM -FilePath "$mfDir\Microsoft-Windows-CoreSystem-DISM-Deployment.manifest"
$reminDeployment | Out-File -Encoding utf8NoBOM -FilePath "$mfDir\remin-deployment.manifest"

if (Test-Path "$SaveRoot\OS.vhdx") {
    Remove-Item -Force "$SaveRoot\OS.vhdx"
}

$vhd = New-VHD -Path "$SaveRoot\OS.vhdx" -SizeBytes 32GB
Mount-DiskImage -ImagePath "$SaveRoot\OS.vhdx"

$pdn = (Get-DiskImage -ImagePath "$SaveRoot\OS.vhdx").Number

try {
    Initialize-Disk -Number $pdn -PartitionStyle GPT
    $sysPart = New-Partition -DiskNumber $pdn -Size 350MB
    $sysPart | Format-Volume -FileSystem FAT32
    $spn = $sysPart.PartitionNumber

    $vol = New-Partition -DiskNumber $pdn -UseMaximumSize
    $vol | Format-Volume -FileSystem NTFS
    $vol | Set-Partition -NewDriveLetter V

    $TargetRoot = "V:"
    
    .\tools\servicing\servicing.exe $mfDir $TargetRoot\

    # needed to let ServicingStack-OneCore-Package succeed first try
    New-Item -ItemType Directory -Force $TargetRoot\Windows\system32\downlevel | Out-Null

    $earlyPkgs = @(
        "Microsoft-Windows-ServicingStack-OneCore-Package",
        "Microsoft-Windows-Servicing-Core-Package",
        #"Microsoft-Windows-ServicingStack-OneCore-Package",
        "Microsoft-Windows-ServicingStack-OneCoreadmin-Package"
    )

    foreach ($package in $earlyPkgs) {
        dism /image:$TargetRoot\ /add-package /packagepath:"$SaveRoot\packages\$SourceBuild\$package.cab"
    }

    $actualPkgs = @(
        <#"Microsoft-Windows-Foundation-Group-merged-Package.cab",
        "en-US\Microsoft-Windows-Foundation-Group-merged-Package.cab",
        "Microsoft-Windows-BootEnvironment-BootManagers-Package.cab",
        "en-US\Microsoft-Windows-BootEnvironment-BootManagers-Package.cab",
        "Microsoft-Windows-Common-DriverClasses-Package.cab",
        "en-US\Microsoft-Windows-Common-DriverClasses-Package.cab",
        "Microsoft-Windows-Server-Minimal-Drivers-Package.cab",
        "en-US\Microsoft-Windows-Server-Minimal-Drivers-Package.cab",
        "Microsoft-Windows-ServerCore-Drivers-Package.cab",
        "en-US\Microsoft-Windows-ServerCore-Drivers-Package.cab",
        "Microsoft-Windows-CoreSystem-merged-Package.cab",
        "en-US\Microsoft-Windows-CoreSystem-merged-Package.cab",
        "Microsoft-OneCore-Wer-merged-Package.cab",
        "en-US\Microsoft-OneCore-Wer-merged-Package.cab",
        "Microsoft-Windows-Online-Setup-State-Full-Package.cab",
        "en-US\Microsoft-Windows-Online-Setup-State-Full-Package.cab",
        "Microsoft-OneCore-CoreSystem-Core-Package.cab",
        "en-US\Microsoft-OneCore-CoreSystem-Core-Package.cab",
        "Microsoft-OneCore-EnterpriseNetworking-Package.cab",
        "en-US\Microsoft-OneCore-EnterpriseNetworking-Package.cab",
        "Microsoft-OneCore-Pnp-Full-Package.cab",
        "en-US\Microsoft-OneCore-Pnp-Full-Package.cab",
        "Microsoft-Windows-Network-Security-Core-Package.cab",
        "en-US\Microsoft-Windows-Network-Security-Core-Package.cab",
        "Microsoft-Windows-CoreSystem-RemoteFS-Package.cab",
        "en-US\Microsoft-Windows-CoreSystem-RemoteFS-Package.cab",
        "Microsoft-Windows-RemoteFS-Legacy-Package.cab",
        "Microsoft-OneCore-Console-Host-Package.cab",
        "en-US\Microsoft-OneCore-Console-Host-Package.cab",
        "Microsoft-NanoServer-Edition-Core-Package.cab",
        "en-US\Microsoft-NanoServer-Edition-Core-Package.cab",#>
        
        #"Microsoft-Windows-ServerDatacenterNanoEdition.cab",
        #"en-US\Microsoft-Windows-ServerDatacenterNanoEdition.cab",
        "Microsoft-Windows-Online-Setup-State-Full-Package.cab",
        "en-US\Microsoft-Windows-Online-Setup-State-Full-Package.cab",

        "Microsoft-Windows-Common-DriverClasses-Package.cab",
        "en-US\Microsoft-Windows-Common-DriverClasses-Package.cab",
        "Microsoft-Windows-Server-Minimal-Drivers-Package.cab",
        "en-US\Microsoft-Windows-Server-Minimal-Drivers-Package.cab",
        "Microsoft-Windows-ServerCore-Drivers-Package.cab",
        "en-US\Microsoft-Windows-ServerCore-Drivers-Package.cab",

        "Microsoft-OneCore-Transactions-Package.cab",
        "en-US\Microsoft-OneCore-Transactions-Package.cab",

        "Microsoft-OneCore-EnterpriseNetworking-Package.cab",
        "en-US\Microsoft-OneCore-EnterpriseNetworking-Package.cab",
        "Microsoft-OneCore-Pnp-Full-Package.cab",
        "en-US\Microsoft-OneCore-Pnp-Full-Package.cab",

        "Microsoft-Windows-CoreSystem-RemoteFS-Package.cab",
        "en-US\Microsoft-Windows-CoreSystem-RemoteFS-Package.cab",
        "Microsoft-Windows-RemoteFS-Legacy-Package.cab",

        "Remin-SKU-Foundation-Package.cab",
        "en-US\Remin-SKU-Foundation-Package.cab",
        "Microsoft-Windows-HyperV-Guest-Package.cab",
        "en-US\Microsoft-Windows-HyperV-Guest-Package.cab"
    )

    Invoke-EpsTemplate -Path $PSScriptRoot\editionpack.xml -Safe -Binding @{ Version = $SourceBuild; SaveRoot = $SaveRoot } `
        | Out-File -Encoding utf8NoBOM -FilePath $WorkRoot\editionpack.xml

    dism /image:$TargetRoot\ /apply-unattend:$WorkRoot\editionpack.xml

    if (!$?) {
        throw "failed to apply edition";
    }

    $string = @()

    foreach ($package in $actualPkgs) {
        $string += '/packagepath:"' + $SaveRoot + '\packages\' + $SourceBuild + '\' + $package + '"'
    }

    & dism /image:$TargetRoot\ /add-package @string

    if (!$?) {
        throw "failed to apply post-edition packages";
    }

    # post-install, we add catroot files
    $cats = @(
        "Microsoft-Windows-ServerCore-SKU-Foundation-merged-Package",
        "Microsoft-OneCore-BootableSKU-merged-Package"
    )

    foreach ($cat in $cats) {
        $pn = Resolve-PackageName $cat
        $pn = "Windows\System32\catroot\{F750E6C3-38EE-11D1-85E5-00C04FC295EE}\$pn.cat"

        $filePath = "$WorkRoot\Docker\Files\$pn"
        if (!(Test-Path $filePath)) {
            $filePath = "$WorkRoot\SourceOS\1\$pn"

            if (!(Test-Path $filePath)) {
                throw "Can't find $destPath"
            }
        }

        $pn = $pn -replace "\.cat", "-RM.cat"

        Copy-Item -Force $filePath $TargetRoot\$pn
    }

    dism /image:$TargetRoot\ /apply-unattend:$PSScriptRoot\unattend.xml

    # SMI/WCM probably doesn't allow removing an entry, so we manually remove kmode
    try {
        reg.exe load HKLM\TempSystem $TargetRoot\Windows\System32\config\SYSTEM

        # pretend we're WTG so we don't try to break firmware boot order
        Set-ItemProperty -Path "HKLM:\TempSystem\ControlSet001\Control" -Name "PortableOperatingSystem" -Value 1 -Type DWord

        Remove-ItemProperty -Path "HKLM:\TempSystem\ControlSet001\Control\Session Manager\SubSystems" -Name "Kmode"
    } finally {
        reg.exe unload HKLM\TempSystem
    }

    # KTM won't load with Nano Server apisetschema (no tm.sys reference), and WCP expects this for servicing normally
    <#try {
        reg.exe load HKLM\TempSoftware $TargetRoot\Windows\System32\config\SOFTWARE
        Set-ItemProperty -Path "HKLM:\TempSoftware\Microsoft\Windows\CurrentVersion\SideBySide" -Name "DisableKernelTransactions" -Value 1 -Type DWord
    } finally {
        reg.exe unload HKLM\TempSoftware
    }#>

    $sysPart | Set-Partition -NewDriveLetter Z
    $sysRoot = "Z:"

    #New-Item -ItemType Directory $TargetRoot\tmp
    #Copy-Item -Force "$env:TEMP\u.msu" $TargetRoot\tmp\u.msu

    #TODO: patched msxml6

    Copy-Item -Force $TargetRoot\windows\system32\boot\winload.efi $TargetRoot\windows\system32
    Copy-Item -Force $TargetRoot\windows\system32\boot\en-us\winload.efi.mui $TargetRoot\windows\system32\en-us

    bcdboot $env:WINDIR /s $sysRoot /f UEFI /p /d
    bcdedit /store $sysRoot\efi\Microsoft\boot\bcd /set '{default}' osdevice hd_partition=$TargetRoot
    bcdedit /store $sysRoot\efi\Microsoft\boot\bcd /set '{default}' device hd_partition=$TargetRoot
    bcdedit /store $sysRoot\efi\Microsoft\boot\bcd /set '{default}' testsigning on
    bcdedit /store $sysRoot\efi\Microsoft\boot\bcd /set '{bootmgr}' device hd_partition=$sysRoot
    bcdedit /store $sysRoot\efi\Microsoft\boot\bcd /set '{default}' debug off
    #bcdedit /store $sysRoot\efi\Microsoft\boot\bcd /dbgsettings net hostip:0.0.0.0 port:50000 key:1.2.3.4
    bcdedit /store $sysRoot\efi\Microsoft\boot\bcd /set '{default}' bootdebug off
    bcdedit /store $sysRoot\efi\Microsoft\boot\bcd /set '{bootmgr}' bootdebug off

    # to be able to load apisetschema forks (so online servicing can work)
    & "$env:WINDIR\system32\curl.exe" -Lo "$WorkRoot\EfiGuard-v1.1.zip" "https://github.com/Mattiwatti/EfiGuard/releases/download/v1.1/EfiGuard-v1.1.zip"
    Push-Location $WorkRoot
    & "$env:WINDIR\system32\tar.exe" -xf "EfiGuard-v1.1.zip"
    & "$env:WINDIR\system32\curl.exe" -Lo "$sysRoot\EFI\boot\bootx64.efi" "https://github.com/tianocore/edk2/blob/edk2-stable201903/ShellBinPkg/UefiShell/X64/Shell.efi?raw=true"

    @"
load fs0:\efi\boot\efiguarddxe.efi
fs0:\efi\microsoft\boot\bootmgfw.efi
"@ | Out-File -Encoding utf8NoBOM "$sysRoot\startup.nsh"

    # this doesn't correctly chainload
    #Copy-Item -Force "EfiGuard-v1.1\EFI\Boot\Loader.efi" "$sysRoot\efi\boot\bootx64.efi"
    Copy-Item -Force "EfiGuard-v1.1\EFI\Boot\EfiGuardDxe.efi" "$sysRoot\efi\boot\EfiGuardDxe.efi"
    Pop-Location

    @"
select disk $pdn
select partition $spn
set id=c12a7328-f81f-11d2-ba4b-00a0c93ec93b
"@ | diskpart.exe
} finally {
    Dismount-DiskImage -ImagePath "$SaveRoot\OS.vhdx"
}