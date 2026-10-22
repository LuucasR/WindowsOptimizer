# WindowsOptimizer

App mínima para la bandeja del sistema de Windows con dos modos:

- **Modo Tradicional**: Windows funciona normalmente.
- **Modo Trabajo**: deja a Windows con el menor consumo posible de CPU, RAM y GPU, para jugar o usar programas pesados (Unreal Engine, Blender, etc.).

Antes de cambiar nada guarda una copia exacta del estado del sistema, y al volver a Tradicional lo restaura. Si la PC se reinicia o la app se cierra en Modo Trabajo, al abrirla de nuevo ofrece restaurar.

## Qué hace el Modo Trabajo

- Detiene y deshabilita temporalmente unos 150 servicios innecesarios (telemetría, Windows Update, indexación, impresión, sincronización, actualizadores de terceros...). Nunca toca audio, red, drivers de GPU, Bluetooth, mandos ni anti-cheats.
- Pausa tareas programadas de telemetría y mantenimiento.
- Activa el plan de energía *Máximo rendimiento*.
- Quita animaciones, sombras y transparencias al instante.
- Desactiva Game DVR, apps en segundo plano y notificaciones; da más prioridad a la ventana activa.
- Cierra programas de una lista editable y da prioridad ALTA de CPU a otra (Unreal, juegos...).
- Libera RAM de procesos en segundo plano.
- Opcional: pausa Defender en tiempo real y cierra el escritorio de Windows (Ctrl+Alt+W para volver).

## Uso

- **Doble clic** en el icono o **Ctrl+Alt+W**: cambia de modo.
- **Clic derecho**: configuración visual (programas, servicios, opciones), idioma (Español / English / 中文), liberar RAM, iniciar con Windows y reiniciar el Modo Trabajo para aplicar cambios.

La configuración se guarda en `C:\ProgramData\WindowsOptimizer\config.json` y el registro de acciones en `log.txt` en la misma carpeta.

## Compilar

Requiere el SDK de .NET 8.

```powershell
# Desarrollo (requiere .NET 8 instalado en la PC)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish

# Portable para otras PCs (no necesita .NET)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish-portable
```

Para distribuir, copia `publish-portable\WindowsOptimizer.exe` junto con los archivos de `Dist\` (`Instalar.cmd`, `Desinstalar.cmd`, `LEEME.txt`).

## Estructura

| Archivo | Función |
|---|---|
| `TrayApp.cs` | Icono de bandeja, menú y cambio de modo |
| `ConfigForm.cs` | Ventana de configuración |
| `Optimizer.cs` | Aplica y restaura los módulos en orden |
| `Modules/` | Servicios, tareas, energía, registro, efectos, Defender, procesos, memoria, Explorer |
| `Config.cs` | Configuración por defecto y migración de versiones |
| `L10n.cs` | Textos en español, inglés y chino |
