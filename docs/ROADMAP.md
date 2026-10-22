# Estado 0.3

## Implementado para probar
- Principal, selector de pantalla/ventana, interruptor, duraciones y atajo global.
- Miniaturas estáticas, recientes y visor local con recorte/exportación.
- Fuentes WASAPI de escritorio, micrófono y aplicaciones, activación explícita, pistas y nombres.
- Opciones de calidad, espacio temporal, notificación, sonido, inicio minimizado y con Windows.
- Paquete con runtime .NET 10. FFmpeg continúa externo.
- Pruebas de modelos, audio circular, mux con tonos sintéticos y construcción de ventanas.

## Pendiente antes de considerar estable
- Probar hardware, procesos reales y sincronización AV; calibrar origen temporal.
- Reconectar fuentes y resolver cambios de dispositivos/aplicaciones sin perder sesión.
- Captura de ventana por GPU, eliminar copias innecesarias y medir rendimiento.
- Preescucha real de mezcla individual, controles de recorte más precisos, identificación de pantallas en varias GPU.
- Limpieza segura de sesiones abandonadas y recuperación ante disco lleno.
- Revisar empaquetado/licencia para motor integrado, firma y auditoría externa.

No se garantiza seguridad absoluta ni consumo mínimo sin pruebas y revisión adicionales.
