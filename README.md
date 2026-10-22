# VibeClip · Desarrollo 0.3

Clips del escritorio, privados y locales. Windows, C# / WPF, interfaz oscura y lavanda. Predeterminado: **1080p, 60 FPS, 60 segundos**. Sin cuentas, nube, telemetría, descargas automáticas ni clientes HTTP en la app.

**Versión experimental.** Las pruebas automáticas usan video/tonos sintéticos, sin grabar pantalla ni micrófono. La captura con tus dispositivos, aplicaciones, GPU y ventanas requiere prueba en el equipo real. No se garantiza aún consumo mínimo, compatibilidad universal ni sincronización audiovisual precisa.

## Probar
1. Cierra versiones anteriores, descomprime el ZIP completo y abre `VibeClip.App.exe`. El paquete incluye el runtime de escritorio .NET 10; no necesita instalación global ni administrador.
2. FFmpeg sigue siendo externo: conserva `ffmpeg.exe` y `ffprobe.exe` juntos y selecciónalo en **Configuración → Video → Opciones avanzadas**. Usa un distribuidor enlazado en [el sitio oficial](https://ffmpeg.org/download.html). La app no descarga ni ejecuta motores desde PATH. También reconoce una carpeta `tools` junto a la app si tú colocas allí el motor.
3. Selecciona una pantalla por nombre/resolución, o pulsa **Elegir ventana**. No necesitas introducir índices DXGI.
4. En **Configuración → Audio**, elige y activa tus fuentes. Micrófono apagado por defecto. Abre las aplicaciones elegidas antes de activar la captura.
5. Pulsa el interruptor de captura y guarda con el botón o tu atajo. La app nunca empieza a grabar automáticamente, incluso si eliges iniciar con Windows.

## Funciones
- Pantallas detectadas desde DXGI con resolución e identificación de principal; ventanas abiertas mediante selector. El modo ventana es de compatibilidad GDI y puede tener mayor consumo o no capturar contenido protegido/minimizado.
- Duraciones 30/60/90/120 segundos y duración personalizada desde Configuración → Clips.
- Calidad ligera/equilibrada/alta; 30/60 FPS, cursor, codificador GPU automático o elección explícita de CPU.
- Audio WASAPI de la salida predeterminada o dispositivo elegido, micrófono y aplicaciones por proceso/árbol de procesos. NAudio.Wasapi 3.3.0, versión fijada y licencia MIT incluida.
- Fuentes activables con nombre, dispositivo o aplicación. Agregar/quitar aplicaciones hasta ocho fuentes. Pistas separadas con nombres conservados o mezcla única.
- Clips recientes con miniaturas estáticas generadas localmente bajo demanda, sin reproducción automática. La generación se cancela al minimizar; caché limitada a 60 imágenes y opción para borrarla.
- Visor bajo demanda: reproducir, recortar y exportar una copia MP4. Corte rápido aproximado o corte exacto con recodificación, volumen/silencio por pista existente y cancelación.
- Notificaciones y sonido al guardar; inicio minimizado e inicio con Windows opcionales. Este último registra únicamente un valor de inicio para el usuario actual.
- Minimizar → bandeja; X en principal → detener video/audio y cerrar. Solo una instancia de esta versión por sesión de Windows.

## Audio y límites
Escritorio contiene toda la salida seleccionada. Para quitar música al editar se usa una mezcla de fuentes individuales **sin sumar Escritorio**. No se pueden recuperar fuentes que no se grabaron por separado. El visor reproduce la mezcla seleccionada, incluido el micrófono. Cada cambio prepara una copia temporal local con video sin recodificar y una pista de audio mezclada; pulsa reproducir cuando esté lista. La selección inicial usa fuentes separadas sin sumar Escritorio si hay pistas de aplicaciones. Puedes cambiar las casillas y volúmenes, o escuchar solo la primera pista. Límite de 1 GB por copia temporal; se elimina al cerrar normalmente.

La captura de aplicaciones requiere Windows 10 actualizado o Windows 11. Se resuelve el proceso al activar y se incluye su árbol; varias instancias, procesos compartidos o aplicaciones que renderizan audio fuera de ese árbol pueden no comportarse como esperas. Si cambias de dispositivo o reinicias una aplicación, detén y vuelve a activar. No hay reconexión transparente todavía. Si una fuente falla, se detiene la sesión tras terminar el guardado en curso.

El audio usa PCM16 estéreo a 48 kHz con marcas QPC y un máximo agregado de aproximadamente 128 MiB. La duración se reduce si las fuentes y el tiempo elegido exceden ese límite. El origen temporal del video se estima a partir del primer segmento cerrado: **puede existir desfase**, pendiente de calibración y pruebas reales. No describir este prototipo como sincronización de producción.

El video usa segmentos comprimidos en disco, no un búfer en RAM. El límite de espacio se aplica a segmentos cerrados; segmento activo y copias temporales necesitan espacio adicional. Guardar espera alrededor de 1.3 segundos y trabaja con granularidad aproximada de un segundo. El camino de monitor todavía hace copias GPU/CPU para escalar. No hay cifras de rendimiento garantizadas.

## Privacidad y seguridad
- No se graba al abrir la app ni Configuración. Los dispositivos se activan al pulsar el interruptor.
- Motor externo bajo tus permisos: elige un ejecutable de confianza. Comandos con argumentos separados, sin shell, y entradas de archivos restringidas a protocolos locales.
- Originales conservados; exportación a temporales y finalización solo al terminar. Cancelar no deja una copia final incompleta.
- Procesos multimedia ligados a la vida de la app mediante Job Objects; controles de tiempo de espera y cierre/cancelación.
- Ajustes limitados y validados. Dependencias fijadas; consulta [la revisión](docs/SECURITY.md).
- El caché puede contener imágenes/audio/video privados de tu escritorio. Se almacena localmente, **sin cifrado propio**. Un cierre abrupto puede dejar restos en `%LOCALAPPDATA%/VibeClip/buffer`. Con VibeClip cerrado puedes eliminar esa carpeta. Las miniaturas están en `%LOCALAPPDATA%/VibeClip/thumbnails`; se borran desde General.

No es una versión firmada ni auditada por terceros. La revisión de paquetes no equivale a ausencia de fallos.

## Desarrollo y pruebas
Windows y SDK .NET 10:
```powershell
dotnet build src/VibeClip.App -c Release
dotnet run --project src/VibeClip.App
dotnet run --project tests/VibeClip.Core.Checks -c Release -- C:\ruta\ffmpeg.exe
dotnet run --project tests/VibeClip.Ui.Checks -c Release
dotnet publish src/VibeClip.App -c Release -r win-x64 --self-contained true -o artifacts/VibeClip
```
La suite multimedia genera patrones y tonos: no abre dispositivos de captura. CI descarga FFmpeg 9.0.2 con SHA-256 fijado solo para pruebas; no se incluye en la app. El motor de video externo requiere `ddagrab`, `gdigrab`, codificador compatible y `libx264` para recorte exacto.

[Arquitectura](docs/ARCHITECTURE.md) · [Plan](docs/ROADMAP.md) · [Revisión de seguridad](docs/SECURITY.md)
