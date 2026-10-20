# Validación

## Automática

- Pruebas C del mismo CRC/empaquetador que usa el PIC.
- Pruebas .NET: fragmentación CDC aleatoria, re-sincronización después de CRC corrupto, conversión bipolar/unipolar, combinaciones inválidas, media/σ, FFT con seno conocido, histograma, pérdidas, ring y rollover.
- Compilación real XC8 para PIC18F45K50 con DFP fijado.
- Compilación real de WPF en Windows.
- Smoke test Windows: abre ventana, adquiere datos demo, renderiza imágenes oscuro/claro y crea PDF.
- Publicación standalone win-x64. Los artefactos QA son evidencia de software, no mediciones de hardware.

## Primer ensayo físico

1. Programar el HEX y confirmar enumeración COM. HELLO debe mostrar firmware e ID.
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

El actualizador conserva `Teckio.AdcLab.exe.previous` junto al ejecutable. Si la nueva versión no inicia, cierra la app y restaura esa copia. Configuración y perfiles permanecen en `%LOCALAPPDATA%/TECKIO/ADC Lab`.
