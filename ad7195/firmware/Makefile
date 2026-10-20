XC8 ?= xc8-cc
DFP ?=
SOURCES = main.c board.c ad7195.c protocol.c usb/usb.c usb/usb_app.c usb/usb_cdc_acm.c usb/usb_descriptors.c
DFPFLAG = $(if $(DFP),-mdfp="$(DFP)",)
.PHONY: all
all:
	mkdir -p build
	$(XC8) -mcpu=18F45K50 $(DFPFLAG) -O2 -std=c99 -I. -Iusb $(SOURCES) -o build/teckio-ad7195.elf
