# Arquitectura 0.3

Core: modelos, ajustes, catálogo, PCM circular por páginas, replay por segmentos y exportador. Audio: WASAPI mediante NAudio 3.3.0, incluyendo loopback por proceso. App: interfaz WPF, selector DXGI/ventanas, bandeja, atajo, miniaturas y opciones de inicio. SDK/runtime .NET 10.

Video de monitor: ddagrab → descarga BGRA → escala/padding → NV12 → codificador → segmentos MKV. Ventana: gdigrab por HWND validado con PID/visibilidad al iniciar; es un modo de compatibilidad con más trabajo CPU. El selector de monitor enumera las salidas de la primera GPU para corresponder al dispositivo DXGI predeterminado del motor.

Audio: WASAPI PCM16/48 kHz/estéreo, una fuente por dispositivo o árbol de proceso. Callback de captura copia los paquetes a un anillo de páginas de un segundo, con marcas QPC y limpieza de páginas reutilizadas. Los intervalos ausentes son silencio. La memoria total de anillos se limita aproximadamente a 128 MiB, acortando la duración solicitada si se necesita.

Al guardar: snapshot de segmentos cerrados, extracción de audio por QPC y unión a MP4 con nombres de pistas y disposiciones explícitas. Video se copia; audio se codifica AAC. Mezcla única usa Escritorio/micrófono cuando existe Escritorio, excluyendo aplicaciones que duplicarían su contenido. El origen de video se estima con el primer segmento y debe calibrarse para mayor precisión. La suite sintética comprueba el mux y el anillo, no certifica sincronización de hardware real.

Herramientas: argumentos separados, sin shell ni PATH automático; diagnósticos drenados con límite, cancelación/timeout y Job Objects para vida de procesos. Exportación temporal sin sobrescribir originales. Dependencias con versión exacta y lockfile; runtime autocontenido para el usuario.

Miniaturas: primera imagen decodificada por FFmpeg, tamaño limitado; se cargan secuencialmente hasta 12 clips al actualizar la lista visible. Se detiene el trabajo al minimizar. PNG locales con claves hash, límite de 60; borrado explícito en General. No hay captura de pantalla para producir miniaturas: se leen los clips existentes.

Pendiente: pruebas interactivas de fuentes reales, reconexión, calibración AV, captura de ventanas por GPU, eliminación de copias y mediciones de CPU/GPU/RAM/disco.

Referencias: [NAudio](https://github.com/naudio/NAudio/tree/v3.3.0), [WASAPI por proceso](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/), [FFmpeg](https://ffmpeg.org/documentation.html), [Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).
