/* TECKIO ADC Lab. Standalone PIC18F45K50 firmware, no bootloader offset. */
#include "board.h"
#include "ad7195.h"
#include "protocol.h"
#include "usb.h"
#include "usb_cdc.h"
#include <string.h>
#define RING 64u
static uint8_t samples[RING][12],head,tail,count;
static uint8_t rx[64],rx_used,reply[64],tx[64];
static bool rx_ready,tx_free=true,reply_ready,streaming,single,configured_before;
static uint32_t index_next,dropped,crc_errors,last_flush,last_sample,single_start;
static uint16_t stream_seq;
static uint8_t stim_direction,stim_value;
static void respond(uint8_t op,uint16_t seq,uint8_t error,uint8_t n) {
    reply[8]=error;tk_frame(reply,(uint8_t)(op|0x80),seq,(uint8_t)(n+1));reply_ready=true;
}
static void command(void) {
    uint8_t op=rx[3],n=rx[6],*p=rx+8,reg,error=0,len=0,i,lo,hi;
    uint16_t seq=(uint16_t)rx[4]|((uint16_t)rx[5]<<8);
    uint32_t value,m,c;
    if(streaming||single) {
        if(op!=1&&op!=0x21&&op!=0x41&&op!=0x42) {respond(op,seq,3,0);return;}
    }
    switch(op) {
    case 1:
        if(n) {error=2;break;}
        /* protocol, firmware semver, ADC ID, tick Hz, ring depth, max batch */
        reply[9]=1;reply[10]=1;reply[11]=0;reply[12]=0;
        reply[13]=(uint8_t)ad_read(4);tk_put32(reply+14,3000000UL);
        reply[18]=RING;reply[19]=4;len=11;break;
    case 2:
        if(n) {error=2;break;}
        for(i=0;i<8;i++) tk_put32(reply+9+4*i,ad_read(i));
        reply[41]=board_pins();reply[42]=stim_direction;reply[43]=stim_value;len=35;break;
    case 0x10:
        if(n!=1||p[0]>7) {error=2;break;}
        reg=p[0];reply[9]=reg;tk_put32(reply+10,ad_read(reg));len=5;break;
    case 0x11:
        if(n!=5||p[0]>7||p[0]==0||p[0]==3||p[0]==4) {error=2;break;}
        reg=p[0];value=tk_u32(p+1);
        if(value>0xFFFFFFUL||(reg==5&&(value&~0x40UL))) {error=2;break;}
        m=reg==1?value:ad_mode;c=reg==2?value:ad_config;
        if(reg==1||reg==2) {error=ad_validate(m,c);if(error)break;}
        if(reg>=6) {
            uint8_t channels=(uint8_t)(ad_config>>8);
            if(!channels||(channels&(channels-1))||(channels&4)) {error=2;break;}
            ad_write(1,(ad_mode&0x1FFFFFUL)|0x400000UL);
        }
        ad_write(reg,value);
        m=ad_read(reg);tk_put32(reply+9,m);len=4;
        if(m!=value)error=4;
        break;
    case 0x12:
        if(n!=8) {error=2;break;}
        m=tk_u32(p)|0x100000UL;c=tk_u32(p+4);
        if((m>>21)!=0) {error=2;break;}
        error=ad_validate(m,c);if(error)break;
        ad_write(1,(m&0x1FFFFFUL)|0x400000UL);
        ad_write(2,c);ad_write(1,m);
        if(ad_read(1)!=m||ad_read(2)!=c)error=4;
        break;
    case 0x20: case 0x22:
        if(n) {error=2;break;}
        error=ad_validate(ad_mode,ad_config);if(error)break;
        head=tail=count=0;index_next=dropped=0;stream_seq=0;
        last_flush=last_sample=single_start=board_ticks();
        ad_write(1,(ad_mode&0x1FFFFFUL)|0x100000UL|(op==0x22?0x200000UL:0));
        streaming=op==0x20;single=op==0x22;break;
    case 0x21:
        if(n) {error=2;break;}
        streaming=single=false;ad_write(1,(ad_mode&0x1FFFFFUL)|0x400000UL);break;
    case 0x23:
        if(n) {error=2;break;}
        AD_SYNC=0;__delay_us(2);AD_SYNC=1;break;
    case 0x24:
        if(n) {error=2;break;}
        ad_reset();ad_write(2,0x000140UL);ad_write(1,0x180060UL);break;
    case 0x30:
        if(n!=1||p[0]<4||p[0]>7) {error=2;break;}
        reg=(uint8_t)(ad_config>>8);
        if(!reg||(reg&(reg-1))||(reg&4)) {error=2;break;}
        if(p[0]==5&&(ad_config&7)==0) {error=2;break;} /* gain1 factory fullscale */
        ad_write(1,(ad_mode&0x1FFFFFUL)|((uint32_t)p[0]<<21));break;
    case 0x40:
        if(n!=2||(p[0]&0x80)||(p[1]&0x80)) {error=2;break;}
        stim_direction=p[0];stim_value=p[1];board_stimulus(p[0],p[1]);break;
    case 0x41:
        if(n) {error=2;break;}
        reply[9]=board_pins();reply[10]=stim_direction;reply[11]=stim_value;
        lo=TMR0L;hi=TMR0H;reply[12]=lo;reply[13]=hi;
        tk_put32(reply+14,board_ticks());reply[18]=ACTCON;len=10;break;
    case 0x42:
        if(n) {error=2;break;}
        tk_put32(reply+9,index_next);tk_put32(reply+13,dropped);
        tk_put32(reply+17,crc_errors);tk_put32(reply+21,board_ticks());
        reply[25]=count;reply[26]=streaming;reply[27]=single;len=19;break;
    default:error=1;break;
    }
    respond(op,seq,error,len);
}
static void feed(uint8_t b) {
    uint8_t i;
    if(rx_used==0&&b!='T')return;
    if(rx_used==1&&b!='K') {rx_used=b=='T'?1:0;return;}
    rx[rx_used++]=b;
    if(rx_used==64) {
        if(tk_valid(rx)){command();rx_used=0;}
        else {
            ++crc_errors;
            /* Resynchronise without trusting lengths in a damaged packet. */
            for(i=1;i<63;i++) if(rx[i]=='T'&&rx[i+1]=='K')break;
            if(i<63) {rx_used=(uint8_t)(64-i);memmove(rx,rx+i,rx_used);}
            else {rx_used=rx[63]=='T'?1:0;rx[0]='T';}
        }
    }
}
static void send(const uint8_t *p) {
    memcpy(g_cdc_dat_ep_in,p,64);tx_free=false;cdc_arm_data_ep_in(64);
}
static void acquire(uint32_t now) {
    uint8_t data[4];
    if(!(streaming||single)||!ad_ready())return;
    ad_sample(data);
    if(count<RING) {
        tk_put32(samples[head],index_next);tk_put32(samples[head]+4,now);
        memcpy(samples[head]+8,data,4);head=(head+1)&(RING-1);++count;
    } else ++dropped;
    ++index_next;last_sample=now;single=false;
}
void acquisition_service(void) { acquire(board_ticks()); }
void main(void) {
    uint8_t i,n;uint32_t now;
    board_init();ad_reset();ad_write(2,0x000140UL);ad_write(1,0x180060UL);
    usb_init();INTCONbits.GIE=0;INTCONbits.PEIE=0;USB_INTERRUPT_ENABLE=0;
    for(;;) {
        now=board_ticks();usb_tasks();
        if(usb_get_state()<STATE_CONFIGURED) {
            if(configured_before) {
                streaming=single=false;rx_ready=reply_ready=false;rx_used=count=0;tx_free=true;
                stim_direction=stim_value=0;board_stimulus(0,0);
                configured_before=false;
            }
            continue;
        }
        configured_before=true;
        acquire(now);
        if(single&&(uint32_t)(now-single_start)>15000000UL) single=false;
        if(rx_ready&&!reply_ready) {
            n=g_cdc_num_data_out;
            for(i=0;i<n;i++)feed(g_cdc_dat_ep_out[i]);
            rx_ready=false;cdc_arm_data_ep_out();
        }
        if(!tx_free)continue;
        if(reply_ready) {send(reply);reply_ready=false;continue;}
        if(count&&(count>=4||!streaming||(uint32_t)(now-last_flush)>6000UL)) {
            n=count>4?4:count;tk_put32(tx+8,dropped);
            for(i=0;i<n;i++) {
                memcpy(tx+12+12*i,samples[tail],12);tail=(tail+1)&(RING-1);--count;
            }
            tk_frame(tx,0x70,stream_seq++,(uint8_t)(4+12*n));
            send(tx);last_flush=now;
        }
    }
}
void cdc_set_control_line_state(void) {
    if(!g_cdc_set_control_line_state.DTR_bit) {
        streaming=single=false;count=0;
        stim_direction=stim_value=0;board_stimulus(0,0);
    }
}
void cdc_set_line_coding(void) {}
void cdc_data_out(void) {rx_ready=true;}
void cdc_data_in(void) {tx_free=true;}
void cdc_notification(void) {}
