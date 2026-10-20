#ifndef PROTOCOL_H
#define PROTOCOL_H
#include <stdint.h>
#define TK_FRAME_SIZE 64
#define TK_PAYLOAD_SIZE 54
uint16_t tk_crc(const uint8_t *p,uint8_t n);
uint32_t tk_u32(const uint8_t *p);
void tk_put32(uint8_t *p,uint32_t v);
void tk_frame(uint8_t *frame,uint8_t op,uint16_t seq,uint8_t len);
uint8_t tk_valid(const uint8_t *frame);
#endif
