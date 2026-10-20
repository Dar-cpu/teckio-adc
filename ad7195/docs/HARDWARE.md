# Conexiones de TECKIO PIC18K

Basado en el probador AD7195 existente del usuario; PIC18F45K50 TQFP44, sin bootloader. No se reutilizan RC4/RC5 para GPIO: pertenecen al USB nativo.

| PIC | AD7195 LFCSP32 | Función |
|---|---|---|
| RD0 | 32 | /CS |
| RD1 | 31 | SCLK, reposo alto |
| RD2 | 28 | DIN |
| RD3 | 27 | DOUT/RDY |
| RD4 | 25 | SYNC |
| RA4/T0CKI | 30 | MCLK2, conteo opcional |
| RC7 | 20 | BPDSW, entrada de monitor con pull-up externo |
| RD6, RD7 | 3, 4 | ACX1, /ACX1 |
| RE0, RE1 | 1, 2 | ACX2, /ACX2 |
| RA0..RA3 | 9, 10, 15, 16 | AIN1..AIN4, estímulos opcionales |
| RB1 | 8 | AINCOM, estímulo opcional |
| RB2, RB3 | 17, 18 | REFIN+, REFIN−, estímulos opcionales |
| RB0 | — | Monitor del botón del fixture |

AVDD/DVDD y tierras conservan el montaje previo. Los GPIO de estímulo se inicializan como entradas y se liberan al cerrar DTR/desconectar USB. La app permite habilitarlos individualmente como salida alta/baja. No se utiliza la LCD I2C: RC0/RC1 permanecen entradas.

**Medición normal:** fuente analógica y referencia externa; GPIO de estímulos en Hi-Z.

**Fixture original:** Diagnóstico → Fixture original habilita dirección 0x7F, nivel 0x20: AIN1..4 y AINCOM a bajo, REFIN+ alto, REFIN− bajo. Indica en la app la tensión real entre REFIN+/REFIN−. Una referencia producida por GPIO es útil para conectividad, no un patrón de calibración.

## Relojes

CPU/USB: HFINTOSC 16 MHz, PLL ×3, Fosc 48 MHz, CPUDIV sin división, ACT con referencia SOF. El cristal de 8 MHz instalado no participa. Timer1 usa Fosc/4 dividido por 4 = 3 MHz. Timer0 cuenta flancos externos en RA4, prescaler 256.

ADC: reloj interno por defecto. La prueba MCLK2 selecciona temporalmente salida del oscilador interno y restaura MODE. Los otros modos de reloj requieren el reloj/cristal del ADC correspondiente.

## USB

Se incluye el stack CDC utilizado por el usuario (USB uC, John Izzard, MIT). Descriptor bus-powered, datos EP2 64 bytes, control EP0 8 bytes. Mantiene VID/PID de demostración 04D8:000A del stack previo; **no son una asignación exclusiva de TECKIO**. Una distribución comercial necesita el VID/PID autorizado correspondiente. El protocolo HELLO valida el dispositivo después de seleccionar COM.

No UART USB externa. La velocidad COM 115200 es el line coding CDC, no el límite del enlace USB. El firmware no escribe memoria de programa y no implementa actualización USB del PIC.

Fuentes:
- https://www.analog.com/media/en/technical-documentation/data-sheets/ad7195.pdf
- https://www.microchip.com/content/dam/mchp/documents/OTH/ProductDocuments/DataSheets/PIC18F2X_45K50-30000684B.pdf
