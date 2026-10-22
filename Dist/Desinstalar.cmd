@echo off
setlocal
title WindowsOptimizer - Desinstalar

net session >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

rem Si el Modo Trabajo sigue activo, hay que restaurar Windows antes de borrar la app
if exist "%ProgramData%\WindowsOptimizer\state.json" (
    echo El Modo Trabajo esta ACTIVO.
    echo Abre WindowsOptimizer, vuelve a "Modo Tradicional" y despues ejecuta este desinstalador.
    pause
    exit /b 1
)

echo Desinstalando WindowsOptimizer...
taskkill /im WindowsOptimizer.exe /f >nul 2>&1
schtasks /delete /tn WindowsOptimizer /f >nul 2>&1
schtasks /delete /tn WindowsOptimizer_Run /f >nul 2>&1
schtasks /delete /tn WindowsOptimizer_Explorer /f >nul 2>&1
timeout /t 1 /nobreak >nul

powershell -NoProfile -Command ^
  "foreach ($dir in @([Environment]::GetFolderPath('CommonPrograms'), [Environment]::GetFolderPath('CommonDesktopDirectory'))) {" ^
  "  Remove-Item (Join-Path $dir 'WindowsOptimizer.lnk') -ErrorAction SilentlyContinue }"
rmdir /s /q "%ProgramFiles%\WindowsOptimizer" 2>nul
rmdir /s /q "%ProgramData%\WindowsOptimizer" 2>nul

echo Listo. WindowsOptimizer fue desinstalado.
timeout /t 4 >nul
