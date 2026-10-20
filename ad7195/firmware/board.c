#include "board.h"
#pragma config PLLSEL=PLL3X, CFGPLLEN=OFF, CPUDIV=NOCLKDIV, LS48MHZ=SYS48X8
#pragma config FOSC=INTOSCIO, PCLKEN=OFF, FCMEN=OFF, IESO=OFF
#pragma config nPWRTEN=OFF, BOREN=SBORDIS, nLPBOR=ON, WDTEN=OFF
#pragma config MCLRE=ON, PBADEN=OFF, CCP2MX=RC1, T3CMX=RC0, SDOMX=RC7
#pragma config STVREN=ON, LVP=ON, ICPRT=ON, XINST=OFF
#pragma config CP0=OFF, CP1=OFF, CP2=OFF, CP3=OFF, CPB=OFF, CPD=OFF
#pragma config WRT0=OFF, WRT1=OFF, WRT2=OFF, WRT3=OFF, WRTC=OFF, WRTB=OFF, WRTD=OFF
#pragma config EBTR0=OFF, EBTR1=OFF, EBTR2=OFF, EBTR3=OFF, EBTRB=OFF
static uint16_t tick_high;
void board_init(void) {
    OSCTUNE=0x80; OSCCON=0x70; OSCCON2=0x10;
    while (!OSCCON2bits.PLLRDY) {}
    ACTCON=0x90;
    ANSELA=ANSELB=ANSELC=ANSELD=ANSELE=0;
    TRISA=TRISB=TRISC=TRISD=TRISE=0xFF;
    LATA=LATB=LATC=LATE=0;
    LATD=0x17; TRISD=0xE8; /* CS/SCLK/DIN/SYNC outputs; all stimuli high-Z */
    T1CON=0x22; TMR1H=0; TMR1L=0; PIR1bits.TMR1IF=0;
    T1CONbits.TMR1ON=1; /* Fosc/4, 1:4, RD16 */
    T0CON=0x27; TMR0H=0; TMR0L=0; T0CONbits.TMR0ON=1;
}
uint32_t board_ticks(void) {
    uint8_t lo,hi;
    if(PIR1bits.TMR1IF) { PIR1bits.TMR1IF=0; ++tick_high; }
    lo=TMR1L; hi=TMR1H;
    if(PIR1bits.TMR1IF) {
        PIR1bits.TMR1IF=0; ++tick_high; lo=TMR1L; hi=TMR1H;
    }
    return ((uint32_t)tick_high<<16)|((uint16_t)hi<<8)|lo;
}
uint8_t board_pins(void) {
    return (uint8_t)((AD_DOUT?1:0)|(PORTCbits.RC7?2:0)|
      (PORTDbits.RD6?4:0)|(PORTDbits.RD7?8:0)|
      (PORTEbits.RE0?16:0)|(PORTEbits.RE1?32:0)|
      (PORTBbits.RB0?64:0)|(PORTAbits.RA4?128:0));
}
void board_stimulus(uint8_t direction, uint8_t value) {
    /* bits 0..3 AIN1..4, 4 AINCOM, 5 REF+, 6 REF-. 1=output. */
    TRISA |= 0x0F; TRISB |= 0x0E;
    LATA=(LATA&0xF0)|(value&0x0F);
    LATB=(LATB&0xF1)|((value>>3)&0x0E);
    TRISA=(TRISA&0xF0)|((~direction)&0x0F);
    TRISB=(TRISB&0xF1)|(((~direction)>>3)&0x0E);
}
