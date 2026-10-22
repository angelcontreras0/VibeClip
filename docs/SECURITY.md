# Revisión de desarrollo 0.3

## Comprobado
- Compilación con advertencias como errores.
- La consulta de vulnerabilidades NuGet del proyecto, incluidas transitivas, no reportó paquetes vulnerables con la fuente nuget.org consultada en esta revisión. No cubre fallos desconocidos ni binarios FFmpeg externos.
- NAudio.Wasapi 3.3.0 fijado, dependencias y hashes en lockfile; licencia MIT incluida. El motor FFmpeg de CI tiene versión y SHA-256 fijados.
- No hay clientes HTTP, cuentas ni telemetría en el código de la app. No se ejecutan comandos a través de una shell.
- Se validan rutas, límites, rangos y tamaño de configuración; las entradas multimedia de análisis/exportación limitan protocolos a file/pipe.
- Pruebas: conservación del hash original, cancelación sin archivo final, límite de segmentos, borrado de PCM antiguo, ausencia de audio obsoleto al reutilizar páginas, nombres de pistas y terminación del proceso hijo al cerrar su Job Object.
- La captura comienza solo tras acción explícita. Micrófono desactivado por defecto. Startup opcional registra únicamente el ejecutable del usuario con argumento minimizado, sin iniciar captura.

## Límites y riesgos conocidos
- No es auditoría externa ni garantía total. El ejecutable no está firmado.
- El motor externo elegido se ejecuta con los permisos de tu usuario. No se verifica aquí su firma o procedencia; usa la fuente oficial/distribuidor fiable.
- Clips, miniaturas y temporales no tienen cifrado propio. Pueden contener información privada. No se incluyen en el repositorio ni se suben.
- No hay prueba de tráfico de red a nivel de sistema; la afirmación sobre red corresponde al código revisado y a los argumentos que genera. No sustituye un análisis dinámico de todas las dependencias.
- La captura de hardware, permisos de micrófono, aplicaciones, ventanas y sincronización requieren pruebas interactivas. Las pruebas automatizadas no activan los dispositivos del usuario.
- El identificador de ventana se valida con PID y visibilidad al empezar, pero cambios durante la sesión deben manejarse mejor. El modo ventana de compatibilidad puede capturar negro o detenerse al minimizar.
- Cierres abruptos pueden dejar temporales; se documenta dónde se encuentran. No hay eliminación indiscriminada de otras sesiones.

Las futuras entregas deben repetir las pruebas, consulta de dependencias y revisión de permisos, y añadir validación de hardware, firma y análisis dinámico.

La preescucha crea copias MP4 en la carpeta temporal del usuario, bajo VibeClip-preview-GUID. No recodifica video; mezcla solo el audio seleccionado. Cada copia tiene un límite estimado de 1 GiB y exige espacio libre adicional. Las operaciones se cancelan al cerrar y las copias se eliminan al cerrar normalmente; un cierre abrupto puede dejar restos privados sin cifrar. Cambiar fuentes pausa la reproducción mientras se prepara la nueva mezcla.
