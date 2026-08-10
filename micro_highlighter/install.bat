@echo off
rem Installs the YRN assembly syntax highlighter for the micro editor.
rem Copies yrn.yaml into micro's syntax directory (created if missing).

setlocal

if defined MICRO_CONFIG_HOME (
    set "CONFIG_DIR=%MICRO_CONFIG_HOME%"
) else if defined XDG_CONFIG_HOME (
    set "CONFIG_DIR=%XDG_CONFIG_HOME%\micro"
) else (
    set "CONFIG_DIR=%USERPROFILE%\.config\micro"
)

if not exist "%CONFIG_DIR%\syntax" mkdir "%CONFIG_DIR%\syntax"
copy /Y "%~dp0yrn.yaml" "%CONFIG_DIR%\syntax\yrn.yaml" >nul
if errorlevel 1 (
    echo Failed to install micro syntax to "%CONFIG_DIR%\syntax\yrn.yaml"
    exit /b 1
)
echo Installed micro syntax to "%CONFIG_DIR%\syntax\yrn.yaml"

endlocal
