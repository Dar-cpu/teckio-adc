# TECKIO ADC Lab

Laboratorio nativo de Windows para adquisición y diagnóstico de ADC. Primer dispositivo: **AD7195 + TECKIO PIC18K (PIC18F45K50)**.

## Descargar y usar

- [Descargar instalador Windows y fuentes del firmware](https://github.com/Dar-cpu/teckio-adc/releases/latest)
- [Compilaciones de desarrollo y pruebas](https://github.com/Dar-cpu/teckio-adc/actions/workflows/build.yml)
- Windows 10 (1809 o posterior)/11 **x64**. El instalador incluye .NET; no necesita instalar el SDK.

1. Descarga `TECKIO-AD7195-firmware-sources.zip`, compila localmente los `.c`/`.h` en MPLAB X + XC8 y programa tu PIC mediante PICkit/ICSP. Es firmware independiente, **sin bootloader ni offset**. GitHub no compila firmware ni genera HEX.
2. Ejecuta `Teckio.AdcLab.Setup.exe`. Instala la aplicación, el acceso del menú Inicio y el desinstalador; el acceso al escritorio es opcional. Abre **TECKIO ADC Lab**, selecciona el COM de la PIC18K y pulsa **Conectar**.
3. Si conservas el montaje de prueba original con entradas y referencia alimentadas desde GPIO, entra en **Diagnóstico → Fixture original**. Al arrancar, esos GPIO están en alta impedancia.
4. Configura VREF real, canal, ganancia y filtro. Pulsa **Aplicar configuración**, luego **Iniciar captura**.
5. Para conservar más datos que la ventana RAM, marca **Grabar CSV al iniciar**. Exporta el reporte PDF desde la captura.

**Modo demostración** permite explorar la app sin placa. La interfaz, sesiones y reportes identifican explícitamente los datos simulados.

## Qué incluye

| Área | Funciones |
|---|---|
| Adquisición | USB CDC binario, paquetes CRC16, índices y timestamps del PIC, streaming y conversión única |
| Visualización | Forma de onda, código crudo, autoescala, zoom, cursor, histograma, FFT Hann de hasta 8192 puntos |
| Configuración | Los 8 canales internos/externos, PGA 1/8/16/32/64/128, bipolar/unipolar, sinc3/sinc4, FS, CHOP, ACX, burnout, buffer, paridad, referencia, reloj y latencia cero |
| Registros | Lectura de los 8 registros, escritura validada, hexadecimal/binario/decimal, consola y lectura de verificación |
| Diagnóstico | ID, BPDSW/GPOCON incluyendo bits inesperados como 0x46, pines, SYNC, MCLK2 por contador, estímulos digitales del fixture |
| Calibración | Cero/escala interna y de sistema; lectura de coeficientes; selección de canal validada |
| Datos | Ventana RAM configurable, CSV continuo, exportación CSV/JSON y metadatos de configuración |
| Reportes | PDF paginado: condiciones, integridad, métricas por canal, onda, histograma, FFT, diagnóstico, registros, metodología y anexo crudo |
| Preferencias | Temas oscuro/claro, perfiles JSON, frecuencia de refresco, memoria y metadatos de ensayo |
| Actualizaciones | Consulta GitHub Releases, descarga y verifica SHA-256 del instalador, abre el asistente de actualización y conserva preferencias |

No hay telemetría de mediciones. Sólo la comprobación de actualizaciones consulta Internet.

## Organización extensible

```text
ad7195/
  firmware/        PIC18F45K50, controlador ADC y stack USB incluido
  host/            Perfil AD7195: registros, validaciones y conversión
  docs/            Conexiones, protocolo y procedimiento de validación
src/
  Teckio.Adc.Core/  Protocolo, sesiones y análisis sin dependencia de WPF
  Teckio.AdcLab/    Aplicación Windows, gráficas, PDF y actualización
tests/             Pruebas C y .NET
installer/         Instalador Windows Inno Setup, actualización y desinstalación
img/               Logos originales de TECKIO
.github/workflows/ Compilación y publicación
```

Para otro ADC: añade su carpeta de firmware/perfil e implementa `IAdcProfile`. El transporte, las sesiones y el análisis son reutilizables. La UI de configuración actual es específica del AD7195; no anuncia soporte de otros ADC todavía.

## Compilar

### Windows

```powershell
dotnet run --project tests/Teckio.Adc.Tests -c Release
dotnet build src/Teckio.AdcLab/Teckio.AdcLab.csproj -c Release
dotnet publish src/Teckio.AdcLab/Teckio.AdcLab.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/windows
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DAppVersion=1.0.4 installer/Teckio.AdcLab.iss
```

SDK **.NET 10**. PDFsharp-WPF 6.2.4 (MIT) y System.IO.Ports 10.0.0. Los logos se integran como recursos en el ejecutable.

### Firmware

Fuentes para XC8 y PIC18F45K50; la configuración anterior se desarrolló con XC8 **4.00** y Microchip **PIC18F-K_DFP 1.17.312**. A partir de 1.0.1 se entregan **sólo fuentes**, sin ejecutar compiladores de firmware ni en el agente ni en GitHub. El ZIP contiene únicamente `.c` y `.h`, conservando `usb/`.

```bash
make -C ad7195/firmware XC8=/ruta/xc8-cc DFP=/ruta/Microchip.PIC18F-K_DFP.1.17.312/xc8
```

En MPLAB X: crea un proyecto standalone PIC18F45K50 con XC8, añade `main.c`, `board.c`, `ad7195.c`, `protocol.c` y los cuatro `.c` dentro de `usb/`. Añade `firmware/` y `firmware/usb/` a los include paths. Programa la configuración incluida.

**Reloj USB:** el cristal de 8 MHz de la tarjeta no genera 48 MHz mediante el PLL de este PIC. Este firmware usa HFINTOSC 16 MHz ×3 + ACT sincronizado por USB SOF. No requiere retirar el cristal. El reloj del AD7195 es independiente.

## Publicar actualizaciones

1. Cambia `Version` en `Directory.Build.props`, prueba y envía a `main`.
2. GitHub Actions prueba y compila la app, crea su instalador, prueba instalación/actualización/desinstalación y genera capturas visuales y PDF demo. El firmware sólo se empaqueta.
3. Si esa versión aún no existe, el flujo de `main` crea su tag y publica `Setup.exe`, su SHA-256 y el ZIP de fuentes C/H. Nunca reemplaza los assets de una versión ya publicada. También admite tags manuales coincidentes.
4. **Actualizar aplicación** descubre esa versión y abre el instalador verificado. Éste mantiene la misma instalación y accesos directos. Preferencias/perfiles en `%LOCALAPPDATA%/TECKIO/ADC Lab`; la app se instala por usuario en `%LOCALAPPDATA%/Programs/TECKIO/ADC Lab`. No modifica firmware ni exige tokens de GitHub.

El ejecutable no tiene firma Authenticode. SHA-256 verifica la descarga contra el asset del mismo Release; no es una firma de editor.

## Límites y validación

- El AD7195 permite aproximadamente 4.8 kS/s agregados en la configuración más rápida. Es un límite del ADC, **no una tasa medida/garantizada de esta placa**.
- Firmware: ring de 64 muestras, lotes de hasta 4, lectura SPI por GPIO compatible con el montaje existente. CRC con tabla y servicio de adquisición intercalado.
- Se cuentan pérdidas del buffer y huecos de índice. El ADC no tiene FIFO/contador: una conversión sobrescrita antes de leerla sólo puede sospecharse por tiempos/tasa, no contabilizarse exactamente.
- Las gráficas muestran ventanas; el CSV continuo conserva todas las muestras recibidas desde su activación, limitado por disco.
- σ y bits DC no equivalen a ENOB dinámico. INL/DNL/SINAD/exactitud absoluta requieren ensayos específicos.
- La temperatura interna se convierte a °C sólo con ganancia 1 y bipolar.
- La app se valida mediante compilación y pruebas automáticas. El firmware 1.0.1 se entrega sin compilar; la corrección USB debe confirmarse en tu placa después de tu compilación/programación local.

Consulta [conexión USB y prueba COM](ad7195/docs/CONNECTION.md), [hardware](ad7195/docs/HARDWARE.md), [protocolo](ad7195/docs/PROTOCOL.md), [validación](ad7195/docs/VALIDATION.md) y [terceros](THIRD_PARTY_NOTICES.md).
