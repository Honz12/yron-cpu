@echo off
rem Installs the YRN assembly and YRC compiler syntax highlighters for the
rem micro editor. Copies yrn.yaml and yrc.yaml into micro's syntax directory
rem (created if missing).

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
copy /Y "%~dp0yrc.yaml" "%CONFIG_DIR%\syntax\yrc.yaml" >nul
if errorlevel 1 (
    echo Failed to install micro syntax to "%CONFIG_DIR%\syntax\yrc.yaml"
    exit /b 1
)
echo Installed micro syntax to "%CONFIG_DIR%\syntax\yrn.yaml"
echo Installed micro syntax to "%CONFIG_DIR%\syntax\yrc.yaml"

endlocal
