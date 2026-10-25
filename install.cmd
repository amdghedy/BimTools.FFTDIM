@echo off
setlocal
REM ---------------------------------------------------------------
REM FFTDIM installer — copies the built add-in into the per-user
REM Revit 2020 add-ins folder. Build the solution in Release first:
REM   dotnet build FFDimAddin2020.sln -c Release
REM ---------------------------------------------------------------

set REVIT_VERSION=2020
set ADDIN_DIR=%APPDATA%\Autodesk\Revit\Addins\%REVIT_VERSION%
set DLL_SRC=%~dp0FFDimAddin2020\bin\Release\FFDimAddin2020.dll
set MANIFEST_SRC=%~dp0FFDimAddin2020\FFDimAddin2020.addin

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
echo Restart Revit to load the add-in. The tools appear on the "FF Tools" tab.
endlocal
