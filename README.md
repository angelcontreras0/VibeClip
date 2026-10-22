# VibeClip

Captura tus últimos momentos del escritorio y guarda clips en tu PC. Aplicación Windows en C# / WPF, con interfaz oscura y lavanda, ajustes locales y sin cuentas, telemetría, nube ni descargas automáticas.

**Estado: desarrollo 0.2.** Hay un primer motor experimental de video y exportación real. No hay aún captura de audio, selección de ventanas ni cifras de rendimiento garantizadas.

## Qué funciona

- Principal con estado real de captura, duración, carpeta, atajo global y tarjetas de clips recientes.
- Ajustes persistentes, 1920 × 1080 a 60 FPS por defecto. Atajo personalizable con detección de conflictos.
- Minimizar principal → bandeja; X en principal → detener el motor y cerrar. La captura no se activa automáticamente.
- Captura experimental de un monitor mediante Desktop Duplication (`ddagrab`) de FFmpeg. Automático prueba NVENC, AMF y Quick Sync; CPU requiere selección explícita.
- Búfer de segmentos de video comprimido en disco, con límite de tamaño configurable y eliminación al detenerse normalmente.
- Guardar por botón, bandeja o atajo mientras la captura continúa. Se guardan los segmentos disponibles, con precisión aproximada de un segundo y unos 1.3 segundos de espera para cerrar el segmento en curso.
- Visor local bajo demanda: reproducción, rango de recorte, selección de pistas **presentes en el archivo**, silencio y volumen al exportar.
- Exportación a una copia MP4: modo rápido con copia del video y corte aproximado, o exacto con recodificación. Mezcla de audio a AAC y opción de dejar la copia sin audio. Cancelación sin sobrescribir el original.

## Probar la app

1. Descomprime la compilación y abre `VibeClip.App.exe`. Requiere el runtime de escritorio .NET 8; no pide administrador.
2. Obtén FFmpeg para Windows desde un proveedor enlazado en [su sitio oficial](https://ffmpeg.org/download.html). No se incluye el binario de FFmpeg en este repositorio ni en la compilación de VibeClip.
3. En **Configuración → Video**, selecciona el `ffmpeg.exe` que descargaste. Deja `ffprobe.exe` en la misma carpeta. Para las funciones actuales se necesita una compilación con `ddagrab`, los codificadores de tu GPU y `libx264` para el recorte exacto.
4. Elige el índice DXGI del monitor (inicialmente 0), carpeta y duración. El orden DXGI puede diferir del número de pantalla mostrado por Windows.
5. Pulsa **Activar**, espera a que haya video en el búfer y guarda con el botón o atajo. **Estos clips todavía no tienen audio.**
6. También puedes abrir un video existente con audio para probar recorte y mezcla sin activar la captura.

El ejecutable externo que selecciones se ejecuta con tus permisos: utiliza una fuente de confianza. VibeClip solo genera comandos locales y no añade destinos de red. Los procesos de análisis, concatenación y exportación limitan los protocolos de entrada a archivos y tuberías locales.

## Límites de esta etapa

- No es aún el motor definitivo optimizado. El camino de video descarga fotogramas de GPU a CPU para escalar y luego los entrega al codificador elegido; esto introduce copias y consumo que deben medirse y reducirse.
- El búfer usa disco, **no RAM**. Su límite se aplica a segmentos cerrados; el segmento activo y las copias temporales al guardar requieren espacio adicional. La duración disponible puede ser menor si se alcanza el límite de tamaño.
- Un cierre inesperado puede dejar restos de búfer en `%LOCALAPPDATA%/VibeClip/buffer`; no son clips publicados y puedes eliminarlos con VibeClip cerrado. No se borra automáticamente contenido de otras sesiones.
- La GPU, sus controladores y Desktop Duplication requieren prueba en el equipo real. Las pruebas automatizadas de este avance usan video sintético, sin capturar el escritorio del usuario.
- El visor depende de los códecs de reproducción de Windows. La preescucha usa la pista principal; los cambios individuales de mezcla se aplican a la copia al exportar.
- No hay aún fuentes de audio por aplicación. La pestaña Audio muestra el diseño previsto, identificado como pendiente.
- La selección de ventana, miniaturas bajo demanda, arranque con Windows y actualizaciones quedan para siguientes avances.

## Audio sin duplicados

Cuando se implemente la captura de audio, Escritorio conservará toda la salida predeterminada de Windows y las aplicaciones serán pistas adicionales. Para quitar música, se usará una mezcla de las pistas individuales sin sumar Escritorio. Silenciar una pista individual no elimina el sonido ya incluido en Escritorio.

En archivos importados, el visor trata la primera pista como principal y muestra las demás con los nombres que traiga el archivo. Revisa los nombres antes de mezclar: no puede recuperar fuentes que no fueron grabadas por separado.

## Desarrollo

Windows 10/11, SDK de .NET 8. Se revisará la versión de soporte antes de una entrega estable.

```powershell
dotnet build src/VibeClip.App/VibeClip.App.csproj -c Release
dotnet run --project src/VibeClip.App
dotnet run --project tests/VibeClip.Core.Checks -c Release
dotnet run --project tests/VibeClip.Ui.Checks -c Release
```

Pruebas multimedia opcionales con un FFmpeg local: generan patrones de video y tonos de audio; **no capturan pantalla ni micrófono**.

```powershell
dotnet run --project tests/VibeClip.Core.Checks -c Release -- C:\ruta\ffmpeg.exe
```

```powershell
dotnet publish src/VibeClip.App -c Release -r win-x64 --self-contained false -o artifacts/VibeClip
```

GitHub Actions compila, ejecuta pruebas de modelos/multimedia y construye las ventanas, y publica un artefacto de desarrollo. CI descarga una versión fija de FFmpeg con SHA-256 verificado solo para pruebas sintéticas; el motor no se incluye en el artefacto de la app. El acceso a red de CI es independiente de la app. Consulta [arquitectura](docs/ARCHITECTURE.md), [diseño](docs/DESIGN.md) y [plan](docs/ROADMAP.md).
