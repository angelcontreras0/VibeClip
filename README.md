# VibeClip

Clips del escritorio, locales y privados. Proyecto para Windows con interfaz nativa en C# / WPF, sin navegador integrado ni servicios de red.

**Estado: primera base de desarrollo, todavía no es una grabadora.** El motor de captura y la exportación no están implementados. La interfaz lo indica y no simula grabaciones.

## Ya funciona

- Ajustes locales persistentes en `%LOCALAPPDATA%/VibeClip/settings.json`, con validación y recuperación si el archivo está dañado.
- Valores iniciales 1920 × 1080, 60 FPS y 60 segundos; carpeta y duración configurables.
- Atajo global personalizable con detección de conflictos mediante `RegisterHotKey`. Por ahora informa que el motor no está disponible.
- Minimizar oculta la principal en la bandeja; doble clic en el icono la restaura; X cierra toda la app.
- Catálogo de los últimos 30 archivos MP4/MKV de la carpeta elegida, actualizado al pedirlo.
- Visor local creado bajo demanda: reproducir, pausar, buscar, silenciar todo y previsualizar un rango de recorte. Compatibilidad de reproducción sujeta a los códecs de Windows.

## Pendiente

- Captura de pantalla/ventana y codificación por hardware, con búfer circular de paquetes comprimidos.
- Guardar los últimos 30/60/90/120 segundos con el atajo, sin volver a codificar cuando sea posible.
- Captura de salida predeterminada, micrófono y audio por aplicación, sincronizados en pistas separadas.
- Exportar recortes y mezclar/silenciar pistas individuales. El visor actual **no guarda recortes**.
- Miniaturas bajo demanda, selección de aplicaciones/dispositivos, avisos y arranque opcional con Windows.
- Medición de CPU, GPU, memoria, disco y estabilidad durante sesiones largas a 1080p60.

## Compilar y ejecutar

Requisitos: Windows 10/11, SDK de .NET 8. La compatibilidad exacta del futuro motor se determinará en su implementación.

```powershell
dotnet build src/VibeClip.App/VibeClip.App.csproj -c Release
dotnet run --project src/VibeClip.App
dotnet run --project tests/VibeClip.Core.Checks -c Release
dotnet run --project tests/VibeClip.Ui.Checks -c Release
```

Para distribuir una vista previa que utiliza el runtime de escritorio de .NET 8 instalado:

```powershell
dotnet publish src/VibeClip.App -c Release -r win-x64 --self-contained false -o artifacts/VibeClip
```

GitHub Actions compila, ejecuta comprobaciones y publica un artefacto de desarrollo. El acceso a Internet del proceso de desarrollo/CI no forma parte de la aplicación: el código de la app no realiza solicitudes de red.

## Audio sin duplicados

Escritorio conservará la salida predeterminada completa. Las fuentes individuales serán pistas adicionales. Para quitar música de una mezcla, se excluye Escritorio y se suman solo las pistas individuales grabadas que quieras escuchar. Silenciar una pista individual no puede eliminar su sonido de la grabación mezclada de Escritorio.

Consulta [el plan de desarrollo](docs/ROADMAP.md) y [la arquitectura](docs/ARCHITECTURE.md). No hay cifras de rendimiento garantizadas: todavía deben medirse con el motor real.
