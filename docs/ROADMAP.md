# Plan de VibeClip

## Implementado en desarrollo 0.2
- Base WPF con diseño oscuro/lavanda, ajustes, carpeta, bandeja y atajo global.
- Primer motor de video de monitor, mediante FFmpeg externo seleccionado manualmente.
- Búfer comprimido acotado en disco y guardado por segmentos mientras continúa la captura.
- Visor, recorte, exportación local a copia y mezcla de pistas existentes en videos importados.
- Pruebas sintéticas multimedia, modelos y construcción de ventanas.

## Siguiente: audio y captura de ventanas
- Captura WASAPI de salida predeterminada y micrófono; luego captura por proceso/árbol de procesos.
- Reloj común y sincronización, reconexión de dispositivos y aplicaciones.
- Pistas adicionales con nombres persistentes, evitando duplicarlas con Escritorio en las mezclas.
- Selector real de monitor y ventana. Revisar sesiones bloqueadas, cambios de pantalla y recuperación del motor.

## Optimización pendiente
- Medir CPU/GPU/RAM/disco a 1080p60 durante sesiones largas y exportaciones simultáneas.
- Reducir copias GPU/CPU, evaluar captura/escalado nativos y búfer comprimido en memoria con límite.
- Reducir precisión aproximada y latencia del guardado. Ofrecer diagnóstico claro del codificador y retrasos.
- Recuperar/limpiar sesiones abandonadas sin tocar sesiones activas ni clips guardados.
- Miniaturas estáticas bajo demanda, preescucha real de la mezcla y controles de selección de rango más precisos.

## Antes de una entrega estable
- Revisar soporte de .NET, dependencias/licencias y distribución del motor.
- Pruebas interactivas de bandeja, atajos, reproducción y hardware de captura.
- Revisión de seguridad, firma y empaquetado. No describir una compilación de desarrollo como auditada o totalmente segura.
- Comprobar que la app sigue funcionando sin conexión y que no emite tráfico propio.
