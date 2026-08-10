.org 0x0000                     ; boot section

jmp main

.include "lib/.asm"

.org 0x0200                     ; interrupt table section

.dword error_interrupt
.dword device_init_interrupt

.org 0x0400                     ; code section

error_interrupt:
    ret

device_init_interrupt:
    ; $03 - Device ID
    ; $04 - Init completed
    ; $05 - Mem needed
    ; $06 - Mem allocated address

    mov $10, $05
    call lib_malloc
    mov $06, $11

    ret

main:
    ; initialize lib_malloc
    ldid $10, free_ram
    call lib_init_malloc

    jmp halt

halt:
    jmp halt

free_ram:
