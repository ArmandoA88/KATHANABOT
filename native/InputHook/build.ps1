[CmdletBinding()]
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$installation) { throw 'MSVC x64 build tools are required.' }
$vcvars = Join-Path $installation 'VC/Auxiliary/Build/vcvars64.bat'
$output = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$batch = Join-Path $output 'compile.cmd'
$lines = @(
    '@echo off',
    "call `"$vcvars`" >nul",
    "cd /d `"$output`"",
    "cl /nologo /std:c++17 /EHsc /W4 /O2 /MT /guard:cf /LD `"$PSScriptRoot/InputHook.cpp`" /link /OUT:KathanaInputHook.dll /guard:cf /DYNAMICBASE /NXCOMPAT /CETCOMPAT user32.lib",
    'if errorlevel 1 exit /b 1'
)
if ($Test) {
    $lines += "cl /nologo /std:c++17 /EHsc /W4 /O2 /MT `"$PSScriptRoot/HookTest.cpp`" /link /OUT:HookTest.exe user32.lib"
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookTest.exe'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += "cl /nologo /std:c++17 /W4 /O2 /GS- /LD `"$PSScriptRoot/HookMarker.cpp`" /link /OUT:HookMarker.dll /NODEFAULTLIB /ENTRY:DllMain /DYNAMICBASE /NXCOMPAT /CETCOMPAT user32.lib kernel32.lib"
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += "cl /nologo /std:c++17 /EHsc /W4 /O2 /MT /guard:cf `"$PSScriptRoot/HookLoadProbe.cpp`" /link /OUT:HookLoadProbe.exe /guard:cf /DYNAMICBASE /NXCOMPAT /CETCOMPAT user32.lib advapi32.lib"
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'link /nologo HookLoadProbe.obj /OUT:HookLoadProbeGui.exe /SUBSYSTEM:WINDOWS /ENTRY:wmainCRTStartup /guard:cf /DYNAMICBASE /NXCOMPAT /CETCOMPAT user32.lib advapi32.lib'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test get'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test call'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test call marker normal'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test return marker normal'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test call normal'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test event marker normal'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test event normal'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test cbt marker normal'
    $lines += 'if errorlevel 1 exit /b 1'
    $lines += 'HookLoadProbe.exe --self-test debug marker normal'
    $lines += 'if errorlevel 1 exit /b 1'
}
Set-Content -LiteralPath $batch -Value $lines -Encoding ASCII
& $env:ComSpec /d /c $batch
if ($LASTEXITCODE -ne 0) { throw 'Native input hook build/test failed.' }
if ($Test) {
    $guiLog = Join-Path $output 'gui-self-test.stdout.txt'
    $guiErrors = Join-Path $output 'gui-self-test.stderr.txt'
    $guiTest = Start-Process -FilePath (Join-Path $output 'HookLoadProbeGui.exe') -ArgumentList '--self-test','event','marker','normal' -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $guiLog -RedirectStandardError $guiErrors
    Get-Content -LiteralPath $guiLog
    if ($guiTest.ExitCode -ne 0) { Get-Content -LiteralPath $guiErrors; throw 'GUI hook loader self-test failed.' }
}
