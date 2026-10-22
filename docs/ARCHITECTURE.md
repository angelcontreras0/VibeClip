# Arquitectura inicial

`VibeClip.Core` contiene ajustes, almacenamiento JSON, catálogo y reglas de recorte/mezcla sin dependencia de interfaz. `VibeClip.App` es la capa Windows WPF. `VibeClip.Core.Checks` ejecuta casos de comportamiento sin paquetes externos de pruebas.

La interfaz y el runtime no constituyen por sí solos una garantía de rendimiento. Esta base usa .NET 8, disponible en la máquina de desarrollo; revisar la versión de soporte antes de una entrega estable.

## Límites actuales
No hay motor de captura ni exportador. La UI de Audio describe las fuentes previstas, sin presentar una lista ficticia de procesos detectados. No se guardan credenciales ni contenido multimedia en el repositorio.

No hay hilos de sondeo, bucle de previsualización de escritorio ni análisis de clips en reposo. La lista lee metadatos en un trabajo bajo demanda. El único temporizador del visor está activo al reproducir y se detiene al pausar, ocultar o cerrar. Las miniaturas se posponen para evitar cargar un decodificador por tarjeta.

El futuro motor deberá separar captura, codificación, búfer y exportación. El búfer debe contener paquetes comprimidos y permitir conservar una instantánea mientras se guarda un clip sin detener la captura. Deben definirse límites de memoria, política de fotogramas clave y reloj común para audio/video antes de implementarlo.

## Referencias de implementación
- [WPF](https://learn.microsoft.com/dotnet/desktop/wpf/)
- [RegisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [Captura de audio por proceso en Windows](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/)

El código de estas muestras debe revisarse junto con su licencia antes de copiarlo. La implementación del motor requerirá comprobar capacidades reales del sistema, sin suponer que todo equipo dispone de un codificador concreto.
