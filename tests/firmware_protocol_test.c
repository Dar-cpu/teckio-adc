#include <assert.h>
#include <stdio.h>
#include <string.h>
#include "../ad7195/firmware/protocol.h"
int main(void) {
    uint8_t f[64]={0};
    assert(tk_crc((const uint8_t*)"123456789",9)==0x29B1);
    tk_put32(f+8,0x12345678UL);tk_frame(f,0x12,0xBEEF,4);
    assert(tk_valid(f));assert(tk_u32(f+8)==0x12345678UL);
    assert(f[4]==0xEF&&f[5]==0xBE);
    f[8]^=1;assert(!tk_valid(f));
    puts("PASS firmware CRC / packing / validation");
    return 0;
}
