## TECKIO ADC Lab 1.0.4 / firmware fuente 1.0.1

- Corrección de fuentes PIC18F45K50: cada respuesta CDC de 64 bytes termina con ZLP; el endpoint sólo queda libre al completar ambas transacciones. Estado reiniciado al reconfigurar USB.
- Conexión Windows: HELLO con tres intentos limitados, recuperación STOP, contadores RX/CRC, primeros bytes recibidos en consola y liberación del puerto incluso si falla Open/HELLO. No se reintentan escrituras de registros.
- Instalador normal Windows x64: menú Inicio, escritorio opcional, desinstalador y runtime .NET incluido. Actualización desde la app mediante instalador verificado con SHA-256.
- Se conservan temas oscuro/claro, gráficas, datos crudos, configuración y diagnóstico AD7195, reportes PDF y exportación CSV/JSON.
- Fondo de ventana, desplegables y etiquetas de botones corregidos para mantener contraste en ambos temas; panel lateral desplazable en pantallas pequeñas.
- Firmware entregado exclusivamente como ZIP de `.c` y `.h`, **sin compilar ni generar HEX**. Compílalo y prográmalo localmente para aplicar la corrección USB.

Pruebas de software y del instalador automatizadas en GitHub Actions. La conexión física con tu PIC/AD7195 queda pendiente de tu ensayo después de programar estas fuentes.
