_value_lib_malloc_pointer:
    %dword 0x00010000
    
;
; Initializes the lib_malloc pointer
; Parameters:
;   $10 - memory size
; Returns:
;   $11 - allocated memory address
;
lib_malloc:
    push $12, DWORD                             ; push regs we modify
    push $13, DWORD

    ldid $12, _value_lib_malloc_pointer         ; load pointer address

    ldd $11, $12                                ; load pointer / set return
    add $11, $10, $13                           ; increment pointer

    std $13, $12                                ; write new pointer

    pop $13, DWORD                              ; pop what we pushed
    pop $12, DWORD

    ret
