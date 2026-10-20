#ifndef BOARD_H
#define BOARD_H
#include <xc.h>
#include <stdint.h>
#include <stdbool.h>
#define _XTAL_FREQ 48000000UL
#define AD_CS LATDbits.LATD0
#define AD_SCLK LATDbits.LATD1
#define AD_DIN LATDbits.LATD2
#define AD_DOUT PORTDbits.RD3
#define AD_SYNC LATDbits.LATD4
void board_init(void);
uint32_t board_ticks(void); /* 3 MHz, wraps at ~23.86 minutes */
uint8_t board_pins(void);
void board_stimulus(uint8_t direction, uint8_t value);
#endif
