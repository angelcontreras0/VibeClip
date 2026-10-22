# Plan de VibeClip

## 1. Base verificable (este avance)
Principal, configuración local, atajo, bandeja, catálogo y reproducción local. Validación del modelo de ajustes, rangos y selección de audio. Captura y exportación explícitamente deshabilitadas.

## 2. Primer clip real
Prototipo de captura de un monitor con codificación por hardware. Búfer acotado de video comprimido. Atajo guarda un archivo reproducible en la carpeta local. Primero comprobar tiempos, fotogramas clave y escritura segura; después selección de ventanas. No incorporar un temporizador de capturas de pantalla como motor definitivo.

## 3. Audio
Salida predeterminada de Windows y micrófono; luego captura por proceso y árbol de procesos. Selección de aplicaciones/dispositivos, reconexión y alineación temporal. Pistas adicionales y metadatos que permitan identificar cada fuente al abrir un clip. Probar por separado navegador, juegos y Discord.

## 4. Recorte y mezcla
Visor muestra las pistas realmente presentes en cada archivo. Mutear, ajustar volumen y guardar una copia. No sobrescribir originales. Exportación cancelable y escritura temporal antes del nombre final. Recorte rápido en fotogramas clave; cortes exactos requieren una estrategia explícita de recodificación.

## 5. Rendimiento y entrega
Medir un reposo sin captura, captura 1080p60, ventana oculta, cinco fuentes de audio, exportación simultánea y sesiones largas. Registrar hardware, versión de Windows, codificador, bitrate, duración, CPU/GPU, memoria y latencias. Comparar con la línea base antes de afirmar que es ligera. Revisar licencias de cualquier dependencia multimedia antes de distribuirla.

## Comportamientos acordados
- 1080p60 por defecto; duración y atajo configurables.
- Sin cuenta, telemetría, nube ni llamadas de red en ejecución.
- Minimizar principal → bandeja; X en principal → salida completa.
- Cerrar ventanas secundarias no cierra la principal.
- Audio y dispositivos en Configuración; recientes en la principal.
- Edición sencilla: recorte de extremos, volumen y silencio por pista, copia local.
