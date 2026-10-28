param(
    [ValidateSet('doctor', 'open', 'editor', 'create-emulator', 'emulator', 'build', 'install', 'logcat', 'register')]
    [string]$Action = 'doctor',
    [string]$Device
)

$ErrorActionPreference = 'Stop'
$projectRootPath = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRootPath
$unityVersion = '6000.3.11f1'
$unityEditorRoot = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$unityVersion/Editor"
$unityEditor = Join-Path $unityEditorRoot 'Unity.exe'
$androidPlayerRoot = Join-Path $unityEditorRoot 'Data/PlaybackEngines/AndroidPlayer'
$env:ANDROID_HOME = Join-Path $androidPlayerRoot 'SDK'
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:JAVA_HOME = Join-Path $androidPlayerRoot 'OpenJDK'
$env:PICO_HOME = Join-Path $projectRootPath '.tools/PICO/sdk'
$env:PATH = (Join-Path $projectRootPath '.tools/unity/bin') + ';' +
    (Join-Path $env:ANDROID_HOME 'platform-tools') + ';' +
    (Join-Path $env:JAVA_HOME 'bin') + ';' + $env:PATH
$adbPath = Join-Path $env:ANDROID_HOME 'platform-tools/adb.exe'
$targetArgs = @()
if ($Device) { $targetArgs = @('-s', $Device) }
New-Item -ItemType Directory -Force -Path 'Logs' | Out-Null

function Invoke-Checked {
    param([string]$Program, [string[]]$CommandArgs)
    & $Program @CommandArgs
    if ($LASTEXITCODE -ne 0) { throw "$Program exited with code $LASTEXITCODE" }
}

switch ($Action) {
    'doctor' {
        Write-Host "Project: $projectRootPath"
        Write-Host "PICO SDK: $env:PICO_HOME"
        $requiredPaths = @($unityEditor, $adbPath,
            (Join-Path $androidPlayerRoot 'NDK/source.properties'),
            (Join-Path $env:JAVA_HOME 'bin/java.exe'),
            (Join-Path $env:PICO_HOME '6.1/editor/SpatialEditor/Spatial Editor.exe'),
            (Join-Path $env:PICO_HOME '6.1/editor/SpatialEditor/runtime/python/bin/python.exe'),
            (Join-Path $env:PICO_HOME '6.1/emulator/emulator.exe'),
            (Join-Path $env:PICO_HOME '6.1/emulator/system-images/system-images/system.img'))
        foreach ($requiredPath in $requiredPaths) {
            if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Missing: $requiredPath" }
            Write-Host "OK: $requiredPath"
        }
        Invoke-Checked 'unity.exe' @('editors', '--installed')
        Invoke-Checked (Join-Path $env:PICO_HOME '6.1/emulator/emulator-check.exe') @('accel')
        $projectConfig = Get-Content -Raw -LiteralPath '.pico-cli/config.json' | ConvertFrom-Json
        if (-not $projectConfig.pico_unity_init_completed) {
            Write-Warning 'Unity project initialization is pending. See SETUP.txt for the licensing and first-import steps.'
        }
    }
    'register' { Invoke-Checked 'unity.exe' @('projects', 'add', $projectRootPath) }
    'open' {
        $launchArgs = '-projectPath "' + $projectRootPath + '" -buildTarget Android -logFile "' +
            (Join-Path $projectRootPath 'Logs/UnityEditor.log') + '"'
        Start-Process -FilePath $unityEditor -ArgumentList $launchArgs -WindowStyle Normal
    }
    'editor' {
        $editor = Join-Path $env:PICO_HOME '6.1/editor/SpatialEditor/Spatial Editor.exe'
        if (-not (Test-Path -LiteralPath $editor)) {
            $editor = Join-Path $env:PICO_HOME '6.1/editor/Spatial Editor.exe'
        }
        if (-not (Test-Path -LiteralPath $editor)) { throw 'Spatial Editor is not installed. See SETUP.txt.' }
        Start-Process -FilePath $editor -WindowStyle Normal
    }
    'create-emulator' { Invoke-Checked 'pico-cli.cmd' @('emulator', 'create', '--source', 'global', '--yes') }
    'emulator' { Invoke-Checked 'pico-cli.cmd' @('emulator', 'start', '--source', 'global', '--yes') }
    'build' {
        if (Test-Path -LiteralPath 'Temp/UnityLockfile') {
            throw 'Close this project in Unity, or use PICO > Project > Build Development APK in the Editor.'
        }
        Invoke-Checked $unityEditor @('-batchmode', '-quit', '-projectPath', $projectRootPath,
            '-buildTarget', 'Android', '-executeMethod', 'PicoDevelopmentBuild.Build',
            '-logFile', (Join-Path $projectRootPath 'Logs/AndroidBuild.log'))
    }
    'install' {
        $apkPath = Join-Path $projectRootPath 'Builds/Android/spatial-toolkit.apk'
        if (-not (Test-Path -LiteralPath $apkPath)) { throw 'Build the development APK first.' }
        Invoke-Checked $adbPath ($targetArgs + @('install', '-r', $apkPath))
        Invoke-Checked $adbPath ($targetArgs + @('shell', 'monkey', '-p', 'com.example.spatialtoolkit',
            '-c', 'android.intent.category.LAUNCHER', '1'))
    }
    'logcat' { Invoke-Checked $adbPath ($targetArgs + @('logcat', '-v', 'time', 'Unity:D', 'AndroidRuntime:E', '*:S')) }
}
