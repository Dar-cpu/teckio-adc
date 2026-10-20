# TECKIO ADC binary protocol v1

USB CDC byte stream. La aplicación emite un comando por vez y espera respuesta. Todas las tramas miden **64 bytes**, incluso cuando el payload es menor. Datos multi-byte en **little endian**, incluido el código ADC transportado (el bus SPI es MSB-first).

| Offset | Bytes | Campo |
|---|---:|---|
| 0 | 2 | 54 4B, ASCII TK |
| 2 | 1 | Versión 1 |
| 3 | 1 | Opcode |
| 4 | 2 | Secuencia |
| 6 | 1 | Longitud payload 0..54 |
| 7 | 1 | Reservado, cero |
| 8 | 54 | Payload y padding cero |
| 62 | 2 | CRC16/CCITT-FALSE de bytes 0..61, poly1021, initFFFF |

Respuesta: opcode OR 0x80, misma secuencia. Primer byte del payload: 0 OK, 1 comando desconocido, 2 parámetro inválido, 3 ocupado, 4 verificación no coincide. Resto según tabla.

| Op | Comando | Payload petición | Respuesta tras byte resultado |
|---|---|---|---|
| 01 | HELLO | vacío | protocolo u8, FW major/minor/patch u8×3, ID u8, tick_hz u32, ring u8, batch u8 |
| 02 | SNAPSHOT | vacío | 8 registros u32, pines u8, dirección estímulos u8, niveles u8 |
| 10 | READ | registro u8 | registro u8 + valor u32 |
| 11 | WRITE | registro u8 + valor u32 | readback u32; también se incluye si resultado=4 |
| 12 | CONFIGURE | MODE u32 + CONFIG u32 | vacío; fuerza DAT_STA y verifica |
| 20 | START | vacío | vacío; reinicia índice/drops y convierte continuamente |
| 21 | STOP | vacío | vacío; el resto del ring sale después del ACK |
| 22 | SINGLE | vacío | ACK, luego muestra 70; timeout de adquisición 5 s |
| 23 | SYNC | vacío | vacío |
| 24 | RESET | vacío | vacío; reset SPI y configuración base |
| 30 | CALIBRATE | modo 4..7 u8 | ACK inmediato; consultar STATUS.RDY y coeficientes al finalizar |
| 40 | STIMULUS | dirección u8, nivel u8 | vacío; bit=1 habilita salida |
| 41 | PINS | vacío | pines u8, dir u8, nivel u8, Timer0 u16, ticks u32, ACTCON u8 |
| 42 | STATS | vacío | lecturas u32, drops u32, CRC_rx u32, ticks u32, ring_count u8, streaming u8, single u8 |

Durante adquisición sólo se permiten HELLO, STOP, PINS y STATS. WRITE valida registros escribibles, bits reservados y combinaciones de configuración. Coeficientes requieren un único canal válido; se escriben en idle.

## Streaming 0x70

Payload: drops acumulados u32 + 1..4 muestras de 12 bytes:

| Offset muestra | Campo |
|---|---|
| 0..3 | Índice u32 (de cada lectura completada, incluso si luego se descarta por ring lleno) |
| 4..7 | Timer1 extendido u32, 3 MHz, rollover ~23.86 min |
| 8..10 | Código de 24 bits, little endian |
| 11 | STATUS adjunto por DAT_STA |

STATUS: RDY bit7, ERR6, NOREF5, PARITY4, canal bits2..0. Paridad sólo se evalúa si ENPAR estaba activo en MODE. DAT_STA evita asociar el canal equivocado durante secuencias.

Los tiempos son de observación/lectura por firmware, no timestamps internos del ADC. La PC extiende rollovers por diferencias unsigned. La adquisición debe mantenerse activa sin pausas mayores que un rollover.

CRC con tabla en flash; su procesamiento intercala servicio de adquisición. El ring es 64 entradas, 768 bytes. Los lotes parciales se vacían después de ~2 ms. No hay conversión flotante ni texto durante adquisición.

## Diagnóstico de pines

PINS bits0..7: RDY, BPDSW, ACX1, /ACX1, ACX2, /ACX2, RB0, RA4/MCLK2.

STIMULUS bits0..6: AIN1, AIN2, AIN3, AIN4, AINCOM, REF+, REF−. Cambiar LAT antes de activar TRIS evita pulsos transitorios.

La prueba de GPOCON admite el error de readback para **mostrarlo**: no interpreta un 0x46 como 0x40 correcto; informa los bits extra 0x06 por separado.
