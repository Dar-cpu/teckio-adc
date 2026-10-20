#ifndef AD7195_H
#define AD7195_H
#include "board.h"
extern uint32_t ad_mode,ad_config;
uint8_t ad_spi(uint8_t value);
void ad_reset(void);
uint32_t ad_read(uint8_t reg);
void ad_write(uint8_t reg,uint32_t value);
bool ad_ready(void);
void ad_sample(uint8_t *raw_status);
uint8_t ad_validate(uint32_t mode,uint32_t conf);
#endif
