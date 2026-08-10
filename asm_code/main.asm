%org 0x0000                     ; boot section

jmp main

%include "lib/.asm"

%org 0x0200                     ; interrupt table section

%dword 0
%dword device_init_interrupt

%org 0x0400                     ; code section

main:
    jmp halt

halt:
    jmp halt
