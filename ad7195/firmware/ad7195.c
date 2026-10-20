#include "ad7195.h"
uint32_t ad_mode=0x180060UL,ad_config=0x000140UL;
static const uint8_t widths[8]={1,3,3,3,1,1,3,3};
uint8_t ad_spi(uint8_t v) {
    uint8_t r=0;
    /* Sample before rising edge: DOUT can revert to RDY at final rising edge. */
#define BIT() if(v&0x80u)AD_DIN=1;else AD_DIN=0; NOP(); AD_SCLK=0; NOP(); r<<=1; if(AD_DOUT)r|=1; AD_SCLK=1; v<<=1
    BIT(); BIT(); BIT(); BIT(); BIT(); BIT(); BIT(); BIT();
#undef BIT
    return r;
}
void ad_reset(void) {
    uint8_t i; AD_CS=0;
    for(i=0;i<8;++i) ad_spi(0xFF);
    AD_CS=1; AD_DIN=1; __delay_ms(1);
    ad_mode=0x080060UL; ad_config=0x000117UL;
}
uint32_t ad_read(uint8_t reg) {
    uint8_t i; uint32_t v=0;
    AD_CS=0; ad_spi((uint8_t)(0x40|(reg<<3)));
    for(i=0;i<widths[reg];++i) v=(v<<8)|ad_spi(0);
    if(reg==3 && (ad_mode&0x100000UL)) ad_spi(0);
    AD_CS=1; AD_DIN=1; return v;
}
void ad_write(uint8_t reg,uint32_t v) {
    uint8_t i;
    AD_CS=0; ad_spi((uint8_t)(reg<<3));
    for(i=widths[reg];i>0;--i) ad_spi((uint8_t)(v>>((i-1)*8)));
    AD_CS=1; AD_DIN=1;
    if(reg==1) ad_mode=v;
    if(reg==2) ad_config=v;
}
bool ad_ready(void) {
    bool ready; AD_CS=0; NOP(); NOP(); ready=!AD_DOUT;
    AD_CS=1; return ready;
}
void ad_sample(uint8_t *p) {
    AD_CS=0; ad_spi(0x58);
    p[2]=ad_spi(0); p[1]=ad_spi(0); p[0]=ad_spi(0); p[3]=ad_spi(0);
    AD_CS=1; AD_DIN=1;
}
uint8_t ad_validate(uint32_t m,uint32_t c) {
    uint8_t gain=(uint8_t)(c&7);
    if((m&~0xFCAFFFUL)!=0) return 2;
    if(!(m&0x3FF)||!(c&0xFF00UL)||(c&~0xC0FFDFUL)) return 2;
    if(gain==1||gain==2) return 2;
    if((c&0x400000UL)&&!(c&0x800000UL)) return 2;
    if((c&0x80)&&(!(c&0x10)||(c&0x800000UL))) return 2;
    return 0;
}
