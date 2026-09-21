@echo off
rem Builds ProgressionCheat.dll straight into BepInEx\plugins.
rem Uses the .NET Framework compiler that ships with Windows - no SDK needed.
setlocal
set GAME=%~dp0..
set MANAGED=%GAME%\valheim_Data\Managed
set CORE=%GAME%\BepInEx\core
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo Could not find the C# compiler at "%CSC%".
    exit /b 1
)

if not exist "%GAME%\BepInEx\plugins" mkdir "%GAME%\BepInEx\plugins"

"%CSC%" -nologo -noconfig -nostdlib+ -target:library -optimize+ ^
    -out:"%GAME%\BepInEx\plugins\ProgressionCheat.dll" ^
    -r:"%MANAGED%\mscorlib.dll" ^
    -r:"%MANAGED%\System.dll" ^
    -r:"%MANAGED%\System.Core.dll" ^
    -r:"%MANAGED%\netstandard.dll" ^
    -r:"%MANAGED%\UnityEngine.dll" ^
    -r:"%MANAGED%\UnityEngine.CoreModule.dll" ^
    -r:"%MANAGED%\UnityEngine.IMGUIModule.dll" ^
    -r:"%MANAGED%\UnityEngine.TextRenderingModule.dll" ^
    -r:"%MANAGED%\Unity.InputSystem.dll" ^
    -r:"%MANAGED%\assembly_valheim.dll" ^
    -r:"%MANAGED%\assembly_utils.dll" ^
    -r:"%MANAGED%\assembly_guiutils.dll" ^
    -r:"%MANAGED%\SoftReferenceableAssets.dll" ^
    -r:"%CORE%\BepInEx.dll" ^
    -r:"%CORE%\0Harmony.dll" ^
    "%~dp0*.cs"

if errorlevel 1 (
    echo Build FAILED.
    exit /b 1
)

echo Build OK -^> %GAME%\BepInEx\plugins\ProgressionCheat.dll
endlocal
