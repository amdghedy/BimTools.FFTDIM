@echo off
setlocal
REM ---------------------------------------------------------------
REM FFTDIM installer — copies the built add-in into the per-user
REM add-ins folder of the selected Revit version.
REM
REM Usage:
REM   install.cmd            install for Revit 2020 (default)
REM   install.cmd 2021       install for Revit 2021
REM   install.cmd 2024       install for Revit 2024
REM Supported versions: 2020 2021 2022 2023 2024
REM
REM The same .NET Framework 4.8 assembly serves every supported
REM version; each version only gets its own copy of the manifest,
REM pointing at the DLL in the version's add-ins folder.
REM
REM Build the solution in Release first:
REM   dotnet build FFDimAddin2020.sln -c Release
REM ---------------------------------------------------------------

set "REVIT_VERSION=%~1"
if "%REVIT_VERSION%"=="" set "REVIT_VERSION=2020"

set "VERSION_OK="
for %%v in (2020 2021 2022 2023 2024) do (
    if "%REVIT_VERSION%"=="%%v" set "VERSION_OK=1"
)
if not defined VERSION_OK (
    echo Unsupported Revit version "%REVIT_VERSION%".
    echo Supported versions: 2020 2021 2022 2023 2024
    echo Usage: install.cmd [2020^|2021^|2022^|2023^|2024]
    exit /b 1
)

set "DLL_SRC=%~dp0FFDimAddin2020\bin\Release\FFDimAddin2020.dll"
set "MANIFEST_SRC=%~dp0FFDimAddin2020\FFDimAddin2020.addin"
set "ADDIN_DIR=%APPDATA%\Autodesk\Revit\Addins\%REVIT_VERSION%"

if not exist "%DLL_SRC%" (
    echo Build output not found:
    echo   %DLL_SRC%
    echo Build the solution in Release configuration first, then re-run this script.
    exit /b 1
)

if not exist "%MANIFEST_SRC%" (
    echo Manifest not found: %MANIFEST_SRC%
    exit /b 1
)

if not exist "%ADDIN_DIR%" mkdir "%ADDIN_DIR%"
if not exist "%ADDIN_DIR%\FFDimAddin2020" mkdir "%ADDIN_DIR%\FFDimAddin2020"

copy /Y "%MANIFEST_SRC%" "%ADDIN_DIR%\FFDimAddin2020.addin" >nul
copy /Y "%DLL_SRC%" "%ADDIN_DIR%\FFDimAddin2020\FFDimAddin2020.dll" >nul

echo.
echo FFTDIM installed for Revit %REVIT_VERSION%:
echo   %ADDIN_DIR%\FFDimAddin2020.addin
echo   %ADDIN_DIR%\FFDimAddin2020\FFDimAddin2020.dll
echo.
echo Restart Revit %REVIT_VERSION% to load the add-in. The tools appear on the "FF Tools" tab.
echo Settings and tolerance rules persist in %%APPDATA%%\BimTools\FFTDIM\ and are shared by all versions.
endlocal
