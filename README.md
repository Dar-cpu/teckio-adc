# TECKIO ADC Lab

Aplicación de escritorio para Windows que permite configurar, controlar y adquirir datos del ADC **AD7195** mediante la tarjeta de desarrollo **TECKIO PIC18K**, equipada con el microcontrolador **PIC18F45K50**.

## Descargar e instalar la aplicación

**[Descargar TECKIO ADC Lab para Windows](https://github.com/Dar-cpu/teckio-adc/releases/latest/download/Teckio.AdcLab.Setup.exe)**

1. Descarga `Teckio.AdcLab.Setup.exe` y ejecuta el instalador.
2. Si Windows muestra **Editor desconocido** o el aviso de Microsoft Defender SmartScreen, es porque el instalador no tiene firma digital. Si lo descargaste desde este repositorio, selecciona **Más información → Ejecutar de todas formas** y continúa con la instalación.
3. Abre **TECKIO ADC Lab** desde el menú Inicio.
4. Conecta la TECKIO PIC18K al equipo mediante USB, selecciona su puerto **COM** en la aplicación y pulsa **Conectar**.

Compatible con **Windows 10 (versión 1809 o posterior) y Windows 11, x64**. El instalador incluye lo necesario para ejecutar la aplicación; no se requiere instalar .NET por separado.

También puedes consultar las [versiones publicadas](https://github.com/Dar-cpu/teckio-adc/releases).

## Descargar, compilar y cargar el firmware

El firmware se proporciona como código fuente **C/H** para compilarlo localmente con **MPLAB X IDE** y el compilador **XC8**. No se distribuye un archivo HEX precompilado.

Puedes descargar únicamente las fuentes desde [la última versión publicada](https://github.com/Dar-cpu/teckio-adc/releases/latest/download/TECKIO-AD7195-firmware-sources.zip) o clonar todo el repositorio:

```bash
git clone https://github.com/Dar-cpu/teckio-adc.git
```

Para compilarlo en MPLAB X:

1. Crea un proyecto **Standalone Project** y selecciona el microcontrolador **PIC18F45K50**.
2. Selecciona el compilador **XC8**.
3. Añade los siguientes archivos desde `ad7195/firmware/`:
   - `main.c`, `board.c`, `ad7195.c` y `protocol.c`.
   - `usb/usb.c`, `usb/usb_app.c`, `usb/usb_cdc_acm.c` y `usb/usb_descriptors.c`.
4. Añade `ad7195/firmware/` y `ad7195/firmware/usb/` a las rutas de inclusión, con sus archivos de cabecera `.h`.
5. Compila el proyecto y programa el **HEX generado** en la **TECKIO PIC18K** utilizando tu programador mediante **ICSP**.

El firmware funciona como aplicación independiente, **sin bootloader ni desplazamiento de memoria**. Una vez programado, conecta el USB de la tarjeta y utiliza TECKIO ADC Lab para comunicarte con el AD7195.

## Estructura del repositorio

```text
ad7195/
  firmware/             Firmware PIC18F45K50 y comunicación USB
  host/                 Perfil de registros y configuración del AD7195
  docs/                 Documentación técnica del dispositivo y protocolo
src/
  Teckio.Adc.Core/      Comunicación, adquisición y procesamiento de datos
  Teckio.AdcLab/        Interfaz de escritorio para Windows
installer/               Proyecto del instalador de Windows
tests/                   Pruebas del software
img/                     Recursos gráficos de TECKIO
docs/                    Notas de versiones
.github/workflows/       Automatización de compilación y publicación
```

## Conexiones TECKIO PIC18K ↔ AD7195

Correspondencia de señales utilizada por el firmware. La numeración del AD7195 corresponde al encapsulado **LFCSP de 32 pines**.

| Pin TECKIO PIC18K (PIC18F45K50) | Pin AD7195 | Señal / función |
|---|---:|---|
| RD0 | 32 | CS (selección SPI) |
| RD1 | 31 | SCLK |
| RD2 | 28 | DIN |
| RD3 | 27 | DOUT/RDY |
| RD4 | 25 | SYNC |
| RA4/T0CKI | 30 | MCLK2 (monitor de reloj opcional) |
| RC7 | 20 | BPDSW (monitor de estado) |
| RD6 | 3 | ACX1 |
| RD7 | 4 | /ACX1 |
| RE0 | 1 | ACX2 |
| RE1 | 2 | /ACX2 |
| RA0 | 9 | AIN1 |
| RA1 | 10 | AIN2 |
| RA2 | 15 | AIN3 |
| RA3 | 16 | AIN4 |
| RB1 | 8 | AINCOM |
| RB2 | 17 | REFIN+ |
| RB3 | 18 | REFIN− |
| RB0 | — | Entrada auxiliar para botón del montaje |

La comunicación con el PC utiliza el **USB nativo de la TECKIO PIC18K**. Los pines **RC4 y RC5** se reservan para USB y no forman parte de las conexiones GPIO con el AD7195.

Las líneas AIN y REFIN conectadas a GPIO pueden configurarse desde la aplicación como estímulos de diagnóstico o mantenerse como entradas de alta impedancia para medición externa.
