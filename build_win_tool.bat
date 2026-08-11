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
