@echo off
setlocal

for %%I in ("%~dp0") do set "SCRIPT_DIR=%%~fI"
for %%I in ("%SCRIPT_DIR%..") do set "REPOSITORY_ROOT=%%~fI"

set "DOTNET=%USERPROFILE%\scoop\apps\dotnet-sdk\current\dotnet.exe"
if exist "%DOTNET%" goto :dotnet_ready
set "DOTNET=dotnet"
"%DOTNET%" --list-sdks | findstr /r "." >nul
if errorlevel 1 goto :missing_sdk

:dotnet_ready
set "POWERSHELL=pwsh.exe"
where pwsh.exe >nul 2>&1
if errorlevel 1 set "POWERSHELL=powershell.exe"

pushd "%REPOSITORY_ROOT%" >nul
if errorlevel 1 (
    echo Failed to enter the repository root.
    exit /b 1
)

echo Restoring dependencies...
"%DOTNET%" restore YaziDesktopHost.slnx
if errorlevel 1 goto :failed

echo Building the solution...
"%DOTNET%" build YaziDesktopHost.slnx --no-restore
if errorlevel 1 goto :failed

echo Running the executable test suite...
"%DOTNET%" run --project tests/YaziDesktopHost.Tests/YaziDesktopHost.Tests.csproj --no-build --no-restore
if errorlevel 1 goto :failed

echo Publishing and validating the win-x64 release package...
"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Publish-Release.ps1" -Dotnet "%DOTNET%" -OutputDirectory "%REPOSITORY_ROOT%\artifacts\release\win-x64"
if errorlevel 1 goto :failed

echo Build and publish completed successfully.
popd
exit /b 0

:failed
set "EXIT_CODE=%ERRORLEVEL%"
echo Build and publish failed with exit code %EXIT_CODE%.
popd
exit /b %EXIT_CODE%

:missing_sdk
echo A .NET SDK was not found. Install the .NET 10 SDK or set up dotnet on PATH.
exit /b 1
