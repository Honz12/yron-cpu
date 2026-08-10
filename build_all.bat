@echo off
setlocal enabledelayedexpansion

set FRAMEWORK=net10.0
set OUT_DIR=bin\Release\%FRAMEWORK%

echo =========================================
echo  Building Windows x64 Single-File Executable...
echo =========================================
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
if %errorlevel% neq 0 (
    echo [ERROR] Windows build failed.
    exit /b %errorlevel%
)

echo =========================================
echo  Building Linux x64 Single-File Executable...
echo =========================================
dotnet publish -c Release -r linux-x64 -p:PublishSingleFile=true --self-contained true
if %errorlevel% neq 0 (
    echo [ERROR] Linux build failed.
    exit /b %errorlevel%
)

echo =========================================
echo  Packaging into ZIP Archives...
echo =========================================

powershell -Command "Compress-Archive -Path '%OUT_DIR%\win-x64\publish\*' -DestinationPath '%OUT_DIR%\app-win-x64.zip' -Force"
powershell -Command "Compress-Archive -Path '%OUT_DIR%\linux-x64\publish\*' -DestinationPath '%OUT_DIR%\app-linux-x64.zip' -Force"

echo =========================================
echo  Build and Packaging Complete!
echo  Archives generated at:
echo   - %OUT_DIR%\app-win-x64.zip
echo   - %OUT_DIR%\app-linux-x64.zip
echo =========================================
