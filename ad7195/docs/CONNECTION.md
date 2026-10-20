# Conexión USB CDC: corrección 1.0.1

## Evidencia de la prueba anterior

Windows enumeraba COM20 con `USB\VID_04D8&PID_000A`. El HELLO de 64 bytes comenzaba con `54 4B 01 01 01 00` y terminaba con `F0 6F`: su CRC es correcto. Abrir el puerto y ver su COM confirma enumeración, pero no confirma que las respuestas de EP2 IN lleguen a la aplicación. El cambio previo de banderas a `volatile` no había resuelto el timeout.

## Cambio concreto

El firmware anterior armaba un paquete IN de exactamente 64 bytes (el máximo del endpoint) y marcaba el buffer libre al completarlo. Faltaba un paquete de longitud cero, ZLP, para delimitar la transferencia bulk. Windows puede mantener pendiente su lectura esperando ese final. Las respuestas cortas del bootloader usado como referencia no tenían este caso límite.

Ahora el estado es `TX_IDLE → TX_DATA → TX_ZLP → TX_IDLE`. `cdc_data_in()` arma `cdc_arm_data_ep_in(0)` después de completar los datos y sólo libera el buffer después del ZLP. El paquete vacío no agrega bytes al protocolo ni cambia su CRC. `SET_CONFIGURATION` reinicia el estado junto con los endpoints. Se mantienen las banderas compartidas `volatile`.

Referencia del comportamiento bulk: [Microsoft — USB bandwidth allocation, short packets and zero-length packets](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/usb-bandwidth-allocation).

Es una omisión comprobable en el código y consistente con el síntoma. No equivale a una prueba física del arreglo: el firmware 1.0.1 se entrega sin compilar, como solicitaste.

## Probar desde la app

1. Compila todos los C/H actuales, incluidos `usb/usb_app.c` y `usb/usb_app.h`; programa la PIC sin offset de bootloader.
2. Instala `Teckio.AdcLab.Setup.exe`. Cierra terminales/PowerShell que puedan tener abierto el COM.
3. Selecciona tu COM actual (puede cambiar tras reprogramar) y conecta. Debe aparecer **FW 1.0.1** e ID del ADC. La conexión no depende de que el ID del ADC sea 0xA6: un ID distinto se registra para diagnóstico.
4. Si falla, copia el mensaje y la consola. `RX=0` indica que no llegó ningún byte; RX positivo con CRC inválidos apunta al contenido/tramas. La app cierra el puerto tras fallar, permitiendo reintentar.
5. Desconecta/conecta cinco veces, lee registros y prueba captura breve. Después valida las tasas crecientes descritas en VALIDATION.md.

## Prueba independiente en PowerShell

Cierra ADC Lab antes de ejecutar el bloque completo. Cambia `COM20` por el puerto actual. El `finally` está en el mismo bloque y libera el COM incluso si hay timeout.

```powershell
& {
    $port = [System.IO.Ports.SerialPort]::new('COM20', 115200)
    $port.DtrEnable = $true
    $port.Handshake = [System.IO.Ports.Handshake]::None
    $port.ReadTimeout = 3000
    $port.WriteTimeout = 1000
    try {
        $port.Open()
        $port.DiscardInBuffer()
        Start-Sleep -Milliseconds 150
        [byte[]]$hello = New-Object byte[] 64
        $hello[0]=0x54; $hello[1]=0x4B; $hello[2]=1; $hello[3]=1; $hello[4]=1
        $hello[62]=0xF0; $hello[63]=0x6F
        $port.Write($hello, 0, 64)
        [byte[]]$reply = New-Object byte[] 64
        $received = 0
        while ($received -lt 64) {
            $received += $port.Read($reply, $received, 64 - $received)
        }
        [BitConverter]::ToString($reply)
        # Expected prefix: 54-4B-01-81-01-00-0C-00-00-01-01-00-01
        # Byte 13 = ADC ID; firmware fields at 10..12 = 1.0.1.
    } finally {
        if ($port.IsOpen) { $port.Close() }
        $port.Dispose()
    }
}
```

Si el puerto sigue indicando acceso denegado por la prueba anterior, cierra aquella ventana PowerShell para liberar su objeto SerialPort. Evita abrir el mismo COM simultáneamente desde dos programas.
