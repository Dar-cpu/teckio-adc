# Validación

## Automática

- El firmware se revisa como fuente y se empaqueta en C/H; **no se compila** en CI.
- Pruebas .NET: fragmentación CDC aleatoria, re-sincronización después de CRC corrupto, conversión bipolar/unipolar, combinaciones inválidas, media/σ, FFT con seno conocido, histograma, pérdidas, ring y rollover.
- Pruebas del cliente de conexión real con transporte simulado: HELLO de COM20, reintento, respuesta fragmentada, secuencia atrasada, STOP al conectar, timeout, liberación del COM y ausencia de reintentos de escritura.
- Compilación real de WPF en Windows.
- Smoke test Windows: abre ventana, adquiere datos demo, renderiza imágenes oscuro/claro y crea PDF.
- Instalador win-x64: instalación silenciosa, actualización en la misma carpeta, ejecución del programa instalado y desinstalación; conservación de datos de usuario. Los artefactos QA son evidencia de software, no mediciones de hardware.

## Primer ensayo físico

1. Compilar localmente las fuentes 1.0.1, programar el resultado y confirmar enumeración COM. HELLO debe mostrar firmware e ID. Seguir [la prueba de conexión](CONNECTION.md).
2. Leer registros y guardar captura del diagnóstico ID/BPDSW. Si aparece 0x46, conservar todas las lecturas, no sólo el bit6.
3. Configurar fixture original si se conservan esas conexiones. VREF en UI debe coincidir con la referencia aplicada.
4. Ganancia1, bipolar, buffer OFF, AIN1/AIN2, FS96. Con diferencia cero esperar código próximo a 0x800000.
5. Capturar 10 s y comprobar ERR/NOREF/paridad, índices, CRC y tasa.
6. Activar MCLK2 y medir desde RA4. Verificar pines ACX con la configuración correspondiente.
7. Escalar FS96 → 48 → 16 → 4 → 1 con CSV continuo. Evaluar tasa real, tiempos entre lecturas, gaps y drops en cada punto.
8. Desconectar/reconectar y probar recuperación. La app manda STOP al establecer conexión y los estímulos se liberan al cerrar DTR.
9. Ensayar los cuatro canales/pseudo-diferenciales y revisar que CHD corresponde al canal real.
10. Calibrar con un único canal. Escala interna a ganancia1 se rechaza porque utiliza coeficiente de fábrica.

**No se certifica máximo sostenido sin medirlo en la placa.** El contador no puede detectar todas las conversiones que el ADC sobrescribe antes de la lectura.

## Reportes

- Muestras inválidas excluidas de estadística/FFT; permanecen en crudos/CSV.
- FFT se omite con pérdidas, muestras insuficientes o temporización no uniforme.
- PDF usa snapshot inmutable de la captura y distingue DEMO.
- Registros del informe se etiquetan como última lectura manual, no como snapshot simultáneo.
- La ventana RAM y el archivo CSV tienen alcances distintos y se identifican.

## Recuperación de actualizaciones

El actualizador abre el instalador publicado después de verificar SHA-256. Si el asistente se cancela antes de instalar, la versión existente permanece instalada. Para reinstalar, ejecuta de nuevo su `Setup.exe`. Configuración y perfiles permanecen en `%LOCALAPPDATA%/TECKIO/ADC Lab`, incluso al desinstalar. No se conserva una copia portable `.previous`.
