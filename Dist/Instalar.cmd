@echo off
setlocal
title WindowsOptimizer - Instalar

rem Pedir permisos de administrador si no los tiene
net session >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "DEST=%ProgramFiles%\WindowsOptimizer"
set "EXE=%DEST%\WindowsOptimizer.exe"

echo Instalando WindowsOptimizer en "%DEST%"...
taskkill /im WindowsOptimizer.exe /f >nul 2>&1
timeout /t 1 /nobreak >nul
if not exist "%DEST%" mkdir "%DEST%"
copy /y "%~dp0WindowsOptimizer.exe" "%EXE%" >nul
if errorlevel 1 (
    echo ERROR: no se pudo copiar el programa.
    pause
    exit /b 1
)

rem Accesos directos en el menu Inicio y en el escritorio
powershell -NoProfile -Command ^
  "$sh = New-Object -ComObject WScript.Shell;" ^
  "foreach ($dir in @([Environment]::GetFolderPath('CommonPrograms'), [Environment]::GetFolderPath('CommonDesktopDirectory'))) {" ^
  "  $lnk = $sh.CreateShortcut((Join-Path $dir 'WindowsOptimizer.lnk'));" ^
  "  $lnk.TargetPath = '%EXE%'; $lnk.WorkingDirectory = '%DEST%'; $lnk.IconLocation = '%EXE%,0';" ^
  "  $lnk.Description = 'Modo Tradicional / Modo Trabajo'; $lnk.Save() }"

echo.
echo Listo. WindowsOptimizer aparece junto al reloj de Windows.
echo Clic derecho en el icono ^> "Iniciar con Windows" para que arranque solo.
start "" "%EXE%"
timeout /t 5 >nul
