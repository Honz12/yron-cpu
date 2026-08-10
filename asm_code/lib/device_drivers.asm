%include "malloc.asm"

_value_display_device_buffer:
    %dword 0

device_init_interrupt:
    ; $03 - Device ID
    ; $04 - Init completed
    ; $05 - Mem needed
    ; $06 - Mem allocated address

    mov $10, $05
    call lib_malloc
    mov $06, $11

    ldib $04, 1

    ret
