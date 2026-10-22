# Arquitectura de desarrollo 0.2

`VibeClip.Core`: ajustes JSON, catálogo, validación de rangos, ejecución de herramientas, análisis de pistas, exportador y búfer experimental. `VibeClip.App`: principal, configuración, bandeja, atajo y visor Windows WPF. No contiene clientes HTTP, cuentas ni telemetría.

## Motor experimental

FFmpeg se selecciona manualmente. No se descarga ni distribuye con la app. Cada comando usa `ProcessStartInfo.ArgumentList` y `UseShellExecute=false`, sin construir comandos para una shell. La salida de diagnóstico se drena con límite de memoria. Cancelar o superar un tiempo límite termina el proceso; al cerrar se espera el apagado del motor. En Windows, un Job Object con `KILL_ON_JOB_CLOSE` vincula cada proceso multimedia a la vida de VibeClip para evitar que siga capturando si el proceso de la app se termina inesperadamente.

Captura: `ddagrab` → descarga BGRA → escalado/padding a la resolución elegida → NV12 → codificador → segmentos MKV de alrededor de un segundo. La selección automática intenta GPU; CPU requiere elección explícita. Las copias y el escalado de este primer camino no son la arquitectura final óptima.

Los nombres de segmentos son únicos durante la sesión. Un índice CSV informa de los ya cerrados. Una revisión por segundo (solo durante captura) conserva segmentos según duración y límite de bytes y elimina antiguos. Para guardar se espera el cierre del segmento actual, se toma una instantánea de archivos cerrados bajo exclusión mutua respecto de la poda y se concatenan en MP4 sin recodificar. El motor continúa capturando. Los temporales de guardado se eliminan en `finally`.

El límite excluye el segmento activo y las instantáneas temporales. No se afirma precisión exacta al momento del atajo; existe granularidad de alrededor de un segundo. Un cierre abrupto puede dejar el directorio de sesión, indicado en README.

## Visor/exportación

Se crea el decodificador WPF únicamente al abrir un clip. FFprobe obtiene las pistas reales y la duración. Exportación rápida copia video; exacta usa libx264. La mezcla aplica volumen por pista y un limitador, excluyendo la pista principal al elegir mezcla individual. No es separación de fuentes mediante IA ni permite eliminar una aplicación de audio ya mezclado.

La reproducción actual usa la pista principal: la mezcla por fuentes se aplica solo al guardar. Esto aparece junto a los controles. Las copias se escriben a un nombre temporal local y se mueven al destino solo después de terminar. Se rechaza sobrescribir el original o un archivo existente. Cancelar no deja una copia final incompleta.

## Validación

Pruebas de modelos y almacenamiento; límites y retención de segmentos; construcción de las tres ventanas y tamaño mínimo. La suite opcional de FFmpeg genera video y dos tonos sintéticos, comprueba pistas, exporta recorte exacto y video sin audio, verifica el hash original, cancela una exportación y guarda un replay sintético mientras el motor sigue activo. No prueba la GPU ni Desktop Duplication sobre la pantalla real.

## Referencias
- [FFmpeg ddagrab](https://ffmpeg.org/ffmpeg-filters.html#ddagrab)
- [Formatos y segmentación](https://ffmpeg.org/ffmpeg-formats.html)
- [WPF](https://learn.microsoft.com/dotnet/desktop/wpf/)
- [RegisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [Audio por proceso de Windows, para la siguiente etapa](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/)
